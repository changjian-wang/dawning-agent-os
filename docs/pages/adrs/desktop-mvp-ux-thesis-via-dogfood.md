---
title: ADR-035 桌面 MVP UX 主叙事：暂停按钮累加，dogfood ≥ 7 天后从 4 候选方向收敛
type: adr
subtype: scope
canonical: true
summary: 截至 2026-05-06 桌面 V0（chat 双栏 + inbox 三按钮 + Memory 隐藏次级视图）落地后，主人首次肉眼验收反馈「按钮太多 / 中规中矩 / 没有特色」，同时 PURPOSE.md MVP 第一信号「Memory 真实复用」与当前主舞台是 inbox 不是 Memory 的视觉权重错位；本 ADR 决定不在没有 dogfood 数据的情况下盲选下一步，先暂停所有「再加按钮 / 再加视图」的 ADR 提案（包括预期中的 ADR-036 summary/tags 持久化、ADR-037 chat→ledger 等），由主人按当前形态 dogfood ≥ 7 个自然日且 inbox 真实捕获 ≥ 30 条，期间记录 inbox 捕获率、Save 点击率、Memory 视图打开率与「想 save 但没按」「真的被复用」的样本，然后由数据驱动从 4 候选方向（A 极简：3 按钮合 1 个 Process 弹窗；B 统一收件箱：chat 助手回复也作为 inbox 项；C Memory-first：Memory 升主屏、inbox 降侧栏 / menu bar 常驻；D 零按钮：后端自动 summarize+tag+提案 save、UI 只 confirm/dismiss）中选一条胜出方向开新 ADR supersede 本 ADR；本 ADR 不否决任何候选方向，只否决「无数据拍板」与「按钮无序累加」两条路径。
tags: [agent, product-philosophy, interaction-design]
sources: []
created: 2026-05-06
updated: 2026-05-06
verified_at: 2026-05-06
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/options-over-elaboration.md, pages/adrs/mvp-main-scenario-information-curation.md, pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/butler-positioning-and-subject-object-boundary.md, pages/adrs/long-term-memory-as-core-capability.md, pages/adrs/explicit-memory-ledger-mvp.md, pages/adrs/inbox-v0-capture-and-list-contract.md, pages/adrs/inbox-item-summarize-v0.md, pages/adrs/inbox-item-tagging-v0.md, pages/adrs/chat-v0-streaming-and-persistence.md, pages/adrs/memory-ledger-v0-schema-and-storage.md, pages/adrs/inbox-to-memory-promotion-v0.md, pages/adrs/desktop-renderer-v0-native-html-and-ipc-bridge.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-06
adr_revisit_when: "（任一触发即可复议）主人 dogfood 满 7 个自然日且 inbox 真实捕获条目 ≥ 30 条，且对 4 候选方向之一形成倾向（哪怕只是「先试 C」这种弱倾向）；主人 dogfood 期间主动表达 4 候选方向之一胜出，或提出全新第 5 方向；主人在 dogfood 期间发现非 UI-加按钮的紧急产品需求（先开新 ADR 评估，不直接 supersede 本 ADR）；外部触发的「再加按钮 / 再加视图」ADR 提案出现时（该提案必须在 §备选方案 中先回应本 ADR §决策 中收集到的 dogfood 数据，否则不得合入）；dogfood 期间 Save 按钮真实点击率 < 5%（说明 ADR-034 的核心动作未被采用，需要 D 零按钮派或 C Memory-first 重定位）；dogfood 期间 Memory 视图被打开次数 = 0（说明 PURPOSE.md MVP 第一信号「Memory 真实复用」未发生，Memory-first 重定位变为强候选）；dogfood 期间出现 ≥ 3 次「想 save 但没按 / 不知道按哪个」（说明三按钮模型已破，A 极简弹窗或 D 零按钮派变为强候选）。"
---

# ADR-035 桌面 MVP UX 主叙事：暂停按钮累加，dogfood ≥ 7 天后从 4 候选方向收敛

