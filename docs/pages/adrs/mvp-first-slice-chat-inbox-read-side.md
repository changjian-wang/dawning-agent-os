---
title: ADR-014 MVP 第一版切片：聊天 + inbox + 读侧整理
type: adr
subtype: scope
canonical: true
summary: MVP 第一版采用聊天窗口 + agent inbox，输入限于显式材料与会话沉淀，先做读侧整理、可见 Memory Ledger 和简单兴趣衰减。
tags: [agent, memory, interaction-design, product-philosophy]
sources: []
created: 2026-04-28
updated: 2026-04-28
verified_at: 2026-04-28
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/mvp-main-scenario-information-curation.md, pages/adrs/mvp-input-boundary-no-default-folder-reading.md, pages/adrs/explicit-memory-ledger-mvp.md, pages/adrs/interest-profile-weighting-and-decay.md, pages/adrs/important-action-levels-and-confirmation.md, pages/adrs/repository-shape-product-monorepo-with-wiki.md, pages/adrs/mvp-desktop-stack-electron-aspnetcore.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-04-28
adr_revisit_when: MVP dogfood 发现纯读侧整理无法形成闭环、agent inbox 无法承载真实材料、Memory Ledger 可见性造成明显打扰、或需要接入小批量文件 / 外部数据源时。
---

# ADR-014 MVP 第一版切片：聊天 + inbox + 读侧整理

> MVP 第一版采用聊天窗口 + agent inbox，输入限于显式材料与会话沉淀，先做读侧整理、可见 Memory Ledger 和简单兴趣衰减。

## 背景

ADR-005 已决定 MVP 主场景是信息整理，ADR-012 已决定第一版不默认读取用户文件夹，ADR-011 已决定 Memory MVP 采用显式 Memory Ledger，ADR-013 已决定兴趣画像采用权重与时间衰减。

这些决策仍留下一个工程层面的空白：第一版到底长什么样、接收哪些输入、允许做哪些动作、Memory 对 user 可见到什么程度、兴趣权重第一版用什么规则。若不拍板，开发会在“聊天产品”“知识库产品”“文件整理产品”“推荐系统”之间摆动。

用户已同意以下建议组合：界面采用聊天 + agent inbox；输入先限于显式材料；动作先做读侧整理；Memory Ledger 对用户可查看 / 编辑 / 删除；兴趣权重先用简单可解释规则。

## 备选方案

**界面形态**：

- 方案 A：纯聊天窗口。
- 方案 B：聊天窗口 + agent inbox。
- 方案 C：独立知识库 / 笔记管理界面。

**输入范围**：

- 方案 D：文本、链接、摘录、会话沉淀、手动丢进 inbox 的材料。
- 方案 E：在 D 基础上加入小批量文件上传。
- 方案 F：在 E 基础上加入邮箱、网盘、浏览器历史、内容平台等外部数据。

**动作范围**：

- 方案 G：只做总结、分类、打标签、生成候选整理方案。
- 方案 H：允许自动写入索引 / 修改元数据。
- 方案 I：允许移动、重命名、删除文件。

**Memory 可见性**：

- 方案 J：Memory Ledger 隐藏，只在内部使用。
- 方案 K：提供可查看 / 编辑 / 删除的记忆列表。
- 方案 L：每次写入记忆都弹确认。

**兴趣权重规则**：

- 方案 M：简单规则：选择 / 投喂 / 确认升权，长期不触达降权。
- 方案 N：复杂算法：按行为频率、内容相似度、时间衰减综合算分。
- 方案 O：不自动衰减，只手动调整。

## 被否决方案与理由

**方案 A 纯聊天窗口**：

- 能快速启动，但缺少稳定的待整理材料容器。
- 会把信息整理退回一次性问答，难以观察材料进入、分类、纠错和记忆回写的闭环。

**方案 C 独立知识库 / 笔记管理界面**：

- 功能面过重，容易把 MVP 拖成笔记产品。
- 第一版要验证的是 agent 理解、候选生成和记忆复用，不是知识库编辑器。

**方案 E / F 扩大输入范围**：

- 小批量文件上传可以作为紧随其后的第二步，但不应阻塞第一版。
- 外部数据源会引入 OAuth、同步、隐私解释、失败恢复和平台限制，不适合作为 MVP 默认入口。

**方案 H / I 自动写入或文件操作**：

- 会过早进入 L1 / L2 / L3 动作边界，增加确认、回滚和误操作成本。
- 第一版应先验证“能不能理解和提出好候选”，再扩大到写入与移动。

