# Nexus - Insurance Commission Processing Platform

Backend service for ingestion, validation, and audit of insurance commission transactions.

## Overview

Nexus processes commission data from multiple carriers, detects duplicates and anomalies, and provides audit trails for compliance. The system groups transactions by carrier, policy number, premium amount, and commission on a per-day basis. Flagged transactions require manual review before approval.

## System Flow

```
1. Upload CSV file
   └─> Batch created

2. Background processing
   ├─> Extract and map fields
   ├─> Detect exact duplicates
   ├─> Identify suspicious amounts
   └─> Mark PII for masking

3. Results
   ├─> CLEAN: approved for billing
   ├─> SUSPECT: requires audit review
   └─> INVALID: rejected

4. Manual audit (if needed)
   └─> Approve or reject flagged items
```

## Requirements

- .NET 9.0 SDK
- SQL Server or Azure SQL Database
- OpenAI API Key (optional, for semantic analysis)

## Configuration

### Local Development Setup
1. Copy the example configuration as a template:
   ```bash
   cp src/Nexus.API/appsettings.example.json src/Nexus.API/appsettings.json
   ```

2. Initialize user secrets for sensitive configuration:
   ```bash
   cd src/Nexus.API
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:NexusDb" "Server=localhost;Database=NexusDb;User Id=sa;Password=YOUR_PASSWORD_HERE;TrustServerCertificate=True;"
   dotnet user-secrets set "Security:ApiKey" "your-secret-api-key"
   dotnet user-secrets set "OpenAi:ApiKey" "your-openai-api-key"
   cd ../..
   ```

### Production Configuration
Secrets are managed via:
- **Azure Key Vault** - For Azure deployments
- **AWS Secrets Manager** - For AWS deployments
- Environment variables - As fallback

Update [appsettings.example.json](src/Nexus.API/appsettings.example.json) with placeholder values for documentation.

## Run
From the repository root:
- `dotnet build`
- `dotnet run --project src/Nexus.API/Nexus.API.csproj`

Swagger UI will be available at `/swagger`.

---

## API Reference

Base URLs: `http://localhost:5000` (local) or `http://localhost:8080` (docker)

All endpoints except `/health` require header: `X-Api-Key: dev-test-key-12345`

### Upload Batch

```
POST /api/ingestion/upload
Content-Type: multipart/form-data

Form fields:
- File (required): CSV or Excel file
- SourceName (optional): Carrier name
- CarrierCode (optional): Carrier code
```

Response:
```json
{
  "batchId": "550e8400-e29b-41d4-a716-446655440000"
}
```

### Check Batch Status

```
GET /api/batches/{batchId}
```

Response:
```json
{
  "batchId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Completed",
  "totalRecords": 100,
  "cleanCount": 85,
  "duplicateCount": 10,
  "suspectCount": 5
}
```

Status values: Pending, Processing, Completed, Failed

### List Suspect Transactions

```
GET /api/audit/suspects
```

Response: Array of flagged transactions requiring review.

### Resolve Suspect Transaction

```
POST /api/audit/resolve
Content-Type: application/json

{
  "transactionId": "a1b2c3d4-e5f6-47g8-h9i0-j1k2l3m4n5o6",
  "approve": true,
  "reason": "Verified with carrier"
}
```

### Health Check

```
GET /api/health/database
```

No authentication required.

## Data Model

### Batch
- Id: GUID (primary key)
- SourceName: string (carrier name)
- UploadDate: DateTime
- Status: enum (Pending, Processing, Completed, Failed)
- Transactions: relationship to InsuranceTransaction[]

### InsuranceTransaction
- Id: GUID (primary key)
- BatchId: GUID (foreign key)
- CarrierCode: string
- PolicyNumber: string
- ExternalId: string
- GrossPremium: decimal (nullable)
- NetCommission: decimal
- TransactionDate: DateTime
- Status: enum (Clean, Suspect, Duplicate, Invalid, Pending)
- ConfidenceScore: decimal (0.0-1.0, nullable)
- PIIMasked: bool
- Notes: string (nullable)
- SanitizationLogs: relationship to SanitizationLog[]

### SanitizationLog
- Id: GUID (primary key)
- TransactionId: GUID (foreign key)
- DecisionType: enum (Auto, Manual, Ai)
- Reason: string
- CreatedAt: DateTime

## Duplicate Detection Logic

Transactions are grouped by:
- CarrierCode
- PolicyNumber
- GrossPremium
- NetCommission
- TransactionDate (date only, ignoring time)

Within each group, the first occurrence is marked as Clean. Subsequent occurrences are marked as Duplicate.

## CSV Format

Expected input columns (examples):
```
carrier_code,policy_number,gross_premium,commission,transaction_date
SF-001,POL-2024-001,5000.00,500.00,2024-05-01
SF-001,POL-2024-002,7500.00,750.00,2024-05-02
```

The system maps unknown headers to canonical fields using AI when mappings do not exist.

---
