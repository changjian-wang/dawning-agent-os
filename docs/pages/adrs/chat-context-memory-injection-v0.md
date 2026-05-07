---
title: ADR-038 chat 上下文注入 memory MVP：朴素关键词检索 + system prompt 末尾注入 + 一行小字回报
type: adr
subtype: architecture
canonical: true
summary: dogfood 第 1 天暴露三条关联信号——Save 没回报闭环、chat 不读 memory、Memory 视图对普通用户不必要——共同指向「沉淀-复用」闭环未建；本 ADR 用最薄路径让 ChatAppService 在 send 前从 memory_entries 用朴素关键词匹配挑选 ≤ 5 条 active 状态的记忆，注入到内置 system prompt 末尾，不引入 embedding / vector / RAG 全栈，不暴露 IChatMemoryRetriever 为 API 端点（仅 ChatAppService 内部消费），不改主屏 UI（ADR-035 暂停期合规），失败降级不阻断 chat；新增 IChatMemoryRetriever 端口（Application 内部）+ 默认 Application 内实现，注入段落以一行可见小字「📒 引用了 N 条 memory」+ 可展开列表的 SSE 旁路 chunk 形式回流到 renderer，使 PURPOSE.md MVP 第一信号「Memory 真实复用」第一次发生；不 supersede ADR-035；不动 Save / Summary / Tags 三按钮存留性，留待后续 UI 收敛 ADR；不解决会话切换器形态（drawer vs tab）/ inbox 文案 / 空会话删除等 UI 信号；不引入兴趣画像 / 衰减权重 / archived 范围扩展。
tags: [agent, memory, llm-provider, memory-design]
sources: []
created: 2026-05-07
updated: 2026-05-07
verified_at: 2026-05-07
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/long-term-memory-as-core-capability.md, pages/adrs/memory-privacy-and-user-control.md, pages/adrs/explicit-memory-ledger-mvp.md, pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/chat-v0-streaming-and-persistence.md, pages/adrs/memory-ledger-v0-schema-and-storage.md, pages/adrs/inbox-to-memory-promotion-v0.md, pages/adrs/desktop-mvp-ux-thesis-via-dogfood.md, pages/adrs/llm-provider-v0-openai-deepseek-abstraction.md, pages/adrs/no-mediator-self-domain-event-dispatcher.md, pages/adrs/options-over-elaboration.md, pages/adrs/port-and-cross-layer-contract-assembly-split.md, pages/adrs/inbox-item-summarize-v0.md, pages/adrs/persistence-repository-style-dawning-orm-dapper.md, pages/adrs/interest-profile-weighting-and-decay.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-07
adr_revisit_when: "chat 引用 memory 命中率 < 10%（朴素关键词匹配证伪，迫使升级 embedding 或全文索引）；注入 token 占 prompt 总长 > 30%（C1 上限要降，或上字数预算）；用户开始抱怨「引用了过时 / 错误的 memory」（迫使 score / freshness / 用户标记不相关入口）；inbox 量大到全部进 memory 仍可被关键词检索（迫使 ADR-013 兴趣画像或 Save 重新评估）；多语言 chat 出现（中文 inbox + 英文 chat session 互不命中，迫使语义检索）；用户感知到「引用了 N 条」一行小字打扰（迫使 D 轴回退到 D1 完全不展示）；memory_entries.scope 字段开始有非 'global' 值（迫使按 scope 过滤注入）；archived / corrected 状态条目变多（迫使 E 轴重新评估）；外部触发的 RAG 完整 ADR 提案出现时（必须先回应本 ADR 的命中率与降级数据）；ADR-035 被 supersede 进入 UI 收敛阶段，可能改变 D2 一行小字的承载形态。"
---

# ADR-038 chat 上下文注入 memory MVP：朴素关键词检索 + system prompt 末尾注入 + 一行小字回报

> dogfood 第 1 天暴露：Save 没回报闭环（信号 4）+ chat 不读 memory（信号 7）+ Memory 视图对普通用户不必要（信号 8）共同指向「沉淀-复用」闭环未建。本 ADR 用最薄路径让 `ChatAppService.SendMessageStreamAsync` 在 send 前调 `IChatMemoryRetriever`，朴素关键词匹配挑 ≤ 5 条 active memory，拼接进内置 system prompt 末尾；以一行可见小字「📒 引用了 N 条 memory」+ 可展开列表回流；不引入 embedding；不动主屏 UI（ADR-035 暂停期合规）；失败降级不阻断 chat。

