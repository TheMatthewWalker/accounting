# Database Setup Guide

## SQL Server Installation

### Step 1: Download SQL Server Express

1. Go to: https://www.microsoft.com/en-us/sql-server/sql-server-editions-express
2. Click "Download now"
3. Run the installer

### Step 2: Install SQL Server

1. Select "Basic" installation type
2. Accept license terms
3. Accept default settings:
   - Install Location: Default
   - Instance: SQLEXPRESS
   - Service Account: NT SERVICE\SQLEXPRESS
   - Authentication: Mixed Mode (recommended for development)
     - Enter SA password (admin account)
     - Keep it safe!

4. Click Install and wait for completion

### Step 3: Install SQL Server Management Studio (Optional but Recommended)

1. Go to: https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms
2. Download the latest version
3. Run installer and follow prompts
4. Open SSMS after installation

### Step 4: Verify Installation

**Using PowerShell:**
```powershell
sqlcmd -S .\SQLEXPRESS -U sa -P <your_sa_password>
1> SELECT @@VERSION;
2> GO
```

**Using SSMS:**
1. Open SQL Server Management Studio
2. Server name: `.\SQLEXPRESS`
3. Authentication: Windows Authentication (or SQL Server with sa account)
4. Click Connect

## Database Creation

The database will be created automatically when you run the backend with Entity Framework migrations.

### Manual Creation (Optional)

If you want to create it manually:

```sql
CREATE DATABASE AccountingDB
GO

USE AccountingDB
GO
```

## Connection String

The connection string in `appsettings.json` should be:

**For Windows Authentication:**
```
Server=.\SQLEXPRESS;Database=AccountingDB;Trusted_Connection=true;TrustServerCertificate=true;
```

**For SQL Server Authentication (using sa):**
```
Server=.\SQLEXPRESS;Database=AccountingDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;
```

## Running Migrations

Once connected, run these commands from the backend folder:

```bash
cd accounting-backend

# Install Entity Framework CLI (if not already installed)
dotnet tool install -g dotnet-ef

# Update database (creates schema)
dotnet ef database update

# To create a new migration after model changes:
dotnet ef migrations add MigrationName

# To undo last migration:
dotnet ef database update PreviousMigration
```

## Database Tables

The following tables are automatically created:

1. **Users**
   - Id (GUID)
   - Email (unique)
   - FirstName, LastName
   - PasswordHash
   - GoogleId, MicrosoftId (for OAuth2)
   - CreatedAt, LastLogin
   - IsActive

2. **Organisations**
   - Id (GUID)
   - Name, Description
   - RegistrationNumber, TaxNumber
   - SubscriptionTier
   - CreatedAt, SubscriptionExpiresAt
   - IsActive

3. **OrganisationMembers**
   - Id (GUID)
   - UserId, OrganisationId
   - Role (Owner, Admin, Member)
   - JoinedAt, IsActive

4. **GLAccounts**
   - Id (GUID)
   - OrganisationId
   - Code, Name
   - Type (Asset, Liability, Equity, Revenue, Expense)
   - SubType
   - OpeningBalance
   - CreatedAt, IsActive

5. **DaybookEntries**
   - Id (GUID)
   - OrganisationId
   - Type (Sales, Purchase, Journal, etc.)
   - ReferenceNumber
   - EntryDate
   - Description
   - IsPosted
   - CreatedAt, CreatedBy

6. **JournalEntries**
   - Id (GUID)
   - DaybookEntryId, GLAccountId
   - DebitAmount, CreditAmount
   - NarrationLine

7. **Customers**
   - Id (GUID)
   - OrganisationId
   - Name, Email, Phone
   - Address, City, PostalCode, Country
   - ControlAccountId (link to AR GL account)
   - CreditLimit
   - CreatedAt, IsActive

8. **Suppliers**
   - Id (GUID)
   - OrganisationId
   - Name, Email, Phone
   - Address, City, PostalCode, Country
   - ControlAccountId (link to AP GL account)
   - CreditLimit
   - CreatedAt, IsActive

9. **AccountBalances**
   - Id (GUID)
   - GLAccountId
   - BalanceDate
   - Balance, Debit, Credit

## Backup & Recovery

### Backup Database

**Using SSMS:**
1. Right-click DatabaseName → Tasks → Back Up...
2. Select backup file location
3. Click OK

**Using T-SQL:**
```sql
BACKUP DATABASE [AccountingDB] 
TO DISK = 'C:\Backups\AccountingDB.bak'
GO
```

### Restore Database

**Using SSMS:**
1. Right-click Databases → Restore Database
2. Select device and backup file
3. Click OK

**Using T-SQL:**
```sql
RESTORE DATABASE [AccountingDB]
FROM DISK = 'C:\Backups\AccountingDB.bak'
GO
```

## Troubleshooting

### SQL Server Service Not Running

```powershell
# Start the service
net start MSSQL$SQLEXPRESS

# Stop the service
net stop MSSQL$SQLEXPRESS
```

### Can't Connect

1. Verify service is running:
   ```powershell
   Get-Service -Name "MSSQL$SQLEXPRESS"
   ```

2. Check connection string in appsettings.json

3. Verify firewall allows SQL Server (port 1433)

### Database Already Exists Error

Drop and recreate:
```sql
USE master
GO
DROP DATABASE [AccountingDB]
GO
```

Then run migrations again.

### Mixed Mode Not Enabled

Modify SQL Server Configuration Manager to enable SQL Server Authentication:
1. Open "SQL Server Configuration Manager"
2. Right-click Server instance → Properties
3. Security tab → Server authentication → Select "SQL Server and Windows Authentication mode"
4. Restart SQL Server service

## Performance Tips

For production deployments:

1. **Create Indexes** on frequently queried columns
```sql
CREATE INDEX idx_glaccount_org ON GLAccounts(OrganisationId);
CREATE INDEX idx_daybook_org ON DaybookEntries(OrganisationId, EntryDate);
```

2. **Update Statistics**
```sql
UPDATE STATISTICS GLAccounts;
UPDATE STATISTICS DaybookEntries;
```

3. **Archive Old Data** periodically to Archive tables

4. **Monitor Growth**
```sql
SELECT 
    name,
    size * 8 as 'Size (KB)',
    FILEPROPERTY(name, 'SpaceUsed') * 8 as 'Used (KB)'
FROM sysfiles
GO
```

## Next Steps

1. **Verify connection** works with the backend
2. **Run initial migrations** to create schema
3. **Test API endpoints** with Swagger UI
4. **Set up authentication** and OAuth2 credentials
5. **Configure backups** for production data safety
