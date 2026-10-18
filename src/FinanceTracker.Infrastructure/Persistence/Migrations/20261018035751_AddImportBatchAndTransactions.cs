using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportBatchAndTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    household_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    statement_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    account_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    uploaded_by_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    total_row_count = table.Column<int>(type: "int", nullable: false),
                    ready_count = table.Column<int>(type: "int", nullable: false),
                    duplicate_count = table.Column<int>(type: "int", nullable: false),
                    error_count = table.Column<int>(type: "int", nullable: false),
                    selected_row_count = table.Column<int>(type: "int", nullable: false),
                    committed_count = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    committed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    committed_by_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_batches_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_import_batches_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_import_batches_statements_statement_id",
                        column: x => x.statement_id,
                        principalTable: "statements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_staging_rows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    household_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    source_row_number = table.Column<int>(type: "int", nullable: false),
                    raw_data = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    currency_code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    validation_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    validation_errors = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    duplicate_status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    duplicate_reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    matched_transaction_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    is_selected = table.Column<bool>(type: "bit", nullable: false),
                    resolution = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    resolution_notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    resolved_transaction_date = table.Column<DateOnly>(type: "date", nullable: true),
                    resolved_description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    resolved_amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    resolved_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    committed_transaction_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_staging_rows", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_staging_rows_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    household_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    account_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    statement_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    import_batch_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    source_row_number = table.Column<int>(type: "int", nullable: true),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    original_transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    signed_amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    created_by_user_id = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_transactions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transactions_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transactions_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transactions_statements_statement_id",
                        column: x => x.statement_id,
                        principalTable: "statements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_account_id",
                table: "import_batches",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_household_created_at",
                table: "import_batches",
                columns: new[] { "household_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_statement_id",
                table: "import_batches",
                column: "statement_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_staging_rows_batch_row_number",
                table: "import_staging_rows",
                columns: new[] { "import_batch_id", "source_row_number" });

            migrationBuilder.CreateIndex(
                name: "ix_import_staging_rows_fingerprint",
                table: "import_staging_rows",
                column: "fingerprint");

            migrationBuilder.CreateIndex(
                name: "ix_import_staging_rows_household_batch",
                table: "import_staging_rows",
                columns: new[] { "household_id", "import_batch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_account_fingerprint",
                table: "transactions",
                columns: new[] { "account_id", "fingerprint" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_household_account_date",
                table: "transactions",
                columns: new[] { "household_id", "account_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_import_batch_id",
                table: "transactions",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_statement_id",
                table: "transactions",
                column: "statement_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_staging_rows");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "import_batches");
        }
    }
}