## 背景

### dogfood 信号汇总（2026-05-07，dogfood 第 1 天）

[ADR-035](desktop-mvp-ux-thesis-via-dogfood.md) §D2 的 dogfood 期在第 1 天即收集到足够信号触发其 §adr_revisit_when 「主人主动表达倾向」。原始信号见 [docs/raw/meetings/dogfood-mvp-pause-button-2026-05.md](../../raw/meetings/dogfood-mvp-pause-button-2026-05.md)，本 ADR 直接引用其中决定本 ADR 范围的三条：

- **信号 4「Save 没回报闭环」**：用户原话「我点击 Save 按钮后，当前我发送的这条消息被保存到 memory，不清楚有什么作用」。Save 把 inbox 项晋升到 `memory_entries`，但**没有任何下游消费这条 memory**——chat 不读、UI 仅作为 CRUD 展示。
- **信号 7「没接入 RAG / ReAct」**：用户原话「现在还没有接入 RAG 和 ReAct 等，所以只能问一句答一句」。这条暴露了真正的根因不是 UI 主叙事错位（ADR-035 §备选方案 4 候选都假设的那样），而是**能力天花板：chat 是无记忆问答机**。无论 UI 怎么改，"沉淀的回报"都不会出现。
- **信号 8「Memory 视图对普通用户不必要」**：用户原话「让用户看记忆不是很重要 …… 程序员需要了解原理可能需要查看记忆情况，查看记忆主要是想看有没有记忆断裂」。这条**否决了 ADR-035 §备选方案 C「Memory-first 重定位」**，并把 Memory 视图的产品角色从「用户管理界面」重新框定为「调试 / 信任审计 view」——这与 [ADR-007 记忆隐私与用户控制](memory-privacy-and-user-control.md) 兼容（用户可见性仍是边界，只是触发场景从"日常主舞台"挪到"出问题时审计"）。

### 三条信号合起来指向同一件事

不是 UI 形态错（A/B/D 候选），不是 Memory 视图地位低（C 候选被 §信号 8 否决）；是**「沉淀-复用」闭环没接通**。沉淀这一侧已经建好（[Inbox V0](inbox-v0-capture-and-list-contract.md) 捕获 + [Memory Ledger V0](memory-ledger-v0-schema-and-storage.md) 存储 + [Inbox→Memory 晋升](inbox-to-memory-promotion-v0.md)），复用这一侧空缺。[PURPOSE.md MVP 第一信号「Memory 真实复用」](../../PURPOSE.md) 由此从未发生。

### 为什么本 ADR 不 supersede ADR-035

[ADR-035 §决策 D4 第 1 条](desktop-mvp-ux-thesis-via-dogfood.md) 明文允许「后端能力扩展，只要不在主屏增加按钮 / 视图」。本 ADR 的范围严格落在这条：

- 不加主屏按钮（注入是自动行为，不需要用户触发）
- 不加主屏视图（不展示 memory 列表面板）
- 仅在 chat 回复底部加一行小字 + 可展开（D2，详见 §决策 D 轴）

ADR-035 的 4 候选方向（A/B/C/D）仍然 active：C 已被信号 8 否决，A/B/D 等待路径 β 闭环建起来后用真实使用数据二轮收敛。本 ADR 不动 Save / Summary / Tags 三按钮存留性（信号 9），那是 ADR-035 supersede ADR 的职责。

### 与 [ADR-032 Chat V0](chat-v0-streaming-and-persistence.md) 的关系

ADR-032 §决策 E1 / J1 把内置 system prompt 写为「**`ChatAppService.SystemPrompt` 是 `private const string`，每次 `BuildLlmRequest` 时拼到 `messages[0]`**」。ADR-032 §adr_revisit_when 第 5 条已预告：「产品需要在 chat 上做 tool calling / RAG / Memory Ledger 检索注入」是 ADR-032 复议触发条件之一；本 ADR 即按这条复议线索落地，但**采用最薄路径而非完整 RAG**。

## 备选方案

按 §决策 7 个轴分别列出。

### A. 检索策略

