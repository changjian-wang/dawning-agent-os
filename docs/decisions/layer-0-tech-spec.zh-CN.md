---
title: "Layer 0 技术规格：LLM Driver 抽象"
type: decision
tags: [layer-0, llm-driver, tech-spec, api-design, agent-os]
sources: [decisions/layer-0-requirements.zh-CN.md, decisions/layer-0-features.zh-CN.md]
created: 2026-04-08
updated: 2026-04-17
status: draft
---

# Layer 0 技术规格：LLM Driver 抽象

> 定义 API 接口、数据模型、协议细节和关键技术选型。
>
> **注意**：本文档是 Dawning Agent OS 全新项目的设计规格。
> 接口设计参考了 dawning-agents 中已验证可行的模式，但代码从零实现。

## 1. 核心接口设计

### 1.1 ILLMProvider

```csharp
namespace Dawning.AgentOS.Abstractions.LLM;

/// <summary>
/// LLM 提供商的统一驱动接口。
/// 每个实现（Ollama、OpenAI、Azure、Anthropic）独立 NuGet 包。
/// </summary>
public interface ILLMProvider
{
    /// <summary>提供商名称（如 "ollama"、"openai"、"azure-openai"、"anthropic"）。</summary>
    string Name { get; }

    /// <summary>该 Provider 所使用的模型名称。</summary>
    string Model { get; }

    /// <summary>该模型的能力声明。</summary>
    ModelCapabilities Capabilities { get; }

    /// <summary>非流式对话。等待完整响应后返回。</summary>
    Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>流式对话，返回纯文本增量。适合只需要文本的简单场景。</summary>
    IAsyncEnumerable<string> ChatStreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    /// <summary>结构化流式对话。返回统一事件流，支持 tool call 增量拼接。</summary>
    IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(
        IEnumerable<ChatMessage> messages,
        ChatCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);
}
```

**设计说明**：
- `Model` 属性 — 可观测性需要知道当前使用的模型
- `Capabilities` 属性 — 上层可以查询模型能力（是否支持 vision、tool_use 等）
- 三方法设计已在 dawning-agents 中验证可行

### 1.2 ILLMProviderFactory

```csharp
/// <summary>
/// 按名称解析已注册的 Provider。支持多 Provider 场景。
/// </summary>
public interface ILLMProviderFactory
{
    /// <summary>获取默认 Provider。</summary>
    ILLMProvider GetDefault();

    /// <summary>按注册名称获取特定 Provider。</summary>
    ILLMProvider GetProvider(string name);

    /// <summary>获取所有已注册的 Provider 名称。</summary>
    IReadOnlyList<string> GetRegisteredNames();
}
```

**设计决策**：使用工厂模式而非 .NET Keyed Services，因为：
- Keyed Services 要求消费者在编译时知道 key
- 工厂模式支持运行时动态选择（Agent 配置指定 Provider 名称）
- 降级链需要按顺序遍历多个 Provider

---

## 2. 数据模型

### 2.1 ChatMessage

```csharp
/// <summary>LLM 对话消息。不可变值对象。</summary>
public sealed record ChatMessage
{
    public required string Role { get; init; }        // "system" | "user" | "assistant" | "tool"
    public required string Content { get; init; }
    public string? Name { get; init; }                // 多 Agent 场景下标识发送者
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }  // assistant role only
    public string? ToolCallId { get; init; }          // tool role only

    public bool HasToolCalls => ToolCalls is { Count: > 0 };

    // Factory methods（语义清晰 + 编译时约束）
    public static ChatMessage System(string content);
    public static ChatMessage User(string content);
    public static ChatMessage Assistant(string content);
    public static ChatMessage AssistantWithToolCalls(IReadOnlyList<ToolCall> toolCalls, string content = "");
    public static ChatMessage ToolResult(string toolCallId, string content);
}
```

**设计说明**：四角色 record + factory methods 的模式已在 dawning-agents 中验证可行，沿用此设计。

### 2.2 ChatCompletionResponse

