# Nexus - Insurance Commission Processing Platform

Backend service for insurance commission ingestion, sanitization, and audit workflows.

## 🎯 What Problem Does Nexus Solve?

**Business Problem:** Insurance companies receive thousands of commission transactions from various carriers. These transactions often contain:
- Duplicate entries (same policy, same date)
- Suspicious amounts (potentially fraudulent or data errors)
- Personally Identifiable Information (PII) that needs masking
- Data quality issues requiring manual review

**Nexus Solution:** Automatically process, validate, and sanitize insurance commission data with AI-powered fraud detection.

## 📊 System Architecture

```
Upload CSV/Excel File
        ↓
    [Batch Created & Queued]
        ↓
   [Background Processing]
   - Extract transactions
   - Validate data format
   - Check for duplicates
   - Detect suspicious amounts (AI)
   - Mask PII
   - Log sanitization
        ↓
   [3 Possible Outcomes]
   - ✅ CLEAN: Trusted transaction
   - ⚠️ SUSPECT: Needs manual review
   - ❌ INVALID: Duplicate or error
        ↓
   [Manual Audit Review]
   - Approve suspicious
   - Reject fraudulent
   - Track decisions
        ↓
    [Data Ready for Billing/BI]
```

## Requirements
- .NET 9 SDK
- SQL Server / Azure SQL Database
- OpenAI API Key (for fraud detection)

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

## 🔌 API Endpoints Guide

### Base URL
- **Local:** `http://localhost:5000`
- **Docker:** `http://localhost:8080`

### Authentication
All endpoints (except `/health`) require the API Key header:
```
X-Api-Key: dev-test-key-12345
```

---

## 📤 1. Upload File (Ingestion)

**Purpose:** Submit a batch of insurance transactions for processing

**Endpoint:** 
```
POST /api/ingestion/upload
```

**Headers:**
```
X-Api-Key: dev-test-key-12345
```

**Body Type:** `form-data` (NOT JSON)

**Form Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `File` | File | ✅ Yes | CSV or Excel file |
| `SourceName` | Text | ❌ No | Name of the carrier (e.g., "State Farm", "Geico") |
| `CarrierCode` | Text | ❌ No | Carrier code for tracking |

**Thunder Client Example:**
```
1. Method: POST
2. URL: http://localhost:8080/api/ingestion/upload
3. Headers tab:
   - X-Api-Key: dev-test-key-12345
4. Body tab → Select "form-data"
5. Add fields:
   - Key: "File", Type: file, Value: [select your CSV]
   - Key: "SourceName", Type: text, Value: "State Farm"
   - Key: "CarrierCode", Type: text, Value: "SF-001"
6. Click "Send"
```

**Success Response (200 OK):**
```json
{
  "batchId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Save this Batch ID** - you'll use it to check status!

---

## 📊 2. Check Batch Status

**Purpose:** Track the processing progress of your batch

**Endpoint:**
```
GET /api/batches/{batchId}
```

**Headers:**
```
X-Api-Key: dev-test-key-12345
```

**Thunder Client Example:**
```
1. Method: GET
2. URL: http://localhost:8080/api/batches/550e8400-e29b-41d4-a716-446655440000
3. Headers tab:
   - X-Api-Key: dev-test-key-12345
4. Click "Send"
```

**Response (200 OK):**
```json
{
  "batchId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Processing",
  "totalRecords": 150,
  "cleanCount": 120,
  "duplicateCount": 15,
  "suspectCount": 15
}
```

**Status Values:**
- `Pending` - Waiting to process
- `Processing` - Currently analyzing
- `Completed` - Done (check results)
- `Failed` - Error occurred

**Result Breakdown:**
- `cleanCount` ✅ - Approved transactions (ready for billing)
- `duplicateCount` ❌ - Duplicate entries (discarded)
- `suspectCount` ⚠️ - Needs manual review

---

## 🚨 3. Get Suspicious Transactions (Audit)

**Purpose:** Retrieve all transactions flagged for manual review

**Endpoint:**
```
GET /api/audit/suspects
```

**Headers:**
```
X-Api-Key: dev-test-key-12345
```

**Thunder Client Example:**
```
1. Method: GET
2. URL: http://localhost:8080/api/audit/suspects
3. Headers tab:
   - X-Api-Key: dev-test-key-12345
4. Click "Send"
```

**Response (200 OK):**
```json
[
  {
    "transactionId": "a1b2c3d4-e5f6-47g8-h9i0-j1k2l3m4n5o6",
    "policyNumber": "POL-2024-001",
    "netCommission": 15000.50,
    "transactionDate": "2024-05-01T10:30:00Z",
    "confidenceScore": 0.8742,
    "reason": "Amount exceeds threshold for policy type",
    "notes": "Possible data entry error or high-value transaction"
  },
  {
    "transactionId": "b2c3d4e5-f6g7-48h9-i0j1-k2l3m4n5o6p7",
    "policyNumber": "POL-2024-002",
    "netCommission": 8500.00,
    "transactionDate": "2024-05-01T14:15:00Z",
    "confidenceScore": 0.6234,
    "reason": "Similar transaction exists within 24 hours",
    "notes": "Potential duplicate from different system"
  }
]
```

**Understanding the Data:**
- `confidenceScore` (0.0 - 1.0) - Likelihood of issue (0.5+ = suspicious)
- `reason` - Why it was flagged
- You'll manually review each one and approve/reject

---

## ✅/❌ 4. Manually Resolve a Suspect Transaction

**Purpose:** Auditor reviews a suspicious transaction and approves or rejects it

**Endpoint:**
```
POST /api/audit/resolve
```

**Headers:**
```
X-Api-Key: dev-test-key-12345
Content-Type: application/json
```

**Body (JSON):**
```json
{
  "transactionId": "a1b2c3d4-e5f6-47g8-h9i0-j1k2l3m4n5o6",
  "approve": true,
  "reason": "Verified with carrier - legitimate high-value commission"
}
```

**Thunder Client Example:**
```
1. Method: POST
2. URL: http://localhost:8080/api/audit/resolve
3. Headers tab:
   - X-Api-Key: dev-test-key-12345
   - Content-Type: application/json
