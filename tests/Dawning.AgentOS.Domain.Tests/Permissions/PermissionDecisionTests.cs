using Dawning.AgentOS.Domain.Permissions;
using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Tests.Permissions;

[TestFixture]
public sealed class PermissionDecisionTests
{
    [Test]
    public void Allowed_HasSingletonInstance_AndNoPublicConstructor()
    {
        Assert.That(PermissionDecision.Allowed.Instance, Is.Not.Null);

        var publicCtors = typeof(PermissionDecision.Allowed).GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

        // Records auto-generate a public copy ctor; that's expected. The
        // parameterless ctor must remain non-public so callers cannot
        // bypass the singleton.
        var declaredParameterlessPublic = System.Array.FindAll(
            publicCtors,
            c => c.GetParameters().Length == 0);

        Assert.That(declaredParameterlessPublic, Is.Empty);
    }

    [Test]
    public void RequiresConfirmation_CarriesReason()
    {
        var decision = new PermissionDecision.RequiresConfirmation("editing this file is irreversible");

        Assert.That(decision.Reason, Is.EqualTo("editing this file is irreversible"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void RequiresConfirmation_NullOrWhitespaceReason_Throws(string? reason)
    {
        Assert.That(
            () => new PermissionDecision.RequiresConfirmation(reason!),
            Throws.ArgumentException);
    }

    [Test]
    public void Denied_CarriesReason()
    {
        var decision = new PermissionDecision.Denied("memory deletion is disabled in this profile");

        Assert.That(decision.Reason, Is.EqualTo("memory deletion is disabled in this profile"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Denied_NullOrWhitespaceReason_Throws(string? reason)
    {
        Assert.That(
            () => new PermissionDecision.Denied(reason!),
            Throws.ArgumentException);
    }

    [Test]
    public void Variants_AreDistinctTypes()
    {
        // Pattern-matching on the closed hierarchy must remain unambiguous.
        PermissionDecision allowed = PermissionDecision.Allowed.Instance;
        PermissionDecision confirm = new PermissionDecision.RequiresConfirmation("r");
        PermissionDecision denied = new PermissionDecision.Denied("r");

        Assert.Multiple(() =>
        {
            Assert.That(allowed, Is.InstanceOf<PermissionDecision.Allowed>());
            Assert.That(confirm, Is.InstanceOf<PermissionDecision.RequiresConfirmation>());
            Assert.That(denied, Is.InstanceOf<PermissionDecision.Denied>());
        });
    }

    [Test]
    public void Equality_RequiresConfirmation_IsValueBased()
    {
        var a = new PermissionDecision.RequiresConfirmation("r");
        var b = new PermissionDecision.RequiresConfirmation("r");

        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Equality_DifferentVariant_AreNotEqual()
    {
        PermissionDecision confirm = new PermissionDecision.RequiresConfirmation("r");
        PermissionDecision denied = new PermissionDecision.Denied("r");

        Assert.That(confirm, Is.Not.EqualTo(denied));
    }

    [Test]
    public void ExternalSubclassing_IsForbidden()
    {
        // The base ctor is private; only the three nested records can extend.
        var ctors = typeof(PermissionDecision).GetConstructors(
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);

        // Records auto-generate a protected copy ctor; the only declared
        // primary / parameterless ctor must be private.
        var declaredParameterlessCtors = System.Array.FindAll(
            ctors,
            c => c.GetParameters().Length == 0);

        Assert.That(declaredParameterlessCtors, Has.Length.EqualTo(1));
        Assert.That(declaredParameterlessCtors[0].IsPrivate, Is.True);
    }
}
