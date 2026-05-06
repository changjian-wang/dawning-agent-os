---
title: ADR-027 桌面渲染端 V0：原生 HTML + 预编译 preload + 通过 IPC 桥的 inbox 单页 UI
type: adr
subtype: architecture
canonical: true
summary: V0 渲染端落地为单页原生 HTML + 内联 ES2022 脚本 + 单一通过 IPC 桥的 inbox 表单与列表；preload.ts 通过独立 tsconfig.preload.json 预编译为 dist/preload.cjs.js（修通 ADR-025 §5 留下的 .cjs.js 路径未真正存在的开发态缺陷）；启动 token 仍只留 main 进程，渲染端永远见不到 token；新增 IPC 频道 agentos:inbox:capture / agentos:inbox:list 由 main 持 token + baseUrl 直接调 .NET HTTP；不引入 Vue / React / Pinia / 任何 UI 库；不引入 esbuild / Vite / Webpack 任何 bundler；不内嵌任何 inbox 编辑 / 删除 / 详情 / 多 tab / 聊天形态。
tags: [agent, engineering]
sources: []
created: 2026-05-02
updated: 2026-05-02
verified_at: 2026-05-02
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/desktop-process-supervisor-electron-dotnet-child.md, pages/adrs/inbox-v0-capture-and-list-contract.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/adrs/mvp-desktop-stack-electron-aspnetcore.md, pages/adrs/engineering-skeleton-v0.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-02
adr_revisit_when: "渲染端 LOC 超过 ~400 行 / 出现第二个独立路由（inbox 之外的视图）/ 出现 chat 流式 UI / 出现编辑或详情面板 / 引入第二个进程窗口 / 出现移动端或 web 端复用渲染层的需求 / 渲染端代码出现需要类型推断的独立模块（不再适合 inline JS）/ Electron 在 V≥34 版本支持原生 ES module preload 取代 CJS 预编译 / token 出现新的非 IPC 出口（如 OAuth 跳转、外部链接 deeplink）。"
---

# ADR-027 桌面渲染端 V0：原生 HTML + 预编译 preload + 通过 IPC 桥的 inbox 单页 UI

> V0 渲染端落地为单页原生 HTML + 内联 ES2022 脚本 + 单一通过 IPC 桥的 inbox 表单与列表；preload.ts 通过独立 tsconfig.preload.json 预编译为 dist/preload.cjs.js（修通 ADR-025 §5 留下的 .cjs.js 路径未真正存在的开发态缺陷）；启动 token 仍只留 main 进程，渲染端永远见不到 token；新增 IPC 频道 agentos:inbox:capture / agentos:inbox:list 由 main 持 token + baseUrl 直接调 .NET HTTP；不引入 Vue / React / Pinia / 任何 UI 库；不引入 esbuild / Vite / Webpack 任何 bundler；不内嵌任何 inbox 编辑 / 删除 / 详情 / 多 tab / 聊天形态。

## 背景

[ADR-025](desktop-process-supervisor-electron-dotnet-child.md) 把"Electron 主进程拉起 .NET 子进程 + 启动 token + readiness probe"打通，作为 dogfood 阶段的最小桌面壳；其 §3 / §5 的设计预设是 token 永远留在 main，渲染端通过 preload 提供的 IPC 桥拿运行时数据。但 §5 的实施期间留下两个未关闭的缺陷：

