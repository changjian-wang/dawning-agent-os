# Semantic Kernel 详细分析

> 微软的模型无关 SDK，.NET 生态中最成熟的 AI Agent 开发框架。

---

## 基本信息

| 属性 | 值 |
|------|-----|
| **官方名称** | Semantic Kernel |
| **维护者** | Microsoft |
| **仓库** | https://github.com/microsoft/semantic-kernel |
| **文档** | https://learn.microsoft.com/en-us/semantic-kernel/ |
| **语言** | C#（66.8%）、Python（31.3%）、Java |
| **许可证** | MIT |
| **Stars** | 27.7k |
| **贡献者** | 435 |
| **NuGet 包数** | 43 |
| **最新版本** | Python 1.41.1 / .NET 持续更新 |
| **系统要求** | .NET 10.0+ / Python 3.10+ / JDK 17+ |

---

## 1. 定位与背景

Semantic Kernel 是微软自 2023 年初发布的模型无关 AI SDK，定位为企业开发者构建 AI 应用的基础设施层。它是 .NET 生态中采用最广泛的 AI 框架，拥有最丰富的 NuGet 包生态。

**关键定位**：
- **不是 Agent 框架**（Agent 层已迁移至 MAF）
- 而是 Agent 框架的**底层基础设施** — Kernel + Plugin + Connectors
- 类比关系：SK 之于 MAF ≈ ASP.NET Core 之于具体微服务

---

## 2. 架构设计

### 2.1 核心架构

```
┌─────────────────────────────────────────┐
│              Applications                │
├─────────────────────────────────────────┤
│     Agent Framework (→ MAF)    │  Agents │
├────────────────┬────────────────────────┤
│   Planners     │     Process Framework   │
├────────────────┼────────────────────────┤
│              Kernel                      │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ │
│  │ Plugins  │ │ Memory   │ │Connectors│ │
│  └──────────┘ └──────────┘ └──────────┘ │
├─────────────────────────────────────────┤
│           AI Services (LLMs)             │
│  OpenAI | Azure OpenAI | HuggingFace    │
│  Ollama | NVIDIA NIM | LMStudio | ONNX  │
└─────────────────────────────────────────┘
```

### 2.2 Kernel — 核心引擎

Kernel 是 SK 的中央协调器：
- 管理 Plugin 注册和调用
- 处理 AI 服务（LLM）连接
- 编排函数调用链
- 管理执行设置和参数

```csharp
var builder = Kernel.CreateBuilder();
builder.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
var kernel = builder.Build();
```

### 2.3 Plugin 系统 — 核心优势

Plugin 是 SK 最大的差异化特性：

| Plugin 类型 | 描述 | 示例 |
|------------|------|------|
| **原生代码 Plugin** | C#/Python 函数 + 属性标注 | `[KernelFunction]` |
| **Prompt 模板 Plugin** | 参数化 Prompt 模板 | Handlebars/Liquid 模板 |
| **OpenAPI Plugin** | 从 OpenAPI 规范自动生成 | REST API 自动集成 |
| **MCP Plugin** | Model Context Protocol 集成 | 外部 MCP 服务器 |

```csharp
// 原生代码 Plugin 示例
sealed class MenuPlugin
{
    [KernelFunction, Description("Provides a list of specials from the menu.")]
    public string GetSpecials() => "Special Soup: Clam Chowder";

    [KernelFunction, Description("Provides the price of the requested menu item.")]
    public string GetItemPrice(
        [Description("The name of the menu item.")] string menuItem) => "$9.99";
}

// 注册 Plugin
kernel.Plugins.Add(KernelPluginFactory.CreateFromType<MenuPlugin>());
```

---

## 3. 核心特性

### 3.1 Agent 框架

虽然 Agent 层正在迁移至 MAF，SK 仍提供完整的 Agent 能力：

```csharp
// 创建 Agent
ChatCompletionAgent agent = new()
{
    Name = "SK-Agent",
    Instructions = "You are a helpful assistant.",
    Kernel = kernel,
};

// 调用 Agent
await foreach (var response in agent.InvokeAsync("Write a haiku."))
{
    Console.WriteLine(response.Message);
}
```

### 3.2 多 Agent 系统

通过 **Agent 即 Plugin** 模式实现多 Agent 协作：

