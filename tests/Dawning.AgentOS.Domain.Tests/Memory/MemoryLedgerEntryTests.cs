using Dawning.AgentOS.Domain.Memory;
using NUnit.Framework;

namespace Dawning.AgentOS.Domain.Tests.Memory;

/// <summary>
/// Tests for the <see cref="MemoryLedgerEntry"/> aggregate root and its
/// state machine. Per ADR-033 §决策 A1 the V0 invariants are:
/// non-empty content ≤ 4096 chars, optional non-whitespace scope ≤ 128
/// chars, defined enum values for source / sensitivity, finite
/// confidence in [0.0, 1.0], a UUIDv7 identifier anchored to creation,
/// and a four-state lifecycle with the legal transitions documented in
/// ADR-033 §决策 A1 and §决策 G1.
/// </summary>
[TestFixture]
public sealed class MemoryLedgerEntryTests
{
    private static readonly DateTimeOffset SampleCreatedAt = new(
        2026,
        5,
        6,
        12,
        0,
        0,
        TimeSpan.Zero
    );

    private static readonly DateTimeOffset LaterAt = SampleCreatedAt.AddHours(1);

    // ---------------------------------------------------------------
    // Create — happy path + validation
    // ---------------------------------------------------------------

    [Test]
    public void Create_PopulatesPropertiesAndStampsUuidV7Id()
    {
        var entry = MemoryLedgerEntry.Create(
            content: "remember tea preferences",
            scope: null,
            source: MemorySource.UserExplicit,
            isExplicit: true,
            confidence: 1.0,
            sensitivity: MemorySensitivity.Normal,
            createdAtUtc: SampleCreatedAt
        );

        Assert.Multiple(() =>
        {
            Assert.That(entry.Content, Is.EqualTo("remember tea preferences"));
            Assert.That(entry.Scope, Is.EqualTo(MemoryLedgerEntry.DefaultScope));
            Assert.That(entry.Source, Is.EqualTo(MemorySource.UserExplicit));
            Assert.That(entry.IsExplicit, Is.True);
            Assert.That(entry.Confidence, Is.EqualTo(1.0));
            Assert.That(entry.Sensitivity, Is.EqualTo(MemorySensitivity.Normal));
            Assert.That(entry.Status, Is.EqualTo(MemoryStatus.Active));
            Assert.That(entry.CreatedAt, Is.EqualTo(SampleCreatedAt));
            Assert.That(entry.UpdatedAtUtc, Is.EqualTo(SampleCreatedAt));
            Assert.That(entry.DeletedAtUtc, Is.Null);
            Assert.That(entry.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(
                entry.Id.Version,
                Is.EqualTo(7),
                "ADR-033 §决策 A1: identifier must be UUIDv7"
            );
            Assert.That(
                entry.DomainEvents,
                Is.Empty,
                "V0 raises no domain events per ADR-033 §背景"
            );
        });
    }

    [Test]
    public void Create_RespectsExplicitScopeOverride()
    {
        var entry = MemoryLedgerEntry.Create(
            "x",
            "project:dawning",
            MemorySource.UserExplicit,
            true,
            1.0,
            MemorySensitivity.Normal,
            SampleCreatedAt
        );

        Assert.That(entry.Scope, Is.EqualTo("project:dawning"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t\n  ")]
    public void Create_ThrowsOnWhitespaceContent(string blank)
    {
        Assert.That(
            () =>
                MemoryLedgerEntry.Create(
                    blank,
                    null,
                    MemorySource.UserExplicit,
                    true,
                    1.0,
                    MemorySensitivity.Normal,
                    SampleCreatedAt
                ),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("content")
        );
    }

    [Test]
    public void Create_ThrowsWhenContentExceedsMaxLength()
    {
        var oversize = new string('a', MemoryLedgerEntry.MaxContentLength + 1);

        Assert.That(
            () =>
                MemoryLedgerEntry.Create(
                    oversize,
                    null,
                    MemorySource.UserExplicit,
                    true,
                    1.0,
                    MemorySensitivity.Normal,
                    SampleCreatedAt
                ),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("content")
        );
    }

    [Test]
    public void Create_ThrowsOnWhitespaceScope()
    {
        Assert.That(
            () =>
                MemoryLedgerEntry.Create(
                    "x",
                    "   ",
                    MemorySource.UserExplicit,
                    true,
                    1.0,
                    MemorySensitivity.Normal,
                    SampleCreatedAt
                ),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("scope")
        );
    }

    [Test]
    public void Create_ThrowsOnUndefinedSource()
    {
        Assert.That(
            () =>
                MemoryLedgerEntry.Create(
                    "x",
                    null,
                    (MemorySource)99,
                    true,
                    1.0,
                    MemorySensitivity.Normal,
                    SampleCreatedAt
                ),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("source")
        );
    }

    [Test]
    public void Create_ThrowsOnUndefinedSensitivity()
    {
        Assert.That(
            () =>
                MemoryLedgerEntry.Create(
                    "x",
                    null,
                    MemorySource.UserExplicit,
                    true,
                    1.0,
                    (MemorySensitivity)99,
                    SampleCreatedAt
                ),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("sensitivity")
        );
    }

    [TestCase(-0.01)]
    [TestCase(1.01)]
    [TestCase(double.NaN)]
    public void Create_ThrowsOnConfidenceOutOfRange(double bad)
    {
        Assert.That(
            () =>
                MemoryLedgerEntry.Create(
                    "x",
                    null,
                    MemorySource.UserExplicit,
                    true,
                    bad,
                    MemorySensitivity.Normal,
                    SampleCreatedAt
                ),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("confidence")
        );
    }

    [Test]
    public void Create_ThrowsWhenCreatedAtIsNotUtc()
    {
        var local = new DateTimeOffset(2026, 5, 6, 12, 0, 0, TimeSpan.FromHours(8));

        Assert.That(
            () =>
                MemoryLedgerEntry.Create(
                    "x",
                    null,
                    MemorySource.UserExplicit,
                    true,
                    1.0,
                    MemorySensitivity.Normal,
                    local
                ),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("createdAtUtc")
        );
    }

    // ---------------------------------------------------------------
    // Rehydrate — preserves persisted state without raising events
    // ---------------------------------------------------------------

    [Test]
    public void Rehydrate_RestoresFullStateAndRaisesNoEvents()
    {
        var id = Guid.CreateVersion7(SampleCreatedAt);

        var entry = MemoryLedgerEntry.Rehydrate(
            id: id,
            content: "remembered fact",
            scope: "project:x",
            source: MemorySource.UserExplicit,
            isExplicit: true,
            confidence: 0.7,
            sensitivity: MemorySensitivity.Sensitive,
            status: MemoryStatus.Archived,
            createdAtUtc: SampleCreatedAt,
            updatedAtUtc: LaterAt,
            deletedAtUtc: null
        );

        Assert.Multiple(() =>
        {
            Assert.That(entry.Id, Is.EqualTo(id));
            Assert.That(entry.Content, Is.EqualTo("remembered fact"));
            Assert.That(entry.Scope, Is.EqualTo("project:x"));
            Assert.That(entry.Source, Is.EqualTo(MemorySource.UserExplicit));
            Assert.That(entry.IsExplicit, Is.True);
            Assert.That(entry.Confidence, Is.EqualTo(0.7));
            Assert.That(entry.Sensitivity, Is.EqualTo(MemorySensitivity.Sensitive));
            Assert.That(entry.Status, Is.EqualTo(MemoryStatus.Archived));
            Assert.That(entry.CreatedAt, Is.EqualTo(SampleCreatedAt));
            Assert.That(entry.UpdatedAtUtc, Is.EqualTo(LaterAt));
            Assert.That(entry.DeletedAtUtc, Is.Null);
            Assert.That(entry.DomainEvents, Is.Empty);
        });
    }

    // ---------------------------------------------------------------
    // UpdateContent / UpdateScope / UpdateSensitivity
    // ---------------------------------------------------------------

    [Test]
    public void UpdateContent_StampsUpdatedAtAndPreservesIdAndStatus()
    {
        var entry = NewActive();
        var idBefore = entry.Id;

        entry.UpdateContent("new content", LaterAt);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Content, Is.EqualTo("new content"));
            Assert.That(entry.UpdatedAtUtc, Is.EqualTo(LaterAt));
            Assert.That(entry.Id, Is.EqualTo(idBefore));
            Assert.That(entry.Status, Is.EqualTo(MemoryStatus.Active));
        });
    }

    [Test]
    public void UpdateContent_AllowedFromCorrectedStatus()
    {
        var entry = NewActive();
        entry.MarkCorrected(LaterAt);

        Assert.That(
            () => entry.UpdateContent("post-correction edit", LaterAt.AddMinutes(1)),
            Throws.Nothing
        );
        Assert.That(entry.Status, Is.EqualTo(MemoryStatus.Corrected));
    }

    [Test]
    public void UpdateScope_RejectsOversize()
    {
        var entry = NewActive();
        var oversize = new string('s', MemoryLedgerEntry.MaxScopeLength + 1);

        Assert.That(
            () => entry.UpdateScope(oversize, LaterAt),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("scope")
        );
    }

    // ---------------------------------------------------------------
    // State machine — legal transitions
    // ---------------------------------------------------------------

    [Test]
    public void MarkCorrected_TransitionsActiveToCorrected()
    {
        var entry = NewActive();

        entry.MarkCorrected(LaterAt);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Status, Is.EqualTo(MemoryStatus.Corrected));
            Assert.That(entry.UpdatedAtUtc, Is.EqualTo(LaterAt));
        });
    }

    [Test]
    public void Archive_AllowedFromActiveAndCorrected()
    {
        var fromActive = NewActive();
        fromActive.Archive(LaterAt);
        Assert.That(fromActive.Status, Is.EqualTo(MemoryStatus.Archived));

        var fromCorrected = NewActive();
        fromCorrected.MarkCorrected(LaterAt);
        fromCorrected.Archive(LaterAt.AddMinutes(1));
        Assert.That(fromCorrected.Status, Is.EqualTo(MemoryStatus.Archived));
    }

    [Test]
    public void SoftDelete_StampsDeletedAtUtcAndUpdatedAt()
    {
        var entry = NewActive();

        entry.SoftDelete(LaterAt);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Status, Is.EqualTo(MemoryStatus.SoftDeleted));
            Assert.That(entry.DeletedAtUtc, Is.EqualTo(LaterAt));
            Assert.That(entry.UpdatedAtUtc, Is.EqualTo(LaterAt));
        });
    }

    [Test]
    public void SoftDelete_AllowedFromArchived()
    {
        var entry = NewActive();
        entry.Archive(LaterAt);
        entry.SoftDelete(LaterAt.AddMinutes(1));

        Assert.That(entry.Status, Is.EqualTo(MemoryStatus.SoftDeleted));
    }

    [Test]
    public void Restore_TransitionsSoftDeletedBackToActiveAndClearsDeletedAt()
    {
        var entry = NewActive();
        entry.SoftDelete(LaterAt);

        entry.Restore(LaterAt.AddMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That(entry.Status, Is.EqualTo(MemoryStatus.Active));
            Assert.That(entry.DeletedAtUtc, Is.Null);
            Assert.That(entry.UpdatedAtUtc, Is.EqualTo(LaterAt.AddMinutes(1)));
        });
    }

    // ---------------------------------------------------------------
    // State machine — illegal transitions raise the dedicated exception
    // ---------------------------------------------------------------

    [Test]
    public void UpdateContent_RejectedOnSoftDeletedEntry()
    {
        var entry = NewActive();
        entry.SoftDelete(LaterAt);

        Assert.That(
            () => entry.UpdateContent("late edit", LaterAt.AddMinutes(1)),
            Throws
                .TypeOf<MemoryLedgerInvalidStatusTransitionException>()
                .With.Property("CurrentStatus")
                .EqualTo(MemoryStatus.SoftDeleted)
        );
    }

    [Test]
    public void MarkCorrected_RejectedFromCorrected()
    {
        var entry = NewActive();
        entry.MarkCorrected(LaterAt);

        Assert.That(
            () => entry.MarkCorrected(LaterAt.AddMinutes(1)),
            Throws
                .TypeOf<MemoryLedgerInvalidStatusTransitionException>()
                .With.Property("CurrentStatus")
                .EqualTo(MemoryStatus.Corrected)
                .And.Property("AttemptedAction")
                .EqualTo("MarkCorrected")
        );
    }

    [Test]
    public void Archive_RejectedFromSoftDeleted()
    {
        var entry = NewActive();
        entry.SoftDelete(LaterAt);

        Assert.That(
            () => entry.Archive(LaterAt.AddMinutes(1)),
            Throws
                .TypeOf<MemoryLedgerInvalidStatusTransitionException>()
                .With.Property("CurrentStatus")
                .EqualTo(MemoryStatus.SoftDeleted)
        );
    }

    [Test]
    public void SoftDelete_RejectedOnAlreadySoftDeleted()
    {
        var entry = NewActive();
        entry.SoftDelete(LaterAt);

        Assert.That(
            () => entry.SoftDelete(LaterAt.AddMinutes(1)),
            Throws.TypeOf<MemoryLedgerInvalidStatusTransitionException>()
        );
    }

    [Test]
    public void Restore_RejectedFromActive()
    {
        var entry = NewActive();

        Assert.That(
            () => entry.Restore(LaterAt),
            Throws
                .TypeOf<MemoryLedgerInvalidStatusTransitionException>()
                .With.Property("CurrentStatus")
                .EqualTo(MemoryStatus.Active)
        );
    }

    [Test]
    public void Restore_RejectedFromArchived()
    {
        var entry = NewActive();
        entry.Archive(LaterAt);

        Assert.That(
            () => entry.Restore(LaterAt.AddMinutes(1)),
            Throws
                .TypeOf<MemoryLedgerInvalidStatusTransitionException>()
                .With.Property("CurrentStatus")
                .EqualTo(MemoryStatus.Archived)
        );
    }

    // ---------------------------------------------------------------
    // StampUpdated guards
    // ---------------------------------------------------------------

    [Test]
    public void Mutation_RejectsNonUtcUpdatedAt()
    {
        var entry = NewActive();
        var local = new DateTimeOffset(2026, 5, 6, 13, 0, 0, TimeSpan.FromHours(8));

        Assert.That(
            () => entry.UpdateContent("x", local),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("updatedAtUtc")
        );
    }

    [Test]
    public void Mutation_RejectsUpdatedAtBeforeCreated()
    {
        var entry = NewActive();
        var earlier = SampleCreatedAt.AddSeconds(-1);

        Assert.That(
            () => entry.UpdateContent("x", earlier),
            Throws.TypeOf<ArgumentException>().With.Property("ParamName").EqualTo("updatedAtUtc")
        );
    }

    private static MemoryLedgerEntry NewActive() =>
        MemoryLedgerEntry.Create(
            content: "seed",
            scope: null,
            source: MemorySource.UserExplicit,
            isExplicit: true,
            confidence: 1.0,
            sensitivity: MemorySensitivity.Normal,
            createdAtUtc: SampleCreatedAt
        );
}
