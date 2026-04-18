---
framework: langgraph
type: case-index
tags: [langgraph, cases, production, open-source]
created: 2026-04-18
updated: 2026-04-18
status: active
---

# LangGraph — 产品案例索引

> 本目录收录**真实使用 LangGraph 构建的 Agent 产品**，从系统架构、节点划分、State 设计、工具集、运维角度剖析。
> 目的：补齐"从源码到产品"的最后一公里。

---

## 1. 案例矩阵

| Slug | 产品 / 组织 | 场景 | 档次 | 状态 |
|------|-----------|------|------|------|
| `klarna-customer-support` | Klarna | 金融客服 Agent | A | 🚧 样板 |
| `replit-agent` | Replit Agent | AI 编程助手 | A | ⏳ |
| `elastic-ai-assistant` | Elastic AI Assistant | 搜索辅助 Agent | A | ⏳ |
| `uber-devops-agent` | Uber 内部 | DevOps 自动化 | A | ⏳ |
| `linkedin-sql-agent` | LinkedIn | SQL 查询 Agent | A | ⏳ |
| `open-deep-research` | 开源（OSS） | Deep Research 复刻 | B | ⏳ |
| `gpt-researcher` | 开源（OSS） | 研究 Agent | B | ⏳ |
| `reflex-ai-apps` | 开源（OSS） | 基于 LangGraph 的应用集 | B | ⏳ |

图例：🚧 进行中 · ⏳ 计划 · ✅ 完成

---

## 2. 选择标准

优先收录满足以下之一的案例：

1. 有**公开一手技术资料**（官方博客 / talk / paper / repo）
2. 规模可验证（DAU、QPS、节省成本等有公开数字）
3. 架构有**代表性**（多 Agent / HITL / 长任务 / 企业集成）
4. **开源可读**：直接看代码的优先

不收录：

- 仅"看起来用了"但无公开资料的
- 纯 marketing 案例（没技术细节）

---

## 3. 阅读建议

- 新人：先读 [[klarna-customer-support]]（商业客服最经典）+ [[open-deep-research]]（开源可复刻）
- 架构师：读 [[linkedin-sql-agent]]（企业内部 + 数据平台结合）
- 运维 / SRE：读 [[uber-devops-agent]]（生产 SLO / 回滚 / HITL）
- 产品经理：读 [[replit-agent]]（端到端产品形态）

---

## 4. 与横向对比的关系

如果你想看"同一场景用不同框架怎么做"，请去：

- [[../../_cross-case-comparison/customer-support-agents.zh-CN]]（客服 Agent：LangGraph vs others）
- [[../../_cross-case-comparison/coding-agents.zh-CN]]（Replit vs Cursor vs Devin）
- [[../../_cross-case-comparison/research-agents.zh-CN]]（Deep Research 流派对比）

---

## 5. 延伸阅读

- [[../00-overview]] — LangGraph 定位
- [[../01-architecture]] — LangGraph 架构
- [[../../README]] — 框架解剖库总入口
