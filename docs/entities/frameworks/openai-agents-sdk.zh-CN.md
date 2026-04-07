# OpenAI Agents SDK 详细分析

> OpenAI 官方出品的极简 Agent 框架，Dawning.Agents 的原始设计灵感来源。

---

## 基本信息

| 属性 | 值 |
|------|-----|
| **官方名称** | OpenAI Agents SDK |
| **维护者** | OpenAI |
| **仓库** | https://github.com/openai/openai-agents-python |
| **文档** | https://openai.github.io/openai-agents-python/ |
| **语言** | Python + Node.js/TypeScript |
| **许可证** | MIT |
| **Stars** | 20.6k |
| **贡献者** | 240 |
| **最新版本** | v0.13.5 |
| **前身** | OpenAI Swarm（实验性） |

---

## 1. 定位与背景

OpenAI Agents SDK 是 OpenAI 官方发布的轻量级 Agent 框架，设计哲学是**极致简约**。它是 OpenAI Swarm 实验项目的产品化版本，目标是让开发者用最少的代码构建多 Agent 系统。

**核心理念**：
- **极少的概念**：Agent + Runner + Trace，仅此而已
- **提供商无关**：虽由 OpenAI 开发，但支持 100+ LLM 提供商
- **Guardrails 一等公民**：输入/输出验证作为核心概念

**Dawning.Agents 的渊源**：Dawning.Agents 的极简 API 设计直接受此 SDK 启发。

---

## 2. 架构设计

### 2.1 极简三概念架构

```
┌───────────────────────────────┐
│          Agent                │
│  ┌─────────────────────────┐  │
│  │ name                    │  │
│  │ instructions            │  │
│  │ tools (函数工具)         │  │
│  │ handoffs (移交目标)      │  │
│  │ guardrails (护栏)       │  │
│  │ model (LLM 模型)        │  │
│  └─────────────────────────┘  │
├───────────────────────────────┤
│          Runner               │
│  ┌─────────────────────────┐  │
│  │ 执行循环：              │  │
│  │ 1. 调用 LLM            │  │
│  │ 2. 解析工具调用         │  │
│  │ 3. 执行工具            │  │
│  │ 4. 处理移交            │  │
│  │ 5. 重复直到完成         │  │
│  └─────────────────────────┘  │
├───────────────────────────────┤
│          Trace                │
│  ┌─────────────────────────┐  │
│  │ 每次执行自动追踪        │  │
│  │ 可视化 UI 查看          │  │
│  │ 可导出到外部系统        │  │
│  └─────────────────────────┘  │
└───────────────────────────────┘
```

### 2.2 Agent 定义

```python
from agents import Agent, Runner

agent = Agent(
    name="Assistant",
    instructions="You are a helpful assistant.",
    tools=[get_weather],          # 函数工具
    handoffs=[specialist_agent],  # 移交目标
)

result = Runner.run_sync(agent, "What's the weather?")
print(result.final_output)
```

**核心特点**：< 100 行代码即可跑通完整 Agent。

---

## 3. 核心特性

### 3.1 Handoffs（移交）

Agent 间控制权转移的核心机制：

```python
billing_agent = Agent(
    name="Billing",
    instructions="Handle billing questions.",
)

refund_agent = Agent(
    name="Refund",
    instructions="Handle refund requests.",
)

triage_agent = Agent(
    name="Triage",
    instructions="Route to the right specialist.",
    handoffs=[billing_agent, refund_agent],
)
```

**工作原理**：
1. Triage Agent 接收用户请求
2. 决定应由哪个 Agent 处理
3. 通过 Handoff 将控制权转移
4. 目标 Agent 继续处理

### 3.2 Agents-as-Tools（Agent 即工具）

Agent 可以被包装为另一个 Agent 的工具：

```python
specialist = Agent(
    name="DataAnalyst",
    instructions="You analyze data and provide insights.",
)

coordinator = Agent(
    name="Coordinator",
    instructions="Coordinate analysis tasks.",
    tools=[specialist.as_tool()],  # Agent 作为工具
)
```

**区别**：
- **Handoff**：控制权完全转移，原 Agent 不再参与
- **Agent-as-Tool**：调用后返回结果给原 Agent，控制权保留

### 3.3 Guardrails（护栏）

输入/输出验证作为一等公民：

```python
from agents import Agent, InputGuardrail, OutputGuardrail

# 输入护栏
@InputGuardrail
def check_topic(input: str) -> str:
    if "dangerous" in input:
        raise GuardrailError("Topic not allowed")
    return input

# 输出护栏
@OutputGuardrail
def validate_output(output: str) -> str:
    if len(output) > 10000:
        raise GuardrailError("Output too long")
    return output

agent = Agent(
    name="SafeAssistant",
    instructions="You are a helpful assistant.",
    input_guardrails=[check_topic],
    output_guardrails=[validate_output],
)
```

