# CLAUDE.md — AccuBooks Codebase Guide

This file provides AI assistants with the context needed to work effectively in this repository.

---

## Project Overview

**AccuBooks** is a double-entry bookkeeping system with:
- **Backend**: ASP.NET Core 8 Web API (`accounting-backend/`)
- **Frontend**: Vanilla HTML/CSS/ES6+ JavaScript (`accounting-frontend/`)
- **Solution file**: `accounting.sln` at the repo root

The system is multi-tenant (multi-organisation), supports full double-entry bookkeeping, JWT authentication, and role-based access within each organisation.

---

## Repository Structure

```
accounting/
├── accounting-backend/
│   ├── Controllers/              # Thin API controllers — NO business logic here
│   ├── Services/
│   │   ├── ServiceInterfaces.cs  # All DTOs + service interfaces (single file)
│   │   ├── ServiceImplementations.cs  # All service implementations (single file)
│   │   └── AuthService.cs        # Auth-specific service
│   ├── Models/
│   │   ├── Models.cs             # EF Core entities with validation attributes
│   │   └── ApiResponse.cs        # Standardised error response envelope
│   ├── Data/
│   │   └── ApplicationDbContext.cs
│   ├── Migrations/               # EF Core migrations (PostgreSQL)
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs     # Global exception → HTTP mapping
│   │   └── RequestResponseLoggingMiddleware.cs
│   ├── Exceptions/
│   │   └── CustomExceptions.cs   # Domain exception types
│   ├── Validators/
│   │   └── CustomValidators.cs
│   ├── Filters/
│   │   └── OrganisationRoleFilter.cs  # [RequireOrganisationRole] attribute
│   ├── Tests/                    # xUnit integration tests (82 tests)
│   │   ├── Helpers/
│   │   │   ├── AccountingWebApplicationFactory.cs
│   │   │   └── AuthHelper.cs
│   │   └── *ControllerTests.cs
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── AccountingApp.csproj
│
├── accounting-frontend/
│   ├── pages/                    # One HTML file per page/feature
│   ├── js/
│   │   ├── api.js                # Centralised API service layer (set BASE_URL here)
│   │   ├── export.js             # PDF/Excel client-side export
│   │   └── main.js
│   ├── css/style.css
│   └── index.html
│
├── accounting-tests/             # Standalone integration test project
├── DATABASE_SETUP.md
├── README.md
└── accounting.sln
```

---

## Technology Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 |
| ORM / DB | EF Core 8 + Npgsql (PostgreSQL) — code-first migrations |
| Auth | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| Password hashing | BCrypt.Net-Next |
| Logging | Serilog (console + rolling file) |
| API docs | Swagger / OpenAPI (dev only) |
| Testing | xUnit, `WebApplicationFactory`, FluentAssertions, In-Memory DB |
| Frontend | Vanilla HTML5 / CSS3 / ES6+ |
| PDF export | jsPDF + AutoTable (CDN, client-side) |
| Excel export | SheetJS / xlsx (CDN, client-side) |

---

## Development Commands

### Backend

```bash
# Run the API (from accounting-backend/)
cd accounting-backend
dotnet run
# API: http://localhost:5000  |  Swagger: http://localhost:5000/swagger

# Run integration tests (from accounting-backend/Tests/)
cd accounting-backend/Tests
dotnet test
# Expected: Passed! - Failed: 0, Passed: 82

# Apply DB migrations
cd accounting-backend
dotnet ef database update

# Add a new migration
dotnet ef migrations add <MigrationName>

# Publish for production
dotnet publish -c Release -o ./publish
```

### Frontend

```bash
# Serve locally (from accounting-frontend/)
cd accounting-frontend
python -m http.server 3000
# Open http://localhost:3000
```

---

## Configuration

All configuration lives in `accounting-backend/appsettings.json` (with environment-specific overrides). The app throws `InvalidOperationException` at startup if any required key is missing — this is intentional.

**Required keys:**

| Key | Notes |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `AllowedOrigins` | Array of CORS-allowed frontend origins |
| `JwtSettings:SecretKey` | JWT signing key — minimum 32 characters |

**Optional:**

| Key | Default |
|---|---|
| `JwtSettings:ExpirationInMinutes` | 60 |

**Important Npgsql note:** `Program.cs` sets `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` to treat `DateTime.Unspecified` as UTC. Always use UTC for `DateTime` values.

---

## Architecture Conventions

### 1. Thin Controllers — No Business Logic

Controllers only:
- Extract route/query parameters
- Call a service method
- Return the result with an appropriate HTTP status

**Do not** add database queries, business rules, or conditional logic in controllers.

### 2. Service Pattern

All business logic lives in services. Interfaces and DTOs are defined together in `Services/ServiceInterfaces.cs`. Implementations go in `Services/ServiceImplementations.cs` or a dedicated file for complex services (e.g. `AuthService.cs`).

Register new services as `Scoped` in `Program.cs`:
```csharp
builder.Services.AddScoped<IMyService, MyServiceImpl>();
```

### 3. Exception-Driven Error Handling

**Never use try/catch in controllers.** Throw domain exceptions from services; the `ExceptionHandlingMiddleware` maps them to HTTP responses:

