---
title: ADR-032 Chat V0：分屏 UI + SSE 流式 + SQLite 持久化 + ChatSession 聚合 + 内置 system prompt
type: adr
subtype: architecture
canonical: true
summary: V0 chat 切片采用桌面分屏布局（Chat 65% / Inbox 35%）、SSE 流式响应、SQLite 持久化（migration v3 新增 chat_sessions / chat_messages 两表）、ChatSession 聚合根 + ChatMessage 实体（UUIDv7 主键）、内置硬编码 system prompt（用户不可见、不可改、不入库，每次拼接）；扩展 ILlmProvider 抽象增加 CompleteStreamAsync(IAsyncEnumerable<LlmStreamChunk>)，三家 provider（OpenAI/DeepSeek/AzureOpenAI）全部实现；新增 IChatAppService facade（CreateSession / ListSessions / ListMessages / SendMessageStream），新增 4 个 API 端点（GET /api/chat/sessions、POST /api/chat/sessions、GET /api/chat/sessions/{id}/messages、POST /api/chat/sessions/{id}/messages 走 SSE）；桌面端渲染层重做为左 65% Chat（含会话 mini-sidebar + 消息流 + 输入框）+ 右 35% Inbox 的双栏布局，preload bridge 新增 chat.* 方法，Electron 主进程订阅 SSE 后通过 webContents.send 转发流式片段；自动建会话由 renderer 负责（首次进入若无会话则触发 POST）；新错误码 chat.sessionNotFound 映射 HTTP 404；不引入 tool calling / RAG / 多模型路由 / 提示词配置化 / 用户可编辑 system prompt / 多会话并发 / Memory Ledger 集成 / 消息编辑 / 消息删除 / 跨会话搜索。
tags: [agent, engineering, llm, interaction-design]
sources: []
created: 2026-05-06
updated: 2026-05-06
verified_at: 2026-05-06
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/llm-provider-v0-openai-deepseek-abstraction.md, pages/adrs/llm-provider-azure-openai-extension.md, pages/adrs/desktop-renderer-v0-native-html-and-ipc-bridge.md, pages/adrs/desktop-process-supervisor-electron-dotnet-child.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/adrs/sqlite-dapper-bootstrap-and-schema-init.md, pages/adrs/backend-architecture-equinox-reference.md, pages/adrs/no-mediator-self-domain-event-dispatcher.md, pages/adrs/butler-positioning-and-subject-object-boundary.md, pages/adrs/options-over-elaboration.md, pages/adrs/objective-drafting-style.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-06
adr_revisit_when: "用户反馈 V0 system prompt 文案需要可调（迫使把 prompt 配置化或暴露给用户）；renderer 出现单会话消息上百条后的性能问题（迫使分页 / 虚拟滚动 / 截断历史）；用户开始抱怨"管家定位漂移"或"答非所问"（说明 system prompt 太弱或被 LLM 默认对话姿态压过）；流式 SSE 在某 provider 上稳定性不足（迫使加重连 / 心跳 / 切回 once-shot）；产品需要在 chat 上做 tool calling / RAG / Memory Ledger 检索注入（迫使 IChatAppService 引入工具上下文与多步骤循环）；多会话并发场景出现（user 想同时跑两个对话）；产品需要消息编辑 / 删除 / 重新生成（迫使 chat_messages 表加 status 字段或 soft-delete）；跨会话语义搜索需求出现（迫使引入向量索引）；用户开始抱怨"会话列表太长，找不到旧对话"（迫使加搜索 / 收藏 / 文件夹）。"
---

# ADR-032 Chat V0：分屏 UI + SSE 流式 + SQLite 持久化 + ChatSession 聚合 + 内置 system prompt

> V0 chat 切片：桌面分屏（Chat 65% / Inbox 35%）+ SSE 流式 + SQLite 持久化（migration v3）+ `ChatSession` 聚合 + 硬编码 system prompt（不可见、不可改、不入库）；扩展 `ILlmProvider.CompleteStreamAsync`；新增 4 个 API 端点；renderer 重做为左 65% Chat / 右 35% Inbox；自动建会话由 renderer 负责；新错误码 `chat.sessionNotFound` → 404。

## 背景

[ADR-014 第一版切片](mvp-first-slice-chat-inbox-read-side.md) 把 MVP 第一版界面明确写为「聊天窗口 + agent inbox」。当前已落地的是后半段（[ADR-026](inbox-v0-capture-and-list-contract.md) 数据契约 / [ADR-027](desktop-renderer-v0-native-html-and-ipc-bridge.md) 渲染端 / [ADR-030](inbox-item-summarize-v0.md) 总结 / [ADR-031](inbox-item-tagging-v0.md) 打标签）；前半段「聊天窗口」尚未开工。

ADR-014 的关键约束限定了 chat 第一版的形态：

- **不引入工具调用 / RAG**（动作范围方案 G：「只做总结、分类、打标签、生成候选整理方案」）
- **不读取用户文件夹**（[ADR-012](mvp-input-boundary-no-default-folder-reading.md)）
- **Memory Ledger 暂不集成进 chat**（[ADR-011](explicit-memory-ledger-mvp.md) 单独路径）
- **chat 与 inbox 同时存在**（不二选一）

[PURPOSE.md `core_value`](../../PURPOSE.md) 进一步约束："让用户用最自然的语气说话 …… 用户不需要会写 prompt"。这句话不是说"不要 system prompt"，而是说"产品自己负责把 system prompt 写好并内置"——用户拿到的就是已经具备管家定位的 agent。

V0 必须同时回答以下八个工程问题：

1. UI 形态：chat 与 inbox 如何共存？
2. 流式：是否上 SSE 流式？
3. 持久化：会话历史是否持久化？
4. 聚合形态：chat 的 Domain 模型是什么？
5. system prompt 策略：是否内置？是否可见 / 可改？
6. `ILlmProvider` 是否需要扩展？
7. 端点形态：chat 的 HTTP 表面是什么？
8. 自动建会话：由谁负责？

本 ADR 一次性把这八个问题定下来；落地按 4 个 commit 切分（见 §实施概要 §M1）。

用户已在方案先行讨论中明确选择：分屏（左 chat / 右 inbox）、Chat 占 65%、SSE 流式、SQLite 持久化、自动建会话、旧会话列表、内置 system prompt。

## 备选方案

**A. UI 形态**：

- 方案 A1：分屏（左 Chat / 右 Inbox），Chat 65% / Inbox 35%
- 方案 A2：分屏（左 Inbox / 右 Chat），Inbox 35% / Chat 65%
- 方案 A3：顶部 tab 切换（Chat / Inbox 二选一显示）
- 方案 A4：左侧 sidebar 导航 + 主区域切换
- 方案 A5：50/50 等分

