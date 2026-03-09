# AccuBooks — Double-Entry Bookkeeping System

A professional-grade accounting backend built with ASP.NET Core 8, featuring full double-entry bookkeeping, multi-organisation support, and a comprehensive integration test suite.

---

## Features

- **General Ledger** — Create and manage GL accounts across five account types (Asset, Liability, Equity, Revenue, Expense)
- **Daybook Entries** — Record Sales, Purchase, Journal, Receipt, Payment, and Contra transactions with enforced debit/credit balance
- **Financial Reports** — Trial balance, T-accounts, and general ledger with date-range filtering; PDF and Excel export
- **Customer & Supplier Management** — Subsidiary ledger support linked to control accounts (AR/AP); outstanding invoices/bills view
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
│   ├── Migrations/                  # EF Core migrations (PostgreSQL)
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
│   ├── appsettings.json             # Local development defaults
│   ├── appsettings.Development.json # Dev-specific logging overrides
│   └── appsettings.Production.json  # Production overrides (fill in before deploying)
│
├── accounting-frontend/             # Vanilla HTML/CSS/JS frontend
│   ├── pages/                       # Application pages
│   ├── js/
│   │   ├── api.js                   # API service layer
│   │   ├── export.js                # PDF/Excel export utility
│   │   └── main.js
│   ├── css/style.css
│   └── index.html
│
└── accounting.sln
```

---

## Technology Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 |
| Database | PostgreSQL (Npgsql + EF Core 8, code-first migrations) |
| Authentication | JWT Bearer |
| Logging | Serilog (console + rolling file) |
| API Docs | Swagger / OpenAPI |
| Testing | xUnit, WebApplicationFactory, FluentAssertions |
| Frontend | Vanilla HTML5 / CSS3 / ES6+ JavaScript |
| PDF export | jsPDF + jsPDF-AutoTable (client-side, CDN) |
| Excel export | SheetJS / xlsx (client-side, CDN) |

---

## Local Development Setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PostgreSQL 14+ running locally (e.g. via [PostgreSQL installer](https://www.postgresql.org/download/) or Docker)

### 1. Create the database

Connect to your local PostgreSQL instance and create a database:

```sql
CREATE DATABASE "AccountingDB";
```

### 2. Configure the connection

Edit `accounting-backend/appsettings.json` with your local PostgreSQL credentials:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=AccountingDB;Username=postgres;Password=yourpassword"
},
"AllowedOrigins": [
  "http://localhost:3000",
  "https://localhost:3000"
],
"JwtSettings": {
  "SecretKey": "change-this-to-a-long-random-secret-in-development-min-32-chars",
  "ExpirationInMinutes": 60
}
```

### 3. Run the backend

```bash
cd accounting-backend

# Install EF Core CLI tool (first time only)
dotnet tool install -g dotnet-ef

# Apply migrations to create the schema
dotnet ef database update

# Start the API
dotnet run
```

The API will be available at `http://localhost:5000`.
Swagger UI: `http://localhost:5000/swagger`

### 4. Run the frontend

Open `accounting-frontend/index.html` directly in a browser, or serve it with Python:

```bash
cd accounting-frontend
python -m http.server 3000
# then open http://localhost:3000
```

---

## Hosted / Production Setup

### Prerequisites on your server

