# AccuBooks - Double Entry Bookkeeping System

A professional-grade accounting software system built with ASP.NET Core backend and HTML/CSS/JavaScript frontend.

## Features

- **General Ledger Management**: Create and manage GL accounts with full hierarchy support
- **Daybook Entries**: Record sales, purchases, and journal entries
- **Financial Reports**: Trial balance, T-accounts, and general ledger reports
- **Customer & Supplier Management**: Tracksalesreceiivable and accounts payable with subsidiary accounts
- **Multi-User Support**: Organization-based data isolation and access control
- **Secure Authentication**: OAuth2-ready authentication system
- **Free & Paid Tiers**: Flexible subscription plans for different business sizes

## Project Structure

```
accounting/
├── accounting-backend/          # ASP.NET Core API
│   ├── Controllers/             # API Controllers
│   ├── Services/                # Business Logic
│   ├── Data/                    # Database Context & Migrations
│   ├── Models/                  # Data Models
│   ├── Program.cs               # Application Entry Point
│   ├── appsettings.json         # Configuration
│   └── AccountingApp.csproj     # Project File
│
└── accounting-frontend/         # HTML/CSS/JavaScript Frontend
    ├── pages/                   # Application Pages
    │   ├── login.html
    │   ├── register.html
    │   ├── dashboard.html
    │   ├── gl-accounts.html
    │   ├── daybook.html
    │   ├── reports.html
    │   ├── customers.html
    │   ├── suppliers.html
    │   └── organisations.html
    ├── js/                      # JavaScript Modules
    │   ├── api.js               # API Service & Utilities
    │   └── main.js              # Main Application Logic
    ├── css/                     # Stylesheets
    │   └── style.css            # Global Styles
    ├── index.html               # Landing Page
    └── images/                  # Assets
```

## Technology Stack

### Backend
- **Framework**: ASP.NET Core 8.0
- **Database**: SQL Server
- **ORM**: Entity Framework Core
- **Authentication**: JWT with OAuth2 support
- **API Documentation**: Swagger/OpenAPI

### Frontend
- **HTML5**: Semantic markup
- **CSS3**: Modern responsive design
- **JavaScript (ES6+)**: Vanilla JS with module pattern
- **No Dependencies**: Lightweight, zero external JS dependencies

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- SQL Server Express (download and install from Microsoft)
- SQL Server Management Studio (optional, for database management)
- A modern web browser
- A code editor (VS Code, Visual Studio, etc.)

### Backend Setup

1. **Install SQL Server Express**
   - Download from: https://www.microsoft.com/en-us/sql-server/sql-server-editions-express
   - Install with default settings
   - Note the server name (usually `.\SQLEXPRESS`)

2. **Configure Database Connection**
   - Open `accounting-backend/appsettings.json`
   - Update the connection string if needed:
     ```json
     "DefaultConnection": "Server=.\\SQLEXPRESS;Database=AccountingDB;Trusted_Connection=true;TrustServerCertificate=true;"
     ```

3. **Build and Run Backend**
   ```bash
   cd accounting-backend
   dotnet restore
   dotnet ef database update
   dotnet run
   ```
   - The API will be available at `https://localhost:5001` and `http://localhost:5000`
   - Swagger UI available at `http://localhost:5000/swagger`

### Frontend Setup

1. **Run Frontend Development Server**
   - Open `accounting-frontend/index.html` in a web browser
   - Or use a simple HTTP server:
     ```bash
     cd accounting-frontend
     python -m http.server 3000  # Python 3
     # or
     npx http-server -p 3000     # Node.js
     ```

2. **Access the Application**
   - Landing Page: `http://localhost:3000`
   - Login: `http://localhost:3000/pages/login.html`
   - Register: `http://localhost:3000/pages/register.html`

## Testing

### Running Integration Tests

The project includes integration tests using xUnit and WebApplicationFactory to verify API endpoints work correctly.

```bash
cd accounting-backend/Tests
dotnet test
```

#### Test Results
Tests verify:
- User registration and JWT token generation
- User authentication/login
- Organisation creation and retrieval
- Authenticated API requests

#### Notes
- Tests use an in-memory database (not SQL Server)
- HTTPS redirection is disabled in development/test environments
- All tests run in isolation with fresh data

#### Adding More Tests
Edit `Tests/IntegrationTests.cs` to add:
- GL Account operations
- Daybook entries
- Financial reports
- Customer/supplier management

Example test pattern:
```csharp
[Fact]
public async Task YourTestName()
{
    var client = _factory.CreateClient();
    var response = await client.PostAsJsonAsync("/api/endpoint", new { /* payload */ });
    response.EnsureSuccessStatusCode();
    // Assert response content
}
```

## API Endpoints

### Authentication
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - User login

### Organizations
- `POST /api/organisations` - Create organisation (creator becomes member)
- `GET /api/organisations` - List organisations the user belongs to
- `GET /api/organisations/{id}` - Get organisation details
- `PUT /api/organisations/{id}` - Update organisation