> 桌面 V0 三层 UI（chat 双栏 + inbox 三按钮 + Memory 隐藏次级视图）落地后主人首次肉眼验收反馈「按钮太多 / 中规中矩 / 没有特色」，本 ADR 拒绝在没有 dogfood 数据的情况下盲选下一步：暂停所有「再加按钮 / 再加视图」ADR 提案；由主人 dogfood ≥ 7 天 + ≥ 30 条 inbox 捕获后，从 4 个候选方向（A 极简弹窗 / B 统一收件箱 / C Memory-first 重定位 / D 零按钮自动）由数据驱动选一条胜出方向开新 ADR supersede 本 ADR。

## 背景

### 当前桌面 V0 形态（截至 2026-05-06）

桌面端按 [ADR-014](mvp-first-slice-chat-inbox-read-side.md)（chat + inbox 第一切片）、[ADR-027](desktop-renderer-v0-native-html-and-ipc-bridge.md)（原生 HTML + IPC 桥）、[ADR-032](chat-v0-streaming-and-persistence.md)（分屏 chat）与 [ADR-033](memory-ledger-v0-schema-and-storage.md)（Memory 视图切换器）逐步累加，叠加最近落地的 [ADR-030](inbox-item-summarize-v0.md)（Summarize 按钮）、[ADR-031](inbox-item-tagging-v0.md)（Tags 按钮）和 [ADR-034](inbox-to-memory-promotion-v0.md)（📒 Save 按钮）三层 inbox 动作，呈现为下面三层 UI：

- **顶部视图切换器**：`chat & inbox` ↔ `memory` 两个视图模式
- **chat & inbox 视图**（默认）：左侧 chat 双栏（pinned `+ 新会话`、session pill 列表、消息流、输入框 + Send），右侧 inbox 单栏（顶部 Capture 区 + Recent items 列表，每条 3 个 action 按钮：`Summarize` / `Tags` / `📒 Save`）
- **memory 视图**：CRUD 表 + status 过滤 + soft-delete toggle，需要主动切换到才能看到

这是 ADR 切片自然累加产物：每个 ADR 解决一个具体问题（总结、打标、沉淀），每个都加一个按钮。

### 触发本 ADR 的产品信号

ADR-034 落地后主人首次肉眼验收的原话是：

> "左边和右边要操作的有点多，按钮也太多。看起来也中规中矩，没有特色。"

这是产品定位信号，不是 polish 信号。三个独立判断：

1. **「按钮太多」**：每条 inbox 项 3 个 action 按钮 + 顶部 Capture + 输入区 Send = 5 个动作面，已接近"控制台"密度。如果继续按 [ADR-034 §adr_revisit_when](inbox-to-memory-promotion-v0.md) 中已经预告的「ADR-035 落地 summary / tags 持久化」「ADR-036 chat→ledger」继续走，按钮会从 3 涨到 5–7 个，"按钮太多"会被放大成"按钮不可用"。
2. **「没特色 / 中规中矩」**：当前 UI 像普通"笔记 + 聊天" app，没有让 [PURPOSE.md](../../PURPOSE.md) MVP 第一信号「**Memory 被真实复用**」浮上来——Memory 是隐藏次级视图，inbox 是默认主舞台，视觉权重与产品契约错位。
3. **PURPOSE.md MVP 信号错位**：[PURPOSE.md §2 / §4.1](../../PURPOSE.md) 反复强调"长期记忆是核心而非可选模块"「记忆服务于侍奉」「Memory Ledger 可查看 / 编辑 / 删除」，但当前 UI 把 Memory 藏在视图切换器后面，让用户必须主动点才能看到自己的"长期记忆库"。这违背 [ADR-002 选择题优先于问答题](options-over-elaboration.md) 的精神——我们没有把 Memory 复用本身变成默认入口。

### 此时为什么不能立刻拍板下一步

[Rule 实现前必须方案先行](../rules/plan-first-implementation.md) 要求方案先行；[ADR-002 选择题优先于问答题](options-over-elaboration.md) 要求模糊时给候选而不是反问。这两条对内同样适用：在没有 dogfood 数据的情况下，主人和 agent 都无法准确回答「按钮太多」是 (a) 真的太多还是 (b) 视觉密度问题，「没特色」是 (c) 主叙事错位还是 (d) 美化不足。

不同诊断对应完全不同的修复路径——盲选会把 PURPOSE.md MVP 第一信号验证窗口浪费在错误方向上。

