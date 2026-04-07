# Phase 1: 90-Day Roadmap for Building an Enterprise-Grade Distributed Agent Framework

> Goal: Build an enterprise-grade, distributed, multi-agent .NET framework with continual skill self-evolution in 90 days.
>
> Scope: Production-first architecture, including distributed execution, governance, security, and measurable self-improvement loops.

## 0. Success Criteria (Definition of Done)

At day 90, every item below must be verified. Items are grouped into 10 dimensions.

### SC-1 Distributed Architecture

- SC-1.1: Control Plane is deployed and manages agent registry, policy store, skill lifecycle, and evaluation triggers.
  > The Control Plane is the centralized brain of the platform. It holds the catalog of all registered agents, the policy rules that govern their behavior, the full skill artifact repository with version history, and the triggers that launch evaluation pipelines. Without a functioning Control Plane, no agent can be discovered, no skill can be published, and no policy can be enforced.

  1. **Agent Registry** — A centralized catalog where every agent is registered with its name, version, capabilities, tool-scope, and health status. The registry supports CRUD operations, version tagging, and namespace-based discovery. Other planes query it to resolve which agent handles which task type.
  2. **Policy Store** — A versioned key-value store that holds all governance rules: tool execution policies (allowlist/denylist/risk-gate), delegation depth limits, memory scope ACLs, and skill promotion gates. Policies are immutable per version; updates create new versions. The active policy set is resolved per run by environment (dev/staging/prod).
  3. **Skill Lifecycle Manager** — Manages the full lifecycle of skill artifacts: create, validate (lint + policy), publish, canary, promote, deprecate, archive. It owns the skill version graph, triggers quality gates on every revision, and coordinates with the Skill Router for activation status.
  4. **Evaluation Trigger Scheduler** — Schedules and dispatches evaluation runs: nightly golden-set benchmarks, on-demand PR evaluations, and post-skill-update regression checks. It produces evaluation requests and collects results for the SLO dashboard.

- SC-1.2: Runtime Plane runs agent workers as independent processes. Workers can be started, stopped, and replaced without data loss.
  > Agent workers are the units of execution. Each worker runs in its own process (or container), picks up tasks from a queue, and reports results back. The key requirement is zero-downtime replaceability: you can roll out a new worker version, drain the old one, and the in-flight workflow resumes on the new worker from its last durable checkpoint.

  1. **Task Queue Consumer** — Each worker connects to a durable task queue (e.g., RabbitMQ, Azure Service Bus) and competes for AgentTaskEnvelope messages. Consumption is pull-based with configurable prefetch and visibility timeout to prevent starvation and double-processing.
  2. **Durable Checkpoint Store** — After each execution step, the worker writes a checkpoint (step number, intermediate state, tool results) to a durable store. On crash or replacement, a new worker loads the last checkpoint and resumes without re-executing completed steps.
  3. **Lease and Heartbeat Protocol** — Every active worker holds a time-limited lease and sends periodic heartbeats to the Control Plane. If a worker misses heartbeats beyond the grace period, the Control Plane marks it as failed and reassigns its in-flight tasks to another worker.
  4. **Graceful Drain and Rolling Deploy** — When a worker receives a shutdown signal, it stops accepting new tasks, finishes or checkpoints in-progress tasks, and exits cleanly. The deployment orchestrator (e.g., Kubernetes) replaces it with a new version while maintaining the configured minimum worker count.

- SC-1.3: Memory Plane provides short-term state (per-session) and long-term knowledge (cross-session) as separate, independently scalable services.
  > Short-term state handles the current conversation's message history, tool call results, and scratch context. Long-term knowledge stores facts, skills, and learned patterns that persist across sessions. These two concerns have fundamentally different access patterns (hot vs. warm), so they must be independently scalable and deployable.

  1. **Short-Term State Service** — A low-latency, session-scoped store (e.g., Redis, in-memory with persistence) that holds the current conversation's message list, tool call results, and scratch variables. It supports TTL-based auto-expiry when sessions end, and provides atomic read-modify-write operations for concurrent step execution.
  2. **Long-Term Knowledge Service** — A vector-enabled persistent store (e.g., PostgreSQL + pgvector, Qdrant) that holds cross-session facts, skill artifacts, and distilled knowledge. It exposes semantic retrieval (embedding similarity), metadata filtering, and importance-aware ranking through a unified query API.
  3. **Scope Isolation Layer** — Enforces the four-tier namespace model (global / team / session / private) at the API boundary. Every memory read/write request carries a scope token validated against the agent's RBAC profile. Cross-scope access is denied by default and logged as a security event.
  4. **Independent Scaling Interface** — Short-term and long-term services expose separate health endpoints, connection pools, and scaling knobs. Scaling one does not require scaling the other. Deployment manifests (e.g., Kubernetes HPA) target each service independently based on its own metrics (latency, throughput, storage).