**B. 流式**：

- 方案 B1：V0 直接上 SSE 流式
- 方案 B2：V0 纯 once-shot（一次返回完整回复），流式留后续
- 方案 B3：WebSocket 双向流

**C. 持久化**：

- 方案 C1：V0 完全不存（关闭即丢，刻意把 ChatSession 聚合形态留给后续 ADR）
- 方案 C2：进程内 in-memory 单会话（重启丢失）
- 方案 C3：SQLite 持久化（migration v3 新增 chat_sessions + chat_messages 两表）

**D. 聚合形态**：

- 方案 D1：Domain 引入 `ChatSession` 聚合根（含 `Messages` 列表）+ `ChatMessage` 实体；UUIDv7 主键；与 [ADR-026](inbox-v0-capture-and-list-contract.md) 同形态
- 方案 D2：`ChatMessage` 是聚合根，`SessionId` 仅作 FK（无 `ChatSession` 聚合）
- 方案 D3：Application 层用 record 表达，不落 Domain

**E. system prompt 策略**：

- 方案 E1：内置硬编码，用户不可见、不可改，不入库（每次 Application 层拼接）
- 方案 E2：内置硬编码 + 把 system prompt 也写进 chat_messages 表（role=system）
- 方案 E3：放到 `appsettings.Llm:Prompts:Chat`（配置可改，仍不暴露给用户）
- 方案 E4：用户可见可改（提供"自定义 instructions"输入框）
- 方案 E5：完全裸 chat，不带 system prompt

**F. `ILlmProvider` 扩展**：

- 方案 F1：抽象增加 `CompleteStreamAsync(LlmRequest, CancellationToken) → IAsyncEnumerable<LlmStreamChunk>`，三家 provider 全部实现
- 方案 F2：新建独立接口 `IStreamingLlmProvider`，与 `ILlmProvider` 并列
- 方案 F3：在 `ILlmProvider` 加 `Stream` bool 参数，统一通过 `CompleteAsync` 返回（流式时 `Content` 为 `IAsyncEnumerable<string>`）

**G. 端点形态**：

- 方案 G1：4 个 RESTful 端点
  - `GET /api/chat/sessions`
  - `POST /api/chat/sessions`
  - `GET /api/chat/sessions/{id}/messages`
  - `POST /api/chat/sessions/{id}/messages`（SSE）
- 方案 G2：单 RPC 端点 `POST /api/chat/exchange`，请求体含 `sessionId`（首次为空则后端建）+ `userMessage`
- 方案 G3：WebSocket 单端点 `/api/chat/ws`

**H. SSE 流式协议形态**：

- 方案 H1：每个 chunk 一个 SSE event：`event: chunk\ndata: {"delta": "...", "model": "..."}`，最后一帧 `event: done\ndata: {"messageId": "...", "promptTokens": ..., "completionTokens": ..., "durationMs": ...}`，错误用 `event: error\ndata: {"code": "...", "message": "..."}`
- 方案 H2：仅用默认 event 类型（`data:` 行），用 JSON 内字段区分 chunk / done / error
- 方案 H3：直接转发 OpenAI 原生 SSE 格式（`data: {"choices": [...]}`）

**I. 自动建会话**：

- 方案 I1：renderer 负责。首次进入页面 GET sessions，若空则 POST 建一个，再用其 id 作为当前会话
- 方案 I2：后端在第一次 `POST /api/chat/sessions/{id}/messages` 时若 `id=auto` 自动建
- 方案 I3：`POST /api/chat/sessions/{id}/messages` 端点若 `id` 不存在直接报 404，要求客户端先建

**J. system prompt 文案**：

- 方案 J1：100 字内中文（管家定位 + 客体边界 + 选择题优先 + 客观代笔语气 4 条产品红线浓缩）
- 方案 J2：>200 字详尽版（每条产品红线展开）
- 方案 J3：英文版（按 LLM 训练分布更稳）

## 被否决方案与理由

**方案 A2（左 Inbox / 右 Chat）**：

- 阅读重心反过来——用户主要在 chat 里说话，inbox 是参考；视觉重心应跟主交互对齐
- 中文阅读习惯左到右，主区域放左符合 F-pattern

**方案 A3（tab 切换）**：

- ADR-014 的设计意图是 chat 与 inbox **同时可见**；tab 切换违背"两个一起在场"
- 用户在 chat 里讨论某条 inbox item 时需要不停切 tab
- 切 tab 也丢失 chat 的"焦点输入框"位置

**方案 A4（sidebar + 主区域切换）**：

- 与 A3 同类问题——主区域只显示一个
- V0 没有那么多次级页面，不需要左侧导航

**方案 A5（50/50 等分）**：

- chat 是主交互（每次输入 / 流式渲染都在这），inbox 是参考材料库；视觉重心应跟主交互对齐
- 50/50 让两边都不够大，输入框 + 消息列表在 50% 宽度下偏挤

**方案 B2（纯 once-shot）**：

- LLM 调用动辄 2-5 秒，没有流式时用户面对"转圈圈"等到结果一次性出现，体验差距明显
- 实施成本：流式版只比 once-shot 多 ~30%（`ILlmProvider` 加一个方法 + provider 三处实现 + endpoint 一处 SSE writer），但 UX 改善是数量级
- "V0 留后续"在 chat 这种 hot path 上拖延的代价比 inbox 单条总结大

**方案 B3（WebSocket 双向流）**：

- 单向流就够（client 发完 user message 后就只接收）；WebSocket 引入握手 / 心跳 / 重连 / 子协议复杂度
- ASP.NET Core minimal API 的 SSE 路径比 WebSocket 简单；Electron 主进程订阅 SSE 也比订阅 WebSocket 自然

**方案 C1（完全不存）**：

- 关闭 app 后 chat 历史全部丢失，连"昨天问过的问题"都查不到
- 与 [ADR-014](mvp-first-slice-chat-inbox-read-side.md) "MVP 必须沉淀长期记忆"的产品意图相悖
- 用户体感差距巨大——不持久化的 chat 不是产品，只是 demo

**方案 C2（进程内 in-memory）**：

- 进程重启丢失同样违反产品意图
- 实施复杂度（DI 单例 + 线程安全集合）并不比 SQLite 低多少
- 没有任何收益，仅增加"功能 demo"心智

**方案 D2（ChatMessage 是聚合根）**：

