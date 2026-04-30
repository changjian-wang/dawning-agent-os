using Dawning.AgentOS.Domain.Permissions;
using Dawning.AgentOS.Domain.Services.Permissions;
using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Services.Tests.Permissions;

[TestFixture]
public sealed class ActionClassifierTests
{
    private static readonly ActionClassifier Sut = new();

    [Test]
    public void Classify_ReadSummarize_IsL0()
        => Assert.That(Sut.Classify(ActionKind.ReadSummarize), Is.EqualTo(ActionLevel.L0));

    [Test]
    public void Classify_ReadClassify_IsL0()
        => Assert.That(Sut.Classify(ActionKind.ReadClassify), Is.EqualTo(ActionLevel.L0));

    [Test]
    public void Classify_ReadTag_IsL1()
        => Assert.That(Sut.Classify(ActionKind.ReadTag), Is.EqualTo(ActionLevel.L1));

    [Test]
    public void Classify_InboxAdd_IsL1()
        => Assert.That(Sut.Classify(ActionKind.InboxAdd), Is.EqualTo(ActionLevel.L1));

    [Test]
    public void Classify_MemoryWrite_IsL2()
        => Assert.That(Sut.Classify(ActionKind.MemoryWrite), Is.EqualTo(ActionLevel.L2));

    [Test]
    public void Classify_MemoryDelete_IsL3()
        => Assert.That(Sut.Classify(ActionKind.MemoryDelete), Is.EqualTo(ActionLevel.L3));

    [Test]
    public void Classify_NullKind_Throws()
    {
        Assert.That(
            () => Sut.Classify(null!),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Classify_UnknownCode_Throws()
    {
        // Unknown actions must not silently default to a permissive level;
        // shipping a new action without an explicit risk decision is a bug.
        Assert.That(
            () => Sut.Classify(new ActionKind("unknown.thing")),
            Throws.ArgumentException);
    }

    [Test]
    public void Classify_ManuallyConstructed_StillResolvesByCode()
    {
        // Equality-by-code means callers don't have to use the static field.
        Assert.That(
            Sut.Classify(new ActionKind("memory.write")),
            Is.EqualTo(ActionLevel.L2));
    }

    [Test]
    public void Classify_IsDeterministic()
    {
        var first = Sut.Classify(ActionKind.MemoryDelete);
        var second = Sut.Classify(ActionKind.MemoryDelete);

        Assert.That(first, Is.EqualTo(second));
    }
}
