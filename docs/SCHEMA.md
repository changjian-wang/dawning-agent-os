# SCHEMA.md — Dawning Agent OS Wiki 模式定义

> 本文件是 LLM Wiki 的核心配置文件。它定义了 wiki 的结构约定、工作流规则和维护标准。
> LLM 在操作 wiki 时必须遵循此 Schema。人类和 LLM 共同演化此文件。
>
> **术语变更**（2026-04-17）：项目从 "Agent Framework" 更名为 "Agent OS"。
> 命名空间从 `Dawning.AgentFramework.*` 变更为 `Dawning.AgentOS.*`。

---

## 1. 架构总览

```
docs/                              # Obsidian 知识库根目录
├── SCHEMA.md                      # ⚙️  本文件 — Wiki 模式定义
├── index.md                       # 📋  内容索引（LLM 每次 Ingest 更新）
├── log.md                         # 📅  操作日志（追加式）
│
├── raw/                           # 🔒 原始资料（不可变）
│   ├── papers/                    #    arXiv 论文、学术论文
│   ├── articles/                  #    技术博文、Gist、演讲
│   ├── repos/                     #    GitHub 项目分析笔记
│   ├── official/                  #    官方文档摘录
│   ├── meetings/                  #    会议/讨论记录
│   └── assets/                    #    图片、图表等附件
│
├── entities/                      # 📄 实体页（具名事物）
│   └── frameworks/                #    Agent 框架实体页
│
├── concepts/                      # 💡 概念页（模式、原理、技术）
│
├── comparisons/                   # ⚖️  对比分析
│
├── decisions/                     # 🏗️  架构决策记录（ADR）
│
└── synthesis/                     # 🔬 综合分析（跨源交叉洞察）
```

## 2. 层级规则

### 2.1 Raw Sources（原始资料层）

- **路径**：`raw/{category}/{filename}.md`
- **所有者**：人类（LLM 只读，永不修改）
- **可变性**：不可变。添加后不修改、不删除
- **命名**：`{来源简称}.md`，例如 `memento-skills-2603.18743.md`
- **用途**：真实来源。Wiki 中的所有声明都应可追溯到 raw 层

### 2.2 Wiki Pages（编译产物层）

Wiki 页面分为四类：

| 类型 | 路径 | 说明 | 示例 |
|------|------|------|------|
| **Entity** | `entities/{subcategory}/` | 具名事物的专属页面 | `entities/frameworks/semantic-kernel.md` |
| **Concept** | `concepts/` | 模式、原理、技术概念 | `concepts/memento-skills.md` |
| **Comparison** | `comparisons/` | 多实体对比分析 | `comparisons/agent-framework-landscape.md` |
| **Decision** | `decisions/` | 架构决策记录 | `decisions/roadmap-90-days.md` |
| **Synthesis** | `synthesis/` | 跨领域综合洞察 | `synthesis/memory-plane-design.md` |

- **所有者**：LLM（人类阅读、LLM 编写和维护）
- **可变性**：持续更新。每次 Ingest 可能触及多个页面

## 3. 页面格式

### 3.1 YAML Frontmatter（必填）

每个 wiki 页面必须包含 YAML frontmatter：

```yaml
---
title: 页面标题
type: entity | concept | comparison | decision | synthesis
tags: [tag1, tag2]
sources: [raw/papers/xxx.md, raw/articles/yyy.md]
created: 2026-04-07
updated: 2026-04-07
status: draft | active | stale | archived
---
```

| 字段 | 必填 | 说明 |
|------|------|------|
| `title` | ✅ | 页面标题 |
| `type` | ✅ | 页面类型 |
| `tags` | ✅ | 分类标签（用于 Dataview 查询） |
| `sources` | ✅ | 引用的原始资料路径列表 |
| `created` | ✅ | 创建日期 |
| `updated` | ✅ | 最后更新日期 |
| `status` | ✅ | 页面状态 |

### 3.2 正文结构

```markdown
# {标题}

> 一句话摘要。

## 核心内容
（主体内容）

## 交叉引用
- [[相关页面1]]
- [[相关页面2]]

## 来源
- [来源1](../raw/papers/xxx.md)
- [来源2](../raw/articles/yyy.md)
```