### GL Accounts
- `GET /api/organisations/{orgId}/glaccounts` - List accounts
- `POST /api/organisations/{orgId}/glaccounts` - Create account
- `GET /api/organisations/{orgId}/glaccounts/{accountId}` - Get account
- `PUT /api/organisations/{orgId}/glaccounts/{accountId}` - Update account
- `DELETE /api/organisations/{orgId}/glaccounts/{accountId}` - Delete account

### Daybook Entries
- `GET /api/organisations/{orgId}/daybook` - List entries
- `POST /api/organisations/{orgId}/daybook` - Create entry
- `GET /api/organisations/{orgId}/daybook/{entryId}` - Get entry
- `POST /api/organisations/{orgId}/daybook/{entryId}/post` - Post entry
- `DELETE /api/organisations/{orgId}/daybook/{entryId}` - Delete entry

### Reports
- `GET /api/organisations/{orgId}/reports/trial-balance` - Trial balance report
- `GET /api/organisations/{orgId}/reports/taccounts` - T-accounts report
- `GET /api/organisations/{orgId}/reports/general-ledger` - General ledger report

### Customers
- `GET /api/organisations/{orgId}/customers` - List customers
- `POST /api/organisations/{orgId}/customers` - Create customer
- `GET /api/organisations/{orgId}/customers/{customerId}` - Get customer
- `PUT /api/organisations/{orgId}/customers/{customerId}` - Update customer
- `DELETE /api/organisations/{orgId}/customers/{customerId}` - Delete customer

### Suppliers
- `GET /api/organisations/{orgId}/suppliers` - List suppliers
- `POST /api/organisations/{orgId}/suppliers` - Create supplier
- `GET /api/organisations/{orgId}/suppliers/{supplierId}` - Get supplier
- `PUT /api/organisations/{orgId}/suppliers/{supplierId}` - Update supplier
- `DELETE /api/organisations/{orgId}/suppliers/{supplierId}` - Delete supplier

## Database Schema

### Core Tables
- **Users**: User accounts and authentication
- **Organisations**: Business entities
- **OrganisationMembers**: User-Organization relationships
- **GLAccounts**: General ledger accounts
- **DaybookEntries**: Transaction records
- **JournalEntries**: Individual debit/credit lines
- **Customers**: Customer master data
- **Suppliers**: Supplier master data
- **AccountBalances**: Balance snapshots for reporting

## Key Concepts

### Double Entry Bookkeeping
Every transaction affects at least two accounts:
- One account is debited
- Another account is credited
- Total debits must equal total credits

### Account Types
- **Assets**: What the business owns
- **Liabilities**: What the business owes
- **Equity**: Owner's stake in the business
- **Revenue**: Money earned
- **Expenses**: Money spent

### Subsidiary Accounts
Customers and suppliers can have subsidiary accounts for tracking individual balances within general ledger control accounts (typically Accounts Receivable and Accounts Payable).

## Configuration

### JWT Settings (appsettings.json)
```json
"JwtSettings": {
  "SecretKey": "your-super-secret-key-change-in-production-12345",
  "ExpirationInMinutes": 60
}
```

### Database Connection
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=AccountingDB;Trusted_Connection=true;TrustServerCertificate=true;"
}
```

## Future Enhancements

- [ ] OAuth2 Integration (Google, Microsoft)
- [ ] Multi-currency Support
- [ ] Batch Import/Export
- [ ] Advanced Filtering & Search
- [ ] Mobile App
- [ ] Audit Trail
- [ ] Tax Compliance Reports
- [ ] Bank Reconciliation
- [ ] Inventory Management
- [ ] PDF Report Export

## Security Considerations

1. **Change JWT Secret**: Update the secret key in appsettings.json for production
2. **HTTPS**: Always use HTTPS in production
3. **CORS**: Configure CORS policies appropriately
4. **SQL Injection**: Using parameterized queries via EF Core
5. **Authentication**: Implement OAuth2 for enhanced security
6. **Rate Limiting**: Add API rate limiting in production
7. **Input Validation**: All inputs are validated

## Troubleshooting

### Database Issues
If tables don't appear in SQL Server Management Studio SSMS after running the application:
1. Verify the connection string in `appsettings.json` matches your SQL Server instance
2. Run `dotnet ef database update` to apply migrations
3. Check that LocalDB vs SQL Server Express aren't being mixed (see Program.cs fallback connection)

### HTTPS Redirection Warning
In development/testing environments, you may see:
```
Failed to determine the https port for redirect.
```
This is normal and expected. HTTPS redirection is disabled in development mode and only applies to production environments.

### Cascade Delete Constraints
If you get an error about multiple cascade paths when updating the database, ensure the OnModelCreating in ApplicationDbContext.cs doesn't have multiple CASCADE deletes from the same parent table. Use `OnDelete(DeleteBehavior.NoAction)` for relationships that would create circular paths.

### Database Connection Issues
1. Verify SQL Server is running
2. Check connection string in appsettings.json
3. Ensure database user has appropriate permissions

### CORS Errors
1. Frontend and backend must be on different ports or domains
2. CORS is configured in Program.cs
3. Update allowed origins if needed

### Port Already in Use
```bash
# Find process using port
netstat -ano | findstr :5000

# Kill process (Windows)
taskkill /PID <PID> /F
```

## License

© 2026 AccuBooks. All rights reserved.

## Support

For issues and feature requests, please contact the development team.
