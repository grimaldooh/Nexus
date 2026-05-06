# 🧪 Complete System Testing Guide - Step by Step

This guide walks through testing the entire Nexus platform from unit tests → integration tests → end-to-end API testing.

---

## Phase 1: Unit Testing (Local Machine)

### Why This Phase?
Unit tests verify individual components work correctly in isolation. They're the foundation - if units test pass, we know the business logic is sound before testing with databases or external APIs.

### Step 1.1: Run All Unit Tests

**Command:**
```bash
cd /Users/angel/Documents/Nexus
dotnet test
```

**Why this command?**
- `dotnet test` discovers and runs all `.Tests` projects in the solution
- Runs in parallel across CPU cores (fast)
- Does NOT require database or Docker running
- Uses mocked dependencies (HttpClient, DbContext, Logger)

**What Happens:**
- 📊 Test runner loads all test projects
- 🔍 Discovers ~50+ unit tests across:
  - `Nexus.Domain.Tests` - Entity validation
  - `Nexus.Application.Tests` - Business logic validators
  - `Nexus.Infrastructure.Tests` - Service layer (including NEW AiIntegrityServiceTests)
  - `Nexus.API.Tests` - Controller and middleware tests
- ⚙️ Each test executes mocked dependencies (no real DB calls, no real API calls)
- ✅ Generates results showing pass/fail for each test

**Expected Output (excerpt):**
```
Nexus.Infrastructure.Tests > Nexus.Infrastructure.Tests.Services.AiIntegrityServiceTests > TryMapUnknownAsync_WithValidCsvHeaders_ReturnsMappedRecord PASSED [ 234 ms ]
Nexus.Infrastructure.Tests > Nexus.Infrastructure.Tests.Services.AiIntegrityServiceTests > TryMapUnknownAsync_WithMissingApiKey_ReturnsNull PASSED [ 45 ms ]
...
Total Tests: 50
Passed: 50
Failed: 0
```

**✅ Success Criteria:**
- All tests pass (green ✓)
- No errors or warnings
- Build time < 30 seconds

---

### Step 1.2: Run Only AiIntegrityService Tests

**Command:**
```bash
cd /Users/angel/Documents/Nexus
dotnet test --filter "AiIntegrityService"
```

**Why this command?**
- `--filter` runs only tests matching the pattern
- Tests the NEW Smart Mapping service thoroughly
- Fast feedback on AI integration logic

**What Happens:**
- 🧠 Runs 11 new tests covering:
  - ✅ Happy path: Valid CSV → AI mapping → Database persistence
  - ❌ Error cases: Missing API key, HTTP errors, invalid JSON, empty responses
  - 🔄 Edge cases: Complex headers, markdown-formatted JSON, required field marking
  - 🧹 Cleanup: Filters empty/null mappings

**Tests Included:**
1. `TryMapUnknownAsync_WithValidCsvHeaders_ReturnsMappedRecord` - Main success scenario
2. `TryMapUnknownAsync_WithMissingApiKey_ReturnsNull` - No API key configured
3. `TryMapUnknownAsync_WithPlaceholderApiKey_ReturnsNull` - Unconfigured placeholder key
4. `TryMapUnknownAsync_WithEmptyHeaders_ReturnsNull` - CSV has no headers
5. `TryMapUnknownAsync_WithOpenAiHttpError_ReturnsNull` - OpenAI returns 401/500
6. `TryMapUnknownAsync_WithHttpException_ReturnsNull` - Network timeout/error
7. `TryMapUnknownAsync_WithInvalidJsonResponse_ReturnsNull` - Malformed JSON from OpenAI
8. `TryMapUnknownAsync_WithEmptyMappings_ReturnsNull` - OpenAI returns empty array
9. `TryMapUnknownAsync_WithComplexHeaders_SuccessfullyMapsAllFields` - 6+ headers with extra fields
10. `TryMapUnknownAsync_WithJsonMarkdownFormatted_StripsMarkdownAndParsesSuccessfully` - Markdown-wrapped JSON
11. `TryMapUnknownAsync_MarksPolicyNumberAndExternalIdAsRequired` - Validates required field marking

**Expected Output:**
```
Passed: 11
Failed: 0
Total: 11
```

