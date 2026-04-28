---
title: ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端
type: adr
subtype: architecture
canonical: true
summary: MVP 桌面端采用 Electron 作为成熟桌面壳，ASP.NET Core 作为本地 agent runtime 后端，并优先支持 Windows / macOS、GPT / DeepSeek 与本地 SQLite 数据目录。
tags: [agent, engineering, interaction-design]
sources: []
created: 2026-04-28
updated: 2026-04-28
verified_at: 2026-04-28
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/mvp-first-slice-chat-inbox-read-side.md, pages/adrs/repository-shape-product-monorepo-with-wiki.md, pages/adrs/mvp-input-boundary-no-default-folder-reading.md, pages/adrs/important-action-levels-and-confirmation.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-04-28
adr_revisit_when: Electron 维护状态或安全模型不再满足桌面产品要求、Windows / macOS 双平台维护成本阻碍 dogfood、GPT / DeepSeek 默认接入无法覆盖第一版需求、ASP.NET Core 本地后端与桌面壳耦合过重、MVP dogfood 证明包体 / 资源占用阻碍使用，或需要移动端 / Web-first 分发时。
---

# ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端

> MVP 桌面端采用 Electron 作为成熟桌面壳，ASP.NET Core 作为本地 agent runtime 后端，并优先支持 Windows / macOS、GPT / DeepSeek 与本地 SQLite 数据目录。

## 背景

ADR-014 已决定 MVP 第一版形态是聊天窗口 + agent inbox + 读侧整理；ADR-015 已决定 MVP 产品代码进入 dawning-agent-os 本仓库。下一步需要确定第一版产品的实际承载技术栈。

Dawning Agent OS 的目标不是做一个普通网页聊天工具，而是做个人 AI 管家。第一版虽然不默认扫描文件夹、不默认写外部系统，但仍需要贴近本地桌面使用：材料拖入 inbox、可见 Memory Ledger、本地权限提示、托盘 / 快捷入口、后续本地文件与系统集成。桌面 App 比纯 Web App 更贴近这个产品方向。

用户明确倾向成熟框架，并提出可使用 ASP.NET Core 做后端。后续又明确：第一版需要支持 Windows / macOS，默认接入 GPT / DeepSeek，数据存储按本地应用数据目录 + SQLite 的建议执行，且实现永远方案先行，不一次性生成目录或代码骨架。

基于这些偏好，本 ADR 在“成熟度、交付确定性、桌面能力、后端演进空间、双平台成本、默认模型可用性”之间做取舍。

## 备选方案

- 方案 A：Electron 桌面壳 + ASP.NET Core 本地后端。
- 方案 B：Tauri 桌面壳 + ASP.NET Core 本地后端。
- 方案 C：.NET MAUI / Blazor Hybrid，全 .NET 桌面应用。
- 方案 D：纯 Web App + ASP.NET Core 服务端。
- 方案 E：CLI 优先，后续再补桌面界面。

配套维度：

- 平台范围：Windows-only、Windows + macOS、三平台（Windows + macOS + Linux）。
- 默认 LLM provider：GPT / DeepSeek、Ollama、本地模型优先、OpenAI-compatible 泛化配置。
- 数据位置：仓库内开发数据、系统用户应用数据目录、云同步目录。

## 被否决方案与理由

**方案 B Tauri + ASP.NET Core**：

- 优点是轻量、现代、资源占用低。
- 但用户当前明确偏好成熟框架；Tauri 的整体生态、桌面边角案例、团队熟悉度与 Electron 相比不占优。
- 对 MVP 来说，包体和内存不是第一风险；产品闭环能不能尽快 dogfood 才是第一风险。

**方案 C .NET MAUI / Blazor Hybrid**：

- 全 .NET 技术栈一致性强。
- 但桌面 UI 打磨、Web 前端生态复用、跨平台桌面细节的确定性不如 Electron。
- 第一版需要快速做出聊天、inbox、Memory Ledger 这类交互密集 UI，不宜把风险押在桌面 UI 框架成熟度上。

**方案 D 纯 Web App**：

- 开发与部署路径简单。
- 但会过早引入账号、同步、远端隐私、密钥管理和部署问题。
- 本产品的北极星是个人 OS / 本地个人管家，纯 Web 会削弱本地材料、权限边界和桌面入口的产品气质。

**方案 E CLI 优先**：

- 最快验证后端命令链路。
- 但无法验证 ADR-014 的核心界面假设：聊天 + agent inbox + 可见 Memory Ledger。
- CLI 会把产品再次推向“高级用户工具”，偏离“不想写 prompt / 不想管理上下文”的目标用户。

**Windows-only**：

- 开发最快，但过早把个人桌面产品限制在单平台。
- 用户已明确第一版需要支持 Windows / macOS，因此不作为默认范围。

**三平台同时支持**：