## 备选方案

### A. 极简派：3 按钮合 1 个 "Process →" 弹窗

每条 inbox 项只保留单个 `Process →` 按钮，点击弹出选择面板（Summarize / Tags / Save 三选一或多选）。

- **优点**：按钮数 3→1，视觉密度立刻降低；与 [ADR-002 选择题优先](options-over-elaboration.md) 暗合（用户只需"选一个"而不是"在 3 个动作里推断要哪个"）。
- **风险**：弹窗反而增加点击层级（从 1 click 变 2 click），可能放大"操作多"的反馈而非缓解。
- **dogfood 验证条件**：`Save 点击率 / Capture 数 ≥ 30%` + 「想 save 但没按」反馈 ≥ 3 次时此方向不强。

### B. 统一收件箱派：chat 助手回复也作为 inbox 项

chat 与 inbox 不再是双栏分隔，而是合并成单个时间线，user 输入与 assistant 回复都进入 inbox（assistant 回复带 source = "chat"），统一适用 Summarize / Tags / Save 动作。

- **优点**：左右栏合一，视觉简化；解锁 [ADR-034 §adr_revisit_when](inbox-to-memory-promotion-v0.md) 第四条「用户开始要求 chat 也有同款 Save 按钮」的需求；让 chat 不再是"用完即抛"的会话，而是默认沉淀入口。
- **风险**：重构成本中（chat 的 sessionId 与 inbox item 的语义不同，合并需要新的数据模型）；可能稀释 chat 的"对话感"。
- **dogfood 验证条件**：「chat 中要 save 一条 assistant 回复」反馈 ≥ 2 次时此方向变强。

### C. Memory-first 重定位

把 Memory 视图升为默认主屏（启动时直接看到「这是你的长期记忆库」），inbox 降为侧边栏快捷捕获，或抽到 macOS menu bar / 全局快捷键常驻。chat 也降为侧边栏或独立窗口。

- **优点**：直接回应 PURPOSE.md MVP 第一信号「Memory 被真实复用」——Memory 视觉权重 = 产品契约权重；让"长期记忆库"成为产品身份的视觉锚点；最贴 [ADR-003 长期记忆是核心而非可选模块](long-term-memory-as-core-capability.md) 与 [ADR-011 显式 Memory Ledger MVP](explicit-memory-ledger-mvp.md) 的精神。
- **风险**：重写主叙事，UI 重做面大；需要重新回答「Memory 视图首屏看到的是什么」「inbox / chat 怎么"捕获到位"而不需要切到主屏」。
- **dogfood 验证条件**：`Memory 视图打开次数 = 0` 或主人反馈"我都不记得 Memory 视图存在"时此方向变强。

### D. 零按钮派：后端自动 summarize + tag + 提案 save，UI 只 confirm / dismiss

inbox 项捕获后，后端自动生成 summary + tags + 提案 save 候选；UI 只保留两个动作 `✓ Confirm`（保留 / 沉淀）/ `✗ Dismiss`（丢弃）。三个 LLM 动作变为后台默认行为，user 只做"评委"。

- **优点**：按钮数 3→2，且这 2 个按钮是 [PURPOSE.md core_interaction_principle](../../PURPOSE.md) 的直接体现（让 user 当评委而非作者）；解决"按钮太多"最彻底。
- **风险**：隐式行为成本高（自动调 LLM 即使 user 不需要也烧 token）；非幂等性更难解释（user 看到的是后端"已经处理"而不是"我点了所以处理"）；与 [ADR-030](inbox-item-summarize-v0.md) / [ADR-031](inbox-item-tagging-v0.md) 现有设计「按钮即触发」的语义反转，需要把 cost / consent 模型重写。
- **dogfood 验证条件**：用户 capture 后从未点 Summarize / Tags（仅点 Save 或都不点）时此方向变强。

### E. 立刻在 A/B/C/D 中拍板（无 dogfood 数据）

主人 / agent 凭直觉立即选择上述某一方向开 ADR 落地。

### F. 立刻开 ADR-036 summary/tags 持久化 + ADR-037 chat→ledger，按既定路线扩张

按 [ADR-034 §adr_revisit_when](inbox-to-memory-promotion-v0.md) 预告的两个未来 ADR 直接开工，把 5–7 个按钮塞满。

