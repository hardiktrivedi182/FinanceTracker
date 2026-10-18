using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.ImportBatches.Services;
using FinanceTracker.Application.Statements.Dtos;
using FinanceTracker.Application.Statements.Parsing;
using FinanceTracker.Application.Statements.Persistence;
using FinanceTracker.Application.Statements.Storage;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Statements.Services;

public class StatementService : IStatementService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB limit
    private readonly IStatementRepository _statementRepository;
    private readonly IStatementStorage _statementStorage;
    private readonly ICsvStatementParser _csvParser;
    private readonly IHouseholdContext _householdContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly IImportBatchService? _batchService;

    public StatementService(
        IStatementRepository statementRepository,
        IStatementStorage statementStorage,
        ICsvStatementParser csvParser,
        IHouseholdContext householdContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        IImportBatchService? batchService = null)
    {
        _statementRepository = statementRepository;
        _statementStorage = statementStorage;
        _csvParser = csvParser;
        _householdContext = householdContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _batchService = batchService;
    }

    public async Task<StatementUploadResultDto> UploadAndParseAsync(
        string originalFileName,
        long fileSizeBytes,
        Stream fileContentStream,
        Guid? accountId = null,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            throw new UnauthorizedAccessException("Authentication is required to upload statements.");
        }

        if (!_householdContext.HasHousehold)
        {
            throw new InvalidOperationException("Current user is not associated with an active household.");
        }

        var householdId = _householdContext.HouseholdId;
        var uploaderId = _currentUser.UserId;

        // 1. Validation: Size
        if (fileSizeBytes <= 0)
        {
            throw new ArgumentException("Uploaded file is empty.", nameof(fileSizeBytes));
        }

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            throw new ArgumentException($"File size exceeds the 10 MB maximum limit (received {fileSizeBytes / (1024 * 1024.0):F2} MB).", nameof(fileSizeBytes));
        }

        // 2. Validation: Extension
        var extension = Path.GetExtension(originalFileName);
        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Unsupported file type. Only CSV files are supported in this release.", nameof(originalFileName));
        }

        // 3. Validation: Content inspection (reject disguised binaries / PDFs / ZIPs)
        ValidateCsvContent(fileContentStream);

        // 4. Generate internal storage key and Statement ID
        var statementId = Guid.NewGuid();
        var sanitizedFileName = Statement.SanitizeFileName(originalFileName);
        var now = _timeProvider.GetUtcNow();

        string? storageKey = null;

        try
        {
            // Store original binary in private statement storage
            storageKey = await _statementStorage.SaveAsync(householdId, statementId, fileContentStream, ".csv", ct);

            // Create domain Statement entity
            var statement = new Statement(
                statementId,
                householdId,
                uploaderId,
                sanitizedFileName,
                StatementFormat.Csv,
                fileSizeBytes,
                storageKey,
                now);

            // 5. Parse isolated CSV content
            statement.MarkParsing();
            if (fileContentStream.CanSeek)
            {
                fileContentStream.Seek(0, SeekOrigin.Begin);
            }

            var parseResult = await _csvParser.ParseAsync(fileContentStream, ct);

            if (parseResult.IsSuccess)
            {
                statement.MarkParsed(parseResult.Rows.Count, parseResult.SummaryJson);
            }
            else
            {
                var firstError = parseResult.Errors.Count > 0 ? parseResult.Errors[0].Message : "CSV parsing failed.";
                statement.MarkFailed(firstError);
            }

            // 6. Persist Statement record to database
            await _statementRepository.AddAsync(statement, ct);

            Guid? importBatchId = null;
            if (accountId.HasValue && _batchService != null && statement.Status == StatementProcessingStatus.Parsed)
            {
                var batchDetail = await _batchService.CreateBatchAsync(statement.Id, accountId.Value, ct);
                importBatchId = batchDetail.Batch.Id;
            }

            // Sample rows: first 10 rows for safe preview
            var sampleRows = parseResult.Rows.Take(10).ToList();

            return new StatementUploadResultDto(
                statement.Id,
                statement.OriginalFileName,
                statement.Status,
                statement.ParsedTransactionCount,
                statement.ErrorMessage,
                sampleRows,
                parseResult.Errors,
                statement.Status == StatementProcessingStatus.Parsed
                    ? $"Statement parsed successfully ({parseResult.Rows.Count} transactions found). 0 transactions imported. Ledger mutation requires explicit import confirmation."
                    : $"Statement processing completed with status '{statement.Status}'. 0 transactions imported.",
                importBatchId);
        }
        catch (Exception)
        {
            // Atomic cleanup: If storage succeeded but database persistence or workflow threw an exception,
            // clean up the stored object to prevent orphaned storage bloat.
            if (!string.IsNullOrWhiteSpace(storageKey))
            {
                try
                {
                    await _statementStorage.DeleteAsync(storageKey, ct);
                }
                catch
                {
                    // Best-effort cleanup
                }
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<StatementSummaryDto>> GetStatementsAsync(CancellationToken ct = default)
    {
        if (!_householdContext.HasHousehold)
        {
            throw new InvalidOperationException("Current user is not associated with an active household.");
        }

        var statements = await _statementRepository.GetByHouseholdIdAsync(_householdContext.HouseholdId, ct);

        return statements.Select(MapToSummaryDto).ToList();
    }

    public async Task<StatementSummaryDto?> GetStatementByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (!_householdContext.HasHousehold)
        {
            throw new InvalidOperationException("Current user is not associated with an active household.");
        }

        var statement = await _statementRepository.GetByIdAsync(id, ct);
        if (statement == null || statement.HouseholdId != _householdContext.HouseholdId)
        {
            return null;
        }

        return MapToSummaryDto(statement);
    }

    private static StatementSummaryDto MapToSummaryDto(Statement s)
    {
        return new StatementSummaryDto(
            s.Id,
            s.HouseholdId,
            s.UploaderUserId,
            s.OriginalFileName,
            s.Format,
            s.FileSizeBytes,
            s.UploadedAt,
            s.Status,
            s.ParsedTransactionCount,
            s.ErrorMessage);
    }

    private static void ValidateCsvContent(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        // Read up to first 512 bytes for magic number & binary inspection
        byte[] buffer = new byte[512];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);

        if (bytesRead == 0)
        {
            throw new ArgumentException("Uploaded file contains zero bytes.", nameof(stream));
        }

        // Check well-known binary magic numbers:
        // PE / Windows Executable: MZ (0x4D, 0x5A)
        if (bytesRead >= 2 && buffer[0] == 0x4D && buffer[1] == 0x5A)
        {
            throw new ArgumentException("Disguised executable file detected.", nameof(stream));
        }

        // ELF Executable: 0x7F, 'E', 'L', 'F'
        if (bytesRead >= 4 && buffer[0] == 0x7F && buffer[1] == 0x45 && buffer[2] == 0x4C && buffer[3] == 0x46)
        {
            throw new ArgumentException("Disguised binary executable detected.", nameof(stream));
        }

        // ZIP / Office OpenXML: PK\x03\x04
        if (bytesRead >= 4 && buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04)
        {
            throw new ArgumentException("Disguised zip or archive file detected.", nameof(stream));
        }

        // PDF: %PDF
        if (bytesRead >= 4 && buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46)
        {
            throw new ArgumentException("Disguised PDF file detected. PDF statements are not supported in this slice.", nameof(stream));
        }

        // PNG: \x89PNG
        if (bytesRead >= 4 && buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
        {
            throw new ArgumentException("Disguised image file detected.", nameof(stream));
        }

        // Check for null bytes (\0) which never occur in valid CSV text files
        for (int i = 0; i < bytesRead; i++)
        {
            if (buffer[i] == 0x00)
            {
                throw new ArgumentException("File contains null bytes; binary files cannot be parsed as CSV.", nameof(stream));
            }
        }

        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }
    }
}