- 违反 DDD 聚合不变量——会话标题 / 创建时间 / 消息计数等元数据没有归属
- 列表 chat 时需要"按 SessionId 分组"逻辑，本来 SQL 一次拿就行
- 与 ADR-026 InboxItem 聚合形态对称破坏

**方案 D3（不落 Domain）**：

- chat 是产品核心交互之一，按 [ADR-018](backend-architecture-equinox-reference.md) DDD 分层，核心交互必须有 Domain 表达
- 不落 Domain 会让 Application 直接面对 Repository 与 SQL，拐回 transaction script 心智

**方案 E2（system prompt 写进 chat_messages 表）**：

- system prompt 是产品决策（每次升级 prompt 文案，老会话也应享受新 prompt），不应作为"历史快照"被锁
- 用户看消息列表时不该看到 system 行（要么过滤掉，要么暴露给用户——前者多此一举，后者违反 E1 决策）
- 增加表 schema 复杂度（需要 role 字段且要分 user/assistant/system，加个枚举）；E1 路径 role 只剩 user/assistant 二元

**方案 E3（appsettings 配置）**：

- V0 暴露不出"哪一刀加配置项"的真实需求；过早把 prompt 移进配置等于鼓励"运维侧调 prompt"，但 prompt 是产品决策不是运维决策
- 配置化触发条件已写入 `adr_revisit_when`，待真正出现"prompt 文案需要不重启 / 不发版调整"的信号再做

**方案 E4（用户可见可改）**：

- 直接违反 [PURPOSE.md `core_value`](../../PURPOSE.md)："用户不需要会写 prompt"
- 用户能改 system prompt 等于把 [ADR-001](butler-positioning-and-subject-object-boundary.md) 客体边界、[ADR-002](options-over-elaboration.md) 选择题优先、[ADR-010](objective-drafting-style.md) 客观代笔语气这些产品红线交给用户决定——产品定位失守
- "自定义 instructions"是 ChatGPT / Claude 的形态，本产品差异化恰恰在不让用户写 prompt

**方案 E5（裸 chat 不带 system prompt）**：

- LLM 默认会按 ChatGPT 姿态回应（被动问答 / 不主动给候选 / 模仿用户语气 / 不持守客体边界）
- 用户拿到的是"另一个 ChatGPT"而非"管家"，[ADR-001](butler-positioning-and-subject-object-boundary.md) 失守
- 用户必须自己在第一条 user message 里写 prompt 才能纠回来——直接违反 [PURPOSE.md](../../PURPOSE.md) `core_value`

**方案 F2（独立 IStreamingLlmProvider）**：

- 三家 provider 实现两个接口要么重复构造 HttpClient，要么得引入"基类共享"——同一个 LLM 调用本来就是同一段 HTTP 配置
- 调用方需要同时依赖两个端口：`ILlmProvider` 做 inbox summarize/tag、`IStreamingLlmProvider` 做 chat。心智负担翻倍
- 单 active provider（[ADR-028 §E1](llm-provider-v0-openai-deepseek-abstraction.md)）下两接口必须同时绑定到同一实现，实质就是同一接口——拆分纯属语义噪声

**方案 F3（Stream bool 参数）**：

- 返回类型必须用 union（`object` 或者 `OneOf<...>`）才能兼容 once-shot 与流式，C# 类型系统在这里很难看
- 调用方 if(stream) cast，糟糕的设计
- `IAsyncEnumerable<T>` 是流式正解，与 once-shot 的 `Task<T>` 是不同范畴的方法，应分别声明

**方案 G2（单 RPC 端点）**：

- "首次为空则建" + "已有则继续"是隐式分支，违反 RESTful 资源建模
- 建会话需要可独立调用（renderer 显式 POST 建会话符合 §I1 自动建会话路径）
- 列旧会话需要 GET，单 RPC 不能复用

**方案 G3（WebSocket 单端点）**：

- 与方案 B3 同类问题——单向流不需要 WebSocket
- ASP.NET Core minimal API 的 WebSocket 处理比 SSE 复杂

**方案 H2（仅 data 行）**：

- 客户端必须解析 JSON 内 `type` 字段才能区分 chunk / done / error，逻辑分散
- SSE 标准就是用 `event:` 行做分流；不用就是浪费 spec

**方案 H3（直接转发 OpenAI SSE）**：

- DeepSeek / Azure 的 SSE 帧虽然兼容 OpenAI，但仍可能在边缘字段（如 `usage`、`finish_reason`）不一致
- 后端的 `ILlmProvider` 已经把 provider 差异隐藏在 `LlmCompletion`/`LlmStreamChunk`；端点不应再把这层抽象戳穿
- 客户端绑死 OpenAI 帧格式 = 后端换 provider 时 renderer 也要改

**方案 I2 / I3（后端魔法或客户端硬错）**：

- I2 在端点里塞"id=auto 时建"是隐式行为，违反 REST
- I3 把"首次进入"的复杂度全压给 renderer，但 renderer 反正要做（"GET sessions → 空 → POST 建"逻辑必然存在），不如直接在 I1 里说清楚

**方案 J2（>200 字详尽版）**：

- 每个 user message 都要带这一段，token 成本累加（200 字 ≈ 300 tokens × N 轮），费用没必要
- 详尽版会让 LLM 产生"我必须每次都按 4 条规则"的过拟合姿态，回复反而更刻板
- V0 先用 100 字打底，等 LLM 行为漂移再加细节

**方案 J3（英文版）**：

- 用户输入是中文，LLM 用中文 system prompt 输出更自然
- 英文 prompt 在国产模型上反而会让回复带英文残留

## 决策

### A1：UI 分屏（左 Chat 65% / 右 Inbox 35%）

桌面渲染端从当前的"单页 Inbox"改为左右分屏：

- **左栏 Chat 65%**（主区域）：内嵌纵向布局
  - 顶部 mini-sidebar（横向 tab bar 形态，可滚动）：列旧会话标题，"+ 新会话"按钮在末尾
  - 中部消息流区（vertical scroll，autoscroll-to-bottom）
  - 底部输入框 + 发送按钮（多行 textarea，Cmd/Ctrl+Enter 发送）
- **右栏 Inbox 35%**（参考区）：当前 Inbox V0 视图原封不动迁过来
- **分隔条**：1px 静态边线，V0 不做拖拽调整

布局用 CSS Grid `grid-template-columns: 65fr 35fr`；最小宽度约束 chat 不小于 480px、inbox 不小于 320px。

### A2：会话 mini-sidebar 形态（V0）

