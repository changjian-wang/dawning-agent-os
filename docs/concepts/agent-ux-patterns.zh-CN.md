---
title: "Agent UX 模式：流式 UI、进度指示、HITL 交互与 Artifact 展示"
type: concept
tags: [ux, ui, streaming, hitl, artifacts, generative-ui, copilot-patterns]
sources: [concepts/deployment-architectures.zh-CN.md, concepts/agent-security.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Agent UX 模式：流式 UI、进度指示、HITL 交互与 Artifact 展示

> Agent 的体验不只是"回答对不对"，更是"我知道它在做什么吗？我能干预吗？结果能直接用吗？"
>
> ChatGPT / Claude / Cursor / v0 / Perplexity 把 Agent UX 推到了新高度：流式、步骤可见、artifact 可编辑、审批内嵌、生成式 UI。
>
> 本文梳理主流 Agent UX 模式、常见反模式，以及前后端协议设计（AI Elements / Vercel AI SDK / CopilotKit）。

---

## 1. 为什么 Agent UX 特殊

| 特性 | UX 挑战 |
|------|---------|
| **延迟高** | 第一个 token 200ms-5s，完整响应 10s-10min |
| **非确定性** | 同样输入不同路径，不好"预览" |
| **多步** | Agent 一轮可能调 10 个工具，用户需知道进度 |
| **可能出错** | 工具失败、LLM 错、Policy 拒绝——都要优雅呈现 |
| **可能需要用户** | HITL 审批、澄清、选择 |
| **结构化输出** | 表格、代码、图表、文档——不只是聊天 |
| **可编辑输出** | Artifact（v0/Claude Artifacts）让用户接力 |
| **工具执行可见** | 透明 vs 隐藏，产品决策 |

---

## 2. 核心 UX 模式

### 2.1 Streaming（流式）

**不可或缺**。TTFT > 3s 体验就崩。

```
用户输入 ──► [LLM 调用] ──► 第一个 token 200-500ms 到达 ──► 用户开始看 ──► 持续增量
```

前端渲染：
- Token-by-token 追加
- Markdown 实时解析
- 代码块高亮延迟（等完整 ```后）
- 自动滚动到底（可被用户手动中断）

### 2.2 Step / Trace 可见

用户看得到 Agent 在做什么：

```
▸ 🔍 搜索 "Python async best practices"（0.8s）
▸ 📖 阅读 realpython.com（1.2s）
▸ 📝 正在撰写答案...
```

**两种实现**：
- **事件流**：每步一个 SSE event
- **结构化 think**：output 里有 `<thinking>` 块折叠展示

### 2.3 HITL（Human-in-the-Loop）

**嵌入式审批**：

```
┌────────────────────────────────────────┐
│  Agent 想要发送邮件                      │
│                                        │
│  发件人: alice@corp.com                 │
│  收件人: external@customer.com          │
│  主题: 合同跟进                          │
│  内容: ...                              │
│                                        │
│  [ 批准发送 ]  [ 修改后发送 ]  [ 拒绝 ]    │
└────────────────────────────────────────┘
```

关键：**审批 UI 阻塞 Agent 执行**，但**不阻塞进程**（参见 [[concepts/deployment-architectures.zh-CN]] HITL 续跑）。

### 2.4 Artifact（可编辑产出物）

Claude Artifacts / v0 的核心：Agent 输出分两栏：
- 左：对话（自然语言）
- 右：Artifact（代码 / 文档 / 图表 / 网页）

Artifact 特性：
- 独立版本
- 用户直接编辑
- 再次对话让 Agent 修改
- 导出 / 分享

### 2.5 Generative UI

Agent 直接生成 UI 组件而非文本：

```
用户："帮我比较 iPhone 16 和 Galaxy S25"

Agent 输出:
<ComparisonTable
  products={["iPhone 16", "Galaxy S25"]}
  dimensions={["Price", "Camera", "Battery"]}
  data={...}
/>
```

前端用 Vercel AI SDK 或 CopilotKit 渲染。

### 2.6 工具调用可视化

```
┌──────────────────────────────────────┐
│ 🔧 Calling: fetch_weather             │
│    { "city": "Beijing" }             │
│                                      │
│    ⏳ Running... (0.6s)               │
│                                      │
│    ✓ Returned:                        │
│    { "temp": 22, "condition": "sunny"│
└──────────────────────────────────────┘
```

可展开/折叠，默认折叠输入输出细节。

### 2.7 多模态

- 用户可上传图片 / 文件
- Agent 可生成图片 / 图表 / 音频
- 多轮对话里图文混排

### 2.8 推荐 Follow-up

```
Agent 回答完，底部给 3 个建议跟进:
  💡 "详细讲讲第 3 点"
  💡 "换个角度呢？"
  💡 "给我示例代码"
```

小模型生成（成本低），大幅提升对话深度。

### 2.9 Interruptibility（可中断）

```
[生成中] ━━━━━━━━━ [Stop] 按钮
```

- 前端断流（客户端关连接）
- 后端 CancellationToken 透传
- 已生成 token 保留

### 2.10 Resume（可恢复）

- 网络掉线 → 重连时从断点继续
- `last_event_id` 机制
- 依赖服务端缓存流状态

---

## 3. 前端协议

### 3.1 Vercel AI SDK（JS/React 生态标准）

```typescript
import { useChat } from 'ai/react';

const { messages, input, handleSubmit, isLoading, stop } = useChat({
  api: '/api/chat',
  onToolCall: async ({ toolCall }) => { ... }
});
```

**核心能力**：
- Streaming text
- Tool calling UI
- Generative UI（`streamUI`）
- Reasoning tokens（Claude / DeepSeek）
- Multi-provider 统一

### 3.2 AI Elements（Vercel AI SDK UI）

一组 shadcn 风格组件：
- `<Message>`、`<ToolCall>`、`<Reasoning>`
- `<Artifact>`
- `<Approval>`
- `<Sources>`（Perplexity 风）

### 3.3 CopilotKit（React Agent UI）

```jsx
<CopilotChat
  instructions="..."
  labels={{ title: "Assistant" }}
/>
```

深度集成：
- Action（Agent 能操作 App 状态）
- Readable State（Agent 知道当前页面状态）
- Task 可视化

### 3.4 Assistant UI（新兴）

OpenAI-style 聊天 UI 开源实现，与 LangGraph / AI SDK 都兼容。

### 3.5 协议：SSE vs WebSocket

| 维度 | SSE | WebSocket |
|------|-----|-----------|
| 方向 | 单向（server→client） | 双向 |
| 复杂度 | 低 | 中 |
| 代理友好 | 好 | 一般 |
| 标准度 | HTTP 原生 | 独立协议 |
| 适合 | 聊天 / Agent 输出 | 音视频 / 实时协作 |

**推荐**：默认 SSE，特殊场景（语音 / 实时协作）用 WS。

---

## 4. 事件模型

### 4.1 Vercel AI SDK Data Stream Protocol

```
data: {"type":"text-delta","delta":"你好"}
data: {"type":"text-delta","delta":"！"}
data: {"type":"tool-call","toolCallId":"...","toolName":"search","args":{...}}
data: {"type":"tool-result","toolCallId":"...","result":{...}}
data: {"type":"reasoning","text":"..."}
data: {"type":"finish","finishReason":"stop"}
```

### 4.2 LangGraph Stream Events

```
{"event": "on_chat_model_stream", "data": {...}}
{"event": "on_tool_start", "name": "search", "data": {...}}
{"event": "on_tool_end", "data": {...}}
{"event": "on_chain_end", "data": {...}}
```

### 4.3 Anthropic Events

```
event: content_block_delta
data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"hi"}}

