---
title: ADR-039 chat memory injection 加问句过滤 v0：retrieval-side 后过滤 + 结尾问号判定 + 可接受截断
type: adr
subtype: architecture
canonical: true
summary: ADR-038 dogfood 第 1 天暴露信号 9——朴素 LIKE 把 ledger 中 content 形态为「未答提问」的 memory 当事实注入 LLM system prompt（实例：用户问"我用什么技术栈"，命中既有 memory「以什么方式保存下来？」因共享 bigram「什么」），构成 ADR-038 §`adr_revisit_when` 第 3 条「用户开始抱怨『引用了过时 / 错误的 memory』」的早期形态。本 ADR 用最薄路径在 `ChatMemoryRetriever.RetrieveAsync` 调 `IMemoryLedgerRepository.SearchByKeywordsAsync` 之后对返回结果做 post-filter，丢掉 `content.TrimEnd()` 以 `?` 或 `？` 结尾的条目；不动 Domain 端口签名（`IMemoryLedgerRepository.SearchByKeywordsAsync` 不加参数）、不动 SQL（不加 NOT LIKE）、不动 schema（不加 `memory_kind` 字段、不引 migration v5）、不动 UI（memory 视图不暴露过滤行为），接受过滤后返回 < 5 条的「可接受截断」（ADR-038 §F1 静默降级精神延伸：用户感觉不到的问题不算问题）；新增 `internal static bool IsLikelyQuestion(string)` 平级 `Tokenize`；架构测试不加新断言（无新依赖、无新端口）；不解决跨主题召回率（仍是 ADR-038 §`revisit_when` 第 1 条 命中率 < 10% 的责任）、不解决 ledger 写入路径噪音（ADR-034 promote 路径职责）、不解决信号 10（chat 内沉淀入口）与信号 11（Save 反馈带 token 预览），各留独立 ADR。
tags: [agent, memory, memory-design]
sources: [raw/meetings/dogfood-mvp-pause-button-2026-05.md]
created: 2026-05-07
updated: 2026-05-07
verified_at: 2026-05-07
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/chat-context-memory-injection-v0.md, pages/adrs/memory-ledger-v0-schema-and-storage.md, pages/adrs/inbox-to-memory-promotion-v0.md, pages/adrs/memory-privacy-and-user-control.md, pages/adrs/explicit-memory-ledger-mvp.md, pages/adrs/desktop-mvp-ux-thesis-via-dogfood.md, pages/adrs/port-and-cross-layer-contract-assembly-split.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-07
adr_revisit_when: "用户报告「我存的事实没被引用」（即 question filter 误伤了 fact-shaped-as-question，例如『我妈生日是几月？』被误归类为问句）—— 误伤率 > 20% 时，迫使升级到 A3 持久化 `memory_kind` 枚举字段；用户开始要求「让某条 question 也参与召回」（例如把『下次去日本玩什么？』pin 住作为意图提示）—— 迫使引入 C2 `is_pinned` opt-out；过滤后返回 < `MaxRetrievedEntries` 的频次 > 30% 且对应 ledger 中问句条目占比 > 30% —— 迫使升级到 P2 多路检索（Repo 端 over-fetch 增加 + 端口加 `excludeQuestionLike` 参数）；ADR-038 §`adr_revisit_when` 第 1 条「命中率 < 10%」触发并要求升级到 FTS5 / embedding —— 迫使重审本 ADR：question filter 是否仍需要、还是被新检索引擎天然解决；用户开始 dogfood 多语言 chat（中文 inbox + 英文问句），结尾问号判定不覆盖句中疑问标点（如『How do I save?』） —— 当前 B1 已覆盖 ASCII `?` 故应通过，但跨语言 ledger 出现『记忆内容是英文 + 中文混排 + 结尾用句号但本质是问句』时迫使升级到 B2 疑问词字典；ledger 中 `MemoryStatus.Corrected` 真实出现并要求被注入（[ADR-033](memory-ledger-v0-schema-and-storage.md) 状态机解锁）—— 与 ADR-038 §E1 `revisit` 同步触发，本 ADR 须确认 corrected 条目仍走相同 question filter 还是另开规则。"
---

# ADR-039 chat memory injection 加问句过滤 v0：retrieval-side 后过滤 + 结尾问号判定 + 可接受截断

