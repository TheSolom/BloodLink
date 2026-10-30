# Configuration & Security Refactoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the configuration architecture across .NET ServiceDefaults, Docker Compose, environment files, and application settings to eliminate hardcoded credentials, enforce cloud-native secret handling, fix existing compilation errors, and establish secure local development patterns without modifying technology versions or architecture boundaries.

**Architecture:**
Separate container infrastructure credentials from application runtime settings. Infrastructure credentials in `deploy/docker-compose.yml` use required variable interpolation (`${VAR:?error}`) supplied via a gitignored `deploy/.env`. SQL Server healthcheck references container environment variables via escaped interpolation (`$$MSSQL_SA_PASSWORD`) so credentials never leak into compose dumps. RabbitMQ is configured with a dedicated application user instead of default `guest`. Application runtime configuration uses structured JSON (`ConnectionStrings:DefaultConnection`, `RabbitMq:*`, `OpenTelemetry:Endpoint`) in tracked `appsettings.json` (clean defaults) and gitignored `appsettings.Development.json` / .NET User Secrets for local development. `ServiceDefaults/Extensions.cs` resolves `OpenTelemetry:Endpoint` with robust URI validation, falling back to `OTEL_EXPORTER_OTLP_ENDPOINT` when missing or whitespace.

**Tech Stack:** .NET 11 (pinned in `global.json`), C# 15, Microsoft.Extensions.Configuration, OpenTelemetry .NET SDK 1.20.1, Docker Compose v2, MassTransit RabbitMQ 8.5.12, SQL Server 2025.

**Spec:** User instructions covering 8 revision areas: OpenTelemetry precedence & validation, application settings location, Docker Compose healthcheck credential escaping, quiet compose validation, git security audit, `.env.example` migration, framework preservation, and comprehensive verification.

## Global Constraints

- Preserve all pinned framework, SDK, and package versions in `global.json`, `Directory.Build.props`, and `Directory.Packages.props`. No upgrades.
- Zero committed plain-text credentials in git-tracked files.
- `deploy/docker-compose.yml` must use required interpolation without fallback defaults.
- RabbitMQ must use a dedicated application user (`RABBIT_USER`) rather than `guest`.
- `OpenTelemetry:Endpoint` takes precedence; whitespace or missing values fall back to `OTEL_EXPORTER_OTLP_ENDPOINT`. Invalid endpoints fail fast with a descriptive exception.
- Do not violate existing NetArchTest architecture boundary rules.

## Review Focus

- Build failure: `Extensions.cs` missing `using Microsoft.AspNetCore.Builder;` and `using Microsoft.AspNetCore.Diagnostics.HealthChecks;`.
- Endpoint validation: invalid URI schemes (e.g. `ftp://` or malformed strings) must throw `InvalidOperationException`.
- Credential leak prevention: `docker compose config` must be run with `--quiet` to prevent logging resolved secrets.
- Healthcheck escaping: `$$MSSQL_SA_PASSWORD` must be escaped in Docker Compose to be evaluated by the container shell rather than the Compose parser.
- Git audit: verifying with `git check-ignore -v` that `deploy/.env` and `appsettings.Development.json` are ignored while example templates remain tracked.

---

### Task 1: Fix `shared/ServiceDefaults/Extensions.cs` Compilation and Structured OpenTelemetry Resolution

**Files to Inspect or Modify:**
- Modify: `shared/ServiceDefaults/Extensions.cs`

**Implementation Requirements:**
1. Add missing namespaces: `using Microsoft.AspNetCore.Builder;` and `using Microsoft.AspNetCore.Diagnostics.HealthChecks;`.
2. Refactor `AddOpenTelemetryExporters` to implement robust endpoint resolution:
   - Check `builder.Configuration["OpenTelemetry:Endpoint"]`. If not null and not whitespace, select it.
   - Else check `builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]`. If not null and not whitespace, select it.
   - If both are missing or whitespace, skip exporter registration (do not throw, do not call `UseOtlpExporter`).
   - If an endpoint string is present, validate that it is an absolute URI with `http` or `https` scheme. If invalid, throw `InvalidOperationException` with a descriptive message.
   - Configure `OtlpExporterOptions.Endpoint` and invoke `builder.Services.AddOpenTelemetry().UseOtlpExporter();`.