- **A1 朴素关键词 LIKE 匹配**：从用户当前消息抽 token（中文以单字 / 2-gram 为粒度，英文以 word 边界），对 `memory_entries.content` 做 `LIKE %token%` OR 拼接，按命中数 + `updated_at_utc DESC` 排序，取 top N。
- A2 最近 N 条：完全无关键词，按 `updated_at_utc DESC` 取 top N，不依赖检索质量。
- A3 SQLite FTS5 全文索引：建虚拟表 `memory_entries_fts`，BM25 相关性排序。
- A4 全注入：把所有 active memory 全拼进 system prompt（依赖 LLM 上下文窗口）。
- A5 完整 RAG：embedding（OpenAI text-embedding-3-small 或本地）+ vector store + cosine + rerank。

### B. 注入位置

- **B1 system prompt 末尾**：拼到现有 `ChatAppService.SystemPrompt` 常量后，作为单一 system message 发出。
- B2 独立 system message：messages 列表为 `[原 system, 注入 system, ...历史, user]`，两段 system 物理隔离。
- B3 user 消息前置：messages 列表为 `[原 system, ...历史, 注入 user "下面是相关记忆：...", user]`，记忆显式作为对话上文。
- B4 assistant 自言自语：在 user 之前插入一条 `assistant` 消息，模拟"agent 已经检索过记忆"。

### C. 注入条数上限

- **C1 固定 N = 5**：硬编码 5 条上限，超出按相关性 / 更新时间截断。
- C2 字数预算 = 800 字符：动态条数，累计 ≤ 预算即停。
- C3 token 预算 = 500 tokens：用 tokenizer 估算，更精确但需引入 tiktoken 依赖。

### D. 引用感知（用户可见性）

- D1 完全不展示：注入内部行为，用户不感知。
- **D2 一行小字 + 可展开**：在 assistant 流式回复末尾以独立 SSE chunk 形式追加「📒 引用了 N 条 memory」一行；renderer 显示为浅色小字，可点击展开看 5 条命中条目的标题 / id（不展示完整内容）。
- D3 完整命中列表展示：在 chat 回复下方常驻一个引用列表面板。
- D4 引用 inline：在 LLM 回复正文中由 LLM 自己用 `[memory:id]` 标注引用位置，前端解析高亮。

### E. memory 范围

- **E1 仅 active**：`status = MemoryStatus.Active (1)`。
- E2 active + corrected：`status IN (1, 2)`。
- E3 active + corrected + archived：`status IN (1, 2, 3)`。
- E4 全状态除 SoftDeleted：`status IN (1, 2, 3)`，与 ADR-033 §G1 列表默认值同步。

### F. 失败模式

- **F1 降级不阻断**：检索抛异常 / 超时 / repo 不可达 → log warning + 注入空段 + chat 正常继续。
- F2 报错阻断：检索失败 → 返回 `Result.Failure(chat.memoryRetrievalFailed)`，HTTP 502。
- F3 降级 + 给 user 提示：注入空段 + 在 D2 一行小字处显示「记忆检索暂时失败」。

### G. 触发时机

- **G1 每次 send**：每次 `SendMessageStreamAsync` 都重新检索。
- G2 会话起始一次：建 session 时检索一次，缓存到 `chat_sessions` 表新列。
- G3 滚动 / 增量：每 N 条 user message 触发一次。

### H. 端口位置（工程轴）

- **H1 `Application/Interfaces/IChatMemoryRetriever`（facade）**：与 `IChatAppService` / `IInboxSummaryAppService` 同一目录。
- **H2 `Application/Services/ChatMemoryRetriever`（实现）**：默认实现内联 `IMemoryLedgerRepository` 调用与关键词匹配逻辑。
- H3 接口放 `Abstractions`（按 ADR-037）：让 Infrastructure 可替换实现。

注：本 ADR §H1 / §H2 是「最不确定但默认走对称」的轴；完整论证见 §被否决方案 H。

## 被否决方案与理由

### A 检索策略

**A2（最近 N 条无关键词）被否决**：完全不利用 user 当前问题信号，会注入大量无关记忆，token 浪费且 LLM 注意力被稀释；命中率必然趋近 0%。仅适合 memory 极少（< 10 条）的极早期，但即使 V0 也会迅速超过这个量级。

**A3（SQLite FTS5）被否决**：FTS5 在 SQLite 中需要建虚拟表 + 同步 trigger，对中文需要外接 tokenizer（默认 unicode61 对中文是按字符切，效果接近 A1）；引入新 schema migration，迁移成本不匹配 V0 价值；如果未来要升级，[ADR-013](interest-profile-weighting-and-decay.md) 的兴趣画像或本 ADR 的 §复议触发条件 都是更合适的入口。

