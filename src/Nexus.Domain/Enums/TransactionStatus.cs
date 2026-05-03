namespace Nexus.Domain.Enums;

public enum TransactionStatus
{
    Pending = 0,
    Clean = 1,
    Duplicate = 2,
    Suspect = 3,
    Invalid = 4
}