**✅ Success Criteria:**
- All 11 AiIntegrityService tests pass
- Proves Smart Mapping handles errors gracefully

---

## Phase 2: Docker Integration Testing

### Why This Phase?
Docker testing verifies:
1. Application runs in container (proves Dockerfile is correct)
2. Database connection works (Azure SQL is accessible from container)
3. All services are wired correctly (DI, configuration, logging)
4. OpenAI integration works with real HTTPS calls

### Step 2.1: Build and Start Docker Container

**Command:**
```bash
cd /Users/angel/Documents/Nexus
docker compose up --build
```

**Why this command?**
- `--build` rebuilds the image (applies code changes)
- `docker compose up` starts all services defined in docker-compose.yml
- Runs in foreground so you see real-time logs

**What Happens Internally:**
1. 🏗️ **Build Stage (2-3 seconds)**
   - Docker loads the Dockerfile
   - Compiles .NET code (`dotnet build` inside container)
   - Publishes release build (`dotnet publish`)
   
2. 🚀 **Startup Stage (2-5 seconds)**
   - Loads docker-compose.yml configuration
   - Reads `.env` file for secrets (API_KEY, OPENAI_API_KEY, DATABASE_CONNECTION_STRING)
   - Sets environment variables in container
   - Starts ASP.NET Core on port 5000
   
3. 🔌 **Database Connection**
   - First HTTP request triggers Entity Framework migrations
   - Creates schema in Azure SQL (if not exists)
   - Ensures tables (InsuranceTransactions, Batches, CarrierMappings) exist

**Expected Console Output (excerpt):**
```
Building 74e5f7ce4cd6
Step 1/10 : FROM mcr.microsoft.com/dotnet/sdk:9.0 as build
...
Step 10/10 : ENTRYPOINT ["dotnet", "Nexus.API.dll"]
...
Nexus.API listening on http://0.0.0.0:5000
```

**✅ Success Criteria:**
- No build errors
- Container starts without crashes
- Can see "listening on http://0.0.0.0:5000" in logs

---

### Step 2.2: Health Check - Verify Container is Running

**Command (in NEW terminal):**
```bash
curl http://localhost:8080/api/health/ping
```

**Why this command?**
- Tests the `/api/health/ping` endpoint (requires NO API key)
- Lightweight health check - doesn't query database
- Proves traffic is reaching the application

**What Happens:**
1. 🌐 cURL sends HTTP GET to localhost:8080 (maps to container port 5000)
2. ✅ HealthController receives request
3. 📤 Returns immediate 200 OK response with timestamp

**Expected Response:**
```json
{
  "status": "OK",
  "timestamp": "2024-05-03T10:30:45.123Z",
  "message": "API is running"
}
```

**✅ Success Criteria:**
- HTTP 200 response
- No error messages

---

### Step 2.3: Database Health Check

**Command:**
```bash
curl http://localhost:8080/api/health/database
```

**Why this command?**
- Tests the `/api/health/database` endpoint
- Queries Azure SQL database
- Proves database connection works from container
- Validates table structure

**What Happens:**
1. 🌐 Request reaches HealthController
2. 🔍 Executes queries to count:
   - Rows in `Batches` table
   - Rows in `InsuranceTransactions` table
3. 📊 Calculates metrics (counts, timestamp)
4. 📤 Returns JSON with metrics

**Expected Response:**
```json
{
  "status": "OK",
  "message": "Database is accessible",
  "timestamp": "2024-05-03T10:30:50.456Z",
  "databaseMetrics": {
    "batchesCount": 0,
    "transactionsCount": 0,
    "timestamp": "2024-05-03T10:30:50.456Z"
  }
}
```

**✅ Success Criteria:**
- HTTP 200 response
- `databaseMetrics` shows counts (even if 0)
- Proves Azure SQL connection works

---

## Phase 3: API Integration Testing with Real Data

### Why This Phase?
This phase tests the complete workflow:
- CSV file upload
- Unknown carrier detection
- OpenAI Smart Mapping call
- Database persistence
- Response correctness

### Step 3.1: Prepare Test CSV File

**Create file: `/tmp/test_carrier.csv`**

