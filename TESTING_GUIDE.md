# 🚀 Nexus API - Quick Start Testing Guide

## What is Nexus?

**Insurance Commission Processing Platform** - Automatically processes, validates, and sanitizes thousands of insurance transactions. Flags suspicious entries for manual audit review.

## Key Features

| Feature | What it does | Endpoint |
|---------|-------------|----------|
| **Upload** | Submit CSV/Excel file with transactions | `POST /api/ingestion/upload` |
| **Track** | Check processing status | `GET /api/batches/{id}` |
| **Review** | Get flagged transactions | `GET /api/audit/suspects` |
| **Audit** | Approve/reject suspicious items | `POST /api/audit/resolve` |
| **Health** | Verify API & DB connection | `GET /api/health/database` |

## Your API Key

```
X-Api-Key: dev-test-key-12345
```

## Quick Test (5 minutes)

### 1️⃣ Verify API is Running
```
GET http://localhost:8080/api/health/database

Headers:
- X-Api-Key: dev-test-key-12345
```

Expected Response:
```json
{
  "status": "Healthy",
  "databaseMetrics": {
    "totalBatches": 5,
    "totalTransactions": 1250
  }
}
```

### 2️⃣ Create a Test CSV File
`test-transactions.csv`:
```csv
PolicyNumber,CommissionAmount,TransactionDate,CarrierCode,AgentCode
POL-20240501-001,5000.00,2024-05-01,STATE-FARM,AG-001
POL-20240501-002,7500.50,2024-05-01,GEICO,AG-002
POL-20240501-003,3200.25,2024-05-01,STATE-FARM,AG-001
POL-20240502-001,9999.99,2024-05-02,ALLSTATE,AG-003
POL-20240502-002,500.00,2024-05-02,STATE-FARM,AG-001
POL-20240502-003,15000.00,2024-05-02,GEICO,AG-004
POL-20240503-001,4500.75,2024-05-03,ALLSTATE,AG-002
POL-20240503-002,8000.00,2024-05-03,STATE-FARM,AG-001
POL-20240503-003,6250.50,2024-05-03,GEICO,AG-003
POL-20240503-001,15000.00,2024-05-03,STATE-FARM,AG-004
```

### 3️⃣ Upload File
```
POST http://localhost:8080/api/ingestion/upload

Headers:
- X-Api-Key: dev-test-key-12345

Body: form-data
- File: [select test-transactions.csv]
- SourceName: "Test-Batch-001"
- CarrierCode: "TEST-001"
```

Response:
```json
{
  "batchId": "a1b2c3d4-e5f6-47g8-h9i0-j1k2l3m4n5o6"
}
```

💾 **Save the batchId!**

### 4️⃣ Check Status (Wait 5-10 seconds, then refresh)
```
GET http://localhost:8080/api/batches/a1b2c3d4-e5f6-47g8-h9i0-j1k2l3m4n5o6

Headers:
- X-Api-Key: dev-test-key-12345
```

Response:
```json
{
  "batchId": "a1b2c3d4-e5f6-47g8-h9i0-j1k2l3m4n5o6",
  "status": "Completed",
  "totalRecords": 10,
  "cleanCount": 7,
  "duplicateCount": 1,
  "suspectCount": 2
}
```

### 5️⃣ Review What Was Flagged
```
GET http://localhost:8080/api/audit/suspects

Headers:
- X-Api-Key: dev-test-key-12345
```

Response (example):
```json
[
  {
    "transactionId": "xyz-123",
    "policyNumber": "POL-20240502-003",
    "netCommission": 15000.00,
    "confidenceScore": 0.87,
    "reason": "Amount significantly higher than policy type average"
  },
  {
    "transactionId": "xyz-456",
    "policyNumber": "POL-20240503-001",
    "netCommission": 15000.00,
    "confidenceScore": 0.92,
    "reason": "Exact duplicate policy/amount/date found"
  }
]
```