### 3.4 Sessions（会话管理）

```python
from agents import Session

# 创建会话（自动管理历史）
session = Session()

# 多轮对话
result1 = Runner.run_sync(agent, "Hello", session=session)
result2 = Runner.run_sync(agent, "Follow up", session=session)

# Redis 持久化
redis_session = Session(storage=RedisStorage(...))
```

### 3.5 Human-in-the-Loop（人机协同）

内置人机协同支持：
- 工具执行前的确认
- Agent 决策的审批
- 交互式对话流

### 3.6 Realtime Voice Agents（实时语音）

独特特性 — 支持实时语音 Agent：
- 语音输入/输出
- 实时对话
- 函数调用支持

### 3.7 追踪系统

```python
# 每次 Runner.run 自动生成追踪
result = Runner.run_sync(agent, "query")

# 追踪包含：
# - 每次 LLM 调用
# - 工具调用和结果
# - Handoff 事件
# - Guardrail 检查
# - 时间和 Token 计数
```

---

## 4. 工具系统

### 4.1 函数工具

```python
from agents import function_tool

@function_tool
def get_weather(city: str) -> str:
    """Get the current weather for a city."""
    return f"Sunny in {city}"

agent = Agent(
    name="WeatherBot",
    tools=[get_weather],
)
```

### 4.2 MCP 工具

```python
from agents.mcp import MCPServerStdio

# 连接 MCP 服务器
mcp_server = MCPServerStdio(command="uvx", args=["mcp-server-git"])

agent = Agent(
    name="GitAgent",
    mcp_servers=[mcp_server],
)
```

### 4.3 托管工具

OpenAI 平台提供的托管工具（文件搜索、代码解释器等）。

---

## 5. LLM 提供商

### 5.1 OpenAI 原生

支持 OpenAI 的 Responses API 和 Chat Completions API。

### 5.2 提供商无关

通过 LiteLLM 或 any-llm 适配器支持 100+ 提供商：

| 提供商 | 支持方式 |
|--------|---------|
| OpenAI | ✅ 原生 |
| Azure OpenAI | ✅ 适配器 |
| Anthropic | ✅ 适配器 |
| Google Gemini | ✅ 适配器 |
| Ollama | ✅ 适配器 |
| 其他 100+ | ✅ 通过 LiteLLM |

---

## 6. 与竞品对比

### vs. MAF
| 维度 | OpenAI Agents SDK | MAF |
|------|-------------------|-----|
| 复杂度 | 极低（3 个概念） | 中等（图工作流） |
| 持久执行 | ❌ | ✅ Durable |
| 语言 | Python + TS | Python + .NET |
| 分布式 | ❌ | ✅ A2A |
| 特色 | Guardrails, Voice | Skills, HITL |

### vs. CrewAI
| 维度 | OpenAI Agents SDK | CrewAI |
|------|-------------------|--------|
| 抽象级别 | 极简 | 中等 |
| 多 Agent | Handoff 模式 | Crew 角色模式 |
| 工作流 | 无图引擎 | Flows 引擎 |
| 配置 | 纯代码 | YAML + 代码 |
| 明星特色 | Voice, Guardrails | Crew + Flow 双模式 |

---

## 7. 优势与不足

### 优势
1. **极致简约** — Agent + Runner + Trace，< 100 行代码上手
2. **Guardrails 一等公民** — 输入/输出验证作为核心概念
3. **Handoff 模式** — 简洁的 Agent 间控制权转移
4. **Realtime Voice** — 独有的实时语音 Agent 支持
5. **内置追踪** — 自动追踪每次执行
6. **提供商无关** — 虽然是 OpenAI 出品，支持 100+ LLM
7. **Sessions** — 自动会话历史管理 + Redis 持久化
8. **TypeScript SDK** — 支持前端开发者

### 不足
1. **无工作流引擎** — 没有图/DAG 编排能力
2. **无持久执行** — 进程重启后丢失状态
3. **无分布式** — 单进程执行
4. **仅 Python + TS** — 无 .NET 支持
5. **记忆简单** — 仅 Session 级别，无长期记忆
6. **无评估框架** — 仅有追踪，无系统化评估

---

## 8. 对 Dawning 的启示

| 借鉴点 | 详情 | 映射到 Dawning |
|--------|------|---------------|
| 极简 API | Agent(name, instructions, tools, handoffs) | IAgent 定义模型 |
| Handoff 模式 | Agent 间控制权转移 | IHandoffHandler（已有） |
| Guardrails | 输入/输出验证一等公民 | 控制面 Policy Store |
| Session | 自动会话历史管理 | 工作记忆服务 |
| Agent-as-Tool | Agent 包装为工具 | Agent 组合模式 |
| Runner | 统一执行循环 | AgentRunner |
| 追踪系统 | 自动执行追踪 | OpenTelemetry 集成 |

