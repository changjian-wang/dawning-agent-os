---
title: "本地 LLM 方案对比：Ollama、LM Studio、vLLM、TensorRT-LLM、llama.cpp"
type: comparison
tags: [local-llm, ollama, vllm, tensorrt-llm, llamacpp, lmstudio, tgi, sglang]
sources: [concepts/cost-optimization.zh-CN.md, concepts/deployment-architectures.zh-CN.md]
created: 2026-04-17
updated: 2026-04-17
status: active
---

# 本地 LLM 方案对比：Ollama、LM Studio、vLLM、TensorRT-LLM、llama.cpp

> 本地部署 LLM 的理由：成本、合规、离线、低延迟、数据主权。
> 但方案众多，差别巨大：有的是开发者单机体验，有的是生产级高吞吐服务。
>
> 本文按"开发者工具 / 边缘 / 生产服务"三类梳理 8 个主流方案，并给出选型决策。

---

## 1. 为什么本地 LLM

| 动机 | 说明 |
|------|------|
| **成本** | 大量请求时摊薄硬件成本低于 API |
| **合规** | 数据不出网关 / 不出境 |
| **延迟** | 同机房 / 同主机直连 |
| **离线** | 无网络环境（工厂、船、边缘）|
| **定制** | LoRA / Full FT 私有模型 |
| **隐私** | 敏感 Prompt 不离开 |
| **可靠** | 不受外部 API 影响 |

---

## 2. 选型分类

```
┌──────────────────┬──────────────────┬──────────────────┐
│  开发者工具        │     边缘          │    生产服务        │
│  (单机体验)        │  (资源受限)        │   (高吞吐)        │
├──────────────────┼──────────────────┼──────────────────┤
│  Ollama          │  llama.cpp       │  vLLM            │
│  LM Studio       │  MLX (Apple)     │  TensorRT-LLM    │
│  GPT4All         │  ExecuTorch      │  TGI             │
│                  │                  │  SGLang          │
│                  │                  │  Triton + LMI    │
└──────────────────┴──────────────────┴──────────────────┘
```

---

## 3. Ollama

### 3.1 定位

**开发者体验第一**。一行命令拉起本地 LLM。

### 3.2 特点

- CLI + REST API + OpenAI 兼容端点
- 模型仓库（`ollama pull llama3.3`）
- 基于 llama.cpp，跨平台（Mac/Linux/Windows）
- 支持 CPU + GPU（自动探测）
- Modelfile（类似 Dockerfile）
- 内置量化模型

### 3.3 使用

```bash
ollama run llama3.3
curl http://localhost:11434/v1/chat/completions -d '...'
```

### 3.4 优劣

| 优势 | 劣势 |
|------|------|
| 极简，几乎零配置 | 吞吐低（单机） |
| OpenAI 兼容 | 不适合多租户生产 |
| 模型生态好 | 无认证 / 限流 |
| 社区最大 | 并发能力弱 |

### 3.5 适合

- 开发 / 测试
- 个人应用
- 小团队内部工具

---

## 4. LM Studio

### 4.1 定位

**带 GUI 的 Ollama**。非技术人员友好。

### 4.2 特点

- 桌面 App（Win/Mac/Linux）
- HuggingFace 模型一键下载
- 兼容 OpenAI API
- 内置 Chat UI
- 模型参数可视化调整

### 4.3 适合

- 研究员 / 产品经理本地试用
- PoC / Demo
- 不适合生产服务

---

## 5. llama.cpp

### 5.1 定位

**Ollama 和 LM Studio 的底层引擎**。纯 C++ 推理。

### 5.2 特点

- 零依赖（只需编译器）
- 支持 GGUF 量化格式
- CPU 原生支持，GPU 通过 CUDA/Metal/Vulkan
- 嵌入式友好（Raspberry Pi / 手机）
- 量化方案丰富（Q2/Q3/Q4/Q5/Q6/Q8）

### 5.3 性能

- M2 Ultra 跑 70B Q4：~10 tokens/s
- RTX 4090 跑 70B Q4：~30 tokens/s
- CPU-only 跑 8B Q4：~5 tokens/s

### 5.4 量化对比

| 量化 | 文件大小（70B） | 质量损失 |
|------|---------------|---------|
| FP16 | 140 GB | 0% |
| Q8_0 | 75 GB | < 1% |
| Q6_K | 58 GB | < 2% |
| Q5_K_M | 50 GB | ~2% |
| Q4_K_M | 42 GB | ~3-5% |
| Q3_K_M | 34 GB | ~5-8% |
| Q2_K | 28 GB | > 10% |

**推荐**：Q4_K_M 或 Q5_K_M（质量/大小最佳平衡）。

---

## 6. MLX (Apple Silicon)

### 6.1 定位

Apple 官方，**专为 M 系列芯片优化**。

### 6.2 特点

- 统一内存架构原生支持
- 比 llama.cpp 在 Mac 上快 1.5-2x
- Python + Swift SDK
- 支持微调（LoRA / QLoRA）

### 6.3 适合

- Mac 开发机
- Apple 生态内应用
- M3/M4 Ultra 工作站

---