- 横向 tab bar 而非垂直 sidebar——避免再消耗 chat 主区域横向空间
- 当前会话高亮（背景色区分）
- 旧会话以 `title` 显示；title 由 LLM 在第一轮回复后异步生成（V0 先简化：用 user 第一条 message 的前 24 字符；LLM 自动命名留 ADR-033+）
- "+ 新会话" 按钮始终在末尾
- 不做删除 / 重命名（V0 不做这件事；触发条件见 `adr_revisit_when`）

### B1：SSE 流式

`POST /api/chat/sessions/{id}/messages` 走 Server-Sent Events：

- `Content-Type: text/event-stream; charset=utf-8`
- `Cache-Control: no-cache`
- `X-Accel-Buffering: no`（禁止反向代理缓冲；V0 桌面 localhost 无代理但显式声明）

`ILlmProvider.CompleteStreamAsync` 返回 `IAsyncEnumerable<LlmStreamChunk>`；endpoint 通过 `await foreach` 逐 chunk 写入响应流，并在最后写一帧 `done` 事件携带元数据。

### C1：SQLite 持久化（migration v3）

新增 schema migration v3：

```sql
CREATE TABLE chat_sessions (
    id              TEXT PRIMARY KEY,                  -- UUIDv7
    title           TEXT NOT NULL,
    created_at      TEXT NOT NULL,                     -- ISO 8601 UTC
    updated_at      TEXT NOT NULL                      -- ISO 8601 UTC，最后一条消息时间
);

CREATE INDEX idx_chat_sessions_updated_at ON chat_sessions(updated_at DESC);

CREATE TABLE chat_messages (
    id              TEXT PRIMARY KEY,                  -- UUIDv7
    session_id      TEXT NOT NULL REFERENCES chat_sessions(id) ON DELETE CASCADE,
    role            TEXT NOT NULL CHECK (role IN ('user', 'assistant')),
    content         TEXT NOT NULL,
    created_at      TEXT NOT NULL,                     -- ISO 8601 UTC
    model           TEXT,                              -- assistant 消息记录使用的模型 ID；user 消息为 NULL
    prompt_tokens   INTEGER,                           -- assistant 消息记录；user 消息为 NULL
    completion_tokens INTEGER                          -- 同上
);

CREATE INDEX idx_chat_messages_session_created ON chat_messages(session_id, created_at);
```

字段说明：

- **`role`** 只允许 `user` / `assistant`：system prompt 不入库（决策 §E1）
- **UUIDv7 主键**：与 ADR-026 一致；时间有序，索引 friendly
- **`title`** 非空：建会话时由 server 用占位文案（"新会话"）兜底，待第一条 user message 完成后由 server 截取前 24 字符 set 进去
- **token 字段**仅在 assistant 消息上有值：来自 `LlmCompletion.PromptTokens` / `CompletionTokens`；user 消息为 NULL（语义清晰）

migration 落进 `Infrastructure/Persistence/Migrations` 现有骨架（[ADR-024](sqlite-dapper-bootstrap-and-schema-init.md) §C），加 `V3_CreateChatTables.cs`，`SchemaInitializer` 自动按版本号顺序应用。

### D1：Domain 聚合形态

```csharp
// Domain/Chat/ChatSession.cs
public sealed class ChatSession
{
    public Guid Id { get; }
    public string Title { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // 工厂方法：建会话用占位 title
    public static ChatSession Create(IClock clock);

    // 用户首条 message 完成后由 AppService 调用，覆盖 title
    public void SetTitleFromFirstMessage(string firstUserContent, IClock clock);

    // 每次新增 message 时由 AppService 调用，刷新 updated_at
    public void Touch(IClock clock);
}

// Domain/Chat/ChatMessage.cs
public sealed class ChatMessage
{
    public Guid Id { get; }
    public Guid SessionId { get; }
    public ChatRole Role { get; }
    public string Content { get; }
    public DateTimeOffset CreatedAt { get; }
    public string? Model { get; }
    public int? PromptTokens { get; }
    public int? CompletionTokens { get; }

    public static ChatMessage CreateUser(Guid sessionId, string content, IClock clock);
    public static ChatMessage CreateAssistant(
        Guid sessionId,
        string content,
        string model,
        int? promptTokens,
        int? completionTokens,
        IClock clock);
}

// Domain/Chat/ChatRole.cs
public enum ChatRole
{
    User,
    Assistant,
}
```

不做：

- 不在 `ChatSession` 上做 `Messages: IReadOnlyList<ChatMessage>`——V0 不让聚合根加载完整 history；Repository 提供 `LoadMessagesAsync(sessionId)` 单独查
- 不引入 `System` role——system prompt 不进 Domain
- 不做软删除 / 状态字段——`adr_revisit_when` 列了消息编辑 / 删除触发条件

### D2：Repository 形态

```csharp
// Domain/Chat/IChatSessionRepository.cs
public interface IChatSessionRepository
{
    Task<ChatSession?> GetAsync(Guid sessionId, CancellationToken ct);
    Task<IReadOnlyList<ChatSession>> ListAsync(int limit, int offset, CancellationToken ct);
    Task<IReadOnlyList<ChatMessage>> LoadMessagesAsync(Guid sessionId, CancellationToken ct);
    Task AddAsync(ChatSession session, CancellationToken ct);
    Task UpdateAsync(ChatSession session, CancellationToken ct);  // 用于 title / updated_at 刷新
    Task AddMessageAsync(ChatMessage message, CancellationToken ct);
}
```

实现 `Infrastructure/Persistence/Chat/ChatSessionRepository.cs`：Dapper + 现有 `IConnectionFactory`（[ADR-024](sqlite-dapper-bootstrap-and-schema-init.md) §B）。

`ListAsync` 默认 `ORDER BY updated_at DESC LIMIT ? OFFSET ?`；V0 端点参数 limit=50/offset=0（与 inbox 一致），renderer 不做分页 UI（一屏 mini-sidebar 横向滚动够用）。

### E1：内置 system prompt（不可见、不可改、不入库）

system prompt 由 Application 层在每次发起 LLM 调用时拼接到 `LlmRequest.Messages` 头部，**不**写入 `chat_messages` 表，**不**通过 API 返回给客户端。

文案（V0 写死在 `ChatAppService` 常量；中文，约 100 字）：

```
你是 dawning-agent-os，主人的个人 AI 管家。
你的角色是客体：理解主人意图、给出候选方案、协助执行；不替主人下结论、不塑造偏好。
当主人表达模糊时，先关联上下文推断，能推断就给 2-4 个候选让主人挑选，不把问题原样还回去。
代笔默认冷静客观，不刻意拟人化模仿主人。
```

每次拼接：`[system: <上述文案>] + [user/assistant 历史完整序列] + [当前 user message]`。

