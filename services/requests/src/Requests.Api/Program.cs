using BloodLink.BuildingBlocks.Web;
using BloodLink.Requests.Api.Features.Requests.CancelRequest;
using BloodLink.Requests.Api.Features.Requests.GetRequestStatus;
using BloodLink.Requests.Api.Features.Requests.ListRequests;
using BloodLink.Requests.Api.Features.Requests.SubmitRequest;
using BloodLink.Requests.Api.Infrastructure.Auth;
using BloodLink.Requests.Api.Infrastructure.Clients;
using BloodLink.Requests.Api.Infrastructure.Messaging;
using BloodLink.Requests.Api.Infrastructure.Persistence;
using BloodLink.Requests.Api.Infrastructure.Persistence.ReadModels;
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

// CORS
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? [];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()
    )
);

// Database & Persistence
string connectionString = builder.Configuration.GetConnectionString("RequestsConnection")
    ?? throw new InvalidOperationException("Connection string 'RequestsConnection' not found.");

builder.Services.AddDbContext<RequestsDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Messaging, Outbox, and Consumers
builder.Services.AddRequestsMessaging(builder.Configuration);

// HTTP Client for Inventory Snapshot Sync
string inventoryBaseUrl = builder.Configuration["Inventory:BaseUrl"]
    ?? throw new InvalidOperationException("Configuration value 'Inventory:BaseUrl' is required.");

builder.Services.AddHttpClient<IStockSnapshotClient, StockSnapshotClient>(client =>
{
    client.BaseAddress = new Uri(inventoryBaseUrl, UriKind.Absolute);
});
builder.Services.AddHostedService<StockSnapshotBootstrapService>();

// TimeProvider & Plain Handlers & Contexts
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IHospitalContext, DevHospitalContext>();
builder.Services.AddScoped<IStockReadModelService, StockReadModelService>();

builder.Services.AddScoped<SubmitRequestHandler>();
builder.Services.AddScoped<CancelRequestHandler>();
builder.Services.AddScoped<GetRequestStatusHandler>();
builder.Services.AddScoped<ListRequestsHandler>();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

// Apply migrations on startup ONLY in Development when explicitly configured
if (
    app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup")
)
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    RequestsDbContext requestsDbContext =
        scope.ServiceProvider.GetRequiredService<RequestsDbContext>();
    await requestsDbContext.Database.MigrateAsync();
}

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Map Vertical Slices
app.MapSubmitRequest();
app.MapCancelRequest();
app.MapGetRequestStatus();
app.MapListRequests();

await app.RunAsync();

public partial class Program;
