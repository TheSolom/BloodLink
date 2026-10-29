using BloodLink.BuildingBlocks.Domain;
using Shouldly;
using Xunit;

namespace BloodLink.BuildingBlocks.UnitTests;

public class EntityTests
{
    private sealed class TestEntity(Guid id, string name) : Entity<Guid>(id)
    {
        public string Name { get; set; } = name;
    }

    private sealed class AnotherEntity(Guid id) : Entity<Guid>(id) { }

    [Fact]
    public void Equals_WhenSameIdAndType_ReturnsTrue()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Alpha");
        var entity2 = new TestEntity(id, "Beta"); // different property, same ID

        entity1.Equals(entity2).ShouldBeTrue();
        (entity1 == entity2).ShouldBeTrue();
        (entity1 != entity2).ShouldBeFalse();
        entity1.GetHashCode().ShouldBe(entity2.GetHashCode());
    }

    [Fact]
    public void Equals_WhenDifferentId_ReturnsFalse()
    {
        var entity1 = new TestEntity(Guid.NewGuid(), "SameName");
        var entity2 = new TestEntity(Guid.NewGuid(), "SameName");

        entity1.Equals(entity2).ShouldBeFalse();
        (entity1 == entity2).ShouldBeFalse();
        (entity1 != entity2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WhenDifferentTypeWithSameId_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Entity");
        var entity2 = new AnotherEntity(id);

        entity1.Equals(entity2).ShouldBeFalse();
        (entity1 == entity2).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenNull_ReturnsFalse()
    {
        var entity = new TestEntity(Guid.NewGuid(), "Alpha");

        entity.Equals(null).ShouldBeFalse();
        (entity == null).ShouldBeFalse();
        (null == entity).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WhenBothNull_ReturnsTrue()
    {
        TestEntity? left = null;
        TestEntity? right = null;

        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
    }
}