1. main.ts 用 `preload: path.join(__dirname, "preload.cjs.js")` 指向的 `.cjs.js` 文件**实际并不存在**——tsx 是 node 的 loader hook，**不**会把 `.ts` 文件落盘成 `.cjs.js`。Electron 的 BrowserWindow 加载 preload 时走的是 Electron 自己的 preload 装载链路，而不是 node 的 require / import；tsx 的 loader 在那条链路上根本不参与。也就是说现在 dev 路径下渲染端的 `window.agentos` 一直是 `undefined`，`index.html` 走的是 `'preload bridge missing — run via pnpm run dev'` 的兜底分支。这一缺陷被 ADR-025 §5 的 NOTE 注释「the next ADR (front-end stack) will close it」明确指向本 ADR。
2. 渲染端是一个 70 行的 runtime-status JSON dump 占位页，没有任何业务 UI。[ADR-026](inbox-v0-capture-and-list-contract.md) 已把 `POST /api/inbox` 与 `GET /api/inbox` 端点打通且 smoke 探针验证通过，但用户在桌面壳里**看不到**任何 inbox 入口、也无法通过 UI 触发 capture——HTTP 层闭合，UI 层未闭合。

S7 进入实施前需要锁死的问题：

- 渲染端要不要引入框架（Vue / React / Svelte / lit）？
- 渲染端要不要走 bundler（esbuild / Vite / Webpack / rollup）？
- preload 在 dev 与 prod 路径下用什么形态加载？是改 main 改用原生 ESM preload 吗？还是用 tsc 把 preload 预编译到磁盘？
- token 是否必须留在 main，还是允许通过 preload 注入到 renderer 全局变量？
- inbox 的 IPC 频道命名（`agentos:inbox:capture` 单事件 vs. `agentos:inbox` 复合事件 + op 参数）？
- renderer 端发生写操作（capture）后如何刷新列表？乐观本地 append、refetch list、SSE / WebSocket 推送？
- 错误反馈：内联红条、toast、console-only？
- 渲染端代码与样式：内联在 HTML、独立 .js + .css、还是预编译 .ts？
- inbox UI 是否要承担编辑 / 删除 / 详情 / 标签等扩展？

如果把这些问题留到代码阶段「边写边定」，桌面渲染端会立刻陷入"先选框架再回头改一切"的覆辙——本 ADR 在 ADR-025 / ADR-026 既有契约之上把渲染端 V0 的形态钉死，作为 S7 实施的依据；按 [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)，代码落地仍须在本 ADR 接受后才进行。

## 备选方案

渲染端框架（A 轴）：

- **A1** 原生 HTML + 内联 ES2022 脚本（document.createElement / addEventListener / fetch via IPC bridge），不引入任何框架。
- **A2** Vue 3 + Composition API + Pinia。
- **A3** React 19 + hooks + zustand 或 jotai。
- **A4** Svelte 5 / SolidJS / lit / Web Components。

token 出口（B 轴）：

- **B1** token 全程留 main 进程；任何对 .NET 的 HTTP 请求都通过 IPC 由 main 代发。
- **B2** main 通过 preload 注入到 renderer 全局变量（`window.__token__`），renderer 直接 fetch。
- **B3** 启动时把 token 作为 BrowserWindow.webPreferences.additionalArguments 传给 renderer，renderer 解析后用 fetch 直连。

UI 形态（C 轴）：

- **C1** 单页：上方 textarea + Capture 按钮 + 错误条；下方最近 50 条 list；标题栏一行 status（schema version / api status）。
- **C2** 多 tab：inbox / runtime status / settings。
- **C3** chat-like：消息流 + 输入框，用 inbox 形式承载会话语义。

渲染端打包（D 轴）：

- **D1** 不打包：HTML + 内联 `<script>`（CSP 已允许 `'unsafe-inline'` script-src）。任何类型安全检查由 main / preload 通过 main tsconfig 覆盖；renderer 内的 JS 不进 typecheck。
- **D2** tsc 预编译 `src/renderer/renderer.ts` → `dist/renderer/renderer.js`，HTML 外联 `<script src="renderer.js" type="module">`。
- **D3** esbuild / Vite / rollup bundler，支持代码分割、HMR、source map。

preload 加载（E 轴）：

