---
title: "多模态 Agent：视觉、语音、视频与 Realtime API"
type: concept
tags: [multimodal, vision, voice, video, whisper, gpt-4o-realtime, gemini-live, clip, siglip]
sources: [concepts/agent-loop.md, concepts/structured-output.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# 多模态 Agent：视觉、语音、视频与 Realtime API

> 2025-2026 最快扩展的能力边界：Agent 从"文字进文字出"走向"看得见、听得见、说得出"。
> GPT-4o Realtime、Gemini Live、Claude 视觉、Whisper、视频理解——正在重塑交互范式。
>
> 本文梳理多模态能力现状、工程落地模式、Dawning 的适配策略。

---

## 1. 多模态的分层

```
输入（感知）         →  推理（大脑）  →  输出（表达）
┌─────────────────┐     ┌─────────┐    ┌─────────────────┐
│ Text            │     │         │    │ Text            │
│ Image           │ ──► │ LLM /   │──► │ Image (Dall-E)  │
│ Audio           │     │ VLM     │    │ Audio (TTS)     │
│ Video           │     │         │    │ Video (Sora)    │
│ Sensor/Telemetry│     │         │    │ Action (Tool)   │
└─────────────────┘     └─────────┘    └─────────────────┘
```

---

## 2. 核心模型矩阵（2026）

### 2.1 视觉-语言模型（VLM）

| 模型 | 输入 | 特点 |
|------|------|------|
| **GPT-4o** | Text + Image | 综合最强 |
| **Claude 3.5 Sonnet** | Text + Image | 文档/图表强 |
| **Gemini 2.0** | Text + Image + Video | 长视频原生 |
| **Qwen2-VL** | Text + Image + Video | 开源最强 |
| **LLaVA** | Text + Image | 学术基准 |
| **InternVL** | Text + Image | 开源强 |
| **CogVLM** | Text + Image | |
| **MiniCPM-V** | Text + Image | 小模型高效 |

### 2.2 语音

| 模型 | 能力 | 说明 |
|------|------|------|
| **Whisper / Whisper-Large-v3** | ASR | 开源标杆 |
| **GPT-4o Transcribe** | ASR | API |
| **Gemini ASR** | ASR | |
| **Deepgram Nova** | 商用 ASR | 实时强 |
| **AssemblyAI** | 商用 ASR | |
| **ElevenLabs** | TTS | 语音合成最强 |
| **OpenAI TTS** | TTS | |
| **Azure Speech** | TTS + ASR | 企业级 |
| **Kokoro-TTS** | TTS | 开源新星 |

### 2.3 Realtime 语音 Agent

| 产品 | 协议 | 延迟 |
|------|------|------|
| **OpenAI Realtime API (GPT-4o)** | WebSocket + PCM | 200-500ms |
| **Gemini Live** | WebSocket | 类似 |
| **Claude 实时（开发中）** | — | — |
| **Pipecat / Daily.co** | WebRTC 编排 | 开源 |
| **Vapi / Retell / Livekit Agents** | 语音 Agent 平台 | 企业 |

### 2.4 视频理解

| 模型 | 说明 |
|------|------|
| **Gemini 2.0** | 最长上下文视频 (1 小时+) |
| **GPT-4o** | 通过抽帧 + 音频 |
| **Qwen2-VL** | 开源最强视频理解 |
| **Video-LLaVA** | 学术 |

### 2.5 图像生成

| 模型 | 说明 |
|------|------|
| **Dall-E 3** | |
| **Midjourney v6.1** | 美感冠军 |
| **Stable Diffusion 3.5** | 开源 |
| **FLUX.1** | 开源旗舰 |
| **Imagen 3** | Google |
| **Recraft** | 矢量 / UI |

### 2.6 视频生成

| 模型 | 说明 |
|------|------|
| **Sora** | OpenAI |
| **Veo 2** | Google |
| **Runway Gen-3** | |
| **Kling** | 开源派 |
| **HunyuanVideo** | 腾讯开源 |

---

## 3. 视觉 Agent 模式

### 3.1 截图理解（Computer Use）

```
Agent 看屏幕截图 → 推理下一步操作 → 发出鼠标/键盘指令
```

**代表**：
- Anthropic Computer Use
- OpenAI Operator
- Browserbase + Playwright
- Microsoft UFO

**挑战**：
- 延迟（截图 + VLM 推理）
- 准确率（UI 定位不稳）
- 安全（Agent 操作真机）

### 3.2 文档理解

```
PDF / 扫描件 → 解析（OCR + 布局）→ VLM 理解 → 结构化输出
```

**关键技术**：
- **Layout-aware**（Unstructured, LlamaParse, Docling）
- **表格提取**（Table Transformer）
- **公式识别**（Nougat, Mathpix）

### 3.3 视觉 RAG

```
Query → 检索相关图片 → VLM 综合回答
```

例：图纸问答、产品目录、医学影像。

### 3.4 图表理解

- 条形图 / 折线图 → 精确数值提取（ChartQA）
- 流程图 → 步骤推理
- UI 截图 → 元素识别

### 3.5 CLIP / SigLIP Embedding

文本与图像共享向量空间：

```
搜"红色跑车" → 召回红色跑车图像
```

**模型**：
- CLIP (OpenAI)
- SigLIP / SigLIP 2 (Google)
- OpenCLIP
- Chinese-CLIP
- ImageBind (跨模态)

---

## 4. 语音 Agent 模式

### 4.1 传统管线（Cascade）

```
User Voice
  ↓ ASR (Whisper)
Text
  ↓ LLM
Response Text
  ↓ TTS (ElevenLabs)
Audio Response
```

**优劣**：
- ✅ 组件独立可替换
- ✅ 文字 log 易 debug
- ❌ 延迟叠加（1.5-3 秒）
- ❌ 丢失语音情感 / 语气
- ❌ 无法处理打断

### 4.2 Realtime API（Speech-to-Speech）

```
User Voice ──WebSocket PCM──► GPT-4o Realtime ──WebSocket PCM──► Speaker
```

**优劣**：
- ✅ 低延迟 (<500ms)
- ✅ 保留语气 / 情感
- ✅ 支持打断
- ❌ 可观测性弱（没有中间文字）
- ❌ 调试困难
- ❌ 成本高
- ❌ 锁 OpenAI / Google

### 4.3 混合模式（推荐生产）

```
User Voice
  ↓ WebRTC to server
ASR (实时 streaming) ──► Text
                         ↓
                   LLM (streaming)
                         ↓
                       Text ──► TTS streaming ──► User
                         │
                         ▼
                    Trace / Log / Eval
```

**工具链**：
- **Pipecat**：开源实时 Agent 编排
- **Livekit Agents**：WebRTC + Agent 框架
- **Vapi / Retell**：托管服务

### 4.4 打断处理（Interruption / Barge-in）

```
用户讲话被检测（VAD）
  ↓
取消 LLM / TTS pending
  ↓
处理新输入
```

**关键组件**：
- **VAD (Voice Activity Detection)**：Silero VAD, WebRTC VAD
- **Turn Taking**：什么时候该 Agent 说
- **Echo Cancellation**：避免自己听自己

### 4.5 功能调用在语音中

```
用户："帮我订明天 10 点到北京的机票"
  ↓ ASR
文本意图
  ↓ LLM
tool_call(book_flight, ...)
  ↓
订票系统确认
  ↓
"已帮您订好，是否发送到您邮箱？"
  ↓ TTS
语音
```

**挑战**：
- 参数澄清（需要引导式对话）
- 不可逆操作确认
- 长操作等待期的"嗯，我正在查..."填充

---

## 5. 视频 Agent 模式

### 5.1 视频 QA

```
Video Upload → 抽帧 / 音频转录 → VLM 理解 → 回答
```

### 5.2 视频摘要

```
Long Video 
  → Chunk (每 5 分钟)
  → 每 chunk 摘要
  → 二次合并
```

### 5.3 视频监控 Agent

```
摄像头流 → 持续 VAD / 物体检测 → 异常触发 VLM → Alert
```

### 5.4 关键挑战

- 上下文长度（Gemini 2.0 支持长视频）
- 抽帧策略（均匀 vs 关键帧）
- 成本（视频 token 贵）

---

## 6. 多模态工具调用

### 6.1 工具返回图像

```
Agent：显示本月销售图
  ↓
tool_call(chart, sql="...")
  ↓
返回 base64 png
  ↓
Agent 继续处理 + 下一步
```

### 6.2 工具返回音频

```
tool_call(tts, text="..."）→ audio
Agent 插入回放
```

### 6.3 工具处理图像输入

```
用户贴一张错误截图
  ↓
Agent 传给 tool_call(diagnose, image=...)
  ↓
工具返回诊断
```

---

## 7. Realtime API 工程细节

### 7.1 WebSocket 事件模型

**OpenAI Realtime 关键事件**：

```
Client → Server:
  - session.update
  - input_audio_buffer.append
  - input_audio_buffer.commit
  - conversation.item.create
  - response.create
  - response.cancel

Server → Client:
  - session.created
  - input_audio_buffer.speech_started
  - response.audio.delta
  - response.audio.done
  - response.function_call_arguments.delta
  - response.function_call_arguments.done
  - error
```

### 7.2 音频格式

- **输入**：PCM16 @ 24kHz（主流）
- **输出**：PCM16 @ 24kHz 或 g711 / opus
- **打包**：base64 编码 + WS 发送

### 7.3 延迟分解

```
Mic ──► VAD (50ms) ──► Network (50ms) ──►
LLM first audio token (200-400ms) ──► Network (50ms) ──►
Speaker

总：350-550ms 可达
```

### 7.4 工具调用中的延迟

```
tool_call 检测 ──► 执行工具（可能数秒）──► 结果返回
                                          ↓
                                   Agent 继续
```

**关键策略**：工具执行期间，Agent 说"稍等我查一下"填充。

---

## 8. 多模态 Embedding / 检索

### 8.1 跨模态检索

```
Text Query："上个月的销售图"
  ↓ Text embedding
Vector ──► 向量库（存 Image embeddings）
  ↓
召回图片
```

**模型**：CLIP / SigLIP / Cohere Embed Multi / VoyageAI Multimodal.

### 8.2 ColPali（文档视觉检索）

- 直接用 PDF 页面截图 embedding
- 省去 OCR + parsing
- 对复杂版式文档优秀

---

## 9. 多模态 Agent 评估

### 9.1 视觉任务

- **MMBench** / MMMU：多模态综合
- **ChartQA**：图表 QA
- **DocVQA**：文档 QA
- **OCRBench**：OCR 对齐
- **MMBench-Video**：视频

### 9.2 语音

- **WER (Word Error Rate)**：ASR 准确
- **MOS (Mean Opinion Score)**：TTS 自然度
- **Turn Success Rate**：对话成功率
- **End-to-End Latency**

### 9.3 业务任务

- 电话客服：单呼完成率、NPS、升级率
- 视频总结：信息保真度
- 文档问答：准确率 + 引用

---

## 10. 成本

### 10.1 视觉

- GPT-4o Image：**比文本贵 5-20x**（按 tile）
- Claude 3.5 Sonnet：类似

**优化**：
- 图片压缩
- 关键区域裁剪
- 先 OCR 抽文字
- 缓存重复图 embedding

### 10.2 语音

- Whisper API：$0.006/分钟
- GPT-4o Realtime：$0.06-0.24/分钟（按输入输出）
- TTS：$0.015-0.03/1K 字符

**优化**：
- 用 Whisper 自托管（本地）
- 开源 TTS（Kokoro）
- 混合架构（ASR 自托管 + LLM API）

### 10.3 视频

- Gemini 1h 视频：数美元起
- 抽帧减少 10x

---

## 11. 安全与合规

### 11.1 语音克隆

- **风险**：冒充声音 / 诈骗
- **对策**：水印、同意验证、平台禁用门槛

### 11.2 视觉注入

- 图片中嵌文字指令 → Prompt Injection
- **对策**：图像过滤、signed prompt、白名单

### 11.3 PII

- 语音可能泄露身份
- 视频中人脸、车牌
- **对策**：脱敏管线（见 [[concepts/agent-security.zh-CN]]）

### 11.4 儿童 / 敏感内容

- 图像 / 视频生成必须过内容安全
- **工具**：Azure Content Safety、OpenAI Moderation、开源 NSFW 分类器

---

## 12. 架构模式

### 12.1 单模型多模态

```
GPT-4o：Text + Image + Audio 一把梭
```

简单但锁供应商。

### 12.2 专家组合（Mixture-of-Experts Router）

```
Router
 ├─ Vision Expert (Claude)
 ├─ Voice Expert (OpenAI Realtime)
 ├─ Text Expert (GPT-4o-mini)
 └─ Video Expert (Gemini)
```

灵活但工程复杂。

### 12.3 边 Pipeline + 云推理

```
Edge: 摄像头 + VAD + 轻量 VLM 初筛
Cloud: 大 VLM 深入分析
```

节省成本，低延迟感知。

---

## 13. 开源多模态栈

### 13.1 推理

- **vLLM**：支持 Llava / Qwen2-VL / Pixtral
- **SGLang**：视觉流
- **Ollama**：Llava / BakLlava
- **llama.cpp**：llama.cpp + clip.cpp

### 13.2 端到端

- **Pipecat**：语音 Agent 框架
- **Livekit Agents**：WebRTC + Agent
- **OpenVoice / XTTS**：开源 TTS
- **FunASR**：阿里开源 ASR

### 13.3 基础设施

- **MediaMTX**：RTSP/WebRTC Gateway
- **Jitsi / Livekit**：会议基础设施
- **Coqui TTS**：开源 TTS（已 archive 但可用）

---

## 14. Dawning 的多模态策略

### 14.1 Layer 0 扩展

```csharp
public interface IMultimodalProvider : ILLMProvider
{
    Task<ChatResponse> ChatWithImagesAsync(
        List<Message> messages,
        List<ImageInput> images,
        ...);

    IAsyncEnumerable<AudioChunk> StreamRealtimeAsync(
        AudioStream input,
        ...);
}
```

### 14.2 消息扩展

```csharp
public record Message(
    string Role,
    string Content,
    List<MediaPart>? Media = null);  // 支持多模态

public abstract record MediaPart;
public record ImagePart(string Url, string? MimeType) : MediaPart;
public record AudioPart(Stream Data, string Format) : MediaPart;
public record VideoPart(string Url) : MediaPart;
```

### 14.3 Voice Agent 模板（规划）

```
Dawning.Agents.Voice
  ├── PipecatAdapter
  ├── LivekitAdapter
  └── WhisperAdapter
```

### 14.4 视觉工具

- MCP Vision Tools（截图、OCR、Chart 解析）
- 文档 Parser 适配（Docling / Unstructured via MCP）

### 14.5 观测

- 语音：记录关键 turn + transcript + latency
- 视觉：记录 image hash + token 成本
- 视频：抽样保留 + 指标聚合

### 14.6 成本与预算

- ICostBudget 扩展：image token / audio second / video hour
- 多模态路由：小图走 small VLM，大图走旗舰

---

## 15. 端到端例子：语音客服

```
客户 ──PSTN──► SIP Gateway ──WebRTC──►
  Livekit ──►
    Pipecat Agent ──►
      ├─ VAD (Silero)
      ├─ ASR (Whisper stream)
      ├─ Dawning Agent
      │   ├─ Memory (客户历史)
      │   ├─ Tool (工单系统 via MCP)
      │   └─ Policy (升级 / HITL)
      └─ TTS (Kokoro / ElevenLabs)
  ──► 客户

Telemetry:
  - 每 turn Trace（transcript + tool calls）
  - 延迟、打断次数
  - 成本（ASR 分钟数 + LLM tokens + TTS 字符）
  - Sentiment 分析
```

---

## 16. 未来展望

- **Realtime API 普及**：所有主流 LLM 2026-2027 有 Realtime 版本
- **视频 Agent 实用化**：Gemini 3+ / GPT-5 长视频
- **设备端 VLM**：Apple Foundation Models / Gemini Nano / Phi-multimodal
- **世界模型**：DeepMind Genie 级别
- **统一多模态接口**：类似 Chat Completions 的 Realtime 标准

---

## 17. 小结

> 多模态不是 Chat 的装饰，是**交互范式的跃迁**。
> 从"打字 → 读字"变成"看 / 听 / 说 / 做"——这对延迟、可观测、成本都提出新挑战。
>
> Dawning 的策略：**Layer 0 抽象扩展 + 专用语音/视觉适配 + 保持 8 层治理一致性**，
> 让多模态不是特殊通道，而是 Kernel 能力的自然延展。

---

## 18. 延伸阅读

- [[concepts/structured-output.zh-CN]] — 多模态中的结构化输出
- [[concepts/agent-security.zh-CN]] — 多模态安全
- [[concepts/observability-deep.zh-CN]] — 多模态 Trace
- [[concepts/cost-optimization.zh-CN]] — 多模态成本
- OpenAI Realtime API：<https://platform.openai.com/docs/guides/realtime>
- Pipecat：<https://github.com/pipecat-ai/pipecat>
- Livekit Agents：<https://docs.livekit.io/agents/>
- Gemini Multimodal：<https://ai.google.dev/gemini-api/docs>
