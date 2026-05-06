using Dawning.AgentOS.Domain.Core;

namespace Dawning.AgentOS.Domain.Chat;

/// <summary>
/// Aggregate root for a single chat conversation. Per ADR-032 §决策 D1
/// the V0 schema is intentionally minimal: identity + title +
/// <c>created_at</c> + <c>updated_at</c>. Messages live in their own
/// table and are loaded on demand via
/// <see cref="IChatSessionRepository.LoadMessagesAsync"/>; the aggregate
/// does not eagerly hold them.
/// </summary>
/// <remarks>
/// <para>
/// Identity is a UUIDv7 (<see cref="Guid.CreateVersion7(DateTimeOffset)"/>),
/// matching <see cref="Inbox.InboxItem"/>'s strategy and giving the
/// <c>id</c> column natural time-ordered locality.
/// </para>
/// <para>
/// V0 has two business actions:
/// <list type="bullet">
///   <item>
///     <description>
///       <see cref="SetTitleFromFirstMessage(string, DateTimeOffset)"/> —
///       invoked by the AppService once after the first user turn is
///       persisted. Subsequent calls are no-ops; the title sticks.
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="Touch(DateTimeOffset)"/> — bumps
///       <see cref="UpdatedAt"/> when a new message is appended; lets
///       the session list (sorted by <c>updated_at DESC</c>) keep the
///       most recently used session at the top.
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>
/// Per ADR-022 invariant violations throw <see cref="ArgumentException"/>;
/// loading from a repository must not raise events, so
/// <see cref="Rehydrate(Guid, string, DateTimeOffset, DateTimeOffset)"/>
/// uses <see cref="Entity{TId}"/>'s parameterless rehydration path.
/// </para>
/// </remarks>
public sealed class ChatSession : AggregateRoot<Guid>
{
    /// <summary>Per ADR-032 §决策 A2 default title used when a session is created before its first user turn lands.</summary>
    public const string PlaceholderTitle = "新会话";

    /// <summary>Per ADR-032 §决策 A2 maximum title length (UTF-16 code units); first-user-turn truncates to this.</summary>
    public const int MaxTitleLength = 24;

    /// <summary>Display title; defaults to <see cref="PlaceholderTitle"/> until <see cref="SetTitleFromFirstMessage"/> runs.</summary>
    public string Title { get; private set; } = PlaceholderTitle;

    /// <summary>UTC instant of the most recent activity in this session; used to order the session list.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    private ChatSession(Guid id, string title, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        : base(id, createdAt)
    {
        Title = title;
        UpdatedAt = updatedAt;
    }

    private ChatSession() { }

    /// <summary>
    /// Creates a new empty chat session anchored at <paramref name="createdAtUtc"/>.
    /// The id is a UUIDv7 stamped at the same instant so the store's
    /// natural ordering matches creation order.
    /// </summary>
    /// <param name="createdAtUtc">UTC instant of creation (offset must be zero).</param>
    /// <returns>A new aggregate with placeholder title and matching <see cref="Entity{TId}.CreatedAt"/> / <see cref="UpdatedAt"/>.</returns>
    /// <exception cref="ArgumentException">When <paramref name="createdAtUtc"/> is not UTC.</exception>
    public static ChatSession Create(DateTimeOffset createdAtUtc)
    {
        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "createdAtUtc must be a UTC instant (offset = TimeSpan.Zero).",
                nameof(createdAtUtc)
            );
        }

        var id = Guid.CreateVersion7(createdAtUtc);
        return new ChatSession(id, PlaceholderTitle, createdAtUtc, createdAtUtc);
    }

    /// <summary>
    /// Replaces <see cref="Title"/> with the first
    /// <see cref="MaxTitleLength"/> characters of the supplied user
    /// content. Subsequent calls leave the existing title untouched —
    /// the title sticks once a real one is set.
    /// </summary>
    /// <param name="firstUserContent">The first user-authored message body. Trimmed of leading / trailing whitespace before truncation.</param>
    /// <param name="nowUtc">UTC instant; <see cref="UpdatedAt"/> is bumped to this value when the title is set.</param>
    /// <exception cref="ArgumentException">When <paramref name="firstUserContent"/> is null / whitespace, or <paramref name="nowUtc"/> is not UTC.</exception>
    public void SetTitleFromFirstMessage(string firstUserContent, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(firstUserContent))
        {
            throw new ArgumentException(
                "First user content must be non-empty when setting the chat title.",
                nameof(firstUserContent)
            );
        }

        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "nowUtc must be a UTC instant (offset = TimeSpan.Zero).",
                nameof(nowUtc)
            );
        }

        // Idempotent guard: only the first call wins.
        if (!string.Equals(Title, PlaceholderTitle, StringComparison.Ordinal))
        {
            return;
        }

        var trimmed = firstUserContent.Trim();
        Title = trimmed.Length <= MaxTitleLength ? trimmed : trimmed[..MaxTitleLength];

        UpdatedAt = nowUtc;
    }

    /// <summary>
    /// Bumps <see cref="UpdatedAt"/> to <paramref name="nowUtc"/>. Called
    /// by the AppService after appending each message so the session
    /// list ordering reflects activity.
    /// </summary>
    /// <param name="nowUtc">UTC instant; offset must be zero.</param>
    /// <exception cref="ArgumentException">When <paramref name="nowUtc"/> is not UTC.</exception>
    public void Touch(DateTimeOffset nowUtc)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "nowUtc must be a UTC instant (offset = TimeSpan.Zero).",
                nameof(nowUtc)
            );
        }

        UpdatedAt = nowUtc;
    }

    /// <summary>
    /// Rehydrates an aggregate from persisted row data without raising
    /// any domain events.
    /// </summary>
    public static ChatSession Rehydrate(
        Guid id,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt
    )
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Rehydrated id must not be Guid.Empty.", nameof(id));
        }

        var session = new ChatSession { Title = title, UpdatedAt = updatedAt };
        session.Id = id;
        session.CreatedAt = createdAt;
        return session;
    }
}