- Linux VPS (Ubuntu 22.04+ recommended) or Windows Server
- PostgreSQL 14+ (can be on the same server or a managed database)
- .NET 8 Runtime (or SDK if building on the server)
- A reverse proxy: **Nginx** (recommended) or Apache
- A domain name with SSL certificate (e.g. via Let's Encrypt / Certbot)

---

### Step 1 — Install .NET 8 Runtime (Ubuntu)

```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update && sudo apt install -y aspnetcore-runtime-8.0
```

---

### Step 2 — Install PostgreSQL

```bash
sudo apt install -y postgresql postgresql-contrib
sudo systemctl enable postgresql --now
```

Create the database and a dedicated user:

```bash
sudo -u postgres psql
```

```sql
CREATE USER accounting_user WITH PASSWORD 'a-strong-password-here';
CREATE DATABASE "AccountingDB" OWNER accounting_user;
GRANT ALL PRIVILEGES ON DATABASE "AccountingDB" TO accounting_user;
\q
```

---

### Step 3 — Publish the backend

Build and publish from your development machine (or on the server if .NET SDK is installed):

```bash
cd accounting-backend
dotnet publish -c Release -o ./publish
```

Copy the `publish/` folder to your server, e.g.:

```bash
scp -r ./publish user@your-server:/var/www/accubooks/
```

---

### Step 4 — Configure production settings

On the server, edit `appsettings.Production.json` inside the published folder:

```json
{
  "AllowedOrigins": [
    "https://your-frontend-domain.com"
  ],
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=AccountingDB;Username=accounting_user;Password=a-strong-password-here"
  },
  "JwtSettings": {
    "SecretKey": "replace-with-a-long-random-secret-min-32-characters"
  }
}
```

> **Generate a strong JWT secret:**
> ```bash
> openssl rand -base64 48
> ```

ASP.NET Core automatically merges `appsettings.Production.json` over `appsettings.json` when `ASPNETCORE_ENVIRONMENT=Production`.

**Alternative — use environment variables** (avoids secrets in files):

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=AccountingDB;Username=accounting_user;Password=..."
export JwtSettings__SecretKey="your-secret-here"
export AllowedOrigins__0="https://your-frontend-domain.com"
```

---

### Step 5 — Apply database migrations

On the server, run migrations before starting the app for the first time (and after each deployment):

```bash
cd /var/www/accubooks

# If dotnet-ef is installed on the server:
dotnet-ef database update --project AccountingApp.dll

# Or apply via the app itself (already configured in Program.cs — runs automatically on startup)
```

Migrations run automatically on startup via `dbContext.Database.Migrate()` in `Program.cs`, so for simple deployments you can skip the manual step — the schema will be created/updated when the app starts.

---

### Step 6 — Run as a systemd service

Create a service file:

```bash
sudo nano /etc/systemd/system/accubooks.service
```

```ini
[Unit]
Description=AccuBooks API
After=network.target postgresql.service

[Service]
WorkingDirectory=/var/www/accubooks
ExecStart=/usr/bin/dotnet /var/www/accubooks/AccountingApp.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=accubooks
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000

[Install]
WantedBy=multi-user.target
```

Enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable accubooks
sudo systemctl start accubooks
sudo systemctl status accubooks
```

Check logs:

```bash
sudo journalctl -u accubooks -f
```

---

### Step 7 — Configure Nginx as a reverse proxy

```bash
sudo apt install -y nginx
sudo nano /etc/nginx/sites-available/accubooks
```

```nginx
server {
    listen 80;
    server_name api.your-domain.com;

    location / {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/accubooks /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

### Step 8 — Enable HTTPS with Let's Encrypt

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d api.your-domain.com
sudo systemctl reload nginx
```

Certbot automatically renews certificates. HTTPS redirection is enabled automatically in production mode by `Program.cs`.

---

### Step 9 — Serve the frontend

The frontend is plain static files. Copy `accounting-frontend/` to your server and serve it with Nginx:

```nginx
server {
    listen 80;
    server_name your-frontend-domain.com;

    root /var/www/accubooks-frontend;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

Update `accounting-frontend/js/api.js` before deploying — set `BASE_URL` to your API domain:

```js
const BASE_URL = 'https://api.your-domain.com';
```

Then run `certbot --nginx -d your-frontend-domain.com` for HTTPS on the frontend too.

---

## Running Tests

Tests use an in-memory database — no PostgreSQL required.

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

All endpoints except `/api/auth/*` require an `Authorization: Bearer <token>` header.

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
| GET | `/customers/outstanding` | All unpaid invoices across all customers |
| GET | `/suppliers/outstanding` | All unpaid bills across all suppliers |

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

All required values are read from `appsettings.json` (with production overrides in `appsettings.Production.json`). The app will throw an `InvalidOperationException` at startup if any required key is missing, rather than starting with a broken configuration.

| Key | Required | Description |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Yes | PostgreSQL connection string |
| `AllowedOrigins` | Yes | Array of CORS-allowed frontend origins |
| `JwtSettings:SecretKey` | Yes | JWT signing key (min 32 characters) |
| `JwtSettings:ExpirationInMinutes` | No | Token lifetime (default: 60) |

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

### Backend

- [ ] **Role-based authorisation** — enforce Owner/Admin/Member permissions on write operations
- [ ] **Pagination** — add `page` / `pageSize` query parameters to all list endpoints
- [ ] **Audit trail** — record who created or modified each record and when
- [ ] **OAuth2 login** — Google and Microsoft sign-in (stubs exist in config and User model)
- [ ] **Rate limiting** — protect public auth endpoints from brute-force
- [ ] **Refresh tokens** — replace short-lived JWTs with a refresh-token flow
- [ ] **Multi-currency** — store currency on transactions and convert for reporting
- [ ] **Bank reconciliation** — match bank statement lines to posted daybook entries
- [ ] **Tax compliance reports** — VAT/GST summary reports
- [ ] **Batch import/export** — CSV upload for bulk GL accounts and transactions

### Infrastructure

- [ ] **Docker / Docker Compose** — containerise the API and PostgreSQL for easy local setup
- [ ] **CI pipeline** — run the 82 integration tests on every pull request

---

## Security Notes

1. **JWT secret** — never use the placeholder value from `appsettings.json` in production; generate a random secret (`openssl rand -base64 48`) and store it in `appsettings.Production.json` or an environment variable
2. **Connection string** — do not commit credentials; use `appsettings.Production.json` (keep out of source control) or environment variables
3. **HTTPS** — HTTPS redirection is off in Development mode; it is enabled automatically in Production via `Program.cs`
4. **CORS** — `AllowedOrigins` in `appsettings.json` defaults to localhost; always set your real frontend origin in production
5. **PostgreSQL user** — create a dedicated database user with only the permissions needed (not the `postgres` superuser)
6. **SQL injection** — all database access goes through EF Core parameterised queries

---

## Troubleshooting

**App fails to start with `InvalidOperationException`**
- A required config key is missing. Check that `AllowedOrigins`, `ConnectionStrings:DefaultConnection`, and `JwtSettings:SecretKey` are all present in your active `appsettings.json` or environment variables.

**`dotnet ef database update` fails**
- Verify PostgreSQL is running: `sudo systemctl status postgresql`
- Check that the connection string in `appsettings.json` matches your host, port, username, and password
- Ensure the database exists: `psql -U postgres -c "\l"`

**Port already in use**

```bash
# Linux
sudo ss -tlnp | grep 5000
sudo kill <PID>

# Windows
netstat -ano | findstr :5000
taskkill /PID <PID> /F
```

**Tests fail with `ObjectDisposedException`**
- Do not modify `RequestResponseLoggingMiddleware` to buffer the response body — the current metadata-only implementation avoids stream lifecycle issues in the test host

**Circular reference error in JSON response**
- EF Core navigation property fixup can create cycles; `ReferenceHandler.IgnoreCycles` is already set in `Program.cs`

**Nginx returns 502 Bad Gateway**
- Check the app is running: `sudo systemctl status accubooks`
- Verify the port in your Nginx config matches `ASPNETCORE_URLS` in the service file

---

## License

© 2026 AccuBooks. All rights reserved.