**关键洞察**：

1. **Dawning.Agents 已实现**了 OpenAI Agents SDK 的核心模式（Agent、Handoff、Runner）。下一步是在此基础上添加 SDK 缺少的能力：图编排、持久执行、分布式。

2. **Guardrails 概念至关重要**。它不是简单的输入验证，而是 Agent 行为的安全边界。Dawning 的控制面 Policy Store 应将 Guardrails 作为核心组件。

3. **极简 API 是金标准**。即使 Dawning 添加了图编排和分布式能力，简单场景下的 API 应保持与 OpenAI SDK 相当的简洁度。

---

## 9. 源码结构解析

### 9.1 仓库地址

https://github.com/openai/openai-agents-python

### 9.2 源码目录 (`src/agents/`)

```
src/agents/
├── agent.py                   # 🔵 Agent 核心类定义
├── agent_output.py            # Agent 输出类型
├── agent_tool_input.py        # 结构化工具输入
├── agent_tool_state.py        # Agent-as-Tool 状态隔离
├── run.py                     # 🔵 Runner 执行引擎（核心调度循环）
├── run_config.py              # 运行配置
├── run_context.py             # 运行上下文（DI 容器）
├── run_state.py               # 运行状态管理
├── run_error_handlers.py      # 错误处理器
├── result.py                  # 执行结果
├── guardrail.py               # 🛡️ Guardrails（输入/输出护栏）
├── tool_guardrails.py         # 工具护栏
├── tool.py                    # 🛠️ 工具定义
├── tool_context.py            # 工具执行上下文
├── function_schema.py         # 函数 Schema 生成
├── lifecycle.py               # Agent 生命周期钩子
├── model_settings.py          # 模型设置
├── items.py                   # 运行项（消息、工具调用等）
├── usage.py                   # Token 用量追踪
├── prompts.py                 # Prompt 工具
├── strict_schema.py           # 严格 Schema 验证
├── exceptions.py              # 异常定义
├── handoffs/                  # 🤝 Handoff（移交）
│   └── (Agent 间控制权转移逻辑)
├── models/                    # 🟣 多模型适配
│   └── (OpenAI, Anthropic, LiteLLM 适配器)
├── mcp/                       # 🔌 MCP 集成
│   └── (MCP 服务器连接、工具发现)
├── memory/                    # 💾 记忆/会话
│   └── (Session, SessionStorage, SQLite/Redis)
├── tracing/                   # 📊 追踪系统
│   └── (自动追踪、Span、导出器)
├── extensions/                # 🧩 扩展
│   └── (SQLite session 等扩展)
├── voice/                     # 🎤 语音 Agent
│   └── (实时语音、WebSocket)
├── realtime/                  # 🎤 Realtime API
│   └── (Responses WebSocket 实时 Agent)
├── run_internal/              # ⚙️ 内部执行逻辑
│   └── (执行循环细节、工具调用处理)
├── util/                      # 🔧 工具函数
├── computer.py                # 💻 Computer Use
├── editor.py                  # 📝 编辑器集成
├── apply_diff.py              # Diff 应用
├── repl.py                    # REPL 交互
├── retry.py                   # 重试策略
├── _config.py                 # 全局配置
├── _debug.py                  # 调试标志
├── version.py                 # 版本
└── __init__.py                # 公开 API 导出
```

### 9.3 核心模块分析

| 模块 | 职责 | 行数估算 |
|------|------|---------|
| `agent.py` | Agent 类定义（name, instructions, tools, handoffs） | ~300 |
| `run.py` | Runner 执行循环（LLM → 工具 → Handoff → 重复） | ~500 |
| `guardrail.py` | 输入/输出护栏定义 | ~200 |
| `handoffs/` | Handoff 逻辑 | ~200 |
| `tool.py` | 工具定义和调用 | ~400 |
| `tracing/` | 自动执行追踪 | ~300 |
| `models/` | 多模型适配器 | ~800 |

### 9.4 架构洞察

1. **扁平结构**：大部分核心逻辑在顶层 .py 文件中，子包较少 — 体现极简设计
2. **agent.py + run.py 是核心**：两个文件构成完整 Agent 执行框架
3. **Guardrail 一等公民**：`guardrail.py` + `tool_guardrails.py` 在顶层目录
4. **模型适配器独立**：`models/` 包含多个 LLM 后端适配器
5. **Voice/Realtime 独立**：语音和实时能力作为独立子包
6. **run_internal/**：执行循环的内部细节隔离，保持 `run.py` 的公开 API 简洁
7. **Computer Use**：`computer.py` + `editor.py` + `apply_diff.py` — OpenAI 特有的计算机操作能力
8. **无工作流引擎**：目录中没有图/DAG/工作流相关模块，验证了其极简定位

---

*文档版本：1.1 | 最后更新：2026-04-07*