3. Preserve all existing tracing, metrics, logging, and health check configurations unchanged.

- [ ] **Step 1: Update using directives and `AddOpenTelemetryExporters` in `Extensions.cs`**

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace BloodLink.ServiceDefaults;

// ...

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        string? structuredEndpoint = builder.Configuration["OpenTelemetry:Endpoint"];
        string? fallbackEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        string? endpointStr = !string.IsNullOrWhiteSpace(structuredEndpoint)
            ? structuredEndpoint.Trim()
            : (!string.IsNullOrWhiteSpace(fallbackEndpoint) ? fallbackEndpoint.Trim() : null);

        if (endpointStr is not null)
        {
            if (!Uri.TryCreate(endpointStr, UriKind.Absolute, out Uri? endpointUri) ||
                (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"The configured OpenTelemetry endpoint '{endpointStr}' is invalid. It must be an absolute HTTP or HTTPS URL.");
            }

            builder.Services.Configure<OtlpExporterOptions>(options =>
            {
                options.Endpoint = endpointUri;
            });

            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
```

- [ ] **Step 2: Verify `shared/ServiceDefaults` compiles cleanly**
  - Run: `dotnet build shared/ServiceDefaults/ServiceDefaults.csproj`
  - Expected: Build succeeded with 0 Warning(s) and 0 Error(s).

- [ ] **Step 3: Commit**
  ```bash
  git add shared/ServiceDefaults/Extensions.cs
  git commit -m "fix(defaults): restore WebApplication reference and configure validated OpenTelemetry endpoint resolution"
  ```

---

### Task 2: Add Comprehensive Unit Tests for OpenTelemetry Configuration Resolution

**Files to Inspect or Modify:**
- Create: `tests/Architecture.Tests/ServiceDefaultsConfigurationTests.cs`
- Inspect: `tests/Architecture.Tests/Architecture.Tests.csproj` (already references `ServiceDefaults`)

**Implementation Requirements:**
1. Test structured configuration precedence: `OpenTelemetry:Endpoint` takes precedence over `OTEL_EXPORTER_OTLP_ENDPOINT` when both are valid.
2. Test fallback: empty or whitespace `OpenTelemetry:Endpoint` falls back to `OTEL_EXPORTER_OTLP_ENDPOINT`.
3. Test missing configuration: when both are empty, whitespace, or null, exporter is skipped without exception.
4. Test invalid endpoints: malformed strings or non-HTTP schemes throw `InvalidOperationException`.
5. Test standard .NET environment variable naming hierarchy (`OpenTelemetry__Endpoint`).

- [ ] **Step 1: Write `ServiceDefaultsConfigurationTests.cs`**

```csharp
using BloodLink.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using Shouldly;
using Xunit;

namespace BloodLink.Architecture.Tests;

public class ServiceDefaultsConfigurationTests
{
    [Fact]
    public void ConfigureOpenTelemetry_WhenStructuredEndpointConfigured_ShouldTakePrecedence()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:Endpoint"] = "http://structured-host:4317";
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://fallback-host:4317";

        builder.ConfigureOpenTelemetry();
        using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<OtlpExporterOptions>>().Value;
        options.Endpoint.ShouldBe(new Uri("http://structured-host:4317"));
    }

    [Fact]
    public void ConfigureOpenTelemetry_WhenStructuredEndpointIsWhitespace_ShouldFallbackToOtelEnvVar()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:Endpoint"] = "   ";
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://fallback-host:4317";

        builder.ConfigureOpenTelemetry();
        using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<OtlpExporterOptions>>().Value;
        options.Endpoint.ShouldBe(new Uri("http://fallback-host:4317"));
    }

    [Fact]
    public void ConfigureOpenTelemetry_WhenStandardDotnetEnvVarOverrideUsed_ShouldResolveCorrectly()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry__Endpoint"] = "http://dotnet-env-host:4317";

        builder.ConfigureOpenTelemetry();
        using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<OtlpExporterOptions>>().Value;
        options.Endpoint.ShouldBe(new Uri("http://dotnet-env-host:4317"));
    }

    [Fact]
    public void ConfigureOpenTelemetry_WhenBothEndpointsMissingOrWhitespace_ShouldNotThrowAndSkipRegistration()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:Endpoint"] = "";
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "  ";

        Should.NotThrow(() => builder.ConfigureOpenTelemetry());
        using var app = builder.Build();
    }

    [Theory]
    [InlineData("not-a-valid-uri")]
    [InlineData("ftp://localhost:4317")]
    [InlineData("relative/path/endpoint")]
    public void ConfigureOpenTelemetry_WhenEndpointIsInvalid_ShouldThrowInvalidOperationException(string invalidEndpoint)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:Endpoint"] = invalidEndpoint;

        var ex = Should.Throw<InvalidOperationException>(() => builder.ConfigureOpenTelemetry());
        ex.Message.ShouldContain("invalid");
    }
}
```

- [ ] **Step 2: Run tests and verify all pass**
  - Run: `dotnet test tests/Architecture.Tests/Architecture.Tests.csproj --filter FullyQualifiedName~ServiceDefaultsConfigurationTests`
  - Expected: PASS (5 passed, 0 failed).

- [ ] **Step 3: Commit**
  ```bash
  git add tests/Architecture.Tests/ServiceDefaultsConfigurationTests.cs
  git commit -m "test(defaults): add unit tests for OpenTelemetry endpoint precedence, fallback, and validation"
  ```

---

### Task 3: Update `.gitignore` and Audit Git Tracking

**Files to Inspect or Modify:**
- Modify: `.gitignore`
- Inspect: `git ls-files`

**Implementation Requirements:**
1. Check that no sensitive files (`deploy/.env`, `appsettings.Development.json`) are currently tracked via `git ls-files`.
2. Add explicit ignore rules for `deploy/.env` and all `appsettings.Development.json` files.
3. Explicitly preserve example and non-sensitive files: `!deploy/.env.example`, `!.env.example`.
4. Verify ignore rules with `git check-ignore -v`.

- [ ] **Step 1: Check tracked files before modifying `.gitignore`**
  - Run: `git ls-files --error-unmatch deploy/.env appsettings.Development.json`
  - Expected: Files not tracked (exit code 1).

- [ ] **Step 2: Update `.gitignore` with environment and settings rules**
  Add under `# OS / Environment`:
  ```gitignore
  # Environment & Local Secrets
  *.env
  deploy/.env
  !deploy/.env.example
  !.env.example
  **/appsettings.Development.json
  appsettings.Development.json
  ```