4. Body tab → Select "JSON"
5. Paste the JSON above
6. Click "Send"
```

**Possible Values:**
- `approve: true` → Transaction marked as `Clean` (use for billing)
- `approve: false` → Transaction marked as `Invalid` (discard)

**Response (200 OK):**
```json
{}
```
(Empty response means success)

---

## 🏥 5. Health Check (Monitoring)

**Purpose:** Verify API and database connectivity

**Endpoint:**
```
GET /api/health/database
```

**No authentication required** ✅

**Thunder Client Example:**
```
1. Method: GET
2. URL: http://localhost:8080/api/health/database
3. Headers: (none needed)
4. Click "Send"
```

**Response (200 OK - Database Healthy):**
```json
{
  "status": "Healthy",
  "message": "Database connection successful",
  "timestamp": "2026-05-04T02:30:00Z",
  "databaseMetrics": {
    "totalBatches": 5,
    "totalTransactions": 1250
  }
}
```

**Response (500 OK - Database Unhealthy):**
```json
{
  "status": "Unhealthy",
  "message": "Database connection failed: ...",
  "timestamp": "2026-05-04T02:30:00Z",
  "errorDetails": "SqlException"
}
```

---

## 🔄 Complete Workflow Example

### Step 1: Upload a CSV file
```
POST /api/ingestion/upload
(form-data with your CSV file)
↓
Response: { "batchId": "550e8400-e29b-41d4-a716-446655440000" }
```

### Step 2: Check processing status (poll every 5-10 seconds)
```
GET /api/batches/550e8400-e29b-41d4-a716-446655440000
↓
Response shows: cleanCount: 120, suspectCount: 15
(Wait until status = "Completed")
```

### Step 3: Review suspicious transactions
```
GET /api/audit/suspects
↓
Response: Array of 15 suspicious transactions
```

### Step 4: Approve or reject each suspicious transaction
```
POST /api/audit/resolve
{
  "transactionId": "a1b2c3d4...",
  "approve": true,
  "reason": "Verified with carrier"
}
↓
(Repeat for each flagged transaction)
```

### Step 5: Export clean data
```
Query the database directly or use BI tools to:
- Fetch all transactions where Status = "Clean"
- Ready for billing system
```

---

## 🗂️ Data Model

### Batch Entity
```
Batch
├── Id (GUID)
├── SourceName (string) - Carrier name
├── UploadDate (DateTime)
├── Status (BatchStatus enum)
│   ├── Pending
│   ├── Processing
│   ├── Completed
│   └── Failed
└── Transactions (1-to-many)
```

### InsuranceTransaction Entity
```
InsuranceTransaction
├── Id (GUID)
├── BatchId (GUID) - References Batch
├── PolicyNumber (string)
├── NetCommission (decimal)
├── TransactionDate (DateTime)
├── ConfidenceScore (decimal 0-1)
├── Status (TransactionStatus enum)
│   ├── Clean ✅
│   ├── Suspect ⚠️
│   ├── Duplicate ❌
│   └── Invalid ❌
└── SanitizationLogs (1-to-many)
```

### SanitizationLog Entity
```
SanitizationLog
├── Id (GUID)
├── TransactionId (GUID)
├── Reason (string) - Why was it flagged?
├── DecisionType (SanitizationDecisionType)
│   ├── Automatic
│   └── Manual
├── CreatedAt (DateTime)
└── PiiMasked (bool) - Was sensitive data masked?
```

---

## 🔍 Example CSV Format

Your input CSV should look like:
```csv
PolicyNumber,CommissionAmount,TransactionDate,CarrierCode,AgentCode
POL-2024-001,15000.50,2024-05-01,SF-001,AGENT-123
POL-2024-002,8500.00,2024-05-01,SF-001,AGENT-456
POL-2024-003,12000.75,2024-05-02,AH-002,AGENT-123
```

The system will:
1. ✅ Validate format
2. ✅ Check for duplicates
3. ✅ Mask sensitive fields
4. 🤖 Use AI to detect suspicious amounts
5. 📊 Generate audit report

---

## 🚀 Next Steps for Testing

1. **Prepare a test CSV** with 10-20 transaction records
2. **Upload it** via POST `/api/ingestion/upload`
3. **Monitor** with GET `/api/batches/{batchId}`
4. **Review suspects** with GET `/api/audit/suspects`
5. **Resolve each** with POST `/api/audit/resolve`
6. **Verify database** with GET `/api/health/database`

---
