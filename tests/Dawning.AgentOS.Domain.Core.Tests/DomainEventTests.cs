using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Core.Tests;

[TestFixture]
public sealed class DomainEventTests
{
    [Test]
    public void IDomainEvent_IsAPlainMarkerInterface()
    {
        // Per ADR-022 IDomainEvent has no external base interface; dispatch
        // is handled by the self-built IDomainEventDispatcher port declared
        // in Application/Abstractions, with the implementation living in
        // the infrastructure layer. The earlier MediatR.INotification
        // base was removed when the entire MediatR stack was dropped.
        var iface = typeof(IDomainEvent);

        Assert.Multiple(() =>
        {
            Assert.That(iface.IsInterface, Is.True);
            Assert.That(iface.GetInterfaces(), Is.Empty);
        });
    }

    [Test]
    public void IDomainEventHandler_IsAGenericContravariantInterface()
    {
        var iface = typeof(IDomainEventHandler<>);

        Assert.That(iface.IsInterface, Is.True);
        Assert.That(iface.IsGenericTypeDefinition, Is.True);

        var typeArgs = iface.GetGenericArguments();
        Assert.That(typeArgs, Has.Length.EqualTo(1));

        var tEvent = typeArgs[0];
        Assert.Multiple(() =>
        {
            // Contravariant marker (in TEvent): a handler for a base event
            // type can accept derived event instances.
            Assert.That(
                tEvent.GenericParameterAttributes
                    & System.Reflection.GenericParameterAttributes.Contravariant,
                Is.EqualTo(System.Reflection.GenericParameterAttributes.Contravariant)
            );

            // Constraint: TEvent : IDomainEvent
            Assert.That(
                tEvent.GetGenericParameterConstraints(),
                Does.Contain(typeof(IDomainEvent))
            );
        });
    }
}