- [ ] **Step 3: Verify ignore and whitelist behavior**
  - Run: `git check-ignore -v deploy/.env appsettings.Development.json`
  - Expected: Both match ignore rules.
  - Run: `git check-ignore -v deploy/.env.example appsettings.json`
  - Expected: Neither is ignored (exit code 1).

- [ ] **Step 4: Commit**
  ```bash
  git add .gitignore
  git commit -m "chore(git): ignore deploy/.env and appsettings.Development.json while preserving templates"
  ```

---

### Task 4: Create Canonical `appsettings.json` and Development Configuration

**Files to Inspect or Modify:**
- Create: `appsettings.json` (tracked, safe defaults)
- Create: `appsettings.Development.json` (gitignored, development credentials)
- Inspect: Content root loading conventions across microservices

**Implementation Requirements:**
1. The repository currently contains shared libraries; microservice host projects (`services/inventory`, etc.) will be scaffolded in subsequent phases.
2. Create `appsettings.json` at repository root as the canonical service configuration template.
3. Provide structured sections:
   - `ConnectionStrings:DefaultConnection` (with placeholder `Password=;`)
   - `RabbitMq:Host`, `RabbitMq:Port`, `RabbitMq:VirtualHost`, `RabbitMq:UserName`, `RabbitMq:Password`
   - `OpenTelemetry:Endpoint`
4. Create `appsettings.Development.json` with local development credentials matching `deploy/.env`.
5. Ensure `git status` verifies `appsettings.Development.json` is ignored.

