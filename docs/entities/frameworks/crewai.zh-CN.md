# CrewAI 详细分析

> Stars 最高的纯 Agent 框架，Crew（自主协作）+ Flow（事件驱动）双模式架构。

---

## 基本信息

| 属性 | 值 |
|------|-----|
| **官方名称** | CrewAI |
| **维护者** | CrewAI Inc. |
| **仓库** | https://github.com/crewAIInc/crewAI |
| **文档** | https://docs.crewai.com/ |
| **语言** | Python（98.6%） |
| **许可证** | MIT |
| **Stars** | 48.2k |
| **贡献者** | 302 |
| **最新版本** | v1.13.0 |
| **发布次数** | 165 |
| **认证开发者** | 100,000+ |
| **依赖** | 完全独立（不依赖 LangChain） |

---

## 1. 定位与背景

CrewAI 是 Stars 数最高（48.2k）的纯 Agent 自动化框架，由 João Moura 创建。其核心理念是将 Agent 组织为**角色扮演团队**（Crew），通过协作完成复杂任务。2025 年引入 **Flows** 后，形成了 Crew + Flow 的双模式架构。

**核心理念**：
- **Crews** = 自主智能团队（灵活、创造性）
- **Flows** = 事件驱动工作流（精确、可控）
- **两者结合** = 生产级 AI 自动化

**关键差异**：完全**独立于 LangChain**，从零构建。

---

## 2. 架构设计

### 2.1 双模式架构

```
┌─────────────────────────────────────────┐
│              CrewAI Application           │
├─────────────────┬───────────────────────┤
│    Flows        │       Crews            │
│  (事件驱动)      │    (自主协作)           │
│                 │                        │
│  @start         │  Agent (角色 + 目标)    │
│  @listen        │  Task (任务描述)        │
│  @router        │  Process (执行模式)     │
│  or_ / and_     │  Tool (工具能力)        │
│                 │                        │
│  State 管理     │  Memory 系统           │
├─────────────────┴───────────────────────┤
│           LLM Layer (via LiteLLM)        │
│          100+ 提供商 支持                 │
└─────────────────────────────────────────┘
```

### 2.2 Crews — 自主协作模式

Crew 是 CrewAI 的原始核心概念：

```python
from crewai import Agent, Task, Crew, Process

# 定义 Agent（角色扮演）
researcher = Agent(
    role="Senior Data Researcher",
    goal="Uncover cutting-edge developments in AI",
    backstory="You're a seasoned researcher...",
    tools=[SerperDevTool()],
    verbose=True,
)

# 定义 Task
research_task = Task(
    description="Conduct thorough research about AI Agents",
    expected_output="A list of 10 bullet points",
    agent=researcher,
)

# 组建 Crew
crew = Crew(
    agents=[researcher, analyst],
    tasks=[research_task, reporting_task],
    process=Process.sequential,  # 或 Process.hierarchical
    verbose=True,
)

result = crew.kickoff(inputs={"topic": "AI Agents"})
```

### 2.3 Flows — 事件驱动模式

Flow 是 2025 年引入的生产级编排层：

```python
from crewai.flow.flow import Flow, listen, start, router, or_
from pydantic import BaseModel

class MarketState(BaseModel):
    sentiment: str = "neutral"
    confidence: float = 0.0

class AnalysisFlow(Flow[MarketState]):
    @start()
    def fetch_data(self):
        self.state.sentiment = "analyzing"
        return {"sector": "tech"}

    @listen(fetch_data)
    def analyze_with_crew(self, data):
        # 在 Flow 步骤中使用 Crew
        analysis_crew = Crew(
            agents=[analyst, researcher],
            tasks=[analysis_task],
            process=Process.sequential,
        )
        return analysis_crew.kickoff(inputs=data)

    @router(analyze_with_crew)
    def route_by_confidence(self):
        if self.state.confidence > 0.8:
            return "high_confidence"
        return "low_confidence"

    @listen("high_confidence")
    def execute_strategy(self):
        # 高置信度策略执行
        pass

    @listen(or_("medium_confidence", "low_confidence"))
    def gather_more_data(self):
        # 需要更多数据
        pass
```

### 2.4 YAML 配置驱动

Agent 和 Task 支持 YAML 配置文件定义：

```yaml
# agents.yaml
researcher:
  role: "{topic} Senior Data Researcher"
  goal: "Uncover cutting-edge developments in {topic}"
  backstory: >
    You're a seasoned researcher with a knack for
    uncovering the latest developments in {topic}.

# tasks.yaml
research_task:
  description: "Conduct thorough research about {topic}"
  expected_output: "A list of 10 bullet points"
  agent: researcher
```

---

## 3. 核心特性

### 3.1 Agent 定义

