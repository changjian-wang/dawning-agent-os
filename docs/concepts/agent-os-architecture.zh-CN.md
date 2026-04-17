---
title: "Agent OS 架构：从框架到操作系统"
type: concept
tags: [architecture, agent-os, microkernel, design-philosophy]
sources: [decisions/roadmap.zh-CN.md, concepts/llm-wiki-pattern.zh-CN.md, comparisons/agent-framework-landscape.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Agent OS 架构：从框架到操作系统

> Dawning Agent OS 不是又一个 Agent 框架——它是 AI Agent 的操作系统。
> 本文定义 Agent OS 的核心隐喻、架构映射和设计哲学。

---

## 1. 为什么是 OS 而不是 Framework

### 1.1 框架市场已饱和

截至 2026 年 4 月，市面上有 18+ Agent 框架（详见 [[comparisons/agent-framework-landscape.zh-CN]]）。MAF、LangGraph、CrewAI、OpenAI Agents SDK 等都在做"框架"。在这个拥挤的赛道上做第 19 个框架没有意义。

### 1.2 我们设计的东西超出了框架的范畴

回顾我们的 Layer 0-7 路线图，我们设计的组件——Agent 注册表、策略引擎、记忆面、技能生命周期管理、消息总线、检查点恢复、RBAC——这些不是框架的特征，而是**操作系统的特征**。

### 1.3 OS 隐喻让架构一目了然

当我们说"Agent 框架"时，开发者不知道该放什么、不放什么。当我们说"Agent OS"时，每个组件的职责自然映射到已有认知：

| 传统 OS | Agent OS | 对应 Layer |
|---------|----------|-----------|
| **硬件驱动** | LLM Provider 抽象（适配 OpenAI/Ollama/Azure/Anthropic） | Layer 0 |
| **系统调用** | Agent Loop + Tool 协议 | Layer 1 |
| **虚拟内存 / 文件系统** | Memory Plane（短期 + 长期，四级 scope 隔离） | Layer 2 |
| **进程调度器** | 多 Agent 编排器（顺序/并行/条件/补偿） | Layer 3 |
| **动态链接器** | Skill Router（多信号评分、按需加载） | Layer 4 |
| **包管理器** | Skill Evolution（安装/升级/灰度/回滚/废弃） | Layer 5 |
| **内核 IPC** | 三面体消息总线（异步契约、幂等、死信） | Layer 6 |
| **安全子系统** | 治理、RBAC、审计、策略引擎 | Layer 7 |

---

## 2. 微内核设计哲学

Dawning Agent OS 采用**微内核架构**（Microkernel），而非宏内核。

### 2.1 内核只做最少的事

内核（Kernel）仅包含：

- **Agent Loop**：接收任务 → LLM 推理 → 工具执行 → 结果注入 → 循环/终止
- **IPC**：面间消息传递（`IMessageBus` 抽象）
- **调度**：Agent 任务分发与编排原语
- **安全**：Scope 隔离、策略执行

### 2.2 一切皆可插拔

| 子系统 | 内核内 | 用户态可替换 |
|--------|--------|-------------|
| LLM Provider | `ILLMProvider` 接口 | Ollama / OpenAI / Azure / Anthropic / 自定义 |
| 短期记忆 | `IWorkingMemory` 接口 | 内存 / Redis / 自定义 |
| 长期记忆 | `ILongTermMemory` 接口 | LLM Wiki / 向量数据库 / 自定义 |
| 消息总线 | `IMessageBus` 接口 | 内存 / RabbitMQ / Azure Service Bus |
| 技能存储 | `ISkillRegistry` 接口 | 本地文件 / 数据库 / 远程注册表 |
| 检查点 | `ICheckpointStore` 接口 | 内存 / Redis / 数据库 |

### 2.3 DI 即内核加载器

传统 OS 通过引导加载器（bootloader）加载内核模块。Agent OS 通过 .NET DI 容器完成：

```csharp
services
    .AddAgentOSKernel()                    // 内核：Loop + IPC + 调度
    .AddLLMDriver<OllamaProvider>()        // 驱动：LLM Provider
    .AddMemoryPlane(memory => {            // 存储：记忆面
        memory.UseRedisWorkingMemory();
        memory.UseLLMWikiLongTermMemory();
    })
    .AddSkillManager()                     // 包管理：技能系统
    .AddGovernance();                      // 安全：治理与审计
```

---

## 3. 三面体架构 → OS 子系统

原有的三面体（Control Plane / Runtime Plane / Memory Plane）在 OS 视角下重新定义：

### 3.1 控制面 → OS 管理层

| 组件 | OS 类比 | 职责 |
|------|---------|------|
| Agent 注册表 | 进程表 | 注册、发现、健康监控所有 Agent |
| 策略存储 | 安全策略数据库 | 版本化治理规则（allowlist/denylist/深度限制） |
| 技能生命周期管理器 | 包管理器 | 技能的创建→验证→发布→灰度→废弃→归档 |
| 评测调度器 | Cron 调度器 | 夜间基准测试、PR 评测、回归检查 |

### 3.2 运行面 → OS 执行层

| 组件 | OS 类比 | 职责 |
|------|---------|------|
| Agent Worker | 进程/容器 | 从队列拉取任务，执行 Agent Loop |
| 检查点存储 | 进程快照 | 崩溃恢复、热迁移 |
| 租约与心跳 | 进程心跳 | 失败检测、任务重分配 |
| 优雅排空 | SIGTERM 处理 | 滚动部署、零停机 |

### 3.3 记忆面 → OS 存储层

| 组件 | OS 类比 | 职责 |
|------|---------|------|
| 短期状态 | RAM / 页面缓存 | 会话消息、工具结果、临时变量 |
| 长期知识 | 磁盘 / 文件系统 | 跨会话持久知识（LLM Wiki 模式） |
| Scope 隔离 | 用户/组权限 | global / team / session / private |

---

## 4. 第一个"用户态应用"

**dawning-assistant** 是 Agent OS 之上的第一个应用程序——类似于 Unix 上的 Shell。

```
┌─────────────────────────────────────────┐
│          dawning-assistant (Shell)       │  ← 用户态应用
├─────────────────────────────────────────┤
│          dawning-agents (标准库)          │  ← 系统库
├─────────────────────────────────────────┤
│       Dawning Agent OS (内核 + 子系统)    │  ← 操作系统
│  ┌──────────┬──────────┬──────────┐     │
│  │ 控制面    │ 运行面    │ 记忆面   │     │
│  │ (管理层)  │ (执行层)  │ (存储层)  │     │
│  └──────────┴──────────┴──────────┘     │
├─────────────────────────────────────────┤
│   LLM Drivers (OpenAI/Ollama/Azure/...) │  ← 硬件抽象
└─────────────────────────────────────────┘
```

---

## 5. 命名空间映射

| 旧命名空间 | 新命名空间 | 说明 |
|-----------|-----------|------|
| `Dawning.AgentFramework.Abstractions` | `Dawning.AgentOS.Abstractions` | 内核接口（零依赖） |
| `Dawning.AgentFramework.Core` | `Dawning.AgentOS.Core` | 内核实现 |
| `Dawning.AgentFramework.Providers.*` | `Dawning.AgentOS.Drivers.*` | LLM 驱动 |
| `Dawning.AgentFramework.Memory` | `Dawning.AgentOS.Memory` | 记忆面 |
| `Dawning.AgentFramework.Skills` | `Dawning.AgentOS.Skills` | 技能子系统 |
| `Dawning.AgentFramework.Orchestration` | `Dawning.AgentOS.Scheduler` | 调度/编排 |
| `Dawning.AgentFramework.Governance` | `Dawning.AgentOS.Security` | 安全/治理 |
| `Dawning.AgentFramework.Messaging` | `Dawning.AgentOS.IPC` | 进程间通信 |

---

## 交叉引用

- [[decisions/roadmap.zh-CN]] — 分层学习路径（Layer 0-7）
- [[concepts/llm-wiki-pattern.zh-CN]] — 记忆面的编译式知识管理模式
- [[concepts/agent-loop]] — 内核执行循环
- [[concepts/context-management]] — 存储层上下文管理策略
- [[comparisons/agent-os-vs-frameworks]] — 为什么 OS 而不是 Framework

## 来源

- [decisions/roadmap.zh-CN.md](../decisions/roadmap.zh-CN.md) — 原三面体架构设计
- [concepts/llm-wiki-pattern.zh-CN.md](../concepts/llm-wiki-pattern.zh-CN.md) — 编译式知识管理
- [comparisons/agent-framework-landscape.zh-CN.md](../comparisons/agent-framework-landscape.zh-CN.md) — 框架市场分析