```csharp
public sealed record ChatCompletionResponse
{
    public required string Content { get; init; }
    public TokenUsage Usage { get; init; } = TokenUsage.Empty;
    public string? FinishReason { get; init; }        // "stop" | "tool_calls" | "length" | "content_filter"
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    public ProviderCallMetrics Metrics { get; init; } = ProviderCallMetrics.Empty;  // 新增

    public bool HasToolCalls => ToolCalls is { Count: > 0 };
}
```

**设计说明**：相比 dawning-agents，新增 `Metrics` 字段，包含延迟和成本信息。

### 2.3 TokenUsage

```csharp
public sealed record TokenUsage
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens => PromptTokens + CompletionTokens;

    public static readonly TokenUsage Empty = new();
}
```

### 2.4 ProviderCallMetrics

```csharp
/// <summary>单次 Provider 调用的度量指标。</summary>
public sealed record ProviderCallMetrics
{
    /// <summary>提供商名称。</summary>
    public string? ProviderName { get; init; }
    /// <summary>模型名称。</summary>
    public string? ModelName { get; init; }
    /// <summary>请求开始到响应完成的总延迟。</summary>
    public TimeSpan Latency { get; init; }
    /// <summary>请求开始到首 token 的延迟 (Time To First Token)。仅流式模式。</summary>
    public TimeSpan? TimeToFirstToken { get; init; }
    /// <summary>估算成本（美元）。</summary>
    public CostEstimate? Cost { get; init; }
    /// <summary>是否为降级调用。</summary>
    public bool IsFallback { get; init; }
    /// <summary>降级原因（原始错误）。</summary>
    public string? FallbackReason { get; init; }

    public static readonly ProviderCallMetrics Empty = new();
}

/// <summary>成本估算。</summary>
public sealed record CostEstimate
{
    public decimal InputCostUsd { get; init; }
    public decimal OutputCostUsd { get; init; }
    public decimal TotalCostUsd => InputCostUsd + OutputCostUsd;
}
```

### 2.5 ModelCapabilities

```csharp
/// <summary>模型能力声明。用于上层决策（是否启用 tool_use、vision 等）。</summary>
public sealed record ModelCapabilities
{
    public bool SupportsToolUse { get; init; }
    public bool SupportsParallelToolCalls { get; init; }
    public bool SupportsVision { get; init; }
    public bool SupportsJsonMode { get; init; }
    public bool SupportsStrictJsonSchema { get; init; }
    public int MaxContextWindow { get; init; }         // token 数
    public int? MaxOutputTokens { get; init; }

    public static readonly ModelCapabilities Unknown = new();
}
```

**设计决策 Q5 回答**：能力声明为**配置驱动**（非运行时探测）。理由：
- 不是所有 API 都提供能力查询端点
- 编译时声明更可靠，避免运行时不确定性
- Provider Options 中配置，启动时校验

---

## 3. 流式事件模型

### 3.1 设计决策（Q1 回答）

**选择**：**1 个 record + 判别字段**（而非 5 种独立 record）。

**理由**：
- `IAsyncEnumerable<StreamingChatEvent>` 的泛型参数是固定的，5 种独立 record 需要共同基类或 OneOf，增加复杂度
- 单 record 判别模式（类似 dawning-agents 的方式）配合 pattern matching 使用足够清晰
- 序列化/反序列化更简单（单一 JSON shape）
- 消费者代码可用 `switch (event.EventType)` 分派

### 3.2 StreamingChatEvent

```csharp
public sealed record StreamingChatEvent
{
    /// <summary>事件类型。</summary>
    public required StreamingEventType EventType { get; init; }

    // ── TextDelta ──
    /// <summary>增量文本（EventType == TextDelta 时有值）。</summary>
    public string? ContentDelta { get; init; }

    // ── ToolCallRequested ──
    /// <summary>工具调用增量（EventType == ToolCallDelta 时有值）。</summary>
    public ToolCallDelta? ToolCallDelta { get; init; }

    // ── RunCompleted ──
    /// <summary>完成原因（EventType == RunCompleted 时有值）。</summary>
    public string? FinishReason { get; init; }
    /// <summary>最终 token 用量（EventType == RunCompleted 时有值）。</summary>
    public TokenUsage? Usage { get; init; }
    /// <summary>调用度量（EventType == RunCompleted 时有值）。</summary>
    public ProviderCallMetrics? Metrics { get; init; }

    // ── Error ──
    /// <summary>错误信息（EventType == Error 时有值）。</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>错误代码（EventType == Error 时有值）。</summary>
    public string? ErrorCode { get; init; }

    // Factory methods
    public static StreamingChatEvent TextDelta(string content);
    public static StreamingChatEvent ToolDelta(ToolCallDelta delta);
    public static StreamingChatEvent Completed(string finishReason, TokenUsage usage, ProviderCallMetrics? metrics = null);
    public static StreamingChatEvent Errored(string message, string? code = null);
}

public enum StreamingEventType
{
    TextDelta,
    ToolCallDelta,
    RunCompleted,
    Error
}
```