- SC-1.4: All three planes communicate via well-defined async contracts (AgentTaskEnvelope, HandoffEnvelope, SkillArtifact, PolicyDecision). No direct in-process coupling.
  > Loose coupling between planes is enforced at the protocol level. Each message type has a versioned schema, a unique correlation ID, and a TTL. This means planes can be developed, deployed, and scaled independently. Direct method calls between planes are forbidden — all communication goes through message queues or event buses.

  1. **Versioned Schema Registry** — Every contract type (AgentTaskEnvelope, HandoffEnvelope, SkillArtifact, PolicyDecision) is registered with a semantic version. Schema evolution follows additive-only rules: new fields have defaults, removed fields go through a deprecation cycle. Consumers validate inbound messages against the expected schema version and reject incompatible messages with a clear error.
  2. **Correlation and Causation IDs** — Every message carries a unique `correlationId` (ties all messages in one workflow together) and a `causationId` (points to the message that triggered this one). These IDs propagate through all planes and enable end-to-end trace reconstruction from any single message.
  3. **TTL and Dead-Letter Policy** — Every message has a configurable TTL. Messages that exceed TTL or fail processing after max retries are routed to a dead-letter queue. The dead-letter queue is monitored, and alerts fire when its depth exceeds a threshold. Dead-lettered messages are preserved for manual investigation.
  4. **Transport Abstraction Layer** — The message bus (RabbitMQ, Azure Service Bus, in-memory for testing) is accessed through an `IMessageBus` interface. Switching transports requires only a DI registration change, no code modifications in any plane. Integration tests run on the in-memory transport for speed; staging and prod use the durable transport.

- SC-1.5: Idempotency protocol ensures duplicate message delivery does not produce duplicate side effects. Verified by chaos test.
  > In any distributed system, messages can be delivered more than once (network retries, queue redelivery). Every handler must use an idempotency key to detect and skip duplicates. This is not optional — it is verified by injecting duplicate messages during chaos testing and confirming that tool side effects, state mutations, and audit records are not duplicated.

  1. **Idempotency Key Generation** — Every message is assigned a globally unique idempotency key at the point of creation (producer side). The key is a deterministic hash of (message type + correlation ID + step number + payload hash). Consumers use this key, not their own generated IDs, to detect duplicates.
  2. **Deduplication Store** — A lightweight, TTL-scoped key-value store (e.g., Redis SET with expiry) holds processed idempotency keys. Before executing any side effect, the handler checks the store. If the key exists, the handler returns the cached response without re-executing. Keys expire after a configurable window (e.g., 24 hours) to bound storage growth.
  3. **Side-Effect Fencing** — For handlers that interact with external systems (e.g., sending email, writing to a database), the idempotency check must happen before the side effect, not after. If the handler crashes between the side effect and the key write, a recovery protocol replays the message and the external system's own idempotency mechanism (or the fencing token) prevents duplication.
  4. **Chaos Verification Suite** — A dedicated test suite that injects: (a) exact-duplicate messages, (b) near-simultaneous duplicate messages (race condition), and (c) duplicate messages after handler crash and restart. The suite asserts that tool side effects, state mutations, audit log entries, and cost accounting are not duplicated under any scenario.

### SC-2 Multi-Agent Collaboration

- SC-2.1: Orchestrator supports sequential, parallel, conditional-branch, and retry/compensation workflow primitives.
  > These four primitives cover the vast majority of real-world coordination patterns. Sequential handles pipelines. Parallel handles fan-out/fan-in. Conditional-branch handles decision trees. Retry/compensation handles failure recovery with rollback. The orchestrator must compose these primitives declaratively — no hand-written control flow per workflow.

- SC-2.2: Handoff contract transfers ownership, context snapshot, budget remaining, and tool-scope boundaries between agents. Transfer is atomic.
  > When Agent A hands off to Agent B, the system must guarantee that exactly one agent owns the task at any given moment. The handoff envelope contains a frozen context snapshot (so B knows what A did), the remaining step and token budget (so B cannot overspend), and the tool-scope boundary (so B only accesses tools it is authorized for). Atomicity means there is no window where the task is owned by both or neither agent.

- SC-2.3: Delegation depth is configurable per workflow. Exceeding depth triggers a policy violation and halts the workflow.
  > Without a depth limit, a chain of agents can delegate indefinitely, causing runaway cost and latency. The depth limit is a safety mechanism: it is set per workflow definition (e.g., max 3 handoffs), and when exceeded, the orchestrator emits a PolicyViolation event, stops the workflow, and records the full chain for debugging.

- SC-2.4: Shared memory namespace model is enforced: global, team, session, and private scopes. Agents cannot read scopes they are not authorized for.
  > Memory isolation is critical for multi-agent security. Global scope holds organization-wide knowledge. Team scope holds project-level context. Session scope holds current conversation state. Private scope holds agent-internal scratch data. Access control is enforced at the Memory Plane API level — an agent requesting a scope it lacks authorization for receives an access-denied error, not empty results.