- **E1** 用独立 `tsconfig.preload.json` 把 `src/preload.ts` 预编译为 CommonJS `dist/preload.cjs.js`；main.ts 改路径指向 `dist/preload.cjs.js`；`pnpm run dev` 在启动前先 `tsc -p tsconfig.preload.json`。
- **E2** 维持现状：main.ts 指向并不存在的 `.cjs.js`，渲染端永远走兜底分支。
- **E3** Electron V≥34 的 `webPreferences.preload` 支持 ESM `.mjs`；改 main.ts 指向 `src/preload.mjs`，让 Electron 直接加载 ESM。
- **E4** 把 preload 内联到 main.ts，通过 `BrowserWindow.executeJavaScript()` 在 BrowserWindow 加载完成后注入 `contextBridge.exposeInMainWorld(...)`。

错误显示（F 轴）：

- **F1** 表单下方一条 inline 红色文字 + 列表区一条 inline 红色文字；失败保留输入。
- **F2** toast 弹层（独立组件，3s 自动消失）。
- **F3** 仅 console.error，UI 静默失败。

写后刷新（G 轴）：

- **G1** 乐观本地 append：capture 成功后立刻把返回的 snapshot 插到列表头，不调 list。
- **G2** refetch：capture 成功后立刻调一次 `/api/inbox`，整列表重新渲染。
- **G3** SSE / WebSocket：main 监听 backend push，broadcast 给所有 BrowserWindow，renderer 增量渲染。

IPC 频道命名（H 轴）：

- **H1** 每个用例一个频道：`agentos:inbox:capture`、`agentos:inbox:list`。
- **H2** 单频道带 op：`agentos:inbox` body `{ op: "capture" | "list", payload: ... }`。
- **H3** RPC over IPC：`agentos:rpc` body `{ method: "inbox.capture", params: ... }`。

渲染端样式（I 轴）：

- **I1** 单 `<style>` 块内联在 HTML，原生 dark theme。
- **I2** 独立 .css 文件 + `<link rel="stylesheet">`。
- **I3** CSS-in-JS / Tailwind / Stitches。

渲染端单元测试（J 轴）：

- **J1** 不写渲染端测试；HTTP 层由 Api.Tests 覆盖，IPC 桥手动验证。
- **J2** Vitest + jsdom 跑 inline JS。
- **J3** Playwright 端到端（启动 Electron + 真实交互）。

CSP（K 轴）：

- **K1** 维持 ADR-025 现有 `default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'`，让内联脚本可工作。
- **K2** 移除 `'unsafe-inline'`，配合 D2/D3 强制外联。
- **K3** 加 `connect-src 'self'`（无差别），其它无变化。

## 被否决方案与理由

**A2 / A3 / A4 引入框架**：

- 渲染端 V0 的目标 LOC ~120 行（一个 form + 一个 list + IPC 调用），即使是最轻的 Svelte / lit 也会让 dev tooling 比业务代码大一个数量级。Vue / React 的 Composition API / hooks 学习曲线在 dogfood 阶段无收益。
- 任何框架引入都会触发"组件库 / 状态管理 / 构建工具"的连锁选型——按 [ADR-002 选择题优先于问答题](options-over-elaboration.md)，必须把"是否引框架"作为单一决策点钉死，而不是悄悄通过 V0 实施引入。
- 当渲染端 LOC 超过 ~400 行或出现第二个路由时（见 `adr_revisit_when`）再做框架选型 ADR，届时已有具体使用场景作为决策依据，避免凭空选型。
- A4 的 Web Components 在保留"不引入框架"语义的前提下提供组件化，但 V0 没有第二个组件复用诉求，引入会让"自定义元素 lifecycle"成为新负担。

**B2 / B3 把 token 暴露到渲染端**：

- B2 把 token 写到 `window.__token__`，违反 ADR-025 §3 的 "token 仅 main 持有" 不变量；任何渲染端代码（包括未来引入的第三方库 / 调试工具 / 浏览器扩展）都能读到 token，等于把"启动 token = 进程身份证"的语义降级为"环境变量"。
- B3 通过 webPreferences.additionalArguments 传给 renderer 进程，虽然不在 DOM 上，但 renderer 进程内任意代码（`process.argv`）都能读；与 B2 等价的失守。
- B1 把所有出 .NET 的 HTTP 调用集中在 main，是 contextIsolation + sandbox 设计的标准用法，多一跳 IPC 的延迟在桌面单进程场景可忽略（约 sub-millisecond）。