**注意**：`ToolCallCompleted` 不在 Provider 层产生（它由 Agent Loop 在 tool 执行完后生成），所以 Provider 层只有 4 种事件类型。SC-7.2 中的 5 种是端到端视角，ToolCallCompleted 在 Layer 1 补充。

### 3.3 ToolCallDelta

```csharp
public sealed record ToolCallDelta
{
    public int Index { get; init; }                // 并行 tool call 的索引
    public string? Id { get; init; }               // 首个 delta 携带
    public string? FunctionName { get; init; }     // 首个 delta 携带
    public string? ArgumentsDelta { get; init; }   // 逐片段拼接
}
```

### 3.4 StreamingAccumulator

```csharp
/// <summary>
/// 从 ToolCallDelta 流中拼接完整的 ToolCall 列表。
/// 消费 IAsyncEnumerable<StreamingChatEvent> 后产出完整的 ChatCompletionResponse。
/// </summary>
public sealed class StreamingAccumulator
{
    public void Process(StreamingChatEvent event);
    public ChatCompletionResponse Build();
    public IReadOnlyList<ToolCall> GetCompletedToolCalls();
    public string GetAccumulatedContent();
}
```

---

## 4. Provider 降级设计

### 4.1 设计决策（Q2 回答）

**选择**：**Decorator 模式**（`FallbackLLMProvider` 包装多个 `ILLMProvider`）。

**理由**：
- 对消费者透明，拿到的仍然是 `ILLMProvider`
- 降级链可通过 DI 配置组装，不需要特殊 API
- 降级逻辑独立于具体 Provider，可单独测试
- 与 Polly 等弹性库的 ResiliencePipeline 模式一致

### 4.2 FallbackLLMProvider

```csharp
/// <summary>
/// 降级 Provider。按优先级尝试多个 Provider，第一个成功的返回结果。
/// </summary>
internal sealed class FallbackLLMProvider(
    IReadOnlyList<ILLMProvider> providers,
    FallbackOptions options,
    ILogger<FallbackLLMProvider> logger) : ILLMProvider
{
    // Name 返回 "fallback({primary},{secondary},...)"
    // ChatAsync 逐个尝试，捕获可降级异常
    // ChatStreamEventsAsync 逐个尝试，降级时从头开始新流
}
```

### 4.3 可降级异常判定

```csharp
public static class FallbackPolicy
{
    /// <summary>判断异常是否应触发降级。</summary>
    public static bool ShouldFallback(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => true,    // 429
        HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError } => true, // 5xx
        TaskCanceledException when !ct.IsCancellationRequested => true,  // 超时（非用户取消）
        LLMProviderException { IsTransient: true } => true,
        _ => false
    };
}
```

### 4.4 FallbackOptions

```csharp
public sealed record FallbackOptions
{
    /// <summary>降级链中的 Provider 名称列表（按优先级）。</summary>
    public IReadOnlyList<string> ProviderChain { get; init; } = [];
    /// <summary>每个 Provider 的最大重试次数（降级前）。</summary>
    public int MaxRetriesPerProvider { get; init; } = 2;
    /// <summary>重试基础延迟。</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(1);
    /// <summary>重试最大延迟。</summary>
    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromSeconds(30);
}
```

---

## 5. 成本估算设计

### 5.1 设计决策（Q3 回答）

**选择**：**配置驱动的价格表**（`IOptions<ModelPricingOptions>`）。

