using Dawning.AgentOS.Domain.Memory;

namespace Dawning.AgentOS.Application.Interfaces;

/// <summary>
/// Per ADR-038 §决策 H1 the Application-internal port that
/// <see cref="Services.ChatAppService"/> consults before each chat
/// turn to decide which <see cref="MemoryLedgerEntry"/> rows (if any)
/// to inject into the system prompt as long-term memory context.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-038 §决策 H1 this port lives under
/// <c>Application/Interfaces/</c> alongside the AppService facade
/// contracts but intentionally drops the <c>*AppService</c> suffix:
/// it is not exposed to the API layer, carries no DTO contract, has
/// no REST endpoint, and its only consumer in
/// <c>Dawning.AgentOS.Application</c> is
/// <see cref="Services.ChatAppService"/>. The un-suffixed name keeps
/// it out of the AppService reflection scanner in
/// <c>ApplicationServiceCollectionExtensions</c> (which only matches
/// the <c>*AppService</c> suffix) and forces an explicit manual DI
/// registration so the inner-collaborator nature is visible at the
/// composition root.
/// </para>
/// <para>
/// Per ADR-038 §决策 F1 implementations MUST silently degrade on any
/// downstream failure (storage outage, ORM error, cancellation aside)
/// — returning an empty list rather than throwing — so chat continues
/// uninterrupted when memory retrieval fails. The contract is
/// documented on <see cref="RetrieveAsync"/>.
/// </para>
/// </remarks>
public interface IChatMemoryRetriever
{
    /// <summary>
    /// Per ADR-038 §决策 A1 / C1 / E1 returns up to
    /// <c>ChatMemoryRetriever.MaxRetrievedEntries</c> active memory
    /// entries whose content matches keywords extracted from
    /// <paramref name="userMessage"/>, ordered by the §A1 ranking.
    /// Returns an empty list when no memory is cited (empty input,
    /// no keywords extractable, or no rows match).
    /// </summary>
    /// <param name="userMessage">
    /// The full user message about to be sent to the LLM. Must not be
    /// <c>null</c>; whitespace-only / empty input is allowed and yields
    /// an empty result without touching the database.
    /// </param>
    /// <param name="cancellationToken">Standard cancellation token.</param>
    /// <returns>
    /// A possibly empty, never-<c>null</c> list of memory entries to
    /// inject into the system prompt. Per ADR-038 §决策 F1 this method
    /// MUST NOT throw on retrieval failure — implementations log a
    /// warning and return an empty list so chat keeps working.
    /// <see cref="OperationCanceledException"/> from
    /// <paramref name="cancellationToken"/> is the only expected
    /// exception and is propagated unchanged.
    /// </returns>
    Task<IReadOnlyList<MemoryLedgerEntry>> RetrieveAsync(
        string userMessage,
        CancellationToken cancellationToken
    );
}