**C2 多 tab / C3 chat-like**：

- C2 多 tab 在 V0 没有第二个视图：runtime status 是诊断而非主路径（已被 smoke 覆盖），settings 在 V0 不存在配置项可改。引入 tab 容器会先于业务出现导航 / 路由抽象。
- C3 chat-like 把 inbox 错位为"会话"，违反 [ADR-014 第 3 条](mvp-first-slice-chat-inbox-read-side.md)的 inbox / chat 分层（chat 是入口，inbox 是材料容器）；让 dogfood 用户的"快速捕获一条"操作必须先点"开始会话"，多一步无收益。

**D2 / D3 引入打包**：

- D2 的 tsc 预编译 renderer 在 V0 收益微弱：内联 JS 已能用 `document.createElement` 写出强类型不变量（事件 listener、DOM 引用），且任何 typo 会被 Electron devtools 立即报出；预编译只是把"一处错误"变成"两处来源"。
- D2 / D3 都引入额外的 `dist/renderer/index.html` 拷贝步骤或更复杂的 HTML 注入，让 dev 启动链路从「`tsc preload && tsx main`」膨胀到「`tsc preload && tsc renderer && cp html && tsx main`」或「Vite dev server + Electron」。每加一段都增加调试面。
- D3 的 HMR 在桌面 dogfood 阶段没有 web 那种"边改边看"诉求——重启 Electron < 2s。
- 当渲染端真出现第二个 .ts 模块、或 LOC 超过 ~400 行时（见 `adr_revisit_when`），再用一个 ADR 决策 D 轴升级。

**E2 维持现状**：

- ADR-025 §5 的 NOTE 已经显式标注这是「下一个 ADR 关闭的待办」。本 ADR 就是那个 ADR；继续维持等于让 IPC 桥永远走兜底分支，新加的 inbox UI 就无法工作。

**E3 ESM preload**：

- Electron 33（当前 devDependencies）的 preload 仅支持 CommonJS；ESM preload 自 V≥34 起作为实验特性，且对 contextIsolation + sandbox 组合的支持仍在演进。
- 切到 V≥34 + ESM preload 等于把 ADR-016 的桌面栈版本 ADR 重打开；与"S7 用最小路径修通"的目标相反。
- 当 Electron 项目内升级 V≥34 且 ESM preload 进入 stable 时（见 `adr_revisit_when`），可独立 ADR 升级到 E3。

**E4 executeJavaScript 注入**：

- 等于绕过 contextBridge 设计；注入的脚本运行在 renderer 主世界，丢失 contextIsolation 屏障；同时 `executeJavaScript` 返回 Promise<any>，类型安全消失。
- 与 contextIsolation 推荐做法（preload + contextBridge）背道而驰，被 Electron security checklist 列为反模式。

**F2 toast / F3 console-only**：

- F2 的 toast 组件需要独立 z-index 容器、动画、消失计时器，是 V0 不愿付的复杂度；且 toast 让"capture 失败但 textarea 已清空"这种状态变得隐蔽。
- F3 让用户在 capture 失败时看不到任何反馈，是产品级缺陷。
- F1 一条 inline 红色文字 + 失败保留输入是最小可接受的反馈形态。

**G1 乐观本地 append**：

- 乐观更新意味着客户端构造 snapshot 与服务端真实状态可能漂移（例如时区显示差异、source 字段的 trim 规则差异）。在 V0 阶段宁可让 server 真相覆盖客户端预期，避免 dogfood 期出现"我看到的列表与数据库实际不一致"的隐蔽 bug。
- IPC + HTTP 的 capture→list 串行往返在桌面本地 < 50ms，UX 完全不感知差异。