event: content_block_start
data: {"type":"content_block_start","content_block":{"type":"tool_use"...}}
```

### 4.4 OpenAI Streaming

```
data: {"choices":[{"delta":{"content":"hi"}}]}
data: {"choices":[{"delta":{"tool_calls":[...]}}]}
```

### 4.5 Dawning 统一事件模型

```
AgentStarted
AgentStepStarted { stepIndex }
LLMCallStarted { model }
LLMTextDelta { delta }
LLMReasoningDelta { delta }         ← Claude thinking / o1 reasoning
ToolCallStarted { name, args }
ToolCallCompleted { name, result }
ApprovalRequired { details }         ← HITL
ArtifactUpdated { id, kind, content }
AgentStepCompleted
Error { code, message }
AgentCompleted { usage }
```

---

## 5. 延迟体感优化

### 5.1 TTFT 目标

| 场景 | TTFT 目标 |
|------|----------|
| 简单问答 | < 500ms |
| 搜索 Agent | < 2s（第一个可见 token 是 "正在搜索..."） |
| 代码生成 | < 1s |
| Artifact | < 2s |

### 5.2 技巧

- **预热占位符**：先发 "正在思考..." 立刻让用户看到反应
- **乐观 UI**：发送消息后立即显示用户消息（不等服务端确认）
- **渐进式 Markdown**：不等整段完成就渲染
- **Skeleton 加载**：Artifact 用骨架屏
- **Progressive Streaming**：关键部分先出（摘要 → 详细）

---

## 6. 错误呈现

### 6.1 分类

| 错误 | 呈现 |
|------|------|
| LLM API 临时失败 | "正在重试..." 自动恢复 |
| Tool 失败 | 具体说明 + 重试/跳过选项 |
| Policy 拒绝 | "由于安全策略无法完成" + 理由（不泄露内部） |
| Rate limit | "请求过多，稍后再试" + 倒计时 |
| Timeout | "任务时间较长，已转入后台" + 通知 |
| Validation 失败 | 输入处高亮 |
| 未知错误 | 通用提示 + 支持联系方式 |

### 6.2 优雅降级

```
完整 Agent 失败 ──► 降级为简单 Q&A
RAG 失败       ──► 仍基于通用知识回答（说明无引用）
工具失败      ──► 跳过，用 LLM 已知知识
```

---

## 7. 上下文与记忆 UI

### 7.1 展示 "Agent 知道的"

- 当前对话 summary
- 激活的长期记忆（"我记得你喜欢简短回答"）
- 用户可编辑 / 删除记忆（透明 + 控制权）

### 7.2 Context Pinning

用户可 pin 某条消息作为后续 context（Cursor / Perplexity 模式）。

### 7.3 Memory Review UI

```
┌────────────────────────────────────┐
│ 关于你的记忆（6 条）                 │
│   ✓ 你在使用 .NET                   │
│   ✓ 偏好简洁代码                     │
│   ✓ 项目名为 Dawning                │
│   ...                              │
│                                    │
│   [ 编辑 ] [ 删除 ]                  │
└────────────────────────────────────┘
```

---

## 8. Artifact 深度

### 8.1 类型

| 类型 | 示例 | 渲染 |
|------|------|------|
| **代码** | Python / TS / SQL | Monaco / CodeMirror |
| **文档** | Markdown 长文 | 预览 + 编辑 |
| **React 组件** | 交互 UI | iframe sandbox |
| **HTML** | 单页 app | iframe |
| **SVG / 图表** | Mermaid / D3 | 原生渲染 |
| **表格** | CSV / TSV | DataGrid |

### 8.2 版本管理

```
v1 ──► v2 ──► v3
          └─► v2-alt （分支）