## 被否决方案与理由

### E. 立刻拍板：被否决

无 dogfood 数据时拍板等于赌博——4 个候选方向解决的都是同一个表面信号（"按钮太多 / 没特色"），但根因诊断不同（[A] 视觉密度 / [B] chat-inbox 边界 / [C] 主叙事错位 / [D] 隐式 vs 显式动作）。错误诊断会让重写出来的 UI 解决错误的问题，浪费 PURPOSE.md MVP 第一信号「Memory 真实复用」的验证窗口。

[ADR-002 选择题优先](options-over-elaboration.md) 对内同样适用：模糊时不应反问"哪个对"，应给候选 + 验证条件让数据收敛。本 ADR 把 4 个方向连同各自的 dogfood 触发条件（§备选方案 中 "dogfood 验证条件" 子项）一次性写下，正是把"该选哪个"从问答题降级为选择题。

### F. 直接按既定路线扩张：被否决

[ADR-034 §adr_revisit_when](inbox-to-memory-promotion-v0.md) 提到的「ADR-036 summary/tags 持久化」「ADR-037 chat→ledger」是 ADR-034 接受当时对未来的预测，不是承诺。当 ADR-034 落地后产品信号反对继续按"再加一个按钮"路线扩张时，应优先尊重当前信号而非历史预测。

更关键：F 方向不解决"按钮太多"的根因，反而把按钮数推到 5–7 个，把"中规中矩"放大成"产品没识别度"。这违反 [PURPOSE.md core_value](../../PURPOSE.md) 的"让用户用最自然的语气说话，agent 负责听懂、推断、执行"——按钮越多说明 agent 越懒得推断，把决策推回给用户。

## 决策

### D1. 暂停所有"再加按钮 / 再加视图"的 ADR 提案

在 §复议触发条件 满足之前：

- 不接受任何在 inbox / chat / memory 视图新增按钮、新增 action、新增视图、新增导航的 ADR 提案
- 不开 [ADR-034 §adr_revisit_when](inbox-to-memory-promotion-v0.md) 中预告的「summary / tags 持久化」「chat→ledger」类 ADR
- 当前 push 到 origin 的 [ADR-034](inbox-to-memory-promotion-v0.md) M3 形态保持不动作为 dogfood 形态

### D2. 主人 dogfood 收集真实使用数据

主人按当前形态使用桌面端 ≥ 7 个自然日，期间记录（建议每日扫一次后端 SQLite）：

- inbox 真实捕获次数（目标：≥ 30 条）
- 每条 inbox 的 action 命中分布：`Summarize / Tags / 📒 Save / 都不点`
- Memory 视图被打开次数与每次停留时长
- 「想 save 但没按」「不知道按哪个」「捕获后忘了处理」的反例样本数
- 「新任务时 agent 真的引用了 Memory 中某条历史记录」的正例样本数（验证 [PURPOSE.md MVP 第一信号](../../PURPOSE.md)）

### D3. 由数据驱动选 4 候选方向之一，开新 ADR supersede 本 ADR

dogfood 期满或触发 §复议触发条件 后：

- 优先在本 ADR §备选方案 列出的 A / B / C / D 中选出胜出方向
- 也允许出现全新第 5 方向（dogfood 数据反向暴露的方向）
- 胜出方向以新 ADR 落地，新 ADR 在 §背景 中必须摘录本次 dogfood 关键数据并解释"为什么是这条"
- 新 ADR `supersedes` 字段指向本 ADR；本 ADR 转 `adr_status: superseded`、`canonical: false`

### D4. 不影响项

本 ADR 仅约束「再加按钮 / 再加视图」类提案。以下工作不受影响：

- bug 修复 / 性能优化 / 测试加固 / 文档更新 / wiki ingest
- 后端能力扩展（如全文检索 / 可观测性 / 导出端点），只要不在主屏增加按钮 / 视图
- 现有按钮的 polish（如文案、配色、错误反馈），只要不增加新按钮
- 桌面端非 UI 工程（如打包、签名、自动更新等基础设施）
- ADR 形态外的轻量实验性原型（不改 origin/main）

## 影响

### 立即生效

