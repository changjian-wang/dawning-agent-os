# LLM 技术原理

> Agent 开发者视角的 LLM 核心概念。
> 框架设计决策见 [上下文管理](context-management.md)；Agent 执行模式见 [Agent Loop](agent-loop.md)。

---

## 1. LLM 概述

**Large Language Model** 是一个自回归概率模型：

$$P(\text{next\_token} \mid \text{context})$$

给定上文所有 token，输出下一个 token 的概率分布并采样。三个关键推论：

1. **概率性输出** — 相同输入可能产生不同输出（除非 temperature=0）
2. **无状态** — 每次调用独立，"记忆"依赖请求中携带的消息历史
3. **模式匹配** — LLM 不"理解"语义，而是基于训练语料的统计模式生成文本

---

## 2. Token

Token 是 LLM 分词器（tokenizer）产出的最小单位，不等于字符或单词。

```
"Hello, world!"         → ["Hello", ",", " world", "!"]       4 tokens
"你好世界"               → ["你好", "世界"]                     2 tokens
"ChatCompletionOptions" → ["Chat", "Completion", "Options"]   3 tokens
```

### 估算经验值

| 语言 | 经验规则 |
|------|---------|
| 英文 | 1 token ≈ 4 字符 ≈ 0.75 单词 |
| 中文 | 1 token ≈ 1.5–2 字符 |
| 代码 | 一行 ≈ 10–30 token |

### Agent 场景的成本放大

Agent 每步将完整对话历史发给 LLM，token 消耗呈等差级数增长：

$$\sum_{k=1}^{n} k \cdot \bar{t} = \frac{n(n+1)}{2} \cdot \bar{t}$$

其中 $n$ 为步数，$\bar{t}$ 为每步平均新增 token。10 步 × 500 token/步 ≈ 27,500 total input tokens。

这要求框架提供 `MaxSteps`、`MaxCost` 限制和自动上下文压缩机制（→ [上下文管理](context-management.md)）。

---

## 3. Chat Completion API

所有主流 LLM 提供商（OpenAI / Azure OpenAI / Ollama / Anthropic）收敛到同一请求-响应模式。

### 请求结构

```json
POST /v1/chat/completions
{
  "model": "gpt-4o",
  "messages": [
    {"role": "system", "content": "你是一个助手"},
    {"role": "user",   "content": "今天天气怎么样？"}
  ],
  "temperature": 0.7,
  "max_tokens": 1000,
  "tools": [...],
  "stream": false
}
```

### 响应结构

```json
{
  "choices": [{
    "message": {
      "role": "assistant",
      "content": "...",
      "tool_calls": null
    },
    "finish_reason": "stop"
  }],
  "usage": { "prompt_tokens": 25, "completion_tokens": 30, "total_tokens": 55 }
}
```

### `finish_reason` 语义

| 值 | 含义 | Agent 下一步 |
|----|------|-------------|
| `stop` | 生成完成 | 返回最终答案 |
| `tool_calls` | 请求工具调用 | 执行工具 → 反馈结果 → 再次调用 LLM |
| `length` | 输出被 `max_tokens` 截断 | 可续接或报错 |

---

## 4. 消息角色

```json
[
  {"role": "system",    "content": "你是一个 SQL 专家..."},
  {"role": "user",      "content": "查询最近的订单"},
  {"role": "assistant", "content": null, "tool_calls": [...]},
  {"role": "tool",      "content": "查询结果...", "tool_call_id": "call_xxx"}
]
```

| 角色 | 来源 | Framework 对应 |
|------|------|---------------|
| `system` | 开发者 | `Agent.Instructions` → 自动注入 |
| `user` | 终端用户 | `AgentContext.UserInput` |
| `assistant` | LLM | 框架记录到消息历史 |
| `tool` | 工具执行结果 | 框架执行 `ITool` 后构造 |

**System Prompt = Agent 的行为边界**。同一 LLM 配不同 system prompt 产生截然不同的输出，因此框架将 `Instructions` 作为 Agent 一等属性。

### 消息序列

工具调用的完整循环：

```
[system] → [user] → [assistant, tool_calls] → [tool, tool_call_id] → [assistant]
```

框架通过 `IConversationMemory` 管理此消息数组（追加、压缩、序列化、token 计数）。

---

## 5. 上下文窗口

LLM 单次请求能处理的最大 token 数。各模型差异显著，且随版本迭代持续扩大，具体数值以厂商官方文档为准。

**Agent 为何容易溢出**：一个 10 步运行产生 ~32 条消息 + 工具定义，若工具返回大段文本（文件内容、查询结果），轻松超过 50K token。

上下文管理策略的深入分析 → [上下文管理](context-management.md)。

---

## 6. Temperature 与采样

### Temperature

控制输出随机性，作用于 softmax 前的 logit 缩放：

$$P(token_i) = \frac{e^{z_i / T}}{\sum_j e^{z_j / T}}$$

| $T$ 值 | 效果 |
|--------|------|
| → 0 | 概率集中到最高分 token，近似确定性 |
| 1.0 | 原始分布 |
| → ∞ | 趋近均匀分布 |

### Agent 场景推荐值

| 场景 | Temperature | 理由 |
|------|------------|------|
| Function Calling | 0.0–0.3 | 工具名/参数必须精确 |
| 代码生成 | 0.0–0.2 | 语法不容错 |
| 通用对话 | 0.5–0.7 | 流畅自然 |
| 创意写作 | 0.8–1.2 | 需要多样性 |

### 业界共识：默认 `null`

不显式传值，让 API 使用模型最优默认值。原因：

