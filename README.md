# AccuBooks — Double-Entry Bookkeeping System

A professional-grade accounting backend built with ASP.NET Core 8, featuring full double-entry bookkeeping, multi-organisation support, and a comprehensive integration test suite.

---

## Features

- **General Ledger** — Create and manage GL accounts across five account types (Asset, Liability, Equity, Revenue, Expense)
- **Daybook Entries** — Record Sales, Purchase, Journal, Receipt, Payment, and Contra transactions with enforced debit/credit balance
- **Financial Reports** — Trial balance, T-accounts, and general ledger with date-range filtering
- **Customer & Supplier Management** — Subsidiary ledger support linked to control accounts (AR/AP)
- **Multi-Organisation** — Complete data isolation per organisation; users can belong to multiple organisations
- **JWT Authentication** — Stateless auth with configurable token expiry
- **Structured Logging** — Serilog with rolling file and console sinks, environment-specific log levels
- **Global Error Handling** — Consistent JSON error responses via exception middleware; no try/catch in controllers

---

## Project Structure

```
accounting/
├── accounting-backend/              # ASP.NET Core 8 Web API
│   ├── Controllers/                 # Thin API controllers (no business logic)
│   ├── Services/
│   │   ├── ServiceInterfaces.cs     # DTOs and service interfaces
│   │   ├── ServiceImplementations.cs
│   │   └── AuthService.cs
│   ├── Models/
│   │   ├── Models.cs                # EF Core entities with validation attributes
│   │   └── ApiResponse.cs           # Standardised error response envelope
│   ├── Data/
│   │   └── ApplicationDbContext.cs
│   ├── Migrations/                  # EF Core migrations
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs   # Maps exceptions → HTTP status codes
│   │   └── RequestResponseLoggingMiddleware.cs
│   ├── Exceptions/
│   │   └── CustomExceptions.cs      # ResourceNotFoundException, BusinessRuleException, etc.
│   ├── Validators/
│   │   └── CustomValidators.cs
│   ├── Tests/                       # xUnit integration tests (82 tests)
│   │   ├── Helpers/
│   │   │   ├── AccountingWebApplicationFactory.cs
│   │   │   └── AuthHelper.cs
│   │   ├── AuthControllerTests.cs
│   │   ├── GLAccountsControllerTests.cs
│   │   ├── DaybookControllerTests.cs
│   │   ├── CustomersControllerTests.cs
│   │   ├── SuppliersControllerTests.cs
│   │   ├── ReportsControllerTests.cs
│   │   ├── OrganisationsControllerTests.cs
│   │   └── IntegrationTests.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── appsettings.Development.json
│
├── accounting-frontend/             # Vanilla HTML/CSS/JS frontend
│   ├── pages/                       # Application pages
│   ├── js/
│   │   ├── api.js                   # API service layer
│   │   └── main.js
│   ├── css/style.css
│   └── index.html
│
├── accounting.sln
└── DATABASE_SETUP.md
```

---

## Technology Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 |
| Database | SQL Server (EF Core, code-first migrations) |
| Authentication | JWT Bearer |
| Logging | Serilog (console + rolling file) |
| API Docs | Swagger / OpenAPI |
| Testing | xUnit, WebApplicationFactory, FluentAssertions |
| Frontend | Vanilla HTML5 / CSS3 / ES6+ JavaScript |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server Express (or any SQL Server instance)
- Optionally: SQL Server Management Studio (SSMS)

### 1. Configure the database connection

Edit `accounting-backend/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=AccountingDB;Trusted_Connection=true;TrustServerCertificate=true;"
}
```

> For SQL Server Authentication: replace `Trusted_Connection=true` with `User Id=sa;Password=YourPassword`.
> See [DATABASE_SETUP.md](DATABASE_SETUP.md) for full SQL Server installation instructions.

### 2. Configure the JWT secret

In `appsettings.json`, set a strong secret before running in any shared environment:

```json
"JwtSettings": {
  "SecretKey": "replace-with-a-long-random-secret",
  "ExpirationInMinutes": 60
}
```

### 3. Run the backend

```bash
cd accounting-backend

# Install EF Core CLI tool (first time only)
dotnet tool install -g dotnet-ef

# Apply migrations and start the API
dotnet ef database update
dotnet run
```

