# Phase -1: LLM 技术原理 — Agent 开发者必须掌握的基础

> 不懂 LLM 就写 Agent，相当于不懂 HTTP 就写 Web 框架。
> 本文从 Agent 开发者视角出发，只讲你需要知道的 LLM 知识。

## 目录

1. [LLM 是什么（一句话版本）](#1-llm-是什么)
2. [Token：LLM 的最小单位](#2-token-llm-的最小单位)
3. [Chat Completion API：你唯一需要调用的接口](#3-chat-completion-api)
4. [消息角色：system / user / assistant / tool](#4-消息角色)
5. [上下文窗口：LLM 的"工作记忆"](#5-上下文窗口)
6. [Temperature 与采样：控制 LLM 的"创造力"](#6-temperature-与采样)
7. [Function Calling：Agent 的核心能力](#7-function-calling)
8. [Streaming：流式响应的技术原理](#8-streaming)
9. [Structured Output：让 LLM 返回 JSON](#9-structured-output)
10. [Token 计费与成本控制](#10-token-计费与成本控制)
11. [Rate Limiting：API 限流](#11-rate-limiting)
12. [对 Agent Framework 设计的影响](#12-对-agent-framework-设计的影响)

---

## 1. LLM 是什么

**Large Language Model（大语言模型）**，本质是一个概率函数：

$$P(\text{next\_token} \mid \text{前面所有 token})$$

给定前面的文本，预测下一个 token 的概率分布，然后从中采样。重复这个过程就生成了一段文本。

**对 Agent 开发者意味着什么**：
- LLM 不是"思考"，是"预测下一个词"。它不理解你的代码，只是在模式匹配
- LLM 的输出是概率性的，同样的输入可能产生不同输出（除非 temperature=0）
- LLM 没有状态，每次调用都是独立的。"记忆"要靠你在每次请求中把历史消息都带上

---

## 2. Token：LLM 的最小单位

### 2.1 什么是 Token

Token 不是字符，不是单词，而是 LLM 分词器（tokenizer）切分后的最小单位。

```
输入: "Hello, world!"
Token: ["Hello", ",", " world", "!"]   → 4 个 token

输入: "你好世界"
Token: ["你好", "世界"]                  → 2 个 token（中文按词切分）

输入: "ChatCompletionOptions"
Token: ["Chat", "Completion", "Options"] → 3 个 token（驼峰拆分）
```

### 2.2 为什么 Agent 开发者必须理解 Token

| 原因 | 影响 |
|------|------|
| **计费按 Token** | 输入 1K token + 输出 500 token = 1.5K token 费用 |
| **上下文窗口按 Token** | GPT-4o 有 128K token 限制，不是 128K 字符 |
| **速度按 Token** | 生成速度约 50-100 token/秒，输出越长越慢 |
| **Agent 循环成本** | 每一步都要把所有历史消息发过去，Token 消耗是累积的 |

### 2.3 Token 估算经验值

| 语言 | 经验规则 |
|------|---------|
| 英文 | 1 token ≈ 4 个字符 ≈ 0.75 个单词 |
| 中文 | 1 token ≈ 1.5-2 个字符 |
| 代码 | 差异很大，一行代码可能 10-30 token |

**为什么这对 Agent 重要**：Agent 每一步执行都要把整个对话历史发给 LLM。假设每步平均 500 token：
- 第 1 步：发送 500 token
- 第 2 步：发送 1000 token
- 第 3 步：发送 1500 token
- ... 第 10 步：发送 5000 token

总消耗 = 500 + 1000 + ... + 5000 = **27,500 token**（等差数列求和）

这就是为什么 Agent Framework 需要：
1. `MaxSteps` 限制步数
2. `MaxCost` 限制成本
3. Memory 管理（自动压缩历史消息，类似 GC 自动回收内存）

---

## 3. Chat Completion API

### 3.1 这是 Agent 唯一需要调用的 LLM 接口

不管是 OpenAI、Azure OpenAI、Ollama 还是 Claude，核心 API 都是同一个模式：

**请求**：
```json
POST /v1/chat/completions
{
  "model": "gpt-4o",
  "messages": [
    {"role": "system", "content": "你是一个助手"},
    {"role": "user", "content": "今天天气怎么样？"}
  ],
  "temperature": 0.7,        // 可选，不传则用模型默认值
  "max_tokens": 1000,        // 可选，不传则用模型默认值
  "tools": [...],            // 可选：Function Calling
  "stream": false            // 是否流式
}
```

**响应**：
```json
{
  "id": "chatcmpl-xxx",
  "choices": [{
    "index": 0,
    "message": {
      "role": "assistant",
      "content": "我没有实时天气信息，但你可以...",
      "tool_calls": null
    },
    "finish_reason": "stop"
  }],
  "usage": {
    "prompt_tokens": 25,
    "completion_tokens": 30,
    "total_tokens": 55
  }
}
```

### 3.2 对 Framework 设计的影响

这个 API 结构直接决定了我们的核心抽象：

```
API 请求字段          →  Framework 抽象
─────────────────────────────────────────
messages[]           →  ChatMessage record
model                →  LLMOptions.Model
temperature          →  ChatCompletionOptions.Temperature
max_tokens           →  ChatCompletionOptions.MaxTokens
tools[]              →  ToolDefinition / IToolRegistry
stream               →  ChatAsync vs ChatStreamAsync
```

```
API 响应字段          →  Framework 抽象
─────────────────────────────────────────
choices[0].message   →  ChatCompletionResponse
content              →  string（最终回答）
tool_calls           →  ToolCall record（工具调用请求）
finish_reason        →  "stop" | "tool_calls" | "length"
usage                →  TokenUsage record（成本追踪）
```

**关键**：`finish_reason` 决定了 Agent 的下一步动作：
- `"stop"` → LLM 认为任务完成，返回最终答案
- `"tool_calls"` → LLM 想调用工具，Agent 需要执行工具并把结果反馈
- `"length"` → 输出被 max_tokens 截断，可能需要继续

---

## 4. 消息角色

### 4.1 四种角色

```json
[
  {"role": "system",    "content": "你是一个 SQL 专家..."},
  {"role": "user",      "content": "查询最近的订单"},
  {"role": "assistant", "content": null, "tool_calls": [...]},
  {"role": "tool",      "content": "查询结果...", "tool_call_id": "call_xxx"}
]
```

| 角色 | 谁写的 | 作用 | Agent 中谁负责构造 |
|------|--------|------|-------------------|
| `system` | 开发者 | 定义 Agent 的行为边界和人设 | Framework（从 Agent.Instructions 注入） |
| `user` | 用户 | 用户的输入/提问 | Framework（从 AgentContext.UserInput 获取） |
| `assistant` | LLM | LLM 的回复（文本或工具调用） | LLM 返回，Framework 记录到历史 |
| `tool` | 工具 | 工具执行结果 | Framework（执行完 ITool 后构造） |

### 4.2 System Prompt 的重要性

System prompt 是 Agent 的"灵魂"。同一个 LLM，不同的 system prompt 产生完全不同的行为：

```
System: "你是一个 JSON 生成器，只输出合法 JSON，不要输出任何其他内容"
User: "生成一个用户对象"
→ {"name": "John", "age": 30}

System: "你是一个诗人，用诗歌形式回答所有问题"  
User: "生成一个用户对象"
→ 在代码的花园里，有一位名叫 John 的旅人...
```

**为什么 Framework 要把 Instructions 作为 Agent 的核心属性**：
Instructions = System Prompt。它是区分"通用助手"和"SQL 专家"和"代码审计员"的关键。Framework 在每次 LLM 调用时自动注入 system prompt，开发者不需要手动管理。

### 4.3 消息顺序很重要

LLM 看到的是一个有序的消息数组。对话的"记忆"就是这个数组。

```
[system] → [user] → [assistant] → [user] → [assistant] → ...
```

使用工具时的完整循环：
```
[system]                        ← Agent 指令
[user]                          ← 用户输入
[assistant, tool_calls=[...]]   ← LLM 决定调用工具
[tool, tool_call_id=xxx]        ← 工具执行结果
[assistant]                     ← LLM 根据工具结果生成最终回答
```

**为什么 Framework 需要 IConversationMemory**：
管理这个消息数组——添加消息、自动压缩过长的历史（接近上限时用 LLM 生成摘要）、序列化/反序列化、计算 Token 数。

---

## 5. 上下文窗口

### 5.1 什么是上下文窗口

LLM 能"看到"的最大 Token 数量。超过这个限制，最早的消息会被丢弃或请求直接报错。

| 模型 | 上下文窗口 | 大约多少字 |
|------|-----------|-----------|
| GPT-3.5-Turbo | 16K | ~12,000 字 |
| GPT-4o | 128K | ~96,000 字 |
| Claude 3.5 Sonnet | 200K | ~150,000 字 |
| DeepSeek V3 | 128K | ~96,000 字 |
| Llama 3.2 (Ollama) | 128K | ~96,000 字 |

### 5.2 为什么 Agent 特别容易撞到上下文限制

普通聊天：一来一回，消息很少。
Agent：每一步都会产生大量消息（思考 + 工具调用 + 工具结果 + 回复）。

```
一个 10 步的 Agent 运行，消息数量：
  1 system + 1 user + (10 步 × 3 条/步) = 32 条消息

如果每条消息平均 200 token：
  32 × 200 = 6,400 token（仅消息，不含 tools 定义）

加上 tools 定义（每个工具约 100-200 token，10 个工具）：
  6,400 + 1,500 = ~8,000 token

这还是简单场景。如果工具返回了大段文本（比如文件内容），轻松超过 50K token。
```

### 5.3 各 Agent 框架如何处理上下文超限

#### ❌ 原始写法（我们文档之前的说法）

> "超出限制时，丢弃最早的消息（但保留 system prompt）"

这是最朴素的做法，但**不是现代框架的主流方案**。简单丢弃消息会导致：
- Agent 丢失关键上下文（比如用户在第 2 步说的约束条件）
- 工具调用链断裂（tool_call 和对应的 tool result 必须成对出现，丢了一半 LLM 会报错）
- 不可预测的行为（开发者不知道哪些消息被丢了）

#### 框架对比总览

要理解各框架的差异，关键问题是：**谁负责在 LLM 调用前裁剪消息？什么时候触发？**

| 框架 | 触发方式 | 触发时机 | 开发者需要写什么 |
|------|---------|---------|----------------|
| **OpenAI Agents SDK** | 手动 / 钩子 | 开发者自己选择何时裁剪 | 实现 `call_model_input_filter` 钩子，或手动调用 `result.to_input_list()` |
| **MS Agent Framework** | 自动（配置后） | Agent 每次调用 LLM 前自动执行 Reducer | 选择一个内建 Reducer 或实现 `IChatHistoryReducer` 接口，注册到 Agent 即可 |
| **LangChain** | 自动（组装后） | Chain 的管道中每次流经 `trim_messages` 节点时执行 | 把 `trim_messages()` 函数插入 Chain 管道中（`chain = trim | model`） |
| **CrewAI** | 透明 | Agent 内部自动用向量数据库检索相关记忆 | 无需管理消息列表，框架自动处理 |
| **Claude Agent SDK** | 全自动 | 接近上下文窗口上限时自动触发 | 什么都不用做；可选：在 CLAUDE.md 中写自然语言指令告诉压缩器保留什么，或用 `PreCompact` 钩子存档 |

**解释"触发方式"的区别**：

- **手动**：框架完全不管，开发者自己在代码的某个位置调用截断逻辑。框架只是提供了工具。
- **自动（配置后）**：开发者把 Reducer 注册到 Agent 上，之后每次 LLM 调用前框架自动执行，开发者无需再关心。
- **自动（组装后）**：开发者把 `trim_messages` 放进 Chain 管道。之后每次 Chain 被调用时，消息会先流过 trim 节点再到模型。它不是 Agent 级别的配置，而是管道中的一个处理步骤。
- **透明**：开发者甚至不知道截断在发生。框架用向量数据库存储记忆，每次只检索语义相关的片段，不存在"消息列表太长"的问题。
- **全自动**：框架监控上下文使用量，接近上限时自动用 LLM 把旧消息压缩成摘要。开发者零配置即可工作，想调优可以用钩子或自然语言指令。类似 C#/Java 的 GC——你不需要手动释放内存，但可以通过 `GC.Collect()` 或 GC 调参来影响行为。

> **注**：Microsoft Agent Framework（`microsoft/agent-framework`，2026-04-02 发布 v1.0.0）是 Semantic Kernel + AutoGen 的继任者，提供了从两者的官方迁移指南。下文中提到的 `IChatHistoryReducer` 源自 Semantic Kernel，已被 MS Agent Framework 继承。

---

#### ✅ OpenAI Agents SDK（不自动截断 + 提供钩子）

OpenAI Agents SDK **不会自动截断消息**，而是提供 4 种策略让开发者选择：

| 策略 | 管理方 | 适用场景 | 原理 |
|------|--------|---------|------|
| `result.to_input_list()` | 客户端（手动） | 简单聊天，需要完全控制 | 开发者手动拼接上一轮输出 + 新用户输入 |
| `Session`（SQLite/Redis） | 客户端（自动） | 持久化聊天，可恢复 | SDK 自动存取历史，带 `limit` 参数控制数量 |
| `conversation_id` | OpenAI 服务端 | 跨服务共享对话 | 服务端维护完整历史，客户端只发新消息 |
| `previous_response_id` | OpenAI 服务端 | 轻量级多轮续接 | 链式引用上一个 response，无需创建资源 |

加上 `truncation` 参数（`"auto"` 让 API 服务端智能截断）和 `call_model_input_filter` 钩子：

```python
def drop_old_messages(data: CallModelData) -> ModelInputData:
    trimmed = data.model_data.input[-5:]  # 只保留最近 5 条
    return ModelInputData(input=trimmed, instructions=data.model_data.instructions)
```

**设计哲学**：Framework 不擅自做决定，提供机制让开发者实现自己的策略。

---

#### ✅ Microsoft Agent Framework / Semantic Kernel（内建 Reducer 接口，.NET 生态的标准方案）

Microsoft Agent Framework（继承自 Semantic Kernel）提供了 `IChatHistoryReducer` 接口 + 两个内建实现：

```csharp
// 接口定义
public interface IChatHistoryReducer
{
    Task<IEnumerable<ChatMessageContent>?> ReduceAsync(
        IReadOnlyList<ChatMessageContent> chatHistory,
        CancellationToken cancellationToken = default);
}
```

**两个内建实现**：

1. **`ChatHistoryTruncationReducer`** — 截断并丢弃旧消息

```csharp
// 只保留 system 消息 + 最近 2 条用户消息
var reducer = new ChatHistoryTruncationReducer(targetCount: 2);

// 在每次 LLM 调用前执行
var reducedMessages = await reducer.ReduceAsync(chatHistory);
if (reducedMessages is not null)
{
    chatHistory = new ChatHistory(reducedMessages);
}
```

2. **`ChatHistorySummarizationReducer`** — 截断 + 用 LLM 把被移除的消息压缩成一条摘要

两者**都会自动保留 system 消息**（这点很关键）。

**设计哲学**：提供接口 + 常用实现，开发者也可以自定义 Reducer。

---

#### ✅ CrewAI（完全不同的范式：语义记忆）

CrewAI 走了一条完全不同的路——不维护消息列表，而是用**向量数据库 + 语义检索**：

```python
memory = Memory()

# 存储——LLM 自动推断 scope、分类、重要性
memory.remember("我们决定用 PostgreSQL 作为用户数据库。")

# 检索——按复合分数排名（语义相似度 + 时效性 + 重要性）
matches = memory.recall("我们选了什么数据库？")
```

**复合评分公式**：
$$composite = w_{semantic} \times similarity + w_{recency} \times decay + w_{importance} \times importance$$

其中 $decay = 0.5^{age\_days / half\_life\_days}$（指数衰减）。

**核心特点**：
- 不存原始消息，存**原子事实**（用 LLM 从长文本中提取）
- 层级化 scope（`/project/alpha`、`/agent/researcher`）
- 自动去重和合并（consolidation）
- 隐私隔离（`private=True` + `source` 标签）

**设计哲学**：对话历史不重要，关键信息的检索才重要。适合长期运行的复杂 Agent 任务。

---

#### ✅ LangChain（工具函数 `trim_messages`）

LangChain 提供 `trim_messages()` 函数，可按 token 数或消息数裁剪：

```python
from langchain_core.messages import trim_messages

trimmed = trim_messages(
    messages,
    max_tokens=1000,
    strategy="last",          # 保留最新的（也可以 "first"）
    token_counter=model,      # 用模型自带的 tokenizer 计数
    include_system=True,      # 始终保留 system 消息
    start_on="human",         # 裁剪后第一条必须是 human 消息
)
```

**设计哲学**：提供灵活的工具函数，开发者组合进 chain 中使用。

---

#### 总结：五种设计流派

| 流派 | 代表 | 核心思路 | 优点 | 缺点 |
|------|------|---------|------|------|
| **钩子型** | OpenAI Agents SDK | 不自动截断，提供钩子让开发者拦截 | 最灵活，不会意外丢数据 | 开发者要自己写截断逻辑 |
| **接口型** | MS Agent Framework | 定义 Reducer 接口，配置后 Agent 自动执行 | 配置即生效，开箱即用 + 可扩展 | 需要理解 Reducer 概念 |
| **管道型** | LangChain | `trim_messages` 作为管道节点，数据流过时自动裁剪 | 声明式，组合灵活 | 和 Chain 管道耦合，不适用于非 Chain 场景 |
| **语义型** | CrewAI | 向量数据库 + 语义检索，不维护消息列表 | 长期记忆，自动检索相关上下文 | 复杂度高，需要向量数据库基础设施 |
| **自动压缩型** | Claude Agent SDK | LLM 摘要 + Subagent 分治 + prompt cache | 开发者零配置即可工作，可选调参 | 压缩质量依赖 LLM，每次压缩有额外 token 成本 |

#### 类比：上下文管理的演进 ≈ 内存管理的演进

| 阶段 | 内存管理 | 上下文管理 |
|------|---------|-----------|
| 手动 | C/C++ `malloc/free` | OpenAI Agents SDK（开发者自己截断） |
| 半自动 | C++ 智能指针 `unique_ptr` | MS Agent Framework（选一个 Reducer 注册上去） |
| 全自动 | C#/Java GC | Claude Agent SDK（自动压缩，开发者可调参） |

正如 C#/Java 的 GC 让开发者不再操心内存分配/释放，上下文管理的趋势也是**从开发者手动管理走向框架自动管理**。手动截断就像 `malloc/free`——灵活但容易出错；自动压缩就像 GC——开发者只需关心业务逻辑，框架负责在合适的时机回收空间。

#### dawning-agent-framework 的设计决策

采用**双层记忆架构**：短期用自动压缩，长期用语义记忆。

```
┌───────────────────────────────────────┐
│  短期记忆 (Working Memory)             │ ← 自动压缩型（参考 Claude Agent SDK）
│  · 当前会话的消息列表                    │
│  · 接近上限时自动压缩（类似 GC）         │
└──────────────────┬────────────────────┘
                   │ 会话结束时提取关键事实
┌──────────────────▼────────────────────┐
│  长期记忆 (Long-term Memory)           │ ← 语义记忆型（参考 CrewAI）
│  · remember() / recall() API          │
│  · 向量数据库 + 语义检索                │
│  · 多 Agent 共享 / 分布式存储           │
└───────────────────────────────────────┘
```

**短期记忆：Automatic Compaction（自动压缩）**

当对话历史接近上下文窗口上限时，框架**自动**用 LLM 把旧消息压缩成摘要，腾出空间继续工作。开发者**不需要手动配置 Reducer**。

```
对话进行中:
  [system, user1, assistant1, tool_call1, tool_result1, ..., user50, assistant50]
                                                                    ↑ 快要满了！
自动压缩后:
  [system, 📝摘要("之前分析了auth模块，发现3个bug，已修复2个..."), user49, assistant49, user50, assistant50]
```

三层策略：

1. **Prompt Caching** — 不变的内容（system prompt、工具定义）自动缓存，减少重复 token 成本
2. **Subagent 隔离** — 子任务拆到独立 Subagent，各有独立上下文，只返回摘要结果给父 Agent
3. **Automatic Compaction** — 兜底机制，接近上限时自动用 LLM 压缩旧消息为摘要

**长期记忆：语义记忆（Semantic Memory）**

跨会话的知识库，基于向量数据库 + 语义检索。本框架面向多 Agent 协作 + 分布式部署，长期记忆是一等公民：

- Agent 执行完任务后，框架自动提取关键事实存入向量数据库
- Agent 执行前，自动检索相关记忆注入 prompt
- 多 Agent 通过共享记忆交换知识，不需要传递完整对话历史
- 分布式场景下使用远程存储（Redis / PostgreSQL+pgvector）

**开发者可以做什么（可选，不是必须）：**

- 通过 `CompactionOptions` 配置短期记忆的保留策略
- 通过 `PreCompact` 钩子在压缩前存档完整记录
- 通过 Agent 指令用自然语言告诉压缩器什么信息必须保留
- 手动触发压缩（`agent.CompactAsync()`）
- 通过 `MemoryOptions` 配置长期记忆的复合评分权重、去重阈值、存储后端

**摘要语言与格式策略：**

压缩的目标是用最少的 token 保留最多的关键信息。语言选择和输出格式都会影响压缩效率：

| 策略 | 做法 | 理由 |
|------|------|------|
| **默认** | 跟随对话语言（自动检测） | 避免中英混杂导致 LLM 理解偏差 |
| **可配置** | `CompactionOptions.Language` | 让用户可以强制指定摘要语言 |
| **结构化格式** | 要点列表，不用长段落 | 结构化比语言选择节省更多 token |

> **关于中文 vs 英文**：中文的自然语言概括能力确实更紧凑，但在 Agent 场景中效果有限——技术术语（类名、方法名、文件路径、错误信息）本身就是英文，不会因为摘要用中文就变短。而且主流 LLM 的 tokenizer 以英文语料训练为主，中文的 token 效率优势在 tokenizer 层面被削弱。**真正节省 token 的是结构化格式，而非语言选择。**

默认的压缩指令模板（内置在框架中，开发者可覆盖）：

```
Summarize the conversation history. Use the following format:
- Key decisions: [bullet list]
- Modified files: [file paths]
- Pending tasks: [bullet list]
- Keep original identifiers (class names, method names, paths) as-is, do not translate.
Use the same language as the conversation.
```

**扩展点：**

- `IContextCompactor` 接口 — 开发者可替换默认的 LLM 摘要策略
- `ILongTermMemory` 接口 — 开发者可替换默认的向量数据库后端
- `call_model_input_filter` 钩子 — LLM 调用前的最后拦截点
- 工具结果自动截断 — 限制单个工具返回的最大长度（默认启用）

---

## 6. Temperature 与采样

### 6.1 Temperature 参数

控制 LLM 输出的"随机性"。取值范围 0.0 ~ 2.0。

**数学原理**：LLM 每一步预测下一个 token 时，输出的是一个概率分布（softmax）。Temperature 在 softmax 之前对 logits 做缩放：

$$P(token_i) = \frac{e^{z_i / T}}{\sum_j e^{z_j / T}}$$

- $z_i$ 是模型对 token $i$ 的原始得分（logit）
- $T$ 就是 temperature

**直观理解**：假设 LLM 预测下一个 token，原始 logit 是：

| Token | Logit ($z$) |
|-------|------------|
| "北京" | 5.0 |
| "上海" | 3.0 |
| "广州" | 1.0 |

不同 Temperature 下的概率分布：

| Token | T=0.5（低温） | T=1.0（默认） | T=2.0（高温） |
|-------|------------|------------|------------|
| "北京" | **93.6%** | 84.4% | 57.6% |
| "上海" | 6.1% | 11.4% | 24.2% |
| "广州" | 0.3% | 4.2% | 18.2% |

**规律**：
- **T → 0**：概率极端集中到最高分，几乎确定性选择 → "北京" 每次都选
- **T = 1.0**：原始分布，不做调整
- **T → ∞**：概率趋于均匀分布，完全随机 → 三个 token 各 33%

**比喻**：把 LLM 想象成一个考生在做选择题：
- **T=0**（冰冷理性）：永远选最有把握的答案，绝不冒险
- **T=0.7**（正常发挥）：大部分时候选最优，偶尔会选次优答案
- **T=1.5**（喝醉了）：答案飘忽不定，可能选到低概率选项
- **T=2.0**（乱选）：几乎在瞎猜

### 6.2 Agent 场景该用什么 Temperature

| 场景 | 推荐 Temperature | 理由 |
|------|-----------------|------|
| 工具调用（function calling） | 0.0 - 0.3 | 工具名和参数必须准确，高温会导致 JSON 格式错误或选错工具 |
| 代码生成 | 0.0 - 0.2 | 代码语法不能出错 |
| 通用对话 | 0.5 - 0.7 | 自然流畅 |
| 创意写作 | 0.8 - 1.2 | 需要多样性 |

**实际效果**：
```
用户: "1+1等于几？"

T=0.0 → "2"         （100次都一样）
T=0.7 → "2"         （偶尔 "答案是2"，措辞略有变化）
T=1.5 → "2" / "二" / "答案是两个" / 偶尔"3"（开始出错）
```

### 6.3 各 Agent 框架如何处理 Temperature

| 框架 | temperature 默认值 | 设置位置 |
|------|-------------------|---------|
| **OpenAI Agents SDK** | `None`（不传） | `Agent(model_settings=ModelSettings(temperature=0.7))` |
| **MS Agent Framework** | `null`（不传） | `PromptExecutionSettings.Temperature`（继承自 Semantic Kernel） |
| **LangChain** | `0.7` | `ChatOpenAI(temperature=0.7)` |

**业界共识：默认 `null`（不传），而不是硬编码一个值。** 原因：

1. **不同模型默认值不同**：GPT-4o 默认 1.0，o1/o3 推理模型默认 1.0 且不支持修改
2. **传 `null` = 让 API 用最优默认值**，模型升级时自动适配
3. **显式设 `0.7` 可能和某些模型冲突**（推理模型不接受 temperature 参数，传了会报错）

**OpenAI Agents SDK 的做法**（最新、最有参考价值）：

```python
# 所有参数默认 None，不传给 API，由模型自己决定
class ModelSettings:
    temperature: float | None = None
    top_p: float | None = None
    frequency_penalty: float | None = None
    presence_penalty: float | None = None
    max_tokens: int | None = None
    tool_choice: ToolChoice | None = None
    parallel_tool_calls: bool | None = None

# 使用时按需设置
agent = Agent(
    name="Coder",
    model_settings=ModelSettings(temperature=0)  # 代码场景用 0
)
```

关键设计：`ModelSettings` 独立于 Agent 定义，且支持 `resolve()` 方法做**层级合并** — RunConfig 级别覆盖 Agent 级别。

**dawning-agent-framework 的决策：跟 OpenAI Agents SDK 一致，默认 `null`**：

```csharp
public record ChatCompletionOptions
{
    public float? Temperature { get; init; }    // null = 让模型决定
    public float? TopP { get; init; }           // null = 让模型决定
    public int? MaxTokens { get; init; }        // null = 让模型决定
}
```

### 6.4 其他采样参数

| 参数 | 作用 | 常用值 |
|------|------|--------|
| `top_p` | 核采样，只从概率累积达到 p 的 token 中采样 | 0.9-1.0 |
| `frequency_penalty` | 降低已出现 token 的概率，减少重复 | 0.0-0.5 |
| `presence_penalty` | 鼓励使用新 token，增加多样性 | 0.0-0.5 |

**一般不需要同时调 temperature 和 top_p**，选一个即可。

---

## 7. Function Calling：Agent 的核心能力

### 7.1 这是 Agent 和普通聊天的本质区别

没有 Function Calling，LLM 只能输出文本。有了 Function Calling，LLM 可以"请求"执行外部函数。

### 7.2 完整流程（逐步拆解）

**第一步：发送请求，带上 tools 定义**

```json
{
  "model": "gpt-4o",
  "messages": [
    {"role": "user", "content": "查询北京现在的天气"}
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "get_weather",
        "description": "获取指定城市的当前天气",
        "parameters": {
          "type": "object",
          "properties": {
            "city": {
              "type": "string",
              "description": "城市名称，如 '北京'"
            }
          },
          "required": ["city"]
        }
      }
    }
  ]
}
```

**第二步：LLM 返回 tool_calls（而不是 content）**

```json
{
  "choices": [{
    "message": {
      "role": "assistant",
      "content": null,
      "tool_calls": [{
        "id": "call_abc123",
        "type": "function",
        "function": {
          "name": "get_weather",
          "arguments": "{\"city\": \"北京\"}"
        }
      }]
    },
    "finish_reason": "tool_calls"
  }]
}
```

注意：
- `content` 是 `null` — LLM 选择了调用工具而不是直接回答
- `finish_reason` 是 `"tool_calls"` — 告诉你这不是最终答案
- `arguments` 是 JSON 字符串 — 需要反序列化
- `id` 是 `"call_abc123"` — 后面返回结果时要对应上

**第三步：程序执行工具，将结果作为 tool 消息返回**

```json
{
  "messages": [
    {"role": "user", "content": "查询北京现在的天气"},
    {"role": "assistant", "content": null, "tool_calls": [{"id": "call_abc123", ...}]},
    {"role": "tool", "tool_call_id": "call_abc123", "content": "北京：晴，25°C，湿度 40%"}
  ]
}
```

**第四步：LLM 看到工具结果，生成最终回答**

```json
{
  "choices": [{
    "message": {
      "role": "assistant",
      "content": "北京现在的天气是：晴天，气温 25°C，湿度 40%。"
    },
    "finish_reason": "stop"
  }]
}
```

### 7.3 tool_choice 参数

控制 LLM 是否调用工具：

| 值 | 含义 | 使用场景 |
|----|------|---------|
| `"auto"` | LLM 自己决定是否调用工具（默认） | 大多数场景 |
| `"none"` | 禁止调用工具，只生成文本 | 想要纯文本回答 |
| `"required"` | 必须调用至少一个工具 | 强制执行操作 |
| `{"function": {"name": "xxx"}}` | 强制调用指定工具 | 明确知道该执行什么 |

### 7.4 并行工具调用

LLM 可以在一次响应中请求调用多个工具：

```json
{
  "tool_calls": [
    {"id": "call_1", "function": {"name": "get_weather", "arguments": "{\"city\": \"北京\"}"}},
    {"id": "call_2", "function": {"name": "get_weather", "arguments": "{\"city\": \"上海\"}"}}
  ]
}
```

**Framework 需要**：
- 支持解析多个 tool_calls
- 可以并行执行多个工具（`Task.WhenAll`）
- 每个工具结果都要带上对应的 `tool_call_id`

### 7.5 Parameters Schema（JSON Schema）

LLM 需要知道工具接受什么参数。格式是 JSON Schema：

```json
{
  "type": "object",
  "properties": {
    "city": {
      "type": "string",
      "description": "城市名称"
    },
    "unit": {
      "type": "string",
      "enum": ["celsius", "fahrenheit"],
      "description": "温度单位"
    }
  },
  "required": ["city"]
}
```

**为什么 description 很重要**：LLM 根据 description 决定传什么值。描述越清晰，LLM 传参越准确。这直接影响 Agent 的可靠性。

**为什么 Framework 要自动生成 Schema**：
手写 JSON Schema 很繁琐。Framework 应该从 C# 方法签名自动生成：

```csharp
[FunctionTool("获取指定城市的当前天气")]
public string GetWeather(string city, string unit = "celsius")
→ 自动生成上面的 JSON Schema
```

---

## 8. Streaming：流式响应

### 8.1 为什么需要 Streaming

非流式：等 LLM 生成完所有文本后一次性返回。等待时间可能 5-30 秒。
流式：LLM 每生成一个 token 就立即返回。用户看到"打字机效果"。

**Agent 场景的特殊需求**：
- 用户等 Agent 执行 10 步可能要 1 分钟，没有流式反馈体验极差
- 流式还能实时显示 Agent "正在思考..." / "正在执行工具..."

### 8.2 SSE（Server-Sent Events）协议

流式响应用的是 SSE 协议，HTTP 长连接，服务器持续推送数据：

```
HTTP/1.1 200 OK
Content-Type: text/event-stream

data: {"choices":[{"delta":{"role":"assistant"},"index":0}]}

data: {"choices":[{"delta":{"content":"北京"},"index":0}]}

data: {"choices":[{"delta":{"content":"现在"},"index":0}]}

data: {"choices":[{"delta":{"content":"是晴天"},"index":0}]}

data: {"choices":[{"delta":{},"finish_reason":"stop"}]}

data: [DONE]
```

关键概念：
- 每行以 `data: ` 开头
- 用 `delta` 而不是 `message` — 表示增量数据
- `[DONE]` 标记流结束

### 8.3 流式 Tool Call 的增量拼接

工具调用的流式更复杂——参数是分块传输的，需要拼接：

```
data: {"choices":[{"delta":{"tool_calls":[{"index":0,"id":"call_abc","function":{"name":"get_weather","arguments":""}}]}}]}
data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"{\"ci"}}]}}]}
data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"ty\":"}}]}}]}
data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":" \"北京"}}]}}]}
data: {"choices":[{"delta":{"tool_calls":[{"index":0,"function":{"arguments":"\"}"}}]}}]}
```

**Framework 必须**：
1. 按 `index` 跟踪每个 tool_call 的增量
2. 拼接 `arguments` 字符串直到流结束
3. 流结束后才能解析完整的 JSON 参数

### 8.4 C# 中的实现：IAsyncEnumerable

```csharp
// 消费流式响应
await foreach (var chunk in llm.ChatStreamAsync(messages, options, ct))
{
    Console.Write(chunk);  // 实时输出每个文本片段
}
```

**为什么用 IAsyncEnumerable 而不是回调/事件**：
- C# 原生语法，`await foreach` 简洁直观
- 自带背压（backpressure）：消费者处理不过来时自动暂停生产者
- 支持 `CancellationToken`：用户取消时立即停止
- 可组合：用 LINQ 过滤、转换流数据

---

## 9. Structured Output：让 LLM 返回 JSON

### 9.1 为什么需要

Agent 需要解析 LLM 的输出。自由文本难以可靠解析，JSON 可以直接反序列化。

### 9.2 两种方式

**方式一：response_format（推荐）**

```json
{
  "response_format": { "type": "json_object" }
}
```

LLM 保证输出合法 JSON。但你还需要在 system prompt 中说明 JSON 结构。

**方式二：JSON Schema 模式（更严格）**

```json
{
  "response_format": { 
    "type": "json_schema",
    "json_schema": {
      "name": "weather_response",
      "schema": { "type": "object", "properties": {...} }
    }
  }
}
```

LLM 保证输出符合指定的 JSON Schema。更可靠，但不是所有模型都支持。

### 9.3 对 Framework 的影响

```csharp
public record ResponseFormat
{
    public ResponseFormatType Type { get; init; }  // Text, JsonObject, JsonSchema
    public string? JsonSchema { get; init; }        // 可选的 Schema
}
```

---

## 10. Token 计费与成本控制

### 10.1 计费模型

| 计费项 | 说明 |
|--------|------|
| Input tokens（输入） | 你发给 LLM 的所有 token（system + messages + tools） |
| Output tokens（输出） | LLM 生成的 token |

输出通常比输入贵 2-4 倍（因为生成比理解更耗算力）。

### 10.2 主流模型价格（2025/2026 参考）

| 模型 | 输入价格 | 输出价格 |
|------|---------|---------|
| GPT-4o | $2.50 / 1M token | $10.00 / 1M token |
| GPT-4o-mini | $0.15 / 1M token | $0.60 / 1M token |
| Claude 3.5 Sonnet | $3.00 / 1M token | $15.00 / 1M token |
| DeepSeek V3 | $0.27 / 1M token | $1.10 / 1M token |
| Ollama (本地) | 免费 | 免费 |

### 10.3 Agent 的成本放大效应

一个 10 步的 Agent 运行，假设平均每步 500 input + 200 output token：

```
步骤 1: 500 in + 200 out
步骤 2: 700 in + 200 out   （累积历史）
步骤 3: 900 in + 200 out
...
步骤 10: 2300 in + 200 out

总输入: 500+700+900+...+2300 = 14,000 token
总输出: 200 × 10 = 2,000 token

GPT-4o 成本: (14000 × $2.5 + 2000 × $10) / 1M = $0.055
```

单次 $0.055 看起来不多，但如果每天 10,000 次调用 → **$550/天**。

### 10.4 Framework 需要做什么

1. **Token 追踪**：每次 LLM 调用记录 usage
2. **成本计算**：`AgentResponse.TotalCost`
3. **成本上限**：`AgentOptions.MaxCostPerRun`，超过就终止
4. **日志记录**：输出每步的 token 消耗，方便优化

---

## 11. Rate Limiting

### 11.1 API 限流类型

| 限流维度 | 说明 | 示例 |
|---------|------|------|
| RPM（Requests Per Minute） | 每分钟请求数 | GPT-4o: 500 RPM |
| TPM（Tokens Per Minute） | 每分钟 Token 数 | GPT-4o: 30,000 TPM |
| RPD（Requests Per Day） | 每天请求数 | 免费 tier: 200 RPD |

### 11.2 被限流时的 HTTP 响应

```
HTTP/1.1 429 Too Many Requests
Retry-After: 20

{
  "error": {
    "message": "Rate limit reached...",
    "type": "rate_limit_error"
  }
}
```

### 11.3 Framework 需要做什么

1. **自动重试**：收到 429 时，按 `Retry-After` 头等待后重试
2. **指数退避**：重试间隔递增（1s → 2s → 4s → 8s）
3. **使用 IHttpClientFactory**：连接池管理，避免 Socket 泄漏
4. **Polly 集成**：.NET 生态标准的弹性库

---

## 12. 对 Agent Framework 设计的影响

以上所有 LLM 技术知识，最终影响了 Framework 的核心设计：

| LLM 特性 | Framework 设计决策 |
|---------|-------------------|
| Chat Completion API 结构 | `ChatMessage`, `ChatCompletionOptions`, `ChatCompletionResponse` |
| 消息角色 | `ChatMessage.User()`, `.Assistant()`, `.System()`, `.Tool()` |
| Token 限制 | `IConversationMemory`（管理历史消息，截断策略） |
| Function Calling | `ITool`, `IToolRegistry`, `ToolDefinition`, `ToolCall` |
| Streaming (SSE) | `IAsyncEnumerable<string>`, `StreamingChatEvent` |
| 多提供商 | `ILLMProvider` 接口（统一 OpenAI/Azure/Ollama） |
| 计费 | `TokenUsage`, `AgentResponse.TotalCost`, `MaxCostPerRun` |
| Rate Limiting | `IHttpClientFactory` + Polly 重试策略 |
| Temperature | `ChatCompletionOptions.Temperature` |
| Structured Output | `ResponseFormat`（Text/JsonObject/JsonSchema） |
| 无状态 | 每次请求带完整历史 → Framework 管理消息列表 |

---

## 总结

**LLM 对 Agent 开发者来说就是一个 HTTP API**：
1. 你发送一个消息列表 + 工具定义
2. LLM 返回文本回复，或者工具调用请求
3. 如果是工具调用，你执行工具并把结果反馈
4. 重复 1-3 直到 LLM 给出最终答案

**Framework 的核心工作**就是自动化这个循环，并处理所有边界情况（Token 限制、成本控制、重试、流式、多工具并行、历史管理）。

---

## 下一步

→ [Phase 0: Agent Framework 全景概览](00-OVERVIEW.md)
