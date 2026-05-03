WITH Ranked AS
(
    SELECT Id,
            ROW_NUMBER() OVER (PARTITION BY PolicyNumber, NetCommission, TransactionDate ORDER BY Id) AS rn
    FROM InsuranceTransactions
    WHERE BatchId = @BatchId
)
UPDATE t
SET Status = @DuplicateStatus
FROM InsuranceTransactions t
INNER JOIN Ranked r ON t.Id = r.Id
WHERE r.rn > 1;