```csv
codigo_referencia,numero_poliza,monto_prima_bruta,comision_agente_neta,fecha_vigencia_efectiva,identificador_transaccion,notas_internas
EXT-TST-001,POL-2024-100,10000.00,1000.00,2024-05-01,TXN-ABC-001,Nueva póliza
EXT-TST-002,POL-2024-101,15000.00,1500.00,2024-05-02,TXN-ABC-002,Renovación con aumento
EXT-TST-003,POL-2024-102,8500.00,850.00,2024-05-03,TXN-ABC-003,Descuento especial aplicado
```

**Why this CSV?**
- Uses **Spanish column names** (unknown to system)
- Has 3 rows of insurance transaction data
- Includes all 6 required fields with realistic values
- Matches real-world carrier format variation

**Headers explanation:**
- `codigo_referencia` → should map to **ExternalId**
- `numero_poliza` → should map to **PolicyNumber**
- `monto_prima_bruta` → should map to **GrossPremium**
- `comision_agente_neta` → should map to **NetCommission**
- `fecha_vigencia_efectiva` → should map to **TransactionDate**
- `identificador_transaccion` and `notas_internas` → bonus fields (may be ignored by OpenAI)

---

### Step 3.2: Upload CSV and Trigger Smart Mapping

**Command:**
```bash
curl -X POST http://localhost:8080/api/ingestion/upload \
  -H "X-Api-Key: dev-test-key-12345" \
  -F "file=@/tmp/test_carrier.csv" \
  -F "sourceName=TestCarrier" \
  -F "carrierCode=TEST-CARRIER-001"
```

**Why this command?**
- Uses correct API endpoint `/api/ingestion/upload`
- Includes required `X-Api-Key` header (matches config)
- Sends multipart form data:
  - **file**: CSV content
  - **sourceName**: Human-readable source name
  - **carrierCode**: Unique identifier (used to cache mappings)

**What Happens Inside the System (30 seconds):**

```
1️⃣  CSV Parsing (1 sec)
    - CsvHelper reads file
    - Extracts headers: [codigo_referencia, numero_poliza, ...]
    - Validates columns exist

2️⃣  Carrier Lookup (1 sec)
    - Searches CarrierMappings table
    - Query: WHERE CarrierCode = 'TEST-CARRIER-001'
    - Result: No mappings found (first time seeing this carrier)

3️⃣  Smart Mapping Triggered (15-20 sec)
    - System calls AiIntegrityService.TryMapUnknownAsync()
    - Builds prompt with discovered headers
    - Sends HTTPS request to OpenAI API with:
      * Model: gpt-4o-mini
      * Temperature: 0.1 (deterministic)
      * Prompt: "Map these Spanish headers to our canonical fields"
    
4️⃣  OpenAI Responds (5-10 sec)
    - OpenAI analyzes headers
    - Recognizes Spanish column names
    - Generates mapping JSON:
      ```json
      [
        {"sourceField": "codigo_referencia", "targetField": "ExternalId"},
        {"sourceField": "numero_poliza", "targetField": "PolicyNumber"},
        {"sourceField": "monto_prima_bruta", "targetField": "GrossPremium"},
        {"sourceField": "comision_agente_neta", "targetField": "NetCommission"},
        {"sourceField": "fecha_vigencia_efectiva", "targetField": "TransactionDate"},
        {"sourceField": "notas_internas", "targetField": "Notes"}
      ]
      ```

5️⃣  Persistence (2 sec)
    - System saves mapping to Azure SQL CarrierMappings table
    - 6 rows inserted (one for each mapped field)
    - Includes: CarrierCode, SourceField, TargetField, IsRequired, CreatedAt
    - Timestamp recorded for audit trail

6️⃣  Row Processing (5 sec)
    - For each of 3 CSV rows:
      * Map Spanish columns → Canonical fields
      * Create InsuranceTransaction entity
      * Calculate commission integrity check
      * Insert into database
    
7️⃣  Batch Metadata (2 sec)
    - Create Batch record in Batches table
    - Store: FileName, RowCount (3), Status (SUCCESS), ProcessedAt, Carrier info

8️⃣  Response Generated (1 sec)
    - Return JSON with Batch ID and processing results
```

