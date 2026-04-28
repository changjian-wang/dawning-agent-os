---
title: dawning-agent-os 总入口
type: hub
subtype: map
canonical: true
summary: dawning-agent-os wiki 的 root hub，承载主题分区与阅读起点。
tags: [agent, meta]
sources: []
created: 2026-04-27
updated: 2026-04-28
verified_at: 2026-04-28
freshness: evergreen
status: active
archived_reason: ""
supersedes: []
related: []
part_of: []
---

# dawning-agent-os 总入口

> dawning-agent-os wiki 的 root hub，承载主题分区与阅读起点。

## 范围

本 hub 是 wiki 唯一的 root hub（SCHEMA §7.2）。其他所有 hub / 页面都通过 `part_of` 链路收敛到这里。

收录范围由 [PURPOSE.md](../../PURPOSE.md) 定义；结构契约由 [SCHEMA.md](../../SCHEMA.md) 定义。本页只做导航，不承载论证（§3 / §6.1）。

## 不在范围内       <!-- 可选 -->

- 单个对象的展开论证 → 进入 entity / concept / comparison / adr。
- 不可违反的硬约束 → 进入 rule。
- 一句话能答完且不需复用的回答 → 不回写为页面（§8.2 #5）。

## 从这里开始

当前阶段为 wiki 初建，子 hub 尚未建立。已落地的首批决策先直接从 root hub 暴露，避免孤岛；后续内容增多后再拆出决策记录 hub。

已落地 ADR：

- [ADR-004 重要性级别与确认机制](../adrs/important-action-levels-and-confirmation.md)
- [ADR-005 MVP 主场景选型 = 信息整理](../adrs/mvp-main-scenario-information-curation.md)
- [ADR-006 产品策略收录边界与个人 OS 北极星澄清](../adrs/purpose-scope-and-personal-os-north-star.md)

预期的子 hub（待建）：

- 产品哲学 hub：管家定位、选择题优先、长期记忆等产品红线（来自 PURPOSE §4.1）。
- 框架与协议 hub：被借鉴 / 被对比的 Agent 框架、协议、工具。
- 概念与模式 hub：memory / skill / orchestration / planning / reflection。
- 决策记录 hub：所有 ADR 的入口。

> 提示：单个 hub 的「从这里开始」清单建议 ≤ 12 项（§7.2 拓扑约束）。

## 阅读地图         <!-- 可选 -->

入口阅读顺序：

1. 先读 [PURPOSE.md](../../PURPOSE.md) §1–§3，明确为什么有这个 wiki、收录范围。
2. 再读 [SCHEMA.md](../../SCHEMA.md) §3 / §4 / §6，理解页面类型与模板契约。
3. 从本 hub 跳到子 hub → 具体 canonical 页（§8.2 query 流程）。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：方向意图与收录范围。
- [SCHEMA.md](../../SCHEMA.md)：结构契约。