**理由**：
- 硬编码 → 每次价格变化都要发版本，不可接受
- 远程获取 → 引入外部依赖和延迟，过度设计
- 配置文件 → 用户通过 appsettings.json 或环境变量管理，平衡灵活性和简洁

### 5.2 ModelPricingOptions

```csharp
public sealed class ModelPricingOptions
{
    /// <summary>按模型名称的价格配置。</summary>
    public Dictionary<string, ModelPricing> Models { get; set; } = new();
}

public sealed record ModelPricing
{
    /// <summary>输入价格（美元 / 百万 tokens）。</summary>
    public decimal InputPricePerMillionTokens { get; init; }
    /// <summary>输出价格（美元 / 百万 tokens）。</summary>
    public decimal OutputPricePerMillionTokens { get; init; }
}
```

### 5.3 配置示例

```json
{
  "LLM": {
    "Pricing": {
      "Models": {
        "gpt-4o": { "InputPricePerMillionTokens": 2.50, "OutputPricePerMillionTokens": 10.00 },
        "gpt-4o-mini": { "InputPricePerMillionTokens": 0.15, "OutputPricePerMillionTokens": 0.60 },
        "claude-3.5-sonnet": { "InputPricePerMillionTokens": 3.00, "OutputPricePerMillionTokens": 15.00 },
        "llama3.2": { "InputPricePerMillionTokens": 0.0, "OutputPricePerMillionTokens": 0.0 }
      }
    }
  }
}
```

### 5.4 成本计算

```csharp
internal sealed class CostCalculator(IOptions<ModelPricingOptions> pricing)
{
    public CostEstimate? Calculate(string modelName, TokenUsage usage)
    {
        if (!pricing.Value.Models.TryGetValue(modelName, out var price))
            return null;  // 未配置价格的模型返回 null

        return new CostEstimate
        {
            InputCostUsd = usage.PromptTokens * price.InputPricePerMillionTokens / 1_000_000m,
            OutputCostUsd = usage.CompletionTokens * price.OutputPricePerMillionTokens / 1_000_000m
        };
    }
}
```

---

## 6. Provider 配置与 DI 注册

### 6.1 配置结构

```json
{
  "LLM": {
    "DefaultProvider": "azure-openai",
    "Fallback": {
      "ProviderChain": ["azure-openai", "openai", "ollama"],
      "MaxRetriesPerProvider": 2
    },
    "Providers": {
      "ollama": {
        "Endpoint": "http://localhost:11434",
        "Model": "llama3.2"
      },
      "openai": {
        "ApiKey": "${OPENAI_API_KEY}",
        "Model": "gpt-4o"
      },
      "azure-openai": {
        "Endpoint": "https://my-resource.openai.azure.com",
        "DeploymentName": "gpt-4o",
        "AuthMode": "AzureAD"
      },
      "anthropic": {
        "ApiKey": "${ANTHROPIC_API_KEY}",
        "Model": "claude-sonnet-4-20250514"
      }
    },
    "Pricing": { ... }
  }
}
```

### 6.2 DI 注册 API

```csharp
// 单 Provider
services.AddOllamaProvider(config.GetSection("LLM:Providers:ollama"));
services.AddOpenAIProvider(config.GetSection("LLM:Providers:openai"));
services.AddAzureOpenAIProvider(config.GetSection("LLM:Providers:azure-openai"));
services.AddAnthropicProvider(config.GetSection("LLM:Providers:anthropic"));

// 全自动（读取 LLM 配置节，自动注册所有 Provider + 降级链 + 成本计算）
services.AddLLMProviders(config.GetSection("LLM"));

// Lambda 配置
services.AddOllamaProvider(options =>
{
    options.Endpoint = "http://localhost:11434";
    options.Model = "llama3.2";
});
```

### 6.3 SC-7.5 验证：新 Provider 的最小实现

新 Provider 贡献者**只需要**：

1. **实现 `ILLMProvider`**（1 个接口，3 个方法 + 3 个属性）
2. **写 1 个 DI 扩展**（`AddXxxProvider()`）
3. **通过 Provider 契约测试**（继承 `ProviderContractTests` 基类）