**不做**：

- 不让用户编辑（违反 [PURPOSE.md `core_value`](../../PURPOSE.md)）
- 不持久化（升级 prompt 文案时老会话也享受新 prompt）
- 不做按会话定制 / 按场景切换
- 不做"prompt 配置化"（推迟到 `adr_revisit_when` 触发）

### F1：`ILlmProvider` 扩展 `CompleteStreamAsync`

抽象层（`Infrastructure.Llm.Abstractions` 或 `Domain.Llm`，沿用 [ADR-028](llm-provider-v0-openai-deepseek-abstraction.md) §B 现有位置）追加：

```csharp
public interface ILlmProvider
{
    Task<Result<LlmCompletion>> CompleteAsync(LlmRequest request, CancellationToken ct);

    // 新增：流式
    IAsyncEnumerable<LlmStreamChunk> CompleteStreamAsync(
        LlmRequest request,
        CancellationToken ct);
}

public sealed record LlmStreamChunk
{
    public LlmStreamChunkKind Kind { get; init; }       // Delta | Done | Error
    public string? Delta { get; init; }                 // Kind=Delta 时非空
    public string? Model { get; init; }                 // Kind=Done 时非空
    public int? PromptTokens { get; init; }             // Kind=Done 时可能非空
    public int? CompletionTokens { get; init; }
    public TimeSpan? Latency { get; init; }             // Kind=Done 时非空
    public DomainError? Error { get; init; }            // Kind=Error 时非空
}

public enum LlmStreamChunkKind { Delta, Done, Error }
```

三家 provider 全部实现：

- **OpenAI / DeepSeek**：`stream=true` POST，逐行解析 `data: {...}`；`[DONE]` 终止；HTTP 错误用 `Error` chunk 投递
- **AzureOpenAI**：与 OpenAI 同 SSE 帧格式（[ADR-029](llm-provider-azure-openai-extension.md)），但需带 `api-version` query param
- 取消通过 `CancellationToken` 透传给 `HttpClient.GetStreamAsync`；上游 dispose 即终止流

错误处理：

- 流式没法用 `Result<T>` 包整体（流可能 mid-stream 失败）；改用 `LlmStreamChunkKind.Error` 投递
- HTTP 4xx/5xx / 网络错误 / JSON 解析失败 → 统一映射为 `Error` chunk，含 [ADR-028 §H1](llm-provider-v0-openai-deepseek-abstraction.md) 的错误码
- 流正常结束且 LLM 返回成功 → 投递 `Done` chunk 携带元数据
- 流被 client 主动中断（`ClientDisconnected`）→ 不投递任何 chunk，直接退出（消费者已经走了）

### G1：4 个 RESTful 端点

| 方法 | 路径 | 作用 | 鉴权 |
| --- | --- | --- | --- |
| `GET` | `/api/chat/sessions?limit=50&offset=0` | 列会话（按 updated_at 倒序） | StartupToken |
| `POST` | `/api/chat/sessions` | 建空会话（请求体空，返回 ChatSessionDto） | StartupToken |
| `GET` | `/api/chat/sessions/{id:guid}/messages` | 列消息（按 created_at 升序，全量返回） | StartupToken |
| `POST` | `/api/chat/sessions/{id:guid}/messages` | 发用户消息（SSE 流式返回 assistant 回复） | StartupToken |

DTO：

```csharp
public sealed record ChatSessionDto(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ChatMessageDto(
    Guid Id,
    Guid SessionId,
    string Role,                                // "user" / "assistant"
    string Content,
    DateTimeOffset CreatedAt,
    string? Model,
    int? PromptTokens,
    int? CompletionTokens);

public sealed record CreateChatSessionResponse(ChatSessionDto Session);

public sealed record SendMessageRequest(string Content);

// SSE 流式，无 JSON response 模型；按 §H1 协议
```

V0 不分页消息列表（产品上单会话长度有限；超出后再开 ADR）。

### H1：SSE 协议形态

```
event: chunk
data: {"delta": "你好"}

event: chunk
data: {"delta": "，"}

...

event: done
data: {"messageId": "01976e...", "model": "gpt-4.1-2025-04-14", "promptTokens": 156, "completionTokens": 28, "durationMs": 980}

```

错误：

```
event: error
data: {"code": "llm.upstreamUnavailable", "message": "Provider returned 502"}

```

约束：

- `event` 行先于 `data` 行
- 每个 SSE 帧用空行分隔（标准）
- `data` 始终是单行 JSON（不跨行）
- 不发 `id:` 字段（不需要 Last-Event-ID 重连）
- 不发心跳（V0 LLM 调用通常 < 30 秒，不需要 keep-alive）

server 用 `IAsyncEnumerable<LlmStreamChunk>` 逐 chunk 写入：

```csharp
context.Response.Headers.ContentType = "text/event-stream; charset=utf-8";
context.Response.Headers.CacheControl = "no-cache";
context.Response.Headers["X-Accel-Buffering"] = "no";

await foreach (var chunk in chatStream.WithCancellation(ct))
{
    if (chunk.Kind == LlmStreamChunkKind.Delta)
        await WriteSseAsync("chunk", new { delta = chunk.Delta });
    else if (chunk.Kind == LlmStreamChunkKind.Done)
        await WriteSseAsync("done", new { messageId, model = chunk.Model, ... });
    else if (chunk.Kind == LlmStreamChunkKind.Error)
        await WriteSseAsync("error", new { code = chunk.Error.Code, message = chunk.Error.Message });
}
```

server 在收到 `Done` 后才把 assistant 消息插入 `chat_messages` 表（避免半成品落库）；mid-stream 错误时保留 user 消息但不写 assistant。

### I1：renderer 负责自动建会话

renderer 启动后调用 `chat.list()` → 若 `sessions.length === 0` 则 `chat.create()` → 用其 id 作 currentSessionId；否则用最近一条（`updated_at` 倒序首项）作 currentSessionId。

renderer 也提供 "+ 新会话" 按钮，点击 → `chat.create()` → 切到新会话。

不在后端做"自动建"——后端端点保持纯 RESTful（资源不存在就 404）。

### J1：system prompt 文案 V0 版

见 §E1 决策；约 100 字中文。

### K1：错误模型

复用 [ADR-028 §H1](llm-provider-v0-openai-deepseek-abstraction.md) / [ADR-030 §F1](inbox-item-summarize-v0.md) / [ADR-031 §F1](inbox-item-tagging-v0.md) 的错误码体系，**新增一条**：