- SC-2.5: A 5-agent deterministic scenario completes with a fully auditable handoff trail (who, what, when, why) in traces and logs.
  > This is the integration acceptance test. Five agents collaborate on a predefined task (e.g., audit -> fix -> test -> review -> report). The test verifies: all handoffs are recorded with agent identity, transferred context, timestamp, and reason; the final output is correct; and the entire trail can be reconstructed from the audit log alone.

### SC-3 Stateful Prompt Protocol

- SC-3.1: Every agent run produces a versioned StatefulPrompt record containing system instructions, selected skills, memory context, and tool definitions.
  > The StatefulPrompt is the complete input specification for an agent run. It captures everything the LLM will see: the system instructions that define behavior, the skill artifacts selected by the router, the memory context retrieved from short-term and long-term stores, and the tool definitions available for this run. Versioning means every prompt is assigned a monotonically increasing revision number and stored for reproducibility.

- SC-3.2: StatefulPrompt is immutable once execution begins. Mutations only occur between runs via the evolution pipeline.
  > Immutability during execution guarantees deterministic replay: given the same StatefulPrompt and the same LLM responses, the agent produces the same output. This is essential for debugging, evaluation, and compliance. Any improvement to the prompt (new skill, updated instruction) takes effect only in the next run, via the skill evolution write phase.

- SC-3.3: StatefulPrompt schema is backward-compatible. Old prompts can be replayed on new runtime versions.
  > As the framework evolves, the StatefulPrompt schema may gain new fields. Backward compatibility means: (1) old prompts missing new fields use safe defaults, (2) new runtime versions can deserialize and execute old prompts without error. This is tested by maintaining a corpus of historical prompts and replaying them on every new build.

- SC-3.4: StatefulPrompt diff is available for any two consecutive runs of the same agent, enabling change attribution.
  > When an agent's behavior changes between runs, the diff shows exactly what changed: was a new skill added? Was a memory fact injected? Was an instruction modified? This is the primary tool for diagnosing regressions and understanding the impact of skill evolution. The diff is structured (field-level), not a raw text diff.

### SC-4 Skill Router (Read Phase)

- SC-4.1: Router accepts current prompt state, task description, and optional user hints. Returns top-k scored skills.
  > The router is the read phase of the Memento-Skills learning loop. Its input is the full context of the current request: the stateful prompt (including agent instructions and memory), the user's task description, and any explicit hints (e.g., "use the SQL skill"). Its output is a ranked list of the top-k most relevant skills, each with a numeric score. This replaces the naive approach of injecting all skills into the prompt, which wastes tokens and confuses the LLM.

- SC-4.2: Scoring features include: semantic similarity, historical success rate, recency of last success, failure pattern match, and declared when-to-use metadata.
  > The router uses a multi-signal scoring model. Semantic similarity matches the task description against skill intent and when-to-use fields via embeddings. Historical success rate reflects how often this skill led to successful outcomes. Recency biases toward recently successful skills. Failure pattern match penalizes skills whose known failure patterns match the current context. Declared when-to-use metadata provides the skill author's explicit guidance. These signals are combined with configurable weights.

- SC-4.3: Confidence threshold is configurable. Below threshold, router falls back to full skill injection or asks the user for clarification.
  > Not every routing decision is confident. When the top-scored skill falls below the confidence threshold, the system has two fallback strategies: (1) inject all available skills and let the LLM choose (safe but token-expensive), or (2) ask the user to clarify their intent (better for interactive scenarios). The threshold and fallback strategy are configurable per agent.

- SC-4.4: Router supports online feedback: after each run, the selected skill receives a utility signal (success/failure/partial, latency, cost).
  > This is how the router learns without updating LLM weights. After each run, the outcome (success, failure, or partial success) along with operational metrics (latency, token cost) is fed back to the router as a utility signal for the skill that was selected. Over time, this signal improves future routing decisions by updating the historical success rate and failure pattern features.

- SC-4.5: Router top-5 hit rate >= 85% on internal routing benchmark (>= 30 diverse task scenarios).
  > This is the quantitative acceptance gate. The routing benchmark consists of at least 30 tasks spanning different domains (code, data, search, communication, etc.). For each task, the ground-truth skill is known. The router must include the correct skill in its top-5 results at least 85% of the time. This benchmark is run on every build to detect routing regressions.

### SC-5 Reflective Skill Evolution (Write Phase)

- SC-5.1: After execution, a reflection pipeline summarizes the trajectory: steps taken, tools used, successes, failures, and root causes.
  > The reflection pipeline is triggered after every agent run (or on failure). It uses an LLM to analyze the execution trace and produce a structured summary: which steps were taken, which tools were called (with arguments and results), which steps succeeded, which failed, and a root-cause hypothesis for failures. This summary is the raw material for skill improvement.

- SC-5.2: If reflection identifies an improvable skill, it generates a candidate skill patch (structured markdown diff).
  > Not every reflection leads to a skill update. The pipeline evaluates whether the failure or suboptimal outcome is attributable to the skill itself (as opposed to an LLM error or external service failure). If so, it generates a candidate patch: a structured diff against the current skill markdown that may update the instructions, examples, failure-patterns, or limitations fields. The patch is generated — not yet applied.