| 属性 | 描述 |
|------|------|
| `role` | Agent 的角色定位 |
| `goal` | Agent 要完成的目标 |
| `backstory` | Agent 的背景故事（影响行为） |
| `tools` | Agent 可使用的工具列表 |
| `verbose` | 是否输出详细日志 |
| `allow_delegation` | 是否允许委派任务 |
| `memory` | 是否启用记忆 |

### 3.2 Process 模式

| 模式 | 描述 |
|------|------|
| **Sequential** | 顺序执行，Task A → Task B → Task C |
| **Hierarchical** | 自动分配 Manager Agent 进行委派和验证 |

### 3.3 统一记忆系统（2025 年新增）

| 记忆类型 | 描述 |
|---------|------|
| **短期记忆** | 当前执行上下文 |
| **长期记忆** | 跨执行的知识积累 |
| **实体记忆** | 对特定实体的记忆 |
| **用户记忆** | 用户偏好和历史 |

### 3.4 Flow 装饰器

| 装饰器 | 描述 |
|--------|------|
| `@start()` | 流的启动点 |
| `@listen(source)` | 监听特定步骤的输出 |
| `@router(source)` | 条件路由（返回字符串作为路由键） |
| `or_(a, b)` | 任一条件满足时触发 |
| `and_(a, b)` | 所有条件满足时触发 |

### 3.5 状态检查点（2026 年新增）

- 运行时状态检查点
- 事件系统
- 执行器重构
- 使用 JSONB 存储检查点数据

### 3.6 CLI 工具

```bash
# 创建项目
crewai create crew my_project

# 安装依赖
crewai install

# 运行 Crew
crewai run

# 更新框架
crewai update
```

---

## 4. 工具生态

### 4.1 内置工具

通过 `crewai[tools]` 安装：
- SerperDevTool（网络搜索）
- FileReadTool
- DirectoryReadTool
- WebsiteSearchTool
- 更多...

### 4.2 自定义工具

任意 Python 函数均可作为工具。

### 4.3 LangChain 工具兼容

虽然 CrewAI 独立于 LangChain，但可导入 LangChain 工具。

---

## 5. LLM 提供商

通过 LiteLLM 支持 100+ 提供商：

| 提供商 | 示例 |
|--------|------|
| OpenAI | `gpt-4o` |
| Azure OpenAI | `azure/deployment-name` |
| Anthropic | `claude-3.5-sonnet` |
| Google | `gemini/gemini-pro` |
| Ollama | `ollama/llama3` |
| 更多 | AWS Bedrock, Groq, Mistral... |

---

## 6. CrewAI AMP Suite（企业版）

### 6.1 Crew 控制面

| 特性 | 描述 |
|------|------|
| **追踪与可观测性** | 实时监控 Agent 和工作流 |
| **统一控制面** | 集中管理平台 |
| **无缝集成** | 企业系统连接 |
| **高级安全** | 内置安全合规 |
| **可操作洞察** | 实时分析和报告 |
| **24/7 支持** | 企业级支持 |
| **部署选项** | 本地部署 + 云部署 |

### 6.2 免费试用

Crew 控制面可在 https://app.crewai.com/ 免费试用。

---

## 7. 性能对比

CrewAI 官方基准测试（vs. LangGraph）：

| 场景 | CrewAI | LangGraph | 倍数 |
|------|--------|-----------|------|
| QA Agent | ✅ | 基准 | **5.76x 更快** |
| Coding Assistant | 更高评估分 | 基准 | 更快完成 |

**注意**：这是 CrewAI 官方数据，实际性能因场景而异。

---

## 8. 项目脚手架

```
my_project/
├── .gitignore
├── pyproject.toml
├── README.md
├── .env
└── src/
    └── my_project/
        ├── __init__.py
        ├── main.py          # 入口点
        ├── crew.py           # Crew 定义
        ├── tools/
        │   ├── custom_tool.py
        │   └── __init__.py
        └── config/
            ├── agents.yaml   # Agent 配置
            └── tasks.yaml    # Task 配置
```

---

## 9. 优势与不足

### 优势
1. **最高社区影响力** — 48.2k Stars，100k+ 认证开发者
2. **完全独立** — 不依赖 LangChain，更轻更快
3. **双模式架构** — Crew（自主）+ Flow（精确控制）完美互补
4. **角色扮演范式** — 直觉性的 Agent 定义方式
5. **YAML 配置** — 降低门槛，配置驱动
6. **CLI 工具** — 项目脚手架一键生成
7. **性能优势** — 特定场景 5.76x 快于 LangGraph
8. **丰富的学习资源** — DeepLearning.AI 合作课程

### 不足
1. **仅 Python** — 无 .NET / TypeScript 支持
2. **控制面商业化** — 生产级监控需要 AMP Suite
3. **无原生分布式** — 执行仍在单进程内
4. **无 A2A 协议** — 不支持跨框架 Agent 互操作
5. **记忆系统较新** — 统一记忆系统 2025 年才引入