1. 任何"加按钮 / 加视图"的代码 PR / ADR 提案在合入前必须先 supersede 本 ADR，或在自己的 §背景 中明确说明 §复议触发条件 中哪条已满足
2. [ADR-034 §adr_revisit_when](inbox-to-memory-promotion-v0.md) 中预告的 ADR-036 / ADR-037（summary 持久化 / chat→ledger）默认延后到 dogfood 结束
3. 本 ADR 进入 [hub agent-os.md](../hubs/agent-os.md) 的「从这里开始」章节，与其它 ADR 同级
4. 后续 wiki query 涉及"下一步加什么 UI 功能"时，应先引用本 ADR 而非按时间顺序按 ADR-034 路径预测

### 解锁

1. 主人获得一段没有"加新功能压力"的 dogfood 时间，可专注于"现有功能够不够用"的真实判断
2. agent 获得一段不写 UI 代码的时间，可投入 wiki ingest / 工程债清理 / 测试加固等不增加按钮的工作（[D4](#d4-不影响项)）
3. 4 候选方向（A / B / C / D）的取舍变成"等数据"而不是"等灵感"，把判断从问答题降级为选择题（[ADR-002](options-over-elaboration.md)）

### 风险

1. **dogfood 失能风险**：主人 dogfood 期间忙于其它工作，7 天内未达成 ≥ 30 条捕获目标 — 缓解：复议触发条件包含"主人主动表达倾向"分支，允许在未达数据指标但有清晰倾向时提前复议
2. **被外部需求强行打断**：外部紧急产品需求出现 — 缓解：[D4 不影响项](#d4-不影响项)允许非 UI 加按钮工作；真正的紧急产品需求按 §复议触发条件 第三条走（先开新 ADR 评估，不直接 supersede 本 ADR）
3. **错过黄金"加按钮"窗口期风险**：被否决，因为本 ADR §决策 已论证：盲加按钮的代价（错误诊断 + 错误重写）远高于 dogfood 7 天的延迟代价

## 复议触发条件

参见 front matter `adr_revisit_when` 字段，正文不重复（[SCHEMA §6.0 / §6.6](../../SCHEMA.md)）。

## 相关页面

- [ADR-002 选择题优先于问答题](options-over-elaboration.md)（本 ADR 在内部决策上的应用）
- [ADR-005 MVP 主场景：信息整理](mvp-main-scenario-information-curation.md)（本 ADR 的产品场景前提）
- [ADR-014 MVP 第一切片：chat + inbox 读侧](mvp-first-slice-chat-inbox-read-side.md)（当前桌面形态来源）
- [ADR-001 管家定位与主客体边界](butler-positioning-and-subject-object-boundary.md)（产品哲学根基）
- [ADR-003 长期记忆是核心而非可选模块](long-term-memory-as-core-capability.md)（C 方向 Memory-first 的契约依据）
- [ADR-011 显式 Memory Ledger MVP](explicit-memory-ledger-mvp.md)（C 方向 Memory-first 的契约依据）
- [ADR-026 Inbox V0 数据契约与捕获面](inbox-v0-capture-and-list-contract.md)（B 方向统一收件箱的数据模型起点）
- [ADR-030 Inbox 单条总结 V0](inbox-item-summarize-v0.md)（被本 ADR 暂停扩张的按钮之一）
- [ADR-031 Inbox 单条打标签 V0](inbox-item-tagging-v0.md)（被本 ADR 暂停扩张的按钮之一）
- [ADR-032 Chat V0：分屏 + SSE + 持久化](chat-v0-streaming-and-persistence.md)（B 方向统一收件箱的合并对象）
- [ADR-033 Memory Ledger V0](memory-ledger-v0-schema-and-storage.md)（C 方向 Memory-first 的视图基础）
- [ADR-034 Inbox 显式沉淀进 Memory Ledger V0](inbox-to-memory-promotion-v0.md)（直接前置 ADR；本 ADR 暂停其 adr_revisit_when 中预告的 ADR-036 / ADR-037）
- [ADR-027 桌面渲染端 V0：原生 HTML + IPC 桥](desktop-renderer-v0-native-html-and-ipc-bridge.md)（当前 UI 实现层）
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)（本 ADR 是该 rule 的产物）
