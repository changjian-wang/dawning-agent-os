using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Core.Tests;

[TestFixture]
public sealed class DomainErrorTests
{
    [Test]
    public void Constructor_AllFields_AreSet()
    {
        var sut = new DomainError("auth.unauthorized", "Not signed in", "token");

        Assert.Multiple(() =>
        {
            Assert.That(sut.Code, Is.EqualTo("auth.unauthorized"));
            Assert.That(sut.Message, Is.EqualTo("Not signed in"));
            Assert.That(sut.Field, Is.EqualTo("token"));
        });
    }

    [Test]
    public void Field_DefaultsToNull()
    {
        var sut = new DomainError("x", "y");

        Assert.That(sut.Field, Is.Null);
    }

    [Test]
    public void Equality_IsValueBased()
    {
        var a = new DomainError("c", "m", "f");
        var b = new DomainError("c", "m", "f");

        Assert.Multiple(() =>
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void Equality_DifferentField_AreNotEqual()
    {
        var a = new DomainError("c", "m", "f1");
        var b = new DomainError("c", "m", "f2");

        Assert.That(a, Is.Not.EqualTo(b));
    }
}