### 6️⃣ Approve or Reject Each Flagged Item
```
POST http://localhost:8080/api/audit/resolve

Headers:
- X-Api-Key: dev-test-key-12345
- Content-Type: application/json

Body (JSON):
{
  "transactionId": "xyz-123",
  "approve": true,
  "reason": "Verified with State Farm - high-value policy renewal"
}
```

Repeat for each suspicious transaction, changing `transactionId` and `approve`.

---

## Understanding the Results

### Status Breakdown
After processing, transactions are categorized as:

| Status | Meaning | Action |
|--------|---------|--------|
| ✅ **Clean** | Legitimate transaction | Use for billing |
| ⚠️ **Suspect** | Needs review (high confidence issue) | Manual audit |
| ❌ **Duplicate** | Same transaction submitted twice | Discard |
| ❌ **Invalid** | Data error or rejected during audit | Discard |

### What Gets Flagged?
The system flags transactions when:
- 🤖 **Amount anomaly** - Commission outside expected range for policy type
- 📋 **Duplicate detected** - Same policy + amount + date in batch
- 🔐 **PII present** - Sensitive customer data detected (gets masked)
- 📊 **Confidence score low** - Data quality concerns

### Confidence Score
- **0.0 - 0.4** = Low concern (probably clean)
- **0.4 - 0.7** = Medium concern (review recommended)
- **0.7 - 1.0** = High concern (definitely review)

---

## Common Scenarios

### Scenario 1: All Transactions are Clean
```json
Status: "Completed"
"cleanCount": 100
"suspectCount": 0
"duplicateCount": 0
```
→ Your data is high quality! Ready for billing.

### Scenario 2: Some Duplicates Found
```json
"cleanCount": 95
"duplicateCount": 5
```
→ 5 duplicate entries were found and marked as `Duplicate` (auto-discarded).

### Scenario 3: Suspicious Amounts Detected
```json
"cleanCount": 90
"suspectCount": 10
```
→ 10 transactions flagged for manual review. Review them with `/audit/suspects`.

### Scenario 4: High Confidence Fraud
If AI detects something very suspicious:
```json
{
  "reason": "Amount 10x higher than historical average for agent",
  "confidenceScore": 0.96
}
```
→ Highly likely fraudulent. Recommend rejecting (`approve: false`).

---

## Troubleshooting

### ❌ "Invalid API key"
- Check you're using: `X-Api-Key: dev-test-key-12345`
- Make sure header name is exactly `X-Api-Key` (case matters)

### ❌ "Database connection failed"
- API is running in Docker but can't connect to Azure SQL
- Check your `.env` file has correct connection string
- Verify network access to Azure from your location

### ❌ "File upload failed"
- Ensure file is CSV format
- Check column names match expected format
- File should be < 50MB

### ❌ Batch status stays "Pending"
- Background worker may be processing
- Wait 30 seconds and check again
- Check Docker logs: `docker compose logs nexus-api`

---

## Database State

After testing, you can query the database directly:

**Count all clean transactions:**
```sql
SELECT COUNT(*) FROM InsuranceTransactions 
WHERE Status = 0  -- 0 = Clean
```

**Find all suspects:**
```sql
SELECT PolicyNumber, NetCommission, ConfidenceScore 
FROM InsuranceTransactions 
WHERE Status = 1  -- 1 = Suspect
ORDER BY ConfidenceScore DESC
```

---

## Next: Production-Ready Features

✅ Ready for testing:
1. End-to-end file processing
2. AI-powered fraud detection
3. Duplicate detection
4. Manual audit workflow
5. Database persistence

🔄 Can be implemented:
- Batch export to CSV/Excel
- Email notifications for flagged items
- Approval workflow automation
- Historical audit trail reports
- Integration with billing systems
- Role-based access control (admin/auditor/viewer)

---

Happy Testing! 🎉