**Expected Response:**
```json
{
  "batchId": "a1b2c3d4-e5f6-47g8-h9i0-j1k2l3m4n5o6",
  "fileName": "test_carrier.csv",
  "rowsProcessed": 3,
  "status": "SUCCESS",
  "carrier": {
    "code": "TEST-CARRIER-001",
    "name": "TestCarrier"
  },
  "processingTime": "00:00:28.500",
  "errors": []
}
```

**✅ Success Criteria:**
- HTTP 200 response
- All 3 rows processed successfully
- `status` is "SUCCESS"
- `batchId` provided (save this for next steps)

---

### Step 3.3: Verify Mappings Were Saved in Database

**Command (using Azure Data Studio or similar):**
```sql
SELECT 
    CarrierCode,
    SourceField,
    TargetField,
    IsRequired,
    CreatedAt
FROM CarrierMappings
WHERE CarrierCode = 'TEST-CARRIER-001'
ORDER BY TargetField;
```

**Why this command?**
- Verifies Smart Mapping persisted mappings correctly
- Proves AI response was parsed and saved
- Shows which fields were marked as required

**Expected Result (6 rows):**
```
CarrierCode          | SourceField                | TargetField      | IsRequired | CreatedAt
TEST-CARRIER-001     | codigo_referencia          | ExternalId       | 1          | 2024-05-03 10:35:12
TEST-CARRIER-001     | numero_poliza              | PolicyNumber     | 1          | 2024-05-03 10:35:12
TEST-CARRIER-001     | monto_prima_bruta          | GrossPremium     | 0          | 2024-05-03 10:35:12
TEST-CARRIER-001     | comision_agente_neta       | NetCommission    | 0          | 2024-05-03 10:35:12
TEST-CARRIER-001     | fecha_vigencia_efectiva    | TransactionDate  | 0          | 2024-05-03 10:35:12
TEST-CARRIER-001     | notas_internas             | Notes            | 0          | 2024-05-03 10:35:12
```

**✅ Success Criteria:**
- 6 rows exist (one per canonical field)
- CarrierCode matches upload request
- IsRequired=1 for PolicyNumber and ExternalId only
- CreatedAt timestamp is recent

---

### Step 3.4: Verify Transactions Were Inserted

**Command:**
```sql
SELECT 
    Id,
    ExternalId,
    PolicyNumber,
    GrossPremium,
    NetCommission,
    TransactionDate,
    Status,
    CreatedAt
FROM InsuranceTransactions
WHERE PolicyNumber LIKE 'POL-2024-1%'
ORDER BY ExternalId;
```

**Why this command?**
- Verifies CSV rows were parsed and stored correctly
- Confirms mapping was applied properly
- Shows data integrity

**Expected Result (3 rows):**
```
Id                                   | ExternalId    | PolicyNumber | GrossPremium | NetCommission | TransactionDate | Status | CreatedAt
12345678-abcd-1234-5678-abcdef123456 | EXT-TST-001   | POL-2024-100 | 10000.00     | 1000.00       | 2024-05-01      | VALID  | 2024-05-03 10:35:15
87654321-dcba-4321-8765-fedcba654321 | EXT-TST-002   | POL-2024-101 | 15000.00     | 1500.00       | 2024-05-02      | VALID  | 2024-05-03 10:35:15
11223344-5566-7788-99aa-bbccddeeff00 | EXT-TST-003   | POL-2024-102 | 8500.00      | 850.00        | 2024-05-03      | VALID  | 2024-05-03 10:35:15
```

**✅ Success Criteria:**
- All 3 rows inserted
- Values match CSV input exactly
- Status = "VALID" (passed fraud detection)
- CreatedAt timestamp is recent

---

### Step 3.5: Test Cache Hit - Upload Same Carrier Again

**Why this test?**
Proves Smart Mapping cache works:
- First upload: AI called (took 20+ seconds)
- Second upload: Uses saved mapping (instant)

**Create second test CSV: `/tmp/test_carrier_v2.csv`**

```csv
codigo_referencia,numero_poliza,monto_prima_bruta,comision_agente_neta,fecha_vigencia_efectiva,identificador_transaccion,notas_internas
EXT-TST-004,POL-2024-200,12000.00,1200.00,2024-05-04,TXN-ABC-004,Segunda remesa
EXT-TST-005,POL-2024-201,9500.00,950.00,2024-05-05,TXN-ABC-005,Renovación
```

