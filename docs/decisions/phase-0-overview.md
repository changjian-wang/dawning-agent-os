# Phase 0: AI Agent Framework 全景概览

> **历史文档**：本文档创建于项目初期（Agent Framework 时期），记录从零构建的技术决策和原因。
> 项目已于 2026-04-17 从 "Agent Framework" 演进为 **"Agent OS"**（操作系统），
> 详见 [[concepts/agent-os-architecture.zh-CN]]。
>
> LLM Provider（现称 LLM Driver）相关的详细规格已迁移至 Layer 0 文档（[[decisions/layer-0-tech-spec.zh-CN]]），
> Layer 1+ 的概念（Tool、Memory、Orchestrator 等）仍以本文档为参考。
>
> 命名空间已从 `Dawning.AgentFramework.*` 变更为 `Dawning.AgentOS.*`。

## 目录

1. [什么是 AI Agent？](#1-什么是-ai-agent)
2. [为什么需要 Agent Framework？](#2-为什么需要-agent-framework)
3. [框架定位：三大方向](#3-框架定位三大方向)
4. [核心概念清单](#4-核心概念清单)
5. [架构设计原则](#5-架构设计原则)
6. [技术栈选择与理由](#6-技术栈选择与理由)
7. [项目模块划分与理由](#7-项目模块划分与理由)
8. [Agent 如何调用工具？——Function Calling 决策记录](#8-agent-如何调用工具function-calling-决策记录)
9. [执行流程详解](#9-执行流程详解)

---

## 1. 什么是 AI Agent？

**定义**：AI Agent 是一个能够自主感知环境、做出决策、执行动作的程序实体。与普通的 LLM 聊天不同，Agent 具备：

| 能力 | 普通 LLM 聊天 | AI Agent |
|------|--------------|----------|
| 文本生成 | ✅ | ✅ |
| 工具调用 | ❌ | ✅ 能调用外部函数/API |
| 多步推理 | ❌ 一次性回答 | ✅ 循环执行直到完成 |
| 记忆 | ❌ 无状态 | ✅ 短期记忆 + 长期记忆 |
| 自我修正 | ❌ | ✅ 观察结果后调整策略 |
| 委托协作 | ❌ | ✅ 多 Agent 分工协作 |
| 分布式 | ❌ | ✅ 可跨进程/机器运行 |

**类比**：
- LLM = 一个很聪明的大脑（只能思考，不能动手）
- 单 Agent = 一个完整的人（大脑 + 眼睛 + 手 + 记忆）
- 多 Agent 协作 = 一个团队（架构师 + 开发者 + 测试员，各有专长，共享知识库）

---

## 2. 为什么需要 Agent Framework？

不用框架也能写 Agent，但会面临以下问题：

### 2.1 没有框架的写法（原始方式）

```csharp
// 伪代码：手写 Agent 循环
var messages = new List<ChatMessage> { ChatMessage.User("查询最近的订单") };

for (int step = 0; step < 10; step++)  // 硬编码最大步数
{
    var response = await openAIClient.ChatAsync(messages);  // 硬编码 OpenAI
    
    if (response.HasToolCalls)
    {
        foreach (var toolCall in response.ToolCalls)
        {
            if (toolCall.Name == "query_orders")
            {
                var result = await QueryOrders(toolCall.Arguments);
                messages.Add(/* tool result */);
            }
        }
    }
    else
    {
        Console.WriteLine(response.Content);
        break;
    }
}
```

### 2.2 这种写法的问题

| 问题 | 说明 |
|------|------|
| **LLM 提供商耦合** | 直接用 openAIClient，换了 Azure/Ollama 要改所有代码 |
| **工具管理混乱** | if-else 或 switch-case 匹配工具，新增工具要改核心循环 |
| **无监控** | 没有日志、没有追踪、没有成本记录 |
| **无安全机制** | 没有最大步数限制、没有成本上限、没有危险操作确认 |
| **无记忆管理** | 只有当前会话的消息列表，无法跨会话积累知识 |
| **无法复用** | 每个 Agent 都要重复写这个循环 |
| **单点运行** | Agent 只能在当前进程内执行，无法跨机器协作 |
| **测试困难** | 所有依赖都是具体类，无法 Mock |

### 2.3 Framework 提供的价值

| 价值 | 实现方式 |
|------|---------|
| **LLM 抽象** | `ILLMProvider` 接口，一行切换提供商 |
| **工具系统** | `ITool` + `IToolRegistry`，声明式注册，自动发现 |
| **可观测性** | OpenTelemetry + ILogger，全链路追踪（跨 Agent、跨机器） |
| **安全护栏** | MaxSteps, MaxCost, RequiresConfirmation |
| **双层记忆** | 短期（`IWorkingMemory` + 自动压缩）+ 长期（`ILongTermMemory`，预留接口，待定） |
| **多 Agent 协作** | `IOrchestrator` 编排 + Handoff 移交 + 共享长期记忆 |
| **分布式运行** | Agent 可跨进程/机器部署，通过消息传递协作 |
| **DI 可测试** | 所有依赖通过接口注入，100% 可 Mock |

---

## 3. 框架定位：三大方向

本框架不是一个简单的 "单 Agent 聊天" 框架。核心定位是：

### 3.1 多 Agent 协作

**解决的问题**：复杂任务无法由单个 Agent 完成，需要多个专业 Agent 分工协作。

```
用户: "分析这个项目的代码质量，修复关键 bug，并生成测试报告"

编排器 (Orchestrator)
  ├── 代码审计 Agent → 分析代码，发现 5 个问题
  │     ↓ 把发现存入共享记忆
  ├── 修复 Agent → 从记忆中读取问题列表，逐个修复
  │     ↓ 把修复结果存入共享记忆
  └── 测试 Agent → 从记忆中读取修改内容，生成测试，运行验证
```

**关键机制**：
- **Orchestrator（编排器）**：管理 Agent 的执行顺序和数据流（顺序、并行、条件分支、图）
- **Handoff（移交）**：Agent A 判断当前任务应交给 Agent B，主动移交控制权
- **共享记忆**：所有 Agent 通过长期记忆共享发现和决策

**竞品参考**：CrewAI（Crew/Task）、MS Agent Framework（Graph Workflows）

### 3.2 长期记忆

> **状态：设计探索中** — 长期记忆的实现方案仍在评估，向量数据库方案存在已知局限（详见 [concepts/context-management.md](../concepts/context-management.md)）。框架预留 `ILongTermMemory` 接口，待方案成熟后实现。

**解决的问题**：Agent 需要跨会话积累知识，而不是每次从零开始。

**双层记忆架构**：

![双层记忆架构](../images/decisions/01-dual-layer-memory.png)

**为什么需要长期记忆**：

| 场景 | 没有长期记忆 | 有长期记忆 |
|------|------------|-----------|
| 客服 Agent | 每次客户来都不记得之前的问题 | 自动检索该客户的历史记录 |
| 代码审计 Agent | 每次从头分析整个项目 | 记得上次审计的发现，只关注变更部分 |
| 多 Agent 协作 | Agent 之间传递大量上下文 | 通过共享记忆交换关键事实 |

**竞品参考**：CrewAI（Memory）、LangMem

### 3.3 分布式 Agent

**解决的问题**：Agent 需要运行在不同进程/机器/容器上，远程协作。

**为什么需要分布式**：

| 场景 | 为什么不能单进程 |
|------|-----------------|
| Agent 需要 GPU 机器运行本地模型 | GPU 机器和 Web 服务器是不同的机器 |
| Agent 执行时间很长（几小时） | 不能阻塞 Web 请求线程 |
| 不同 Agent 有不同的安全权限 | 高权限 Agent 运行在隔离环境中 |
| 高可用 / 可扩展 | 多个 Agent 实例负载均衡 |
| 跨组织协作 | Agent 运行在不同公司的基础设施上（A2A Protocol） |

![分布式 Agent 架构](../images/decisions/02-distributed-architecture.png)

**竞品参考**：MS Agent Framework（Durable Agents、A2A Protocol）、AutoGen

### 3.4 三者的关系

![三支柱关系](../images/decisions/03-three-pillars.png)

---

## 4. 核心概念清单

### 4.1 Agent（代理）

**是什么**：执行任务的独立实体，包含名称、指令（system prompt）、可用工具列表。

**关键属性**：
- `Name` — 代理身份标识，多 Agent 场景中用于路由和寻址
- `Instructions` — 系统提示词，定义 Agent 的行为边界
- `MaxSteps` — 最大执行步数，防止无限循环（安全护栏）
- `Tools` — 该 Agent 可用的工具列表
- `MemoryScope` — 该 Agent 的记忆作用域（可共享或私有）

**为什么需要 Instructions**：
LLM 是通用的，Instructions 把它变成了专用的。比如 "你是一个 SQL 专家，只回答数据库相关问题" 就限制了 Agent 的行为边界。

### 4.2 LLM Provider（大模型提供商）

**是什么**：对 LLM API 的统一封装，屏蔽不同提供商的 API 差异。

**三个核心方法**：
1. `ChatAsync` — 非流式对话，等待完整响应（简单场景）
2. `ChatStreamAsync` — 流式对话，返回 `IAsyncEnumerable<string>`（实时显示文本）
3. `ChatStreamEventsAsync` — 结构化流式事件（工具调用需要）

**为什么要抽象成接口**：
OpenAI、Azure OpenAI、Ollama、Claude 的 API 格式都不同。接口抽象让上层 Agent 代码完全不关心用的是哪个 LLM。切换提供商只需改 DI 注册，零代码修改。

### 4.3 ChatMessage（聊天消息）

**是什么**：LLM 对话的基本单元，遵循 OpenAI 的消息格式（已成为事实标准）。

**角色类型**：
- `system` — 系统指令，定义 Agent 行为
- `user` — 用户输入
- `assistant` — LLM 回复
- `tool` — 工具执行结果

**为什么用 record 而不是 class**：
- record 是值语义，不可变（immutable），天然线程安全
- 分布式场景下序列化/反序列化简单
- 自带 `with` 表达式，方便复制修改

### 4.4 Tool（工具）

**是什么**：Agent 可以调用的外部能力，本质就是一个函数。

**核心属性**：
- `Name` — 工具名称，LLM 看到的函数名
- `Description` — LLM 看到的函数描述（直接影响 LLM 选择是否调用）
- `ParametersSchema` — JSON Schema 格式的参数定义
- `RequiresConfirmation` — 是否需要人工确认（安全机制）
- `RiskLevel` — Low/Medium/High 风险等级

### 4.5 ToolRegistry（工具注册表）

**是什么**：管理所有可用工具的中央仓库。

**为什么拆分 IToolReader 和 IToolRegistrar**：
接口隔离原则（ISP）。Agent 执行时只需要读工具（IToolReader），不需要注册能力。这样 Agent 无法在运行时偷偷注册恶意工具。

### 4.6 AgentContext（执行上下文）

**是什么**：单次 Agent 运行的所有上下文信息。

**关键字段**：
- `SessionId` — 会话标识，用于关联多轮对话
- `AgentId` — Agent 实例标识，分布式场景下用于寻址
- `UserId` — 用户标识，用于审计和限流
- `ParentAgentId` — 父 Agent 标识（被谁委派的任务），跨 Agent 追踪的基础
- `Steps` — 执行历史，可回溯每一步的思考和行为
- `MaxSteps` — 步数上限（防止无限循环）
- `Metadata` — 扩展元数据

### 4.7 双层记忆系统

#### 短期记忆（Working Memory）

**核心接口**：`IWorkingMemory`
- 存储当前会话的 `ChatMessage` 列表
- 接近上下文窗口上限时，`IContextCompactor` 自动压缩（LLM 生成摘要）
- 会话结束可持久化，支持 Session Resume

**类比**：人的工作记忆——当前任务的临时信息，容量有限，超了就概括。

#### 长期记忆（Long-term Memory）

**核心接口**：`ILongTermMemory`
- `RememberAsync(content, scope?, importance?)` — 存入记忆
- `RecallAsync(query, scope?, limit?)` — 语义检索
- `ExtractMemoriesAsync(text)` — 从长文本中提取原子事实
- `ForgetAsync(scope)` — 清除指定范围的记忆

**关键特性**：
- **层级作用域（Scope Tree）**：记忆按 `/project/alpha`、`/agent/researcher` 等路径组织
- **复合评分**：`score = semantic × w1 + recency × w2 + importance × w3`
- **自动去重合并（Consolidation）**：新记忆和已有记忆相似度过高时，LLM 决定合并/更新/保留
- **多 Agent 共享**：多个 Agent 可读写同一个 scope；也可以用 MemoryScope 隔离私有记忆
- **远程存储**：分布式场景下使用 Redis / PostgreSQL+pgvector 等远程后端

**类比**：人的长期记忆——积累的知识，按语义检索，有的重要有的淡忘。

#### 两层如何协同

```
会话开始:
  1. recall() 从长期记忆检索与任务相关的知识 → 注入 system prompt
  2. 如果有未完成的会话 → Session Resume 恢复短期记忆

会话进行中:
  3. 短期记忆正常运作（消息列表 + 自动压缩）
  4. Agent 可随时 remember() 向长期记忆存入重要发现

会话结束:
  5. extract_memories() 从本次对话中提取关键事实
  6. 存入长期记忆
  7. 短期记忆可选持久化（供 Session Resume 使用）
```

### 4.8 Orchestrator（编排器）

**是什么**：管理多个 Agent 协作完成复杂任务。

**编排模式**：
- **Sequential**（顺序）：A → B → C，流水线
- **Parallel**（并行）：A, B, C 同时执行，合并结果
- **Graph**（图）：任意 DAG，支持条件分支和循环
- **Dynamic**（动态）：编排器自身也是 Agent，由 LLM 决定下一步调度哪个 Agent

**Handoff（移交）**：
Agent A 判断 "这个任务应该交给数据库专家"，通过 Handoff 将控制权转给 Agent B。

### 4.9 Agent Communication（Agent 通信）

**是什么**：Agent 之间交换信息的机制。

| 模式 | 适用场景 | 实现方式 |
|------|---------|---------|
| **进程内** | 同一进程的多个 Agent | 方法调用 / 内存消息队列 |
| **跨进程** | Agent 运行在不同机器上 | 消息总线（Redis Pub/Sub / RabbitMQ / gRPC） |

**为什么需要统一接口**：
Agent 代码不应该关心对方在本机还是远程。`IAgentChannel` 统一封装通信细节，通过 DI 切换本地或远程实现。

---

## 5. 架构设计原则

### 5.1 Abstractions 与 Core 分离

> **历史命名**：以下命名空间已更新为 `Dawning.AgentOS.*`，见 [[concepts/agent-os-architecture.zh-CN]]。

```
Dawning.AgentFramework.Abstractions  → 接口、record、enum（零依赖包）
Dawning.AgentFramework.Core          → 实现类 + DI 扩展方法
```

**为什么**：
- Abstractions 包零依赖，任何项目都可以引用（轻量）
- 第三方扩展只需引用 Abstractions，不需要依赖 Core 的实现细节
- 遵循依赖倒置原则（DIP）
- 参考：`Microsoft.Extensions.Logging.Abstractions` vs `Microsoft.Extensions.Logging`

### 5.2 纯 DI 架构

**规则**：所有运行时服务通过构造函数注入，禁止：
- `new XxxService()` — 无法 Mock，无法替换
- 静态工厂 `XxxService.Create()` — 隐藏依赖
- Service Locator `serviceProvider.GetService<T>()` — 运行时才发现依赖缺失

```csharp
// ✅ 可测试、可替换
public class MyAgent(ILLMProvider llm, ILongTermMemory memory, ILogger<MyAgent> logger)
{
    // llm 可以是 OpenAI, Azure, Ollama, 或 Mock
    // memory 可以是 Redis, PostgreSQL, 或 InMemory (测试)
}
```

### 5.3 配置驱动

通过 `appsettings.json` + 环境变量切换行为：

```json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "llama3.2"
  },
  "Memory": {
    "LongTerm": {
      "Backend": "InMemory"
    }
  }
}
```

生产环境通过环境变量覆盖：
```bash
LLM__ProviderType=AzureOpenAI
Memory__LongTerm__Backend=PostgreSQL
Memory__LongTerm__ConnectionString=xxx
```

**为什么**：12-Factor App 原则。开发环境用 InMemory + Ollama，生产用 PostgreSQL+pgvector + Azure OpenAI。

### 5.4 Options 模式（IOptions / IValidatableOptions）

```csharp
public class AgentOptions : IValidatableOptions
{
    public string Name { get; set; } = "Agent";
    public int MaxSteps { get; set; } = 10;
    
    public void Validate()
    {
        if (MaxSteps <= 0) throw new ArgumentException("MaxSteps must be > 0");
    }
}
```

**为什么**：集中管理配置，支持热重载，启动时验证，遵循 ASP.NET Core 惯例。

---

## 6. 技术栈选择与理由

| 技术 | 选择 | 理由 |
|------|------|------|
| 运行时 | .NET 10 | 最新 LTS，性能最好，C# 13 语言特性 |
| LLM 客户端 | 原生 HttpClient | 不绑定特定 SDK，保持灵活性 |
| HTTP 工厂 | IHttpClientFactory | 连接池管理，避免 Socket 耗尽 |
| DI | Microsoft.Extensions.DI | .NET 生态标准，轻量 |
| 日志 | ILogger<T> | .NET 标准日志抽象 |
| 配置 | IOptions<T> | 支持 appsettings + 环境变量 + 热重载 |
| 追踪 | OpenTelemetry | CNCF 标准，支持跨 Agent 分布式追踪 |
| 流式 | IAsyncEnumerable | C# 原生异步流，自带背压 |
| 序列化 | System.Text.Json | .NET 内建，高性能 |
| 向量存储 | 接口抽象 | 开发用 InMemory，生产用 PostgreSQL+pgvector / Redis |
| 消息传递 | 接口抽象 | 开发用进程内消息，生产用 Redis Pub/Sub / RabbitMQ |

---

## 7. 项目模块划分与理由

> **历史命名**：以下包名已更新为 `Dawning.AgentOS.*`，完整映射见 [[concepts/agent-os-architecture.zh-CN]]。

```
src/
├── Dawning.AgentFramework.Abstractions/       # 接口层（零依赖）
│     IAgent, ILLMProvider, ITool, IWorkingMemory, 
│     ILongTermMemory, IOrchestrator, IAgentChannel
├── Dawning.AgentFramework.Core/               # 核心实现 + DI 扩展
│     AgentRunner, ContextCompactor, ToolExecutor
├── Dawning.AgentFramework.OpenAI/             # OpenAI Provider
├── Dawning.AgentFramework.Azure/              # Azure OpenAI Provider  
├── Dawning.AgentFramework.Anthropic/          # Claude Provider
├── Dawning.AgentFramework.OpenTelemetry/      # 可观测性（跨 Agent 追踪）
├── Dawning.AgentFramework.Memory.Redis/       # Redis 长期记忆后端
├── Dawning.AgentFramework.Memory.Postgres/    # PostgreSQL+pgvector 长期记忆后端
├── Dawning.AgentFramework.Memory.Sqlite/      # SQLite 长期记忆后端（轻量/开发用）
├── Dawning.AgentFramework.Transport.Redis/    # Redis Pub/Sub Agent 通信
├── Dawning.AgentFramework.Transport.RabbitMQ/ # RabbitMQ Agent 通信
├── Dawning.AgentFramework.MCP/                # Model Context Protocol
tests/
├── Dawning.AgentFramework.Abstractions.Tests/
├── Dawning.AgentFramework.Core.Tests/
├── ...
```

**拆包原则**：
- 每个 NuGet 包一个职责，用户按需引用
- Abstractions 零依赖 → 任何包都可以引用
- Memory 后端和 Transport 后端独立拆包——开发用 InMemory/Sqlite，生产按需选择

---

## 8. Agent 如何调用工具？——Function Calling 决策记录

> **决策结果**：本框架使用 Function Calling 作为**唯一**的工具调用机制。不实现 ReAct 文本解析，不保留 ReAct 兼容层。
>
> 以下记录完整的调研和决策过程。

### 8.1 问题的起源

Agent 最核心的能力就是"调用工具"。Agent 循环（Agent Loop）本质上就是：

```
while (true)
{
    response = LLM(messages + tools)
    if (response 包含工具调用)
        执行工具 → 把结果加入 messages → 继续循环
    else
        返回最终回答 → 退出
}
```

关键问题在于：**LLM 如何告诉我们"它想调用哪个工具、传什么参数"？**

历史上有两种方案。在设计本框架时，我们需要做出选择。

### 8.2 两种方案是什么？

#### 方案 A：ReAct 文本解析（2022 年方案）

**背景**：2022 年 10 月，Yao et al. 发表 ReAct 论文。核心思想是让 LLM 交替进行 **Re**asoning（推理）和 **Act**ing（行动）。

但 2022 年的 LLM **没有原生工具调用能力**。唯一的办法是让 LLM 按约定格式输出文本，程序用正则表达式解析：

```
Thought: 我需要查询数据库中最近的订单
Action: query_orders
Action Input: {"limit": 5}
```

程序这样解析：

```python
# LangChain 早期的 ReAct 解析（简化）
import re
action_match = re.search(r"Action:\s*(.+)", llm_output)
action_input_match = re.search(r"Action Input:\s*(.+)", llm_output)
tool_name = action_match.group(1).strip()
tool_args = json.loads(action_input_match.group(1).strip())
```

**致命问题**：
- LLM 输出 `Action：query_orders`（全角冒号）→ 正则匹配失败
- LLM 在 Action Input 里生成非法 JSON → 解析崩溃
- LLM 跳过 Thought 直接输出 Final Answer → 流程中断
- LLM 一个回复包含多个 Action → 正则只匹配第一个
- **每次只能调用一个工具**，不支持并行

#### 方案 B：Function Calling（2023 年至今）

**背景**：2023 年 6 月，OpenAI 在 GPT-3.5-turbo 中首次发布 Function Calling。模型在训练阶段就学会了"当需要工具时，输出结构化 JSON"。

**工作方式**：在 API 请求中传入工具定义（JSON Schema），LLM 直接返回结构化数据：

```json
{
  "role": "assistant",
  "content": null,
  "tool_calls": [
    {
      "id": "call_abc123",
      "type": "function",
      "function": {
        "name": "query_orders",
        "arguments": "{\"limit\": 5}"
      }
    },
    {
      "id": "call_def456",
      "type": "function",
      "function": {
        "name": "send_email",
        "arguments": "{\"to\": \"user@example.com\", \"body\": \"...\"}"
      }
    }
  ]
}
```

**关键区别**：
- `tool_calls` 是结构化数组——不需要正则解析
- `arguments` 是 JSON 字符串——直接 `JsonSerializer.Deserialize`
- 一次返回**多个** tool_calls——支持并行执行
- 模型在训练时就针对工具调用做了优化——准确率远高于文本生成

**类比**：
- ReAct 好比跟一个人说"你想打电话的时候，在纸上写下电话号码，我帮你拨"——他可能写错格式
- Function Calling 好比手机上的通讯录——直接点按钮拨号，不会格式错误

### 8.3 调研：六大框架怎么选的？

为了做出有依据的决策，我对 6 个主流 Agent 框架的工具调用实现做了调研。

#### 调研结果汇总

| 框架 | 底层机制 | 是否实现了 ReAct 文本解析 | Agent Loop 实现 |
|------|---------|------------------------|--------------------|
| **OpenAI Agents SDK** | Function Calling | ❌ 完全没有 | `while stop_reason == "tool_use"` |
| **Claude Agent SDK** | Tool Use（同类机制） | ❌ 完全没有 | `while stop_reason == "tool_use"` |
| **MS Agent Framework** | Function Calling | ❌ 完全没有 | Auto Function Calling 自动循环 |
| **CrewAI** | 默认 Function Calling | ⚠️ 保留但推荐 FC | `max_iter` 循环 |
| **LangChain 早期** | ReAct 文本解析 | ✅ 曾是默认 | `AgentExecutor` 循环（已弃用） |
| **LangGraph（现在）** | Function Calling | ❌ 名字叫 ReAct，实际用 FC | 状态图循环 |

**结论：6/6 的现行版本都使用 Function Calling。ReAct 文本解析已被全面淘汰。**

#### 逐框架详细分析

**1. OpenAI Agents SDK**

100% Function Calling。Agent loop 的核心逻辑：

```python
# 简化后的 Agent Loop
while True:
    response = model.create(messages, tools)
    if response.stop_reason == "tool_use":
        for tool_call in response.tool_calls:
            result = execute_tool(tool_call.name, tool_call.arguments)
            messages.append(tool_result(tool_call.id, result))
    else:
        return response.content  # 最终回答
```

整个 SDK 中**没有任何正则解析逻辑**，没有 Thought/Action/Observation 格式。文档明确写道：

> "Function tools: Turn any Python function into a tool with automatic schema generation and Pydantic-powered validation."

工具通过 JSON Schema 定义，通过 `tool_calls` 返回。

**2. Claude Agent SDK (Anthropic)**

与 OpenAI 完全一致的模式，只是叫法不同：
- OpenAI 叫 `function_call` / `tool_calls` → Anthropic 叫 `tool_use`
- OpenAI 的 `arguments` → Anthropic 叫 `input`
- 返回 `tool_result` 的方式完全一样

Anthropic 文档原文：

> "When Claude decides to use one of your tools, the API response contains a `tool_use` block with the tool name and a JSON object of arguments. Your application extracts those arguments, runs the operation, and sends the output back in a `tool_result` block."

**3. MS Agent Framework (Semantic Kernel)**

通过 Plugin 和 KernelFunction 实现。底层使用模型的 Function Calling 能力，提供 "Auto Function Calling" 自动循环。完全不涉及文本解析。

文档原文：

> "Agent capabilities can be significantly enhanced by utilizing Plugins and leveraging Function Calling. This allows agents to dynamically interact with external services or execute complex tasks."

**4. CrewAI——唯一保留 ReAct 的框架（但推荐 FC）**

CrewAI 有一个 `function_calling_llm` 参数：

```python
agent = Agent(
    role="Data Analyst",
    llm="gpt-4o",                    # 主 LLM 负责推理
    function_calling_llm="gpt-4o-mini" # 便宜的小模型专门处理工具调用
)
```

**为什么有两个 LLM？** 因为 CrewAI 起源于 LangChain 生态（早期使用 ReAct），后来迁移到 Function Calling。保留 `function_calling_llm` 说明它们认为 **Function Calling 是更好的方式**——好到值得为它用一个专门的模型。

旧的 ReAct 路径是历史包袱而非设计选择。

**5. LangChain → LangGraph 的演进——名字保留，实现已变**

LangChain 是 ReAct 论文的最早实践者（2022-2023）。`AgentExecutor` + ReAct prompt 是它的标志性功能。

从 LangGraph 开始（2024+），一切都变了：

```python
# LangGraph 的 create_react_agent()
from langgraph.prebuilt import create_react_agent
agent = create_react_agent(model, tools)
```

函数名字里有 "react"，但内部做了什么？
1. `model.bind_tools(tools)` — 把工具绑定到模型（Function Calling 方式）
2. 构建状态图：`call_model` → 检查 `tool_calls` → 执行工具 → 回到 `call_model`
3. **完全没有正则解析**，完全没有 Thought/Action/Observation 格式

它保留 "react" 这个名字，是因为沿用了 ReAct 的**概念**（推理+行动循环），但**实现**已经完全是 Function Calling。

好比手机里的"通讯录"还叫"电话簿"——名字是历史遗留，实现方式完全不同。

### 8.4 全维度对比

| 维度 | ReAct（文本解析） | Function Calling |
|------|------------------|-----------------|
| **可靠性** | ❌ 正则解析脆弱，格式偏差即失败 | ✅ 结构化 JSON，解析无歧义 |
| **准确率** | ❌ LLM 可能忘记格式、输出多余文本 | ✅ 模型训练阶段优化了工具调用输出 |
| **并行调用** | ❌ 一次只能调用一个工具 | ✅ 一次返回多个 tool_calls，可并行 |
| **速度** | ❌ N 个工具 = N 轮对话 | ✅ N 个工具可在 1 轮完成 |
| **Strict 模式** | ❌ 不可能 | ✅ OpenAI 支持 Strict 模式，保证参数合法 |
| **Streaming** | ❌ 要等完整文本才能解析 | ✅ 工具参数也支持流式传输 |
| **Prompt 成本** | ❌ 需要在 prompt 中写格式说明 | ✅ 工具定义走专用字段 |
| **兼容性（2026）** | ✅ 任何能生成文本的 LLM | ✅ 几乎所有 LLM 都支持 |
| **推理可见性** | ✅ Thought 步骤可见 | ✅ reasoning_content / thinking 模式已替代 |

关于最后两行的关键变化：

**兼容性**：
- **2023 年**：很多 LLM 不支持 function calling → ReAct 是必要的兼容方案
- **2026 年**：所有商业 LLM（OpenAI、Anthropic、Google、Mistral）和所有主流开源 LLM（Llama 3/4、Qwen、DeepSeek）都支持 function calling。Ollama 本地运行的模型也支持。**兼容性已经不是 ReAct 的优势**

**推理可见性**（曾经是 ReAct 唯一的优势）：
- OpenAI 的 Reasoning models（o1/o3/o4-mini）有内置推理链
- Claude 有 extended thinking 模式，推理步骤完全透明
- 很多模型的 `reasoning_content` 字段可以看到推理过程
- **推理可见性已经不需要通过 ReAct 文本格式来实现**

### 8.5 最终决策

**Function Calling 是本框架唯一的工具调用机制。**

#### 决策理由

| # | 理由 | 详细说明 |
|---|------|---------| 
| 1 | **行业共识** | 6 个主流框架的现行版本全部使用 Function Calling。连最早推广 ReAct 的 LangChain 自己也已转向 FC |
| 2 | **ReAct 没有剩余优势** | 兼容性和推理可见性这两个曾经的优势在 2026 年都已被 FC 阵营填平 |
| 3 | **可靠性要求** | 多 Agent 协作 + 分布式场景需要**可靠的、结构化的**工具调用。正则解析的不确定性是分布式场景的隐患 |
| 4 | **并行工具调用** | 多 Agent 协作中，一个 Agent 经常需要同时调用多个工具。ReAct 每次只能调一个 |
| 5 | **复杂度** | 不支持 ReAct = 不需要正则解析代码、不需要 Thought/Action prompt 模板、不需要双路径测试 |

#### 对框架 API 的影响

Agent 核心循环只认 `tool_calls` 结构化数据：

```csharp
// Agent Loop 伪代码
while (context.Steps < options.MaxSteps)
{
    var response = await llmProvider.ChatAsync(messages, tools, ct);
    
    if (response.ToolCalls is { Count: > 0 } toolCalls)
    {
        // 结构化数据，直接遍历执行
        var results = await toolExecutor.ExecuteAsync(toolCalls, ct);
        messages.AddRange(results.Select(r => ChatMessage.Tool(r)));
    }
    else
    {
        return response.Content; // 最终回答
    }
}
```

没有任何文本解析逻辑。没有正则表达式。没有 Thought/Action/Observation 格式。

`ILLMProvider` 的各个实现（OpenAI / Azure / Anthropic / Ollama）负责与各家 API 对话，但返回的都是统一的 `ToolCall` 结构体。Agent 层永远不需要知道底层用的是哪个 LLM 提供商。

#### 被否决的替代方案

| 方案 | 否决理由 |
|------|---------| 
| **ReAct 作为主要方式** | 2022 年的方案，所有新框架都已放弃 |
| **ReAct 作为兼容层** | 增加复杂度，2026 年没有不支持 FC 的主流 LLM |
| **两种都支持，让用户选** | 双倍维护成本，用户选择困难，测试覆盖指数增长 |
| **在 Provider 层内部做 ReAct 适配** | 没有实际需求场景。如果将来出现不支持 FC 的 LLM 值得适配，届时实现也不晚——这不影响核心 API 设计 |

---

## 9. 执行流程详解

### 9.1 单 Agent 单次运行流程

![单 Agent 单次运行流程](../images/decisions/04-single-agent-flow.png)

### 9.2 多 Agent 协作流程

![多 Agent 协作流程](../images/decisions/05-multi-agent-flow.png)

### 9.3 关键决策点

| 步骤 | 需要处理的边界条件 |
|------|-----------------|
| 步骤 0 | 长期记忆检索延迟？设置超时，检索失败不阻塞任务执行 |
| 步骤 2 | LLM 超时？网络错误？使用 Polly 重试 |
| 步骤 3 | LLM 返回空？返回非法格式？需要优雅降级 |
| 步骤 4 | 工具不存在？参数解析失败？工具执行超时？ |
| 步骤 5 | 已达 MaxSteps？需要强制终止并返回中间结果 |
| 步骤 7 | 提取记忆失败？不能阻塞主流程，异步后台执行 |
| 全程 | Token 接近上限？自动压缩历史消息（IContextCompactor） |
| 全程 | Agent 通信失败？分布式场景需要消息重试和幂等 |
| 全程 | CancellationToken 被取消？所有异步操作都要检查 |

---

## 下一步

Phase 1: 创建项目骨架
- 建立 .NET Solution
- 配置 Directory.Build.props
- 创建 Abstractions 和 Core 项目
- 定义核心接口：IAgent, ILLMProvider, ITool, IWorkingMemory, ILongTermMemory, IOrchestrator, IAgentChannel