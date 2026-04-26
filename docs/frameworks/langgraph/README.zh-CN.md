---
framework: langgraph
type: synthesis
tags: [langgraph, index, reading-guide]
created: 2026-04-21
updated: 2026-04-21
status: active
subtype: overview
title: LangGraph 深度解剖
sources: []
---

# LangGraph 深度解剖

> LangChain 生态的 Agent **编排运行时**。本目录以三层结构呈现：先建立直觉，再看架构，最后啃源码。

> **阅读约定**：正文中带 `（待写）` / `（规划中）` / `⏳` 标记的 `[[wikilink]]` 是**设计性占位** —— 它们指向计划补全的子页或子主题，目前可能落不到具体文件。看到此类标记直接跳过即可，不影响主线阅读。

---

## 目录结构

```
langgraph/
├── tier-1-intuition/     # 新人入口：零前置，纯白话 + 例子 + 比喻
├── tier-2-architecture/  # 整体架构：模块地图、调用链
├── tier-3-internals/     # 源码深读：每个模块逐行解剖
├── cases/                # 真实案例 + 横向对比
├── cross-module-comparison.zh-CN.md   # 模块 ↔ Dawning 映射总表
└── diagrams/             # 所有 Mermaid 源文件 + 渲染 PNG
```

---

## 三种读者路径

### 🧭 路径 A：新人完整路径（~4 小时）

完全按顺序读。

1. [tier-1/01-what-is-langgraph](tier-1-intuition/01-what-is-langgraph.zh-CN.md) — 定位（15 min）
2. [tier-1/02-hello-world](tier-1-intuition/02-hello-world.zh-CN.md) — 跑起来（20 min）
3. [tier-1/03-mental-model](tier-1-intuition/03-mental-model.zh-CN.md) — 4 核心概念（30 min）
4. [tier-1/04-tour-by-example](tier-1-intuition/04-tour-by-example.zh-CN.md) — 8 步演进（45 min）
5. [tier-2/00-overview](tier-2-architecture/00-overview.zh-CN.md) — 框架全景（30 min）
6. [tier-2/01-architecture](tier-2-architecture/01-architecture.zh-CN.md) — 模块地图（30 min）
7. 按需挑 tier-3 模块精读
8. 至少看一个 [cases/](cases/) 真实案例

### 🏗️ 路径 B：架构师视角（~2 小时）

跳过 tier-1，直接对标设计。

1. [tier-2/01-architecture](tier-2-architecture/01-architecture.zh-CN.md)
2. [cross-module-comparison](cross-module-comparison.zh-CN.md) — LangGraph ↔ Dawning 映射
3. [cases/cross-case-comparison](cases/cross-case-comparison.zh-CN.md) — 4 案例横评
4. 按 RFC 优先级挑 tier-3 模块

### 🔬 路径 C：源码工程师（~10 小时）

tier-3 按编号顺序精读，配合源码。

tier-3 模块（按推荐顺序）：

1. [02-state-graph](tier-3-internals/02-state-graph.zh-CN.md) — 构图 DSL
2. [03-pregel-runtime](tier-3-internals/03-pregel-runtime.zh-CN.md) — BSP 调度
3. [04-channels](tier-3-internals/04-channels.zh-CN.md) — 通道家族
4. [05-checkpointer](tier-3-internals/05-checkpointer.zh-CN.md) — 持久化
5. [06-interrupt-hitl](tier-3-internals/06-interrupt-hitl.zh-CN.md) — 人机协作
6. [07-streaming](tier-3-internals/07-streaming.zh-CN.md) — 流式输出
7. [08-prebuilt-agents](tier-3-internals/08-prebuilt-agents.zh-CN.md) — ReAct / Supervisor / Swarm
8. [09-subgraph-functional-api](tier-3-internals/09-subgraph-functional-api.zh-CN.md) — 子图与 Functional API
9. [10-platform-integration](tier-3-internals/10-platform-integration.zh-CN.md) — 平台集成

---

## 跨参考

- **基础概念**：[../../../concepts/02-context-memory/dataflow-channel-version.zh-CN.md](../../concepts/02-context-memory/dataflow-channel-version.zh-CN.md)（channel 版本号机制，所有数据流框架通用）
- **横向对比**：[../../../comparisons/maf-vs-langgraph.zh-CN.md](../../comparisons/maf-vs-langgraph.zh-CN.md)、[agent-framework-landscape](../../comparisons/agent-framework-landscape.zh-CN.md)
- **Dawning 对应**：[cross-module-comparison](cross-module-comparison.zh-CN.md)

---

## 真实案例（按复杂度）

| 案例 | 域 | 亮点 |
|------|---|------|
| [klarna-customer-support](cases/klarna-customer-support.zh-CN.md) | 客服 | Supervisor + 12 Worker |
| [open-deep-research](cases/open-deep-research.zh-CN.md) | 研究 | Send fan-out + 子图 |
| [replit-agent](cases/replit-agent.zh-CN.md) | 代码生成 | 反馈环 + 沙箱 |
| [linkedin-hr-agent](cases/linkedin-hr-agent.zh-CN.md) | 招聘 | 多次 HITL + 长 thread |

案例横评：[cross-case-comparison](cases/cross-case-comparison.zh-CN.md)

---

## 版本 & 更新

- 快照版本：LangGraph v1.1.x（2026-04）
- 上游官方文档：<https://langchain-ai.github.io/langgraph/>
- 源码：<https://github.com/langchain-ai/langgraph>