**Command:**
```bash
curl -X POST http://localhost:8080/api/ingestion/upload \
  -H "X-Api-Key: dev-test-key-12345" \
  -F "file=@/tmp/test_carrier_v2.csv" \
  -F "sourceName=TestCarrier" \
  -F "carrierCode=TEST-CARRIER-001"
```

**Why same carrierCode?**
- Same carrier code triggers cache lookup
- System checks: "Have I seen TEST-CARRIER-001 before?"
- Result: "Yes! Mappings exist in database"
- Skips OpenAI call completely

**Expected Processing Time:**
- **First upload**: 25-35 seconds (includes OpenAI call)
- **Second upload**: 2-5 seconds (cache hit, no AI call) ← **3-7x FASTER** ✨

**Expected Response (similar structure, but faster):**
```json
{
  "batchId": "f7g8h9i0-j1k2-l3m4-n5o6-p7q8r9s0t1u2",
  "fileName": "test_carrier_v2.csv",
  "rowsProcessed": 2,
  "status": "SUCCESS",
  "processingTime": "00:00:03.200",
  "errors": []
}
```

**✅ Success Criteria:**
- HTTP 200 response
- Processing time < 10 seconds (NO AI call)
- 2 new rows inserted
- No new mappings created in CarrierMappings table (reused existing)

---

### Step 3.6: Verify Second Batch Used Cached Mappings

**Command:**
```sql
SELECT COUNT(*) as MappingCount
FROM CarrierMappings
WHERE CarrierCode = 'TEST-CARRIER-001'
AND CreatedAt > DATEADD(SECOND, -10, GETUTCDATE());  -- Within last 10 seconds
```

**Expected Result:**
```
MappingCount
0
```

**Why 0?**
- No NEW mappings were created (proves cache was used)
- Same 6 mappings from Step 3.3 still exist (not duplicated)

**Alternative Verification (check transaction count increased):**
```sql
SELECT COUNT(*) as NewTransactions
FROM InsuranceTransactions
WHERE PolicyNumber IN ('POL-2024-200', 'POL-2024-201');
```

**Expected Result:**
```
NewTransactions
2
```

**✅ Success Criteria:**
- No new CarrierMappings rows created
- 2 new InsuranceTransactions created
- Proves cache mechanism works perfectly

---

## Phase 4: Error Scenarios (Robustness Testing)

### Why This Phase?
Real-world systems fail. This phase verifies graceful error handling.

---

### Step 4.1: Missing API Key

**Command:**
```bash
curl -X POST http://localhost:8080/api/ingestion/upload \
  -F "file=@/tmp/test_carrier.csv" \
  -F "sourceName=TestCarrier" \
  -F "carrierCode=TEST-ERROR-001"
```

**Note:** No `X-Api-Key` header

**What Happens:**
1. Request reaches ApiKeyMiddleware
2. Middleware checks for `X-Api-Key` header
3. Header missing → rejects request
4. Returns 401 Unauthorized

**Expected Response:**
```json
{
  "statusCode": 401,
  "message": "API Key is required",
  "timestamp": "2024-05-03T10:40:00.000Z"
}
```

**✅ Validation:**
- HTTP 401 status code
- Clear error message
- No data processed

---

### Step 4.2: Invalid API Key

**Command:**
```bash
curl -X POST http://localhost:8080/api/ingestion/upload \
  -H "X-Api-Key: wrong-key-xyz" \
  -F "file=@/tmp/test_carrier.csv" \
  -F "sourceName=TestCarrier" \
  -F "carrierCode=TEST-ERROR-002"
```

**What Happens:**
1. Middleware validates key against configured value
2. Key doesn't match
3. Returns 401

**Expected Response:**
```json
{
  "statusCode": 401,
  "message": "Invalid API Key",
  "timestamp": "2024-05-03T10:41:00.000Z"
}
```

**✅ Validation:**
- HTTP 401 status code
- No database modifications

---

### Step 4.3: Malformed CSV

**Create invalid file: `/tmp/invalid.csv`**
```
This is not,a valid,CSV with,mismatched,columns
and,this,row,has,too,many,columns
```

