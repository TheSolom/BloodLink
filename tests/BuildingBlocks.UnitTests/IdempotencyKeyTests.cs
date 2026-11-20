using BloodLink.BuildingBlocks.Domain;
using Shouldly;
using Xunit;

namespace BloodLink.BuildingBlocks.UnitTests;

public class IdempotencyKeyTests
{
    [Fact]
    public void Create_ValidKey_ShouldTrimAndStoreValue()
    {
        var key = IdempotencyKey.Create("   valid-key-123   ");
        key.Value.ShouldBe("valid-key-123");
        key.ToString().ShouldBe("valid-key-123");
        string str = key;
        str.ShouldBe("valid-key-123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullOrWhiteSpace_ShouldThrowDomainException(string? invalidKey)
    {
        DomainException ex = Should.Throw<DomainException>(() => IdempotencyKey.Create(invalidKey));
        ex.Code.ShouldBe(IdempotencyKey.RequiredErrorCode);
    }

    [Fact]
    public void Create_KeyExceeding128Characters_ShouldThrowDomainException()
    {
        string tooLong = new('x', 129);
        DomainException ex = Should.Throw<DomainException>(() => IdempotencyKey.Create(tooLong));
        ex.Code.ShouldBe(IdempotencyKey.TooLongErrorCode);
    }

    [Fact]
    public void TryCreate_ValidKey_ShouldReturnTrue()
    {
        bool success = IdempotencyKey.TryCreate(
            "  abc  ",
            out IdempotencyKey key,
            out string? errorCode
        );
        success.ShouldBeTrue();
        key.Value.ShouldBe("abc");
        errorCode.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_MissingKey_ShouldReturnFalseWithRequiredErrorCode(string? missing)
    {
        bool success = IdempotencyKey.TryCreate(missing, out _, out string? errorCode);
        success.ShouldBeFalse();
        errorCode.ShouldBe(IdempotencyKey.RequiredErrorCode);
    }

    [Fact]
    public void TryCreate_TooLongKey_ShouldReturnFalseWithTooLongErrorCode()
    {
        string tooLong = new('k', 130);
        bool success = IdempotencyKey.TryCreate(tooLong, out _, out string? errorCode);
        success.ShouldBeFalse();
        errorCode.ShouldBe(IdempotencyKey.TooLongErrorCode);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var key1 = IdempotencyKey.Create("test-key");
        var key2 = IdempotencyKey.Create("test-key");
        key1.ShouldBe(key2);
    }
}
