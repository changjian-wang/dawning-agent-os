---
title: "小模型与边缘 Agent：Phi-4、Gemini Nano、Apple Foundation、Qwen3-Small、设备端推理"
type: concept
tags: [small-models, edge, on-device, phi, gemini-nano, apple-foundation, qwen, mlc, core-ml, llama-cpp, mlx]
sources: [comparisons/local-llm-comparison.zh-CN.md, concepts/multimodal-agents.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# 小模型与边缘 Agent：Phi-4、Gemini Nano、Apple Foundation、Qwen3-Small、设备端推理

> 不是所有 Agent 都在云端。
> - 隐私敏感场景（医疗 / 金融 / 个人助手）数据不出端
> - 离线场景（飞机 / 厂区 / 矿井）无网络
> - 实时场景（毫秒响应、AR、机器人）网络延迟不可接受
> - 成本场景（高 QPS / 长会话）云成本爆炸
>
> 2025-2026 是端侧 Agent 元年：Apple Intelligence / Gemini Nano / Phi-4 / Qwen3-Small / DeepSeek-R1-Distill 都在落地。
> 本文梳理小模型生态、设备端推理引擎、边缘 Agent 架构、Dawning 的 hybrid 策略。

---

## 1. 为什么需要端侧 Agent

### 1.1 四大驱动力

```
1. 隐私
   - 个人对话、健康数据、商业秘密
   - 端侧推理 = 数据不出门
   
2. 延迟
   - 50ms 网络 + 200ms LLM = 不可接受
   - 端侧 < 10ms 响应
   
3. 成本
   - 高频低利润场景
   - 千万次 / 月的简单意图分类
   
4. 可用性
   - 离线场景
   - 弱网 / 不稳定
```

### 1.2 反例：什么不该端侧

- 复杂推理（端侧模型还不够强）
- 多模态长视频
- 全球知识检索（需 Web）
- 企业全栈系统操作（需后端）

### 1.3 黄金混合

```
端侧小模型 = 快速响应、隐私、过滤
   ↕
云端大模型 = 复杂推理、最新知识、高质量

Hybrid Agent = 两者协同
```

---

## 2. 主流小模型矩阵（2026）

### 2.1 通用小模型（< 10B）

| 模型 | 大小 | 出品 | 特点 |
|------|------|------|------|
| **Phi-4** | 14B | Microsoft | 推理强 |
| **Phi-4-mini** | 3.8B | Microsoft | 端侧友好 |
| **Phi-4-multimodal** | 5.6B | Microsoft | 视觉 + 语音 |
| **Llama 3.3 8B** | 8B | Meta | 通用 |
| **Llama 3.2 3B / 1B** | 1-3B | Meta | 端侧 |
| **Qwen3-7B / 4B / 1.5B** | 1.5-7B | 阿里 | 中文最强 |
| **Qwen3-Embedding** | 0.5B+ | 阿里 | Embedding 端侧 |
| **Gemma 3** | 1B / 4B / 9B | Google | 多模态 |
| **Mistral Small 3** | 22B | Mistral | 多 |
| **Ministral 3B / 8B** | 3-8B | Mistral | 端侧 |
| **DeepSeek-V3-Distill** | 1.5-32B | DeepSeek | R1 蒸馏 |
| **DeepSeek-R1-Distill-Qwen-7B** | 7B | DeepSeek | 推理蒸馏 |
| **SmolLM3** | 3B | Hugging Face | 完全开放 |
| **MiniCPM-3** | 4B | Modelbest | 中国 |
| **InternLM3-8B** | 8B | 上海 AI Lab | |

### 2.2 设备端专属（厂商独占）

| 模型 | 出品 | 部署 | 备注 |
|------|------|------|------|
| **Apple Foundation Models** | Apple | iOS 18+ / macOS 15+ | ~3B 端侧 + 大型云端 |
| **Gemini Nano** | Google | Pixel / Chrome / Android | 1.8B / 3.25B |
| **Microsoft Phi Silica** | Microsoft | Copilot+ PC | NPU 优化 |
| **Samsung Gauss** | Samsung | Galaxy AI | |

### 2.3 多模态小模型

| 模型 | 大小 | 输入 |
|------|------|------|
| Phi-4-multimodal | 5.6B | T+V+A |
| Gemma 3 4B/9B | 4-9B | T+V |
| Qwen2.5-VL-3B / 7B | 3-7B | T+V |
| MiniCPM-V 2.6 | 8B | T+V |
| LLaVA-OneVision | 0.5-7B | T+V |
| Moondream 2 | 1.9B | T+V (轻量级) |
| Florence-2 | 0.23B / 0.77B | 视觉 |

### 2.4 ASR 小模型

| 模型 | 大小 | 端侧 |
|------|------|------|
| Whisper-tiny / base / small | 39M-244M | ✅ |
| Distil-Whisper | 166M-756M | ✅ |
| Moonshine | 27M / 61M | ✅ 极致小 |
| FunASR-streaming | 数百 M | ✅ |

### 2.5 TTS 小模型

| 模型 | 大小 | 端侧 |
|------|------|------|
| Kokoro-TTS | 82M | ✅ |
| Piper | 几十 M | ✅ |
| Coqui XTTS | 几百 M | ✅ |
| Orca | 数百 M | ✅ |

---

## 3. Apple Foundation Models 深度剖析

### 3.1 架构

```
Apple Intelligence
  ├── On-Device Model (~3B)
  │     ├── 苹果定制 transformer
  │     ├── LoRA 适配器（每场景一个）
  │     └── KV cache 共享
  ├── Private Cloud Compute (PCC)
  │     ├── 大模型云端
  │     ├── 加密 + 不留痕
  │     └── 端 → PCC 透明
  └── 第三方（ChatGPT 接入）
```

### 3.2 关键创新

- **统一架构**：端侧 + PCC 同模型家族
- **LoRA 适配器**：根据场景动态加载（写作 / 总结 / Mail）
- **Private Cloud Compute**：行业首创"零信任 AI 云"
- **8-bit 端侧推理**：iPhone 15 Pro 起跑

### 3.3 开发者 API（2025）

```swift
import FoundationModels

let session = LanguageModelSession(...)
let response = try await session.respond(to: "总结这段")
```

- **声明式 prompt**
- **tool calling 支持**
- **结构化输出**（@Generable macro）
- **Streaming**

### 3.4 局限

- Apple 生态独占
- 模型不可定制（不能 fine-tune）
- 业务 LLM 选型受限

---

## 4. Gemini Nano 深度剖析

### 4.1 部署

- Pixel 9+
- Chrome AI（Built-in API, Origin Trial）
- Android AICore
- ChromeOS

### 4.2 大小

- Nano-1: 1.8B
- Nano-2: 3.25B

### 4.3 Chrome AI API（开发者）

```javascript
const session = await ai.languageModel.create();
const response = await session.prompt("写诗");

// Streaming
const stream = session.promptStreaming("...");
```

**Chrome 内置**：
- Translator API
- Summarizer API
- Writer / Rewriter API
- Prompt API

### 4.4 价值

- **零安装** Web AI
- **离线工作**
- **隐私**

### 4.5 局限

- 仅 Chrome / Pixel
- 模型小（不擅复杂任务）
- 早期阶段（API 变动）

---

## 5. Microsoft Phi 系列 + Copilot+ PC

### 5.1 Phi-4 全家

| 版本 | 大小 | 模态 | 用途 |
|------|------|------|------|
| Phi-4 | 14B | Text | 通用强推理 |
| Phi-4-mini | 3.8B | Text | 端侧 |
| Phi-4-multimodal | 5.6B | T+V+A | 多模态端侧 |
| Phi Silica | 3.3B | Text | Windows NPU |

### 5.2 Copilot+ PC

- 必须 NPU > 40 TOPS（Snapdragon X / Intel Core Ultra 2 / AMD Ryzen AI 300）
- Phi Silica 内置
- Recall（截屏索引 + 本地 LLM 检索）
- Click to Do

### 5.3 开发者 API

- Windows AI Studio / Foundry Local
- DirectML 推理
- ONNX Runtime + ML.NET

### 5.4 .NET 集成

- Microsoft.Extensions.AI 抽象
- Foundry Local 服务

---

## 6. Qwen3-Small / 中国端侧生态

### 6.1 Qwen3 系列

- Qwen3-0.5B / 1.5B / 4B / 7B / 32B / 72B
- 0.5-7B 端侧适配
- 强中文 + 多语言

### 6.2 Qwen3-Embedding

专门为 RAG 端侧设计：
- 0.5B / 1.5B / 8B
- 多语言
- Matryoshka 支持

### 6.3 中国厂商方案

| 厂商 | 端侧策略 |
|------|---------|
| 华为 | 盘古 + 鸿蒙集成 |
| OPPO | AndesGPT + 自研 |
| vivo | 蓝心 + Qwen 合作 |
| 小米 | MiLM + 自研 |
| 荣耀 | MagicLM |

### 6.4 国产 NPU

- 华为达芬奇
- 寒武纪 / 地平线
- 紫光展锐

端侧国产芯片 + 国产小模型 = 国内合规推荐路线。

---

## 7. 设备端推理引擎

### 7.1 推理引擎矩阵

| 引擎 | 平台 | 模型格式 | 量化 | 特点 |
|------|------|---------|------|------|
| **llama.cpp** | 全平台 | GGUF | Q2-Q8 | 开源主力 |
| **MLX** | Apple Silicon | safetensors / mlx | 4/8-bit | Apple 原生 |
| **MLC LLM** | iOS/Android/Web | MLC | 4-bit | TVM 编译 |
| **ONNX Runtime** | 全平台 | ONNX | INT8/INT4 | Microsoft |
| **Core ML** | Apple | mlmodel/mlpackage | 4/8-bit | iOS/macOS |
| **TensorFlow Lite** | Android/iOS/edge | TFLite | INT8 | Google |
| **NCNN** | Mobile | ncnn | 多 | 腾讯 |
| **MNN** | Mobile | MNN | 多 | 阿里 |
| **Executorch** | Mobile | PT2.0 | 多 | Meta |
| **TGI / vLLM** | Server | safetensors | 多 | 服务端 |
| **WebLLM** | Browser (WebGPU) | MLC | 4-bit | 浏览器 |
| **Transformers.js** | Browser | ONNX | INT8 | HuggingFace |
| **Ollama** | Desktop/Server | GGUF | Q4-Q8 | 易用包装 |

### 7.2 选型决策

```
平台？
  iOS/macOS → MLX / Core ML / llama.cpp
  Android   → MLC / TFLite / Executorch / MNN
  Windows   → ONNX Runtime / DirectML / Ollama
  Linux     → llama.cpp / Ollama / vLLM
  Browser   → WebLLM / Transformers.js
  跨平台    → MLC / ONNX

硬件？
  CPU only    → llama.cpp / Ollama
  Apple GPU   → MLX
  NVIDIA GPU  → vLLM / TGI / TensorRT
  AMD GPU     → llama.cpp + ROCm
  NPU         → 平台原生（Core ML / NNAPI / DirectML）
```

### 7.3 量化等级影响

| 量化 | 大小 | 速度 | 质量 |
|------|------|------|------|
| FP16 | 100% | 1x | 100% |
| INT8 | 50% | 1.5-2x | 99% |
| Q4_K_M | 30% | 2-3x | 95% |
| Q4_K_S | 25% | 2-3x | 92% |
| Q3_K_M | 20% | 3x | 88% |
| Q2_K | 15% | 3.5x | 80% |

**经验**：4-bit 是端侧甜区。

---

## 8. 端侧硬件能力（2026）

### 8.1 手机芯片

| 芯片 | NPU TOPS | 推理能力 |
|------|---------|---------|
| Apple A18 Pro | 38 | 3B 流畅 |
| Snapdragon 8 Gen 4 | 45 | 3-7B 流畅 |
| Tensor G4 | 数十 | Nano + 3B |
| Dimensity 9400 | 数十 | 3-7B |

### 8.2 PC 芯片

| 芯片 | NPU TOPS | 推理能力 |
|------|---------|---------|
| Apple M4 / M4 Pro / Max | 38-40 | 7-70B (Max) |
| Snapdragon X Elite | 45 | 3-13B |
| Intel Core Ultra 200V | 48 | 3-13B |
| AMD Ryzen AI 300 | 50 | 3-13B |
| NVIDIA RTX (笔记本) | 数百+ | 7-70B |

### 8.3 边缘嵌入式

- NVIDIA Jetson Orin / Thor
- Google Coral
- Hailo-10
- Qualcomm RB5 / RB6

### 8.4 容量瓶颈

- 4GB RAM → 1-3B Q4
- 8GB RAM → 7B Q4
- 16GB RAM → 13B Q4 / 7B Q8
- 32GB+ → 30B+ Q4
- 64GB+ → 70B Q4

---

## 9. 边缘 Agent 架构模式

### 9.1 Pure On-Device

```
[Device]
  Agent Loop ↔ Local LLM
              ↔ Local Tools
              ↔ Local Memory
```

适合：完全离线、强隐私。

### 9.2 Cloud-First Hybrid

```
[Device]
  UI + 简单意图分类（小模型）
       ↓
[Cloud]
  完整 Agent + 大模型 + 工具
```

适合：UI 体验 + 云能力。

### 9.3 Edge-First Hybrid（推荐）

```
[Device]
  Edge Agent
    ├── 意图分类 (Local)
    ├── 简单回复 (Local)
    ├── 隐私脱敏 (Local)
    └── 升级到云 (按需)
       ↓
[Cloud]
  Heavy Agent (Reasoning, Long context)
```

### 9.4 Federated（多端 + 云）

```
[Device A] ↔ [Device B] ↔ [Cloud]
(P2P 协作 + 云协调)
```

研究阶段，未广泛落地。

### 9.5 Mesh (Local Network)

```
[家用 NAS / 边缘 Server]
  Mid-size LLM (e.g., 14B-32B)
     ↑ Local Network
  [Phone] [TV] [Speaker] [Watch]
```

家庭 / SOHO 私有 AI。

---

## 10. 模型路由（Hybrid Routing）

### 10.1 路由策略

```python
def route(query: str) -> ModelChoice:
    # 1. Privacy classification
    if contains_pii(query):
        return Local
    
    # 2. Complexity estimate
    if estimated_tokens(query) > 4000 or needs_reasoning(query):
        return Cloud
    
    # 3. Latency requirement
    if latency_critical:
        return Local
    
    # 4. Cost budget
    if user.over_budget:
        return Local
    
    # Default
    return Cloud
```

### 10.2 路由模型

可以用极小模型做 router（< 100M）：
- Distil-RouterBERT
- 轻量分类器
- 关键词规则 + ML

### 10.3 渐进升级

```
Local Try → 自评信心 →
  High → 用 Local 答案
  Low  → 升级 Cloud
```

实现：让 Local 模型输出 confidence + reasoning。

---

## 11. 端侧 RAG

### 11.1 端侧向量库

| 选项 | 端侧友好 | 备注 |
|------|---------|------|
| **SQLite + sqlite-vec** | ✅ | 极轻 |
| **DuckDB + VSS** | ✅ | 分析强 |
| **LanceDB** | ✅ | 移动端可 |
| **ChromaDB** | ⚠️ | 偏服务端 |
| **Qdrant embedded** | ⚠️ | 较重 |

### 11.2 端侧 Embedding

- Qwen3-Embedding-0.5B
- BGE-Small (33M)
- jina-embeddings-v2-small
- gte-tiny / gte-small
- 端侧用 Q4 量化

### 11.3 离线知识包

```
App ships with:
  - SQLite + 预算文档 embeddings
  - Local LLM
  - Static knowledge cards

User queries → Local RAG → Local Answer
```

适合：旅游 App / 字典 / 工具书 / 离线手册。

---

## 12. 端侧工具调用

### 12.1 端侧工具

- 系统能力（联系人 / 日历 / 文件 / 截图）
- 应用内功能（Safari / Mail / Notes）
- 本地数据库
- Bluetooth 设备
- 摄像头 / 麦克风

### 12.2 苹果 App Intents

iOS / macOS 提供"App Intents" 框架：
- App 注册 intents
- Apple Intelligence 路由
- 跨 App 协作

### 12.3 Android App Actions

类似框架，与 Gemini Nano 整合。

### 12.4 通用 MCP On-Device

- 端侧也跑 MCP server
- 跨语言 / 跨 App 工具协议
- 待生态成熟

---

## 13. 性能优化技巧

### 13.1 KV cache 共享

多场景共享 base model + 加载不同 LoRA。

### 13.2 Speculative Decoding

小模型 draft + 大模型 verify → 端侧推理快 2-3x。

### 13.3 Batching

多请求批处理（端侧不太常用，单用户场景）。

### 13.4 Continuous batching

vLLM / SGLang 风（服务端）。

### 13.5 Prefill / Decode 拆分

Prefill 慢但能并行；Decode 顺序但快。
端侧优化 Prefill 用 NPU，Decode 用 CPU。

### 13.6 Streaming

首 token 时间（TTFT）优化最重要。

---

## 14. 端侧 Agent 安全

### 14.1 数据隔离

- 每 App / 用户独立 LLM context
- 端侧 LLM 不能"跨 App 串谋"

### 14.2 输出过滤

- 端侧也要 content safety
- 可用小型 classifier (Llama Guard / Granite Guardian 小版本)

### 14.3 Prompt Injection

- 端侧场景输入大多结构化
- 仍要防 user content 注入
- 系统 prompt 强约束

### 14.4 Apple PCC 模式

- 数据加密发送到云
- 云端可证明不留痕
- 第三方审计

是行业值得借鉴的"零信任 AI 云"模式。

---

## 15. 端侧 Agent 评估

### 15.1 关键指标

- **TTFT**（首 token）<= 500ms
- **TPS**（每秒 token）>= 20
- **能耗**（每次调用 mAh）
- **温度**（机身热）
- **内存峰值**
- **存储**（模型大小）
- **质量**（端侧 vs 云对比）

### 15.2 端侧专用基准

- MobileBench
- EdgeBench
- LMSys Mobile Arena（出现中）

### 15.3 用户感知

- 启动响应时间
- 离线可用性
- 续航影响

---

## 16. Dawning 端侧策略

### 16.1 Layer 0 适配

```csharp
public interface ILLMProvider { ... }

// 端侧实现
Dawning.LLM.Ollama         (跨平台本地)
Dawning.LLM.OnnxRuntime    (Windows / Linux)
Dawning.LLM.CoreML         (iOS / macOS)
Dawning.LLM.MLC            (跨平台编译)
Dawning.LLM.Gemini.Nano    (Chrome AI)
Dawning.LLM.AppleFoundation (iOS 18+)
```

### 16.2 Hybrid Router

```csharp
public class HybridProvider : ILLMProvider
{
    private readonly ILLMProvider _local;
    private readonly ILLMProvider _cloud;
    private readonly IRouter _router;
    
    public async Task<Response> ChatAsync(...)
    {
        var choice = _router.Route(...);
        return choice == Local 
            ? await _local.ChatAsync(...)
            : await _cloud.ChatAsync(...);
    }
}
```

### 16.3 端侧 Memory

- IWorkingMemory：内存
- ILongTermMemory：SQLite + sqlite-vec
- 增量同步到云（可选）

### 16.4 端侧 MCP

- 端侧也起 MCP Server（如 file access / system tools）
- Agent 可调本地能力

### 16.5 .NET MAUI 集成

- Dawning.Maui Package（规划）
- iOS / Android / Win / Mac 一套代码
- 端侧 LLM Provider 自动选择

### 16.6 Layer 7 端侧治理

- 本地 PII 脱敏（不上云）
- 本地 Policy（家长锁 / 企业策略）
- 离线审计 log（同步上云）

---

## 17. 实战场景

### 17.1 个人助手 (iOS)

```
用户："帮我整理今天的会议笔记，发给同事"
  ↓
Apple Foundation Model on-device:
  - 读取 Notes（系统 API）
  - 摘要（端侧）
  - PII 检测（端侧）
  ↓
不敏感 → 发送 Mail
敏感 → HITL 确认
```

### 17.2 工厂边缘 Agent

```
车间边缘服务器（Jetson Orin）：
  - 7B 本地模型
  - RAG (设备手册)
  - 工人语音询问 → ASR (Whisper-small)
  - 推理 → TTS (Piper)
  - 完全离线
```

### 17.3 浏览器 AI（Chrome）

```
用户在网页选中一段文字
  → Chrome Built-in AI Summarizer
  → Gemini Nano 端侧总结
  → 显示
  → 全程不发服务器
```

### 17.4 智能手表

```
Watch:
  超小模型 (200M Distil-Whisper)
  → 监听唤醒词
  → 转 Phone (Bluetooth)
  → Phone Foundation Model (3B)
  → 必要时升 Cloud
```

### 17.5 汽车智能座舱

```
车机：
  4-13B 本地模型 (8GB+ RAM)
  - 语音控制
  - 导航推理
  - 离线 RAG (车主手册)
  - 同步个人偏好
```

---

## 18. 典型反模式

| 反模式 | 教训 |
|-------|------|
| "端侧也要旗舰 70B" | 内存爆 / 烫手 / 卡顿 |
| "全部上端侧" | 复杂任务质量差 |
| "全部云端" | 隐私 / 离线场景死 |
| "不量化" | 小模型也跑不动 |
| "无 router" | Hybrid 无策略 |
| "端侧 ≠ 端侧"（混淆 NPU/CPU/GPU） | 实际比预期慢 |
| "忽视能耗" | 用户骂耗电 |
| "端侧无观测" | 出问题不知道 |

---

## 19. 趋势

### 19.1 2026-2027 可期

- **3-7B 端侧成为主流**
- **多模态端侧成熟**（Phi-4-multimodal / Gemma 3）
- **NPU 普及**（手机 / PC 标配）
- **WebGPU 普及** → 浏览器 LLM 实用
- **端云协同标准**（类 Apple PCC 模式扩散）
- **专用端侧 Agent OS**（Dawning Mobile / Apple Intelligence / Gemini）

### 19.2 不会的

- 端侧完全替代云（推理模型 / 复杂任务仍需云）
- 一刀切（场景多元化）

---

## 20. 小结

> 端侧 Agent 不是"云的折扣版"，是**新的设计范式**：
> - 隐私是产品特性，不是合规义务
> - 延迟是 UX，不是后端指标
> - 离线是能力，不是异常
>
> Hybrid（端 + 云）才是常态。
> Dawning 的策略：**Layer 0 抽象多 Provider + Hybrid Router + 端侧 Memory + MCP On-Device + Layer 7 端侧治理**——
> 让一套 Agent 代码同时跑在云、PC、手机、IoT。

---

## 21. 延伸阅读

- [[comparisons/local-llm-comparison.zh-CN]] — 本地推理引擎
- [[comparisons/polyglot-agent-ecosystem.zh-CN]] — 跨语言生态
- [[concepts/multimodal-agents.zh-CN]] — 端侧多模态
- [[concepts/cost-optimization.zh-CN]] — 端云成本权衡
- [[concepts/agent-security.zh-CN]] — 端侧安全
- Apple Foundation Models: <https://developer.apple.com/apple-intelligence/>
- Gemini Nano (Chrome AI): <https://developer.chrome.com/docs/ai/built-in>
- Phi: <https://huggingface.co/microsoft>
- MLX: <https://github.com/ml-explore/mlx>
- MLC LLM: <https://llm.mlc.ai/>
- llama.cpp: <https://github.com/ggerganov/llama.cpp>
- ONNX Runtime: <https://onnxruntime.ai/>
- WebLLM: <https://github.com/mlc-ai/web-llm>
- Executorch: <https://pytorch.org/executorch/>
