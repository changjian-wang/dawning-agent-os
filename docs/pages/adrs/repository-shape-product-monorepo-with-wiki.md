---
title: ADR-015 仓库形态：产品 monorepo + 内置 LLM-Wiki
type: adr
subtype: architecture
canonical: true
summary: dawning-agent-os 不再只是 wiki-only 仓库，而升级为产品 monorepo；docs/ 继续作为内置 LLM-Wiki 和决策记忆。
tags: [agent, product-philosophy, meta]
sources: []
created: 2026-04-28
updated: 2026-04-28
verified_at: 2026-04-28
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/purpose-scope-and-personal-os-north-star.md, pages/adrs/mvp-main-scenario-information-curation.md, pages/adrs/mvp-first-slice-chat-inbox-read-side.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-04-28
adr_revisit_when: MVP 代码落地后发现 docs 与代码耦合过重、产品实现需要拆出独立仓库、或 framework 抽取进入真实复用阶段时。
---

# ADR-015 仓库形态：产品 monorepo + 内置 LLM-Wiki

> dawning-agent-os 不再只是 wiki-only 仓库，而升级为产品 monorepo；docs/ 继续作为内置 LLM-Wiki 和决策记忆。

## 背景

早期 dawning-agent-os 被定位为 LLM-Wiki：仓库主要保存 docs/ 下的决策、外部资料消化、ADR 与产品方向记忆。AGENTS.md 也明确写过“本仓库目前是文档与研究知识库，不是源代码项目”。

现在前提已经变化：dawning-assistant 即将删除，dawning-agents 也已弃用。若 dawning-agent-os 仍保持 wiki-only，最重要的产品决策会留在本仓库，而真正的产品实现又会被迫迁移到另一个仓库，重新制造上下文分裂。

PURPOSE 已长期写明项目策略是“先做一款 Agent 产品，待产品成熟后再从中提取 Agent Framework”。ADR-014 也已经定义了 MVP 第一版切片：聊天窗口 + agent inbox + 读侧整理。这个阶段需要一个主产品仓库承载实现，而不只是一个 wiki。

## 备选方案

- 方案 A：dawning-agent-os 继续保持 wiki-only，未来另建产品实现仓库。
- 方案 B：dawning-agent-os 升级为产品 monorepo；docs/ 保持 LLM-Wiki，代码实现也进入本仓库。
- 方案 C：把产品实现继续放在 dawning-assistant，把 dawning-agent-os 只作为决策 wiki。
- 方案 D：恢复 dawning-agents，先做 framework，再派生产品。

## 被否决方案与理由

**方案 A 继续 wiki-only**：

- 决策和代码会天然分裂，future agent 写代码时必须跨仓库找上下文。
- 产品第一版已经由 ADR-014 收敛，如果代码不在本仓库，wiki 的“LLM-friendly 项目记忆”价值会下降。
- 旧 AGENTS.md 中“不是源代码项目”的限制会阻止后续产品实现。

**方案 C 继续 dawning-assistant**：

- 用户已明确 dawning-assistant 即将删除。
- 保留它会延续“产品代码在一个仓库、决策记忆在另一个仓库”的问题。

**方案 D 恢复 dawning-agents / framework 先行**：

- 用户已明确 dawning-agents 已弃用。
- 与 PURPOSE 的“产品先行，framework 是副产物”相冲突。
- 还没有足够 dogfood 和复用信号支撑 framework 抽象。

## 决策

采用方案 B：dawning-agent-os 升级为产品 monorepo，并保留 docs/ 作为内置 LLM-Wiki。

仓库职责：

- docs/：继续作为 LLM-Wiki，保存 PURPOSE、SCHEMA、ADR、外部资料消化与产品 / 架构决策记忆。
- 应用代码：后续可在仓库根部新增 apps/、src/、tests/ 等目录，承载 MVP 产品实现。
- 产品实现优先：先在本仓库实现 ADR-014 的 MVP 第一版切片。
- framework 后置：只有当产品代码被复用 ≥ 2 次、或出现外部依赖 / API 稳定信号时，再考虑从产品代码中抽取 framework。

文档与代码的边界：

- docs/ 仍受 SCHEMA 约束；写 docs/ 前必须读取 PURPOSE 与 SCHEMA。
- docs/raw/ 仍不可修改；overview.md / log.md 仍是派生物，不手维。
- 代码实现细节不写入 wiki 正文；wiki 记录“为什么这样做”、边界和关键取舍，源码与 XML doc / 注释记录“怎么实现”。
- 入口指令应从“本仓库不是源代码项目”改为“本仓库是产品 monorepo，docs/ 是 LLM-Wiki”。

## 影响

**正向影响**：

- 产品决策、长期记忆和代码实现聚合到同一个主仓库，减少上下文分裂。
- Future coding agent 可以先读 docs/ 获取边界，再直接在同仓库实现。
- dawning-assistant / dawning-agents 退场后，dawning-agent-os 成为唯一主线，项目方向更清晰。

**代价 / 风险**：

- docs/ 与代码共仓后，需要更清楚地区分 wiki 结构规则与代码工程规则。
- 仓库会从知识库变成产品 monorepo，后续需要补充代码目录、构建、测试、格式化与运行约定。
- SCHEMA 仍只约束 docs/，不能被误用为应用代码结构规范。

## 复议触发条件

`adr_revisit_when` 已写入 front matter（见 SCHEMA §4.3.2 / §6.0），本节不重复。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：本 ADR 对应的仓库形态与产品策略。
- [SCHEMA.md](../../SCHEMA.md)：docs/ LLM-Wiki 的结构契约。
- [ADR-006 产品策略收录边界与个人 OS 北极星澄清](purpose-scope-and-personal-os-north-star.md)：定义产品形态决策可收录。
- [ADR-005 MVP 主场景选型 = 信息整理](mvp-main-scenario-information-curation.md)：定义产品 MVP 主场景。
- [ADR-014 MVP 第一版切片：聊天 + inbox + 读侧整理](mvp-first-slice-chat-inbox-read-side.md)：定义第一版产品切片。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。