**G3 SSE / WebSocket**：

- V0 没有"多窗口同步"需求（桌面壳只有一个 BrowserWindow）；SSE / WebSocket 的端到端复杂度（断线重连 / message ordering / heartbeat）远超出 V0 收益。
- 当出现"第二个进程 / 设备 / 窗口"或"agent 后台 push 通知"时（见 `adr_revisit_when`）再走独立 ADR。

**H2 / H3 复合频道**：

- H2 的单频道 + op 参数让类型推断退化为 `unknown`；preload 桥的 `inbox.capture(req)` / `inbox.list(query)` 形态被迫扁平化为 `inbox(op, payload)`。
- H3 的"RPC over IPC"模式适合 ≥ 10 个用例的成熟 API，V0 只有 2 个用例，提前抽 RPC 框架是 over-engineering。
- H1 的 `agentos:inbox:capture` / `agentos:inbox:list` 让每个 IPC handler 与其对应的 .NET endpoint 1:1 命名映射，调试时一眼定位。

**I2 / I3 独立 / 复杂样式**：

- I2 多一个 `<link>` 与一份 .css 文件，对 ~100 行 inline CSS 是过度抽象。
- I3 的 CSS-in-JS / Tailwind 必须先选 D2/D3 引入打包，被 D 轴递归否决。

**J2 / J3 渲染端测试**：

- J2 Vitest + jsdom 测试 inline JS 需要把脚本抽出 HTML，触发 D2 升级；递归依赖被否决。
- J3 Playwright 端到端测试需要真实 Electron 启动 + 探针，单次执行时间 > 10s，不适合 dogfood 阶段的 fast feedback 循环。
- J1 把渲染端测试推到「真有第二个用例 / 出现可复现的 UI bug」时再升级；当前 capture / list 的真值由 Api.Tests 覆盖，桥的 IPC contract 由 main.ts 内部一致性保证，UI 由人眼验收。

**K2 / K3 CSP 调整**：

- K2 移除 `'unsafe-inline'` 配合 D2/D3 强制外联，被 D 轴递归否决。
- K3 加 `connect-src` 没有真实收益——所有出 main 的 HTTP 都已经被 contextIsolation + IPC 屏蔽，renderer 永远不直接 fetch 后端，CSP `connect-src` 只是 belt-and-suspenders。
- K1 维持现状最小化变化面。

## 决策

采用 A1 + B1 + C1 + D1 + E1 + F1 + G2 + H1 + I1 + J1 + K1 的组合。

### 1. 渲染端形态（A1 + C1 + D1 + I1 + K1）

`apps/desktop/src/renderer/index.html` 是唯一的渲染端入口，其形态：

- 单 HTML 文件，单 `<style>` 块（dark theme），单 `<script>` 块（ES2022 内联）。
- 所有 DOM 操作通过 `document.createElement` + `addEventListener`；不引入任何 UI 库 / 框架。
- CSP 维持 ADR-025 现有：`default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'`。
- 视觉结构（自上而下）：
  1. 顶部一行 status：`schema=N · api=ok` 或 `api=disconnected`，取自 `window.agentos.runtime.getStatus()`。
  2. capture 区：textarea（占满宽度，固定 6 行）+ source 输入框（可选）+ Capture 按钮 + 表单下方红色错误条（hidden 直到出错）。
  3. list 区：`<ul>` 列表 + 空态提示（`empty`），每条目显示 `content`（multi-line 保留）+ 副信息（`source · captured at ISO 时间`）+ 缩短显示的 `id`（前 8 字符）。
  4. list 头部一个 Refresh 按钮 + 一个 total 计数（`Showing N of M`）。
- inline JS 不进 `pnpm run typecheck`；正确性由人眼 + smoke 探针 + Api.Tests 共同保证。

### 2. preload 预编译（E1）

