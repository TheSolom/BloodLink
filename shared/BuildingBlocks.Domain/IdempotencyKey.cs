namespace BloodLink.BuildingBlocks.Domain;

public readonly record struct IdempotencyKey : IEquatable<IdempotencyKey>
{
    public const string HeaderName = "Idempotency-Key";
    public const int MaxLength = 128;
    public const string RequiredErrorCode = "request.idempotency_key_required";
    public const string TooLongErrorCode = "request.idempotency_key_too_long";

    public string Value { get; }

    private IdempotencyKey(string value)
    {
        Value = value;
    }

    public static IdempotencyKey Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Idempotency-Key is required.", RequiredErrorCode);
        }

        string trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new DomainException(
                $"Idempotency-Key must not exceed {MaxLength} characters.",
                TooLongErrorCode
            );
        }

        return new IdempotencyKey(trimmed);
    }

    public static bool TryCreate(string? value, out IdempotencyKey key, out string? errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            key = default;
            errorCode = RequiredErrorCode;
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            key = default;
            errorCode = TooLongErrorCode;
            return false;
        }

        key = new IdempotencyKey(trimmed);
        errorCode = null;
        return true;
    }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string(IdempotencyKey key) => key.ToString();
}