### 3.3 链接约定

- **Wiki 内部链接**：使用 Obsidian wikilink `[[页面名]]` 或 `[[路径/页面名|显示文本]]`
- **原始资料引用**：使用相对路径 `[标题](../raw/category/file.md)`
- **外部链接**：使用完整 URL `[标题](https://...)`

## 4. 操作工作流

### 4.1 Ingest（摄入新资料）

触发条件：人类将新资料放入 `raw/` 目录。

流程：

1. 读取原始资料
2. 与人类讨论关键要点
3. 在对应类别创建/更新 wiki 页面（可能触及多个页面）
4. 更新 `index.md`（新增/修改条目）
5. 追加 `log.md` 条目

日志格式：
```markdown
## [2026-04-07] ingest | {资料标题}
- 来源：`raw/{category}/{file}.md`
- 新建页面：{列表}
- 更新页面：{列表}
- 关键要点：{1-3 句话}
```

### 4.2 Query（查询）

触发条件：人类提出问题。

流程：

1. 读取 `index.md` 或搜索定位相关页面
2. 深入阅读相关页面
3. 综合回答，附引用
4. 若回答有持久价值 → 回写为新 wiki 页面或更新现有页面
5. 追加 `log.md` 条目

日志格式：
```markdown
## [2026-04-07] query | {问题摘要}
- 查阅页面：{列表}
- 回答要点：{1-3 句话}
- 回写页面：{列表，若有}
```

### 4.3 Lint（健康检查）

触发条件：人类请求或定期执行。

检查项：

- [ ] 页面间矛盾（新资料推翻旧结论）
- [ ] 过时声明（已被新资料取代）
- [ ] 孤立页面（无入站链接）
- [ ] 重要概念缺少独立页面
- [ ] 缺失交叉引用
- [ ] frontmatter 完整性（必填字段缺失）
- [ ] 状态为 `stale` 的页面需复查

日志格式：
```markdown
## [2026-04-07] lint | 健康检查
- 检查页面数：{n}
- 发现问题：{列表}
- 修复：{列表}
- 待办：{列表}
```

## 5. 命名约定

| 类型 | 命名规则 | 示例 |
|------|---------|------|
| 文件名 | `kebab-case.md` | `semantic-kernel.md` |
| 中文版 | `{name}.zh-CN.md` | `semantic-kernel.zh-CN.md` |
| 原始资料 | `{来源简称}.md` | `karpathy-llm-wiki.md` |
| 图片 | `{相关页面}-{描述}.png` | `three-plane-architecture-overview.png` |

## 6. 标签分类法

wiki 使用以下标签分类体系（用于 Obsidian Dataview 查询）：

### 领域标签
- `#agent` — Agent 相关
- `#memory` — 记忆系统
- `#skill` — 技能系统
- `#orchestration` — 编排协作
- `#llm-provider` — LLM 提供者
- `#observability` — 可观测性
- `#security` — 安全治理
- `#eval` — 评测评估

### 类型标签
- `#framework` — Agent 框架
- `#pattern` — 设计模式
- `#protocol` — 通信协议
- `#adr` — 架构决策记录
- `#research` — 研究参考

### 状态标签
- `#draft` — 草稿
- `#active` — 活跃
- `#stale` — 可能过时

## 7. 双语约定

- 每个 wiki 页面优先写中文版（`.zh-CN.md`），按需提供英文版
- 双语版本必须同步：更新一个时必须检查另一个是否需要同步更新
- `index.md` 统一使用中文
- `SCHEMA.md` 使用中文
- 原始资料保持其原始语言

## 8. 质量标准

### 必须遵循
- ✅ 每页有完整 YAML frontmatter
- ✅ 每个声明可追溯到 `raw/` 层资料
- ✅ 交叉引用使用 wikilink
- ✅ Ingest 后更新 `index.md` 和 `log.md`

### 禁止
- ❌ 修改 `raw/` 目录下的文件
- ❌ 无来源的声明（hallucination guard）
- ❌ 创建无 frontmatter 的 wiki 页面
- ❌ 删除 wiki 页面（用 `status: archived` 标记）

---

*Schema 版本：1.0 | 最后更新：2026-04-07 | 协作演化：人类 + LLM*