- SC-5.3: Candidate patches pass through quality gates before promotion: lint check, policy compliance, regression evaluation on golden set, and optional human approval.
  > This is the governance layer that prevents bad skill updates from reaching production. Gate 1 (lint): the updated skill markdown must parse correctly and contain all mandatory fields. Gate 2 (policy): the skill content must not violate security policies (e.g., no unrestricted shell access). Gate 3 (regression): the updated skill must not degrade performance on the golden evaluation set. Gate 4 (human approval): optionally, a human reviewer must approve the change. Any gate failure rejects the patch.

- SC-5.4: Approved patches are published as new skill revisions with full revision metadata (author: system/human, trigger: failure/optimization, parent revision, timestamp).
  > Once all gates pass, the patch becomes a new immutable skill revision. The revision metadata records: whether the author was the system (automated reflection) or a human; what triggered the update (a specific failure type, or a proactive optimization); the parent revision it was derived from; and the exact timestamp. This metadata is essential for auditing and rollback.

- SC-5.5: Rejected patches are logged with rejection reason and linked to the originating run for post-mortem analysis.
  > Rejected patches are not discarded silently. Every rejection is recorded with: the candidate diff, which gate rejected it, the specific reason (e.g., "regression on task #17: success rate dropped from 90% to 75%"), and a link to the originating run. This allows engineers to review rejection patterns, identify systemic issues, and manually apply improvements that the automated pipeline could not.

### SC-6 Skill Lifecycle Management

- SC-6.1: Every skill is a structured markdown artifact with mandatory fields: intent, when-to-use, limitations, failure-patterns, examples, and revision-metadata.
  > The skill artifact is the unit of knowledge in the framework. By standardizing on structured markdown, skills are human-readable, LLM-parseable, and version-controllable. Mandatory fields ensure every skill has a clear purpose (intent), usage guidance (when-to-use), known boundaries (limitations), historical failure modes (failure-patterns), concrete usage examples (examples), and full change history (revision-metadata). A skill missing any mandatory field fails validation and cannot be registered.

- SC-6.2: Skills are stored in a versioned registry. Any historical version can be retrieved and compared.
  > The skill registry is append-only: every update creates a new revision, never overwrites. This means you can always answer "what did this skill look like on March 15?" and "what changed between revision 7 and revision 8?". Historical retrieval supports debugging ("this task worked last week but fails now — what changed?") and compliance ("show me the skill that was active when this incident occurred").

- SC-6.3: Canary release is supported: a new skill revision serves a configurable percentage of traffic before full promotion.
  > Canary release is the controlled rollout mechanism for skill updates. When a new revision is published, it initially handles only a small percentage of matching requests (e.g., 5%). If canary metrics (success rate, latency, cost) remain healthy over a configurable observation window, the percentage increases until full promotion. This prevents a bad skill update from affecting all users at once.

- SC-6.4: Automatic rollback triggers when canary metrics (success rate, latency, cost) degrade beyond configurable thresholds.
  > Rollback is the safety net for canary release. If the canary revision's success rate drops below the threshold (e.g., 10% worse than the previous revision), or if latency or cost spike beyond limits, the system automatically reverts to the previous stable revision. Rollback is immediate (≤ 1 minute), logged with the degradation metrics that triggered it, and generates an alert to the platform team.

- SC-6.5: Skill deletion or deprecation follows a governance workflow: mark deprecated -> grace period -> archive. No hard deletes without audit record.
  > Skills accumulate usage history and may be referenced by running workflows. Hard deletion would break reproducibility and auditability. Instead, deprecation follows a three-stage process: (1) mark as deprecated — the skill still works but emits a warning; (2) grace period — a configurable duration (e.g., 30 days) during which consumers migrate; (3) archive — the skill is moved to cold storage, no longer routable, but still retrievable for audit and replay.

### SC-7 LLM Provider Layer

- SC-7.1: At least 3 providers are implemented: one local (e.g., Ollama), one public cloud (e.g., OpenAI), one enterprise cloud (e.g., Azure OpenAI).
  > Provider diversity is essential for cost optimization, latency optimization, data residency compliance, and fault tolerance. A local provider (Ollama) enables offline development and sensitive-data scenarios. A public cloud provider (OpenAI) offers the latest models. An enterprise cloud provider (Azure OpenAI) satisfies corporate compliance and SLA requirements. All three must pass the same provider contract test suite.

- SC-7.2: Unified streaming event model covers: TextDelta, ToolCallRequested, ToolCallCompleted, RunCompleted, and Error.
  > Different LLM APIs have different streaming formats. The unified event model normalizes them into five event types that the agent loop consumes. TextDelta delivers incremental text. ToolCallRequested signals the LLM wants to call a tool (with name and arguments). ToolCallCompleted carries the tool result back. RunCompleted signals the final answer. Error carries provider-level errors. This normalization allows the agent loop to be completely provider-agnostic.

