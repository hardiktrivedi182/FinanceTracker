using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database — uses in-memory provider by default for development.
// Will be replaced with SQL Server in production configuration.
builder.Services.AddDbContext<FinanceTrackerDbContext>(options =>
    options.UseInMemoryDatabase("FinanceTrackerDev"));

builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Make the implicit Program class accessible for integration tests
public partial class Program
{
}
