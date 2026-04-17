---
title: "Structured Output 深度：JSON Schema、Constrained Decoding 与 Outlines/Instructor"
type: concept
tags: [structured-output, json-schema, constrained-decoding, outlines, instructor, xgrammar]
sources: [comparisons/function-calling-comparison.zh-CN.md, comparisons/local-llm-comparison.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# Structured Output 深度：JSON Schema、Constrained Decoding 与 Outlines/Instructor

> LLM 生成自由文本容易，生成**严格合法的结构化数据**难。
> Agent 的工具调用、数据抽取、工作流节点路由都依赖结构化输出——一次格式错误就断流。
>
> 本文梳理结构化输出的三代技术（Prompt、Function Calling、Constrained Decoding），对比 Outlines / Instructor / XGrammar，以及 Dawning 的统一策略。

---

## 1. 为什么结构化输出难

### 1.1 问题本质

LLM 本质是**概率采样**：每步选下一个 token。没有内建机制保证"输出符合 JSON Schema"。

### 1.2 常见失败模式

| 失败 | 示例 |
|------|------|
| 尾部多余文本 | `{"name":"Alice"} Hope this helps!` |
| Markdown 包裹 | `\`\`\`json\n{...}\n\`\`\`` |
| 字段缺失 | Schema 要求 `age`，输出没有 |
| 类型错误 | `age: "25"` 而非 `age: 25` |
| 枚举越界 | `status: "maybe"` 而 Schema 只允许 `active\|inactive` |
| 深度嵌套错 | 数组套对象时某处漏 `{` |
| 中途截断 | max_tokens 打断 JSON |
| 引号转义错 | 字符串含 `"` 未转义 |

### 1.3 成本

一次失败 =
- 重试一次（token × 2）
- 用户等待延迟（2x）
- 逻辑防御代码（大量 try/parse/retry）
- 隐蔽错误（类型对但值不对）

**目标**：100% 合法 + 最小开销。

---

## 2. 三代技术

```
Gen 1: Prompt 工程               Gen 2: Function Calling        Gen 3: Constrained Decoding
─────────────────────            ───────────────────────        ──────────────────────────
"请输出 JSON..."                 tools=[{schema...}]            推理时限制 token 选择
+ 示例                            Provider 内部约束              100% 合法 + 结构化
+ 校验重试                       高合法率但非 100%
                                                               Outlines / XGrammar / vLLM
成功率 85-95%                    成功率 97-99%                   成功率 100%
```

---

## 3. Gen 1：Prompt Engineering

### 3.1 基础做法

```
System: "Output ONLY valid JSON matching this schema: {...}. 
No explanations, no markdown."

User: "..."
```

### 3.2 加强技术

- Few-shot 示例
- "Think in your head, output only JSON"
- JSON mode（OpenAI `response_format: {"type": "json_object"}`）
- 温度降低（temperature=0）

### 3.3 问题

- 模型越小失败越多
- 长 schema 失败率上升
- 无法保证**100%** 合法
- 代码里永远需要兜底

### 3.4 何时仍用

- 快速原型
- 简单 schema
- 模型不支持更高级特性

---

## 4. Gen 2：Function Calling / Tools

### 4.1 机制

Provider 通过微调让模型学会输出 Tool Call：

```json
{
  "tool_calls": [{
    "function": {
      "name": "extract_user",
      "arguments": "{\"name\":\"Alice\",\"age\":30}"
    }
  }]
}
```

Schema 在 Request 里：

```json
{
  "tools": [{
    "type": "function",
    "function": {
      "name": "extract_user",
      "parameters": {
        "type": "object",
        "properties": {
          "name": {"type":"string"},
          "age": {"type":"integer"}
        },
        "required": ["name","age"]
      }
    }
  }]
}
```

### 4.2 OpenAI Structured Outputs

2024 年推出，**真·100% 保证**：

```json
{
  "response_format": {
    "type": "json_schema",
    "json_schema": {
      "name": "user",
      "schema": {...},
      "strict": true
    }
  }
}
```

OpenAI 服务端做了 Constrained Decoding（见下）。

### 4.3 Anthropic Tool Use

Claude 的 tool_use 保证 JSON 合法，但**不保证 Schema 严格**（如必填字段可能缺）。需要应用层校验。

### 4.4 Gemini

类似 OpenAI，但 schema 支持子集有限。

### 4.5 跨供应商差异

| Provider | JSON Mode | Schema 严格 | 备注 |
|----------|-----------|------------|------|
| OpenAI | ✅ | ✅ (Structured Outputs) | 最强 |
| Anthropic | ✅ via tool | ⚠️ | 需自己校验 |
| Gemini | ✅ | ✅ (部分) | schema 支持有限 |
| Mistral | ✅ | ⚠️ | — |
| 开源模型 | 靠推理引擎 | 靠推理引擎 | vLLM/SGLang 支持 |

详见 [[comparisons/function-calling-comparison.zh-CN]]。

---

## 5. Gen 3：Constrained Decoding

### 5.1 核心思想

**在推理时，每步只允许"能让输出仍然合法"的 token**。

```
生成中: '{"name": "'
下一步可选:  字符 a-z, A-Z, 0-9, ...
不允许:     { } [ ] （会破坏 JSON 结构）

生成中: '{"name": "Alice", "a'
下一步只能继续 "age" 这个字段（schema 要求）
```

### 5.2 实现机制

- **FSM / 正则**：简单格式用有限状态机
- **LL/LR 语法**：上下文无关文法
- **JSON Schema → Grammar 编译**：动态生成约束
- **Logit Mask**：把非法 token 的 logit 设为 -∞

### 5.3 核心引擎

| 引擎 | 说明 |
|------|------|
| **Outlines** | Python，最早开源，基于 FSM |
| **XGrammar** | 华人团队，CMU/MLC，极快，vLLM 集成 |
| **LMFE** (llama-cpp) | llama.cpp 集成 |
| **Guidance** | Microsoft，统一 Prompt + 约束 |
| **Instructor** | Pydantic 友好包装，基于 FC |
| **JSONformer** | 较早，仅支持 JSON |

### 5.4 性能

早期 Constrained Decoding 慢（FSM 编译开销）。
2024-2025 **XGrammar** 实现接近零开销：
- 编译一次，复用
- GPU 侧 Logit Mask
- 实测只慢 3-5%

---

## 6. Outlines 详解

### 6.1 定位

开源结构化生成，模型无关（HF / vLLM / Ollama / llama.cpp）。

### 6.2 用法

```python
import outlines
from outlines import models, generate

model = models.transformers("meta-llama/Llama-3.3-8B")

# JSON Schema
generator = generate.json(model, schema)
result = generator("Extract user: Alice, 30")

# Regex
generator = generate.regex(model, r"\d{3}-\d{4}")

# Choice
generator = generate.choice(model, ["positive", "negative"])

# Pydantic
class User(BaseModel):
    name: str
    age: int

generator = generate.json(model, User)
```

### 6.3 与 vLLM 集成

```bash
vllm serve ... --guided-decoding-backend outlines
```

```python
response = client.chat.completions.create(
    model="llama3.3",
    messages=[...],
    extra_body={"guided_json": user_schema}
)
```

---

## 7. Instructor 详解

### 7.1 定位

**Pydantic 第一公民**。不做 constrained decoding，而是包装 Function Calling + 自动重试。

### 7.2 用法

```python
import instructor
from pydantic import BaseModel

client = instructor.from_openai(OpenAI())

class User(BaseModel):
    name: str
    age: int

user = client.chat.completions.create(
    model="gpt-4o",
    response_model=User,
    messages=[...],
    max_retries=3  # 失败自动重试
)
# user 是 User 实例，已校验
```

### 7.3 特色

- 与 Pydantic 深度集成
- Stream 部分结果（`create_partial`）
- 自动重试 + 错误反馈给模型
- 多供应商支持
- 零依赖于特定推理引擎

### 7.4 vs Outlines

| 维度 | Instructor | Outlines |
|------|-----------|----------|
| 机制 | FC + 重试 | Constrained Decoding |
| 保证 | 概率保证 | 100% 保证 |
| 速度 | 依赖模型 | 编译后快 |
| 易用性 | Pydantic | 多接口 |
| 适用 | 闭源模型 API | 开源模型推理 |

---

## 8. XGrammar / SGLang

### 8.1 XGrammar

- 2024 CMU / MLC 出品
- CFG 文法约束
- 预编译 FSM + GPU Logit Mask
- 比 Outlines 快 100x（在复杂 schema 下）
- vLLM / SGLang 集成

### 8.2 SGLang 内建

```python
response = sgl.gen(
    "output",
    max_tokens=200,
    regex=r'{"name":"[^"]+","age":\d+}'
)
```

---

## 9. JSON Schema 最佳实践

### 9.1 保持简单

```json
// 差：深度嵌套 + 多态
{
  "oneOf": [
    {"type":"object","properties":{...}},
    {"type":"object","properties":{...}}
  ]
}

// 好：扁平 + 枚举
{
  "type": "object",
  "properties": {
    "kind": {"enum":["A","B"]},
    "data": {...}
  }
}
```

### 9.2 字段命名

- snake_case（与大多数模型训练一致）
- 短但有语义
- 避免缩写

### 9.3 描述为王

```json
{
  "type": "string",
  "description": "ISO 8601 datetime, UTC, e.g., 2026-04-17T10:00:00Z"
}
```

**模型用 description 做推理**——描述好坏决定成败。

### 9.4 必填 vs 可选

- 保持 `required` 明确
- 可选字段少用（模型不确定时倾向乱填）
- OpenAI Structured Outputs 要求所有字段都 required（nullable 表达可选）

### 9.5 枚举

```json
{"enum": ["active","inactive","pending"]}
```

模型输出不在枚举内时 Constrained Decoding 直接拦截。

---

## 10. Streaming Structured Output

### 10.1 场景

长 JSON 输出时用户希望看到部分结果。

### 10.2 方案

| 方案 | 说明 |
|------|------|
| **Partial JSON** | 解析不完整 JSON（容错解析器） |
| **Jsonl** | 多行 JSON，每行一个对象 |
| **Instructor create_partial** | Pydantic 部分填充 |
| **Outlines Stream** | 流式 token + 增量校验 |

### 10.3 部分 JSON 解析

```python
# partial-json-parser (Rust)
{"name": "Ali   ← 此时 name 字段尚未完成
```

前端可以渐进渲染"姓名：Ali..."。

---

## 11. 结构化输出 + Agent

### 11.1 应用场景

- **Tool Call Arguments**：必须严格 schema
- **路由决策**：`{"route": "agent_b"}`
- **提取**：从非结构化文本抽取
- **评估**：LLM-as-judge 输出 `{"score": 8, "reasoning": "..."}`
- **Plan**：`{"steps": [...]}`

### 11.2 组合模式

```
Agent Loop 里每步：
  ┌─────────────────────────────┐
  │  LLM Call (自由输出 thinking)│
  │     +                        │
  │  Structured Decision         │
  │  {"action": "tool_call",     │
  │   "tool": "search",          │
  │   "args": {...}}             │
  └─────────────────────────────┘
```

DSPy / Instructor / Outlines 都支持这种"思考 + 决定"模式。

---

## 12. 性能与质量权衡

| 维度 | Gen 1 (Prompt) | Gen 2 (FC) | Gen 3 (Constrained) |
|------|---------------|-----------|---------------------|
| 合法率 | 85-95% | 97-99%+ | 100% |
| 速度 | 快 | 快 | 慢 3-5%（XGrammar） |
| 创造性 | 高 | 中 | 受约束 |
| 模型可用 | 所有 | 支持 FC 的 | 开源自建 |
| 重试成本 | 高 | 低 | 零 |
| 复杂 schema | 差 | 中 | 好 |

---

## 13. 常见坑

| 坑 | 说明 / 解决 |
|----|-----------|
| Schema 太严反而降质量 | Constrained 让模型走窄路，整体推理变差 |
| 枚举太多 | 超过 50 项时考虑分层或字符串 |
| 数字精度 | JSON Number 不保证精度，金融用字符串 |
| Unicode 转义 | 确保 UTF-8 直通，不强制 `\uXXXX` |
| 模型 "think" 泄漏 | Structured mode 下模型仍可能输出 chain-of-thought 前置；需要显式分离 |
| Schema 漂移 | 客户端与服务端 schema 不同步 → 随版本化 |
| 中文 schema 描述 | 有效，但英文描述通常效果更好 |
| 极长 JSON | 超过 max_tokens 会截断，需要分批或大 max |

---

## 14. Dawning 统一抽象

### 14.1 IStructuredOutput

```csharp
public interface IStructuredOutput
{
    Task<T> GenerateAsync<T>(
        string prompt,
        StructuredOutputOptions? options = null,
        CancellationToken ct = default);
}

public record StructuredOutputOptions(
    StructuredMode Mode = StructuredMode.Auto,  // Prompt | FunctionCall | Constrained | Auto
    int MaxRetries = 2,
    double Temperature = 0);
```

### 14.2 Backend 适配

```csharp
services.AddStructuredOutput(o =>
{
    o.PreferConstrainedDecoding = true;  // 有条件时优先
    o.AddBackend<OutlinesBackend>();     // 本地 vLLM
    o.AddBackend<OpenAIStructuredBackend>();  // OpenAI Structured Outputs
    o.AddBackend<InstructorBackend>();   // 通用 FC + 重试
    o.FallbackChain = [Constrained, FunctionCall, Prompt];
});
```

### 14.3 与 Pydantic/POCO 对等

.NET 侧用 POCO + JsonSchema 生成：

```csharp
public record ExtractedUser(string Name, int Age);

var user = await structured.GenerateAsync<ExtractedUser>(
    "Extract user info: Alice, 30");
```

内部自动：
- 生成 JSON Schema
- 选合适 backend
- 校验 + 反序列化

---

## 15. 工程实践清单

- [ ] Schema 保持扁平 + 枚举清晰
- [ ] 每字段有 description
- [ ] Required 字段明确
- [ ] 100% 合法要求 → 用 Constrained
- [ ] 无法用 Constrained 时 → Instructor + 重试
- [ ] 长输出考虑 partial parsing
- [ ] 流式场景用 partial-json-parser
- [ ] Schema 版本化（API 演进）
- [ ] 客户端仍做 Schema validation（双保险）
- [ ] 记录 Schema 失败日志（调优依据）

---

## 16. 小结

> **结构化输出是 Agent 可靠性的根基。**
>
> Gen 1 靠 Prompt 祈祷，Gen 2 靠 Provider 微调，Gen 3 靠 Constrained Decoding 物理保证。
> OpenAI Structured Outputs 和 vLLM/Outlines/XGrammar 把 100% 合法从梦变成了默认。
>
> Dawning 的立场：**上层代码无感知，底层智能选路**——
> 云端用 Provider 原生 Structured Outputs，本地用 Constrained Decoding，都不行就用 Instructor 包装 + 重试。

---

## 17. 延伸阅读

- [[comparisons/function-calling-comparison.zh-CN]] — FC 规范差异
- [[concepts/prompt-engineering-dspy.zh-CN]] — 结构化 Prompt 设计
- [[comparisons/local-llm-comparison.zh-CN]] — vLLM/SGLang Constrained Decoding
- Outlines：<https://dottxt-ai.github.io/outlines/>
- Instructor：<https://python.useinstructor.com/>
- XGrammar：<https://github.com/mlc-ai/xgrammar>
- OpenAI Structured Outputs：<https://platform.openai.com/docs/guides/structured-outputs>
- JSON Schema：<https://json-schema.org/>
