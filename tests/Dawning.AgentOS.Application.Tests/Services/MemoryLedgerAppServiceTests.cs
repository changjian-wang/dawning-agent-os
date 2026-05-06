using Dawning.AgentOS.Application.Abstractions;
using Dawning.AgentOS.Application.Memory;
using Dawning.AgentOS.Application.Services;
using Dawning.AgentOS.Domain.Memory;
using Moq;
using NUnit.Framework;

namespace Dawning.AgentOS.Application.Tests.Services;

/// <summary>
/// Unit tests for <see cref="MemoryLedgerAppService"/>. Per ADR-033
/// §决策 J1 the service validates input DTOs, stamps timestamps from
/// <see cref="IClock"/>, forces user-explicit invariants on create,
/// and translates state-machine exceptions to the
/// <c>memory.invalidStatusTransition</c> error code.
/// </summary>
[TestFixture]
public sealed class MemoryLedgerAppServiceTests
{
    private static readonly DateTimeOffset SampleNow = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = SampleNow.AddHours(1);

    // ===============================================================
    // CreateExplicitAsync
    // ===============================================================

    [Test]
    public async Task CreateExplicitAsync_ForcesUserExplicitSourceAndStampsTimestamps()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);

        MemoryLedgerEntry? persisted = null;
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<MemoryLedgerEntry, CancellationToken>((e, _) => persisted = e)
            .Returns(Task.CompletedTask);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.CreateExplicitAsync(
            new CreateMemoryEntryRequest("favorite tea: longjing", null, null),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(persisted, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.Source, Is.EqualTo(MemorySource.UserExplicit));
            Assert.That(persisted.IsExplicit, Is.True);
            Assert.That(persisted.Confidence, Is.EqualTo(1.0));
            Assert.That(persisted.Sensitivity, Is.EqualTo(MemorySensitivity.Normal));
            Assert.That(persisted.Status, Is.EqualTo(MemoryStatus.Active));
            Assert.That(persisted.CreatedAt, Is.EqualTo(SampleNow));
            Assert.That(persisted.UpdatedAtUtc, Is.EqualTo(SampleNow));
        });
    }

    [Test]
    public async Task CreateExplicitAsync_ReturnsDtoMatchingPersistedAggregate()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);

        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.CreateExplicitAsync(
            new CreateMemoryEntryRequest("x", "project:y", MemorySensitivity.Sensitive),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        var dto = result.Value;
        Assert.Multiple(() =>
        {
            Assert.That(dto.Content, Is.EqualTo("x"));
            Assert.That(dto.Scope, Is.EqualTo("project:y"));
            Assert.That(dto.Sensitivity, Is.EqualTo(MemorySensitivity.Sensitive));
            Assert.That(dto.Status, Is.EqualTo(MemoryStatus.Active));
            Assert.That(dto.IsExplicit, Is.True);
            Assert.That(dto.Confidence, Is.EqualTo(1.0));
            Assert.That(dto.DeletedAt, Is.Null);
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateExplicitAsync_ReturnsFieldFailure_WhenContentBlank(string blank)
    {
        var sut = NewServiceWithStrictRepository();

        var result = await sut.CreateExplicitAsync(
            new CreateMemoryEntryRequest(blank, null, null),
            CancellationToken.None
        );

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Errors, Has.Length.EqualTo(1));
            Assert.That(result.Errors[0].Field, Is.EqualTo("content"));
            Assert.That(result.Errors[0].Code, Is.EqualTo("memory.content.required"));
        });
    }

    [Test]
    public async Task CreateExplicitAsync_ReturnsFieldFailure_WhenContentTooLong()
    {
        var sut = NewServiceWithStrictRepository();
        var oversize = new string('a', MemoryLedgerEntry.MaxContentLength + 1);

        var result = await sut.CreateExplicitAsync(
            new CreateMemoryEntryRequest(oversize, null, null),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo("memory.content.tooLong"));
    }

    [Test]
    public async Task CreateExplicitAsync_ReturnsFieldFailure_WhenScopeWhitespace()
    {
        var sut = NewServiceWithStrictRepository();

        var result = await sut.CreateExplicitAsync(
            new CreateMemoryEntryRequest("x", "   ", null),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Field, Is.EqualTo("scope"));
        Assert.That(result.Errors[0].Code, Is.EqualTo("memory.scope.invalid"));
    }

    [Test]
    public async Task CreateExplicitAsync_ReturnsFieldFailure_WhenSensitivityUndefined()
    {
        var sut = NewServiceWithStrictRepository();

        var result = await sut.CreateExplicitAsync(
            new CreateMemoryEntryRequest("x", null, (MemorySensitivity)99),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo("memory.sensitivity.invalid"));
    }

    // ===============================================================
    // ListAsync
    // ===============================================================

    [Test]
    public async Task ListAsync_ReturnsPageWithFilteredItems()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);

        var entry = MemoryLedgerEntry.Create(
            "x",
            null,
            MemorySource.UserExplicit,
            true,
            1.0,
            MemorySensitivity.Normal,
            SampleNow
        );

        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.ListAsync(null, false, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });
        repo.Setup(r => r.CountAsync(null, false, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.ListAsync(
            new MemoryListQuery(50, 0, null, false),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Items, Has.Count.EqualTo(1));
        Assert.That(result.Value.Items[0].Id, Is.EqualTo(entry.Id));
        Assert.That(result.Value.Total, Is.EqualTo(7));
        Assert.That(result.Value.Limit, Is.EqualTo(50));
        Assert.That(result.Value.Offset, Is.EqualTo(0));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(MemoryLedgerAppService.MaxListLimit + 1)]
    public async Task ListAsync_RejectsLimitOutOfRange(int badLimit)
    {
        var sut = NewServiceWithStrictRepository();

        var result = await sut.ListAsync(
            new MemoryListQuery(badLimit, 0, null, false),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo("memory.limit.outOfRange"));
    }

    [Test]
    public async Task ListAsync_RejectsNegativeOffset()
    {
        var sut = NewServiceWithStrictRepository();

        var result = await sut.ListAsync(
            new MemoryListQuery(50, -1, null, false),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo("memory.offset.outOfRange"));
    }

    [Test]
    public async Task ListAsync_RejectsUndefinedStatus()
    {
        var sut = NewServiceWithStrictRepository();

        var result = await sut.ListAsync(
            new MemoryListQuery(50, 0, (MemoryStatus)99, false),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo("memory.status.invalid"));
    }

    [Test]
    public async Task ListAsync_PassesIncludeSoftDeletedThroughToRepository()
    {
        var clock = new Mock<IClock>();
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.ListAsync(null, true, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MemoryLedgerEntry>());
        repo.Setup(r => r.CountAsync(null, true, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.ListAsync(
            new MemoryListQuery(50, 0, null, true),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        repo.Verify(r => r.ListAsync(null, true, 50, 0, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.CountAsync(null, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ===============================================================
    // GetByIdAsync
    // ===============================================================

    [Test]
    public async Task GetByIdAsync_ReturnsNotFound_WhenRepositoryMisses()
    {
        var clock = new Mock<IClock>();
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryLedgerEntry?)null);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Errors[0].Code, Is.EqualTo(MemoryErrors.NotFoundCode));
        });
    }

    [Test]
    public async Task GetByIdAsync_ReturnsDtoOnHit()
    {
        var clock = new Mock<IClock>();
        var entry = NewActiveEntry();

        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.GetByIdAsync(entry.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Id, Is.EqualTo(entry.Id));
    }

    // ===============================================================
    // UpdateAsync
    // ===============================================================

    [Test]
    public async Task UpdateAsync_ReturnsEmptyError_WhenAllFieldsNull()
    {
        var sut = NewServiceWithStrictRepository();

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            new UpdateMemoryEntryRequest(null, null, null, null),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo(MemoryErrors.UpdateEmptyCode));
    }

    [Test]
    public async Task UpdateAsync_ReturnsNotFound_WhenRepositoryMisses()
    {
        var clock = new Mock<IClock>();
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryLedgerEntry?)null);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            new UpdateMemoryEntryRequest("new", null, null, null),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo(MemoryErrors.NotFoundCode));
    }

    [Test]
    public async Task UpdateAsync_AppliesContentScopeAndSensitivityAndStatusTransitions()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Later);

        var entry = NewActiveEntry();
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        repo.Setup(r => r.UpdateAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.UpdateAsync(
            entry.Id,
            new UpdateMemoryEntryRequest(
                "new content",
                "project:dawning",
                MemorySensitivity.Sensitive,
                MemoryStatus.Corrected
            ),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Content, Is.EqualTo("new content"));
            Assert.That(result.Value.Scope, Is.EqualTo("project:dawning"));
            Assert.That(result.Value.Sensitivity, Is.EqualTo(MemorySensitivity.Sensitive));
            Assert.That(result.Value.Status, Is.EqualTo(MemoryStatus.Corrected));
            Assert.That(result.Value.UpdatedAt, Is.EqualTo(Later));
        });
    }

    [Test]
    public async Task UpdateAsync_TranslatesIllegalStatusTransitionToInvariantError()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Later);

        var entry = NewActiveEntry();
        entry.SoftDelete(SampleNow.AddMinutes(1));

        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.UpdateAsync(
            entry.Id,
            new UpdateMemoryEntryRequest(null, null, null, MemoryStatus.Archived),
            CancellationToken.None
        );

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(
                result.Errors[0].Code,
                Is.EqualTo(MemoryErrors.InvalidStatusTransitionCode)
            );
        });
    }

    [Test]
    public async Task UpdateAsync_StatusActiveOnSoftDeleted_RestoresEntry()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Later);

        var entry = NewActiveEntry();
        entry.SoftDelete(SampleNow.AddMinutes(1));

        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        repo.Setup(r => r.UpdateAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.UpdateAsync(
            entry.Id,
            new UpdateMemoryEntryRequest(null, null, null, MemoryStatus.Active),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Status, Is.EqualTo(MemoryStatus.Active));
        Assert.That(result.Value.DeletedAt, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_StatusEqualToCurrent_IsNoOpAndSucceeds()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Later);

        var entry = NewActiveEntry();
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        repo.Setup(r => r.UpdateAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.UpdateAsync(
            entry.Id,
            new UpdateMemoryEntryRequest("new", null, null, MemoryStatus.Active),
            CancellationToken.None
        );

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Status, Is.EqualTo(MemoryStatus.Active));
    }

    [Test]
    public async Task UpdateAsync_RaceConditionVanishingRow_SurfacesNotFound()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Later);

        var entry = NewActiveEntry();
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        repo.Setup(r => r.UpdateAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.UpdateAsync(
            entry.Id,
            new UpdateMemoryEntryRequest("new", null, null, null),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo(MemoryErrors.NotFoundCode));
    }

    [Test]
    public async Task UpdateAsync_RejectsBlankContent()
    {
        var sut = NewServiceWithStrictRepository();

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            new UpdateMemoryEntryRequest("   ", null, null, null),
            CancellationToken.None
        );

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo("memory.content.required"));
    }

    // ===============================================================
    // SoftDeleteAsync
    // ===============================================================

    [Test]
    public async Task SoftDeleteAsync_ReturnsNotFound_WhenRepositoryMisses()
    {
        var clock = new Mock<IClock>();
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryLedgerEntry?)null);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.SoftDeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo(MemoryErrors.NotFoundCode));
    }

    [Test]
    public async Task SoftDeleteAsync_TransitionsActiveToSoftDeletedAndStampsDeletedAt()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Later);

        var entry = NewActiveEntry();
        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        repo.Setup(r => r.UpdateAsync(It.IsAny<MemoryLedgerEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.SoftDeleteAsync(entry.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.Status, Is.EqualTo(MemoryStatus.SoftDeleted));
            Assert.That(result.Value.DeletedAt, Is.EqualTo(Later));
            Assert.That(result.Value.UpdatedAt, Is.EqualTo(Later));
        });
    }

    [Test]
    public async Task SoftDeleteAsync_RejectedOnAlreadySoftDeleted()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Later);

        var entry = NewActiveEntry();
        entry.SoftDelete(SampleNow.AddMinutes(1));

        var repo = new Mock<IMemoryLedgerRepository>();
        repo.Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var sut = new MemoryLedgerAppService(clock.Object, repo.Object);

        var result = await sut.SoftDeleteAsync(entry.Id, CancellationToken.None);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Errors[0].Code, Is.EqualTo(MemoryErrors.InvalidStatusTransitionCode));
    }

    // ===============================================================
    // helpers
    // ===============================================================

    private static MemoryLedgerEntry NewActiveEntry() =>
        MemoryLedgerEntry.Create(
            "seed",
            null,
            MemorySource.UserExplicit,
            true,
            1.0,
            MemorySensitivity.Normal,
            SampleNow
        );

    private static MemoryLedgerAppService NewServiceWithStrictRepository()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(SampleNow);

        var repo = new Mock<IMemoryLedgerRepository>(MockBehavior.Strict);
        return new MemoryLedgerAppService(clock.Object, repo.Object);
    }
}
