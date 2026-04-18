# Agent Framework 源码解剖库

> 以**模块为单位**逐框架深读开源 Agent 框架源码，产出可供架构师 / 研发 / Dawning 设计参考的"第二官方文档"。
>
> - 纵向：每个框架独立子目录，从 overview 到核心模块 5-10 篇
> - 横向：`_cross-module-comparison/` 同类模块跨框架对比
> - 可视化：每模块配 Mermaid 架构图 / 时序图 / 模块 DAG
> - 映射：每模块末尾给出对 Dawning 对应 Layer / Interface 的映射

---

## 1. 目录约定

```
frameworks/
├── README.zh-CN.md                    # 本文件
├── <framework>/
│   ├── 00-overview.zh-CN.md           # 定位 / 仓库 / 版本 / 依赖
│   ├── 01-architecture.zh-CN.md       # 整体架构图 + 模块边界
│   ├── 02..N-<module>.zh-CN.md        # 核心模块逐一源码解析
│   ├── cases/                         # 产品级案例剖析
│   │   ├── README.zh-CN.md            # 案例索引
│   │   ├── <product-slug>.zh-CN.md
│   │   └── diagrams/
│   └── diagrams/
│       ├── module-map.mmd
│       └── *.mmd
├── _cross-module-comparison/          # 同类模块横切对比
│   ├── state-model.zh-CN.md
│   ├── tool-schema.zh-CN.md
│   ├── streaming-event.zh-CN.md
│   ├── checkpoint-impl.zh-CN.md
│   ├── handoff-routing.zh-CN.md
│   └── observability.zh-CN.md
└── _cross-case-comparison/            # 同场景跨框架案例对比
    ├── customer-support-agents.zh-CN.md
    ├── coding-agents.zh-CN.md
    ├── research-agents.zh-CN.md
    └── enterprise-copilots.zh-CN.md
```

---

## 2. 模块解析文档统一模板

每份 `NN-<module>.zh-CN.md` 必含：

```yaml
framework: <name>
version: <version>
module: <module-id>
type: source-analysis
repo-path: <relative-path>
```

正文章节：

1. **模块职责** —— 在整体架构中的位置
2. **对外 API** —— 公开接口 / 类
3. **源码结构** —— 文件清单 + GitHub permalink + 行号
4. **核心类型** —— class / protocol 骨架
5. **关键流程** —— sequence diagram / state machine
6. **扩展点** —— 用户如何自定义
7. **已知限制 / 陷阱**
8. **依赖关系** —— 模块间上下游
9. **Dawning 映射** —— 对应 Layer / Interface
10. **参考链接**

---

## 2.1 案例剖析文档模板

每份 `cases/<product-slug>.zh-CN.md` 必含：

```yaml
framework: <name>
case: <slug>
case-type: production | open-source | reference
url: <官方 blog / repo>
last-verified: <YYYY-MM>
```

正文章节：

1. **产品背景** —— 公司 / 场景 / 规模 / 选型原因
2. **整体架构** —— 系统全景图 + Agent 拓扑图（Mermaid）
3. **框架用法映射** —— 用了哪些模块 / State 设计 / 节点划分
4. **数据与记忆** —— Memory 分层 / 数据源 / RAG
5. **工具集** —— Tool / MCP / API / 安全边界
6. **可观测与运维** —— Tracing / Eval / HITL / 成本
7. **关键工程决策** —— 为什么这么切图 / 踩过的坑
8. **对 Dawning 的启示** —— 可复用模式
9. **局限与未解** —— 未披露部分（需标注"推测"）
10. **参考资料** —— 一手博客 / talk / paper / repo

**三档案例来源**：

| 档次 | 来源 | 可信度 |
|------|------|--------|
| A | 公司官方博客 / talk / paper | 高，可引用 |
| B | 开源产品仓库（可直接读代码） | 极高 |
| C | 基于公开 demo / 行为推测 | 中，必须标注"推测" |

---

## 3. 框架矩阵（2026-04）

| 框架 | 语言 | 版本 | 定位 | 源码解剖状态 |
|------|------|------|------|-------------|
| **LangGraph** | Python/TS | 1.x | 有状态图编排 | 🚧 起步 |
| **OpenAI Agents SDK** | Python/TS | 0.x | 极简 Agent Loop | ⏳ 计划 |
| **Microsoft Agent Framework (MAF)** | .NET / Python | 1.x | 企业级 Agent + Workflow | ⏳ 计划 |
| **Semantic Kernel** | .NET / Python | 1.x | Kernel + Plugin | ⏳ 计划 |
| **CrewAI** | Python | 0.x | 角色化多 Agent | ⏳ 计划 |
| **Google ADK** | Python | 1.x | Vertex 生态 Agent | ⏳ 计划 |
| **AutoGen** | Python/.NET | 0.4+ | Actor 模型多 Agent | ⏳ 计划 |
| **LlamaIndex Workflows** | Python | 1.x | Event-Driven Workflow | ⏳ 计划 |
| **Pydantic AI** | Python | 0.x | Type-Safe Agent | ⏳ 计划 |
| **Mastra** | TS | 0.x | TypeScript 全栈 | ⏳ 计划 |

图例：✅ 完成 · 🚧 进行中 · ⏳ 计划

---

## 4. 阅读路径

### 4.1 选框架（横向）

