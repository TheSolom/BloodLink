using BloodLink.Inventory.Api.Features.BloodBanks.RegisterBloodBank;
using BloodLink.Inventory.Api.Features.Donations.ReceiveDonation;
using BloodLink.Inventory.Api.Features.Stock.GetAvailableStock;
using BloodLink.Inventory.Api.Infrastructure.Middleware;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.ServiceDefaults;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Service defaults (OpenTelemetry, HealthChecks, Resilience, Service Discovery)
builder.AddServiceDefaults();

// Built-in OpenAPI & Problem Details
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Database & Persistence
string connectionString = builder.Configuration.GetConnectionString("InventoryDb")
    ?? builder.Configuration["INVENTORY_DB_CONNECTION_STRING"]
    ?? "Server=localhost,1433;Database=BloodLink_Inventory;TrustServerCertificate=True;Integrated Security=True;";

builder.Services.AddDbContext<InventoryDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// TimeProvider & Plain Handlers
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<RegisterBloodBankHandler>();
builder.Services.AddScoped<ReceiveDonationHandler>();
builder.Services.AddScoped<GetAvailableStockHandler>();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Apply migrations on startup ONLY in Development when explicitly configured
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Map Vertical Slices
app.MapRegisterBloodBank();
app.MapReceiveDonation();
app.MapGetAvailableStock();

app.Run();

public partial class Program;
