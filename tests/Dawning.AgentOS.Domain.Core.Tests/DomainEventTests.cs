using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Core.Tests;

[TestFixture]
public sealed class DomainEventTests
{
    [Test]
    public void IDomainEvent_InheritsMediatRINotification()
    {
        // Domain.Core touches MediatR only via the Contracts abstraction package.
        Assert.That(
            typeof(MediatR.INotification).IsAssignableFrom(typeof(IDomainEvent)),
            Is.True);
    }
}