**A4（全注入）被否决**：直接违反 token 预算，单 chat 请求 prompt token 会随 memory 数量线性增长；用户写满 inbox 后 chat 直接超 context window 报错。

**A5（完整 RAG）被否决**：核心理由是 [ADR-035](desktop-mvp-ux-thesis-via-dogfood.md) 与 [ADR-002 选择题优先于问答题](options-over-elaboration.md) 共同隐含的「最薄路径优先」精神。完整 RAG 需要：embedding provider 选型 + vector store 选型（pgvector / sqlite-vss / chromadb）+ 重排策略 + 嵌入版本兼容性 + offline 重嵌入路径，这些每一项都值得独立 ADR；在还没有证据证明 A1 朴素方案不够用时，先做 A1，等 §复议触发条件第 1 条「命中率 < 10%」触发再升级。**用户在 dogfood 信号 7 已经自我误归因「要做 RAG 才能…」，本 ADR 的工程价值之一就是证伪这个误归因——chat 读 memory ≠ RAG**。

### B 注入位置

**B2（独立 system message）被否决**：多段 system 在主流 LLM 上行为不稳定（OpenAI / Anthropic / DeepSeek 处理 messages 中第二条 system 的方式各不相同），有的会忽略、有的会合并、有的会按顺序处理；为单点轻量功能引入 provider-specific 行为风险不值。

**B3（user 消息前置）被否决**：会让 LLM 把"相关记忆"当成 user 的输入处理，可能产生「user 在告诉我这些事」的语义错位（实际是 agent 自己检索来的）；同时增加一条 user message 会扰乱后续会话历史的 turn 计数（ADR-032 的 message append 逻辑要适配）。

**B4（assistant 自言自语）被否决**：会让 LLM 把检索到的记忆当成 agent 自己之前说过的话，违背 [ADR-032](chat-v0-streaming-and-persistence.md) 关于 chat_messages 表只持久化真实双方对话的设计；且这是 prompt 工程层的"hack"，不利于后续替换 LLM。

### C 注入条数上限

**C2（字数预算 800）被否决**：用户感受到的"5 条 vs 10 条"是离散感（"我有几条相关记忆"），用条数边界更直观；800 字符在中文（≈400 字）上偏紧，5 条 V0 inbox/memory（每条 ≤ 4096 字符）上限不会爆 token，C1 已经隐式做了字数约束。

**C3（token 预算）被否决**：需要引入 tokenizer 包（每个 provider 不同）+ 估算误差；V0 阶段不值得。可作为复议方向。

### D 引用感知

**D1（完全不展示）被否决**：直接撞上 dogfood 信号 4「Save 没回报闭环」。如果用户感受不到 memory 被引用，本 ADR 的「让 PURPOSE.md MVP 第一信号发生」就失败。

**D3（完整命中列表常驻面板）被否决**：违反 dogfood 信号 8「让用户看记忆不是很重要」；在 chat 主屏旁边塞一个常驻 memory 面板，等于把 Memory 视图的内容反向塞回主屏，方向相反。

**D4（LLM inline 标注）被否决**：要求 LLM 在 user-facing 回复正文里嵌入 `[memory:id]` 标注，对 prompt 工程要求高，主流 provider 在中文上稳定性参差不齐；前端解析也是新工程。可作为后续优化。

### E memory 范围

**E2 / E3 / E4 被否决**：`MemoryStatus.Corrected (2)` 在 V0 阶段不会出现（[ADR-033](memory-ledger-v0-schema-and-storage.md) 没建对应 UI / 端点 让用户把 active 改成 corrected）；`Archived (3)` 同理；`SoftDeleted (4)` 必须排除（已删除的不应注入）。E1 是 V0 唯一现实选项；待 corrected / archived 真实出现时（§adr_revisit_when 已列），再升级到 E2/E3。

### F 失败模式

**F2（报错阻断）被否决**：用户的核心诉求是 chat 能用，记忆只是"上下文加分项"。memory 检索失败 → chat 全挂，等于让加分项升级为必备项，违背 [ADR-007](memory-privacy-and-user-control.md) 的"记忆服务于侍奉，不喧宾夺主"精神。

