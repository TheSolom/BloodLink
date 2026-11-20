namespace BloodLink.BuildingBlocks.Domain;

/// <summary>
/// Common domain error codes shared across services.
/// </summary>
public static class DomainErrors
{
    public const string RuleViolation = "domain.rule_violation";
    public const string IdempotencyKeyRequired = IdempotencyKey.RequiredErrorCode;
    public const string IdempotencyKeyTooLong = IdempotencyKey.TooLongErrorCode;
}
