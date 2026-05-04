# 🧠 Smart Mapping - Professional Technical Documentation

## Executive Summary

**Smart Mapping** is an artificial intelligence feature that enables Nexus to automatically process CSV/Excel files from unknown insurance carriers, regardless of how they name their column headers.

**Problem Solved:** Previously, we required manual database configuration for each new carrier. Now, OpenAI automatically analyzes headers and proposes the mapping.

---

## How Does It Work?

### Complete Flow

```
Upload CSV (Unknown Carrier)
        ↓
CarrierMappingService searches database
        ↓
No existing mappings found
        ↓
Calls AiIntegrityService.TryMapUnknownAsync()
        ↓
Extracts ALL headers from CSV
        ↓
Sends prompt to OpenAI with:
  - Headers discovered
  - Canonical fields to map
  - Examples of known variations
        ↓
OpenAI responds with mapping as JSON
        ↓
System saves mapping to database (for reuse)
        ↓
Continues processing with NOW-KNOWN mappings
```

---

## Canonical Fields (What We Need)

Our system internally always requires these 6 minimum fields:

| Canonical Field | Type | Description | Example |
|---|---|---|---|
| `ExternalId` | string | Unique transaction ID from the carrier | `TXN-2024-0001` |
| `PolicyNumber` | string | Insurance policy number | `POL-2024-123456` |
| `GrossPremium` | decimal | Gross premium before commissions | `5000.00` |
| `NetCommission` | decimal | Net agent/broker commission | `500.00` |
| `TransactionDate` | DateTime | Exact transaction date | `2024-05-03` |
| `Notes` | string | Additional notes (optional) | `Renewal, late payment, etc` |

---

## Header Variations OpenAI Can Recognize

### For `PolicyNumber` (the most critical)

**English Variations:**
- `policy_number`, `policy_no`, `policy_id`, `policy`
- `policyNumber`, `PolicyNo`, `Policy_ID`
- `pol_no`, `pol_num`, `policy_num`
- `policy_reference`, `policy_ref`
- `contract_number`, `contract_no`

**Spanish Variations:**
- `numero_poliza`, `número_poliza`, `num_poliza`
- `póliza`, `poliza`
- `numero_contrato`, `contrato`
- `referencia_poliza`

**Industry Specific:**
- `certificate_number` (group insurance)
- `master_policy_no` (master policies)
- `sub_policy_no` (sub-policies)

---

### For `NetCommission`

**English Variations:**
- `commission`, `commission_amount`, `comm_amount`
- `net_commission`, `net_comm`
- `agent_commission`, `broker_commission`
- `commission_total`, `total_commission`
- `comm`, `comm_amt`, `commision` (common typo)

**Spanish Variations:**
- `comisión`, `comision`
- `comisión_neta`, `comision_neta`
- `comisión_agente`, `comisión_broker`
- `monto_comisión`

**Abbreviations:**
- `net_comm`, `comm_net`, `nc`, `cm`
- `agent_comm`, `broker_comm`, `ac`, `bc`

**Currency Variations:**
- `commission_usd`, `commission_mxn`, `commission_ars`
- `comm_ccy`, `comm_currency`

---

### For `GrossPremium`

**English Variations:**
- `premium`, `gross_premium`, `total_premium`
- `base_premium`, `written_premium`
- `premium_amount`, `premium_total`
- `gross`, `gross_amt`

**Spanish Variations:**
- `prima`, `prima_bruta`, `prima_total`
- `monto_prima`, `prima_escrita`

**Insurance Industry Specific:**
- `earned_premium` (earned premium)
- `issued_premium` (issued premium)
- `annual_premium` (annual premium)
- `face_amount` (policy limit/face amount)

---

### For `TransactionDate`

**English Variations:**
- `transaction_date`, `trans_date`, `txn_date`
- `effective_date`, `eff_date`, `eff_dt`
- `issue_date`, `issue_dt`, `issue_date`
- `date`, `trans_dt`
- `date_effective`, `date_issued`
- `transaction_dt`, `comm_date`, `commission_date`