**F3（降级 + 用户提示）被否决**：在用户大概率不关心记忆机制存在的前提下（信号 8），把"记忆暂时失败"提示给用户是 over-communication；warning log 给开发者看就够了。

### G 触发时机

**G2（会话起始一次）被否决**：每次 send 的话题都可能不同，会话起始检索的 5 条记忆与第 10 轮 user 问题大概率无关；缓存粒度错。

**G3（滚动 / 增量）被否决**：增加状态机复杂度，V0 不值得；G1 简单且每次都是新鲜检索结果。

### H 工程轴

**H3（接口放 Abstractions 包）被否决**：[ADR-037](port-and-cross-layer-contract-assembly-split.md) 把 Abstractions 限定为「跨层技术契约 + LLM DTO」；`IChatMemoryRetriever` 是 **Application 内部协调端口**（仅 ChatAppService 消费，不被 Infrastructure 跨过来调用），不属于 Abstractions 的本职。放 `Application/Interfaces/` 与 `IInboxSummaryAppService`、`IChatAppService` 等内部 facade 对称（[ADR-030 Inbox 摘要 V0](inbox-item-summarize-v0.md) 同款论证）。

## 决策

### A1（A 轴）：朴素关键词 LIKE OR 匹配

**算法**：

1. 取 user 当前消息正文 `request.Content`
2. token 化：
   - 英文：`Regex.Matches(content, @"\p{L}{2,}")` 抽 ≥ 2 字母的单词
   - 中文：抽连续中文字符串，按 2-gram 切分（"信息整理" → ["信息", "息整", "整理"]）
   - 全部 lower-case，去重，保留 ≤ 16 个 token（防止 user 输入超长时 SQL 爆炸）
3. SQL：`SELECT ... FROM memory_entries WHERE status = 1 AND (content LIKE '%t1%' OR content LIKE '%t2%' OR ...) ORDER BY ... LIMIT 5`
4. 排序：按命中 token 数 DESC，并列时 `updated_at_utc DESC`，再并列 `id DESC`

**新方法在 `IMemoryLedgerRepository`**：

```csharp
Task<IReadOnlyList<MemoryLedgerEntry>> SearchByKeywordsAsync(
    IReadOnlyList<string> keywords,
    int limit,
    CancellationToken cancellationToken
);
```

实现放 `Infrastructure/Persistence/Memory/MemoryLedgerRepository`（[Dawning.ORM.Dapper 风格](persistence-repository-style-dawning-orm-dapper.md) 已锁定）。

### B1（B 轴）：拼接到内置 system prompt 末尾

`ChatAppService.BuildLlmRequest` 改造：检索结果非空时，把段落格式化为：

```
（你的长期记忆中以下记录可能与当前对话相关，每条最多 4096 字符；请仅在自然相关时引用，不要提示用户"我查过你的记忆"。）
- [m1.id 短前缀] m1.content
- [m2.id 短前缀] m2.content
...
```

拼到 `ChatAppService.SystemPrompt` 常量字符串末尾，仍以单一 system message 发出。检索返回空 → 不附加任何段落。

### C1（C 轴）：固定 N = 5

常量 `ChatMemoryRetriever.MaxRetrievedEntries = 5`。≥ 5 时按 §A1 排序截断。

### D2（D 轴）：一行小字 + 可展开

通过 SSE 旁路 chunk 实现，避免污染 LLM 回复正文：

1. `ChatAppService.SendMessageStreamAsync` 在检索完毕后立即 yield 一个非 LLM 的 `LlmStreamChunk`，kind = `MemoryAnnotation`（新枚举值，加在 `LlmStreamChunkKind`）
2. payload JSON：`{ "count": 3, "entries": [{"id":"019e...","contentPreview":"前 80 字符..."}] }`
3. renderer 端在消息卡片下方渲染浅灰色一行：「📒 引用了 3 条 memory」+ 可点击展开看 entries
4. 默认折叠（信号 8：默认不打扰）；展开后只看 id + content 前 80 字符（不展示完整内容，引用 id 让程序员可在 Memory 视图调试）

新增 `LlmStreamChunkKind.MemoryAnnotation`（在 `Dawning.AgentOS.Abstractions/Llm/`，按 ADR-037 §D 该枚举位置）。

### E1（E 轴）：仅 active

`status = MemoryStatus.Active (1)` 单一过滤。