| 场景 | 错误码 | HTTP 状态 |
| --- | --- | --- |
| chat session 不存在 | `chat.sessionNotFound` | 404 |
| LLM 401/403 | `llm.authenticationFailed` | 401 |
| LLM 429 | `llm.rateLimited` | 429 |
| LLM 408/5xx | `llm.upstreamUnavailable` | 502 |
| LLM 其它 4xx | `llm.invalidRequest` | 400 |
| LLM HttpRequestException | `llm.upstreamUnavailable` | 502 |
| `OperationCanceledException` | 透传，不映射 | — |

`ChatErrors` 静态类（新建 `Application/Chat/ChatErrors.cs`）：

```csharp
public static class ChatErrors
{
    public const string SessionNotFoundCode = "chat.sessionNotFound";

    public static DomainError SessionNotFound(Guid sessionId) =>
        new(Code: SessionNotFoundCode, Message: $"Chat session '{sessionId}' not found.", Field: null);
}
```

非流式端点（GET sessions / POST sessions / GET messages）走标准 `Result<T>.ToHttpResult` 路径；流式端点走 §H1 SSE error event。

### L1：DI 注册

沿用 `Application` 项目的反射扫描——`ChatAppService` 命名匹配自动 Scoped 注册（[ADR-022 §G](no-mediator-self-domain-event-dispatcher.md)）。

`Infrastructure.Persistence.Chat.ChatSessionRepository` 显式 `services.AddScoped<IChatSessionRepository, ChatSessionRepository>()`（与 `InboxRepository` 同形态）。

V3 migration 在 `SchemaInitializer` 现有版本表自动应用，无需手工注册。

### M1：测试覆盖

V0 必须验证（合计 ~30 个新测试）：

1. **Domain Tests**（`Domain.Tests/Chat/ChatSessionTests`、`ChatMessageTests`）：
   - `Create_AssignsUuidV7AndPlaceholderTitle`
   - `SetTitleFromFirstMessage_TruncatesTo24Chars`
   - `Touch_AdvancesUpdatedAt`
   - `ChatMessage.CreateUser_RejectsEmptyContent`
   - `ChatMessage.CreateAssistant_RecordsTokensAndModel`

2. **Application Tests**（`Application.Tests/Chat/ChatAppServiceTests`，mock `IChatSessionRepository` + `ILlmProvider` + `IClock`）：
   - `CreateSessionAsync_PersistsAndReturnsSession`
   - `ListSessionsAsync_ReturnsByUpdatedAtDesc`
   - `ListMessagesAsync_ReturnsSessionNotFound_WhenMissing`
   - `ListMessagesAsync_ReturnsByCreatedAtAsc`
   - `SendMessageStreamAsync_PrependsSystemPrompt`（关键：验证 system prompt 实际被注入）
   - `SendMessageStreamAsync_PassesFullHistory`（验证多轮上下文累积）
   - `SendMessageStreamAsync_PersistsUserMessageBeforeStreamStarts`
   - `SendMessageStreamAsync_PersistsAssistantMessageAfterDoneChunk`
   - `SendMessageStreamAsync_DoesNotPersistAssistant_WhenStreamErrors`
   - `SendMessageStreamAsync_SetsTitle_OnFirstUserMessage`
   - `SendMessageStreamAsync_DoesNotResetTitle_OnSubsequentMessages`
   - `SendMessageStreamAsync_TouchesSessionUpdatedAt`
   - `SendMessageStreamAsync_PropagatesCancellation`

3. **Api Tests**（`Api.Tests/Endpoints/ChatEndpointTests`，`WebApplicationFactory<Program>` + mock `IChatAppService`）：
   - GET `/api/chat/sessions` 200 + StartupToken 缺失 401
   - POST `/api/chat/sessions` 201 + Location header
   - GET `/api/chat/sessions/{id}/messages` 200 / 404
   - POST `/api/chat/sessions/{id}/messages` SSE 帧序列断言（chunk → chunk → done）
   - POST `/api/chat/sessions/{id}/messages` 流式中 LLM 错误 → SSE error event
   - POST `/api/chat/sessions/{id}/messages` session 不存在 → 404（在握手阶段，非 SSE）

4. **Architecture Tests**：
   - `Domain_Chat_DoesNotReferenceApplication`
   - `Application_Chat_DoesNotReferenceInfrastructure`
   - `Infrastructure_ChatRepository_OnlyAccessedViaIChatSessionRepository`

5. **Infrastructure 集成测试**（`Infrastructure.Tests/Persistence/Chat/ChatSessionRepositoryTests`，临时 SQLite）：
   - `AddAsync_AndGetAsync_RoundTrips`
   - `LoadMessagesAsync_ReturnsByCreatedAtAsc`
   - `ListAsync_RespectsLimitOffset`
   - `Migration_V3_CreatesTablesAndIndexes`

## 实施概要

按 4 个 commit 切分（[Rule 实现前必须方案先行](../rules/plan-first-implementation.md) §commit-shape 一致）：

### Commit 1 — `docs(adr): add ADR-032 chat V0`

- 新增本文件 `docs/pages/adrs/chat-v0-streaming-and-persistence.md`
- 更新 `docs/pages/hubs/agent-os.md` 加 ADR-032 链接

### Commit 2 — `feat(domain+app+infra): chat V0 backend per ADR-032`

- **Domain（新增）**：
  - `src/Dawning.AgentOS.Domain/Chat/ChatSession.cs`
  - `src/Dawning.AgentOS.Domain/Chat/ChatMessage.cs`
  - `src/Dawning.AgentOS.Domain/Chat/ChatRole.cs`
  - `src/Dawning.AgentOS.Domain/Chat/IChatSessionRepository.cs`
- **Application（新增）**：
  - `src/Dawning.AgentOS.Application/Chat/ChatErrors.cs`
  - `src/Dawning.AgentOS.Application/Chat/ChatSessionDto.cs`
  - `src/Dawning.AgentOS.Application/Chat/ChatMessageDto.cs`
  - `src/Dawning.AgentOS.Application/Chat/Abstractions/IChatAppService.cs`
  - `src/Dawning.AgentOS.Application/Chat/ChatAppService.cs`（含 system prompt 常量）
- **Infrastructure（新增/扩展）**：
  - `src/Dawning.AgentOS.Infrastructure/Persistence/Migrations/V3_CreateChatTables.cs`
  - `src/Dawning.AgentOS.Infrastructure/Persistence/Chat/ChatSessionRepository.cs`
  - 三家 provider 加 `CompleteStreamAsync`：`OpenAiLlmProvider`、`DeepSeekLlmProvider`、`AzureOpenAiLlmProvider`
  - `LlmStreamChunk` / `LlmStreamChunkKind` 加进抽象层