> [ADR-038](chat-context-memory-injection-v0.md) dogfood 第 1 天暴露信号 9：朴素 `LIKE` 把 ledger 中 content 形态为「未答提问」的 memory 当事实注入 LLM system prompt。本 ADR 用最薄路径在 `ChatMemoryRetriever.RetrieveAsync` 拿到 Repo 返回结果后做 post-filter，丢掉 `content.TrimEnd()` 以 `?` 或 `？` 结尾的条目；不动 Domain 端口、不动 SQL、不动 schema、不动 UI；接受过滤后 < 5 条的可接受截断（ADR-038 §F1 静默降级精神延伸）。

## 背景

### dogfood 信号汇总（2026-05-07，dogfood 第 2 天，ADR-038 上线后约 2 小时）

[ADR-038](chat-context-memory-injection-v0.md) 在 commit `15a9dca` 全面落地后，作者按本 ADR §sources 引用的 [docs/raw/meetings/dogfood-mvp-pause-button-2026-05.md](../../raw/meetings/dogfood-mvp-pause-button-2026-05.md) 「2026-05-07 第 3 次使用感受」节四步实验脚本试用，得到 PURPOSE.md MVP 第一信号「Memory 真实复用」**首次发生**（step 4 中 LLM 真的从注入的 system prompt 末尾读到 memory 并据此回答 .NET 10 + Electron）。但同一次 step 4 也立刻暴露了三条新信号，本 ADR 仅就**信号 9** 落决策，信号 10 与信号 11 留独立 ADR。

### 信号 9 的具体表现

step 4 用户输入「我用什么技术栈开发桌面应用？」时，[`ChatMemoryRetriever.Tokenize`](../../../src/Dawning.AgentOS.Application/Services/ChatMemoryRetriever.cs) 切出 12 个 CJK 2-gram，其中包含通用 bigram 「什么」。SQL `LIKE '%什么%'` 命中了 ledger 中两条 active memory：

- 条目 A（fact）：「我目前在用 .NET 10 + Electron 开发桌面应用 dawning-agent-os」—— 命中 4 个 keyword（开发 / 桌面 / 面应 / 应用），按命中数排序排第 1
- 条目 B（**question**）：「以什么方式保存下来？」—— 仅命中「什么」这一个高频通用 bigram，但因 over-fetch=20 + Take(5) 仍被注入

条目 B 是一条**未答的提问**，被注入到 LLM system prompt 末尾后，最坏情况是让 LLM 把它当作"用户已在询问的事实"参考，污染回复。

### 命中 ADR-038 自身的复议触发

[ADR-038 §`adr_revisit_when`](chat-context-memory-injection-v0.md) 第 3 条原文：

> 用户开始抱怨「引用了过时 / 错误的 memory」（迫使 score / freshness / 用户标记不相关入口）

当前情境是「**事实已发生但用户尚未抱怨**」的早期形态——作者作为 dogfood 用户在 §raw/meetings 信号稿里留下了观察，但没有升级为 D 级信任损伤。本 ADR 选择**早一步行动**，原因有二：

1. **修复成本极低**：[ADR-002 选择题优先于问答题](options-over-elaboration.md) 与 [Rule 实现前必须方案先行](../rules/plan-first-implementation.md) 共同隐含的精神是「让用户用最低成本收敛」——本 ADR 估算 1 文件 / +30 行业务代码 / +80 行测试，远低于"等用户抱怨再修"的迟滞成本。
2. **下一次 dogfood 大概率会复发**：ledger 中现存 question 条目（来自 [ADR-034](inbox-to-memory-promotion-v0.md) Inbox→Memory promote 路径，scope 固定为 "inbox"）至少占 dogfood 第 1 天 ledger 的 1/2（用户把"产品自我解释类提问"全部 capture 进 inbox 并 Save 进 memory，见 §raw/meetings 「2026-05-07 真实使用 1 小时」节信号 3）。下一次同主题 chat 极易再次命中。

### 与 ADR-038 / ADR-033 / ADR-034 的关系

本 ADR **不 supersede** ADR-038；ADR-038 的 7 条决策（A1 / B1 / C1 / D2 / E1 / F1 / G1 / H1）全部保持。本 ADR 在 ADR-038 §A1 检索结果之后**追加一道过滤**，结构上是 ADR-038 的"后处理薄层"。