```python
# Python 多 Agent 示例
triage_agent = ChatCompletionAgent(
    service=OpenAIChatCompletion(),
    name="TriageAgent",
    instructions="Evaluate requests and forward to specialists.",
    plugins=[billing_agent, refund_agent],  # Agent 作为 Plugin
)
```

### 3.3 Process Framework

用于建模复杂业务流程的结构化工作流引擎：
- 步骤定义
- 状态管理
- 条件分支
- 错误处理

### 3.4 Memory / Vector DB 集成

| 向量数据库 | 支持状态 |
|-----------|---------|
| Azure AI Search | ✅ |
| Elasticsearch | ✅ |
| Chroma | ✅ |
| Qdrant | ✅ |
| Pinecone | ✅ |
| Weaviate | ✅ |
| Redis | ✅ |
| PostgreSQL (pgvector) | ✅ |

### 3.5 多模态支持

- 文本输入/输出
- 视觉（图像）输入
- 音频输入
- 结构化输出（Structured Output）

### 3.6 可观测性

- OpenTelemetry 集成
- 分布式追踪
- 性能指标收集

---

## 4. NuGet 包生态

SK 的 43 个 NuGet 包构成了 .NET AI 开发的核心基础设施：

### 4.1 核心包

| 包名 | 用途 |
|------|------|
| `Microsoft.SemanticKernel` | 主包 |
| `Microsoft.SemanticKernel.Abstractions` | 接口 / 抽象 |
| `Microsoft.SemanticKernel.Core` | 核心实现 |
| `Microsoft.SemanticKernel.Agents.Core` | Agent 核心 |

### 4.2 AI 连接器

| 包名 | 提供商 |
|------|--------|
| `Connectors.OpenAI` | OpenAI / Azure OpenAI |
| `Connectors.HuggingFace` | Hugging Face |
| `Connectors.Ollama` | Ollama（本地） |
| `Connectors.Onnx` | ONNX Runtime |

### 4.3 向量存储连接器

| 包名 | 数据库 |
|------|--------|
| `Connectors.AzureAISearch` | Azure AI Search |
| `Connectors.Chroma` | Chroma |
| `Connectors.Elasticsearch` | Elasticsearch |
| `Connectors.Qdrant` | Qdrant |
| `Connectors.Redis` | Redis |
| `Connectors.Postgres` | PostgreSQL |

---

## 5. LLM 提供商支持

| 提供商 | 支持方式 |
|--------|---------|
| Azure OpenAI | ✅ 一等公民 |
| OpenAI | ✅ 一等公民 |
| Hugging Face | ✅ 连接器 |
| NVIDIA NIM | ✅ 连接器 |
| Ollama | ✅ 连接器（本地） |
| LM Studio | ✅ OpenAI 兼容 |
| ONNX Runtime | ✅ 连接器（本地） |
| Google Gemini | ✅ 连接器 |
| Anthropic Claude | ✅ 连接器 |
| Mistral | ✅ 连接器 |

---

## 6. 与其他微软产品的关系

```
Microsoft AI 产品矩阵：

Semantic Kernel ──── MAF ──── Azure AI Foundry
    (SDK 基础设施)    (Agent 编排)    (云端 AI 服务)
         │                │               │
         └── Plugin 层 ──→│               │
                          └── 部署层 ────→│
                                          │
                          ┌── Copilot Studio
                          │   (低代码 Agent 平台)
                          │
                          └── M365 Agents SDK
                              (M365/Teams 渠道)
```

---

## 7. 社区与生态

| 指标 | 值 |
|------|-----|
| GitHub Stars | 27.7k |
| 贡献者 | 435 |
| NuGet 包 | 43 |
| 发布次数 | 263 |
| Discord 社区 | 活跃 |
| 学习资源 | learn.microsoft.com 完整文档 |
| Office Hours | 定期社区活动 |

---

## 8. 优势与不足

### 优势
1. **.NET 生态中最成熟的 AI SDK** — 3 年积累，43 个 NuGet 包
2. **Plugin 系统** — 最丰富的工具集成模式（原生、Prompt、OpenAPI、MCP）
3. **三语言支持** — C# + Python + Java
4. **向量数据库生态** — 8+ 主流向量数据库连接器
5. **企业采用率最高** — 微软官方推荐 .NET AI 入口
6. **学习资源丰富** — 官方文档、教程、社区活跃

### 不足
1. **Agent 层正在迁移至 MAF** — 未来 Agent 能力可能降级
2. **无原生分布式运行时** — 需要外部托管
3. **Kernel 抽象** — 不是纯 DI，有自己的 Kernel 容器概念
4. **概念多** — Kernel、Plugin、Planner、Process、Agent 概念层较多