- **Tests**：Domain Tests + Application Tests + Infrastructure 集成测试 + Architecture Tests（合计 ~25 个）
- 构建 + `dotnet test` 全绿

### Commit 3 — `feat(api): chat V0 endpoints per ADR-032 §G`

- `src/Dawning.AgentOS.Api/Endpoints/Chat/ChatEndpoints.cs`（4 个端点）
- `src/Dawning.AgentOS.Api/Endpoints/Chat/SseStreamWriter.cs`（SSE 帧写入封装）
- 注册到 `Program.cs` 的端点 mapping
- Api 测试（5 个）
- 构建 + `dotnet test` 全绿
- 用 `curl --no-buffer` 实际打一遍流式端点验证

### Commit 4 — `feat(desktop): chat V0 split-panel UI per ADR-032 §A1`

- `apps/desktop/src/preload/index.ts`：bridge 加 `chat.list / chat.create / chat.listMessages / chat.streamMessage`；流式 chunk 通过 `webContents.send('chat:chunk', ...)` 转发，renderer 用 `chat.onChunk(callback)` 监听
- `apps/desktop/src/main.ts`：
  - 新增 `subscribeToChatStream(sessionId, content, ipcChannel)` 函数：用 `fetch` + `ReadableStream` 订阅 SSE，解析 event/data 帧后逐条 `webContents.send`
  - IPC handlers `chat:list / chat:create / chat:listMessages / chat:sendMessage`（后者触发 SSE 订阅）
- `apps/desktop/src/renderer/index.html`：左 65% / 右 35% 分屏布局；左侧 chat（mini-sidebar + 消息流 + 输入框），右侧 inbox（保留现有视图）
- `apps/desktop/src/renderer/assets/main.js`：新增 chat 模块（消息渲染 / 流式累积 / 输入处理 / 自动滚动 / 自动建会话逻辑）
- `apps/desktop/src/renderer/assets/main.css`：分屏样式 + chat 视觉
- 启动 + 手测：建会话 → 发消息 → 流式渲染 → 重启 app → 历史会话仍在
- 构建 + 启动 e2e 烟测

## 不在本 ADR 范围内

- **Tool calling / function calling** — chat 第一版纯文本对话，不让 LLM 调工具
- **RAG / 知识库注入** — 不把 inbox 内容、Memory Ledger 注入 chat 上下文
- **Memory Ledger 集成** — chat 历史不写记忆账本；记忆账本检索不注入 prompt（[ADR-011](explicit-memory-ledger-mvp.md) 单独路径）
- **多模型路由 / 智能调度** — 全部 chat 走 `[ADR-028 §E1](llm-provider-v0-openai-deepseek-abstraction.md)` 单 active provider；不按场景切模型
- **Prompt 配置化** — system prompt 写死在 `ChatAppService`；不进 appsettings；不暴露给 user
- **用户可编辑 system prompt** — 永远不做（违反 [PURPOSE.md `core_value`](../../PURPOSE.md)）
- **多会话并发** — UI 一次只能在一个会话里打字；后端 V0 不阻止多 client 同时 POST，但产品行为上 single user / single session
- **Streaming 重连 / Last-Event-ID** — 不发 `id:`；流断了用户重新发
- **消息编辑 / 删除 / 重新生成** — `chat_messages` 没有 status / soft-delete 列
- **跨会话搜索** — 不引入全文索引；不建 FTS 表
- **会话删除 / 重命名 / 收藏 / 文件夹** — V0 mini-sidebar 只读；触发条件见 `adr_revisit_when`
- **会话标题自动命名** — V0 用 user 第一条 message 前 24 字符；LLM 自动命名留 ADR-033+
- **Token 上限管理 / 历史截断 / 摘要压缩** — V0 全量历史投喂；超过 LLM context window 时让 provider 报错（用户体感"超长会话报错"，触发 `adr_revisit_when` 中"消息上百条性能问题"信号）
- **导出 / 分享会话** — V0 没有导出按钮
- **多模态（图片 / 文件附件）** — 纯文本
- **语音输入 / 语音播放** — 纯文本
- **代码块语法高亮** — V0 渲染纯 plain text + 换行；不做 markdown 渲染（避免 XSS 担忧 + 实施成本）；触发条件：用户开始抱怨"代码看不清"
- **流式中断（用户点"停止生成"按钮）** — V0 不做停止按钮；流式途中用户切走会话也只关闭 SSE 但保留 user message
- **CORS / 跨域** — 仅供 Electron renderer 通过主进程代理调用，不暴露给浏览器
- **限流 / 速率限制** — V0 单用户单进程；不做 rate limit
- **审计日志** — 不写 chat 调用流水

## 影响

**正向影响**：

- ADR-014 第一版切片"聊天窗口"前半段闭环，距离 MVP 第一版整体闭环只剩"读侧整理候选生成"
- 首次走通 SSE 流式 + `IAsyncEnumerable<LlmStreamChunk>`，未来 inbox summarize / tagging 想升级流式时可复用同一抽象
- 内置 system prompt 把 [ADR-001](butler-positioning-and-subject-object-boundary.md)/[ADR-002](options-over-elaboration.md)/[ADR-010](objective-drafting-style.md) 三条产品红线落到了"代码层强制"
- `ChatSession` 聚合 + UUIDv7 + Dapper Repository 与 `InboxItem` 完全对称，DDD 分层骨架第二次验证
- 流式错误用 `LlmStreamChunkKind.Error` 投递的模式，是非流式 `Result<T>` 的天然补集
- `chat.sessionNotFound` 错误码累积进 `Application/Chat/ChatErrors`，与 `Application/Inbox/InboxErrors` 对称
- migration v3 让 `SchemaInitializer` 真正经历"非首次升级"路径，验证版本表机制按 ADR-024 §C 设计工作

**代价 / 风险**：