本 ADR **不修改** [ADR-033 Memory Ledger V0](memory-ledger-v0-schema-and-storage.md) schema：不加 `memory_kind` 字段、不进 migration v5、不动 ledger 写入约束。这与 ADR-033 §H1「兴趣画像字段不进 V0 schema，留待旁表」精神一致——schema 演化必须由真实需求强证据驱动。

本 ADR **不修改** [ADR-034 Inbox→Memory promote](inbox-to-memory-promotion-v0.md) 写路径——不在 promote 时拦截问句（被 §被否决方案 A4 否决，理由见下）。promote 路径仍按 ADR-034 §决策原样：用户对"什么值得记"保留全部主权，符合 [ADR-007 记忆隐私与用户控制](memory-privacy-and-user-control.md) 「记忆服务于侍奉，不替主人筛选」精神。

## 备选方案

按本 ADR 的 4 个决策轴分别列出。

### A. 在哪一层做过滤？

- **A1 retrieval-side 后过滤（Application 层）**：在 [`ChatMemoryRetriever.RetrieveAsync`](../../../src/Dawning.AgentOS.Application/Services/ChatMemoryRetriever.cs) 拿到 `IMemoryLedgerRepository.SearchByKeywordsAsync` 返回的 `IReadOnlyList<MemoryLedgerEntry>` 后，丢掉问句条目，再交给 `ChatAppService` 拼 system prompt。
- A2 SQL-side 过滤（Infrastructure 层）：在 `MemoryLedgerRepository.SearchByKeywordsAsync` 的 SQL 里加 `AND content NOT LIKE '%?%' AND content NOT LIKE '%？%'`；端口签名加 `bool excludeQuestionLike` 参数。
- A3 持久化 `memory_kind` 枚举字段：migration v5 加 `memory_kind INTEGER NOT NULL DEFAULT 0`（Unknown / Fact / Question / Preference / Task）+ 改 `MemoryLedgerEntry` 聚合 + 改 `IInboxToMemoryAppService` 写入路径 + 改 Memory 视图编辑界面 + retrieve 时 `WHERE memory_kind IN (Fact, Preference)`。
- A4 write-side block：在 [ADR-034](inbox-to-memory-promotion-v0.md) `IInboxToMemoryAppService.PromoteAsync` 拦下结尾问号 content，禁止入 ledger。

### B. 用什么规则识别"问句"？

- **B1 仅看 `content.TrimEnd()` 是否以 `?` 或 `？` 结尾**：纯字符串末位判定，不依赖任何词典 / 模型。
- B2 结尾问号 OR 含疑问词字典命中（什么 / 如何 / 为什么 / 怎么 / 怎样 / 吗 / 吧 / what / how / why / where / when / who）。
- B3 LLM 分类：每次 retrieval 增加一次 LLM call 把候选 memory 标 `fact | question | other`。
- B4 启发式降权而非丢弃：keyword tokens 含「什么 / 如何 / 怎么」时，对命中的"含问号" memory 降权（命中数减 1）而不是直接丢弃。

### C. 误伤如何处理？

- **C1 V0 接受误伤**：用户可在 Memory 视图编辑界面把问号去掉重写。
- C2 加 `is_pinned BOOLEAN` schema 字段：pin = 强制注入，绕过 question filter。
- C3 多路检索补满：filter 后 < 5 条时，从剩余的"被过滤问句"中选最高 hit-count 的若干条补到 5 条上限。

### D. UI 是否暴露过滤行为？

- **D1 完全不暴露**：与 ADR-038 §决策 D2 静音原则一致——retrieval 内部行为，用户不感知。
- D2 Memory 视图给问句条目加徽章「不会被自动召回」。
- D3 SSE `memoryAnnotation` chunk 加 `filteredCount` 字段，renderer 可选展示「⨯ 过滤了 N 条问句」。

## 被否决方案与理由

### A 在哪一层

**A2（SQL-side 过滤）被否决**：要么把"问句判定"硬编进 Domain 端口签名（污染契约——领域端口不该知道 chat 调用方的注入策略），要么用 `excludeQuestionLike` 这种泄漏型参数让 Repo 知道上层语义。两条都违反 [ADR-037 端口与跨层契约](port-and-cross-layer-contract-assembly-split.md) 的端口职责单一原则。SQL `NOT LIKE '%?%'` 本身也无法处理"末位 vs 句中"区别（如「我有 3 个项目? 不对，是 4 个」会被误伤）。