1. 不同模型默认值不同（GPT-4o 默认 1.0；推理模型不支持修改）
2. 显式传 `0.7` 可能与推理模型冲突（会报错）
3. `null` 对模型升级自动适配

### 其他采样参数

| 参数 | 作用 |
|------|------|
| `top_p` | 核采样，从累积概率达 p 的 token 子集中采样 |
| `frequency_penalty` | 降低已出现 token 概率，减少重复 |
| `presence_penalty` | 鼓励新 token，增加多样性 |

通常 `temperature` 和 `top_p` 二选一调整。

---

## 7. Function Calling

Function Calling 使 LLM 从纯文本生成器变为可调用外部函数的 Agent 核心。

### 完整流程

```
1. 客户端发送 messages + tools 定义 (JSON Schema)
2. LLM 返回 tool_calls（name + arguments），finish_reason="tool_calls"
3. 客户端执行工具，将结果作为 role="tool" 消息反馈
4. LLM 基于工具结果生成最终回答，finish_reason="stop"
```

### tool_choice

| 值 | 含义 |
|----|------|
| `"auto"` | LLM 自行决定（默认） |
| `"none"` | 禁止工具调用 |
| `"required"` | 必须调用至少一个工具 |
| `{"function": {"name": "xxx"}}` | 强制调用指定工具 |

### 并行工具调用

LLM 可在一次响应中返回多个 `tool_calls`，框架应支持并行执行（`Task.WhenAll`），每个结果匹配对应 `tool_call_id`。

### JSON Schema 参数定义

```json
{
  "type": "object",
  "properties": {
    "city": { "type": "string", "description": "城市名称" },
    "unit": { "type": "string", "enum": ["celsius", "fahrenheit"] }
  },
  "required": ["city"]
}
```

`description` 直接影响 LLM 传参准确性。框架应从 C# 方法签名自动生成 Schema。

---

## 8. Streaming

### 协议

流式响应基于 SSE（Server-Sent Events），使用增量字段 `delta`（区别于完整的 `message`）：

```
data: {"choices":[{"delta":{"content":"北京"}}]}
data: {"choices":[{"delta":{"content":"现在是晴天"}}]}
data: {"choices":[{"delta":{},"finish_reason":"stop"}]}
data: [DONE]
```

### 流式 Tool Call

工具调用的 `arguments` 分块传输，需按 `index` 拼接直到流结束后才能解析完整 JSON。

### C# 实现

```csharp
await foreach (var chunk in llm.ChatStreamAsync(messages, options, ct))
{
    Console.Write(chunk);
}
```

选择 `IAsyncEnumerable` 的原因：原生背压、`CancellationToken` 支持、LINQ 可组合。

---

## 9. Structured Output

### 两种模式

| 模式 | 约束级别 | 兼容性 |
|------|---------|-------|
| `response_format: { type: "json_object" }` | 保证合法 JSON，需 prompt 描述结构 | 广泛支持 |
| `response_format: { type: "json_schema", ... }` | 保证符合指定 JSON Schema | 部分模型支持 |

---

## 10. 成本模型

### 计费结构

- **Input tokens**：所有发送给 LLM 的 token（system + messages + tools 定义）
- **Output tokens**：LLM 生成的 token，通常单价是 input 的 2–4 倍

具体定价因模型和厂商差异较大且频繁调整，请参考各厂商官方定价页面。本地部署（Ollama 等）无 API 调用费用但有硬件成本。

### 框架应对

1. **Token 追踪** — 每次调用记录 `usage`
2. **成本上限** — `AgentOptions.MaxCostPerRun`，超限终止
3. **日志** — 逐步输出 token 消耗

---

## 11. Rate Limiting

### 限流维度

| 维度 | 说明 |
|------|------|
| RPM (Requests/min) | 每分钟请求数 |
| TPM (Tokens/min) | 每分钟 token 数 |
| RPD (Requests/day) | 每天请求数 |

触发限流返回 HTTP 429 + `Retry-After` 头。

### 框架应对

1. 自动重试 + 指数退避
2. `IHttpClientFactory` 连接池管理
3. Polly 弹性策略集成

---

## 12. LLM 特性 → Framework 抽象映射

| LLM 特性 | Framework 抽象 |
|---------|---------------|
| Chat Completion API | `ChatMessage`, `ChatCompletionOptions`, `ChatCompletionResponse` |
| 消息角色 | `ChatMessage.User()` / `.Assistant()` / `.System()` / `.Tool()` |
| Token 限制 | `IConversationMemory`（→ [上下文管理](context-management.md)）|
| Function Calling | `ITool`, `IToolRegistry`, `ToolDefinition`, `ToolCall` |
| Streaming | `IAsyncEnumerable<string>`, `StreamingChatEvent` |
| 多提供商 | `ILLMProvider`（统一 OpenAI / Azure / Ollama）|
| 计费 | `TokenUsage`, `AgentResponse.TotalCost`, `MaxCostPerRun` |
| Rate Limiting | `IHttpClientFactory` + Polly |
| Temperature | `ChatCompletionOptions.Temperature` (nullable) |
| Structured Output | `ResponseFormat` |

---

## 延伸阅读

- [上下文管理](context-management.md) — 五种上下文管理流派对比 + Dawning 双层记忆架构设计
- [Agent Loop](agent-loop.md) — ReAct / Plan-and-Execute / Reflexion 执行模式
- [LLM Wiki 模式](llm-wiki-pattern.zh-CN.md) — 编译式知识管理与记忆面设计

---

*最后更新：2026-04-11*
