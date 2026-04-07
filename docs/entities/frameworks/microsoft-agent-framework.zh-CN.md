# Microsoft Agent Framework (MAF) 详细分析

> AutoGen 的企业级继任者，首个同时官方支持 .NET 和 Python 的 Agent 框架。

---

## 基本信息

| 属性 | 值 |
|------|-----|
| **官方名称** | Microsoft Agent Framework |
| **维护者** | Microsoft |
| **仓库** | https://github.com/microsoft/agent-framework |
| **文档** | https://devblogs.microsoft.com/agent-framework/ |
| **语言** | Python + .NET (C#) |
| **许可证** | MIT |
| **Stars** | ~9k |
| **贡献者** | 121 |
| **最新版本** | v1.0（2026 年 4 月 3 日） |
| **前身** | AutoGen（已进入维护模式）、Semantic Kernel（Agent 层迁移至此） |

---

## 1. 定位与背景

Microsoft Agent Framework 是微软在 2025 年 10 月推出的统一 Agent 框架，目标是整合 Semantic Kernel 的企业就绪基础和 AutoGen 的多 Agent 编排创新。2026 年 4 月 3 日正式发布 v1.0，标志着 API 稳定和长期支持承诺。

**关键背景**：
- AutoGen 已进入**维护模式**，所有新开发迁移至 MAF
- Semantic Kernel 的 Agent 层被整合进 MAF，SK 本身继续作为 Plugin/Kernel SDK 存在
- 同时提供从 SK 和 AutoGen 的**迁移路径**文档

---

## 2. 架构设计

### 2.1 三层架构

```
┌─────────────────────────────────┐
│        Agent Definition         │  ← Agent 定义层
│  (Agent, Instructions, Skills)  │
├─────────────────────────────────┤
│      Workflow Orchestration      │  ← 编排层
│  (Graph Workflows, Streaming,   │
│   Checkpointing, HITL)          │
├─────────────────────────────────┤
│       Hosting & Deployment       │  ← 部署层
│  (Azure Functions, A2A, MCP,    │
│   Durable Agents)               │
└─────────────────────────────────┘
```

### 2.2 Agent 定义

MAF 支持两种 Agent 定义方式：

**代码方式（.NET）**：
```csharp
var agent = new ChatCompletionAgent()
{
    Name = "Analyst",
    Instructions = "You are a financial analyst.",
    Kernel = kernel,
};
```

**声明式方式（YAML/JSON）**：
Agent 可通过 YAML 或 JSON 文件声明式定义，无需编写代码。适合从配置管理系统加载 Agent 定义。

### 2.3 工作流图

MAF 的核心编排机制是**基于图的工作流**：
- 节点代表 Agent 或函数
- 边代表控制流转换
- 支持条件分支、循环、并行执行
- 内置流式传输（Streaming）
- 支持检查点（Checkpointing）用于持久执行
- 支持时间旅行调试（Time-Travel Debugging）

---

## 3. 核心特性

### 3.1 多 Agent 编排
- 基于图的工作流，支持任意复杂的 Agent 协作拓扑
- Agent 间通过工作流图的边进行控制权转移
- 支持层级式、顺序式、并行式编排

### 3.2 Agent Skills（技能系统）
- 2026 年 3 月新增的核心特性
- 可移植、可复用的技能包，提供领域专业知识
- 技能基于 Markdown 文件定义（开放格式）
- Agent 在运行时按需发现和加载技能
- Python SDK 支持**代码定义技能**、**脚本执行**和**人工审批**

### 3.3 Agent Harness（运行装具）
- Shell 和文件系统访问
- 审批流程
- 跨长时间会话的上下文管理
- .NET 和 Python 都支持

### 3.4 持久执行
- **Durable Agents**：Agent 生命周期跨越进程重启
- **Durable Workflows**：工作流通过检查点持久化
- **后台响应**（Background Responses）：长时间运行操作不阻塞客户端

### 3.5 多模型提供商
- Azure OpenAI
- OpenAI
- Microsoft Foundry（本地和云端）
- Claude Agent SDK 集成（2026 年 1 月）
- GitHub Copilot SDK 集成（2026 年 1 月）
- 可扩展提供商接口

### 3.6 协议支持
- **A2A（Agent-to-Agent）**：标准化 Agent 间通信协议
- **MCP（Model Context Protocol）**：工具集成协议
- 中间件管道（Middleware Pipeline）

### 3.7 可观测性
- 内置 OpenTelemetry 集成
- DevUI 调试界面
- 追踪和指标收集

---

## 4. 工具与插件

### 4.1 工具类型
| 类型 | 描述 |
|------|------|
| **原生函数工具** | C# 或 Python 函数标注为工具 |
| **MCP 服务器** | 连接外部 MCP 兼容工具 |
| **Agent 技能** | Markdown 文档定义的领域知识包 |
| **中间件** | 请求/响应管道中间件 |

### 4.2 工具审批
- 支持人机协同（HITL）审批流
- 脚本执行可门控人工审批

---

## 5. 分布式能力

| 能力 | 支持情况 |
|------|---------|
| A2A 协议 | ✅ 原生支持 |
| Azure Functions 托管 | ✅ |
| Durable Agents | ✅ 跨进程重启生存 |
| Durable Workflows | ✅ 检查点 + 恢复 |
| 后台长时间运行 | ✅ Background Responses |
| 水平扩展 | ✅ 通过 Azure 托管 |
| 本地部署 | ✅ Foundry Local 支持 |

---

## 6. 与 Semantic Kernel 的关系

| 维度 | Semantic Kernel | MAF |
|------|----------------|-----|
| **定位** | Plugin SDK + AI 基础设施 | Agent 编排框架 |
| **Agent 层** | 迁移至 MAF | ✅ 核心功能 |
| **Plugin 系统** | ✅ 核心功能 | 通过 SK 集成 |
| **工作流** | Process Framework | Graph Workflows |
| **NuGet 包** | 43 个包 | 新的统一包 |

**关系**：MAF 建立在 SK 的基础之上。SK 继续存在作为 Plugin/Kernel 层，MAF 负责 Agent 编排和部署。两者互补而非替代。

---

## 7. 生态系统

### 7.1 SDK 集成
- Claude Agent SDK → 通过 MAF 使用 Claude 的全部 Agent 能力
- GitHub Copilot SDK → 函数调用、流式、MCP、Shell、文件操作
- Microsoft Foundry Local → 本地模型 + Agent 工作流

### 7.2 迁移路径
- **从 Semantic Kernel 迁移**：官方迁移指南，API 表面兼容
- **从 AutoGen 迁移**：官方迁移指南，概念映射文档

---

## 8. 优势与不足

### 优势
1. **唯一的 .NET + Python 双语言企业 Agent 框架**
2. Azure 生态深度集成（Foundry、Functions、Durable）
3. 完整的从 SK/AutoGen 迁移路径
4. 声明式 + 代码式双模 Agent 定义
5. A2A + MCP 协议原生支持
6. Agent Skills 开放格式（Markdown 定义）
7. 微软长期支持承诺

### 不足
1. v1.0 刚发布，社区生态仍在建设中
2. 示例和文档与 Azure 耦合较重
3. 相比前身 AutoGen（56.8k stars），社区规模小
4. 本地/非 Azure 部署场景的文档较少

---

## 9. 对 Dawning 的启示

| 借鉴点 | 详情 | 映射到 Dawning |
|--------|------|---------------|
| Graph Workflows | 基于图的有状态工作流编排 | 运行面 Orchestrator |
| Agent Skills | Markdown 定义的可移植技能包 | 技能系统设计 |
| 声明式 Agent | YAML/JSON Agent 定义 | 配置驱动 Agent |
| 中间件管道 | 请求/响应管道 | 控制面 Policy Store |
| Durable Agents | 跨进程持久 Agent | 运行面检查点存储 |
| A2A 集成 | Agent 间标准通信 | SC-1.4 异步契约 |
| DevUI | 可视化调试界面 | 可观测性工具 |

**竞争差异**：MAF 不具备技能自演化能力，没有三面分布式架构，DI 模式不如我们纯粹。这是 Dawning 的差异化空间。

---

## 10. 源码结构解析

### 10.1 仓库地址

https://github.com/microsoft/agent-framework

### 10.2 .NET 源码目录 (`dotnet/src/`)

```
dotnet/src/
├── Microsoft.Agents.AI/                          # 🔵 Agent 核心层
│   └── (Agent 基类、ChatCompletionAgent、生命周期管理)
├── Microsoft.Agents.AI.Abstractions/              # 🔵 抽象接口
│   └── (IAgent、IChatClient、模型抽象)
├── Microsoft.Agents.AI.Workflows/                 # 🟢 工作流引擎
│   └── (图工作流编排、节点/边定义、执行引擎)
├── Microsoft.Agents.AI.Workflows.Declarative/     # 🟢 声明式工作流
│   └── (YAML/JSON 工作流定义解析)
├── Microsoft.Agents.AI.Workflows.Declarative.Foundry/ # 🟢 Foundry 声明式
├── Microsoft.Agents.AI.Workflows.Declarative.Mcp/ # 🟢 MCP 声明式
├── Microsoft.Agents.AI.Workflows.Generators/      # 🟢 工作流代码生成
├── Microsoft.Agents.AI.Declarative/               # 🟡 声明式 Agent 定义
├── Microsoft.Agents.AI.DurableTask/               # 🔴 持久任务
│   └── (Durable Agents、Durable Workflows、检查点)
├── Microsoft.Agents.AI.OpenAI/                    # 🟣 OpenAI 连接器
├── Microsoft.Agents.AI.Anthropic/                 # 🟣 Anthropic 连接器
├── Microsoft.Agents.AI.Foundry/                   # 🟣 Foundry 连接器
├── Microsoft.Agents.AI.GitHub.Copilot/            # 🟣 GitHub Copilot 连接器
├── Microsoft.Agents.AI.CopilotStudio/             # 🟣 Copilot Studio 连接器
├── Microsoft.Agents.AI.A2A/                       # 🟠 A2A 协议
│   └── (Agent-to-Agent 通信实现)
├── Microsoft.Agents.AI.AGUI/                      # 🟠 AG-UI 协议
├── Microsoft.Agents.AI.Hosting/                   # 🔵 托管基础设施
│   └── (DI 注册、ServiceLifetime 支持)
├── Microsoft.Agents.AI.Hosting.A2A/               # 🟠 A2A 托管
├── Microsoft.Agents.AI.Hosting.A2A.AspNetCore/    # 🟠 A2A ASP.NET Core
├── Microsoft.Agents.AI.Hosting.AGUI.AspNetCore/   # 🟠 AG-UI ASP.NET Core
├── Microsoft.Agents.AI.Hosting.AzureFunctions/    # ☁️ Azure Functions 托管
├── Microsoft.Agents.AI.Hosting.OpenAI/            # 🟣 OpenAI 兼容托管
├── Microsoft.Agents.AI.DevUI/                     # 🛠️ 开发调试 UI
├── Microsoft.Agents.AI.Mem0/                      # 💾 记忆集成
├── Microsoft.Agents.AI.Purview/                   # 🔒 合规/治理
├── Microsoft.Agents.AI.CosmosNoSql/               # 💾 Cosmos DB 存储
├── LegacySupport/                                 # ⚠️ 旧版兼容
├── Shared/                                        # 📦 共享工具
└── Directory.Build.props                          # 构建配置
```

### 10.3 命名空间分析

| 命名空间前缀 | 职责 | 包数量 |
|-------------|------|-------|
| `Microsoft.Agents.AI` | Agent 核心 + 抽象 | 2 |
| `Microsoft.Agents.AI.Workflows*` | 工作流引擎 | 5 |
| `Microsoft.Agents.AI.Hosting*` | 托管基础设施 | 6 |
| `Microsoft.Agents.AI.{Provider}` | LLM 连接器 | 4 |
| `Microsoft.Agents.AI.{Protocol}` | 协议支持（A2A/AGUI） | 2 |
| `Microsoft.Agents.AI.{Storage}` | 存储后端 | 2 |

### 10.4 架构洞察

1. **Abstractions 分离**：`AI.Abstractions` 与 `AI` 分包，遵循 .NET 社区标准（零依赖接口包）
2. **Declarative 子模块**：声明式工作流有独立的 Foundry 和 MCP 子包，支持不同声明式来源
3. **Hosting 层丰富**：6 个 Hosting 包，覆盖 ASP.NET Core、Azure Functions、A2A、AGUI、OpenAI 兼容
4. **DurableTask 独立**：持久任务作为独立包，非核心依赖
5. **多连接器**：OpenAI、Anthropic、Foundry、GitHub Copilot、Copilot Studio — 连接器即 NuGet 包

---

*文档版本：1.1 | 最后更新：2026-04-07*