```csharp
// 1. 实现接口
public sealed class MistralProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<MistralOptions> options,
    ILogger<MistralProvider> logger) : ILLMProvider
{
    public string Name => "mistral";
    public string Model => options.Value.Model;
    public ModelCapabilities Capabilities => new() { SupportsToolUse = true, ... };

    public async Task<ChatCompletionResponse> ChatAsync(...) { ... }
    public async IAsyncEnumerable<string> ChatStreamAsync(...) { ... }
    public async IAsyncEnumerable<StreamingChatEvent> ChatStreamEventsAsync(...) { ... }
}

// 2. DI 扩展
public static class MistralServiceCollectionExtensions
{
    public static IServiceCollection AddMistralProvider(
        this IServiceCollection services, IConfigurationSection config)
    {
        services.AddOptions<MistralOptions>().Bind(config).ValidateOnStart();
        services.AddSingleton<ILLMProvider, MistralProvider>();
        return services;
    }
}

// 3. 契约测试
public class MistralProviderContractTests : ProviderContractTests
{
    protected override ILLMProvider CreateProvider() => /* setup */;
}
```

---

## 7. 异常层次

```csharp
/// <summary>所有 Provider 异常的基类。</summary>
public class LLMProviderException : Exception
{
    public string ProviderName { get; }
    public bool IsTransient { get; }     // 可重试/可降级
}

/// <summary>认证失败（API Key 无效、Azure AD token 过期等）。</summary>
public class LLMAuthenticationException : LLMProviderException
{
    // IsTransient = false（认证问题不应重试）
}

/// <summary>速率限制 (429)。</summary>
public class LLMRateLimitException : LLMProviderException
{
    public TimeSpan? RetryAfter { get; }
    // IsTransient = true
}

/// <summary>模型不存在或不可用。</summary>
public class LLMModelNotFoundException : LLMProviderException
{
    // IsTransient = false
}

/// <summary>内容安全过滤触发。</summary>
public class LLMContentFilterException : LLMProviderException
{
    // IsTransient = false
}

/// <summary>上下文窗口溢出。</summary>
public class LLMContextOverflowException : LLMProviderException
{
    public int RequestedTokens { get; }
    public int MaxTokens { get; }
    // IsTransient = false
}
```

---

## 8. NuGet 包结构

```
Dawning.AgentOS.Abstractions          ← ILLMProvider, records, enums（零依赖）
Dawning.AgentOS.Core                  ← FallbackLLMProvider, CostCalculator, StreamingAccumulator
Dawning.AgentOS.Drivers.Ollama        ← OllamaProvider + AddOllamaDriver()
Dawning.AgentOS.Drivers.OpenAI        ← OpenAIProvider + OpenAICompatibleProvider + AddOpenAIDriver()
Dawning.AgentOS.Drivers.Azure         ← AzureOpenAIProvider + AddAzureOpenAIDriver()
Dawning.AgentOS.Drivers.Anthropic     ← AnthropicProvider + AddAnthropicDriver()
Dawning.AgentOS.Testing               ← InMemoryProvider + RecordingProvider + DriverContractTests
```

---

## 9. 开放问题

| # | 问题 | 当前倾向 | 待确认 |
|---|------|---------|--------|
| Q4 | 上下文窗口管理放 Provider 还是独立服务？ | 独立服务（`IContextWindowManager`），但 Provider 通过 `Capabilities.MaxContextWindow` 提供窗口大小 | Layer 2 设计时确定 |
| Q7 | 流中断恢复机制 | P2 优先级延后。初版依赖重试（整个请求重发）。SSE Last-Event-Id 恢复仅 OpenAI 支持且不保证可靠 | 实现时验证 |
| Q8 | Structured Output 在 Anthropic 上的兜底 | 在 system prompt 中追加格式说明 + 响应后 JSON validation + 一次自动重试 | 实现时验证 |

---

## 交叉引用

- [[decisions/layer-0-requirements.zh-CN]] — 需求说明
- [[decisions/layer-0-features.zh-CN]] — 功能清单
- [[decisions/success-criteria.zh-CN]] — SC-7 验收标准
- [[decisions/phase-0-overview]] — 原始架构概览与 Function Calling 决策记录
- [[decisions/roadmap.zh-CN]] — Layer 0 在分层路线图中的位置