### F1（F 轴）：降级不阻断

`ChatMemoryRetriever.RetrieveAsync` 内层 try/catch；异常 → `_logger.LogWarning(ex, "memory retrieval failed; chat continues without injection")` + 返回 `IReadOnlyList<MemoryLedgerEntry>.Empty`。chat 继续走原有流程，注入段为空。

### G1（G 轴）：每次 send

`SendMessageStreamAsync` 在持久化 user message 之前调用 `RetrieveAsync(request.Content, ct)`；不缓存到 session。

### H1 / H2（H 轴）：内部 facade + 默认实现

- 接口：`Application/Interfaces/IChatMemoryRetriever`，签名：

  ```csharp
  Task<IReadOnlyList<MemoryLedgerEntry>> RetrieveAsync(
      string userMessage,
      CancellationToken cancellationToken
  );
  ```

- 默认实现：`Application/Services/ChatMemoryRetriever`，依赖 `IMemoryLedgerRepository`（已存在）+ `ILogger<ChatMemoryRetriever>`
- DI 注册：scoped，与 `IChatAppService` 同 lifetime
- `ChatAppService` 构造函数追加 `IChatMemoryRetriever` 参数
- **API 不暴露**：不新增任何 endpoint；不暴露在 `IChatAppService` facade 上；架构测试断言 `IChatMemoryRetriever` 只被 `ChatAppService` 与同 namespace 测试引用

### I（错误码）：无新增

按 §F1 失败降级，本 ADR **不引入**任何新错误码。

## 影响

### 立即生效

1. **PURPOSE.md MVP 第一信号「Memory 真实复用」第一次有机器化路径触达**：之前 chat 完全不读 memory，本 ADR 之后任何被 Save 进 memory 的内容都有非零概率被 chat 引用，闭环建起。
2. **dogfood 信号 4 解锁**：用户点 Save 后下次 chat 真有可能引用，「Save 之后有什么用」可被实证回答。
3. **dogfood 信号 7 根因解决**：chat 不再是无记忆问答机，但**仍然不是 RAG**——证伪用户自我误归因（参见 [ADR-002](options-over-elaboration.md) 的最薄路径价值）。
4. **dogfood 信号 8 同时被尊重**：D2 默认折叠的一行小字，既给程序员/审计者「memory 断裂检测」入口（点开看 id + 前 80 字符可去 Memory 视图详查），又对普通用户最大化默认不打扰。
5. **ADR-032 §adr_revisit_when 第 5 条**「Memory Ledger 检索注入」**部分兑现**：tool calling / 多步骤循环仍未引入；Memory 检索注入的最薄版本落地。
6. **ADR-035 仍 active**：本 ADR 不 supersede ADR-035；UI 收敛由后续 ADR 负责。

### 解锁

1. 后续可基于命中率数据评估是否升级到 A3 / A5（§adr_revisit_when 第 1 条）。
2. 后续 [ADR-013 兴趣画像](interest-profile-weighting-and-decay.md) 落地后，可把"被本 ADR 引用过的 memory"作为兴趣信号源（被引用次数 ↑ → 权重 ↑）。
3. UI 收敛 ADR（supersede ADR-035）届时手上有真实数据：哪些 memory 被反复引用、哪些从不被引用 → 决定 Save 该不该存在、Memory 视图该不该浮上主屏。

### 风险

1. **关键词匹配粒度太粗**：中文 2-gram 在专业术语 / 同义词上会漏；缓解：§adr_revisit_when 第 1 条监测命中率 < 10% 触发升级。
2. **注入污染**：如果 memory 中存了过期或错误信息，LLM 可能盲信引用导致回复错误。缓解：§决策 D2 一行小字让用户能感知到引用过；prompt 中显式写「请仅在自然相关时引用」做引导。
3. **§决策 D2 可见性反过来打扰**：用户可能反而觉得「每条 chat 下面都一行小字烦」（信号 8 反向触发）。缓解：§adr_revisit_when 第 6 条专门列了「用户感知到打扰 → 回退 D1」分支。
4. **token 预算超**：5 条 × 4096 字符 = 20480 字符 ≈ 10000 tokens，叠加历史 + system prompt 可能吃满 GPT-4 / DeepSeek 的 context window。**早期 inbox 内容不会都是 4096 字符上限**（dogfood 数据：3 条 inbox 平均 < 100 字符），实际预算松。§adr_revisit_when 第 2 条监测注入占 prompt > 30% 时降 C1。
5. **被 LLM 当用户话**：§决策 B1 拼到 system prompt 末尾时，prompt 显式指示「这是长期记忆，不要提示用户」；如果不同 provider 表现不一，由 §决策 F1 降级 + 监测兜住。
6. **scope 字段未来扩展**：当前 `memory_entries.scope` 全是 `'global'`，§决策 A1 不按 scope 过滤。§adr_revisit_when 第 7 条已声明 scope 出现非 global 值时复议。