- SC-7.3: Provider failover is automatic: if primary provider returns error or exceeds latency SLO, request is retried on secondary within the same run.
  > Failover happens transparently within the agent execution loop. If the primary provider returns an HTTP error (429, 500, 503) or the response latency exceeds the configured SLO (e.g., 10 seconds), the request is automatically forwarded to the next provider in the failover chain. The retry uses the same prompt and tools. The failover event is recorded in the run trace with the original error and the fallback provider used.

- SC-7.4: Token usage, latency, and estimated cost are captured per provider call and aggregated per run.
  > Every LLM API call produces a TokenUsage record: prompt tokens, completion tokens, total tokens, model name, and dollar cost estimate. Latency is measured from request start to last byte received. These are stored per call and aggregated per run, enabling dashboards like "cost per agent per day" and "p95 latency by provider". Cost estimation uses configurable pricing tables per model.

- SC-7.5: Adding a new provider requires implementing one interface and registering one DI extension. No core code changes.
  > Provider extensibility is a first-class design goal. A new provider author implements `ILLMProvider` (with `ChatAsync`, `ChatStreamAsync`, `ChatStreamEventsAsync` methods), writes a `AddXxxProvider()` DI extension method, and the framework discovers it at startup. No changes to the agent loop, orchestrator, or any other core component are needed. This is verified by the provider contract test suite.

### SC-8 Memory System

- SC-8.1: Short-term memory supports buffer, sliding window, and summary compression strategies. Strategy is configurable per agent.
  > Short-term memory manages the current conversation's message history. Buffer mode keeps all messages (simple but token-expensive). Sliding window keeps only the last N messages (bounded but may lose early context). Summary compression uses an LLM to condense older messages into a summary (preserves key facts while controlling tokens). The strategy is selected per agent via configuration, because different agents have different context needs.

- SC-8.2: Long-term memory supports semantic vector retrieval with configurable embedding provider.
  > Long-term memory stores facts, skills, and patterns that persist across sessions. Retrieval is done via semantic similarity: the query is embedded, and the nearest vectors in the store are returned. The embedding provider (e.g., OpenAI text-embedding-3-small, Ollama nomic-embed-text) is configurable and swappable without changing the memory store implementation.

- SC-8.3: Recall scoring combines semantic similarity, temporal recency (exponential decay), and importance weight. Weights are configurable.
  > Pure semantic similarity is not enough for high-quality recall. A fact that is semantically relevant but was stored 6 months ago may be stale. A fact marked as "critical" by the user should rank higher. The composite score formula is: `score = w_semantic * similarity + w_recency * exp(-decay * age_days) + w_importance * importance_level`. All three weights and the decay constant are configurable per agent.

- SC-8.4: Cross-session recall is verified: a fact stored in session A is retrievable in session B by the same agent with >= 90% recall on test set.
  > This is the quantitative acceptance test for long-term memory. A test set of at least 50 fact-query pairs is maintained. In Phase 1, a fact is stored; in Phase 2 (a different session), the query is issued. The fact must appear in the top-5 results at least 90% of the time. This test runs on every build to catch recall regressions.

- SC-8.5: Memory compaction runs automatically when context window usage exceeds configurable threshold. Compaction preserves key facts verified by diff test.
  > As conversations grow, the context window fills up. When usage exceeds the threshold (e.g., 80% of model limit), compaction triggers automatically: older messages are summarized, redundant facts are deduplicated, and low-importance entries are pruned. A diff test verifies that key facts (flagged as important or frequently recalled) survive compaction. This prevents silent knowledge loss.

### SC-9 Security, Compliance, and Governance

- SC-9.1: RBAC is enforced for: tool execution, skill publishing, skill approval, agent deployment, and memory scope access.
  > Role-Based Access Control is not optional in an enterprise agent framework. Five domains are protected: (1) tool execution — only agents with the correct role can invoke a given tool; (2) skill publishing — only authorized accounts can submit new skill revisions; (3) skill approval — only designated reviewers can approve skill promotions; (4) agent deployment — only platform operators can deploy or scale agents; (5) memory scope access — agents can only access memory scopes assigned to their role.

- SC-9.2: Every run, handoff, skill revision, and policy decision is recorded in an immutable append-only audit log.
  > The audit log is the system of record for compliance and incident investigation. It captures: every agent run (input, output, steps, cost), every handoff (from-agent, to-agent, context transferred), every skill revision (diff, author, gates passed), and every policy decision (allowed/denied, rule matched). The log is append-only and tamper-evident (e.g., hash-chained entries). It supports time-range queries and full-text search.

- SC-9.3: Sensitive data (PII, secrets) is automatically redacted in logs and traces. Redaction rules are configurable.
  > Agent runs may process sensitive data: email addresses, API keys, personal names, financial data. Before any data reaches the log or trace store, a redaction pipeline applies configurable rules (regex patterns, named-entity detection). Redacted fields are replaced with tokens like `[PII:email]` that preserve structure without exposing content. Redaction rules can be updated without redeployment via configuration.