```

用户可切换版本，让 Agent 基于特定版本继续修改。

### 8.3 Sandbox

用户生成代码可能危险——必须 sandbox：
- React 组件在 iframe + CSP
- 代码执行在 E2B / WebContainer / Modal
- 绝不 eval 到主页面

---

## 9. 流式对话的交互细节

### 9.1 输入阶段

- Enter 发送 / Shift+Enter 换行（约定俗成）
- @ 提及文件、工具、Agent
- / 触发命令（slash command）
- 文件拖拽 → 附件

### 9.2 生成阶段

- Stop 按钮永远可见
- 估计进度（可选，基于历史）
- 当前步骤指示

### 9.3 完成阶段

- 复制按钮
- Regenerate
- Branch（从这里分叉）
- Rate（👍/👎，反馈 Eval）
- Follow-up 建议

---

## 10. 代表产品拆解

### 10.1 ChatGPT / Claude

- 流式 + Artifact
- Thinking block 可折叠
- Memory 可控
- Canvas / Artifact 双栏

### 10.2 Cursor

- Inline AI（代码旁直接输出）
- Composer（多文件编辑 Agent）
- Diff 视图 + 一键接受
- Tab autocomplete

### 10.3 v0.dev

- 生成式 UI 专家
- 组件预览实时渲染
- 版本分叉
- 一键部署

### 10.4 Perplexity

- 搜索 Agent
- Sources 引用面板
- Related 问题
- Focus 模式（academic/youtube/reddit）

### 10.5 Devin / Claude Code

- 长任务 Agent
- 文件树可见
- Terminal 可见
- 用户可随时接管

---

## 11. 反模式

| 反模式 | 后果 |
|--------|------|
| 无流式（等完整响应） | 用户以为死了 |
| 无 Stop 按钮 | 失控焦虑 |
| Tool 执行完全隐藏 | 用户不知在干啥 |
| Tool 细节全展开 | 信息过载 |
| 错误只给 "出错了" | 无法处理 |
| 无 HITL 审批 | 危险操作失控 |
| Artifact 无版本 | 误改覆盖 |
| Artifact 可 eval 主页面 | 安全灾难 |
| 无可恢复 | 网络抖动即崩 |
| Memory 黑盒 | 隐私担忧 |

---

## 12. 移动端 / 多端

- Token 渲染节流（移动端 CPU 弱）
- 折叠默认更激进
- Streaming 打字机效果可关闭
- 离线缓存最近对话
- 推送通知（长任务完成）

---

## 13. 可访问性

- ARIA live region（screen reader 能听到流式）
- 键盘导航（工具展开、Artifact 切换）
- 色盲友好配色
- 字号可调
- 高对比度模式

---

## 14. Dawning 交付

### 14.1 抽象

```csharp
// 统一事件流
public interface IAgentEventStream : IAsyncEnumerable<AgentEvent>;