**Command:**
```bash
curl -X POST http://localhost:8080/api/ingestion/upload \
  -H "X-Api-Key: dev-test-key-12345" \
  -F "file=@/tmp/invalid.csv" \
  -F "sourceName=TestCarrier" \
  -F "carrierCode=TEST-ERROR-003"
```

**What Happens:**
1. CSV parser attempts to read file
2. Encounters inconsistent column counts
3. Validates against schema
4. Returns appropriate error
5. **Important**: No partial data saved to database

**Expected Response (example):**
```json
{
  "statusCode": 400,
  "message": "Invalid CSV format",
  "details": "Row 2 has 7 columns, expected 5",
  "timestamp": "2024-05-03T10:42:00.000Z"
}
```

**✅ Validation:**
- HTTP 400 status code
- Error details provided
- No transactions created

---

## Phase 5: Performance Testing (Optional)

### Step 5.1: Bulk Upload Performance

**Why?** Ensure system handles large files (100+ rows)

**Create large test file: `/tmp/large_upload.csv`**
```bash
# Generate CSV with 100 rows
python3 << 'EOF'
with open('/tmp/large_upload.csv', 'w') as f:
    f.write('codigo_referencia,numero_poliza,monto_prima_bruta,comision_agente_neta,fecha_vigencia_efectiva,identificador_transaccion,notas_internas\n')
    for i in range(1, 101):
        f.write(f'EXT-PERF-{i:03d},POL-PERF-{i:03d},{5000 + i*100}.00,{500 + i*10}.00,2024-05-{(i%28)+1:02d},TXN-PERF-{i:03d},Bulk test row {i}\n')
print("Generated 100-row CSV")
EOF
```

**Command:**
```bash
time curl -X POST http://localhost:8080/api/ingestion/upload \
  -H "X-Api-Key: dev-test-key-12345" \
  -F "file=@/tmp/large_upload.csv" \
  -F "sourceName=PerfTest" \
  -F "carrierCode=PERF-TEST-100"
```

**What Happens:**
- First time: AI called (includes OpenAI latency)
- 100 rows parsed and inserted
- Time recorded

**Expected Performance:**
```
Total Time: 25-40 seconds (including AI call)
Throughput: ~3-4 rows/second
```

**✅ Success Criteria:**
- All 100 rows processed
- Status SUCCESS
- No timeouts

---

## Summary: What Each Phase Tests

| Phase | What | Why | Tools |
|-------|------|-----|-------|
| **1: Unit Tests** | Individual components | Foundation logic | `dotnet test` |
| **2: Docker** | Application container + database | Infrastructure | `docker compose up` |
| **3: API + Smart Mapping** | Real CSV upload + AI mapping | Business flow | `curl` + SQL |
| **4: Error Handling** | Missing keys, malformed data | Robustness | `curl` + validation |
| **5: Performance** | Large files, throughput | Scalability | `time` + SQL |

---

## Expected Outcomes After Complete Testing

✅ **System Proven Ready For:**
- Unknown carrier CSV uploads
- Automatic header mapping via AI
- Intelligent caching (no duplicate AI calls)
- Graceful error handling
- Audit trail via database persistence
- Performance under load

✅ **Enterprise Features Demonstrated:**
- API security (X-Api-Key validation)
- Health monitoring endpoints
- Smart Mapping with OpenAI
- Professional error responses
- Complete logging for troubleshooting

---

## Troubleshooting Guide

### Issue: Docker won't start
```bash
# Clean and retry
docker compose down -v
docker compose up --build
```

### Issue: Database connection fails
```bash
# Check health endpoint
curl http://localhost:8080/api/health/database

# Verify .env file has correct connection string
cat .env
```

### Issue: OpenAI returns error
```bash
# Verify API key is set
grep OPENAI_API_KEY .env

# Check rate limiting (requests/min)
# Reduce parallelism or add delays between uploads
```

### Issue: CSV upload returns 401
```bash
# Ensure correct API key
curl -H "X-Api-Key: dev-test-key-12345" http://localhost:8080/api/health/ping
```

---

## Next Steps After Successful Testing

1. ✅ Deploy to production environment
2. ✅ Set up monitoring and alerts
3. ✅ Configure backup strategy for database
4. ✅ Document API for customer integration
5. ✅ Plan feature enhancements (validation UI, analytics dashboard)