**A3（持久化 `memory_kind`）被否决**：成本与价值不匹配。需要：

- migration v5（破坏性，需评估现有 ledger 数据兜底）
- 改 [ADR-033](memory-ledger-v0-schema-and-storage.md) `MemoryLedgerEntry` 聚合 + 全部相关测试
- 改 [ADR-034](inbox-to-memory-promotion-v0.md) `IInboxToMemoryAppService.PromoteAsync` 写入路径
- 改 Memory 视图编辑界面（让用户能看 / 改 kind）
- 决定"未指定 kind 时怎么算"（默认 Fact 会改变 ADR-038 注入行为；默认 Unknown 又让 retrieve 必须保守过滤导致零召回）

至少 5+ 文件改动，且把"问句 vs 事实"提升为持久化语义后退出成本极高（schema 字段不能轻易撤）。在还没有证据证明 A1 不够用之前，A3 是过度工程；列入 §`adr_revisit_when` 第 1 条作为升级路径。

**A4（write-side block）被否决**：违反 [ADR-007 记忆隐私与用户控制](memory-privacy-and-user-control.md) 与 [ADR-001 管家定位与主客体边界](butler-positioning-and-subject-object-boundary.md)——agent 不替主人决定"什么值得记"。用户主动选择 Save 进 memory 的内容（哪怕是问句、哪怕是无意义字符），ledger 都应忠实保存；过滤是 retrieve 时机的选择，不是 store 时机的审查。这是 PURPOSE.md §4.1 「记忆服务于侍奉，不用于行为操控」的硬约束。

### B 识别规则

**B2（疑问词字典）被否决**：误伤面大且文化敏感。

- 中文「我有什么计划」其实是 fact-shaped-as-question（"用户记下自己的计划清单标题"）但会被误归类
- 「如何」常作介词（"如何看待"），不必然是问句
- 「吗 / 吧」在不同方言 / 网络语中意义偏移（"好吧" 不是问句）
- 多语言 dogfood 时（PURPOSE.md §4.1 多语言 chat 是已预告的能力扩展点）字典维护成本指数级上升

B1 用末位标点判定既客观又稳定，且 TrimEnd 已自然处理 trailing whitespace。误伤极少数 fact-shaped-as-question 场景（"我妈生日是几月？" 这种把事实自问形式记下来的）已列入 §`adr_revisit_when` 第 1 条触发条件。

**B3（LLM 分类）被否决**：每次 retrieval 增加一次 LLM call，违反 ADR-038 §决策 G1「每次 send 都查」的零额外成本前提。Token 成本、延迟、稳定性（中文 prompt 工程在不同 provider 上行为差异大）三项都不可控。

**B4（启发式降权）被否决**：实现复杂度（要改 [`MemoryLedgerRepository.SearchByKeywordsAsync`](../../../src/Dawning.AgentOS.Infrastructure/Persistence/Repositories/Memory/MemoryLedgerRepository.cs) 的 hit-count 排序逻辑）远高于 B1 直接丢弃；且"降权后仍可能进入前 5"意味着污染未根治。`filter-or-keep` 的二值决策比"调权重"在工程层更易解释、更易测试、更易回退。

### C 误伤处理

**C2（`is_pinned` schema 字段）被否决**：与 A3 同根问题——schema 演化成本高。在没有真实用户要求"pin 一条问句"的证据之前，加这个字段是 WIP。列入 §`adr_revisit_when` 第 2 条作为升级路径。

**C3（多路检索补满）被否决**：违反"接受可接受截断"的 V0 精神。多路检索意味着：

- 要在 `ChatMemoryRetriever` 里维护两个候选池（filtered / filtered-out）
- 要决定补满策略（按 hit-count？按 updated_at？随机？）
- 要确保补进来的"被过滤问句"不会再次踩到信号 9 的污染

ADR-038 §F1 已经确立"静默降级"原则——filter 后 0 条与 retrieve 失败 0 条对用户体验等价（都是"📒 引用了 N 条 memory"小字不出现）。ledger 中 question 条目占比的统计列入 §`adr_revisit_when` 第 3 条作为升级路径触发条件。

### D UI 暴露