**方案 J 隐藏 Memory Ledger**：

- 与 ADR-011 的可解释 / 可控方向不匹配。
- user 不知道 agent 记住了什么，会放大误用记忆带来的不信任。

**方案 L 每次写入都确认**：

- 安全但打扰，会削弱管家感。
- 更适合敏感记忆或低置信推断，不适合作为所有记忆的默认流程。

**方案 N / O 复杂算法或纯手动调整**：

- 复杂算法第一版不透明，不利于 dogfood 时调试。
- 纯手动调整会让兴趣画像维护成本回到 user 身上，违背产品要降低表达和管理成本的方向。

## 决策

MVP 第一版采用以下组合：

1. **界面形态 = 聊天窗口 + agent inbox**。
   - 聊天用于表达意图、澄清、候选生成和反馈。
   - inbox 用作 user 主动投喂的待整理材料容器。

2. **输入范围 = 显式材料优先**。
   - 支持文本、链接、摘录、会话沉淀、手动丢进 inbox 的材料。
   - 小批量文件上传后置为第二步。
   - 邮箱、网盘、浏览器历史、内容平台习惯数据后置，不进入 MVP 默认入口。

3. **动作范围 = 读侧整理优先**。
   - 第一版只做总结、分类、打标签、生成候选整理方案。
   - 不默认写入外部系统，不默认修改文件内容，不默认移动 / 重命名 / 删除文件。
   - 如后续加入写索引或文件操作，必须重新按 ADR-004 做动作分级与确认设计。

4. **Memory Ledger = 可查看 / 可编辑 / 可删除**。
   - 不隐藏关键记忆。
   - 不对每条普通记忆都强制弹窗确认。
   - 敏感记忆、低置信推断、身份 / 价值观相关记忆需要更严格确认。

5. **兴趣权重 = 简单可解释规则**。
   - user 选择、主动投喂、明确确认可升权。
   - 长期未触达、未确认、无新材料进入默认降权。
   - user 可 pin、降权、归档或删除关注主题。
   - 第一版不采用黑盒推荐模型。

这组切片的目标不是做完整知识库，也不是做全自动文件管家，而是验证四件事：user 是否愿意把材料交给 inbox、agent 是否能给出有用候选、Memory 是否减少重复表达、兴趣权重是否减少噪声。

实现承载位置由 ADR-015 决定：MVP 产品代码进入 dawning-agent-os 本仓库，docs/ 继续作为内置 LLM-Wiki。

桌面技术栈由 ADR-016 决定：MVP 采用 Electron 桌面壳 + ASP.NET Core 本地后端。

## 影响

**正向影响**：

- 第一版形态足够窄，能尽快进入 dogfood。
- 聊天 + inbox 同时保留自然交互和可观察材料流。
- 读侧整理把风险压低，避免第一版被文件操作、外部写入和权限问题拖住。
- 可见 Memory Ledger 与简单兴趣衰减能让错误更容易被发现和纠正。

**代价 / 风险**：

- 第一版自动化感会弱于能直接移动文件或接入外部数据源的方案。
- 用户需要主动投喂材料，无法自动发现历史资料。
- 如果 inbox 体验太弱，用户可能不会形成持续使用习惯。
- 读侧整理如果不能带来足够省事感，需要尽快复议是否加入写索引或小批量文件能力。

## 复议触发条件

`adr_revisit_when` 已写入 front matter（见 SCHEMA §4.3.2 / §6.0），本节不重复。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：本 ADR 对应的 MVP 第一版切片。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-004 重要性级别与确认机制](important-action-levels-and-confirmation.md)：定义动作分级与确认边界。
- [ADR-005 MVP 主场景选型 = 信息整理](mvp-main-scenario-information-curation.md)：定义 MVP 主场景。
- [ADR-011 Memory MVP 采用显式记忆账本](explicit-memory-ledger-mvp.md)：定义 Memory Ledger。
- [ADR-012 MVP 输入边界：不默认读取用户文件夹](mvp-input-boundary-no-default-folder-reading.md)：定义输入边界。
- [ADR-013 兴趣画像采用权重与时间衰减](interest-profile-weighting-and-decay.md)：定义兴趣权重与衰减。
- [ADR-015 仓库形态：产品 monorepo + 内置 LLM-Wiki](repository-shape-product-monorepo-with-wiki.md)：定义第一版产品代码的承载仓库。
- [ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端](mvp-desktop-stack-electron-aspnetcore.md)：定义第一版产品技术栈。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。