新增独立 `apps/desktop/tsconfig.preload.json`：

```jsonc
{
  "extends": "./tsconfig.json",
  "compilerOptions": {
    "module": "CommonJS",
    "moduleResolution": "Node",
    "target": "ES2022",
    "outDir": "dist",
    "rootDir": "src",
    "noEmit": false,
    "allowImportingTsExtensions": false,
    "declaration": false
  },
  "include": ["src/preload.ts"]
}
```

约束：
- 编译产物为 `apps/desktop/dist/preload.cjs.js`（tsc 默认 .js 后缀；保留 `.cjs.js` 双段后缀让文件名同时表达"CommonJS"与"JavaScript"两个语义，与 ADR-025 现有 main.ts 引用一致——main.ts 中的路径需相应改为 `path.join(__dirname, "..", "dist", "preload.cjs.js")` 因为 `__dirname` 在 dev 路径下是 `src/`）。等价做法：tsc 默认输出 `dist/preload.js`，然后显式 cp / mv 到 `dist/preload.cjs.js`。本 ADR 不锁死具体重命名手法，只锁死最终文件名为 `dist/preload.cjs.js`。
- `dist/` 目录纳入 `.gitignore`（约定俗成，不需要单独说明）。
- `pnpm run dev` 与 `pnpm run smoke` 在启动 main 之前必须先跑 `tsc -p tsconfig.preload.json`。通过 npm script 串接：`"build:preload": "tsc -p tsconfig.preload.json"`，`"dev": "pnpm run build:preload && tsx src/main.ts"`。
- 已有 `pnpm run typecheck`（基于根 `tsconfig.json` `noEmit: true`）继续覆盖 main / preload / scripts 的全量类型检查；`build:preload` 只承担"emit JS"职责。

### 3. token 永远留 main（B1）

- `apps/desktop/src/main.ts` 的 `runtime: RuntimeContext` 仍是 module-scope let 持有的唯一 token / baseUrl 持有点。
- preload **不**接收 token 任何形式：既不通过 `additionalArguments` 也不通过 `contextBridge`。
- preload 暴露的所有方法都封装为「先发 IPC，再由 main 用持有的 token 发 HTTP」。renderer 进程内任意代码读 `window.agentos.*` 拿不到 token。
- ADR-025 §3 现有的 `additionalArguments: ["--agentos-base-url=...", "--agentos-token=..."]` 注入到 BrowserWindow 的设计**保持不变**——这是 renderer 进程的命令行参数，不进 DOM；本 ADR 不依赖也不读取这两个 args（继续保留是为了向 ADR-025 §3 兼容，未来若用 `process.argv` 解析它们由后续 ADR 显式开启）。

### 4. IPC 频道（H1）

main.ts 注册新 IPC handlers：

- `agentos:inbox:capture`，body `{ content: string, source?: string }`，返回 `{ ok: true, item: InboxItemSnapshot }` 或 `{ ok: false, status: number, problem: ProblemDetails }`。
- `agentos:inbox:list`，body `{ limit?: number, offset?: number }`，返回 `{ ok: true, page: InboxListPage }` 或 `{ ok: false, status: number, problem: ProblemDetails }`。

handler 实现职责：

1. 检查 `runtime` 是否就绪；未就绪抛 `Error("runtime not initialized")`（与现有 `agentos:runtime:get-status` 一致）。
2. `fetch` 到 `runtime.baseUrl + "/api/inbox"`，附带 `runtime.token` 头（`HEADER_NAME` 常量已从 `supervisor/spawn-backend.ts` 导出）。
3. 解析 response：`response.ok` ⇒ 返回 `{ ok: true, ... }`；否则解析 ProblemDetails 返回 `{ ok: false, status, problem }`。
4. 不在 main 层做业务校验（content 必填等）；让 .NET 端（`InboxAppService`）作为校验真值源，main 透传错误。