## 可机器化判据（架构测试）

新增以下断言（`tests/Dawning.AgentOS.Architecture.Tests`）：

- **`IChatMemoryRetriever_OnlyConsumedByChatAppService`**：扫描 `Dawning.AgentOS.Application` 程序集，断言 `IChatMemoryRetriever` 接口只被 `ChatAppService` 类型与 `*Tests` 的类型引用；不被 `Application/Interfaces/IChatAppService`（facade）暴露；不被任何 `Endpoints/` 类型引用（API 不暴露）。
- **`ChatMemoryRetriever_LivesInApplicationServicesNamespace`**：默认实现必须在 `Dawning.AgentOS.Application.Services` 命名空间。
- **`SearchByKeywordsAsync_LivesInDomainMemoryRepositoryPort`**：新方法必须挂在 `IMemoryLedgerRepository`（Domain 层端口），不得新建并行 repository。
- **`LlmStreamChunkKind_HasMemoryAnnotationValue`**：枚举有 `MemoryAnnotation` 值，且位置在 `Dawning.AgentOS.Abstractions.Llm`（按 ADR-037）。
- **`ChatAppService_DependsOnIChatMemoryRetriever`**：`ChatAppService` 构造函数参数列表包含 `IChatMemoryRetriever`。

新增以下单元 / 集成测试要点：

- 关键词 token 化：纯英文 / 纯中文 / 中英混合 / 数字 / 特殊字符 / 超长输入（截断到 ≤ 16 token）
- 检索：空 keywords → 返回空（不查 DB）；命中条数 < 5；命中条数 ≥ 5 排序；status 过滤
- 失败降级：repository 抛 → 返回 empty + 无异常透出
- ChatAppService 集成：检索结果非空 → LLM request 的 system message 包含注入段；为空 → system message 与 ADR-032 形态完全一致

## 复议触发条件

参见 front matter `adr_revisit_when` 字段，正文不重复（[SCHEMA §6.0 / §6.6](../../SCHEMA.md)）。

## 相关页面

- [PURPOSE.md MVP 第一信号「Memory 真实复用」](../../PURPOSE.md)
- [ADR-003 长期记忆是核心而非可选模块](long-term-memory-as-core-capability.md)
- [ADR-007 记忆隐私与用户控制](memory-privacy-and-user-control.md)
- [ADR-011 显式 Memory Ledger MVP](explicit-memory-ledger-mvp.md)
- [ADR-014 MVP 第一切片：chat + inbox 读侧](mvp-first-slice-chat-inbox-read-side.md)
- [ADR-032 Chat V0：分屏 + SSE + 持久化 + 内置 system prompt](chat-v0-streaming-and-persistence.md)（本 ADR 在其 §adr_revisit_when 第 5 条上落地）
- [ADR-033 Memory Ledger V0：schema + 状态机](memory-ledger-v0-schema-and-storage.md)
- [ADR-034 Inbox 显式沉淀进 Memory Ledger V0](inbox-to-memory-promotion-v0.md)（本 ADR 让 Save 第一次有回报闭环）
- [ADR-035 桌面 MVP UX 主叙事：dogfood 收敛](desktop-mvp-ux-thesis-via-dogfood.md)（本 ADR 严格落在其「后端能力扩展不增加主屏按钮 / 视图」的允许域内，不 supersede ADR-035）
- [ADR-028 LLM Provider V0：OpenAI/DeepSeek 抽象](llm-provider-v0-openai-deepseek-abstraction.md)
- [ADR-022 自维 Domain Event Dispatcher](no-mediator-self-domain-event-dispatcher.md)
- [ADR-002 选择题优先于问答题](options-over-elaboration.md)（dogfood 第 1 天 § 决策的 7 轴选择展开，是该规则的应用）
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)