- [ ] **Step 1: Create `appsettings.json` with safe defaults**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=BloodLink;User Id=sa;Password=;TrustServerCertificate=True;"
  },
  "RabbitMq": {
    "Host": "localhost",
    "Port": 5672,
    "VirtualHost": "/",
    "UserName": "",
    "Password": ""
  },
  "OpenTelemetry": {
    "Endpoint": "http://localhost:4317"
  }
}
```

- [ ] **Step 2: Create `appsettings.Development.json` with local development credentials**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=BloodLink;User Id=sa;Password=BloodLink!Password2026;TrustServerCertificate=True;"
  },
  "RabbitMq": {
    "Host": "localhost",
    "Port": 5672,
    "VirtualHost": "/",
    "UserName": "bloodlink_app",
    "Password": "BloodLink!Rabbit2026"
  },
  "OpenTelemetry": {
    "Endpoint": "http://localhost:4317"
  }
}
```

- [ ] **Step 3: Verify Git tracking status**
  - Run: `git status --porcelain`
  - Expected: `appsettings.json` is untracked; `appsettings.Development.json` does NOT appear.

- [ ] **Step 4: Commit `appsettings.json`**
  ```bash
  git add appsettings.json
  git commit -m "feat(config): add canonical appsettings.json template with structured configuration"
  ```

---

### Task 5: Refactor `deploy/docker-compose.yml` to Eliminate Hardcoded Credentials

**Files to Inspect or Modify:**
- Modify: `deploy/docker-compose.yml`

**Implementation Requirements:**
1. Replace `MSSQL_SA_PASSWORD: "${SA_PASSWORD:-BloodLink!Password2026}"` with required variable syntax: `${SQL_PASSWORD:?SQL_PASSWORD is required}`.
2. In SQL Server healthcheck, do not interpolate password. Reference container environment variable with escaped Compose interpolation: `$$MSSQL_SA_PASSWORD`.
3. Verify `sqlcmd` paths: test `/opt/mssql-tools18/bin/sqlcmd` with `-C` (trust certificate) and fallback to `/opt/mssql-tools/bin/sqlcmd`.
4. In RabbitMQ, replace default `guest` with dedicated application user:
   - `RABBITMQ_DEFAULT_USER: "${RABBIT_USER:?RABBIT_USER is required}"`
   - `RABBITMQ_DEFAULT_PASS: "${RABBIT_PASSWORD:?RABBIT_PASSWORD is required}"`
5. Keep volume and network isolation intact.

- [ ] **Step 1: Update `deploy/docker-compose.yml`**

```yaml
name: bloodlink

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2025-CU10-ubuntu-24.04
    container_name: bloodlink-sqlserver
    restart: unless-stopped
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${SQL_PASSWORD:?SQL_PASSWORD is required}"
      MSSQL_PID: "Developer"
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q 'SELECT 1' || /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -Q 'SELECT 1'"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    networks:
      - bloodlink-network

  rabbitmq:
    image: masstransit/rabbitmq:4.3.1
    container_name: bloodlink-rabbitmq
    restart: unless-stopped
    environment:
      RABBITMQ_DEFAULT_USER: "${RABBIT_USER:?RABBIT_USER is required}"
      RABBITMQ_DEFAULT_PASS: "${RABBIT_PASSWORD:?RABBIT_PASSWORD is required}"
    ports:
      - "5672:5672"     # AMQP
      - "15672:15672"   # Management Web UI
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "check_port_connectivity"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 15s
    networks:
      - bloodlink-network

  otel-lgtm:
    image: grafana/otel-lgtm:0.38.1
    container_name: bloodlink-otel-lgtm
    restart: unless-stopped
    ports:
      - "3000:3000"   # Grafana
      - "4317:4317"   # OTLP gRPC receiver
      - "4318:4318"   # OTLP HTTP receiver
    environment:
      - OTEL_LGTM_LOG_LEVEL=INFO
    volumes:
      - otel-data:/data
    networks:
      - bloodlink-network

volumes:
  sqlserver-data:
  rabbitmq-data:
  otel-data:

networks:
  bloodlink-network:
    driver: bridge
```

- [ ] **Step 2: Verify Compose fails when required variables are missing**
  - Run: `docker compose -f deploy/docker-compose.yml config --quiet`
  - Expected: Non-zero exit code with error message indicating `SQL_PASSWORD is required`, `RABBIT_USER is required`, or `RABBIT_PASSWORD is required`.