handler 不写日志（dogfood 阶段 main 已有 `console.log`/`console.error` 覆盖；额外的 IPC trace 留待可观测性 ADR）。

### 5. preload 桥扩展（E1 + H1）

`apps/desktop/src/preload.ts` 在现有 `runtime` 命名空间下新增 `inbox`：

```ts
const api = {
  runtime: { /* unchanged */ },
  inbox: {
    capture: (req: { content: string; source?: string }) =>
      ipcRenderer.invoke("agentos:inbox:capture", req) as Promise<InboxIpcResult<InboxItemSnapshot>>,
    list: (query: { limit?: number; offset?: number } = {}) =>
      ipcRenderer.invoke("agentos:inbox:list", query) as Promise<InboxIpcResult<InboxListPage>>,
  },
};
```

类型定义（`InboxItemSnapshot` / `InboxListPage` / `InboxIpcResult<T>`）在 preload.ts 内声明并 export 出 `AgentOsBridge` 供未来 renderer.ts 使用；V0 的 inline JS 不消费这些类型。

不对外暴露 `getStatus / getBaseUrl 之外的 runtime 方法`。

### 6. 错误显示（F1）

- capture 表单下方一条 `<div id="capture-error" hidden>`，失败时设置 `textContent` 与 `removeAttribute("hidden")`。
- list 区一条 `<div id="list-error" hidden>`，list 失败时显示，并保留旧的列表内容（不清空）。
- capture 失败时**保留 textarea 输入**；用户可二次提交。
- 错误文案直接来自 ProblemDetails 的 `title` 与 `detail`（已由 ADR-023 §4 钉死的 `ResultHttpExtensions.ToHttpResult` 生成），不在客户端做转译。

### 7. 写后刷新（G2）

- capture 成功后立刻调一次 `window.agentos.inbox.list({ limit: 50, offset: 0 })`，整列表重新渲染。
- 不做乐观本地 append。
- list 区独立的 Refresh 按钮也走同一条路径（`refreshList()`）。
- 不做轮询 / 自动刷新；ADR-025 现存的 `setInterval` 5s 刷新 runtime status 的做法**不复用到 inbox**——inbox 数据"新鲜度"由用户主动行为驱动，避免无写时持续打 API。

### 8. 启动链路（pnpm scripts）

`apps/desktop/package.json` 的 scripts：

```json
{
  "dev": "pnpm run build:preload && tsx src/main.ts",
  "smoke": "pnpm run build:preload && tsx scripts/smoke.ts",
  "build:preload": "tsc -p tsconfig.preload.json",
  "typecheck": "tsc --noEmit"
}
```

约束：
- `dev` 与 `smoke` 都先跑 `build:preload`，保证 preload.cjs.js 在 main 启动前已就位。
- `typecheck` 不依赖 emit；继续覆盖全量类型检查。
- `dist/` 不入版本库（`.gitignore`）。

### 9. 不在范围内

V0 渲染端**不**承载：

- inbox 的编辑（PUT）、删除（DELETE）、详情视图（GET by id）。
- 多窗口 / 多 tab / 路由。
- 标签 / 摘要 / 分类等读侧整理 UI（按 ADR-026 §背景 / 否决理由，这是读侧整理 agent 的产物）。
- chat 形态 UI（属于未来 chat ADR 范畴）。
- i18n、主题切换、字号控制。
- 通知 / system tray / 全局快捷键。
- 渲染端单元测试或 E2E 测试。

任何 §9 列出的能力进入 V1 范畴，须独立 ADR。

## 影响

涉及的代码与配置变更：

- 新增 `apps/desktop/tsconfig.preload.json`（独立 emit 配置）。
- 新增 `apps/desktop/dist/`（gitignored）输出目录。
- 修改 `apps/desktop/package.json` scripts。
- 修改 `apps/desktop/.gitignore` 加入 `dist/`（如未存在）。
- 修改 `apps/desktop/src/main.ts`：
  - preload 路径改为 `dist/preload.cjs.js`。
  - 新增 `agentos:inbox:capture` 与 `agentos:inbox:list` 两个 IPC handler。
