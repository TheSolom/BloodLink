using BloodLink.Inventory.Api.Features.BloodBanks.RegisterBloodBank;
using BloodLink.Inventory.Api.Features.Donations.ReceiveDonation;
using BloodLink.Inventory.Api.Features.Expiry.ExpireUnits;
using BloodLink.Inventory.Api.Features.Reservations.ReleaseUnits;
using BloodLink.Inventory.Api.Features.Reservations.ReserveUnits;
using BloodLink.Inventory.Api.Features.Stock.GetAvailableStock;
using BloodLink.Inventory.Api.Infrastructure.Messaging;
using BloodLink.Inventory.Api.Infrastructure.Middleware;
using BloodLink.Inventory.Api.Infrastructure.Persistence;
using BloodLink.ServiceDefaults;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Service defaults (OpenTelemetry, HealthChecks, Resilience, Service Discovery)
builder.AddServiceDefaults();

// Built-in OpenAPI & Problem Details
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Database & Persistence
string connectionString =
    builder.Configuration.GetConnectionString("InventoryConnection")
    ?? throw new InvalidOperationException("Connection string 'InventoryConnection' not found.");

builder.Services.AddDbContext<InventoryDbContext>(
    (sp, options) =>
    {
        options.UseSqlServer(connectionString);
        options.AddInterceptors(sp.GetRequiredService<DomainEventOutboxInterceptor>());
    }
);

// Messaging, Outbox, and Consumers
builder.Services.AddInventoryMessaging(builder.Configuration);

// TimeProvider & Plain Handlers
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<RegisterBloodBankHandler>();
builder.Services.AddScoped<ReceiveDonationHandler>();
builder.Services.AddScoped<GetAvailableStockHandler>();
builder.Services.AddScoped<ReserveUnitsHandler>();
builder.Services.AddScoped<ReleaseUnitsHandler>();
builder.Services.AddScoped<ExpireUnitsHandler>();

// Expiry Background Service
builder.Services.Configure<ExpireUnitsOptions>(
    builder.Configuration.GetSection(ExpireUnitsOptions.SectionName)
);
builder.Services.AddHostedService<ExpireUnitsBackgroundService>();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Apply migrations on startup ONLY in Development when explicitly configured
if (
    app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup")
)
{
    using IServiceScope scope = app.Services.CreateScope();
    InventoryDbContext dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
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
app.MapReserveUnits();
app.MapReleaseUnits();

app.Run();
