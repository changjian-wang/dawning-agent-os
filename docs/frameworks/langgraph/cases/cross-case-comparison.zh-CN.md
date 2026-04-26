---
framework: langgraph
type: comparison
cases: [klarna, open-deep-research, replit-agent, linkedin-hr-agent]
tags: [langgraph, comparison, patterns, lessons]
created: 2026-04-18
updated: 2026-04-18
status: active
subtype: cross-comparison
title: LangGraph 案例横向对比
sources: []
---

# LangGraph 案例横向对比

> 4 个生产案例的并排分析：拓扑、State 设计、HITL 强度、工具集、Streaming、Checkpoint 用法。
> 目的：从案例反推 Dawning 该提供哪些"可复用模式"。


## 目录 <!-- TOC-AUTOGEN -->

- [1. 案例一句话](#1-案例一句话)
- [2. 9 维度对比表](#2-9-维度对比表)
- [3. 拓扑选择决策树](#3-拓扑选择决策树)
- [4. State 设计模式](#4-state-设计模式)
- [5. HITL 模式分布](#5-hitl-模式分布)
- [6. 反馈环强度](#6-反馈环强度)
- [7. Stream 模式选择](#7-stream-模式选择)
- [8. 失败 & 恢复](#8-失败--恢复)
- [9. 启示给 Dawning（必须支持的能力）](#9-启示给-dawning必须支持的能力)
- [10. 反模式提醒](#10-反模式提醒)
- [11. 阅读顺序](#11-阅读顺序)
- [12. 延伸](#12-延伸)
<!-- /TOC-AUTOGEN -->

---

## 1. 案例一句话

| 案例 | 域 | 核心模式 |
|------|---|---------|
| [[klarna-customer-support]] | 客服 | Supervisor + 12 worker (handoff tools) |
| [[open-deep-research]] | 研究 / 报告 | Send fan-out + 子图 Researcher 群 |
| [[replit-agent]] | 代码生成 | Manager / Editor / Verifier + 强反馈环 |
| [[linkedin-hr-agent]] | 招聘 (HITL) | 多次 interrupt + 长 thread + 长期记忆 |

---

## 2. 9 维度对比表

| 维度 | Klarna | ODR | Replit | LinkedIn HR |
|------|--------|-----|--------|------------|
| 编排拓扑 | Supervisor | Map-Reduce 子图 | 三角 + 反馈环 | 顺序 + HITL |
| 主要 prebuilt | `create_supervisor` | 自定义 + Send | 自定义 + 沙箱 | `create_react_agent` |
| 子图使用 | 弱（worker 独立 graph） | 强（Researcher 子图） | 强（3 子图） | 中（每阶段子图） |
| Send fan-out | 偶尔 | 重度 | 几乎不 | 不 |
| Checkpointer | Postgres，会话级 | Postgres，run 级 | Postgres，project 级 | Postgres，长会话 |
| Stream 模式 | messages | updates + custom | 多模式（messages + custom + tool） | updates 为主 |
| HITL 强度 | 弱（refund 确认） | 中（clarify 1 次） | 强（多种 approve） | 极强（每阶段必断） |
| Store / 长期记忆 | 客户档案 | 报告库 | project 索引 | 候选人画像 + 偏好 |
| 工具数量 | ~15 | ~8 | ~30 | ~10 (含 ATS / mail) |
| 沙箱 / 隔离 | API rate limit | 不需要 | 容器强隔离 | RBAC + audit |
| 并发模式 | 单 thread 单 turn | 大量并发 Send | 多 task 并行 | 串行（按候选人） |

---

## 3. 拓扑选择决策树

<!-- 拓扑选择决策树 -->
````mermaid
flowchart TD
    Start[新场景]
    Q1{是否多个独立专家协作?}
    Q2{是否需要并行处理多个独立子任务?}
    Q3{是否需要 run → verify → fix 反馈环?}
    Q4{是否每步都要人确认?}
    Q5{是否单一 Agent + 工具足够?}

    Start --> Q1
    Q1 -->|是| Sup[Supervisor 模式 like Klarna]
    Q1 -->|否| Q2
    Q2 -->|是| MR[Map-Reduce + Send like ODR]
    Q2 -->|否| Q3
    Q3 -->|是| FB[反馈环三角 like Replit]
    Q3 -->|否| Q4
    Q4 -->|是| HITL[HITL pipeline like LinkedIn]
    Q4 -->|否| Q5
    Q5 -->|是| ReAct[create_react_agent]
    Q5 -->|否| Custom[自定义 StateGraph]

    classDef pat fill:#e7f5ff,stroke:#1971c2,color:#0b3d91
    class Sup,MR,FB,HITL,ReAct,Custom pat
```
> 源文件：[`diagrams/topology-decision.mmd`](./diagrams/topology-decision.mmd)

---

## 4. State 设计模式

| 模式 | 案例 | 关键字段 |
|------|------|---------|
| 对话 + 路由 | Klarna | `messages` + `current_worker` |
| 计划 + 任务列表 + 结果聚合 | ODR | `plan` + `Annotated[list, add]` for sections |
| 工作区快照 + 验证结果 | Replit | `workspace_hash` + `last_verify` |
| 流程阶段 + 候选人对象 | LinkedIn HR | `stage` (enum) + `candidate` |

> 通用建议：**state 不放大对象**，放指针 / hash / id；大对象走 store 或外部 DB。

---

## 5. HITL 模式分布

```
弱 HITL  ────────────────────────────────────────────────────  强 HITL
        ODR        Klarna              Replit              LinkedIn
       (clarify)  (refund 确认)   (危险/不确定/大改)  (每阶段必停)
```

**频率不同 → UX 不同**：

- 弱：聊天框内确认按钮即可
- 中：弹模态
- 强：必须有任务列表 / 邮件提醒 / SLA

---

## 6. 反馈环强度

| 案例 | 反馈来源 | 强度 |
|------|---------|------|
| ODR | LLM 自评 | 弱（自评易过拟合） |
| Klarna | 工具响应（成功 / 错误码） | 中 |
| Replit | 真机 build + run | 强（确定性反馈） |
| LinkedIn HR | 人工评分 + 候选人回应 | 强（但慢） |

> **强反馈环 + max iterations** 是产线必备防呆。

---

## 7. Stream 模式选择

| 案例 | 主要 stream mode | UI |
|------|-----------------|-----|
| Klarna | messages | 聊天打字机 |
| ODR | updates + custom | 进度条 + section 卡片 |
| Replit | messages + custom + 自定义 tool span | 多区域（聊天 / 任务 / preview） |
| LinkedIn HR | updates + 节点级状态 | 流程图节点高亮 |

---

## 8. 失败 & 恢复

| 案例 | 主要失败点 | 恢复 |
|------|-----------|------|
| Klarna | LLM 路由错 worker | 兜底 fallback worker |
| ODR | Researcher 失败 | 单 Researcher 失败不影响其他（隔离） |
| Replit | tool error / build error | Verifier 自动 replan + max iters |
| LinkedIn HR | HR 不操作 | SLA 超时 → 提醒 → 升级 |

---

## 9. 启示给 Dawning（必须支持的能力）

✅ **来自全部 4 个案例**：

- Postgres / SQLite / DuckDB Checkpointer
- LangSmith 等价的 trace
- 多种 stream channel
- HITL（resume + ns 自动路由）
- 长期记忆 store

✅ **来自至少 2 个案例**：

- Supervisor / Map-Reduce / 反馈环 三种 prebuilt 拓扑
- 子图 + 命名空间
- Send fan-out
- 工具风险分级 + interrupt

✅ **来自单一案例但关键**：

- Replit → 沙箱执行 / 多 stream 并发
- LinkedIn → 多次 HITL 对齐 + SLA
- ODR → 大量并发 Researcher 资源管控
- Klarna → handoff tool 模式（隐式路由）

---

## 10. 反模式提醒

| 反模式 | 谁踩过 | 后果 |
|--------|-------|------|
| State 塞大对象 | 早期 ODR | checkpoint 表巨大 |
| 不限 recursion | 早期 Replit | 死循环烧钱 |
| 工具不分级 | 早期 Klarna | 高风险操作直接执行 |
| stream 单模式 | 早期 Replit | UI 信息密度低 |
| HITL 不可中断 | 早期 LinkedIn | HR 等不到提醒 |

---

## 11. 阅读顺序

- 案例 1 → [[klarna-customer-support]]
- 案例 2 → [[open-deep-research]]
- 案例 3 → [[replit-agent]]
- 案例 4 → [[linkedin-hr-agent]]
- 模块全集 → [[../00-overview]]

---

## 12. 延伸

- LangChain 案例库：<https://www.langchain.com/built-with-langgraph>
- 业务模式分类：[[../../../concepts/agent-loop]]
