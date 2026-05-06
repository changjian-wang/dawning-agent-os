using Dawning.AgentOS.Infrastructure.Hosting;
using NUnit.Framework;

namespace Dawning.AgentOS.Infrastructure.Tests.Hosting;

/// <summary>
/// Tests for <see cref="AppDataPathProvider"/>. Per ADR-024 §B1 the
/// provider must return a non-empty absolute path that ends with the
/// <c>dawning-agent-os/agentos.db</c> tail and creates the containing
/// directory eagerly.
/// </summary>
[TestFixture]
public class AppDataPathProviderTests
{
    [Test]
    public void GetDatabasePath_ReturnsPathThatEndsWithExpectedAppFolderAndFile()
    {
        var sut = new AppDataPathProvider();

        var path = sut.GetDatabasePath();

        Assert.That(path, Is.Not.Null.And.Not.Empty);
        Assert.That(Path.IsPathRooted(path), Is.True, "path must be absolute");
        Assert.That(
            path,
            Does.EndWith(Path.Combine("dawning-agent-os", "agentos.db")),
            "path tail must be dawning-agent-os/agentos.db"
        );
    }

    [Test]
    public void GetDatabasePath_CreatesParentDirectory()
    {
        var sut = new AppDataPathProvider();

        var path = sut.GetDatabasePath();

        var directory = Path.GetDirectoryName(path);
        Assert.That(directory, Is.Not.Null.And.Not.Empty);
        Assert.That(
            Directory.Exists(directory!),
            Is.True,
            "parent directory must be created eagerly so SQLite can write the file"
        );
    }

    [Test]
    public void GetDatabasePath_IsIdempotent()
    {
        var sut = new AppDataPathProvider();

        var first = sut.GetDatabasePath();
        var second = sut.GetDatabasePath();

        Assert.That(second, Is.EqualTo(first), "the resolved path must be deterministic");
    }
}