- **system prompt 文案过强或过弱** — 100 字版本是初稿，需要 dogfood 才能确认；`adr_revisit_when` 第一条专门为此预留触发
- **流式 SSE 在 Electron 主进程的 fetch + ReadableStream 实现** — Node 18+ 原生支持 fetch & async iterable response，但需要确认 `ReadableStream.getReader()` 对 SSE 帧分割的处理（可能需要手写 line buffer）
- **三家 provider 的 SSE 帧格式细微差异** — 文档号称兼容，实测可能在 `usage` 字段位置 / `[DONE]` 终止时机 / token 计数有出入；测试用 mock，集成验证用真实 provider 跑一遍
- **CASCADE delete on chat_sessions** — 删 session 会连带删 messages；V0 没有删除 UI，但 schema 上保留 CASCADE 是 future-proof
- **全量历史投喂的 token 成本** — 长会话第 N 轮拼接 N×2 条历史，token 累加；V0 不截断（产品上单会话长度有限），超出 context window 时让 provider 报错
- **renderer 流式累积渲染的性能** — 每个 chunk 触发一次 DOM update，长回复（如 500 字）期间会有 hundred-level event 触发；V0 直接 `textContent +=`（不做 RAF batching），假设 V0 LLM 回复体量在 100-300 字之间不会成为瓶颈
- **mini-sidebar 横向滚动 UX** — 旧会话过多时横向 tab 体验弱于垂直 sidebar；触发条件已写入 `adr_revisit_when`
- **`POST /api/chat/sessions/{id}/messages` 半成功语义** — user message 已落库但 assistant 流式失败时，DB 留半截会话；renderer 重试只能再发一次（产生重复 user message 行）。V0 接受此 trade-off（不做 transaction-like 回滚）；触发条件：用户抱怨"看到自己消息却没有回复"
- **system prompt token 成本** — 每轮调用都带 ~150 tokens 的 system prompt，乘以会话长度 N 会累积；GPT-4.1 的 input token 价格已经够低，V0 可接受
- **migration v3 在已有用户数据库上的兼容性** — `SchemaInitializer` 按版本表幂等执行；空库新装与升级老库都安全；本地用户数据库（`%APPDATA%/dawning-agent-os/agentos.db`）首次启动 v0.* 后再启动 v0.* + V3 时自动升级
- **Domain Tests 引入 `IClock` 假对象** — 已有 inbox 用同款；测试期间需要可控时钟，与现有 fixture 复用

## 复议触发条件

- **system prompt 文案需要调** — 见 `adr_revisit_when` 第一条；实测后调文案。如果调一次就稳定，直接改 ADR；如果发现需要按场景 / 时间动态变，开 ADR-033+ 把 prompt 移进配置 / Memory Ledger 注入。
- **renderer 性能** — 单会话消息上百条 + 流式累积渲染卡顿出现时，开 ADR 引入消息分页 / 虚拟滚动 / 历史截断。
- **管家定位漂移** — dogfood 时发现 LLM 仍按 ChatGPT 姿态回应、不主动给候选、过度模仿用户；先调 system prompt 文案；调三次仍漂移则需要更复杂的 prompt 策略（few-shot examples / role-play scenarios），开新 ADR。
- **流式 SSE 稳定性不足** — 某 provider mid-stream 经常断 / token 计数错误 / `usage` 缺失；可能需要加重连 / 心跳，或对该 provider 切回 once-shot；开 ADR 处理 provider-specific stream policy。
- **Tool calling / RAG 触发** — 用户开始希望 agent "查我的 inbox" 或 "记住这个偏好"；引入 tool calling 框架与 Memory Ledger 检索注入，开新 ADR（chat 上下文管线大改）。
- **多会话并发** — 用户想同时开多个对话窗口；renderer 改多 tab + 后端不变（已支持）；触发条件后开 UI ADR。
- **消息编辑 / 删除 / 重新生成** — `chat_messages` 加 status / soft-delete 列；migration v4。
- **跨会话搜索** — 引入 SQLite FTS5 表（`chat_messages_fts`）+ 索引；migration v5；新端点 `GET /api/chat/search`；新 ADR。
- **mini-sidebar 体验不够** — 用户反馈"会话太多找不到旧的"；改 sidebar 形态（垂直 / 搜索框 / 收藏 / 文件夹）；UI ADR。
- **流式中断按钮** — 用户希望"看到生成方向不对，立刻停"；renderer 加"停止"按钮 → IPC abort SSE → server 取消 LLM 调用；开 UI ADR（涉及前后端协议）。
- **多模态触发** — 图片 / 文件附件需求；schema 加 attachments 表 + endpoint 改 multipart；大改。
- **token 上限触发** — 长会话超出 LLM context window；引入历史截断或滚动摘要；开 ADR-033+。

## 相关页面

- [ADR-014 MVP 第一版切片](mvp-first-slice-chat-inbox-read-side.md)：本 ADR 兑现"聊天窗口"前半段。
- [ADR-001 管家定位与主客体边界](butler-positioning-and-subject-object-boundary.md)：内置 system prompt 落地这条产品红线。
- [ADR-002 选择题优先于问答题](options-over-elaboration.md)：内置 system prompt 落地这条产品红线。
- [ADR-010 客观代笔语气](objective-drafting-style.md)：内置 system prompt 落地这条产品红线。
- [ADR-018 后端架构参考 Equinox](backend-architecture-equinox-reference.md)：本 ADR 沿用 DDD 分层（Domain/Application/Infrastructure/Api）。
- [ADR-022 去 MediatR：自研领域事件分发器](no-mediator-self-domain-event-dispatcher.md)：本 ADR 的 `IChatAppService` 是 facade 模式同款。
- [ADR-023 Api 入口立面](api-entry-facade-and-v0-endpoints.md)：本 ADR 4 个端点遵循其形态约定。
- [ADR-024 SQLite/Dapper 通电](sqlite-dapper-bootstrap-and-schema-init.md)：本 ADR 的 migration v3 走其 `SchemaInitializer` 路径。
- [ADR-025 桌面进程监督](desktop-process-supervisor-electron-dotnet-child.md)：本 ADR 流式由 Electron 主进程订阅 SSE 后转发。
- [ADR-026 Inbox V0 数据契约](inbox-v0-capture-and-list-contract.md)：聚合 + UUIDv7 + Dapper Repository 形态对称。
- [ADR-027 桌面渲染端 V0](desktop-renderer-v0-native-html-and-ipc-bridge.md)：本 ADR 把渲染端从单页 inbox 改为分屏 chat + inbox；preload bridge 路径同款。
- [ADR-028 LLM Provider V0](llm-provider-v0-openai-deepseek-abstraction.md)：本 ADR 扩展 `ILlmProvider` 加 `CompleteStreamAsync`。
- [ADR-029 Azure OpenAI 扩展](llm-provider-azure-openai-extension.md)：流式同样支持 Azure。
- [ADR-030 Inbox 单条总结 V0](inbox-item-summarize-v0.md)：错误码体系同源。
- [ADR-031 Inbox 单条打标签 V0](inbox-item-tagging-v0.md)：错误码体系同源；解析失败映射方式可参考。
- [PURPOSE.md](../../PURPOSE.md)：`core_value` "用户不需要会写 prompt" 直接驱动决策 §E1。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 即"方案先行"。