- SC-9.4: Tool execution policy supports allowlist, denylist, and risk-level-based confirmation gates. High-risk tools require explicit human approval.
  > Not all tools are equal in risk. A read-only search tool is low-risk. A tool that sends emails or modifies databases is high-risk. The tool execution policy framework supports three layers: (1) allowlist — only explicitly permitted tools can run; (2) denylist — specific tools are blocked regardless of other rules; (3) risk-level gate — tools tagged as High-risk require an explicit human approval step before execution.

- SC-9.5: Skill evolution write phase is gated by a policy firewall. Skills containing unsafe patterns (e.g., unrestricted network calls, file system writes outside sandbox) are blocked.
  > The skill evolution pipeline can generate new skill content autonomously. Without a policy firewall, it could inadvertently create skills that exfiltrate data, write to the host filesystem, or make unrestricted API calls. The firewall scans every candidate skill patch for unsafe patterns (configurable rule set) and blocks any skill that matches. Blocked skills are logged with the specific violation for review.

- SC-9.6: Compliance evidence can be exported for audit: who approved what skill, which policy was active, what data was accessed.
  > Enterprise customers need to demonstrate regulatory compliance (SOC 2, ISO 27001, GDPR). The compliance export produces a structured report covering: skill approval chain (submitter, reviewer, timestamp, gate results), active policy snapshot at time of approval, and data access records for a given time range. The export format is machine-readable (JSON) and human-readable (PDF) for auditor review.

### SC-10 Observability, SLOs, and Release Gates

- SC-10.1: OpenTelemetry traces cover every run with span hierarchy: run -> step -> provider call / tool call / memory call.
  > Every agent run produces a trace tree. The root span is the run itself (with run ID, agent name, input hash). Child spans represent each execution step. Each step has further children for: LLM provider calls (with model, token count, latency), tool calls (with tool name, arguments, result, duration), and memory calls (with operation type, scope, hit/miss). This hierarchy enables drill-down from "this run was slow" to "this specific tool call in step 3 took 8 seconds".

- SC-10.2: Metrics are emitted for: run success rate, step count, token usage, cost, latency (p50/p95/p99), router hit rate, and skill evolution gain.
  > Metrics are the foundation of SLO enforcement and capacity planning. Run success rate tracks the percentage of runs that produce a correct final answer. Step count tracks execution efficiency. Token usage and cost track resource consumption. Latency percentiles (p50/p95/p99) track user-facing performance. Router hit rate tracks skill selection quality. Skill evolution gain tracks the measurable improvement from skill updates over time. All metrics are labeled by agent, workflow, and environment.

- SC-10.3: SLO targets are defined and enforced:
  - Distributed workflow success rate >= 90%.
    > At least 90% of multi-agent workflows must complete successfully. A workflow is considered successful when its final output matches the expected outcome as judged by the evaluation framework. Failures include: agent errors, tool failures that exhaust retries, policy violations, and timeout.
  - Multi-agent handoff accuracy >= 95%.
    > At least 95% of handoffs must transfer the correct context and ownership without data loss or duplication. Accuracy is measured by comparing the handoff envelope contents with the expected context at each handoff point in the evaluation scenario.
  - Skill router top-5 hit rate >= 85%.
    > The correct skill must appear in the router's top-5 results at least 85% of the time across the routing benchmark. This ensures the agent consistently receives the right skill for the task.
  - Evolution gain >= 10% relative improvement over baseline per quarter.
    > Skill evolution must produce measurable improvement. Each quarter, the golden evaluation set is re-run with current skills and compared against the baseline (skills from 90 days prior). At least 10% relative improvement in overall task success rate is expected.
  - P95 end-to-end latency <= 15s for standard workflows.
    > 95% of standard workflows (defined as <= 5 steps, <= 3 tool calls) must complete within 15 seconds. This includes LLM inference time, tool execution, memory retrieval, and orchestration overhead.
  - Availability >= 99.9% in staging stress window.
    > During the staging stress test window (sustained load at 2x expected peak), the platform must maintain >= 99.9% availability. Availability is defined as: the percentage of incoming requests that receive a valid response (success or graceful error) within the latency SLO.
  - Skill update safety: 0 critical policy violations promoted to production.
    > No skill revision that violates a critical security policy may reach the production environment. This is an absolute gate: even one violation is a release blocker. Violations are defined by the policy firewall rules (SC-9.5).

- SC-10.4: Release promotion (dev -> staging -> prod) is blocked when any SLO regresses beyond policy threshold.
  > The promotion pipeline runs the full evaluation suite at each environment boundary. If any SLO metric in the target environment is worse than the threshold (e.g., success rate drops by more than 2% compared to the previous release), promotion is automatically blocked. The blocking reason is reported in the CI/CD dashboard with specific metric values and the threshold that was violated.