- 先看 [agent-framework-landscape](../comparisons/agent-framework-landscape.zh-CN.md)
- 再看 [maf-vs-langgraph](../comparisons/maf-vs-langgraph.zh-CN.md) 等 1v1 对比
- 选定后进入本目录下的 `<framework>/00-overview`

### 4.2 读单框架（纵向）

```
00-overview          定位、依赖、版本节奏
01-architecture      模块地图 + 整体调用链
02..N-<module>       核心模块深读（按模块编号推进）
```

### 4.3 读同类模块（横切）

进入 `_cross-module-comparison/`：

- `state-model.zh-CN.md` —— 各框架"State"实现对比
- `tool-schema.zh-CN.md` —— 工具定义方式对比
- `streaming-event.zh-CN.md` —— 流式事件对比
- `checkpoint-impl.zh-CN.md` —— 持久化对比
- `handoff-routing.zh-CN.md` —— Agent 路由对比
- `observability.zh-CN.md` —— 可观测性对比

### 4.4 看产品案例（实战）

- 进入 `<framework>/cases/`：读该框架下的生产 / 开源案例
- 进入 `_cross-case-comparison/`：同一场景（客服 / 代码 / 研究 / 企业 copilot）下不同框架的选型对比

### 4.5 四重视角小结

| 视角 | 目录 | 回答 |
|------|------|------|
| 源码模块 | `<framework>/02..N-*` | "这块代码怎么工作" |
| 框架全貌 | `<framework>/00-01` | "这个框架整体设计" |
| 同类模块 | `_cross-module-comparison/` | "不同框架的 State / Tool 都怎么做" |
| 产品案例 | `<framework>/cases/` | "用这个框架真的建出什么" |
| 同场景跨框架 | `_cross-case-comparison/` | "同一种产品用不同框架怎么选" |

---

## 5. 源码阅读约定

### 5.1 引用格式

引用源码必须使用 GitHub permalink（含 commit SHA 或 tag），格式：

```
[langchain-ai/langgraph · libs/langgraph/langgraph/pregel/__init__.py#L120-L145](https://github.com/langchain-ai/langgraph/blob/v1.1.6/libs/langgraph/langgraph/pregel/__init__.py#L120-L145)
```

### 5.2 代码片段

- 优先"**骨架化**"（签名 + 注释 + 关键分支），不照搬实现
- 超过 40 行的块须改写为表格 / 伪代码 / 图
- 必须标明源文件与行号

### 5.3 图示

- **源文件**：Mermaid（`flowchart`, `sequenceDiagram`, `stateDiagram-v2`, `classDiagram`），存为 `.mmd` 放在模块目录的 `diagrams/` 下
- **产出**：渲染为同名 `.png`，**文档只引用 PNG**（避免依赖阅读器的 Mermaid 插件）
- **渲染命令**：`bash docs/scripts/render-mermaid.sh` —— 依赖 `mmdc`（`pnpm add -g @mermaid-js/mermaid-cli`）
- **Markdown 引用样式**：
  ```markdown
  ![模块地图](./diagrams/module-map.png)
  > 源文件：[`diagrams/module-map.mmd`](./diagrams/module-map.mmd)
  ```
- **重绘策略**：脚本按 mtime 增量渲染；修改 `.mmd` 后直接跑脚本即可

### 5.4 版本

- 每份文档头 `version:` 字段锁定分析时所用版本
- 后续版本若有破坏性变化，新建 `NN-<module>--v<x>.zh-CN.md` 或在原文增补 "版本变更" 节

---

## 6. Dawning 映射规则

每模块末尾给出：

| Dawning Layer | Dawning Interface | 对应关系 | 说明 |
|---------------|-------------------|---------|------|
| 例：Layer 2 | `IWorkingMemory` | 同构 | 此模块几乎 1:1 对应 |
| 例：Layer 4 | `IReasoningStrategy` | 部分 | 仅覆盖 ReAct 策略 |
| 例：Layer 7 | `IAgentEventStream` | 非对应 | 框架内部用，未暴露 |

用于：
- 设计 Dawning 接口时的参考
- 评估"我们是否该自己做 vs 接入"
- 生成 Dawning 与主流框架的 compat matrix

---

## 7. 贡献指南（自用）

新增一个框架解剖时：

1. 创建目录 `frameworks/<name>/`
2. 先写 `00-overview` + `01-architecture`
3. 在 `01-architecture` 给出**模块地图（DAG）**
4. 按模块逐篇写 `02..N`
5. 更新本 README 的"框架矩阵"
6. 在 `_cross-module-comparison/` 相应主题追加该框架的段落
7. 交叉链接至 `comparisons/` 中已有的主题对比

---

## 8. 与 entities/frameworks 的关系

- `entities/frameworks/<name>.zh-CN.md` —— **Profile 级**（版本、团队、生态、简要架构）
- `frameworks/<name>/` —— **源码级**（本目录，按模块深读）

Profile 文档会在本目录各框架的 `00-overview` 顶部引用，作为背景阅读。

---

## 9. 延伸阅读

- [[../index]] — 知识库主入口
- [[../comparisons/agent-framework-landscape.zh-CN]] — 框架全景
- [[../comparisons/framework-modules-mapping.zh-CN]] — 模块 ↔ Dawning 映射总览
- [[../comparisons/maf-vs-langgraph.zh-CN]] — MAF vs LangGraph 1v1
- [[../concepts/dawning-capability-matrix.zh-CN]] — Dawning 能力矩阵