- 覆盖面最大，但 Linux 桌面差异会增加打包、托盘、权限、自动更新和文件关联等边角成本。
- MVP 第一版应先保证 Windows / macOS dogfood，不把 Linux 作为同等优先级。

**Ollama / 本地模型优先**：

- 有隐私优势，但第一版默认效果、模型质量和配置门槛不如 GPT / DeepSeek 稳定。
- 可作为后续 provider 扩展，不进入默认接入。

**仓库内开发数据或云同步目录**：

- 仓库内数据会污染源码和 git 状态，不适合真实 dogfood。
- 云同步目录会过早引入同步冲突、隐私解释和跨设备一致性问题。

## 决策

采用方案 A：MVP 桌面端使用 **Electron + ASP.NET Core 本地后端**。

边界定义：

- Electron 负责桌面壳与 UI：窗口、托盘、快捷键、通知、文件拖拽、聊天界面、agent inbox、Memory Ledger 展示与确认交互。
- ASP.NET Core 负责本地 agent runtime：LLM provider、agent orchestration、Memory Ledger、inbox、兴趣权重、动作分级、操作记录和本地 API。
- 第一版平台范围：支持 Windows / macOS；Linux 暂不作为 MVP 必须支持平台。
- 第一版后端以本地服务形态运行，不作为远端云服务默认部署。
- 第一版通信采用 localhost + 随机端口 + 启动 token，优先保证可调试；如后续安全或平台限制要求更强隔离，再评估 named pipe / Unix domain socket。
- 第一版默认 LLM provider：GPT 与 DeepSeek。实现上保留 provider 抽象，避免把业务逻辑绑定到单一供应商。
- 第一版存储优先 SQLite，用于 inbox item、Memory Ledger、interest weights、operation log。
- 第一版数据目录使用系统用户应用数据目录：Windows 为 `%AppData%/DawningAgentOS/`，macOS 为 `~/Library/Application Support/DawningAgentOS/`。
- 默认不做账号系统、云同步和远端部署；这些不进入 MVP 第一刀。
- 实现遵循方案先行：本 ADR 只确定技术边界，不授权立即 scaffold；目录和项目生成必须先给出具体方案并获得确认。

工程目录建议：

- `apps/desktop/`：Electron 前端与桌面壳。
- `src/Dawning.AgentOS.Api/`：ASP.NET Core 本地后端。
- `src/Dawning.AgentOS.Core/`：agent runtime、memory、inbox、权限与动作分级等核心逻辑。
- `tests/`：后端核心与 API 测试。

## 影响

**正向影响**：

- 技术栈成熟，生态和故障案例丰富，符合用户“成熟框架优先”的偏好。
- Electron 能快速交付 chat / inbox / Memory Ledger 这类桌面 UI。
- ASP.NET Core 后端与未来 framework 抽取方向兼容，核心逻辑可先以产品代码存在。
- 本地后端保留了隐私、权限和 dogfood 的控制力，不必第一版就引入云服务复杂度。
- Windows / macOS 双平台覆盖了主要个人桌面环境，又避免三平台同时支持带来的过早复杂度。
- GPT / DeepSeek 默认接入更利于第一版质量和可用性验证。

**代价 / 风险**：

- Electron 包体和内存占用较高；MVP 阶段接受，后续 dogfood 观察是否影响使用。
- Electron + ASP.NET Core 是双运行时，打包、启动、进程管理和日志收集需要明确约定。
- localhost 通信需要防止被非本应用进程滥用；启动 token 只是第一版边界，不应被视为长期安全模型。
- 桌面壳与后端必须保持低耦合，否则未来抽取 framework 或替换 UI 会变困难。
- Windows / macOS 双平台意味着打包、自动更新、用户数据目录和权限差异要从第一版就纳入设计。
- GPT / DeepSeek 默认接入涉及 API Key 管理、网络错误处理、费用控制与模型可替换性。
- SQLite 本地数据目录需要迁移、备份、清理和隐私说明；不得把真实 dogfood 数据写入仓库目录。

## 复议触发条件

`adr_revisit_when` 已写入 front matter（见 SCHEMA §4.3.2 / §6.0），本节不重复。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：本 ADR 对应的产品契约与 MVP 技术形态。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-004 重要性级别与确认机制](important-action-levels-and-confirmation.md)：定义本地动作与确认边界。
- [ADR-012 MVP 输入边界：不默认读取用户文件夹](mvp-input-boundary-no-default-folder-reading.md)：定义本地材料读取边界。
- [ADR-014 MVP 第一版切片：聊天 + inbox + 读侧整理](mvp-first-slice-chat-inbox-read-side.md)：定义第一版界面与动作范围。
- [ADR-015 仓库形态：产品 monorepo + 内置 LLM-Wiki](repository-shape-product-monorepo-with-wiki.md)：定义产品代码承载仓库。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：定义实现前先方案、后确认、再执行。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。