- SC-10.5: Nightly evaluation produces a report with per-skill, per-agent, and per-workflow breakdown. Report is versioned and diffable.
  > Every night, the evaluation framework runs the full golden set and all routing benchmarks. The resulting report contains: per-skill success rate and cost; per-agent execution profile (steps, latency, tokens); per-workflow end-to-end metrics. Reports are stored with a version number and can be diffed against any previous report to show trends, regressions, and improvements over time.

## 0.1 Research Basis: arXiv 2603.18743

This roadmap incorporates core ideas from Memento-Skills (arXiv:2603.18743):

- Agent-designing-agent direction: the system improves itself by evolving externalized skills.
- Stateful prompts as persistent and evolvable memory context.
- Read-Write Reflective Learning loop for continual adaptation without model weight updates.
- Behavior-trainable skill router for context-conditioned skill selection.

Reference: https://arxiv.org/abs/2603.18743

---

## 1. Product Positioning and Non-Goals

### 1.1 Positioning

Build a framework, not a single assistant app.

- Focus on reusable abstractions and stable extension points.
- Keep API minimal and DI-native.
- Prioritize reliability and testability over flashy features.

### 1.2 Non-Goals for v0.1

- No broad connector marketplace.
- No full autonomous code modification in production without approval workflows.
- No immediate cross-region active-active setup in v0.1 (single region first, multi-region in v0.2+).
- No uncontrolled self-writing skills without policy and quality gates.

---

## 2. Technical Principles

- Minimal API surface: fewer concepts, better defaults.
- Pure DI: all runtime services injected by constructor.
- Provider-agnostic core: no provider logic leaking into core loop.
- Configuration-driven behavior: appsettings + env override.
- Safety by default: max steps, max cost, and risk confirmation enabled.
- Observable by default: every run traceable and measurable.
- Distributed by design: asynchronous workflow, idempotency, and replayability as first-class requirements.
- Evolution with governance: skill self-update must pass quality, security, and policy checks.

---

## 3. 90-Day Plan Overview

## Month 1: Enterprise Architecture Baseline (Weeks 1-4)

### Week 1 - Control Plane and Runtime Plane Contracts

Deliverables:

- Define platform modules:
  - ControlPlane (registry, policy, evaluation, skill lifecycle)
  - RuntimePlane (agent workers, orchestration runtime)
  - MemoryPlane (short-term state + long-term knowledge)
  - ObservabilityPlane (logs, traces, metrics, audit)
- Define distributed contracts:
  - AgentTaskEnvelope
  - HandoffEnvelope
  - SkillArtifact
  - PolicyDecision
- Define idempotency and replay protocol.

Acceptance:

- Architecture decision records approved.
- End-to-end contract tests pass in local cluster.

### Week 2 - Distributed Orchestration Runtime

Deliverables:

- Implement workflow orchestration primitives:
  - sequential
  - parallel
  - conditional branch
  - retry/compensation
- Add durable state store for workflow steps.
- Add agent worker lease and heartbeat protocol.

Acceptance:

- Runtime can recover unfinished workflows after process restart.
- Duplicate delivery does not duplicate side effects.

### Week 3 - Multi-Agent Collaboration and Handoff Governance

Deliverables:

- Implement multi-agent handoff contract with explicit ownership transfer.
- Add delegation depth, budget, and tool-scope boundaries per agent.
- Add shared memory namespace model (global, team, session, private).

Acceptance:

- 5-agent workflow completes with deterministic handoff trail.
- Policy violations are blocked and fully audited.

### Week 4 - Stateful Prompt Protocol and Skill Artifact Schema

Deliverables:

- Define stateful prompt schema and lifecycle.
- Define structured markdown skill schema:
  - intent
  - when-to-use
  - limitations
  - failure patterns
  - examples
  - revision metadata
- Build skill package registry and version resolver.

Acceptance:

- Skill artifacts are versioned and reproducible.
- Runtime can resolve exact skill versions per run.

Milestone M1:

- v0.1.0-alpha-enterprise
- Distributed multi-agent baseline is operational.

---

## Month 2: Skill Self-Evolution System (Weeks 5-8)

### Week 5 - Read Phase: Behavior-Trainable Skill Router

Deliverables:

- Implement skill router with top-k scoring.
- Features include prompt state, task type, historical utility, and recent failures.
- Add online feedback capture for routing quality.

Acceptance:

- Router top-5 hit rate meets baseline target on internal benchmark.
- Router confidence threshold correctly triggers fallback behavior.

### Week 6 - Write Phase: Reflective Skill Update Pipeline

Deliverables:

- Implement reflection pipeline:
  - execution trace summarization
  - failure diagnosis
  - candidate skill patch generation
- Add skill quality gates:
  - lint
  - policy check
  - regression evaluation
  - human approval mode (configurable)

Acceptance:

- Skill updates can be auto-generated and safely promoted to staging.
- Unsafe or low-quality updates are automatically rejected.

### Week 7 - Closed-Loop Continual Learning

Deliverables:

- Integrate read-write loop into production workflow.
- Add skill canary release and automatic rollback.
- Add utility tracking per skill revision.

