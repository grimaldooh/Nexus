namespace Nexus.Application.DTOs;

public class RawCarrierRecord
{
    public RawCarrierRecord(string carrierCode, IDictionary<string, string?> fields)
    {
        CarrierCode = carrierCode;
        Fields = fields;
    }

    public string CarrierCode { get; }
    public IDictionary<string, string?> Fields { get; }
}