---

## 9. 对 Dawning 的启示

| 借鉴点 | 详情 | 映射到 Dawning |
|--------|------|---------------|
| Plugin 架构 | 四种 Plugin 类型的统一抽象 | IToolRegistry 设计 |
| NuGet 包拆分 | Abstractions / Core / Connectors 分层 | 包结构设计 |
| `[KernelFunction]` 属性 | 声明式工具定义 | `[AgentTool]` 特性设计 |
| 连接器模式 | 一致的 AI 服务连接器接口 | ILLMProvider 实现 |
| 结构化输出 | Generic 类型化的输出模式 | Agent 输出类型化 |
| 向量存储抽象 | 统一的向量 DB 接口 | 记忆面存储抽象 |

**关键洞察**：SK 的 Plugin 模式是 .NET AI 开发的事实标准。Dawning 的 ITool/IToolRegistry 应与 SK Plugin 模式保持概念兼容，以降低迁移成本。

---

## 10. 源码结构解析

### 10.1 仓库地址

https://github.com/microsoft/semantic-kernel

### 10.2 .NET 源码目录 (`dotnet/src/`)

```
dotnet/src/
├── SemanticKernel.Abstractions/       # 🔵 核心抽象（零依赖接口包）
│   └── (IKernel, KernelFunction, KernelPlugin, IPromptTemplate)
├── SemanticKernel.Core/               # 🔵 核心实现
│   └── (Kernel 引擎、函数调用、Plugin 加载)
├── SemanticKernel.MetaPackage/        # 📦 元包（一站式引用）
├── Agents/                            # 🟢 Agent 框架（迁移至 MAF 中）
│   └── (ChatCompletionAgent, AgentChat, AgentThread)
├── Connectors/                        # 🟣 AI 服务连接器
│   ├── OpenAI/
│   ├── AzureOpenAI/
│   ├── HuggingFace/
│   ├── Ollama/
│   ├── Onnx/
│   ├── Google/
│   └── MistralAI/
├── VectorData/                        # 💾 向量数据存储抽象 + 实现
│   ├── Abstractions/
│   ├── AzureAISearch/
│   ├── Chroma/
│   ├── Elasticsearch/
│   ├── Qdrant/
│   ├── Redis/
│   ├── Postgres/
│   └── Weaviate/
├── Functions/                         # ⚙️ 函数系统
│   └── (KernelFunction 实现、函数调用行为)
├── Plugins/                           # 🔌 内置插件
│   └── (Web、File 等内置工具)
├── Extensions/                        # 🧩 扩展包
│   └── (DI 扩展、配置扩展)
├── Experimental/                      # 🧪 实验性功能
├── InternalUtilities/                 # 🔧 内部工具
└── IntegrationTests/                  # 🧪 集成测试
```

### 10.3 Python 源码目录 (`python/semantic_kernel/`)

```
python/semantic_kernel/
├── agents/              # Agent 框架
├── connectors/          # AI 服务连接器
├── contents/            # 内容类型（消息、文件等）
├── core_plugins/        # 核心插件
├── data/                # 向量数据
├── exceptions/          # 异常定义
├── filters/             # 请求/响应过滤器
├── functions/           # 函数系统
├── memory/              # 记忆系统
├── processes/           # Process Framework
├── prompt_template/     # Prompt 模板引擎
├── reliability/         # 可靠性（重试等）
├── services/            # 服务抽象
├── template_engine/     # 模板引擎
├── utils/               # 工具函数
├── kernel.py            # Kernel 主类
└── __init__.py          # 版本 1.41.1
```

### 10.4 架构洞察

1. **Abstractions / Core 分包**：经典的 .NET 接口/实现分离模式（Dawning 已遵循）
2. **Connectors 按提供商分包**：每个 LLM 提供商一个独立 NuGet 包
3. **VectorData 独立体系**：向量存储有自己的 Abstractions 包和 8+ 实现包
4. **Agent 层正在迁移**：`Agents/` 目录仍在但逐步移至 MAF
5. **Python 结构镜像**：Python 包结构几乎是 .NET 的 1:1 映射
6. **43 个 NuGet 包**的细粒度拆分是 .NET 生态中最细致的 AI SDK 包结构

---

*文档版本：1.1 | 最后更新：2026-04-07*