**D2（Memory 视图加徽章）被否决**：与 ADR-038 信号 8「Memory 视图对普通用户不必要」继续冲突。让 Memory 视图承担「retrieve 行为可视化」会反过来支撑 [ADR-035](desktop-mvp-ux-thesis-via-dogfood.md) §被否决方案 C「Memory-first 重定位」，而该方向已在 ADR-038 §背景被信号 8 否决。

**D3（SSE 加 `filteredCount`）被否决**：

- 把 retrieve 内部细节扩展进 [SSE wire 契约](../../../src/Dawning.AgentOS.Api/Endpoints/Chat/ChatEndpoints.cs)（已由 ADR-032 §H1 + ADR-038 §决策 D2 锁定），扩张 API 表面积
- D2 静音原则：用户不感知 = 用户不需要被告知"有过滤发生"
- 调试需要时，warn-level log 已能给开发者看（与 ADR-038 §F1 同款机制）

## 决策

### A1（A 轴）：retrieval-side 后过滤

[`ChatMemoryRetriever.RetrieveAsync`](../../../src/Dawning.AgentOS.Application/Services/ChatMemoryRetriever.cs) 流程改为：

```
1. Tokenize(userMessage) → keywords (现有逻辑，不变)
2. keywords.Count == 0 → return Array.Empty (现有，不变)
3. raw = await _memoryRepository.SearchByKeywordsAsync(keywords, MaxRetrievedEntries=5, ct)  (现有，不变)
4. ★ 新增：filtered = raw.Where(e => !IsLikelyQuestion(e.Content)).ToList()
5. return filtered  (类型保持 IReadOnlyList<MemoryLedgerEntry>)
```

`MaxRetrievedEntries` 仍传 5，`MemoryLedgerRepository.SearchByKeywordsAsync` 的实现不动（内部 over-fetch=20 + hit-count rerank + Take(5) 全部保留）。过滤发生在 Repo 返回之后，意味着可能返回 0–5 条。

**Domain 端口契约不变**：[`IMemoryLedgerRepository.SearchByKeywordsAsync`](../../../src/Dawning.AgentOS.Domain/Memory/IMemoryLedgerRepository.cs) 签名不加任何参数，符合 [ADR-037 端口与跨层契约](port-and-cross-layer-contract-assembly-split.md) 端口职责单一原则。

### B1（B 轴）：仅看 `content.TrimEnd()` 是否以 `?` 或 `？` 结尾

```csharp
internal static bool IsLikelyQuestion(string content)
{
    if (string.IsNullOrWhiteSpace(content)) return false;
    var trimmed = content.TrimEnd();
    if (trimmed.Length == 0) return false;
    var last = trimmed[^1];
    return last == '?' || last == '？';
}
```

放在 [`ChatMemoryRetriever`](../../../src/Dawning.AgentOS.Application/Services/ChatMemoryRetriever.cs) 内，与现有 `internal static IReadOnlyList<string> Tokenize(string?)` 平级。`internal` 让 `Dawning.AgentOS.Application.Tests` 直接覆盖 §可机器化判据中列出的边界。

### C1（C 轴）：V0 接受误伤

不引入 schema 字段、不引入 opt-out 机制。fact-shaped-as-question 的极少数误伤由 §`adr_revisit_when` 第 1 条监测；用户可在 [Memory 视图](../../../apps/desktop/src/renderer/index.html) 编辑界面把问号去掉重写。

### D1（D 轴）：完全不暴露

不动 SSE wire（[`ChatEndpoints.cs`](../../../src/Dawning.AgentOS.Api/Endpoints/Chat/ChatEndpoints.cs) 不改）、不动 renderer、不动 Memory 视图。过滤行为对用户透明，与 ADR-038 §决策 D2 静音原则一致。

## 决策结果

### 文件改动清单（plan-first，未实施）

| 文件 | 改动 | 估算 |
|---|---|---|
| [src/Dawning.AgentOS.Application/Services/ChatMemoryRetriever.cs](../../../src/Dawning.AgentOS.Application/Services/ChatMemoryRetriever.cs) | 加 `IsLikelyQuestion`；`RetrieveAsync` 在 Repo 调用后追加过滤；class summary / remarks 段补 §A1 来源说明 | +30 / -2 |
| [tests/Dawning.AgentOS.Application.Tests/Services/ChatMemoryRetrieverTests.cs](../../../tests/Dawning.AgentOS.Application.Tests/Services/ChatMemoryRetrieverTests.cs) | 新增 5–7 个单测覆盖 §可机器化判据 | +90 / -0 |
| 架构测试（[tests/Dawning.AgentOS.Architecture.Tests/LayeringTests.cs](../../../tests/Dawning.AgentOS.Architecture.Tests/LayeringTests.cs)） | **不加新断言**（无新依赖、无新端口、无新枚举）| 0 |

