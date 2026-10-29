using BloodLink.BuildingBlocks.Domain;
using Shouldly;
using Xunit;

namespace BloodLink.BuildingBlocks.UnitTests;

public class DomainExceptionTests
{
    [Fact]
    public void DomainException_StoresMessageAndCode()
    {
        var ex = new DomainException("Invalid operation", "INVALID_OP");

        ex.Message.ShouldBe("Invalid operation");
        ex.Code.ShouldBe("INVALID_OP");
    }

    [Fact]
    public void DomainException_WithInnerException_PreservesInnerException()
    {
        var inner = new InvalidOperationException("Inner error");
        var ex = new DomainException("Domain error", "DOMAIN_ERR", inner);

        ex.Message.ShouldBe("Domain error");
        ex.Code.ShouldBe("DOMAIN_ERR");
        ex.InnerException.ShouldBe(inner);
    }
}
