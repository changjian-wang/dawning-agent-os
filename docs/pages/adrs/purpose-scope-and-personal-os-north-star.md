---
title: ADR-006 产品策略收录边界与个人 OS 北极星澄清
type: adr
subtype: scope
canonical: true
summary: PURPOSE v1.7 明确产品形态决策可收录、纯商业策略不收录，并把个人 OS 定位为非承诺北极星。
tags: [agent, product-philosophy]
sources: []
created: 2026-04-28
updated: 2026-04-28
verified_at: 2026-04-28
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/mvp-main-scenario-information-curation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-04-28
adr_revisit_when: MVP 信息整理主场景跑通后，若个人 OS 北极星开始驱动具体架构边界；或出现商业 / roadmap 内容与产品形态决策难以区分的收录争议时。
---

# ADR-006 产品策略收录边界与个人 OS 北极星澄清

> PURPOSE v1.7 明确产品形态决策可收录、纯商业策略不收录，并把个人 OS 定位为非承诺北极星。

## 背景

[PURPOSE.md](../../PURPOSE.md) 同时承担两个职责：定义 wiki 收录范围，以及记录 dawning-agent-os 当前可站住的产品 / 架构方向。

v1.6 中存在几处容易被 future agent 误读的点：

- §3.2 写「产品 / 商业策略」不收录，但 §2 / §4 又已经收录 MVP 主场景、管家定位、交互哲学等产品形态决策。
- §2 写「其它 app 接入它而不是它去接入其它 app」，容易被理解为短期不应接入文件、笔记、邮件、日历等外部数据源。
- §2 `target_user` 容易被读成只服务「不会用 LLM」的人，而不是服务「不想写 prompt / 不想管理上下文」的人。
- §4.1「长期记忆必须自研」容易被读成底层存储、检索、embedding 全部自研，而不是记忆模型与控制策略自研。

这些问题会让后续 ingest / query 产生漂移：要么错误拒收产品 ADR，要么把个人 OS 北极星误读成当下架构承诺，要么过早排除仍在目标用户内的高级用户。

## 备选方案

- 方案 A：维持 v1.6，不新增 ADR，只在后续页面中临时解释。
- 方案 B：在 PURPOSE v1.7 中澄清边界，并用本 ADR 记录变更理由。
- 方案 C：立刻把个人 OS 升级为 §4.1 thesis，开始驱动功能清单和架构路线。

## 被否决方案与理由

**方案 A 维持 v1.6**：

- 会继续保留「产品决策是否可收录」的文本矛盾。
- future agent 可能把纯商业策略、产品形态决策、架构决策混为一谈。
- 个人 OS 表述过强，容易和 MVP 阶段的外部数据源接入冲突。

**方案 C 立刻升级个人 OS 为 thesis**：

- 当前 MVP 尚未跑通，Memory 模块也没有出现外部依赖。
- 过早把个人 OS 写成当前 thesis，会把短期产品变成大而空的系统工程。
- 这违反 PURPOSE §4.3「产品阶段宁可 thesis 少而真，不要多而虚」。

## 决策

采用方案 B：PURPOSE v1.7 做边界澄清，但不升级当前 thesis。

具体决策：

- §3.1 明确「影响 agent 行为边界的产品形态决策」可收录。
- §3.2 把不收录范围收窄为纯商业 / 增长策略，例如商业模式、定价、渠道、市场营销、竞品市场分析。
- §2 将个人 OS 表述为长期北极星与路径过滤器，而不是短期功能清单或当前架构承诺。
- §2 同时说明个人 OS 的非目标：不是操作系统内核、不是 app launcher、不是企业工作流平台、不是注意力推荐系统。
- §2 将目标用户澄清为「不想写 prompt / 不想管理上下文」的人，避免把高级用户整体排除在外。
- §4.1 将长期记忆自研边界澄清为「记忆模型、用户画像语义、可解释 / 可控策略」自研，底层基础设施可复用成熟组件。

## 影响

**正向影响**：

- 后续产品哲学 ADR 不再被 §3.2 误判为越界。
- 个人 OS 继续保留为长期方向，但不会压迫 MVP 阶段过早做平台化。
- 外部 app / 数据源接入与长期「其它 app 反向接入个人 OS 能力」不再冲突。

**代价 / 风险**：

- PURPOSE 中产品方向内容变多，后续需要继续防止它滑向 roadmap 或商业计划。
- 个人 OS 北极星仍未形成完整 ADR；本 ADR 只澄清边界，不证明个人 OS 路线已经成立。

## 复议触发条件

`adr_revisit_when` 已写入 front matter（见 SCHEMA §4.3.2 / §6.0），本节不重复。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：方向意图与本 ADR 对应的 v1.7 修改位置。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-005 MVP 主场景选型 = 信息整理](mvp-main-scenario-information-curation.md)：MVP 主场景决策。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
