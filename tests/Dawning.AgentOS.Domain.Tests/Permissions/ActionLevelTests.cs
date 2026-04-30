using Dawning.AgentOS.Domain.Permissions;
using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Tests.Permissions;

[TestFixture]
public sealed class ActionLevelTests
{
    [Test]
    public void Ordinals_AreStableAcrossVersions()
    {
        // Persisted / serialized as the ordinal; pinning prevents accidental
        // reordering that would silently change historical records.
        Assert.Multiple(() =>
        {
            Assert.That((int)ActionLevel.L0, Is.EqualTo(0));
            Assert.That((int)ActionLevel.L1, Is.EqualTo(1));
            Assert.That((int)ActionLevel.L2, Is.EqualTo(2));
            Assert.That((int)ActionLevel.L3, Is.EqualTo(3));
        });
    }
}