## 7. vLLM

### 7.1 定位

**生产级高吞吐推理**。UC Berkeley 开源，业界最常用。

### 7.2 核心创新

**PagedAttention**：借鉴操作系统虚拟内存，让 KV Cache 分块管理：
- 内存利用率从 30% 提升到 90%+
- 吞吐提升 2-4x

**Continuous Batching**：
- 动态拼批，新请求可插入运行中 batch
- GPU 利用率接近 100%

### 7.3 部署

```bash
vllm serve meta-llama/Llama-3.3-70B-Instruct \
    --tensor-parallel-size 4 \
    --max-model-len 32768
```

OpenAI 兼容端点自动启动在 `http://localhost:8000/v1`。

### 7.4 性能

- Llama 3.3 70B on 4× A100：~1000 tokens/s 聚合吞吐
- 单请求 TTFT：~200ms
- 量化支持：AWQ / GPTQ / FP8 / INT4

### 7.5 特色

- Prefix Caching（系统级）
- Speculative Decoding
- Multi-LoRA（动态切换 LoRA 适配器）
- Tool Calling（v0.6+）
- Structured Output（Outlines 集成）

### 7.6 适合

- 生产服务
- 多租户
- 需要高吞吐

---

## 8. TensorRT-LLM

### 8.1 定位

**NVIDIA 官方最快**。GPU 极致优化。

### 8.2 特点

- C++ 编译引擎（需预编译模型）
- 比 vLLM 快 1.3-1.8x（在 NVIDIA 硬件上）
- 支持 H100 / A100 / L40S / L4
- FP8 原生
- In-flight batching

### 8.3 劣势

- 部署复杂（需构建 engine）
- 只支持 NVIDIA
- 模型支持不如 vLLM 广
- 版本升级 painful

### 8.4 部署

用 **Triton Inference Server** 包装：

```bash
# 1. 构建 engine
python build.py --model llama3.3-70b --fp8 --tp 4

# 2. Triton 启动
tritonserver --model-repository=/models
```

### 8.5 适合

- NVIDIA 大规模部署
- 极致延迟要求
- 有专门 ML Engineer

---

## 9. Text Generation Inference (TGI)

### 9.1 定位

**HuggingFace 官方**生产推理。

### 9.2 特点

- Rust 实现（高性能）
- 开箱即用 Docker 镜像
- 支持主流架构
- Continuous batching + PagedAttention
- 与 HuggingFace Hub 深度集成

### 9.3 vs vLLM

| 维度 | TGI | vLLM |
|------|-----|------|
| 语言 | Rust | Python |
| 性能 | 接近 | 稍快 |
| 生态 | HF 原生 | 社区大 |
| 新模型支持 | 快 | 更快 |
| 许可证 | HFOIL（v2） | Apache 2 |

### 9.4 适合

- HuggingFace 生态用户
- 不愿自己运维 vLLM 的团队

---

## 10. SGLang

### 10.1 定位

**新兴生产推理**，2024-2025 快速崛起。

### 10.2 核心创新

**RadixAttention**：Prefix Cache 用 Radix Tree，命中率高于 vLLM：
- 共享 system prompt 场景快 3-5x
- 多轮对话共享 context 显著加速

**结构化输出原生**：
- JSON Schema 编译到推理路径
- 比 Outlines 快 3x

### 10.3 vs vLLM

- 在结构化输出 / agent 场景更快
- 生态小于 vLLM
- 被 LMSYS 等高流量场景采用

### 10.4 适合

- 大量共享 prompt 的场景
- 严格结构化输出
- 前沿性能探索

---

## 11. 横向性能对比

### 11.1 场景：Llama 3.3 70B on 4× A100

| 方案 | 吞吐 (tokens/s) | TTFT (p50) | 部署难度 | 许可证 |
|------|----------------|-----------|---------|-------|
| llama.cpp | 30 | 400ms | 易 | MIT |
| Ollama | 30 | 400ms | 极易 | MIT |
| TGI | 800 | 250ms | 中 | HFOIL v2 |
| vLLM | 1000 | 200ms | 中 | Apache 2 |
| SGLang | 1100 | 180ms | 中 | Apache 2 |
| TensorRT-LLM | 1400 | 150ms | 难 | Apache 2 |

（数据为参考量级，实际随模型 / batch / 量化变化大）

### 11.2 场景：8B 模型单机 CPU

| 方案 | tokens/s |
|------|---------|
| llama.cpp (Q4) | 8-15 |
| Ollama | 8-15 |
| vLLM (CPU) | 5-10 |

---

## 12. 其他值得关注

| 方案 | 特点 |
|------|------|
| **ExecuTorch** (Meta) | 移动端推理 |
| **MediaPipe** (Google) | 跨平台端侧 |
| **NVIDIA NIM** | 企业级托管 |
| **AWS LMI** | Bedrock 自定义模型 |
| **Groq Cloud** | 专用 LPU，延迟极低（托管） |
| **Cerebras** | Wafer-scale，极快 |

---

## 13. 模型选择

### 13.1 2026 Q1 本地可用主流模型

