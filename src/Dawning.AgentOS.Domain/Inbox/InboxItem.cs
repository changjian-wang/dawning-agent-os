using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Domain.Inbox;

/// <summary>
/// Aggregate root for a single piece of material captured into the agent
/// inbox. Per ADR-026 §1 the V0 schema is intentionally minimal:
/// content + optional source + captured-at + created-at, no tags, no
/// summary, no category. Those derived fields belong to a future read-side
/// processing aggregate, not to inbox itself.
/// </summary>
/// <remarks>
/// <para>
/// Identity is a UUIDv7 (<see cref="Guid.CreateVersion7(DateTimeOffset)"/>),
/// which gives the same time-sortable property that ADR-026 originally
/// asked of ULID while staying BCL-native and struct-compatible with the
/// existing <see cref="Entity{TId}"/> base. The version-7 byte is verified
/// inside <see cref="Capture(string, string?, DateTimeOffset)"/>.
/// </para>
/// <para>
/// <see cref="Capture(string, string?, DateTimeOffset)"/> is the only
/// business factory; it raises <see cref="InboxItemCaptured"/> exactly
/// once. <see cref="Rehydrate(Guid, string, string?, DateTimeOffset, DateTimeOffset)"/>
/// is the persistence factory and never raises events — loading from a
/// repository is not a business action.
/// </para>
/// </remarks>
public sealed class InboxItem : AggregateRoot<Guid>
{
    /// <summary>Per ADR-026 §1 max content length (UTF-16 code units).</summary>
    public const int MaxContentLength = 4_096;

    /// <summary>Per ADR-026 §1 max source length (UTF-16 code units).</summary>
    public const int MaxSourceLength = 64;

    /// <summary>The user-supplied material captured into the inbox.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// Optional capture-route marker (e.g. <c>"chat"</c>, <c>"clipboard"</c>).
    /// <c>null</c> when the caller did not specify a source.
    /// </summary>
    public string? Source { get; private set; }

    /// <summary>
    /// UTC instant the material was captured. V0 keeps this equal to
    /// <see cref="Entity{TId}.CreatedAt"/>; future scenarios such as
    /// historical bulk import will let the two diverge.
    /// </summary>
    public DateTimeOffset CapturedAtUtc { get; private set; }

    private InboxItem(Guid id, string content, string? source, DateTimeOffset capturedAtUtc)
        : base(id, capturedAtUtc)
    {
        Content = content;
        Source = source;
        CapturedAtUtc = capturedAtUtc;
    }

    private InboxItem() { }

    /// <summary>
    /// Captures a new piece of material into the inbox. Validates input
    /// invariants (programming-error level: empty content, oversize content,
    /// empty / oversize source, non-UTC <paramref name="capturedAtUtc"/>),
    /// generates a UUIDv7 anchored to <paramref name="capturedAtUtc"/>,
    /// and raises <see cref="InboxItemCaptured"/>.
    /// </summary>
    /// <param name="content">Required, non-whitespace, length ≤ <see cref="MaxContentLength"/>.</param>
    /// <param name="source">Optional; if provided must be non-whitespace and length ≤ <see cref="MaxSourceLength"/>.</param>
    /// <param name="capturedAtUtc">UTC instant of capture (offset must be zero).</param>
    /// <returns>The newly created aggregate.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when input violates the invariants listed above. Per ADR-022
    /// invariant violations are programming errors, not business failures.
    /// </exception>
    public static InboxItem Capture(string content, string? source, DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Inbox content must be non-empty.", nameof(content));
        }

        if (content.Length > MaxContentLength)
        {
            throw new ArgumentException(
                $"Inbox content length {content.Length} exceeds the {MaxContentLength}-character limit.",
                nameof(content)
            );
        }

        if (source is not null)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException(
                    "Inbox source, when provided, must be non-whitespace.",
                    nameof(source)
                );
            }

            if (source.Length > MaxSourceLength)
            {
                throw new ArgumentException(
                    $"Inbox source length {source.Length} exceeds the {MaxSourceLength}-character limit.",
                    nameof(source)
                );
            }
        }

        if (capturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "capturedAtUtc must be a UTC instant (offset = TimeSpan.Zero).",
                nameof(capturedAtUtc)
            );
        }

        var id = Guid.CreateVersion7(capturedAtUtc);
        var item = new InboxItem(id, content, source, capturedAtUtc);
        item.Raise(new InboxItemCaptured(id, capturedAtUtc, capturedAtUtc));
        return item;
    }

    /// <summary>
    /// Rehydrates an aggregate from persisted row data. Per ADR-022 this
    /// path must not raise domain events; loading is not a business action.
    /// </summary>
    /// <param name="id">Persisted UUIDv7 identifier.</param>
    /// <param name="content">Persisted content; assumed already valid.</param>
    /// <param name="source">Persisted source (or null).</param>
    /// <param name="capturedAtUtc">Persisted capture instant.</param>
    /// <param name="createdAt">Persisted creation instant (V0 equals capture).</param>
    /// <returns>An aggregate populated from the row, with no domain events queued.</returns>
    public static InboxItem Rehydrate(
        Guid id,
        string content,
        string? source,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset createdAt
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Rehydrated id must not be Guid.Empty.", nameof(id));
        }

        var item = new InboxItem
        {
            Content = content,
            Source = source,
            CapturedAtUtc = capturedAtUtc,
        };
        item.Id = id;
        item.CreatedAt = createdAt;
        return item;
    }
}
