using System.Globalization;
using CsvHelper;
using Nexus.Application.DTOs;

namespace Nexus.Infrastructure.Ingestion;

public class CsvIngestionStrategy : IIngestionStrategy
{
    public bool CanHandle(string fileExtension)
    {
        return string.Equals(fileExtension, ".csv", StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<RawCarrierRecord> ReadAsync(
        string filePath,
        string carrierCode,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        foreach (var record in csv.GetRecords<dynamic>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (record is IDictionary<string, object?> expando)
            {
                foreach (var (key, value) in expando)
                {
                    dict[key] = value?.ToString();
                }
            }

            yield return new RawCarrierRecord(carrierCode, dict);
        }
    }
}