| 模型 | 参数 | 强项 | 许可证 |
|------|------|------|-------|
| Llama 3.3 70B | 70B | 通用强 | Llama License |
| Llama 3.3 8B | 8B | 轻量通用 | Llama License |
| Qwen 3 72B | 72B | 中文 + 通用 | Apache 2 |
| Qwen 3 32B | 32B | 性价比 | Apache 2 |
| DeepSeek V3 | 671B MoE | 推理 + 代码 | Open |
| Mistral Large 2 | 123B | 欧洲合规 | Mistral |
| Mixtral 8x22B | MoE | 性价比 | Apache 2 |
| Gemma 3 27B | 27B | Google 系 | Gemma |
| Phi-4 14B | 14B | 小但强 | MIT |
| Codestral 22B | 22B | 代码 | Non-commercial |

### 13.2 选型建议

| 场景 | 模型 |
|------|------|
| 通用对话（有足够 GPU） | Llama 3.3 70B / Qwen 3 72B |
| 轻量嵌入（边缘） | Llama 3.3 8B / Phi-4 |
| 代码 | Codestral / Qwen 3 Coder |
| 中文 | Qwen 3 |
| 合规严苛 | Mistral Large (EU) |

---

## 14. 硬件规划

### 14.1 粗略显存需求

| 模型 | FP16 | Q8 | Q4 |
|------|------|-----|-----|
| 7-8B | 16GB | 8GB | 5GB |
| 13-14B | 28GB | 14GB | 8GB |
| 30-34B | 70GB | 35GB | 20GB |
| 70-72B | 140GB | 75GB | 42GB |
| 175B | 350GB | — | — |

### 14.2 推荐硬件

| 预算 | 方案 |
|------|------|
| 个人 < $3K | Mac Studio M4 Ultra / RTX 4090 |
| 单机 < $30K | 2× H100 80GB / 4× A100 40GB |
| 集群 > $100K | 8× H100 / 集群方案 |
| 边缘 < $500 | Jetson Orin Nano |

---

## 15. Dawning 本地 LLM 集成

### 15.1 IProvider 的多后端

```csharp
services.AddLLMProvider(llm =>
{
    llm.AddLocal<OllamaProvider>(cfg =>
    {
        cfg.BaseUrl = "http://localhost:11434";
        cfg.DefaultModel = "llama3.3";
    });

    llm.AddLocal<VLLMProvider>(cfg =>
    {
        cfg.BaseUrl = "http://vllm:8000/v1";
        cfg.DefaultModel = "meta-llama/Llama-3.3-70B-Instruct";
    });
});
```

### 15.2 混合路由

```csharp
services.AddModelRouter(r =>
{
    r.AddTier("cloud-heavy", "openai:gpt-4o");
    r.AddTier("cloud-light", "openai:gpt-4o-mini");
    r.AddTier("local-medium", "vllm:llama3.3-70b");
    r.AddTier("local-light", "ollama:llama3.3-8b");
    r.Policy = RoutingPolicy.CostAware;
});
```

### 15.3 Fallback 链

```yaml
primary: cloud-heavy
on_error: cloud-light
on_rate_limit: local-medium
on_offline: local-light
```

---

## 16. 本地 LLM 的生产陷阱

| 陷阱 | 方案 |
|------|------|
| 首次加载慢（分钟级） | 预热 / 保活 |
| GPU OOM | 合理 batch size / 量化 |
| 模型版本漂移 | 固定 hash / checksum |
| 并发打崩 | Rate limit + queue |
| 数据持久化 | KV Cache 外存 |
| 观测盲区 | 埋点同 OTel 协议 |
| 合规审计 | 本地也要 AuditTrail |
| 版本回滚 | Multi-version 部署 |

---

## 17. 决策树

```
需要离线 / 不出境？
├─ 是 ──► 必须本地
│    │
│    └─► 流量？
│         ├─ 小（< 10 QPS）──► Ollama / llama.cpp
│         └─ 大            ──► vLLM / TensorRT-LLM
│
└─ 否 ──► 成本敏感？
         ├─ 是 ──► 评估自建 vs API（盈亏平衡约 100K req/day）
         └─ 否 ──► 继续用 API
```

---

## 18. 小结

> **本地 LLM 不是"一招通吃"**。
>
> - 开发体验：Ollama
> - 边缘 / 嵌入：llama.cpp / MLX
> - 生产高吞吐：vLLM / SGLang / TensorRT-LLM
>
> Dawning 通过统一的 `ILLMProvider` 抽象，让本地与云端在业务代码中无差别——
> 业务只关心"我要调用 LLM"，基础设施决定"调用什么"。

---

## 19. 延伸阅读

- [[concepts/cost-optimization.zh-CN]] — 何时切本地
- [[concepts/deployment-architectures.zh-CN]] — GPU 节点管理
- vLLM：<https://docs.vllm.ai/>
- Ollama：<https://ollama.com/>
- TensorRT-LLM：<https://github.com/NVIDIA/TensorRT-LLM>
- SGLang：<https://github.com/sgl-project/sglang>
- llama.cpp：<https://github.com/ggerganov/llama.cpp>
- MLX：<https://github.com/ml-explore/mlx>
