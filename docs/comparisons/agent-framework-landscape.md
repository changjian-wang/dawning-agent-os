# Agent Framework Landscape Analysis

> Comprehensive survey of all major AI agent frameworks as of April 2026.
> Purpose: Inform architecture decisions for the Dawning Agent Framework.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Framework Catalog](#2-framework-catalog)
3. [Detailed Analysis](#3-detailed-analysis)
4. [Cross-Framework Comparison Matrix](#4-cross-framework-comparison-matrix)
5. [Architecture Patterns Analysis](#5-architecture-patterns-analysis)
6. [Key Takeaways for Dawning](#6-key-takeaways-for-dawning)

---

## 1. Executive Summary

The AI agent framework space has undergone rapid consolidation in 2025-2026. Key trends:

- **Microsoft consolidation**: AutoGen → maintenance mode; Semantic Kernel agents + new Microsoft Agent Framework (MAF) as the enterprise successor.
- **Multi-language expansion**: Google ADK (Python + Java + Go), MAF (.NET + Python), Semantic Kernel (C# + Python + Java). The .NET ecosystem remains underserved by most frameworks.
- **Protocol standardization**: A2A (Agent-to-Agent) and MCP (Model Context Protocol) are becoming de facto standards for agent interop and tool integration.
- **Workflow-as-graph**: Nearly every production framework now models agent orchestration as a stateful graph (LangGraph, MAF Workflows, CrewAI Flows, Pydantic AI Graph).
- **Memory maturation**: Short-term (session) + long-term (semantic/vector) memory is now table stakes; the frontier is self-improving memory (skill evolution, observational memory).
- **Code-first agents**: smolagents and DSPy represent a shift toward agents that write and execute code rather than emitting tool-call JSON.

**Gap for Dawning**: No .NET-native framework offers all of: distributed three-plane architecture + skill self-evolution + enterprise DI patterns. This is our differentiator.

---

## 2. Framework Catalog

### 2.1 Tier 1 — Major Production Frameworks (★★★★★)

| # | Framework | Maintainer | Language | Stars | License | Status |
|---|-----------|-----------|----------|-------|---------|--------|
| 1 | **Microsoft Agent Framework (MAF)** | Microsoft | Python + .NET | 9k | MIT | **Active** (v1.0 .NET, v1.0 Python) |
| 2 | **Semantic Kernel** | Microsoft | C# + Python + Java | 27.7k | MIT | **Active** (v1.41+) |
| 3 | **LangGraph** | LangChain | Python + JS/TS | 28.6k | MIT | **Active** (v1.1.6) |
| 4 | **CrewAI** | CrewAI Inc | Python | 48.2k | MIT | **Active** (v1.13.0) |
| 5 | **OpenAI Agents SDK** | OpenAI | Python + JS/TS | 20.6k | MIT | **Active** (v0.13.5) |
| 6 | **Google ADK** | Google | Python + Java + Go | 18.8k | Apache-2.0 | **Active** (v1.28.1) |

### 2.2 Tier 2 — Significant Frameworks (★★★★)

| # | Framework | Maintainer | Language | Stars | License | Status |
|---|-----------|-----------|----------|-------|---------|--------|
| 7 | **Agno** (ex-Phidata) | Agno AGI | Python | 39.2k | Apache-2.0 | **Active** (v2.5.14) |
| 8 | **smolagents** | Hugging Face | Python | 26.5k | Apache-2.0 | **Active** (v1.24.0) |
| 9 | **Pydantic AI** | Pydantic | Python | 16.1k | MIT | **Active** (v1.77.0) |
| 10 | **DSPy** | Stanford NLP | Python | 33.5k | MIT | **Active** (v3.1.3) |
| 11 | **Mastra** | Mastra AI (ex-Gatsby) | TypeScript | 22.7k | Apache-2.0 + EE | **Active** (v1.16.0) |
| 12 | **AG2** (AutoGen fork) | AG2AI | Python | 4.4k | Apache-2.0 | **Active** (v0.11.5) |

### 2.3 Tier 3 — Research / Niche Frameworks (★★★)

| # | Framework | Maintainer | Language | Stars | License | Status |
|---|-----------|-----------|----------|-------|---------|--------|
| 13 | **MetaGPT** | FoundationAgents | Python | 66.7k | MIT | **Slowing** (v0.8.1, last release Apr 2024) |
| 14 | **AutoGen** (original) | Microsoft | Python + .NET | 56.8k | MIT | **Maintenance mode** → MAF |
| 15 | **LlamaIndex Agents** | LlamaIndex | Python + TS | ~37k | MIT | Active (RAG-centric) |
| 16 | **Haystack** | deepset | Python | ~18k | Apache-2.0 | Active (pipeline-centric) |
| 17 | **Camel** | CAMEL-AI | Python | ~20k | Apache-2.0 | Active (research) |
| 18 | **SuperAGI** | SuperAGI | Python | ~15k | MIT | Slowing |

### 2.4 Supporting Infrastructure (Not Agent Frameworks)

| # | Tool | Role | Stars |
|---|------|------|-------|
| 19 | **LiteLLM** | LLM Gateway / Router (100+ providers) | 42.4k |
| 20 | **Dify** | LLMOps Platform with visual agent builder | ~55k |
| 21 | **LangSmith** | Observability + Eval for LangChain/LangGraph | Commercial |
| 22 | **Pydantic Logfire** | OpenTelemetry observability for Pydantic AI | Commercial |

---

## 3. Detailed Analysis

### 3.1 Microsoft Agent Framework (MAF)

**Positioning**: Enterprise-ready successor to AutoGen. First framework with official .NET + Python dual support.

| Dimension | Details |
|-----------|---------|
| **Architecture** | Agent → Workflow (graph-based) → Hosting. Three layers: Agent definition, Orchestration, Deployment |
| **Multi-Agent** | Graph-based workflows with streaming, checkpointing, human-in-the-loop, time-travel |
| **Memory** | Foundry Memory integration (cloud); session state via checkpoints |
| **Tools** | Native function tools, MCP integration, middleware pipeline |
| **Distributed** | A2A protocol support, Azure Functions hosting, Durable Agents, Durable Workflows |
| **Observability** | Built-in OpenTelemetry integration |
| **LLM Support** | Azure OpenAI, OpenAI, Foundry; extensible providers |
| **Unique Feature** | Declarative agents (YAML/JSON), DevUI for debugging, migration paths from SK and AutoGen |
| **Weakness** | Very new (v1.0 just released); tight Azure coupling in samples; smaller community than predecessors |

**Relevance to Dawning**: Closest competitor in .NET space. Study their workflow graph model and A2A integration. Their declarative agent pattern is worth considering.

---

### 3.2 Semantic Kernel

**Positioning**: Microsoft's model-agnostic SDK for building AI agents. Mature, heavily enterprise-adopted.

| Dimension | Details |
|-----------|---------|
| **Architecture** | Kernel-centric: Kernel → Plugins → Planners → Agent. Plugin ecosystem is core strength |
| **Multi-Agent** | Multi-agent via plugin composition; agents-as-plugins pattern |
| **Memory** | Vector DB integrations (Azure AI Search, Elasticsearch, Chroma, Qdrant, etc.) |
| **Tools** | Plugin system: native code functions, prompt templates, OpenAPI specs, MCP |
| **Distributed** | No native distributed runtime; relies on external hosting |
| **Observability** | OpenTelemetry integration |
| **LLM Support** | OpenAI, Azure OpenAI, Hugging Face, NVIDIA NIM, Ollama, many more |
| **Unique Feature** | Richest .NET-native experience; Process Framework for complex business workflows; 43 NuGet packages |
| **Weakness** | Agents layer added late; being partially superseded by MAF for agent workflows |

**Relevance to Dawning**: Study their Plugin architecture (closest to our ITool/IToolRegistry). Their .NET API design patterns are the gold standard.

---

### 3.3 LangGraph

**Positioning**: Low-level orchestration framework for stateful agents. The "graph engine" underneath LangChain agents.

| Dimension | Details |
|-----------|---------|
| **Architecture** | Pregel-inspired graph model: Nodes (functions/agents) + Edges (transitions) + State (shared dict). Compiled graph is the execution unit |
| **Multi-Agent** | Arbitrary DAG orchestration; subgraphs; conditional branching; cycles allowed |
| **Memory** | Durable execution with checkpointing; short-term (thread state) + long-term (cross-thread store) |
| **Tools** | LangChain tool ecosystem; MCP integration |
| **Distributed** | LangSmith Deployment for production hosting; checkpointing enables resume-from-failure |
| **Observability** | LangSmith integration (tracing, debugging, evals) |
| **LLM Support** | All LangChain-supported models (100+) |
| **Unique Feature** | Durable execution (survive failures); human-in-the-loop at any graph node; time-travel debugging; Deep Agents (new) for planning + subagents |
| **Weakness** | Complexity ceiling is high; boilerplate for simple cases; LangChain coupling |

**Relevance to Dawning**: Their checkpointing/durable execution model maps directly to our SC-1.2 (Runtime Plane). Study their state persistence layer for checkpoint store design.

---

### 3.4 CrewAI

**Positioning**: Fast, standalone multi-agent framework. Most popular pure agent framework by stars. Two modes: Crews (autonomous) + Flows (event-driven).

| Dimension | Details |
|-----------|---------|
| **Architecture** | Dual-mode: **Crews** (Agent + Task + Process) for autonomous collaboration; **Flows** (event-driven, `@start`/`@listen`/`@router` decorators) for precise control |
| **Multi-Agent** | Role-based Crews with sequential/hierarchical processes; dynamic delegation |
| **Memory** | Unified Memory System (new); short-term + long-term + entity memory |
| **Tools** | CrewAI Tools package; any Python function; LangChain tool import |
| **Distributed** | Crew Control Plane (commercial) for monitoring/managing; no native distributed worker model |
| **Observability** | Built-in tracing; Crew Control Plane for enterprise monitoring |
| **LLM Support** | Via LiteLLM: 100+ providers |
| **Unique Feature** | YAML-based agent/task configuration; Crew AMP enterprise suite; event-driven Flows with `or_`/`and_` logical operators |
| **Weakness** | Python only; commercial control plane for production features; no native distributed execution |

**Relevance to Dawning**: Their Crew (autonomous) + Flow (deterministic) dual pattern is elegant. Our Orchestrator should support both modes. Their YAML config approach for agent definitions is worth studying.

---

### 3.5 OpenAI Agents SDK

**Positioning**: Lightweight, provider-agnostic framework from OpenAI. Minimalist API design philosophy.

| Dimension | Details |
|-----------|---------|
| **Architecture** | Agent → Runner → Trace. Extremely minimal: Agent (instructions + tools + handoffs) → Runner (execution loop) |
| **Multi-Agent** | Handoffs (agent-to-agent transfer); Agents-as-tools pattern |
| **Memory** | Sessions with automatic conversation history; Redis session persistence |
| **Tools** | Function tools, hosted tools, MCP integration; Guardrails for input/output validation |
| **Distributed** | No native distributed runtime |
| **Observability** | Built-in tracing UI; trace export |
| **LLM Support** | OpenAI Responses + Chat Completions + 100+ via LiteLLM/any-llm adapter |
| **Unique Feature** | Extreme simplicity (< 100 lines for a working agent); Realtime voice agents; Guardrails as first-class concept; human-in-the-loop built-in |
| **Weakness** | No workflow/graph engine; no durable execution; no distributed model; Python only |

**Relevance to Dawning**: Our Dawning.Agents was inspired by this SDK's minimalism. The Handoff pattern is already in our codebase. Their Guardrails concept should be part of our Control Plane's Policy Store.

---

### 3.6 Google ADK (Agent Development Kit)

**Positioning**: Google's code-first agent framework optimized for Gemini but model-agnostic. Multi-language (Python + Java + Go).

| Dimension | Details |
|-----------|---------|
| **Architecture** | Agent → sub_agents hierarchy. Parent agents coordinate children. Code-first + Agent Config (no-code YAML) |
| **Multi-Agent** | Hierarchical sub_agent composition; coordinator pattern |
| **Memory** | Session-based state management; Vertex AI integration for persistent state |
| **Tools** | Google Search, MCP tools, OpenAPI specs, custom functions; tool confirmation (HITL) |
| **Distributed** | A2A protocol integration; Cloud Run deployment; Vertex AI Agent Engine for scaling |
| **Observability** | Built-in development UI; evaluation framework (`adk eval`) |
| **LLM Support** | Gemini (optimized), model-agnostic |
| **Unique Feature** | Built-in eval framework; A2A protocol native; custom service registry; session rewind; three languages |
| **Weakness** | Gemini-optimized (other models are second-class); strong Google Cloud coupling |

**Relevance to Dawning**: Their A2A protocol integration is exactly what our SC-1.4 async contracts should be compatible with. The eval framework and session rewind are features to include in our Control Plane.

---

### 3.7 Agno (ex-Phidata)

**Positioning**: Runtime for agentic software. Build → Run → Manage paradigm. Strong production focus with AgentOS.

| Dimension | Details |
|-----------|---------|
| **Architecture** | Three-tier: **Framework** (agents/teams/workflows) + **Runtime** (FastAPI backend, stateless, session-scoped) + **Control Plane** (AgentOS UI) |
| **Multi-Agent** | Teams with skills; coordinated multi-agent systems |
| **Memory** | History-aware agents; knowledge bases; session isolation per-user |
| **Tools** | 100+ integrations; MCP tools; custom functions |
| **Distributed** | Stateless runtime, horizontally scalable; AgentOS for management |
| **Observability** | Native tracing; AgentOS UI for monitoring |
| **LLM Support** | All major providers (OpenAI, Anthropic, Google, etc.) |
| **Unique Feature** | AgentOS as a complete control plane; stateless runtime design; self-learning agents (Pal, Dash, Scout, Gcode); observational memory |
| **Weakness** | Python only; AgentOS is commercial/hosted |

**Relevance to Dawning**: **Closest architectural peer**. Their Framework + Runtime + Control Plane maps to our three-plane architecture. Their observational memory and self-learning agent concept aligns with our Memento-Skills vision. Study extensively.

---

### 3.8 smolagents (Hugging Face)

**Positioning**: Minimalist "agents that think in code." Code agents write Python actions instead of JSON tool calls.

| Dimension | Details |
|-----------|---------|
| **Architecture** | CodeAgent (writes Python) + ToolCallingAgent (writes JSON). Core logic in ~1000 lines |
| **Multi-Agent** | Multi-agent hierarchies; managed agents |
| **Memory** | Conversation history; no native long-term memory |
| **Tools** | MCP tools, LangChain tools, HF Spaces as tools, Hub tool sharing |
| **Distributed** | No native distributed model; sandboxed execution (E2B, Docker, Modal, Pyodide+Deno) |
| **Observability** | Minimal; relies on external logging |
| **LLM Support** | transformers, Ollama, HF inference providers, LiteLLM, OpenAI, Anthropic |
| **Unique Feature** | Code-as-action paradigm (30% fewer steps, higher benchmark scores); agent sharing via HF Hub; sandboxed execution options; CLI tools |
| **Weakness** | Limited memory; no persistence; no enterprise features; code execution security concerns |

**Relevance to Dawning**: The code-agent paradigm is worth supporting as an alternative execution mode. Their benchmark data (code agents outperform tool-call agents) should inform our agent type design.

---

### 3.9 Pydantic AI

**Positioning**: "FastAPI feeling" for agent development. Type-safe, DI-native, production-grade.

| Dimension | Details |
|-----------|---------|
| **Architecture** | Agent[DepsType, OutputType] — generic, fully typed. RunContext for DI. Capability abstraction for composable extensions |
| **Multi-Agent** | Agents-as-plugins; graph support for complex orchestration |
| **Memory** | Via capabilities; durable execution for state persistence |
| **Tools** | Decorated functions with type-safe DI; MCP; deferred tools with human approval |
| **Distributed** | Durable execution (survive API failures/restarts); A2A protocol support |
| **Observability** | Pydantic Logfire (OpenTelemetry); alternative OTel backends supported |
| **LLM Support** | ~30+ providers native; custom model interface |
| **Unique Feature** | Full static type safety; Capability abstraction (composable bundles of tools+hooks+instructions); AgentSpec (YAML/JSON agent definition); pydantic_evals (systematic evaluation); durable execution |
| **Weakness** | Python only; graph support is new; smaller than LangGraph/CrewAI ecosystems |

**Relevance to Dawning**: **Most aligned API design philosophy**. Their `Agent[DepsType, OutputType]` generic pattern + RunContext DI is exactly our DI-first principle. The Capability abstraction is how we should package skill bundles. Study their type-safety patterns for our C# implementation.

---

### 3.10 DSPy

**Positioning**: "Programming—not prompting—LLMs." Declarative modules with automatic prompt optimization.

| Dimension | Details |
|-----------|---------|
| **Architecture** | Modules (Predict, ChainOfThought, ReAct) → Signatures (input/output schemas) → Optimizers (prompt tuners) |
| **Multi-Agent** | Not agent-centric; focuses on composable LLM modules |
| **Memory** | Not a core concept; stateless modules |
| **Tools** | ReAct module supports tools; general function integration |
| **Distributed** | No native distributed model |
| **Observability** | Evaluation-driven development; optimization traces |
| **LLM Support** | Model-agnostic via adapter layer |
| **Unique Feature** | **Automatic prompt optimization** — define what you want (signatures), DSPy finds the best prompt. Self-improving pipelines. GEPA algorithm (reflective evolution outperforms RL) |
| **Weakness** | Not an agent framework per se; steep learning curve; different paradigm from others |

**Relevance to Dawning**: **Critical for skill self-evolution**. DSPy's automatic prompt optimization and GEPA algorithm (reflective prompt evolution) directly inform our Memento-Skills implementation. The concept of compiling declarative intent into optimized prompts is foundational for SC-7.

---

### 3.11 Mastra

**Positioning**: TypeScript-native agent framework from the Gatsby team. Full-stack agent development.

| Dimension | Details |
|-----------|---------|
| **Architecture** | Agents + Workflows (graph-based, `.then()/.branch()/.parallel()`) + Storage + Memory |
| **Multi-Agent** | Team orchestration; workflow-based coordination |
| **Memory** | Conversation history; working memory; semantic recall; observational memory |
| **Tools** | MCP servers (authoring + consuming); 100+ integrations |
| **Distributed** | Deploy as standalone server; deployers for various platforms |
| **Observability** | Built-in evals; observability packages (OTel Bridge) |
| **LLM Support** | 40+ providers via AI SDK |
| **Unique Feature** | TypeScript-native; MCP server authoring; observational memory (human-like recall); auth/RBAC (enterprise edition); React/Next.js integration |
| **Weakness** | TypeScript only; enterprise features behind EE license |

**Relevance to Dawning**: Their observational memory concept aligns with our Memory Plane's long-term knowledge service. The TypeScript ecosystem context is different but their workflow API design (`.then()/.branch()/.parallel()`) is clean.

---

### 3.12 AG2 (AutoGen Fork)

**Positioning**: Open-source community fork of AutoGen. "AgentOS for AI Agents." Maintained by volunteers.

| Dimension | Details |
|-----------|---------|
| **Architecture** | ConversableAgent as building block; GroupChat patterns; heading toward beta v1.0 architecture |
| **Multi-Agent** | Group chats (auto/manual/round-robin selection); swarms; nested chats; sequential chats |
| **Memory** | Conversation history; teachable agents |
| **Tools** | register_function pattern; MCP integration; A2A support |
| **Distributed** | No native distributed runtime |
| **Observability** | Basic logging |
| **LLM Support** | Via config files; multi-model |
| **Unique Feature** | Rich orchestration patterns (9+ in pattern cookbook); backward compatibility with AutoGen |
| **Weakness** | Community-managed; transitioning to new architecture; smaller than MAF |

**Relevance to Dawning**: Their group chat orchestration patterns (AutoPattern, RoundRobinPattern, etc.) are useful references for our Orchestrator design.

---

### 3.13 MetaGPT

**Positioning**: "Software company as multi-agent system." SOP-driven multi-agent collaboration.

| Dimension | Details |
|-----------|---------|
| **Architecture** | Role-based agents (PM, Architect, Engineer, QA) following SOP (Standard Operating Procedures) |
| **Multi-Agent** | Team with predefined roles; message-passing via shared environment; SOP-driven sequencing |
| **Memory** | Shared workspace; document-based context |
| **Tools** | Code execution; web search; file management |
| **Distributed** | No native distributed model |
| **Observability** | Minimal |
| **LLM Support** | OpenAI, Azure, Ollama, Groq, etc. |
| **Unique Feature** | `Code = SOP(Team)` philosophy; AFlow (automated agentic workflow generation, ICLR 2025 oral); data interpreter |
| **Weakness** | Last release v0.8.1 (Apr 2024); development appears slowed; tight coupling to software development domain |

**Relevance to Dawning**: Their SOP-as-code concept is interesting for domain-specific agent templates. AFlow paper for automated workflow generation could inform our dynamic orchestration.

---

## 4. Cross-Framework Comparison Matrix

### 4.1 Core Capabilities

| Capability | MAF | SK | LangGraph | CrewAI | OpenAI SDK | Google ADK | Agno | smolagents | Pydantic AI | DSPy | Mastra |
|-----------|-----|----|----|--------|---------|---------|------|------------|------------|------|--------|
| **Multi-Agent** | ✅ Graph | ✅ Plugin | ✅ Graph | ✅ Crew | ✅ Handoff | ✅ Hierarchy | ✅ Team | ✅ Hierarchy | ✅ Graph | ❌ | ✅ Workflow |
| **Memory (Short)** | ✅ | ✅ | ✅ | ✅ | ✅ Session | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| **Memory (Long)** | ✅ Foundry | ✅ Vector | ✅ Store | ✅ Unified | ❌ | ⚠️ Vertex | ✅ | ❌ | ✅ Capability | ❌ | ✅ Semantic |
| **Durable Execution** | ✅ | ❌ | ✅ | ⚠️ Checkpoint | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ Suspend |
| **MCP Support** | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| **A2A Protocol** | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ |
| **Human-in-Loop** | ✅ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ Approval | ❌ | ✅ Deferred | ❌ | ✅ Suspend |
| **Code Execution** | ⚠️ | ❌ | ⚠️ | ❌ | ❌ | ✅ Sandbox | ❌ | ✅ Sandboxed | ❌ | ❌ | ❌ |
| **Guardrails** | ✅ Middleware | ⚠️ | ⚠️ | ⚠️ | ✅ Native | ⚠️ | ✅ | ❌ | ⚠️ | ❌ | ⚠️ |
| **Eval Framework** | ⚠️ Labs | ❌ | ⚠️ LangSmith | ❌ | ✅ Trace | ✅ `adk eval` | ✅ AgentOS | ❌ | ✅ pydantic_evals | ✅ Core | ✅ |
| **Auto-Optimization** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ Core | ❌ |

### 4.2 Language & Ecosystem

| Framework | Python | .NET/C# | TypeScript | Java | Go | Stars | Contributors |
|-----------|--------|---------|------------|------|-----|-------|-------------|
| **MAF** | ✅ | ✅ | ⚠️ UI | ❌ | ❌ | 9k | 121 |
| **Semantic Kernel** | ✅ | ✅ | ⚠️ | ✅ | ❌ | 27.7k | 435 |
| **LangGraph** | ✅ | ❌ | ✅ | ❌ | ❌ | 28.6k | 289 |
| **CrewAI** | ✅ | ❌ | ❌ | ❌ | ❌ | 48.2k | 302 |
| **OpenAI SDK** | ✅ | ❌ | ✅ | ❌ | ❌ | 20.6k | 240 |
| **Google ADK** | ✅ | ❌ | ❌ | ✅ | ✅ | 18.8k | 261 |
| **Agno** | ✅ | ❌ | ❌ | ❌ | ❌ | 39.2k | 428 |
| **smolagents** | ✅ | ❌ | ❌ | ❌ | ❌ | 26.5k | 208 |
| **Pydantic AI** | ✅ | ❌ | ❌ | ❌ | ❌ | 16.1k | 425 |
| **DSPy** | ✅ | ❌ | ❌ | ❌ | ❌ | 33.5k | 394 |
| **Mastra** | ❌ | ❌ | ✅ | ❌ | ❌ | 22.7k | 400 |
| **AG2** | ✅ | ❌ | ❌ | ❌ | ❌ | 4.4k | 179 |

### 4.3 Production Readiness

| Framework | Distributed | Horizontal Scale | State Persistence | OTel | Enterprise Support |
|-----------|------------|------------------|-------------------|------|-------------------|
| **MAF** | ✅ A2A + Durable | ✅ Azure | ✅ Checkpoints | ✅ | ✅ Microsoft |
| **Semantic Kernel** | ⚠️ Manual | ⚠️ Manual | ✅ Process state | ✅ | ✅ Microsoft |
| **LangGraph** | ⚠️ LangSmith Deploy | ✅ Deploy | ✅ Checkpoints | ⚠️ LangSmith | ✅ LangChain |
| **CrewAI** | ❌ | ⚠️ Control Plane | ✅ JSONB checkpoints | ✅ Telemetry | ✅ AMP Suite |
| **OpenAI SDK** | ❌ | ❌ | ⚠️ Redis Sessions | ✅ Tracing | ❌ |
| **Google ADK** | ✅ A2A | ✅ Cloud Run / Vertex | ✅ Session store | ⚠️ | ✅ Google Cloud |
| **Agno** | ✅ StatelessRT | ✅ Horizontal | ✅ DB-backed | ✅ Tracing | ✅ AgentOS |
| **Pydantic AI** | ✅ Durable Exec | ⚠️ | ✅ State persistence | ✅ Logfire | ⚠️ |

---

## 5. Architecture Patterns Analysis

### 5.1 Agent Definition Patterns

| Pattern | Frameworks | Description |
|---------|-----------|-------------|
| **Code-first Class** | SK, smolagents, AG2 | Agent defined as a class with methods/decorators |
| **Config Object** | OpenAI SDK, Google ADK, Agno | Agent = `Agent(name, instructions, tools)` |
| **Generic Typed** | Pydantic AI | `Agent[DepsType, OutputType]` with full type inference |
| **YAML/Declarative** | CrewAI, MAF, Google ADK, Pydantic AI | Agent defined in config files, no code required |
| **SOP-based** | MetaGPT | Agent = Role with predefined SOP steps |

**Recommendation for Dawning**: Start with Config Object (like OpenAI SDK for simplicity) + Generic Typed (like Pydantic AI for type safety). Add YAML/Declarative later.

### 5.2 Orchestration Patterns

| Pattern | Frameworks | Description |
|---------|-----------|-------------|
| **Sequential Pipeline** | CrewAI, MetaGPT | A → B → C, each passes output to next |
| **Stateful Graph (DAG)** | LangGraph, MAF, Pydantic AI, Mastra | Nodes + edges + state. Most flexible |
| **Hierarchical Sub-agents** | Google ADK, smolagents | Parent delegates to children |
| **Handoff Chain** | OpenAI SDK, MAF | Agent hands off control to another agent |
| **Group Chat** | AG2, AutoGen | Multiple agents in a shared conversation |
| **Crew + Flow** | CrewAI | Autonomous Crews embedded in deterministic Flows |
| **Dynamic (LLM-decided)** | LangGraph, AG2 | LLM decides which agent to invoke next |

**Recommendation for Dawning**: Implement Stateful Graph as the core primitive (like LangGraph). Build Sequential, Handoff, and Hierarchical as graph presets. Support Dynamic routing via LLM-based edge decisions.

### 5.3 Memory Architecture Patterns

| Pattern | Frameworks | Description |
|---------|-----------|-------------|
| **Message List Only** | OpenAI SDK, smolagents, AG2 | In-memory conversation history |
| **Session + Vector Store** | SK, LangGraph, CrewAI, Google ADK | Short-term sessions + long-term vector retrieval |
| **Dual-Layer (Working + Long)** | Agno, Mastra | Explicit working memory + semantic long-term memory |
| **Observational Memory** | Agno, Mastra | Agent learns from interactions, builds understanding over time |
| **Auto-Optimization** | DSPy | Prompts are automatically optimized based on evaluation |
| **Scope-Isolated** | Dawning (planned), AgentOS | Four-tier namespace: global/team/session/private |

**Recommendation for Dawning**: Our dual-layer + scope-isolation design is ahead of most frameworks. Add observational memory (auto-extract patterns from interactions) as the foundation for Memento-Skills.

### 5.4 Tool Integration Patterns

| Pattern | Frameworks | Description |
|---------|-----------|-------------|
| **Decorated Functions** | Pydantic AI, OpenAI SDK, SK, Google ADK | `@tool` decorator on Python/C# functions |
| **MCP Servers** | Most (2025+) | Connect to any MCP-compatible tool server |
| **Agents-as-Tools** | OpenAI SDK, SK, MAF | Wrap an agent as a tool for another agent |
| **Code Execution** | smolagents, Google ADK, AG2 | Agent writes and executes code as actions |

**Recommendation for Dawning**: Support all four patterns. Decorated functions for simple tools, MCP for ecosystem interop, agents-as-tools for multi-agent, code execution as an advanced mode.

### 5.5 Distributed Runtime Patterns

| Pattern | Frameworks | Description |
|---------|-----------|-------------|
| **In-Process Only** | OpenAI SDK, smolagents, DSPy | Single process, no distribution |
| **Cloud-Hosted** | LangGraph (Deploy), CrewAI (AMP) | Vendor manages infrastructure |
| **Durable Tasks** | MAF (Durable Agents), LangGraph | Checkpoint + resume workflow across process restarts |
| **A2A Protocol** | MAF, Google ADK, Pydantic AI | Standardized inter-agent communication |
| **Stateless Runtime** | Agno | FastAPI backend, session-scoped, horizontally scalable |

**Recommendation for Dawning**: Combine Durable Tasks (for resilience) + A2A Protocol (for interop) + Stateless Workers (for scaling). This is our SC-1.2 Runtime Plane design.

---

## 6. Key Takeaways for Dawning

### 6.1 Competitive Advantages (Unique Positioning)

| Advantage | Why No One Else Has It |
|-----------|----------------------|
| **.NET-native three-plane distributed architecture** | MAF and SK are .NET but don't have our three-plane model. Everyone else is Python/TS |
| **Skill self-evolution (Memento-Skills)** | Only DSPy does auto-optimization, but for prompts not skills. No framework has agent-designing-agent capability |
| **Pure DI architecture** | SK uses kernel abstraction; MAF is close but newer. We can be the cleanest DI-first design |
| **Scope-isolated memory with RBAC** | Agno has session isolation but not four-tier scoped memory with policy enforcement |

### 6.2 Must-Have Features (Table Stakes by 2026)

Based on this survey, the following are **minimum requirements** for any serious agent framework:

1. **MCP Support** — Universal tool integration protocol
2. **A2A Protocol** — Agent-to-agent interoperability standard
3. **Durable Execution** — Checkpointing, resume from failure
4. **Human-in-the-Loop** — Approval workflows, deferred tools
5. **Graph-Based Orchestration** — DAG with conditional edges
6. **OpenTelemetry** — Distributed tracing, observability
7. **Multi-Provider LLM** — At minimum: OpenAI, Azure OpenAI, Ollama, Anthropic
8. **Evaluation Framework** — Benchmark, regression test, quality gates

### 6.3 Design Inspirations (Cherry-Pick from Each)

| Source Framework | What to Borrow | Map to Dawning |
|-------------------|---------------|---------------|
| **Pydantic AI** | `Agent[Deps, Output]` generic typing + Capability abstraction | IAgent generic, Skill bundles |
| **OpenAI Agents SDK** | Minimalist API, Handoff, Guardrails | Agent definition, Handoff, Policy Store |
| **LangGraph** | Durable execution, checkpointing, state graph | Runtime Plane checkpoint store |
| **Agno** | Framework + Runtime + Control Plane separation | Three-plane architecture |
| **CrewAI** | Crew (autonomous) + Flow (deterministic) dual mode | Orchestrator presets |
| **DSPy** | Auto prompt optimization, GEPA algorithm | Memento-Skills evolution engine |
| **Google ADK** | A2A integration, eval framework, session rewind | Inter-agent protocol, eval triggers |
| **MAF** | .NET graph workflows, middleware pipeline, declarative agents | Workflow engine, middleware, agent config |
| **Semantic Kernel** | Plugin architecture, NuGet packaging, .NET patterns | IToolRegistry, package structure |
| **smolagents** | Code-as-action paradigm (30% fewer steps) | CodeAgent execution mode |

### 6.4 Anti-Patterns to Avoid

| Anti-Pattern | Seen In | Lesson |
|-------------|---------|--------|
| LLM provider lock-in | Google ADK (Gemini-first) | Always design provider-agnostic first |
| Framework coupling | LangGraph ↔ LangChain | Keep abstractions and implementations cleanly separated |
| Monolithic single-process | OpenAI SDK, smolagents | Design for distribution from day 1, even if MVP is in-process |
| Too many concepts upfront | LangGraph (nodes, edges, state, channels) | Start simple, add complexity progressively |
| Commercial features gating | CrewAI AMP, Agno AgentOS | Keep all core distributed features open source |

---

*Document Version: 1.0*
*Last Updated: 2026-04-07*
*Author: AI-assisted analysis for Dawning Agent Framework*
