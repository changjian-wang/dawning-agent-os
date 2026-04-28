---
title: Rule 实现前必须方案先行
type: rule
subtype: process
canonical: true
summary: 产品代码实现、目录生成、依赖引入和架构性修改前，必须先给出方案并获得确认。
tags: [agent, process]
sources: []
created: 2026-04-28
updated: 2026-04-28
verified_at: 2026-04-28
freshness: evergreen
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/repository-shape-product-monorepo-with-wiki.md, pages/adrs/mvp-desktop-stack-electron-aspnetcore.md]
part_of: [pages/hubs/agent-os.md]
level: 强制
---

# Rule 实现前必须方案先行

> 产品代码实现、目录生成、依赖引入和架构性修改前，必须先给出方案并获得确认。

## 规则

在 dawning-agent-os 中，产品实现永远遵循“方案先行”。

以下操作前必须先输出方案，并等待 user 确认：

- 新增产品目录结构，例如 apps/、src/、tests/ 下的新项目或大块 scaffold。
- 引入新的运行时、框架、数据库、LLM provider、打包工具或跨进程通信方式。
- 修改核心模块边界，例如 agent runtime、Memory、inbox、权限 / 动作分级、LLM provider 抽象。
- 生成大量文件或一次性铺开多个层级。
- 会影响 ADR / PURPOSE 中已落地产品边界的实现选择。

方案至少说明：目标、范围、目录 / 模块边界、关键技术选择、风险、验证方式，以及本次明确不做什么。

如果该方案回答了持久产品或架构问题，先写 ADR 或更新既有 ADR；如果只是局部实现计划，可在对话中确认后再执行。

## 正例

- 先给出 Electron + ASP.NET Core 本地后端的目录与进程模型方案，经确认后再 scaffold。
- 先说明 SQLite 存储哪些表、落在哪个用户数据目录、如何迁移，再创建数据库代码。
- 先列出 GPT / DeepSeek provider 抽象与配置方式，再新增 provider 实现。

## 反例

- 未经确认直接生成 apps/desktop、src/、tests/ 等完整目录树。
- 因为某框架看起来合适，直接安装依赖并改构建配置。
- 把“以后可能需要”的模块一次性全建出来，导致 MVP 范围膨胀。

## 相关页面

- [ADR-015 仓库形态：产品 monorepo + 内置 LLM-Wiki](../adrs/repository-shape-product-monorepo-with-wiki.md)：定义产品代码进入本仓库。
- [ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端](../adrs/mvp-desktop-stack-electron-aspnetcore.md)：定义第一版产品技术栈。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。