---

## 10. 对 Dawning 的启示

| 借鉴点 | 详情 | 映射到 Dawning |
|--------|------|---------------|
| Crew + Flow 双模式 | 自主协作 + 确定性编排的完美结合 | Orchestrator 双模式设计 |
| YAML 配置 | 声明式 Agent/Task 定义 | 配置驱动 Agent 定义 |
| @start/@listen/@router | 优雅的事件驱动装饰器 DSL | C# Attribute 等价设计 |
| or_/and_ 逻辑运算 | 复合触发条件 | 图编排条件组合 |
| 角色扮演范式 | Role/Goal/Backstory | Agent 描述模型 |
| Process 模式 | Sequential / Hierarchical | 编排预设 |
| CLI 脚手架 | `crewai create crew` | `dotnet new dawning-agent` |
| JSONB 检查点 | 运行时状态检查点 | 检查点存储方案 |

**关键洞察**：CrewAI 的 Flow 装饰器模式（`@start`/`@listen`/`@router`）在 C# 中可映射为 `[Start]`/`[Listen("source")]`/`[Router]` Attribute，这是比纯图 API 更友好的编排方式。两种模式应并存。

---

## 11. 源码结构解析

### 11.1 仓库地址

https://github.com/crewAIInc/crewAI

### 11.2 源码目录 (`lib/crewai/src/crewai/`)

CrewAI 在 v1.x 重构后采用 `lib/crewai/` monorepo 结构：

```
lib/crewai/
├── src/crewai/                  # 主源码
│   ├── agent.py                 # 🔵 Agent 核心类
│   ├── crew.py                  # 🔵 Crew 编排核心
│   ├── task.py                  # 🔵 Task 任务定义
│   ├── process.py               # 🔵 Process 枚举（sequential/hierarchical）
│   ├── flow/                    # 🟢 Flow 事件驱动引擎
│   │   ├── flow.py              # Flow 基类、@start/@listen/@router
│   │   ├── flow_events.py       # 事件定义
│   │   └── utils.py             # or_/and_ 逻辑组合
│   ├── agents/                  # Agent 构建器和执行器
│   │   ├── agent_builder/       # AgentBuilder 模式
│   │   ├── executor.py          # Agent 执行器（重构中）
│   │   └── crew_agent_executor.py
│   ├── tools/                   # 🛠️ 工具系统
│   │   ├── base_tool.py         # BaseTool 抽象
│   │   ├── tool_calling.py      # 工具调用逻辑
│   │   └── structured_tool.py   # 结构化工具
│   ├── memory/                  # 💾 统一记忆系统
│   │   ├── short_term/          # 短期记忆
│   │   ├── long_term/           # 长期记忆
│   │   ├── entity/              # 实体记忆
│   │   └── user/                # 用户记忆
│   ├── knowledge/               # 📚 知识库
│   ├── llm.py                   # LLM 抽象（via LiteLLM）
│   ├── project/                 # 项目装饰器（@CrewBase, @agent, @task）
│   ├── cli/                     # CLI 工具（crewai create/run/install）
│   ├── telemetry/               # 遥测数据收集
│   └── utilities/               # 工具函数
├── tests/                       # 测试
├── pyproject.toml               # 包配置
└── README.md
```

### 11.3 核心模块分析

| 模块 | 职责 | 关键类/函数 |
|------|------|----------|
| `agent.py` | Agent 定义（角色/目标/工具） | `Agent` |
| `crew.py` | 多 Agent 编排 | `Crew`, `Process` |
| `task.py` | 任务定义和执行 | `Task` |
| `flow/flow.py` | 事件驱动工作流 | `Flow`, `@start`, `@listen`, `@router` |
| `memory/` | 四级记忆系统 | `ShortTermMemory`, `LongTermMemory`, `EntityMemory` |
| `project/` | 装饰器式项目定义 | `@CrewBase`, `@agent`, `@task`, `@crew` |
| `cli/` | 脚手架和运行器 | `crewai create crew`, `crewai run` |

### 11.4 架构洞察

1. **单文件核心三件套**：`agent.py` + `crew.py` + `task.py` 是最核心的三个文件，代码量适中
2. **Flow 独立子包**：`flow/` 作为事件驱动引擎独立存在，与 Crew 松耦合
3. **四级记忆**：`memory/` 下有 short_term/long_term/entity/user 四个子包
4. **装饰器式项目**：`project/` 提供 `@CrewBase` 等装饰器，实现声明式 Crew 定义
5. **检查点新增**：JSONB 检查点存储刚刚加入（提交于数小时前），说明 CrewAI 正在补齐持久执行能力
6. **LiteLLM 集成**：`llm.py` 通过 LiteLLM 统一所有 LLM 调用

---

*文档版本：1.1 | 最后更新：2026-04-07*