| Exception | HTTP Status |
|---|---|
| `ValidationException` | 400 |
| `BusinessRuleException` | 400 |
| `OperationFailedException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `ResourceNotFoundException` | 404 |
| `DuplicateResourceException` | 409 |
| `ServiceUnavailableException` | 503 |
| Unhandled | 500 |

All exceptions are in `Exceptions/CustomExceptions.cs`.

### 4. Consistent API Response Format

Successful responses return the resource directly (no envelope).
Error responses always use this envelope:
```json
{
  "success": false,
  "error": {
    "code": "RESOURCE_NOT_FOUND",
    "message": "The GLAccount with ID '...' was not found."
  }
}
```

### 5. Organisation-Scoped Routes

All data endpoints are scoped under an organisation:
```
/api/organisations/{organisationId}/[controller]
```

Controllers declare this via `[Route("api/organisations/{organisationId}/[controller]")]`.

### 6. Role-Based Access Control

Use the `[RequireOrganisationRole("RoleName")]` attribute on controller actions to enforce organisation membership and minimum role.

**Role hierarchy (ascending):** `Viewer` → `Bookkeeper` → `Manager` → `Owner`

Example:
```csharp
[HttpPost]
[RequireOrganisationRole("Bookkeeper")]
public async Task<IActionResult> CreateEntry(...) { ... }
```

### 7. EF Core — Soft Deletes

`Customer`, `Supplier`, `GLAccount`, `OrganisationMember`, and `ProductService` use soft-delete (`IsActive = false`) rather than hard-delete. Always filter `IsActive == true` in list queries.

### 8. Double-Entry Rules

- Every `DaybookEntry` must have at least 2 `JournalLine` records
- Entries can only be **posted** when `SUM(DebitAmount) == SUM(CreditAmount)` — enforced at posting time
- Once posted (`IsPosted = true`), an entry is **immutable** and cannot be deleted
- GL account types and their normal balance: Asset/Expense → Debit; Liability/Equity/Revenue → Credit

### 9. Models vs DTOs

- **`Models/Models.cs`** — EF Core entities (the database schema). Add validation attributes here.
- **`Services/ServiceInterfaces.cs`** — Request/Response DTOs. Never expose EF entities directly from the API.

---

## Testing Conventions

Tests live in `accounting-backend/Tests/` and are compiled separately (excluded from the main project via `.csproj`).

**Key patterns:**

- Use `AccountingWebApplicationFactory` as `IClassFixture<>` — it spins up the full ASP.NET pipeline with an **in-memory database** (unique per class, no PostgreSQL required).
- Use `AuthHelper` to register/login and obtain a JWT for authenticated requests.
- Each test class gets its own isolated in-memory database (`Guid.NewGuid()` db name).
- Tests are xUnit integration tests that exercise the full HTTP stack via `HttpClient`.

**Running tests:**
```bash
cd accounting-backend/Tests
dotnet test
```

**Do not** mock services in these tests — the integration test approach tests the full stack including EF Core.

---

## Frontend Conventions

- All API calls go through `accounting-frontend/js/api.js`. The `BASE_URL` constant must be updated before production deployment.
- Pages are standalone HTML files in `accounting-frontend/pages/`.
- PDF and Excel export is handled client-side in `export.js` using CDN libraries (no server-side rendering).
- No build step — files are served as static assets.

---

## Adding New Features — Checklist

When adding a new domain entity or endpoint:

1. **Model** — Add EF Core entity to `Models/Models.cs` with validation attributes.
2. **DbContext** — Add `DbSet<T>` to `Data/ApplicationDbContext.cs` and configure relationships.
3. **Migration** — Run `dotnet ef migrations add <Name>` from `accounting-backend/`.
4. **DTO/Interface** — Add request/response DTOs and service interface to `Services/ServiceInterfaces.cs`.
5. **Service** — Implement the interface in `Services/ServiceImplementations.cs` (or a new dedicated file).
6. **Register** — Add `builder.Services.AddScoped<IMyService, MyServiceImpl>()` in `Program.cs`.
7. **Controller** — Create a thin controller in `Controllers/`. Apply `[Authorize]`, `[ApiController]`, and the org-scoped route. Use `[RequireOrganisationRole]` on write actions.
8. **Tests** — Add an `*ControllerTests.cs` file in `Tests/` using `AccountingWebApplicationFactory`.

---

## Known Issues / Important Notes

- **`RequestResponseLoggingMiddleware`** only logs metadata (method, path, status, duration) — it does **not** buffer the request/response body. Do not change this; buffering causes `ObjectDisposedException` in the test host.
- **Circular reference handling** — `ReferenceHandler.IgnoreCycles` is set in `Program.cs` to handle EF Core navigation property cycles.
- **Npgsql legacy timestamp** — The `AppContext.SetSwitch` in `Program.cs` must remain; removing it will break datetime handling with PostgreSQL.
- **Test project exclusion** — `Tests/` is excluded from the main project build via `<Compile Remove="Tests\**\*.cs" />` in `AccountingApp.csproj`. The tests have their own `.csproj`.

---

## Roadmap (Planned, Not Yet Implemented)

- Role-based write authorisation enforcement (Owner/Admin/Member)
- Pagination for list endpoints
- Audit trail (created/modified by whom)
- OAuth2 login (Google, Microsoft — stubs exist in User model)
- Rate limiting on auth endpoints
- Refresh token flow
- Multi-currency support
- Bank reconciliation
- VAT/GST compliance reports
- CSV bulk import/export
- Docker / Docker Compose setup
- CI pipeline (run 82 tests on every PR)
