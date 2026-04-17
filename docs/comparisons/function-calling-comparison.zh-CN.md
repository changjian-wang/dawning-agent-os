---
title: "Tool / Function Calling 深度对比：OpenAI vs Anthropic vs Gemini"
type: comparison
tags: [tool-calling, function-calling, openai, anthropic, gemini, mcp]
sources: [concepts/agent-loop.md, concepts/protocols-a2a-mcp.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Tool / Function Calling 深度对比：OpenAI vs Anthropic vs Gemini

> Function Calling 是现代 Agent 的原子操作。三家主流 LLM 供应商（OpenAI / Anthropic / Google）都提供了结构化工具调用能力，**但规范差异足以让跨供应商切换的代码大量重写**。
>
> 本文做一次彻底的规范级对比，并给出 Dawning `ILLMProvider` 抽象层的统一设计。

---

## 1. 为什么 Function Calling 重要

### 1.1 从 ReAct 文本到结构化调用的演进

| 时代 | 实现 | 问题 |
|------|------|------|
| 2022 ReAct 原始论文 | 让 LLM 输出 `Action: search(xxx)` 文本 | 解析脆弱，参数格式错漏 |
| 2023 OpenAI Function Calling | LLM 返回结构化 JSON | 行业共识 |
| 2024-2026 | 三家各自 API + MCP 标准化 | **规范不统一**，跨供应商代码重写 |

### 1.2 Function Calling 的本质

```
User: "北京天气怎么样？"
  │
  ▼
LLM 决策层
  │
  ├─► 决定：需要调用 get_weather 工具
  │
  ▼
结构化输出 { name: "get_weather", arguments: { city: "北京" } }
  │
  ▼
框架执行工具 → 返回结果 → 喂回 LLM → 生成最终回答
```

**关键**：**工具的选择 + 参数的填充都是 LLM 的职责**。框架只负责路由、执行、注入结果。

---

## 2. OpenAI Function Calling（行业原型）

### 2.1 请求格式

```json
{
  "model": "gpt-4o",
  "messages": [...],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "get_weather",
        "description": "获取指定城市的天气",
        "parameters": {
          "type": "object",
          "properties": {
            "city": { "type": "string" },
            "unit": { "type": "string", "enum": ["celsius", "fahrenheit"] }
          },
          "required": ["city"]
        }
      }
    }
  ],
  "tool_choice": "auto"
}
```

### 2.2 响应格式

```json
{
  "choices": [{
    "message": {
      "role": "assistant",
      "content": null,
      "tool_calls": [{
        "id": "call_abc123",
        "type": "function",
        "function": {
          "name": "get_weather",
          "arguments": "{\"city\":\"北京\",\"unit\":\"celsius\"}"
        }
      }]
    },
    "finish_reason": "tool_calls"
  }]
}
```

**关键特性**：
- `arguments` 是**字符串化的 JSON**，需要二次解析
- 每个 tool_call 有 `id`，用于关联后续 `tool` 角色消息
- `tool_choice`: `"auto"` / `"none"` / `"required"` / 指定工具名
- **并行工具调用**：一次响应可返回多个 tool_calls

### 2.3 工具结果喂回

```json
{
  "role": "tool",
  "tool_call_id": "call_abc123",
  "content": "北京晴，25°C"
}
```

### 2.4 2024-2026 演进

| 特性 | 版本 |
|------|------|
| Parallel Tool Calls | GPT-4 Turbo+ |
| Strict Mode（100% JSON Schema 合规） | GPT-4o+ |
| Structured Outputs | 2024-08 |
| `tool_choice: "required"` | GPT-4 Turbo+ |

---

## 3. Anthropic Tool Use

### 3.1 请求格式

```json
{
  "model": "claude-3-5-sonnet-20241022",
  "messages": [...],
  "tools": [
    {
      "name": "get_weather",
      "description": "获取指定城市的天气",
      "input_schema": {
        "type": "object",
        "properties": {
          "city": { "type": "string" },
          "unit": { "type": "string", "enum": ["celsius", "fahrenheit"] }
        },
        "required": ["city"]
      }
    }
  ]
}
```

**差异点**：
- 没有顶层 `type: "function"` 包装
- 字段叫 `input_schema` 而非 `parameters`
- 没有 `tool_choice` 参数（2024-06 后加入 `tool_choice` 对象）

### 3.2 响应格式

```json
{
  "stop_reason": "tool_use",
  "content": [
    {
      "type": "text",
      "text": "我来查询一下北京的天气。"
    },
    {
      "type": "tool_use",
      "id": "toolu_01A2B3...",
      "name": "get_weather",
      "input": { "city": "北京", "unit": "celsius" }
    }
  ]
}
```

**关键差异**：
- `content` 是**数组**，可以包含 text 和 tool_use 的混合
- `input` 是**原生对象**，不是 JSON 字符串（不需要二次解析）
- `stop_reason: "tool_use"` 对应 OpenAI 的 `finish_reason: "tool_calls"`

### 3.3 工具结果喂回

```json
{
  "role": "user",
  "content": [
    {
      "type": "tool_result",
      "tool_use_id": "toolu_01A2B3...",
      "content": "北京晴，25°C"
    }
  ]
}
```

**差异点**：
- 用 **user 角色**而非 tool 角色
- content 可以是字符串或结构化数组（支持图片结果）
- 可带 `is_error: true` 标记错误

### 3.4 独有能力

| 能力 | 说明 |
|------|------|
| **Tool Use with Thinking** | Extended Thinking 模式下，可以看到 LLM 决策推理过程 |
| **Computer Use**（beta） | 特殊工具 `computer_20241022`，LLM 直接截图 + 点击 |
| **Cache Control** | Tool 定义可加 `cache_control` 用 Prompt Caching 降本 |

---

## 4. Google Gemini Function Calling

### 4.1 请求格式

```json
{
  "contents": [...],
  "tools": [{
    "function_declarations": [{
      "name": "get_weather",
      "description": "获取指定城市的天气",
      "parameters": {
        "type": "OBJECT",
        "properties": {
          "city": { "type": "STRING" },
          "unit": { "type": "STRING", "enum": ["celsius", "fahrenheit"] }
        },
        "required": ["city"]
      }
    }]
  }],
  "tool_config": {
    "function_calling_config": {
      "mode": "AUTO"
    }
  }
}
```

**独有特征**：
- **类型名大写**（`OBJECT` / `STRING`），不是 JSON Schema 标准
- Tool 结构嵌套 `function_declarations`（一次可注册多组）
- `tool_config.function_calling_config.mode`: `AUTO` / `ANY` / `NONE`
- Gemini 2.0+ 支持 **`ANY` 模式 + `allowed_function_names`**：强制只调用特定工具

### 4.2 响应格式

```json
{
  "candidates": [{
    "content": {
      "role": "model",
      "parts": [{
        "functionCall": {
          "name": "get_weather",
          "args": { "city": "北京", "unit": "celsius" }
        }
      }]
    },
    "finishReason": "STOP"
  }]
}
```

**差异点**：
- `parts` 结构与 Anthropic 类似（混合 text + functionCall）
- `args` 是原生对象
- **没有 tool_call id** —— 这在并行调用时需要靠位置索引

### 4.3 工具结果喂回

```json
{
  "role": "function",
  "parts": [{
    "functionResponse": {
      "name": "get_weather",
      "response": { "result": "北京晴，25°C" }
    }
  }]
}
```

### 4.4 独有能力

| 能力 | 说明 |
|------|------|
| **Composition Function Calling** | Gemini 可以串联多个工具调用作为单次 response（类似内建 ReAct） |
| **Code Execution Tool** | 内建 Python 解释器工具（`tools: { code_execution: {} }`） |
| **Google Search Grounding** | 内建搜索工具，直接返回带引用的结果 |

---

## 5. 三家对照速查表

| 维度 | OpenAI | Anthropic | Gemini |
|------|--------|-----------|--------|
| Schema 标准 | JSON Schema | JSON Schema | 类 JSON Schema（大写类型） |
| 请求字段 | `tools[].function.parameters` | `tools[].input_schema` | `tools[].function_declarations[].parameters` |
| 响应标记 | `finish_reason: "tool_calls"` | `stop_reason: "tool_use"` | `finishReason: "STOP"` |
| 响应位置 | `message.tool_calls` | `content[]` 数组中的 tool_use | `candidates[].content.parts[]` 中的 functionCall |
| 参数格式 | **字符串化 JSON** | 原生对象 | 原生对象 |
| Tool Call ID | ✅ `call_xxx` | ✅ `toolu_xxx` | ❌ 无 |
| 结果角色 | `"tool"` | `"user"` + tool_result block | `"function"` |
| 并行调用 | ✅ | ✅ | ✅ |
| 强制调用 | `tool_choice: "required"` | `tool_choice: { type: "tool", name: "..." }` | `tool_config.mode: "ANY"` |
| 流式 tool_calls | ✅ 增量 JSON 片段 | ✅ content_block_delta | ✅ 按 part 流 |
| 结构化输出 | ✅ Strict Mode | ✅ 通过 tool use 模拟 | ✅ `responseSchema` |
| 内建工具 | Web Search, File Search | Computer Use, Text Editor | Code Exec, Google Search |

---

## 6. 常见跨供应商陷阱

### 6.1 参数解析

```csharp
// OpenAI：必须二次解析
var args = JsonSerializer.Deserialize<WeatherArgs>(toolCall.Function.Arguments);

// Anthropic / Gemini：直接反序列化对象
var args = toolUse.Input.Deserialize<WeatherArgs>();
```

### 6.2 Tool Call ID 缺失（Gemini）

并行调用时，OpenAI / Anthropic 有 ID 关联，Gemini 必须按顺序对应：

```
Gemini 响应: [functionCall A, functionCall B]
工具执行:  [resultA, resultB]
喂回:     [functionResponse A, functionResponse B]  ← 顺序必须一致
```

### 6.3 Schema 大小写

```json
// Gemini 专用
{ "type": "OBJECT", "properties": { "age": { "type": "INTEGER" } } }

// 其他两家
{ "type": "object", "properties": { "age": { "type": "integer" } } }
```

跨供应商代码必须做 schema 转换。

### 6.4 消息角色

```
OpenAI:    system / user / assistant / tool
Anthropic: system（顶层字段）/ user / assistant（用户消息里带 tool_result）
Gemini:    user / model / function
```

### 6.5 JSON Schema 支持度

| 特性 | OpenAI | Anthropic | Gemini |
|------|--------|-----------|--------|
| `enum` | ✅ | ✅ | ✅ |
| `oneOf` / `anyOf` | ⚠️ Strict Mode 有限 | ✅ | ⚠️ 有限 |
| `$ref` | ❌ | ⚠️ | ❌ |
| 嵌套深度 | ≤ 5 | 无限制 | ≤ 5 |
| `additionalProperties: false` | Strict Mode 必需 | 可选 | 可选 |

---

## 7. MCP：统一工具发现的尝试

前文 [[concepts/protocols-a2a-mcp.zh-CN]] 介绍过 MCP。在 Function Calling 维度上：

```
┌─────────────────────┐
│   LLM Provider      │
│   (OpenAI/Claude/..)│
└──────────┬──────────┘
           │ 三家各自格式
           ▼
┌─────────────────────┐
│   Framework 抽象层    │  ← 把三家差异隐藏
└──────────┬──────────┘
           │ MCP 协议
           ▼
┌─────────────────────┐
│   MCP Server        │  ← 工具定义一次，三家都能用
└─────────────────────┘
```

**MCP 的价值**：**工具定义与 LLM 供应商解耦**。同一个 MCP Server 可以被 OpenAI / Claude / Gemini 的 Agent 同时使用。

---

## 8. 各框架的抽象方式

### 8.1 OpenAI Agents SDK

```python
@function_tool
def get_weather(city: str) -> str:
    """获取天气"""
    return f"{city}晴"

agent = Agent(name="assistant", tools=[get_weather])
```

Python 类型注解 → 自动生成 JSON Schema。

### 8.2 LangChain

```python
@tool
def get_weather(city: str) -> str:
    """获取天气"""
    return f"{city}晴"
```

底层统一 `BaseTool` 接口；LangChain 的 `ChatModel` 子类各自适配三家 API 差异。

### 8.3 Semantic Kernel

```csharp
[KernelFunction]
[Description("获取天气")]
public string GetWeather([Description("城市名")] string city) => $"{city}晴";
```

`KernelFunction` 是抽象，底层 Connector 分别对接 OpenAI/Azure/等。

### 8.4 MAF（Microsoft Agent Framework）

```csharp
[Description("获取天气")]
public string GetWeather(string city) => $"{city}晴";

var agent = new ChatCompletionAgent
{
    Tools = [AIFunctionFactory.Create(GetWeather)]
};
```

通过 **MEAI `AIFunction`**（Microsoft.Extensions.AI）统一抽象，让 `IChatClient` 接口不关心底层供应商。

### 8.5 Dawning（设计方向）

```csharp
public sealed class GetWeatherTool : ITool
{
    public string Name => "get_weather";
    public string Description => "获取指定城市的天气";
    public JsonSchema Schema => JsonSchema.For<WeatherInput>();

    public async Task<ToolResult> InvokeAsync(
        ToolInvocationContext ctx,
        CancellationToken ct)
    {
        var input = ctx.GetArguments<WeatherInput>();
        return ToolResult.Success($"{input.City}晴");
    }
}
```

**统一 `ITool` 抽象，`ILLMProvider` 负责翻译到三家具体格式**。

---

## 9. Dawning `ILLMProvider` 统一抽象

### 9.1 统一请求模型

```csharp
public record LLMRequest
{
    public IReadOnlyList<Message> Messages { get; init; }
    public IReadOnlyList<ITool>? Tools { get; init; }
    public ToolChoice? ToolChoice { get; init; }  // Auto/None/Required/Specific(name)
    public bool ParallelToolCalls { get; init; } = true;
    public ResponseFormat? Format { get; init; }  // Text/Json/Schema
    public ModelOptions Options { get; init; }
}
```

### 9.2 统一响应模型

```csharp
public record LLMResponse
{
    public string? Text { get; init; }
    public IReadOnlyList<ToolInvocation> ToolCalls { get; init; }
    public FinishReason Finish { get; init; }  // Stop / ToolCalls / Length / ContentFilter
    public TokenUsage Usage { get; init; }
    public string Model { get; init; }
}

public record ToolInvocation(
    string Id,           // 统一生成（OpenAI/Anthropic 直接用，Gemini 合成）
    string Name,
    JsonElement Arguments);
```

### 9.3 适配器职责

| 适配器 | 职责 |
|--------|------|
| `OpenAIProvider` | Schema → `parameters`；解析 `message.tool_calls`；`arguments` JSON 反序列化 |
| `AnthropicProvider` | Schema → `input_schema`；扁平化 `content[]` 中 tool_use；role 转换 |
| `GeminiProvider` | Schema 大小写转换；并行调用生成合成 ID；role 映射（assistant → model） |

### 9.4 流式统一

```csharp
public interface ILLMProvider
{
    IAsyncEnumerable<LLMStreamEvent> ChatStreamAsync(
        LLMRequest request,
        CancellationToken ct);
}

public abstract record LLMStreamEvent;
public record TextDelta(string Text) : LLMStreamEvent;
public record ToolCallStart(string Id, string Name) : LLMStreamEvent;
public record ToolCallArgumentsDelta(string Id, string Fragment) : LLMStreamEvent;
public record ToolCallEnd(string Id) : LLMStreamEvent;
public record Finished(FinishReason Reason, TokenUsage Usage) : LLMStreamEvent;
```

三家流式协议各不相同，**适配器负责翻译到这套统一事件流**。

---

## 10. 工具调用的工程最佳实践

### 10.1 Schema 设计

| 原则 | 说明 |
|------|------|
| 用 `description` 解释**用途**而非**实现** | LLM 看 description 决定是否调用 |
| 参数描述要说明**示例和约束** | "city: 城市中文名，如 '北京' '上海'" |
| 枚举优于字符串 | `enum: ["celsius", "fahrenheit"]` 比自由字符串可靠 |
| 数量控制在 **≤ 20 个工具** | LLM 在工具过多时决策质量下降明显 |
| 结构化返回值 | 返回 JSON 而非自然语言，方便下游工具链式调用 |

### 10.2 错误处理

```csharp
// 好：结构化错误 + 建议下一步
return ToolResult.Failure(new {
    error = "InvalidCityCode",
    message = "城市代码不存在",
    suggestion = "请使用中文城市名，例如 '北京'"
});

// 差：抛异常
throw new Exception("city not found");  // LLM 看不到上下文
```

### 10.3 幂等性

工具应满足幂等性（相同输入得相同输出），因为 Agent 可能重试：

```csharp
public interface ITool
{
    bool IsIdempotent { get; }  // 框架根据此决定是否允许重试
}
```

### 10.4 成本与延迟

| 策略 | 适用 |
|------|------|
| **工具结果缓存** | 高频只读工具（如天气、汇率） |
| **并行调用上限** | 防止 LLM 一次调用太多（OpenAI 默认无上限） |
| **超时保护** | 每个工具独立 CancellationToken |
| **成本预算** | 累计 tool_calls 次数 + 累计 token |

---

## 11. 小结

> 三家 Function Calling 在 **语义上一致**（LLM 决定 → 结构化输出 → 框架执行），
> 但在 **规范上差异巨大**（字段名、类型大小写、消息角色、ID 机制都不同）。
>
> **不统一抽象，跨供应商切换的成本很高**。
> Dawning `ILLMProvider` + `ITool` 的核心使命，就是把三家差异彻底屏蔽，让应用代码只关心业务。

---

## 12. 延伸阅读

- [[concepts/protocols-a2a-mcp.zh-CN]] — MCP 协议作为跨供应商的工具层标准
- [[concepts/dawning-capability-matrix.zh-CN]] — `ILLMProvider` 和 `IToolRegistry` 抽象
- [[concepts/agent-loop.md]] — Tool 调用在 Agent Loop 中的位置
- OpenAI Function Calling：<https://platform.openai.com/docs/guides/function-calling>
- Anthropic Tool Use：<https://docs.anthropic.com/en/docs/build-with-claude/tool-use>
- Gemini Function Calling：<https://ai.google.dev/gemini-api/docs/function-calling>
