---
title: "Multi-Agent 协作模式：Swarm / Supervisor / Hierarchical / Network"
type: concept
tags: [multi-agent, orchestration, swarm, supervisor, hierarchical, network, handoff]
sources: [concepts/agent-loop.md, comparisons/maf-vs-langgraph.zh-CN.md, concepts/protocols-a2a-mcp.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Multi-Agent 协作模式：Swarm / Supervisor / Hierarchical / Network

> 单 Agent 能做的有限。真实复杂任务需要多个 Agent 协作：分工、移交、监督、汇总。
> 业界在 2024-2026 沉淀出了四种主流协作模式，每家框架各有偏好。
>
> 本文梳理四种模式的结构、适用场景、实现差异，以及 Dawning Layer 3 的原语设计。

---

## 1. 为什么要多 Agent

### 1.1 单 Agent 的天花板

| 问题 | 原因 |
|------|------|
| 工具太多 → 选择困难 | LLM 在 > 20 工具时决策质量下降 |
| 角色多 → Prompt 冲突 | "你是医生 + 律师 + 程序员"会让模型迷惑 |
| 上下文爆炸 | 任务复杂后上下文长度超限 |
| 难以优化 | 整体 Prompt 修改一处影响全局 |
| 权限混乱 | 不同职责需要不同权限边界 |

### 1.2 多 Agent 的核心价值

- **关注点分离**：每个 Agent 专注一个领域
- **权限隔离**：按 Agent 划分工具和数据访问
- **独立演化**：Agent 可独立版本化、A/B
- **并行执行**：独立子任务并发
- **可观测性**：每个 Agent 独立 Trace

---

## 2. 四种协作模式概览

```
┌─────────────────┬──────────────────┬─────────────────┬─────────────────┐
│    Network      │    Supervisor    │   Hierarchical  │      Swarm      │
│   (对等网状)      │    (中心化)        │    (树状层级)      │    (去中心化)      │
├─────────────────┼──────────────────┼─────────────────┼─────────────────┤
│   A ←→ B        │       S          │       S         │   A ── B         │
│   ↕   ↕         │      ↙↓↘         │      ↙ ↘        │   │    │         │
│   C ←→ D        │     A  B  C      │     M₁  M₂      │   D ── C         │
│                 │                  │    ↙↘   ↙↘     │    (handoff)     │
│                 │                  │   a b  c d       │                 │
└─────────────────┴──────────────────┴─────────────────┴─────────────────┘
```

| 模式 | 控制中心 | 典型代表 | 适用规模 |
|------|---------|---------|---------|
| **Network** | 无（对等） | LangGraph / AutoGen GroupChat | 2-5 个 |
| **Supervisor** | 单一协调者 | LangGraph Supervisor / CrewAI Hierarchical | 3-10 个 |
| **Hierarchical** | 多级协调 | MetaGPT / Enterprise 场景 | 10+ 个 |
| **Swarm** | 无（handoff 链） | OpenAI Swarm / MAF Handoff | 2-5 个 |

---

## 3. Pattern 1：Network（对等网状）

### 3.1 结构

```
    ┌─────┐         ┌─────┐
    │  A  │◄───────►│  B  │
    └──┬──┘         └──┬──┘
       │    ┌───┐      │
       └───►│ C │◄─────┘
            └─┬─┘
              │
       ┌──────▼──────┐
       │  Shared     │
       │  State      │
       └─────────────┘
```

**特征**：
- 每个 Agent 都可以给任何其他 Agent 发消息
- 共享全局状态
- 终止条件由某个 Agent 显式声明或达到上限

### 3.2 代表实现：LangGraph Network

```python
from langgraph.graph import StateGraph

def agent_a(state):
    ...
    return Command(goto=["agent_b", "agent_c"])  # 可以发给任意 Agent

graph = StateGraph(State)
graph.add_node("a", agent_a)
graph.add_node("b", agent_b)
graph.add_node("c", agent_c)

# 每个 Agent 之间相互可达
for src in ["a", "b", "c"]:
    for dst in ["a", "b", "c"]:
        if src != dst:
            graph.add_edge(src, dst)
```

### 3.3 代表实现：AutoGen GroupChat

```python
from autogen import GroupChat, GroupChatManager

group_chat = GroupChat(
    agents=[analyst, coder, reviewer],
    messages=[],
    speaker_selection_method="auto"  # LLM 决定谁发言
)
```

**核心机制**：GroupChatManager 每轮用 LLM 决定"谁下一个说话"。

### 3.4 优缺点

| 优点 | 缺点 |
|------|------|
| 灵活，任意拓扑 | 容易"死循环"（A→B→A→B） |
| 适合头脑风暴、辩论 | 收敛难，终止条件脆弱 |
| 多视角协作 | token 消耗大 |

### 3.5 适用场景

- 多视角评估（analyst + coder + reviewer）
- 角色扮演对话
- 辩论 / 批评式推理

---

## 4. Pattern 2：Supervisor（中心化）

### 4.1 结构

```
              ┌──────────────┐
              │  Supervisor  │
              │   (路由/汇总)   │
              └──┬───┬───┬───┘
                 │   │   │
            ┌────▼┐ ┌▼┐ ┌▼────┐
            │  A  │ │B│ │  C  │
            │专家 1│ │ │ │专家 3│
            └─────┘ └─┘ └─────┘
```

**特征**：
- Supervisor 是唯一决策者
- 每轮 Supervisor 选择下一个 worker Agent
- worker 完成后返回 Supervisor
- Supervisor 决定何时汇总并终止

### 4.2 代表实现：LangGraph Supervisor

```python
from langgraph_supervisor import create_supervisor

research_agent = create_react_agent(llm, [search_tool], name="researcher")
writer_agent = create_react_agent(llm, [write_tool], name="writer")

supervisor = create_supervisor(
    agents=[research_agent, writer_agent],
    model=llm,
    prompt="你管理 researcher 和 writer，根据任务指派合适的 agent。"
)
```

### 4.3 代表实现：CrewAI Hierarchical Process

```python
from crewai import Crew, Process, Agent, Task

manager = Agent(role="Project Manager", goal="Coordinate the team")
researcher = Agent(role="Researcher", goal="Find information")
writer = Agent(role="Writer", goal="Produce final report")

crew = Crew(
    agents=[researcher, writer],
    manager_agent=manager,  # 明确指定 manager
    process=Process.hierarchical,
    tasks=[...]
)
```

### 4.4 Supervisor 的决策难题

Supervisor 必须在每轮回答：
1. **当前谁做最合适？**（routing）
2. **需要继续还是已完成？**（termination）
3. **多路结果如何合并？**（aggregation）

**常见策略**：
- Rule-based：根据 task 类型映射
- LLM-based：用 LLM 评估 worker 能力
- Learned：从历史表现学习（Layer 5 机会点）

### 4.5 优缺点

| 优点 | 缺点 |
|------|------|
| 决策集中，易控制 | Supervisor 成为瓶颈 |
| 终止清晰 | worker 无法横向协作 |
| 容易加 HITL | Supervisor Prompt 复杂 |
| 审计友好 | 所有决策都经过 Supervisor，延迟累加 |

### 4.6 适用场景

- 客服路由（意图分类 → 专家）
- 研究报告流水线（研究 → 写作 → 审校）
- 客户旅程编排（售前 → 销售 → 售后）

---

## 5. Pattern 3：Hierarchical（树状层级）

### 5.1 结构

```
                ┌──────────────┐
                │   CEO Agent   │
                └───┬──────┬───┘
                    │      │
         ┌──────────▼──┐ ┌─▼────────┐
         │  Manager A  │ │Manager B │
         └──┬───────┬──┘ └──┬───────┘
            │       │       │
         ┌──▼──┐ ┌─▼─┐  ┌──▼──┐
         │ a1  │ │a2 │  │ b1  │ ...
         └─────┘ └───┘  └─────┘
```

**特征**：
- 多层 Supervisor 嵌套
- 每层有独立目标和决策
- 任务逐层分解 → 叶子 Agent 执行 → 结果逐层汇聚

### 5.2 代表实现：MetaGPT

MetaGPT 用 "Software Company" 模型：

```
CEO → Product Manager → Architect → Engineers → QA
      (需求文档)       (设计文档)    (代码)    (测试)
```

每层 Agent 有**固定 SOP（Standard Operating Procedure）**，而非自由决策。

### 5.3 代表实现：LangGraph 子图嵌套

```python
# 子图 = 下一层
research_subgraph = StateGraph(SubState)
research_subgraph.add_node("scout", scout_agent)
research_subgraph.add_node("deep_dive", deep_dive_agent)
compiled_research = research_subgraph.compile()

# 主图把子图当作一个节点
main_graph = StateGraph(MainState)
main_graph.add_node("research_phase", compiled_research)
main_graph.add_node("writing_phase", compiled_writing)
```

### 5.4 Deep Agents（LangGraph 2025 新范式）

```
Main Agent（规划 + 协调）
    ├─► Sub-Agent 1（执行子任务）
    ├─► Sub-Agent 2（执行子任务）
    └─► Todo Tracker（跨 Agent 状态管理）
```

**关键创新**：
- 主 Agent 维护"todo list" 全局任务清单
- 动态分配给 sub-agent
- sub-agent 完成后更新 todo
- 主 Agent 决定何时停止

### 5.5 优缺点

| 优点 | 缺点 |
|------|------|
| 可扩展到大量 Agent | 结构复杂 |
| 关注点清晰分层 | 层级固化，不灵活 |
| 易组织大项目 | 跨层通信需要协议 |
| 企业组织直觉映射 | Token 消耗累加 |

### 5.6 适用场景

- 软件开发（需求 → 设计 → 编码 → 测试）
- 研究报告（规划 → 多源研究 → 写作 → 审校）
- 企业流程自动化

---

## 6. Pattern 4：Swarm（去中心化 handoff）

### 6.1 结构

```
  User ──► Agent A ──handoff──► Agent B ──handoff──► Agent C ──► User
                │
                └───可以 handoff 回 A
```

**特征**：
- 没有 Supervisor
- 每个 Agent 完成自己的部分后**显式 handoff** 给下一个
- 控制权完整转移（不保留状态）
- 由 Agent 自己判断是否结束

### 6.2 代表实现：OpenAI Swarm / Agents SDK

```python
from agents import Agent, handoff

support_agent = Agent(
    name="Support",
    instructions="Handle customer questions; hand off to billing for payments."
)

billing_agent = Agent(
    name="Billing",
    instructions="Handle billing questions."
)

# 在 support 的工具列表里加上 handoff
support_agent.tools = [handoff(billing_agent)]

# 运行
runner.run(support_agent, user_input)
```

关键：`handoff` 是一种特殊的 Tool，调用后 Runner 切换当前 Agent。

### 6.3 代表实现：MAF Handoff

```csharp
var salesAgent = new ChatCompletionAgent {
    Name = "Sales",
    Handoffs = [supportAgent, billingAgent]
};
```

MAF 的 Handoff 与 OpenAI SDK 语义一致。

### 6.4 Handoff 的数据传递

```
Agent A 移交给 Agent B 时：

方案 1：传递完整历史（Agent B 能看到 A 的对话）
方案 2：传递摘要（Agent B 只看到 task brief）
方案 3：传递结构化 context（JSON）

OpenAI SDK 默认方案 1，可自定义 on_handoff 钩子做方案 2/3。
```

### 6.5 优缺点

| 优点 | 缺点 |
|------|------|
| 实现简洁 | 没有全局视图 |
| 每个 Agent 独立自治 | 难以协调并行任务 |
| 延迟低（无中心） | handoff 链长时难追踪 |
| 对用户透明（像和一个系统对话） | 错误传播风险（A 错了，B 基于错误继续） |

### 6.6 适用场景

- 客服分流（一线 → 二线 → 专家）
- 语音 IVR 系统
- 旅行预订（机票 → 酒店 → 签证）

---

## 7. 横向对比

| 维度 | Network | Supervisor | Hierarchical | Swarm |
|------|---------|-----------|--------------|-------|
| 控制流 | 自由 | 中心化 | 树状 | 链式 |
| 共享状态 | 是 | 是 | 分层 | 否（手动传） |
| 终止机制 | 脆弱 | 清晰 | 分层清晰 | 取决于最后 Agent |
| 并发支持 | 好 | 中 | 好 | 差 |
| Token 效率 | 低 | 中 | 低 | 高 |
| 可审计性 | 中 | 高 | 高 | 中 |
| 适合规模 | 2-5 | 3-10 | 10+ | 2-5 |
| 代表 | AutoGen / LangGraph | LangGraph / CrewAI | MetaGPT / Deep Agents | OpenAI SDK / MAF |

---

## 8. 选型决策

```
是否需要并行？
├─ 是 ──► 多个并行分支结果如何汇聚？
│         ├─ 需要统一决策 ──► Supervisor
│         └─ 各自独立      ──► Hierarchical（子图并发）
│
└─ 否 ──► Agent 数量？
          ├─ 2-5 且控制流线性   ──► Swarm (handoff)
          ├─ 2-5 且需要讨论     ──► Network (GroupChat)
          ├─ 3-10 且需要路由   ──► Supervisor
          └─ 10+ 且流程复杂    ──► Hierarchical
```

---

## 9. 跨协议互操作（A2A 的角色）

多 Agent 不一定在同一进程：

```
Dawning Agent    ──A2A──►    LangGraph Agent    ──A2A──►    MAF Agent
（.NET 进程）                （Python 进程）               （.NET 进程）
```

**A2A 让跨框架的 Multi-Agent 成为可能**：
- Agent Card 暴露各自能力
- Task 生命周期统一
- 消息格式标准化

详见 [[concepts/protocols-a2a-mcp.zh-CN]]。

---

## 10. 共同挑战与解决方案

### 10.1 循环检测

**问题**：Network 模式下 A→B→A→B→...

**方案**：
- 最大 step 上限（所有框架必需）
- 无进展检测（连续 N 轮状态无变化 → 终止）
- LLM 终止判定（"任务是否已完成？"）

### 10.2 状态一致性

**问题**：多 Agent 并发写同一状态

**方案**：
- Reducer 合并（LangGraph）
- 消息传递替代共享状态（MAF Actor 模型）
- 乐观锁 + 冲突检测

### 10.3 可观测性

**问题**：多 Agent 混合 trace 难以分析

**方案**：
- 每个 Agent 独立 span
- 跨 Agent 用 correlation_id 串联
- 可视化图（LangGraph Studio / Arize Phoenix）

### 10.4 成本失控

**问题**：Agent 数量 × LLM 调用 = 成本爆炸

**方案**：
- 分级：关键 Agent 用强模型，辅助 Agent 用小模型
- 结构化替代对话（不是所有 Agent-Agent 通信都需要 LLM）
- 缓存：子 Agent 结果缓存

### 10.5 权限隔离

**问题**：Agent A 不应访问 Agent B 的工具

**方案**：
- 按 Agent 配置独立 ToolRegistry
- Layer 7 Policy Engine 强制拦截
- Scope 感知（Dawning 独有）

---

## 11. Dawning Layer 3 的原语设计

### 11.1 原语而非模式

Dawning 不绑定到某一种模式，而是提供**底层原语**，上层可组合出所有模式：

| 原语 | 用途 |
|------|------|
| `IAgent` | Agent 实体 |
| `IMessageBus` | 跨 Agent 消息（本地 + 分布式） |
| `IHandoffProtocol` | 显式控制权移交 |
| `IOrchestrator` | 编排原语（顺序 / 并行 / 条件 / 补偿） |
| `IWorkflowEngine` | 状态图（对应 Supervisor / Hierarchical） |
| `IAgentDirectory` | Agent 发现（本地 + A2A 远程） |

### 11.2 组合出四种模式

```csharp
// Swarm 模式
var chain = new HandoffChain(salesAgent, billingAgent, supportAgent);

// Supervisor 模式
var supervisor = new WorkflowEngine()
    .WithRoot<SupervisorExecutor>()
    .AddWorker<ResearcherExecutor>()
    .AddWorker<WriterExecutor>()
    .Build();

// Network 模式
var bus = serviceProvider.GetRequiredService<IMessageBus>();
bus.Subscribe<AnalyzerAgent>();
bus.Subscribe<CoderAgent>();
bus.Subscribe<ReviewerAgent>();

// Hierarchical 模式
var subWorkflow = ...;  // 子图
var mainWorkflow = new WorkflowEngine()
    .AddSubWorkflow(subWorkflow)
    .Build();
```

### 11.3 Dawning 的增值

| 能力 | 常规框架 | Dawning |
|------|---------|---------|
| 跨协议（A2A） | MAF / Google ADK 有 | ✅ 原生 |
| Scope 感知 | ❌ | ✅ Agent 间消息带 scope |
| 治理拦截 | ❌ | ✅ Layer 7 前置 |
| Skill 演化 | ❌ | ✅ 每个 Agent 的 Skill 独立演化 |
| 分布式运行时 | ⚠️ 商业 | ✅ 开源三面体 |
| .NET 原生 | MAF | ✅ |

---

## 12. 常见坑与最佳实践

| 坑 | 避免方法 |
|----|---------|
| Supervisor Prompt 过长 | 只让 Supervisor 看到简要路由信息，不传完整历史 |
| Agent 之间自然语言通信 | 用**结构化数据**（JSON）替代自由文本 |
| 共享状态竞态 | 用消息传递（Actor 模型）替代共享状态 |
| Agent 数量过多 | 先合并职责相近的 Agent，再按需拆分 |
| Handoff 链过长 | 设置最大深度 + 循环检测 |
| 无法调试 | 从单 Agent 开始，逐步加 Agent 并验证 |
| 工具散落 | Agent 级 ToolRegistry，清晰权限边界 |

---

## 13. 小结

> 单 Agent 是"函数调用"，多 Agent 是"分布式系统"。
> 每一种多 Agent 模式都有对应的单机并发模型（Network = 进程共享内存；Supervisor = 主从；Hierarchical = 多级调度；Swarm = 管道）。
>
> **Dawning Layer 3 不绑定任何模式**，而是提供原语让你按需组合。
> 这个策略的代价是"抽象层次更高"，回报是"一套机制 cover 所有场景，且与 Scope/演化/治理原生联动"。

---

## 14. 延伸阅读

- [[concepts/agent-loop.md]] — 单 Agent 基础
- [[comparisons/maf-vs-langgraph.zh-CN]] — 两大图编排引擎
- [[concepts/protocols-a2a-mcp.zh-CN]] — 跨进程 Multi-Agent 协议
- [[concepts/skill-evolution.zh-CN]] — 每个 Agent 独立演化
- LangGraph Multi-Agent：<https://langchain-ai.github.io/langgraph/concepts/multi_agent/>
- OpenAI Swarm：<https://github.com/openai/swarm>
- AutoGen GroupChat：<https://microsoft.github.io/autogen/>
- MetaGPT：<https://github.com/geekan/MetaGPT>
