using Dawning.AgentOS.Domain.Permissions;
using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Tests.Permissions;

[TestFixture]
public sealed class ActionKindTests
{
    [Test]
    public void Constructor_AssignsCode()
    {
        var kind = new ActionKind("custom.action");

        Assert.That(kind.Code, Is.EqualTo("custom.action"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t")]
    public void Constructor_NullOrWhitespace_Throws(string? code)
    {
        Assert.That(
            () => new ActionKind(code!),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("code"));
    }

    [Test]
    public void Equality_IsValueBased()
    {
        var a = new ActionKind("memory.write");
        var b = new ActionKind("memory.write");

        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Equality_DifferentCode_AreNotEqual()
    {
        Assert.That(new ActionKind("a"), Is.Not.EqualTo(new ActionKind("b")));
    }

    [Test]
    public void ToString_ReturnsCode()
    {
        Assert.That(new ActionKind("inbox.add").ToString(), Is.EqualTo("inbox.add"));
    }

    [Test]
    public void V0Vocabulary_StaticInstancesHaveExpectedCodes()
    {
        // The codes are persisted / surfaced in audit logs; pin them down.
        Assert.Multiple(() =>
        {
            Assert.That(ActionKind.ReadSummarize.Code, Is.EqualTo("read.summarize"));
            Assert.That(ActionKind.ReadClassify.Code, Is.EqualTo("read.classify"));
            Assert.That(ActionKind.ReadTag.Code, Is.EqualTo("read.tag"));
            Assert.That(ActionKind.InboxAdd.Code, Is.EqualTo("inbox.add"));
            Assert.That(ActionKind.MemoryWrite.Code, Is.EqualTo("memory.write"));
            Assert.That(ActionKind.MemoryDelete.Code, Is.EqualTo("memory.delete"));
        });
    }

    [Test]
    public void V0Vocabulary_StaticInstance_EqualsManuallyConstructed()
    {
        Assert.That(ActionKind.MemoryDelete, Is.EqualTo(new ActionKind("memory.delete")));
    }
}