### 不变量

- Domain：`IMemoryLedgerRepository` / `MemoryLedgerEntry` / `MemoryStatus` / `MemorySource` / `MemorySensitivity` 全部不动
- Infrastructure：`MemoryLedgerRepository.SearchByKeywordsAsync` 实现不动，schema 不动，无 migration
- Abstractions：`LlmStreamChunk` / `LlmStreamChunkKind` 不动
- Api：[`ChatEndpoints.MapChatEndpoints`](../../../src/Dawning.AgentOS.Api/Endpoints/Chat/ChatEndpoints.cs) 不动，SSE wire 不动
- Desktop：[apps/desktop/src/renderer/index.html](../../../apps/desktop/src/renderer/index.html) / [main.ts](../../../apps/desktop/src/main.ts) / [preload.ts](../../../apps/desktop/src/preload.ts) 不动

### 与 ADR-038 决策的并存关系

| ADR-038 决策 | 是否仍有效 | 关系 |
|---|---|---|
| A1 朴素 LIKE | ✅ 仍有效 | 本 ADR 在其结果之后追加过滤 |
| B1 system prompt 末尾注入 | ✅ 仍有效 | 注入位置不变；过滤后空列表 ⇒ 不附加 memory 段落 |
| C1 N=5 | ✅ 仍有效 | filter 后 ≤ 5 |
| D2 折叠静音提示 | ✅ 仍有效 | filter 后 0 条 ⇒ 不发 SSE memoryAnnotation 帧（与 ADR-038 §F1 0 条不显示同款） |
| E1 active-only | ✅ 仍有效 | 过滤前提 |
| F1 静默降级 | ✅ 仍有效，且本 ADR 借用其精神 | filter 截断 ⇒ 用户不感知，与 retrieve 失败的静默对齐 |
| G1 每次 send 重新检索 | ✅ 仍有效 | filter 在每次 retrieve 内 |
| H1 Application 内部门面 | ✅ 仍有效 | 过滤逻辑在同一 facade 实现内 |

## 可机器化判据（实现 ADR 时落实）

实现 commit 必须让以下判定全部可被自动化验证：

1. **`IsLikelyQuestion` 单测覆盖以下用例（全部断言 true）**：
   - `"以什么方式保存下来？"` → true（CJK 问号末位）
   - `"保存后会怎样?"` → true（ASCII 问号末位）
   - `"以什么方式保存下来？  "` → true（trailing whitespace 后仍以 `？` 结尾）
2. **`IsLikelyQuestion` 单测覆盖以下用例（全部断言 false）**：
   - `"我目前在用 .NET 10 + Electron 开发桌面应用 dawning-agent-os"` → false（无问号）
   - `"我有 3 个项目? 不对，是 4 个"` → false（问号在句中）
   - `""` → false（空字符串非问句）
   - `null` → false（实测时让方法签名容纳，或在 `RetrieveAsync` 调用前已过滤 null content）
3. **`RetrieveAsync` 集成路径单测**：
   - 给 `_memoryRepository.SearchByKeywordsAsync` mock 返回 `[fact_a, question_b, fact_c]` → 实测 `RetrieveAsync` 返回 `[fact_a, fact_c]`，且 `question_b` 的 Id / Content 不出现在结果中
   - mock 返回全 question 列表 → 实测返回 `Array.Empty<MemoryLedgerEntry>`
4. **架构测试（LayeringTests）不新增断言**：本 ADR 不引入新端口 / 新枚举 / 新依赖；现有 5 条 ADR-038 不变量（[LayeringTests.cs](../../../tests/Dawning.AgentOS.Architecture.Tests/LayeringTests.cs) 中 `IChatMemoryRetriever_OnlyConsumedByChatAppService` 等）覆盖即可。
5. **不变量回归**：本 ADR 实现 commit 后，全部测试用例（含 ADR-038 commit 5 中的 5 条架构断言）必须保持 green。