public abstract record AgentEvent
{
    public record TextDelta(string Delta) : AgentEvent;
    public record ReasoningDelta(string Delta) : AgentEvent;
    public record ToolCallStarted(string Name, JsonElement Args) : AgentEvent;
    public record ToolCallCompleted(string Name, JsonElement Result) : AgentEvent;
    public record ApprovalRequired(string Reason, JsonElement Payload) : AgentEvent;
    public record ArtifactUpdated(string Id, string Kind, string Content, int Version) : AgentEvent;
    public record Error(string Code, string Message) : AgentEvent;
    public record Done(Usage Usage) : AgentEvent;
}
```

### 14.2 协议适配

- `Dawning.AgentOS.Host` 暴露：
  - SSE：`/stream/vercel-ai` （Vercel AI SDK 兼容）
  - SSE：`/stream/openai` （OpenAI 兼容）
  - WS：`/ws`
  - A2A：标准 A2A 协议

### 14.3 官方前端组件（未来）

可选 React / Blazor / Avalonia 组件库，业务可直接嵌。

---

## 15. UX 设计清单

- [ ] 首 token < 1s，否则给占位符
- [ ] 流式渲染 + Markdown 实时解析
- [ ] Stop 按钮永远可见
- [ ] 每步工具调用有可见事件
- [ ] HITL 审批 UI 内嵌
- [ ] 错误分类呈现 + 恢复路径
- [ ] Artifact 独立区 + 版本
- [ ] 输出 Sandbox（代码/HTML）
- [ ] Memory 透明可控
- [ ] Regenerate / Branch
- [ ] 复制 / 分享
- [ ] 推荐 Follow-up
- [ ] 可中断 + 可恢复
- [ ] 多端自适应
- [ ] A11y 完整

---

## 16. 小结

> **Agent UX 的本质是"与不确定性共存"**。
>
> 流式让延迟可感知、步骤让过程可理解、HITL 让危险可控、Artifact 让结果可用、生成式 UI 让交互升维。
>
> Dawning 后端输出统一事件流，前端可接 Vercel AI SDK / CopilotKit / 自建 UI，
> 让 UX 成为 Agent 能力的放大器而非瓶颈。

---

## 17. 延伸阅读

- [[concepts/deployment-architectures.zh-CN]] — HITL 续跑
- [[concepts/agent-security.zh-CN]] — Artifact Sandbox
- [[concepts/multi-agent-patterns.zh-CN]] — 多 Agent UI 展示
- Vercel AI SDK：<https://ai-sdk.dev/>
- AI Elements：<https://ai-sdk.dev/elements>
- CopilotKit：<https://docs.copilotkit.ai/>
- Assistant UI：<https://www.assistant-ui.com/>
- Claude Artifacts：<https://www.anthropic.com/news/artifacts>
