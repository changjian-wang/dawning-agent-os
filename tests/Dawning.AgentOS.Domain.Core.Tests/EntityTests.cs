using System.Reflection;
using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Core.Tests;

[TestFixture]
public sealed class EntityTests
{
    private sealed class TestEntity : Entity<Guid>
    {
        public TestEntity(Guid id, DateTimeOffset createdAt)
            : base(id, createdAt) { }

        // Mimics ORM rehydration path: parameterless ctor + protected setters.
        private TestEntity() { }

        public static TestEntity Rehydrate(Guid id, DateTimeOffset createdAt)
        {
            var e = new TestEntity();
            // Reflection only because protected setters are not visible from outside;
            // production rehydration calls the protected setter from within the
            // derived class, not via reflection.
            typeof(Entity<Guid>)
                .GetProperty(nameof(Id))!
                .SetValue(e, id);
            typeof(Entity<Guid>)
                .GetProperty(nameof(CreatedAt))!
                .SetValue(e, createdAt);
            return e;
        }
    }

    private sealed class OtherEntity : Entity<Guid>
    {
        public OtherEntity(Guid id, DateTimeOffset createdAt)
            : base(id, createdAt) { }
    }

    [Test]
    public void Constructor_AssignsIdAndCreatedAt()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var sut = new TestEntity(id, createdAt);

        Assert.Multiple(() =>
        {
            Assert.That(sut.Id, Is.EqualTo(id));
            Assert.That(sut.CreatedAt, Is.EqualTo(createdAt));
        });
    }

    [Test]
    public void Constructor_DefaultId_Throws()
    {
        Assert.That(
            () => new TestEntity(Guid.Empty, DateTimeOffset.UtcNow),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("id"));
    }

    [Test]
    public void Equals_SameIdAndType_AreEqual()
    {
        var id = Guid.NewGuid();
        var a = new TestEntity(id, DateTimeOffset.UtcNow);
        var b = new TestEntity(id, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void Equals_DifferentId_AreNotEqual()
    {
        var a = new TestEntity(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var b = new TestEntity(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.That(a.Equals(b), Is.False);
        Assert.That(a != b, Is.True);
    }

    [Test]
    public void Equals_DifferentType_SameId_AreNotEqual()
    {
        var id = Guid.NewGuid();
        var a = new TestEntity(id, DateTimeOffset.UtcNow);
        var b = new OtherEntity(id, DateTimeOffset.UtcNow);

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Rehydrate_AssignsIdViaProtectedSetter()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var sut = TestEntity.Rehydrate(id, createdAt);

        Assert.Multiple(() =>
        {
            Assert.That(sut.Id, Is.EqualTo(id));
            Assert.That(sut.CreatedAt, Is.EqualTo(createdAt));
        });
    }

    [Test]
    public void Operators_NullSafety()
    {
        TestEntity? a = null;
        TestEntity? b = null;
        var c = new TestEntity(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(a == b, Is.True);
            Assert.That(a == c, Is.False);
            Assert.That(c == a, Is.False);
            Assert.That(a != c, Is.True);
        });
    }

    [Test]
    public void IdSetter_IsProtected()
    {
        // Guard rail: Id setter must remain protected.
        // If someone widens it to public this test fails.
        var setter = typeof(Entity<Guid>)
            .GetProperty(nameof(Entity<Guid>.Id))!
            .GetSetMethod(nonPublic: true);

        Assert.That(setter, Is.Not.Null);
        Assert.That(setter!.IsFamily, Is.True, "Id setter must be protected.");
    }

    [Test]
    public void CreatedAtSetter_IsProtected()
    {
        var setter = typeof(Entity<Guid>)
            .GetProperty(nameof(Entity<Guid>.CreatedAt))!
            .GetSetMethod(nonPublic: true);

        Assert.That(setter, Is.Not.Null);
        Assert.That(setter!.IsFamily, Is.True, "CreatedAt setter must be protected.");
    }

    [Test]
    public void DefaultCtor_IsAccessibleFromDerivedOnly()
    {
        // Rehydrate path uses parameterless ctor; it must not be public.
        var ctor = typeof(Entity<Guid>)
            .GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                Type.EmptyTypes);

        Assert.That(ctor, Is.Not.Null);
        Assert.That(ctor!.IsFamily, Is.True, "Parameterless ctor must be protected.");
    }
}