- 修改 `apps/desktop/src/preload.ts`：
  - 在 `api` 对象上新增 `inbox: { capture, list }`。
  - 新增 `InboxItemSnapshot` / `InboxListPage` / `InboxIpcResult<T>` 类型声明。
- 重写 `apps/desktop/src/renderer/index.html` 为 §1 形态。

不涉及的代码与配置：

- ADR-026 落地的 6 层（Domain / Application / Infrastructure / Api）一个字节都不动。
- ADR-025 的 spawn-backend / port-handshake / readiness-probe / shutdown 一个字节都不动。
- `additionalArguments` 注入 token 到 BrowserWindow 的现有行为保持（renderer 不消费它，但 ADR-025 §3 的契约保留）。
- smoke 探针 (`scripts/smoke.ts`) 形态保持不变；本 ADR 不要求 smoke 调 IPC（IPC 在 Electron 进程内、smoke 是独立 node 进程，无法直接调）。

破坏性变更：

- 维持 ADR-025 §5 的 .cjs.js 路径不变意味着接受"dev 路径下 preload 永远不工作"的现状。本 ADR **不**通过新文件名废弃旧契约——它正是来关闭那条 NOTE 的。无人在依赖那个 broken state，因此非破坏性。
- pnpm scripts 的 `dev` 行为变化（多一步 tsc emit）；调用方（人 / CI）需重新跑一次 install 后 `dev` 即可，无 API 变化。

ADR-022 §10 的 IDomainEventDispatcher 待办**不在本 ADR 关闭**——本 ADR 不引入任何 domain event handler，inbox capture 事件仍按 ADR-026 §3 + ADR-022 §10 现状（聚合 raise + AppService clear，无 dispatcher）。

## 复议触发条件

- 渲染端 LOC 超过 ~400 行（达到框架收益门槛）。
- 出现第二个独立路由 / 视图（inbox 之外的视图，例如 settings / chat / read-side review）。
- 出现 chat 流式 UI 需求。
- 出现编辑 / 删除 / 详情面板需求。
- 引入第二个进程窗口或第二个 BrowserWindow。
- 出现移动端 / web 端复用渲染层的需求。
- 渲染端代码出现需要类型推断的独立模块（不再适合 inline JS 形态）。
- Electron 在 V≥34 版本支持原生 ESM preload 取代 CJS 预编译，并稳定通过 contextIsolation + sandbox 的组合验证。
- token 出现新的非 IPC 出口（如 OAuth 跳转、外部链接 deeplink、IPC 之外的 stdio 通道）。
- 渲染端出现可复现的 UI bug 且无法通过 inline JS + 人眼验收捕获（触发 J 轴升级）。

## 相关页面

- [ADR-025 桌面进程监督：Electron 主进程拉起 .NET Api 子进程的拓扑、端口与启动 token](desktop-process-supervisor-electron-dotnet-child.md)：本 ADR 关闭其 §5 关于 preload `.cjs.js` 的 NOTE。
- [ADR-026 Inbox V0 数据契约与捕获面：聚合形态、表结构、UUIDv7 主键与列表分页](inbox-v0-capture-and-list-contract.md)：本 ADR 是其在 UI 层的对应面，闭合 dogfood 端到端环路。
- [ADR-023 Api 入口立面：AppService 接入与 V0 端点形态](api-entry-facade-and-v0-endpoints.md)：本 ADR §4 / §6 错误透传依赖其 ProblemDetails 形态。
- [ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端](mvp-desktop-stack-electron-aspnetcore.md)：本 ADR 在 Electron 33 + contextIsolation + sandbox 的既有栈上落地。
- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](engineering-skeleton-v0.md)：本 ADR 是其桌面壳侧 V0 收尾。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：本 ADR 由该 rule 触发。