The API will be available at `http://localhost:5000`.
Swagger UI: `http://localhost:5000/swagger`

### 4. Run the frontend

Open `accounting-frontend/index.html` directly in a browser, or serve it locally:

```bash
cd accounting-frontend
python -m http.server 3000
# then open http://localhost:3000
```

---

## Running Tests

Tests use an in-memory database — no SQL Server required.

```bash
cd accounting-backend/Tests
dotnet test
```

```
Passed! - Failed: 0, Passed: 82, Skipped: 0, Total: 82
```

Tests cover:
- Registration, login, protected endpoint access
- GL account CRUD and business rules (duplicate codes, active-entry delete guard)
- Daybook entry lifecycle (create → post → delete restrictions, balance validation)
- Customer and supplier CRUD with soft-delete verification
- Financial reports (trial balance, T-accounts, general ledger, date-range filtering)
- Organisation create, read, and update

---

## API Reference

All endpoints except `/api/auth/*` require a `Authorization: Bearer <token>` header.

### Authentication

| Method | Path | Description |
|---|---|---|
| POST | `/api/auth/register` | Register a new user |
| POST | `/api/auth/login` | Login and receive a JWT |

### Organisations

| Method | Path | Description |
|---|---|---|
| POST | `/api/organisations` | Create organisation (creator becomes Owner) |
| GET | `/api/organisations` | List organisations the current user belongs to |
| GET | `/api/organisations/{id}` | Get organisation details |
| PUT | `/api/organisations/{id}` | Update organisation |

### GL Accounts

Base path: `/api/organisations/{orgId}/glaccounts`

| Method | Path | Description |
|---|---|---|
| GET | `/glaccounts` | List all active GL accounts |
| POST | `/glaccounts` | Create a GL account |
| GET | `/glaccounts/{accountId}` | Get a GL account |
| PUT | `/glaccounts/{accountId}` | Update a GL account |
| DELETE | `/glaccounts/{accountId}` | Delete (soft-delete; blocked if posted entries exist) |

### Daybook

Base path: `/api/organisations/{orgId}/daybook`

| Method | Path | Description |
|---|---|---|
| GET | `/daybook` | List entries (optional `?fromDate=` / `?toDate=` filters) |
| POST | `/daybook` | Create a daybook entry with journal lines |
| GET | `/daybook/{entryId}` | Get a daybook entry |
| POST | `/daybook/{entryId}/post` | Post an entry (debits must equal credits) |
| DELETE | `/daybook/{entryId}` | Delete an unposted entry |

### Reports

Base path: `/api/organisations/{orgId}/reports`

| Method | Path | Description |
|---|---|---|
| GET | `/reports/trial-balance` | Trial balance (optional `?fromDate=` / `?toDate=`) |
| GET | `/reports/taccounts` | All T-accounts with balances |
| GET | `/reports/taccounts/{accountId}` | Single T-account detail |
| GET | `/reports/general-ledger` | General ledger (requires `?fromDate=` and `?toDate=`) |

### Customers & Suppliers

Base path: `/api/organisations/{orgId}/customers` (same pattern for `/suppliers`)

| Method | Path | Description |
|---|---|---|
| GET | `/customers` | List active customers |
| POST | `/customers` | Create a customer |
| GET | `/customers/{customerId}` | Get a customer |
| PUT | `/customers/{customerId}` | Update a customer |
| DELETE | `/customers/{customerId}` | Soft-delete a customer |

---

## Error Response Format

Successful responses return the resource directly. Errors return a consistent envelope:

```json
{
  "success": false,
  "error": {
    "code": "RESOURCE_NOT_FOUND",
    "message": "GL Account with ID '...' was not found."
  }
}
```

| HTTP Status | Exception type | Typical cause |
|---|---|---|
| 400 | `ValidationException` | Invalid input |
| 400 | `BusinessRuleException` | Business rule violated (e.g. unbalanced entry) |
| 400 | `OperationFailedException` | Operation blocked (e.g. deleting a posted entry) |
| 401 | — | Missing or invalid JWT |
| 404 | `ResourceNotFoundException` | Entity not found |
| 409 | `DuplicateResourceException` | Duplicate (e.g. GL account code already exists) |
| 500 | Unhandled exception | Unexpected server error |

---

## Configuration

### Logging (`appsettings.json` / `appsettings.Development.json`)

