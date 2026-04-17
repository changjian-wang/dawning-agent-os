---
title: "Agentic Coding 深度解析：Claude Code / Cursor / Aider / Devin 架构对比"
type: comparison
tags: [agentic-coding, claude-code, cursor, aider, devin, windsurf, cline, roo-code]
sources: [comparisons/agent-framework-landscape.zh-CN.md, concepts/multi-agent-patterns.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Agentic Coding 深度解析：Claude Code / Cursor / Aider / Devin 架构对比

> Agentic Coding 是 2024-2026 最成熟、最赚钱的 Agent 品类。
> 从"补全代码"到"写完整个 feature"——Cursor / Claude Code / Devin / Aider 已经形成可工程化的模式。
>
> 这些工具的架构值得所有 Agent 框架作者研究——它们是**经过真实开发者每日打磨**的 Agent 设计范式。
> 本文深度拆解 9 个主流产品的架构模式，总结对通用 Agent 框架的启发。

---

## 1. Agentic Coding 的演化

### 1.1 五个世代

```
Gen 1: Tab 补全          (Copilot 2021-2023)
         ↓
Gen 2: Inline 多行补全   (Cursor Tab 2023)
         ↓
Gen 3: Chat 编辑         (Cursor Composer / Continue 2024)
         ↓
Gen 4: Agent 自主编辑     (Cursor Agent / Aider / Cline 2024)
         ↓
Gen 5: Headless 工程 Agent (Claude Code / Devin / OpenAI Codex CLI 2025)
```

### 1.2 Gen 4 与 Gen 5 分水岭

| 维度 | Gen 4（IDE 内 Agent） | Gen 5（Headless 工程 Agent） |
|------|---------------------|----------------------------|
| 载体 | VS Code / JetBrains | CLI / Terminal |
| 自主度 | 人看着改 | 长时间自主 |
| 任务粒度 | 几个文件 | 整个 repo / PR |
| 使用场景 | 写代码 | 布任务、回来看结果 |
| 代表 | Cursor Agent, Cline | Claude Code, Devin, Codex CLI |

---

## 2. 产品谱系（2026 现状）

### 2.1 闭源商业

| 产品 | 出品 | 模式 | 代表用户 |
|------|------|------|---------|
| **Claude Code** | Anthropic | CLI + Headless | 工程师日常 |
| **Cursor** | Anysphere | IDE (VS Code fork) + Agent | 主流 IDE 替代 |
| **Windsurf** | Codeium | IDE (VS Code fork) | Cursor 对手 |
| **Devin** | Cognition | Web 自主 Agent | 远程工程师 |
| **GitHub Copilot** | Microsoft | IDE 插件 + Agent | 企业广覆盖 |
| **Cody** | Sourcegraph | IDE + Enterprise | 企业代码库 |
| **Zed AI** | Zed | 原生 IDE + AI | 性能派 |
| **JetBrains AI Assistant** | JetBrains | IDE | Java/Kotlin 生态 |

### 2.2 开源

| 产品 | 模式 | 特点 |
|------|------|------|
| **Aider** | CLI | 最早 Agentic Coding 开源工具 |
| **Cline** (原 Claude Dev) | VS Code 插件 | 透明 Agent Loop |
| **Roo Code** (Cline fork) | VS Code 插件 | 更激进的自动化 |
| **Continue** | IDE | 可自定义 |
| **Plandex** | CLI | 计划驱动 |
| **GPT Engineer** | CLI | 项目脚手架 |
| **OpenHands** (OpenDevin) | Web | Devin 开源对手 |
| **SWE-Agent** | CLI | Princeton 学术 |

### 2.3 评估基准

- **SWE-Bench Verified** (500 条真实 GitHub issue)
- **SWE-Bench Multi-modal**
- **SWE-Lancer** (OpenAI, 真实工作报酬)
- **HumanEval / MBPP**（过时）
- **LiveCodeBench**
- **TerminalBench**（Anthropic）

2026 SWE-Bench Verified 头部成绩：Claude 4.5 / GPT-5 ≈ 70-80%。

---

## 3. Claude Code 架构解析

### 3.1 定位

**Headless + CLI + 无状态每次启动**——Anthropic 自家生产力工具演化出的产品。

### 3.2 核心特性

#### 3.2.1 工具集（精简但强大）

```
read, write, edit, multi_edit, glob, grep, bash, webfetch, webtools,
task (subagent), todowrite
```

**设计原则**：工具尽量少、通用、正交。

#### 3.2.2 Plan Mode

- 进入 Plan Mode 后只读不写
- 产出 plan → 用户批准 → 执行
- 大任务前置思考

#### 3.2.3 Compaction（上下文压缩）

- 长对话自动压缩历史
- 保留关键信息 + 最近对话
- 解决 context window 持续消耗问题

#### 3.2.4 Sub-agent (Task)

- 主 Agent 派发隔离子任务
- 子任务 context 独立
- 返回总结到主 Agent

#### 3.2.5 Hooks（2025 引入）

```
PreToolUse / PostToolUse / UserPromptSubmit / Stop / SubagentStop ...
```

用户可注入脚本：
- 代码审查钩子
- 安全扫描
- Lint 自动运行
- 提交审批

#### 3.2.6 Skills

- 用户自定义能力包
- Claude Code 自动调用相关 skill

#### 3.2.7 Memory 文件

- `CLAUDE.md`（项目记忆）
- `~/.claude/CLAUDE.md`（全局）
- Agent 每次启动自动加载

### 3.3 架构图

```
CLI
 │
 ▼
Session Manager ── Memory Files (CLAUDE.md)
 │                ── Command History
 ▼
Agent Loop
 ├── Tool Registry (read/write/edit/bash/...)
 ├── Subagent Runtime (Task tool)
 ├── Hook Runtime
 ├── Skill Runtime
 ├── Compaction Engine
 └── Todo List
 │
 ▼
Claude Sonnet 4 / Opus 4
```

### 3.4 值得学习的点

- **Tool 极简**（10 个核心覆盖大部分场景）
- **Plan / Edit 两模式分离**（安全 + 可审）
- **Sub-agent context 隔离**（避免 context 互污染）
- **Memory 文件驱动**（用户可见、可编辑、可版本化）
- **Hook 机制**（企业可插治理）

---

## 4. Cursor 架构解析

### 4.1 定位

**IDE-Native + Agent + Composer**——融合补全、Chat、Agent 三种交互的 VS Code fork。

### 4.2 关键能力

#### 4.2.1 Cursor Tab

- 比 Copilot 更激进的多行预测
- 基于自家小模型（低延迟）
- 支持跨行跳跃编辑

#### 4.2.2 Composer（Chat 面板）

- 对话式多文件编辑
- @file / @folder / @web / @docs / @git 引用
- Diff 预览 + 批量 apply

#### 4.2.3 Agent Mode

- 自主读写、执行命令
- 任务驱动（"帮我加登录功能"）
- Checkpoint / Rollback

#### 4.2.4 Background Agent

- 云端异步跑 Agent
- 适合长任务、并行任务

### 4.3 架构要点

```
VS Code Fork
 │
 ├── Tab Model (小模型, 低延迟)
 ├── Composer (Chat UI + Inline Edit)
 ├── Agent Runtime
 │     ├── Tool: read/edit/apply_patch/terminal
 │     └── Checkpoint System
 ├── Indexer (代码库 embedding)
 ├── Context Assembly
 └── LLM Gateway (多模型路由)
```

### 4.4 值得学习的点

- **多模态交互**（Tab / Chat / Agent 一体）
- **代码库索引**（符号 + embedding 双路）
- **Checkpoint 文件系统**（Snapshot + Revert）
- **Apply Patch 范式**（不是写完整文件，是 diff）

---

## 5. Aider 架构解析

### 5.1 定位

**最早的开源 Agentic Coding CLI**——命令行纯粹派，极简哲学。

### 5.2 核心概念

#### 5.2.1 Repo Map

- 静态分析生成 repo 符号树
- 按相关性选择性注入 context
- 基于 tree-sitter

#### 5.2.2 Edit Formats

Aider 开创了多种 edit 格式：

- **whole**: 全文件替换（笨但稳）
- **diff**: Unified diff（小模型难生成）
- **diff-fenced**: 标记化 diff
- **udiff**: 改进 diff
- **editblock**: SEARCH/REPLACE 块（Aider 代表作）

```
<<<<<<< SEARCH
def foo():
    return 1
=======
def foo():
    return 2
>>>>>>> REPLACE
```

**设计权衡**：token 成本 vs 成功率 vs 模型兼容性。

#### 5.2.3 Git 集成

- 每次修改自动 commit
- 用户可 `git reset` 撤销

### 5.3 架构要点

```
CLI
 │
 ├── Repo Map (tree-sitter)
 ├── Coder
 │     ├── Edit Format 选择
 │     ├── Prompt Template
 │     └── Diff Apply
 ├── Git Runner
 └── LLM Client (OpenAI / Anthropic / 本地)
```

### 5.4 值得学习的点

- **Repo Map 智能 context**
- **Edit Format 的工程学**（Dawning 做 code edit skill 时参考）
- **Git 原生工作流**
- **精简 + 透明**（所有 prompt 可查）

---

## 6. Cline / Roo Code 架构解析

### 6.1 定位

**VS Code 插件 + 透明 Agent Loop**——把 Agent 每一步都展示给用户。

### 6.2 Cline 特点

- 每步工具调用前用户确认（或 auto-approve）
- 清晰展示 thinking / action / observation
- 支持 MCP
- 支持 Browser Use（playwright）
- 支持多模型（OpenRouter / Ollama / Anthropic / OpenAI）

### 6.3 Roo Code 特点（Cline fork）

- 更激进自动化
- Mode 系统：Code / Architect / Ask / Debug
- 自定义 Mode
- Orchestrator 模式（多 Mode 协作）

### 6.4 架构图

```
VS Code Extension
 │
 ├── UI Panel (Webview)
 │     ├── Message History
 │     ├── Diff Viewer
 │     └── Approval Buttons
 ├── Agent Runtime
 │     ├── ReAct Loop
 │     ├── Tool Registry
 │     │    ├── read_file, write_to_file, replace_in_file
 │     │    ├── execute_command
 │     │    ├── browser_action
 │     │    └── use_mcp_tool
 │     └── Auto-approve / HITL
 ├── MCP Client
 └── LLM Client (多 Provider)
```

### 6.5 值得学习的点

- **透明 Agent Loop 展示**（教育意义）
- **Auto-approve 粒度**（每种工具独立配置）
- **MCP 优先**
- **Mode 系统**（Roo Code 的 orchestrator 接近 Dawning Skill 概念）

---

## 7. Devin 架构解析

### 7.1 定位

**第一个商用"AI 软件工程师"**——Cognition 出品，Web 界面远程工作。

### 7.2 关键能力

- 完整虚拟机环境
- 浏览器 + 终端 + 编辑器
- 长任务（数小时到数天）
- 任务跟踪 / 进度报告
- Slack / GitHub 集成

### 7.3 推测架构

```
Web UI
 │
 ▼
Task Orchestrator
 │
 ├── Planning Agent
 ├── Execution Agent
 ├── Self-Reflection Loop
 └── Progress Reporter
 │
 ▼
Sandbox (Docker/VM)
 ├── Browser (Chromium + CDP)
 ├── Shell
 ├── Code Editor
 └── Git
```

### 7.4 值得学习的点

- **完整虚拟环境**（执行任意命令）
- **长任务支持**（持续数天）
- **主动报告**（不等用户问）
- **任务状态持久化**

### 7.5 开源对等：OpenHands (OpenDevin)

- CodeAct 范式（用代码行动）
- Agenthub（多个 Agent 可选）
- Runtime 抽象（本地/Docker/E2B）
- 与 Devin 学术对等

---

## 8. GitHub Copilot 演化

### 8.1 从 Tab 到 Agent

```
Copilot (2021)       - Tab 补全
  ↓
Copilot Chat (2023)   - IDE 侧边栏 Chat
  ↓
Copilot Workspace (2024) - Web 上 Plan→Edit→PR
  ↓
Copilot Agents (2025) - Task-based, 多 Agent
  ↓
Copilot Coding Agent (2026) - 异步 PR 自动化
```

### 8.2 Copilot Coding Agent

- 指派 GitHub Issue 给 Copilot
- 自动开 branch、写代码、跑测试、开 PR
- Review 循环
- 基于 Microsoft Agent Framework

### 8.3 值得学习

- **原生 GitHub 工作流集成**
- **PR 为单位的 Agent**
- **与 CI 整合**
- **企业级权限**

---

## 9. 对比矩阵

| 维度 | Claude Code | Cursor | Aider | Cline | Devin |
|------|-------------|--------|-------|-------|-------|
| 载体 | CLI | IDE Fork | CLI | VS Code Ext | Web |
| 开源 | ❌ | ❌ | ✅ | ✅ | ❌ (OpenHands ✅) |
| 自主度 | 高 | 中-高 | 中 | 中-高（可配） | 极高 |
| 特色 | Plan/Sub-agent/Hook/Skill | Tab + Composer + Agent | Repo Map + Edit Format | 透明 + MCP | 完整 VM |
| Context 压缩 | ✅ 原生 | ✅ | ✅ Repo Map | ⚠️ 靠模型 | ✅ |
| MCP | ✅ | ✅ | ⚠️ | ✅ | ⚠️ |
| 多 Agent | ✅ Sub-agent | ⚠️ Background | ❌ | Roo: ✅ | ✅ |
| Hook/扩展 | ✅ | ⚠️ | ❌ | ⚠️ | ⚠️ |
| Memory | CLAUDE.md | .cursorrules | .aider.conf | .clinerules | Notes |
| PR/Git | 命令化 | Git 集成 | 原生 | 命令化 | 自主 |
| 模型依赖 | Claude | 多模型 | 多模型 | 多模型 | 多模型 |

---

## 10. 共性模式（对框架作者的启发）

### 10.1 Tool 设计

**共同工具集**：
```
文件：read, write, edit (patch), glob, grep
执行：bash/terminal
浏览：browser (fetch + click)
搜索：web search
任务：task/subagent
内存：todo_write / memory
```

**设计原则**：
- 工具少、粒度正交
- 有 read/write 分离（支持 Plan Mode）
- 对文件操作优先 diff/patch，不写整文件

### 10.2 Context Assembly

```
Context = 
  System Prompt +
  Memory Files (CLAUDE.md / .cursorrules) +
  Repo Map (符号 or 目录) +
  Relevant Files (grep/embedding 召回) +
  Conversation History (压缩 or 滑窗) +
  Tool Results
```

**关键**：Context 是"装"出来的，不是"塞"出来的。

### 10.3 Edit Format

从 Aider 开始成为标准工程议题：

| Format | 成本 | 可靠 | 模型支持 |
|--------|------|------|---------|
| Whole file | 贵 | 高 | 所有 |
| Unified diff | 便宜 | 中 | 强模型 |
| SEARCH/REPLACE block | 中 | 高 | 所有 |
| AST patch | 便宜 | 极高 | 需定制 |

**趋势**：SEARCH/REPLACE + 结构化 JSON（Anthropic text_editor tool）。

### 10.4 HITL 粒度

```
Auto-approve 维度：
  - Read tools（总是允许）
  - Write tools（按文件路径 allow-list）
  - Execute（按命令 allow-list，或沙箱内任意）
  - Network（按域名）
  - Destructive（总是确认）
```

### 10.5 Sub-agent / 多 Agent

**共同模式**：
- 主 Agent 保持 overall context
- 子 Agent 接隔离子任务（搜索、分析、长工具链）
- 子 Agent 返回结构化结果
- 避免 context 污染

### 10.6 Plan / Edit 分离

- **Plan Mode**：只读 + 思考 → 产出 plan
- **User Approval**
- **Edit Mode**：执行

收益：
- 大任务质量提升
- 可审计
- 早期发现错误意图

### 10.7 Memory 文件

几乎所有工具都有：

```
.cursorrules / CLAUDE.md / .aider.conf.yml / .clinerules / AGENTS.md
```

**统一趋势**：`AGENTS.md`（2025 社区共识）—— Agent 项目规则共享格式。

### 10.8 Compaction

- 长任务必备
- 保留：最近 N 轮、关键决策、todo、重要文件 state
- 丢弃：早期 trial-and-error、冗余 tool output

### 10.9 Checkpointing

- 文件修改快照
- 命令副作用 log
- Rollback 能力

---

## 11. 反模式（避免踩坑）

### 11.1 "写一个万能 Agent"

失败原因：context 爆炸、决策混乱、可观测性差。
正确：任务 → plan → 小 sub-agent。

### 11.2 "工具越多越好"

失败原因：模型工具选择混乱、上下文臃肿。
正确：10 个以内正交工具 + MCP 动态扩展。

### 11.3 "靠 RAG 解决 context"

失败原因：代码非 RAG 友好（结构强、关联深）。
正确：Repo Map + 符号索引 + 工具按需读取。

### 11.4 "无视 Edit Format"

失败原因：模型生成的 diff 应用失败率高。
正确：选择匹配模型的 format，并测量成功率。

### 11.5 "无 HITL"

失败原因：Agent 执行破坏性命令。
正确：危险命令白名单 + 确认。

### 11.6 "不压缩历史"

失败原因：长任务 token 爆炸 + 模型能力衰减。
正确：Compaction + Sub-agent context 隔离。

---

## 12. 对 Dawning 的启发

### 12.1 Layer 3 Tool Registry

- 参考 Claude Code 的 10 核心工具作为默认 Skill Pack
- 按"文件/执行/浏览/搜索/任务/内存" 6 类正交

### 12.2 Layer 2 Memory

- `AGENTS.md` 作为项目级 Memory 文件标准
- IWorkingMemory 支持 Compaction 策略（钩子）
- Scope 设计对应 Sub-agent 隔离

### 12.3 Layer 4 Multi-Agent

- Sub-agent = 隔离 context 的短期 Agent
- 主 Agent 保持全局 plan
- 共享 Bus 传 summary / artifact

### 12.4 Layer 5 Skill

- Skill = Claude Code Skill / Cursor Rules 的合并
- 版本化、演化（见 [[concepts/skill-evolution.zh-CN]]）

### 12.5 Layer 7 治理

- Hook 机制（Claude Code Hooks）→ Dawning Policy Engine 执行点
- Allow-list / Deny-list
- 审计 trail

### 12.6 Dawning.Coding（可选产品）

基于 Dawning Kernel 的 Agentic Coding 产品：

```
Dawning.Coding
 ├── Repo Map (Roslyn for .NET + tree-sitter for others)
 ├── Core Tools (read/edit/bash/search/task)
 ├── Edit Format Router
 ├── Memory: AGENTS.md + ProjectRules
 ├── Sub-agent Runtime (用 Dawning Bus)
 ├── MCP Host (接所有 MCP 工具)
 ├── Hook Engine
 └── IDE Plugin (VS Code / JetBrains)
```

---

## 13. 边界与未来

### 13.1 当前能做好的

- 单 file bug 修复
- 中等 feature（数文件）
- 重构（有明确模式）
- 测试生成
- 文档生成
- Bug 定位

### 13.2 当前仍吃力

- 架构级决策（需要品味）
- 跨团队协作（需要人际）
- 复杂 debug（非文本线索）
- 生产事故响应
- 产品需求澄清

### 13.3 2026-2027 趋势

- **真实 CI 集成**：Agent 成为 CI 参与者
- **Code Review Agent** 成熟
- **Spec-first**：用 spec 驱动 Agent 写代码
- **Self-improving**：Agent 分析自己失败 → 改 prompt
- **Multi-agent 协作**：设计/实现/测试/审核分离
- **组织级 Agent**：整个团队的 AI 成员

### 13.4 企业采用成熟度

```
L0 探索: 开发者个人用 Cursor / Copilot
L1 团队: 统一订阅、内部 rules
L2 集成: CI 中跑 Agent、PR 自动审查
L3 Agent 作为 contributor: Copilot Coding Agent / Devin
L4 Agent 工厂: 定制 Agent + 内部 Skill 库（Dawning.Coding）
```

---

## 14. 评估框架

### 14.1 评估维度

- **成功率**（SWE-Bench 对应）
- **首次通过率**（无修改即可 commit）
- **Token 成本** / 任务
- **耗时** / 任务
- **代码质量**（lint / test / review 过率）
- **破坏率**（造成回归的比例）

### 14.2 企业内部评估集

- 取本公司 100 个历史 issue
- 构造"Agent 指令 + 预期 PR"pairs
- 每次模型 / 配置变更跑一次
- 指标：成功率 / 首次通过率 / 回归率

---

## 15. 小结

> Agentic Coding 是 Agent 领域**最真实的压力测试场**——
> 每天数百万开发者用 Cursor、Claude Code、Aider 产出代码，
> 工具设计、Context 组装、Edit Format、HITL、Sub-agent、Compaction——
> 这些模式被反复淘汰、迭代，成为通用 Agent 框架的黄金参考。
>
> Dawning 做 .NET-native Agent OS，Agentic Coding 是**必修**不是选修。

---

## 16. 延伸阅读

- [[comparisons/agent-framework-landscape.zh-CN]] — 18+ 框架全景
- [[concepts/multi-agent-patterns.zh-CN]] — 多 Agent 模式
- [[concepts/memory-architecture.zh-CN]] — Compaction 与 Scope
- [[concepts/skill-evolution.zh-CN]] — Skill 概念
- Claude Code: <https://docs.anthropic.com/claude/docs/claude-code>
- Cursor: <https://cursor.com>
- Aider: <https://aider.chat>
- Cline: <https://github.com/cline/cline>
- Roo Code: <https://github.com/RooVetGit/Roo-Code>
- OpenHands: <https://github.com/All-Hands-AI/OpenHands>
- SWE-Bench: <https://www.swebench.com/>
- AGENTS.md 共识: <https://agents.md>