Acceptance:

- Measured uplift from new skill revisions versus baseline.
- Failed canary revisions rollback automatically within SLA.

### Week 8 - Memory-Aware Evolution and Knowledge Distillation

Deliverables:

- Add memory summarization for evolution context windows.
- Add temporal + importance + semantic scoring for recall.
- Distill successful trajectories into reusable skill examples.

Acceptance:

- Evolution quality remains stable under long-horizon tasks.
- Skill files improve benchmark score over two consecutive iterations.

Milestone M2:

- v0.2.0-preview-evolving
- Skill self-evolution loop is live with governance.

---

## Month 3: Enterprise Hardening and Release Gates (Weeks 9-12)

### Week 9 - Reliability Engineering and SLOs

Deliverables:

- Define and enforce platform SLOs:
  - availability
  - latency
  - success rate
  - recovery time
- Add chaos tests for queue delay, provider outage, and worker crash.
- Add runbook and automated incident diagnostics.

Acceptance:

- System meets target SLOs in load and fault tests.
- MTTR and failed-workflow replay metrics are reported.

### Week 10 - Security, Compliance, and Governance

Deliverables:

- Add RBAC for tool execution and skill publishing.
- Add immutable audit log for run, handoff, and skill revision.
- Add compliance controls for data residency and PII handling.

Acceptance:

- Unauthorized skill publish and high-risk tool calls are blocked.
- Audit evidence is complete for sampled compliance scenarios.

### Week 11 - Evaluation Framework and Promotion Gates

Deliverables:

- Build enterprise benchmark suites:
  - task success
  - coordination quality
  - routing quality
  - evolution gain
  - cost stability
- Add environment promotion gates: dev -> staging -> prod.

Acceptance:

- Promotion is blocked when regression exceeds policy threshold.
- Benchmark reports include revision-level explainability.

### Week 12 - Packaging, Documentation, and Operating Model

Deliverables:

- Release package set for enterprise deployment.
- Publish operations guide:
  - architecture
  - SRE runbook
  - security model
  - skill lifecycle governance
- Publish reference blueprints for distributed deployment.

Acceptance:

- New team can deploy a 3-agent distributed scenario in <= 2 hours.
- On-call and release checklists pass in rehearsal.

Milestone M3:

- v0.3.0-enterprise
- Production-grade distributed and self-evolving agent framework.

---

## 4. Work Breakdown by Track (Cross-cutting)

## 4.1 Platform Engineering Track

- Distributed orchestration runtime quality
- Multi-agent collaboration correctness
- Skill router and evolution pipeline quality

## 4.2 Reliability and SRE Track

- SLO/SLI dashboards and alerting
- Fault injection and replay validation
- Capacity planning and autoscaling policies

## 4.3 Research-to-Production Track

- Read-write reflective learning validation
- Skill revision quality attribution
- Controlled experimentation (A/B and canary)

## 4.4 Security and Governance Track

- Policy-as-code and RBAC enforcement
- Immutable audit and compliance reporting
- Skill lifecycle approval and rollback governance

---

## 5. KPI Targets

Use these metrics from Week 4 onward:

- Distributed Workflow Success Rate: >= 90% by Week 12.
- Multi-Agent Handoff Accuracy: >= 95% (correct ownership and context transfer).
- Skill Router top-5 Hit Rate: >= 85% on routing benchmark.
- Evolution Gain: >= 10% relative improvement over baseline on enterprise golden set.
- P95 End-to-End Latency: <= 15s for standard distributed workflows.
- Availability SLO: >= 99.9% in staging stress window.
- Skill Update Safety: 0 critical policy violations promoted to production.

---

## 6. Risk Register and Mitigation

1. Skill drift degrades reliability
- Mitigation: canary rollout, revision scoring, and auto rollback.

2. Distributed state inconsistency across workers
- Mitigation: idempotency keys, durable checkpoints, and replay-safe handlers.

3. Router overfitting to recent tasks
- Mitigation: mixed offline/online evaluation and diversity constraints.

4. Reflection loop amplifies unsafe behavior
- Mitigation: policy firewall before write phase and mandatory safety tests.

5. Cost explosion under multi-agent parallelism
- Mitigation: budget-aware orchestrator and adaptive concurrency limits.

---

## 7. Suggested Immediate Next Actions (This Week)

1. Finalize control plane and runtime plane architecture ADR.
2. Define skill artifact schema and revision metadata contract.
3. Implement distributed task envelope and idempotency protocol.
4. Build first router benchmark dataset and baseline scorer.
5. Launch v0.1.0-alpha-enterprise internal cluster demo.

---

## 8. Version Plan

- v0.1.0-alpha-enterprise (end of Month 1): distributed collaboration baseline
- v0.2.0-preview-evolving (end of Month 2): governed skill self-evolution
- v0.3.0-enterprise (end of Month 3): production-grade distributed framework

This roadmap is intentionally execution-focused: distributed reliability, measurable evolution, and enterprise governance.