Serilog is configured via `appsettings.json`. Override minimum levels per environment:

```json
// appsettings.json — production defaults
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": { "Microsoft.AspNetCore": "Warning" }
  },
  "WriteTo": [
    { "Name": "Console" },
    { "Name": "File", "Args": { "path": "logs/accounting-app-.txt", "rollingInterval": "Day" } }
  ]
}

// appsettings.Development.json — verbose for local dev
"Serilog": {
  "MinimumLevel": {
    "Default": "Debug",
    "Override": { "Microsoft.AspNetCore": "Information" }
  }
}
```

Logs are written to `accounting-backend/logs/` and rotated daily (30-day retention).

---

## Double-Entry Bookkeeping Concepts

Every transaction is recorded in a **Daybook Entry** containing two or more **Journal Lines**:

- Each line specifies a GL account, and either a debit or credit amount
- **Total debits must equal total credits** — the API enforces this at posting time
- Once posted, an entry is immutable and cannot be deleted

**Account types and their normal balance:**

| Type | Normal Balance |
|---|---|
| Asset | Debit |
| Expense | Debit |
| Liability | Credit |
| Equity | Credit |
| Revenue | Credit |

---

## Roadmap

The following improvements are planned for future development.

### Backend

- [ ] **Role-based authorisation** — enforce Owner/Admin/Member permissions on write operations (currently all authenticated members can write)
- [ ] **Pagination** — add `page` / `pageSize` query parameters to all list endpoints
- [ ] **Audit trail** — record who created or modified each record and when
- [ ] **OAuth2 login** — Google and Microsoft sign-in (stubs exist in config and User model)
- [ ] **Rate limiting** — protect public auth endpoints from brute-force
- [ ] **Refresh tokens** — replace short-lived JWTs with a refresh-token flow
- [ ] **Multi-currency** — store currency on transactions and convert for reporting
- [ ] **Bank reconciliation** — match bank statement lines to posted daybook entries
- [ ] **Tax compliance reports** — VAT/GST summary reports
- [ ] **Soft-delete recovery** — admin endpoint to restore soft-deleted records
- [ ] **Batch import/export** — CSV upload for bulk GL accounts and transactions

### Reporting

- [ ] **PDF export** — generate printable versions of trial balance and general ledger
- [ ] **Profit & Loss statement** — derived from Revenue and Expense accounts
- [ ] **Balance sheet** — derived from Asset, Liability, and Equity accounts
- [ ] **Cash flow statement**

### Frontend

- [ ] **Connect to live API** — the frontend currently uses placeholder data; wire up to the backend
- [ ] **Suppliers page** — the supplier management page is not yet built
- [ ] **Reports page** — build UI for trial balance, T-accounts, and general ledger
- [ ] **Real-time updates** — WebSocket or polling for multi-user environments

### Infrastructure

- [ ] **Docker / Docker Compose** — containerise the API and SQL Server for easy local setup
- [ ] **CI pipeline** — run the 82 integration tests on every pull request
- [ ] **Production deployment guide** — IIS / Kestrel behind reverse proxy, HTTPS, environment secrets

---

## Security Notes

1. **JWT secret** — the default key in `appsettings.json` is for development only; replace it before any shared deployment
2. **Connection string** — do not commit credentials; use environment variables or `dotnet user-secrets` in production
3. **HTTPS** — HTTPS redirection is off in development mode; enable it in production
4. **CORS** — currently allows `localhost:3000`; restrict to your actual frontend origin in production
5. **SQL injection** — all database access goes through EF Core parameterised queries

---

## Troubleshooting

**`dotnet ef database update` fails**
- Verify SQL Server is running: `Get-Service -Name "MSSQL$SQLEXPRESS"` (PowerShell)
- Check the connection string matches your instance name and credentials

**Port already in use**
```bash
netstat -ano | findstr :5000
taskkill /PID <PID> /F
```

**Tests fail with `ObjectDisposedException`**
- Do not modify `RequestResponseLoggingMiddleware` to buffer the response body — the current metadata-only implementation avoids stream lifecycle issues in the test host

**Circular reference error in JSON response**
- EF Core navigation property fixup can create cycles; `ReferenceHandler.IgnoreCycles` is already set in `Program.cs`

---

## License

© 2026 AccuBooks. All rights reserved.
