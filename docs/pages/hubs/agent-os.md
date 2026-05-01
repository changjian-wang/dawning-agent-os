---
title: dawning-agent-os 总入口
type: hub
subtype: map
canonical: true
summary: dawning-agent-os wiki 的 root hub，承载主题分区与阅读起点。
tags: [agent, meta]
sources: []
created: 2026-04-27
updated: 2026-05-01
verified_at: 2026-05-01
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

- [ADR-001 管家定位与主客体边界](../adrs/butler-positioning-and-subject-object-boundary.md)
- [ADR-002 选择题优先于问答题](../adrs/options-over-elaboration.md)
- [ADR-003 长期记忆是核心能力](../adrs/long-term-memory-as-core-capability.md)
- [ADR-004 重要性级别与确认机制](../adrs/important-action-levels-and-confirmation.md)
- [ADR-005 MVP 主场景选型 = 信息整理](../adrs/mvp-main-scenario-information-curation.md)
- [ADR-006 产品策略收录边界与个人 OS 北极星澄清](../adrs/purpose-scope-and-personal-os-north-star.md)
- [ADR-007 记忆隐私与用户控制](../adrs/memory-privacy-and-user-control.md)
- [ADR-008 主动性与打断边界](../adrs/proactivity-and-interruption-boundary.md)
- [ADR-009 抽象指令兜底机制](../adrs/abstract-instruction-fallback.md)
- [ADR-010 客观代笔语气](../adrs/objective-drafting-style.md)
- [ADR-011 Memory MVP 采用显式记忆账本](../adrs/explicit-memory-ledger-mvp.md)
- [ADR-012 MVP 输入边界：不默认读取用户文件夹](../adrs/mvp-input-boundary-no-default-folder-reading.md)
- [ADR-013 兴趣画像采用权重与时间衰减](../adrs/interest-profile-weighting-and-decay.md)
- [ADR-014 MVP 第一版切片：聊天 + inbox + 读侧整理](../adrs/mvp-first-slice-chat-inbox-read-side.md)
- [ADR-015 仓库形态：产品 monorepo + 内置 LLM-Wiki](../adrs/repository-shape-product-monorepo-with-wiki.md)
- [ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端](../adrs/mvp-desktop-stack-electron-aspnetcore.md)
- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](../adrs/engineering-skeleton-v0.md)
- [ADR-018 后端架构参考 Equinox：DDD + MediatR + Result 模式](../adrs/backend-architecture-equinox-reference.md)
- [ADR-019 测试栈：NUnit + Moq + NetArchTest](../adrs/testing-stack-nunit-v0.md)
- [ADR-020 架构测试断言策略：层级用 assembly 引用 + 类型级用 NetArchTest 到具体类型名](../adrs/architecture-test-assertion-strategy.md)
- [ADR-021 Application 项目目录约定：Abstractions / Messaging / 垂直切片](../adrs/application-folder-layout.md)（superseded by ADR-022）
- [ADR-022 去 MediatR：自研领域事件分发器与 AppService 立面](../adrs/no-mediator-self-domain-event-dispatcher.md)
- [ADR-023 Api 入口立面：AppService 接入与 V0 端点形态](../adrs/api-entry-facade-and-v0-endpoints.md)

已落地规则：

- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)
- [Rule Git Commit 规范](../rules/git-commit-conventions.md)

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