**Spanish Variations:**
- `fecha_transaccion`, `fecha_trans`
- `fecha_efectiva`, `fecha_efe`
- `fecha_emision`, `fecha_emitida`
- `fecha`

**ISO/Standard:**
- `date_iso`, `date_utc`
- `timestamp`, `datetime`

**Format Variations (OpenAI handles these):**
- `2024-05-03` (ISO)
- `05/03/2024` (US)
- `03/05/2024` (EU)
- `03-May-2024` (Text)
- `20240503` (Compact)

---

### For `ExternalId`

**English Variations:**
- `transaction_id`, `txn_id`, `trans_id`
- `external_id`, `ext_id`
- `reference_number`, `reference_no`, `ref_no`
- `batch_number`, `batch_id`
- `control_number`, `control_id`
- `unique_id`, `uid`, `id`

**Spanish Variations:**
- `id_transaccion`, `id_trans`
- `numero_referencia`, `número_referencia`
- `id_lote`, `numero_lote`
- `numero_control`

**Insurance Specific:**
- `claim_number`, `claim_id` (for claim payments)
- `endorsement_number` (for endorsements)
- `transaction_reference`

---

### For `Notes`

**English Variations:**
- `notes`, `note`, `comments`, `comment`
- `description`, `desc`, `details`
- `remarks`, `remark`, `memo`, `memos`
- `special_notes`, `additional_notes`
- `status_notes`, `processing_notes`

**Spanish Variations:**
- `notas`, `nota`
- `comentarios`, `comentario`
- `descripción`, `detalles`
- `observaciones`, `observación`

---

## Real-World Example: Multiple Carriers

### Carrier A (United States - English)

```csv
policy_number,gross_amount,commission_amt,effective_date,transaction_ref,notes
POL-2024-001,5000.00,500.00,2024-05-01,TXN-0001,Initial commission
POL-2024-002,7500.00,750.00,2024-05-02,TXN-0002,Renewal with increase
```

OpenAI would map to:
```json
[
  {"sourceField": "policy_number", "targetField": "PolicyNumber"},
  {"sourceField": "gross_amount", "targetField": "GrossPremium"},
  {"sourceField": "commission_amt", "targetField": "NetCommission"},
  {"sourceField": "effective_date", "targetField": "TransactionDate"},
  {"sourceField": "transaction_ref", "targetField": "ExternalId"},
  {"sourceField": "notes", "targetField": "Notes"}
]
```

### Carrier B (Mexico - Spanish)

```csv
num_poliza,prima_bruta,comisión,fecha_efe,id_trans,observaciones
POL-2024-001,8500.00,850.00,2024-05-01,MXN-0001,Comisión inicial
POL-2024-002,6000.00,600.00,2024-05-02,MXN-0002,Renovación
```

OpenAI would map to:
```json
[
  {"sourceField": "num_poliza", "targetField": "PolicyNumber"},
  {"sourceField": "prima_bruta", "targetField": "GrossPremium"},
  {"sourceField": "comisión", "targetField": "NetCommission"},
  {"sourceField": "fecha_efe", "targetField": "TransactionDate"},
  {"sourceField": "id_trans", "targetField": "ExternalId"},
  {"sourceField": "observaciones", "targetField": "Notes"}
]
```

### Carrier C (Argentina - Abbreviated)

```csv
póliza,prima,com_neta,f_efe,ref,obs
POL-2024-001,12000.00,1200.00,01/05/2024,ARG-001,Pago inicial
POL-2024-002,15000.00,1500.00,02/05/2024,ARG-002,Renovación x2
```

OpenAI would map to:
```json
[
  {"sourceField": "póliza", "targetField": "PolicyNumber"},
  {"sourceField": "prima", "targetField": "GrossPremium"},
  {"sourceField": "com_neta", "targetField": "NetCommission"},
  {"sourceField": "f_efe", "targetField": "TransactionDate"},
  {"sourceField": "ref", "targetField": "ExternalId"},
  {"sourceField": "obs", "targetField": "Notes"}
]
```

---

## Datos de Entrada Complejos

OpenAI también maneja correctamente formatos mixtos:

### 1. Múltiples Níveis de Comisiones
```csv
gross_premium, agent_commission, broker_commission, house_commission
5000, 200, 250, 50
```
→ OpenAI selecciona el más apropiado basado en contexto (típicamente agent_commission para nuestro caso)

### 2. Múltiples Fechas
```csv
policy_date, effective_date, issue_date, last_modified
2024-05-01, 2024-05-03, 2024-04-20, 2024-05-05
```
→ OpenAI elige effective_date (la más relevante para transacción)

### 3. Identificadores Redundantes
```csv
policy_id, policy_number, contract_number, external_reference
POL-001, 001, C-001, EXT-001
```
→ OpenAI elige el más estructurado/consistente

### 4. Extra Fields (Not Required but Present)
```csv
policy_number, gross_premium, commission, agent_name, agency_code, region, created_at
```
→ OpenAI maps only canonical fields and safely ignores the rest

---

## Configuration Parameters

The `AiIntegrityService` service uses these OpenAI parameters:

```csharp
var requestBody = new
{
    model = _options.Model ?? "gpt-4o-mini",  // Model configured in appsettings
    messages = new[] { ... },
    temperature = 0.1  // Very low = consistent and predictable responses
};
```

**Temperature = 0.1:**
- ✅ Very deterministic (no "creativity")
- ✅ Consistent JSON responses
- ✅ Ideal for data mapping
- ❌ Not creative (but we don't need that here)

---

## Detailed Code Flow

### 1. When an Unknown CSV Arrives

```csharp
public async Task MapAsync(Guid batchId, RawCarrierRecord record, CancellationToken cancellationToken)
{
    // Search for mappings for this carrier
    var mappings = await _dbContext.CarrierMappings
        .Where(x => x.CarrierCode == record.CarrierCode)
        .ToListAsync();

    if (mappings.Count == 0)  // ← No known mapping
    {
        // Call AI
        var aiRecord = await _aiIntegrityService.TryMapUnknownAsync(record, cancellationToken);
        
        if (aiRecord is not null)  // ← AI was successful
        {
            // Now search for the mappings AI saved
            mappings = await _dbContext.CarrierMappings
                .Where(x => x.CarrierCode == record.CarrierCode)
                .ToListAsync();
        }
    }
}
```

### 2. AI Generates Mapping (AiIntegrityService)

```csharp
public async Task<RawCarrierRecord?> TryMapUnknownAsync(RawCarrierRecord record, CancellationToken cancellationToken)
{
    // 1. Extract headers
    var sourceHeaders = record.Fields.Keys.ToList();
    
    // 2. Build dynamic prompt
    var prompt = $"""
        Map these fields: {sourceHeaders}
        To our canonical fields: ExternalId, PolicyNumber, GrossPremium, NetCommission, TransactionDate, Notes
    """;
    
    // 3. Llama OpenAI
    var response = await _httpClient.SendAsync(requestMsg);
    
    // 4. Parsea JSON response
    var mappings = JsonSerializer.Deserialize<List<AiMappingResult>>(contentString);
    
    // 5. Guarda en BD
    await _dbContext.CarrierMappings.AddRangeAsync(dbMappings);
    
    // 6. Retorna record (ahora con mappings guardados)
    return record;
}
```

### 3. Next Upload from Same Carrier (Automatic Cache Hit)

When another file is uploaded from the same carrier:

```csharp
var mappings = await _dbContext.CarrierMappings
    .Where(x => x.CarrierCode == record.CarrierCode)
    .ToListAsync();
    
if (mappings.Count > 0)  // ← Found in cache
{
    // Use saved mappings (no AI call needed)
    foreach (var mapping in mappings)
    {
        ApplyMapping(transaction, mapping.TargetField, rawValue);
    }
}
```

---

## Database: CarrierMappings Table

Table that stores learned mappings:

```sql
CREATE TABLE CarrierMappings (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    CarrierCode NVARCHAR(100) NOT NULL,
    SourceField NVARCHAR(255) NOT NULL,  -- Original header ("comisión", "comm_amt")
    TargetField NVARCHAR(100) NOT NULL,  -- Canonical field ("NetCommission")
    TransformRule NVARCHAR(MAX),         -- Optional: transformation rules
    IsRequired BIT NOT NULL,              -- True if PolicyNumber or ExternalId
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2,
    INDEX IX_CarrierCode (CarrierCode)
);
```

**Sample Saved Data:**

| CarrierCode | SourceField | TargetField | IsRequired | CreatedAt |
|---|---|---|---|---|
| `STATE-FARM` | `policy_number` | `PolicyNumber` | 1 | 2024-05-03 |
| `STATE-FARM` | `commission_amt` | `NetCommission` | 0 | 2024-05-03 |
| `GEICO-MX` | `num_poliza` | `PolicyNumber` | 1 | 2024-05-03 |
| `GEICO-MX` | `comisión` | `NetCommission` | 0 | 2024-05-03 |

---

## Error Handling

### Scenario 1: OpenAI API Not Configured

```csharp
if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey.StartsWith("your-"))
{
    _logger.LogWarning("OpenAI API key not configured. Skipping smart mapping.");
    return null;  // ← Returns null, transaction marked INVALID
}
```

### Scenario 2: OpenAI API Fails

```csharp
try
{
    var response = await _httpClient.SendAsync(requestMsg, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        _logger.LogError("OpenAI API error: {Status}", response.StatusCode);
        return null;  // ← Transaction INVALID
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to perform AI smart mapping");
    return null;  // ← Transaction INVALID
}
```

### Scenario 3: OpenAI Responds But With Invalid/Empty Mappings

```csharp
var mappings = JsonSerializer.Deserialize<List<AiMappingResult>>(contentString);
if (mappings == null || !mappings.Any())
{
    return null;  // ← Transaction INVALID
}
```

---

## Testing Smart Mapping

### Test CSV: Unknown Carrier

```csv
codigo_aseguradora,numero_contrato,monto_prima,comision_agente,fecha_vigencia,codigo_transaccion,observaciones
ABC-001,CONT-2024-001,10000.00,1000.00,2024-05-01,TRANS-ABC-001,Comisión inicial
ABC-001,CONT-2024-002,15000.00,1500.00,2024-05-02,TRANS-ABC-002,Renovación con aumento
ABC-001,CONT-2024-003,8500.00,850.00,2024-05-03,TRANS-ABC-003,Descuento especial
```

### Testing Steps

1. **Upload the CSV with SourceName = "NewCarrier"**
   ```
   POST http://localhost:8080/api/ingestion/upload
   Headers: X-Api-Key: dev-test-key-12345
   Body: form-data
   - File: [csv above]
   - SourceName: "NewCarrier"
   - CarrierCode: "ABC-001"
   ```

2. **Wait for processing (5-10 seconds)**

3. **Check the status**
   ```
   GET http://localhost:8080/api/batches/{batchId}
   ```
   → Should show 3 transactions processed

4. **Verify mappings were saved in database**
   ```sql
   SELECT * FROM CarrierMappings WHERE CarrierCode = 'ABC-001';
   ```
   → Should have 6-7 rows with mapped fields

5. **Upload ANOTHER CSV from the same carrier**
   → This time it uses saved mappings (NO AI call needed - instant processing)

---

## Advantages of This Approach

✅ **Flexible:** Handles any header format and language
✅ **Scalable:** Automatically learns new carriers without code changes
✅ **Efficient:** Caches mappings (doesn't call AI every time)
✅ **Professional-Grade:** Handles real-world scenarios (multiple languages, abbreviations, industry-specific terms)
✅ **Auditable:** All mappings stored in database with timestamps
✅ **Fault-Tolerant:** If AI fails, transaction marked INVALID (no data loss)

---

## Next Steps (Optional)

1. **Manual Validation:** Endpoint for auditors to review and correct AI-generated mappings
2. **Feedback Loop:** Improve prompts based on real usage patterns
3. **Machine Learning:** Train custom model with historical mapping data
4. **Analytics Dashboard:** Show which mappings are used most frequently

---