- [ ] **Step 3: Commit**
  ```bash
  git add deploy/docker-compose.yml
  git commit -m "refactor(deploy): enforce required credentials and escape container healthcheck password"
  ```

---

### Task 6: Migrate `.env.example` to `deploy/.env.example`

**Files to Inspect or Modify:**
- Create: `deploy/.env.example`
- Delete: `.env.example` (at root)
- Inspect: Repository references to `.env.example` (verified: only `.gitignore`)

**Implementation Requirements:**
1. Create `deploy/.env.example` documenting all variables required by `docker-compose.yml`.
2. Variable list: `SQL_PASSWORD`, `RABBIT_USER`, `RABBIT_PASSWORD`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`.
3. Use empty placeholders or clearly non-functional dummy values. Do not commit actual passwords.
4. Remove legacy root `.env.example`.

- [ ] **Step 1: Create `deploy/.env.example`**

```env
# ==============================================================
# BloodLink Local Infrastructure Environment Example
# Copy this file to 'deploy/.env' and provide secure credentials.
# Do NOT commit 'deploy/.env' to version control.
# ==============================================================

# Microsoft SQL Server 2025
# sa account password (must meet complexity requirements: >= 8 chars, uppercase, lowercase, digit/symbol)
SQL_PASSWORD=

# RabbitMQ
# Dedicated application user account and password
RABBIT_USER=
RABBIT_PASSWORD=

# OpenTelemetry Exporter
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_SERVICE_NAME=BloodLink
```

- [ ] **Step 2: Remove root `.env.example`**
  - Run: `git rm .env.example`
  - Expected: `.env.example` staged for removal.

- [ ] **Step 3: Commit**
  ```bash
  git add deploy/.env.example
  git commit -m "docs(deploy): migrate environment example to deploy/.env.example with empty placeholders"
  ```

---

### Task 7: Create `deploy/.env` and Validate Docker Compose

**Files to Inspect or Modify:**
- Create: `deploy/.env` (gitignored local file)

**Implementation Requirements:**
1. Populate `deploy/.env` with local development credentials matching `appsettings.Development.json`.
2. Validate Docker Compose configuration using `--quiet` to prevent logging resolved secrets.
3. Confirm `deploy/.env` is ignored by Git.

- [ ] **Step 1: Create `deploy/.env`**

```env
SQL_PASSWORD=BloodLink!Password2026
RABBIT_USER=bloodlink_app
RABBIT_PASSWORD=BloodLink!Rabbit2026
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_SERVICE_NAME=BloodLink
```

- [ ] **Step 2: Validate Docker Compose configuration quietly**
  - Run: `docker compose --env-file deploy/.env -f deploy/docker-compose.yml config --quiet`
  - Expected: Exit code 0, no output, no plain-text passwords printed in terminal.

- [ ] **Step 3: Verify git status confirms `deploy/.env` is ignored**
  - Run: `git status --porcelain deploy/.env`
  - Expected: Empty output (file is untracked and ignored).

---

### Task 8: End-to-End Solution Verification & Documentation

**Files to Inspect or Modify:**
- Modify: `README.md`

**Implementation Requirements:**
1. Update `README.md` to document the new infrastructure startup command:
   `docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d`
2. Update RabbitMQ credentials documented in README (`bloodlink_app` / configured password).
3. Run complete test suite and solution build to confirm no regressions.

- [ ] **Step 1: Update README.md infrastructure instructions**
  - Update command to `docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d`.
  - Add instructions to copy `deploy/.env.example` to `deploy/.env`.
  - Update RabbitMQ Management UI credential note from `guest/guest` to `bloodlink_app` / password from `deploy/.env`.

- [ ] **Step 2: Run full build and test suite**
  - Run: `dotnet build BloodLink.slnx`
  - Expected: Build succeeded with 0 errors.
  - Run: `dotnet test BloodLink.slnx`
  - Expected: All unit and architecture tests pass.

- [ ] **Step 3: Commit**
  ```bash
  git add README.md
  git commit -m "docs(readme): document deploy/.env setup and updated infrastructure startup command"
  ```
