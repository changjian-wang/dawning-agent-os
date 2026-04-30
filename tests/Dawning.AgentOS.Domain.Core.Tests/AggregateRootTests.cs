using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Core.Tests;

[TestFixture]
public sealed class AggregateRootTests
{
    private sealed record TestEvent(DateTimeOffset OccurredOn) : IDomainEvent;

    private sealed class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate(Guid id, DateTimeOffset createdAt)
            : base(id, createdAt) { }

        // Rehydrate path must not raise events.
        private TestAggregate() { }

        public static TestAggregate Rehydrate(Guid id, DateTimeOffset createdAt)
        {
            var a = new TestAggregate();
            typeof(Entity<Guid>)
                .GetProperty(nameof(Id))!
                .SetValue(a, id);
            typeof(Entity<Guid>)
                .GetProperty(nameof(CreatedAt))!
                .SetValue(a, createdAt);
            return a;
        }

        public void DoBusinessAction(DateTimeOffset at) => Raise(new TestEvent(at));

        public void RaiseNull() => Raise(null!);
    }

    [Test]
    public void NewAggregate_HasNoDomainEvents()
    {
        var sut = new TestAggregate(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.That(sut.DomainEvents, Is.Empty);
    }

    [Test]
    public void Raise_AppendsEvent()
    {
        var sut = new TestAggregate(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var at = DateTimeOffset.UtcNow;

        sut.DoBusinessAction(at);

        Assert.Multiple(() =>
        {
            Assert.That(sut.DomainEvents, Has.Count.EqualTo(1));
            Assert.That(sut.DomainEvents[0], Is.InstanceOf<TestEvent>());
            Assert.That(((TestEvent)sut.DomainEvents[0]).OccurredOn, Is.EqualTo(at));
        });
    }

    [Test]
    public void Raise_PreservesOrder()
    {
        var sut = new TestAggregate(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var t1 = DateTimeOffset.UtcNow;
        var t2 = t1.AddSeconds(1);
        var t3 = t1.AddSeconds(2);

        sut.DoBusinessAction(t1);
        sut.DoBusinessAction(t2);
        sut.DoBusinessAction(t3);

        Assert.Multiple(() =>
        {
            Assert.That(sut.DomainEvents.Count, Is.EqualTo(3));
            Assert.That(((TestEvent)sut.DomainEvents[0]).OccurredOn, Is.EqualTo(t1));
            Assert.That(((TestEvent)sut.DomainEvents[1]).OccurredOn, Is.EqualTo(t2));
            Assert.That(((TestEvent)sut.DomainEvents[2]).OccurredOn, Is.EqualTo(t3));
        });
    }

    [Test]
    public void Raise_Null_Throws()
    {
        var sut = new TestAggregate(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.That(
            () => sut.RaiseNull(),
            Throws.ArgumentNullException);
    }

    [Test]
    public void ClearDomainEvents_EmptiesList()
    {
        var sut = new TestAggregate(Guid.NewGuid(), DateTimeOffset.UtcNow);
        sut.DoBusinessAction(DateTimeOffset.UtcNow);
        sut.DoBusinessAction(DateTimeOffset.UtcNow);

        sut.ClearDomainEvents();

        Assert.That(sut.DomainEvents, Is.Empty);
    }

    [Test]
    public void DomainEvents_IsReadOnlySnapshot()
    {
        var sut = new TestAggregate(Guid.NewGuid(), DateTimeOffset.UtcNow);

        // The contract is IReadOnlyList<>; the runtime type is the read-only wrapper
        // returned by List<T>.AsReadOnly(); writes are not part of the contract.
        Assert.That(sut.DomainEvents, Is.AssignableTo<IReadOnlyList<IDomainEvent>>());
    }

    [Test]
    public void Rehydrate_DoesNotRaiseEvents()
    {
        // Rehydration loads state without raising events (DDD convention).
        var sut = TestAggregate.Rehydrate(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.That(sut.DomainEvents, Is.Empty);
    }
}
