---
title: ADR-025 桌面进程监督：Electron 主进程拉起 .NET Api 子进程的拓扑、端口与启动 token
type: adr
subtype: architecture
canonical: true
summary: 桌面壳采用 Electron main 同步 spawn `dotnet` 子进程作为本地 Api host；子进程绑 127.0.0.1:0 随机端口并通过 stdout 回报实际端口；启动 token 由主进程生成 GUID 并通过 env 注入子进程；主进程 HTTP 轮询 /api/runtime/status 直到 Healthy && Database.Ready；窗口关闭时 SIGTERM dotnet 并保留 10s SIGKILL 兜底；前端 UI 框架（Vite/Vue/Arco）推迟到下一个 ADR。
tags: [agent, engineering]
sources: []
created: 2026-05-02
updated: 2026-05-02
verified_at: 2026-05-02
freshness: volatile
status: active
archived_reason: ""
supersedes: []
related: [pages/adrs/mvp-desktop-stack-electron-aspnetcore.md, pages/adrs/engineering-skeleton-v0.md, pages/adrs/api-entry-facade-and-v0-endpoints.md, pages/adrs/sqlite-dapper-bootstrap-and-schema-init.md, pages/rules/plan-first-implementation.md]
part_of: [pages/hubs/agent-os.md]
adr_status: accepted
adr_date: 2026-05-02
adr_revisit_when: "Vite/Vue/Arco 前端栈进入项目（需要决定 dev server vs file:// 加载方式）；或自动更新 / 安装包 / electron-builder 进入项目（需要决定 dotnet 子进程发布形态从 dotnet run 切到 self-contained publish）；或多窗口 / 托盘常驻形态出现（关闭语义需要从 G1 改为后台常驻）；或子进程通信形态从 HTTP 切到 named pipe / Unix domain socket；或第二个本地子进程出现（如独立 LLM proxy / 向量数据库进程，需要决定多子进程编排）；或 Electron / Node / .NET 主版本不再兼容当前 spawn API。"
---

# ADR-025 桌面进程监督：Electron 主进程拉起 .NET Api 子进程的拓扑、端口与启动 token

> 桌面壳采用 Electron main 同步 spawn `dotnet` 子进程作为本地 Api host；子进程绑 127.0.0.1:0 随机端口并通过 stdout 回报实际端口；启动 token 由主进程生成 GUID 并通过 env 注入子进程；主进程 HTTP 轮询 /api/runtime/status 直到 Healthy && Database.Ready；窗口关闭时 SIGTERM dotnet 并保留 10s SIGKILL 兜底；前端 UI 框架（Vite/Vue/Arco）推迟到下一个 ADR。

## 背景

[ADR-016](mvp-desktop-stack-electron-aspnetcore.md) 已确定桌面壳 = Electron + ASP.NET Core 本地后端，并明确"第一版通信采用 localhost + 随机端口 + 启动 token"。[ADR-017](engineering-skeleton-v0.md) 把"桌面壳能启动、后端能启动、二者能用受控 token 通信"列为 V0 通电的最后一道闸门，复议触发条件第一句就是"V0 骨架通电后"。[ADR-023](api-entry-facade-and-v0-endpoints.md) 已经把 `StartupTokenMiddleware` 与 `/api/runtime/status` endpoint 做完。[ADR-024](sqlite-dapper-bootstrap-and-schema-init.md) 已经把 schema bootstrap 与 `RuntimeStatus.Database` 做完，并且在 Api 集成测试中证明了 `Database.Ready=true && SchemaVersion>=1`。

但截至 2026-05-02，仓库里还没有 `apps/desktop/`，没有任何 Electron 代码，也没有任何把 Electron main 与 `dotnet` 子进程串起来的代码。ADR-016 / ADR-017 决定了**要做**，没有决定**怎么做**。落地前必须把以下工程问题钉死：

- 进程拓扑：Electron main 直接 spawn `dotnet` 子进程，还是子进程作为系统服务 / Photino in-process？
- 端口策略：固定端口、随机端口 + stdout 回报、还是 Unix socket？
- Token 协商：env 注入、文件交换、还是固定 dev token？
- 后端发布形态：dev 用 `dotnet run` / prod 用 `dotnet publish` 产物 / 全程 self-contained publish？
- 健康探活：HTTP 轮询 `/api/runtime/status`、stdout "READY" 信号、还是双信号？
- UI 表达：内联 `data:text/html`、单 `index.html` + preload、还是直接拉 Vite/Vue/Arco？
- 关闭语义：关窗即 quit、后台常驻 + 托盘、还是用户显式 quit？
- 烟雾测试形态：纯 Node 脚本、Electron `--smoke` flag、还是仅人工？
- 目录结构：`apps/desktop/` 单包、`apps/shell` + `apps/web` workspace、还是 `scripts/desktop-supervisor/`？
- 包管理器：pnpm workspace + 根 `package.json` / 单 `apps/desktop/package.json` 不开 workspace / npm？
- Electron 依赖等级：仅 electron + typescript + tsx + cross-fetch / + electron-builder（打包）/ + concurrently 等开发器糖？

如果让 S5b 落地代码"边写边定"，会重蹈 ADR-021 → ADR-022 一周内就被推翻的覆辙：进程拓扑、端口、token 这三件事是后续每一个 desktop / IPC / 自动更新工作都要继承的硬决定。本 ADR 在 ADR-016 / ADR-017 / ADR-023 / ADR-024 既有契约之上把桌面进程监督形态钉死，作为 S5b 实施的依据；按 [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)，代码落地仍须在本 ADR 接受后才进行。

本 ADR 的职责边界：

- 在范围内：进程拓扑、端口与 token 协商、健康探活、关闭语义、目录结构、包管理器、Electron 依赖等级、烟雾测试形态。
- 不在范围内：Vite/Vue/Arco 前端栈选型与目录、IPC bridge / preload 安全模型的细节超出"暴露 status 读取"之外的部分、自动更新、安装包、托盘 / 多窗口 / 快捷键、第二个子进程（独立 LLM proxy / 向量数据库）。这些进入后续 ADR。

## 备选方案

进程拓扑（A 轴）：

- **A1** Electron main 同步 spawn `dotnet ...dll` 作为子进程；子进程生命周期与 Electron 主进程绑定。
- **A2** 后端作为系统级服务（`launchctl` / `systemd` / Windows Service），Electron 作纯客户端。
- **A3** Photino / WebView2 in-process：Api host 与 UI 进程合一。

端口策略（B 轴）：

- **B1** 子进程绑 `http://127.0.0.1:0`（OS 分配随机端口）；启动后通过 stdout 单行 JSON `{"event":"listening","url":"http://127.0.0.1:NNNN"}` 回报实际端口给主进程。
- **B2** 固定端口（如 5050）；冲突时启动失败。
- **B3** Unix domain socket（macOS/Linux）/ named pipe（Windows）。

Token 协商（C 轴）：

- **C1** 主进程启动时 `crypto.randomUUID()` 生成 token；通过 env `DAWNING_STARTUP_TOKEN` 注入 spawn 的子进程；浏览器侧通过 preload 暴露给 renderer，renderer 在每次 fetch 时加 `X-Startup-Token`。
- **C2** 主进程把 token 写到一个临时文件（仅当前用户可读）；子进程启动时读该文件；后续传递路径同 C1。
- **C3** 固定 dev token（如 `"local-dev-token"`）；只在开发期生效。

后端发布形态（D 轴）：

- **D1** 开发期 spawn `dotnet run --project src/Dawning.AgentOS.Api`；生产期 spawn 已经 `dotnet publish` 出来的 self-contained 可执行（`Dawning.AgentOS.Api` 或对应平台 binary）。运行模式由 env `DAWNING_DESKTOP_MODE=dev|prod` 切换。
- **D2** 全程 self-contained publish；开发期也走 publish 产物。
- **D3** S5b 暂只支持 dev 模式；publish / packaging 推迟到打包 ADR。

健康探活（E 轴）：

- **E1** 主进程 HTTP 轮询 `/api/runtime/status`（带 token），间隔 250ms，超时 30s；条件 `200 OK && Healthy === true && Database.Ready === true`。
- **E2** 子进程在 schema bootstrap + Kestrel listening 后通过 stdout 输出 `{"event":"ready"}` 信号，主进程读 stdin 信号。
- **E3** 双信号：先等 stdout "listening" 拿端口，再 HTTP 轮询拿 `Database.Ready`。

UI 表达（F 轴）：

- **F1** Electron `BrowserWindow.loadURL('data:text/html,...')`，HTML 直接 hardcode 渲染逻辑。
- **F2** 一个静态 `apps/desktop/src/renderer/index.html` + 一个 `apps/desktop/src/preload.ts` 暴露 `agentos.runtime.{getStatus, port, token}` 给 renderer；renderer 内联一段 JS 调 fetch 并 DOM 渲染 status。
- **F3** 直接拉 Vite + Vue + Arco，写一个 `RuntimeStatusView.vue` 组件。

关闭语义（G 轴）：

- **G1** 关窗 → 主进程 SIGTERM dotnet 子进程 → 等 10s → 仍存活则 SIGKILL → 主进程 quit。
- **G2** 关窗后后台常驻 + 托盘图标。
- **G3** 用户显式选 "Quit" 才退出，关窗只隐藏。

目录结构（H 轴）：

- **H1** `apps/desktop/` 单包，main / preload / renderer / scripts/smoke 都在同一 package。
- **H2** `apps/desktop/`（Electron main + preload）+ `apps/web/`（renderer）双 package + workspace。
- **H3** `scripts/desktop-supervisor/`（不进 apps/）放 Node 监督脚本，Electron 推迟。

包管理器（I 轴）：

- **I1** pnpm workspace + 根 `package.json` + `pnpm-workspace.yaml`。
- **I2** 单 `apps/desktop/package.json` + `pnpm-lock.yaml`，不开 root workspace。
- **I3** npm。

Electron 依赖等级（J 轴）：

- **J1** 仅 `electron` + `typescript` + `tsx`（开发执行器）+ `@types/node`。
- **J2** + `electron-builder`（自动更新 / 安装包）。
- **J3** + `concurrently` + `wait-on` 等开发流糖。

烟雾测试形态（K 轴）：

- **K1** 纯 Node 脚本 `apps/desktop/scripts/smoke.ts` spawn dotnet + HTTP probe + 退码（无 Electron 头，CI 友好；可手动跑 `pnpm run dev` 看真实 Electron 路径）。
- **K2** Electron 启动接收 `--smoke` flag 自动跑 status probe 后 quit。
- **K3** 仅人工跑 Electron 看窗口。

## 被否决方案与理由

**A2 系统服务**：

- 安装期需要请求系统级权限（macOS authorization、Windows UAC）；与 [PURPOSE.md](../../PURPOSE.md) `mvp_input_boundary`（不默认越权读取用户文件夹）的精神冲突——一个本地 dogfood 工具不应该需要 root。
- 卸载清理路径复杂；用户卸载 Electron 时容易残留 daemon。
- 启动顺序变复杂：Electron 启动时 daemon 可能未就绪，需要额外的握手协议。

**A3 Photino / in-process**：

- ADR-016 已明确选 Electron + ASP.NET Core 双运行时，不是 in-process；A3 在本仓库范围内已被 ADR-016 否决。
- in-process 模型让"后端独立可测、可分离 dogfood、未来可作为 framework 抽出"这条线消失。

**B2 固定端口**：

- 5050 这种数字在用户机上几乎一定撞到其它工具（开发期 dotnet 默认 50xx、Vue dev server 8080、postgres 5432、…），首次启动失败用户体验最差。
- 如果改成"撞了就换下一个"的递增策略，本质上还是 B1 的随机分配。

**B3 Unix socket / named pipe**：

- 跨平台抽象层比 HTTP loopback 厚一档；Electron 主进程要选用 Node 的 `net` 模块包 HTTP，renderer 侧的 `fetch` 不能直接用 socket URL。
- 第一版的安全模型由"localhost + token"承担；socket 的隔离收益仅在多用户机上明显，本产品 dogfood 阶段是单用户。
- ADR-016 复议触发条件已写明"如后续安全或平台限制要求更强隔离，再评估 named pipe / Unix domain socket"——本 ADR 直接遵守。

**C2 token 文件**：

- 多了一个临时文件的清理责任与权限校验；在 macOS / Windows 上"仅当前用户可读"的实现需要平台特定 chmod 0600 / ACL，复杂度高于收益。
- env 注入在 `child_process.spawn` 上是一等公民，不需要额外抽象。

**C3 固定 dev token**：

- 仅在 dev 期能用；生产构建必须切换到随机 token，等于要写两套；不如一开始就走 C1。
- 如果 dev token 不慎在 commit 里硬编码，会让 [ADR-016](mvp-desktop-stack-electron-aspnetcore.md) 中"启动 token 是第一版安全边界"这条假设失效。

**D2 全程 self-contained publish**：

- 开发期每改一行 C# 都要先 `dotnet publish`，反馈循环慢一档。
- self-contained 二进制每个平台 ~80MB；本地 dev 不需要这个体积。

**D3 S5b 暂只支持 dev**：

- 与 D1 的差别只是"开发模式"先行落地；prod 模式形态仍然要在本 ADR 钉死，否则后续打包 ADR 还要回头改 spawn 路径。
- D1 把 dev / prod 两条 spawn 路径都写明，将来打包 ADR 只需要决定"如何产生 publish 产物"，不需要回改 supervisor。

**E2 stdout READY 信号**：

- 信号语义不可观察：renderer 端拿不到这条信号，无法在 UI 上显示"连接中…"进度。
- HTTP 轮询自然附带"已经能 200 OK && 已经能查到 schemaVersion"两层确认；stdout 信号要重新定义这两层语义。
- stdout 信号在 Windows + .NET 控制台缓冲下偶尔丢首行，已知坑。

**E3 双信号**：

- 引入两套握手代码（stdout 解析 + HTTP 轮询），两条都可能失败，错误路径复杂度翻倍。
- B1 已经决定 stdout 回报端口，E3 等于让 stdout 既报端口又报 ready；两个事件混在一条 stream 里需要额外的格式协议。E1 把"ready"语义直接交给 HTTP 探针，stdout 只承担端口回报这一件事。

**F1 内联 data: URL**：

- HTML 内联在 main 文件里会让 renderer 脚本的调试体验消失（Electron DevTools 对 data: URL 支持差）。
- 当我们将来要换到真实的 Vite/Vue/Arco 时，从 data: URL 迁出去比从一个静态 `index.html` 迁出去多一步"先把 HTML 拆出来"。

**F3 直接拉 Vite/Vue/Arco**：

- 撕裂面太大：S5b 的目的是"通电"，不是"前端栈选型"。把 Vite + Vue + Arco + 路由 + 组件库 + i18n 一次性引入，commit 的 diff 会被前端模板淹没，将来回看"electron 怎么跟 .NET 通电的"很难读。
- 前端栈值得单独一个 ADR + 单独一个 commit；本 ADR 复议触发条件第一项就是 "Vite/Vue/Arco 前端栈进入项目"。

**G2 后台常驻 + 托盘**：

- 托盘交互引入"主窗口关闭 ≠ App 退出"的心智模型，需要单独决定"完全退出"入口、菜单项、生命周期信号、自动启动设置等多件事。
- MVP dogfood 阶段不需要常驻；用户需要时再开窗口即可。
- ADR 复议触发条件已写入"多窗口 / 托盘常驻形态出现"。

**G3 关窗只隐藏**：

- macOS 的 dock 标准行为是"关窗不退出"，但本产品的"关窗即退出"对 dogfood 更直观（避免用户以为已经退出而后端仍在跑）；macOS 行为差异留待第二阶段再统一。
- 与 G2 的代价相同。

**H2 双 package + workspace**：

- 收益是"将来 web 渲染端可以独立部署"，但本产品定位是桌面应用，不打算做 web 端独立部署；workspace 复杂度无对应收益。
- pnpm workspace 引入需要 root `pnpm-workspace.yaml` + 跨包依赖解析；S5b 不需要这层。

**H3 `scripts/desktop-supervisor/`**：

- 把 Electron 拖到下一个 ADR 才出现，意味着 ADR-016 决定的 Electron 路径仍然没有真实活线，等于把"通电"的目标向后推一周。
- 监督逻辑写在脚本里再写一次到 Electron main 里，等于两次实现两次决策。

**I1 pnpm workspace**：

- 仓库中目前只有一个前端 package（apps/desktop），没有第二个；workspace 是为了"多 package 共享 lockfile"，零收益。
- root `package.json` 会污染仓库根，将来若要去除 workspace 反而要清理。
- 复议触发条件已留出"第二个本地 Node 进程出现"——届时再开 workspace。

**I3 npm**：

- 与 [PURPOSE.md](../../PURPOSE.md) `mvp_desktop_stack` 中"Vite + TypeScript"配套的事实标准是 pnpm；用 npm 会在 install 速度、磁盘占用、lockfile 稳定性上劣于 pnpm。

**J2 electron-builder**：

- 打包属于 ADR 复议触发条件之一（"自动更新 / 安装包 / electron-builder 进入项目"）；S5b 不打包，无需引入。
- electron-builder 会拉一大堆 native 构建依赖（fpm / dmg / rpm 等），install 时间显著拉长。

**J3 concurrently / wait-on**：

- 这些是"同时跑前端 dev server 与后端"的开发糖；本 ADR 没有前端 dev server（F2），没有"两个 watch 并行"的需要。

**K2 Electron --smoke flag**：

- Electron 进程启动需要 X server（Linux CI）或 macOS 头（macOS CI），CI 跑会麻烦；K1 的纯 Node 脚本可以在任意 runner 上裸跑。
- 真实 Electron 路径仍然存在（`pnpm run dev` 手动跑），不会丢失验证面。

**K3 仅人工**：

- 没有自动化烟雾，每次 ADR-024 / ADR-023 / Application 改动都要人工跑一遍；漂移成本高。

## 决策

采用：A1 + B1 + C1 + D1 + E1 + F2 + G1 + H1 + I2 + J1 + K1。

### 1. 进程拓扑

Electron main 进程在 `app.whenReady()` 内同步 spawn 一个 `dotnet` 子进程作为本地 Api host。子进程是 Electron 主进程的 child；Electron 退出时主进程负责按 §6 关闭子进程。

```text
Electron main  ──spawn──▶  dotnet (Dawning.AgentOS.Api)
       │                       │
       │  HTTP /api/runtime    │  Kestrel on 127.0.0.1:NNNN
       │ ◀─────  status  ──────┤
       │                       │
       │   stdout "listening"  │
       │ ◀─────────────────────┤
```

ASP.NET Core 端不感知 Electron 的存在；它只是一个普通的本地 Api host，复用 [ADR-023](api-entry-facade-and-v0-endpoints.md) 的 `StartupTokenMiddleware` 与 `/api/runtime/status` 端点，以及 [ADR-024](sqlite-dapper-bootstrap-and-schema-init.md) 的 `SchemaInitializerHostedService`。

### 2. 目录结构

```text
apps/desktop/
├── package.json                  # pnpm，单包，"type": "module"
├── tsconfig.json
├── .gitignore                    # node_modules / dist
├── README.md                     # 本地启动 / smoke 跑法（不复述本 ADR）
├── src/
│   ├── main.ts                   # Electron main 入口；spawn / token / 端口握手 / 关闭
│   ├── preload.ts                # 暴露 window.agentos.runtime.{port, token, getStatus}
│   ├── supervisor/
│   │   ├── spawn-backend.ts      # 跨平台决定 dotnet 命令 / 工作目录 / env
│   │   ├── port-handshake.ts     # 解析 stdout "listening" JSON 行
│   │   ├── readiness-probe.ts    # HTTP 轮询 /api/runtime/status
│   │   └── shutdown.ts           # SIGTERM + 10s SIGKILL
│   └── renderer/
│       └── index.html            # 静态页面；fetch /api/runtime/status 后渲染
└── scripts/
    └── smoke.ts                  # 不依赖 Electron 的烟雾脚本（CI 友好）
```

`apps/desktop/` 是 S5b 引入的第一个非 .NET 包；目录结构与 [ADR-016](mvp-desktop-stack-electron-aspnetcore.md) "工程目录建议"一致（`apps/desktop/` 单包，与 `src/Dawning.AgentOS.Api` 平级）。

### 3. 端口与 token 协商

启动序列（Electron main 视角）：

```text
1. const token = crypto.randomUUID()
2. spawn 'dotnet':
     args = ['run', '--project', '<repo>/src/Dawning.AgentOS.Api', '--no-launch-profile']
     env  = {
       ...process.env,
       Api__StartupToken__HeaderName: 'X-Startup-Token',
       Api__StartupToken__ExpectedToken: token,
       ASPNETCORE_URLS: 'http://127.0.0.1:0',          // OS 分配随机端口
       ASPNETCORE_ENVIRONMENT: 'Development',
       DOTNET_NOLOGO: 'true',
     }
3. 在 child.stdout 上行扫描，匹配第一条 "Now listening on: http://127.0.0.1:NNNN"
   → 解析出端口 NNNN，记录 baseUrl = http://127.0.0.1:NNNN
4. 进入 §4 健康探活
```

ASP.NET Core 默认会在 stdout 输出形如 `info: Microsoft.Hosting.Lifetime[14] Now listening on: http://127.0.0.1:51234` 的日志行；本 ADR 复用这条行作为端口握手信号，不另外要求 .NET 侧改日志格式。

token 通过 `Api__StartupToken__ExpectedToken` env 注入，Api 侧由 [ADR-023](api-entry-facade-and-v0-endpoints.md) 已实现的 `StartupTokenOptions` 自动绑定；本 ADR 不要求 .NET 侧改任何代码。

renderer 拿 token 与端口的方式：preload 在 `window.agentos.runtime` 上暴露三个 API：

```ts
// apps/desktop/src/preload.ts
contextBridge.exposeInMainWorld('agentos', {
  runtime: {
    port: () => process.env.AGENTOS_DESKTOP_PORT,        // main → preload via env
    token: () => process.env.AGENTOS_DESKTOP_TOKEN,
    getStatus: async () => {
      const port = process.env.AGENTOS_DESKTOP_PORT;
      const token = process.env.AGENTOS_DESKTOP_TOKEN;
      const res = await fetch(`http://127.0.0.1:${port}/api/runtime/status`, {
        headers: { 'X-Startup-Token': token! }
      });
      return await res.json();
    },
  },
});
```

main 在 `BrowserWindow` 创建前用 `app.commandLine.appendSwitch` / `additionalArguments` 把 token / port 透传给 preload 进程的 env；具体透传机制由实现层决定。

### 4. 健康探活

主进程在拿到端口后立即开始轮询：

```text
loop:
  GET http://127.0.0.1:NNNN/api/runtime/status
  headers: X-Startup-Token = <token>
  expected:
    HTTP 200 OK
    body.healthy === true
    body.database.ready === true
    body.database.schemaVersion >= 1
  interval: 250ms
  total timeout: 30s
on success:
  开窗 BrowserWindow，加载 index.html
on timeout:
  打印诊断日志（最近 10 条 dotnet stdout / stderr）
  关闭子进程并 quit Electron，进程退码 1
```

主进程不在 ready 之前打开 BrowserWindow，避免 renderer 看到空白后端。

### 5. UI 表达（F2）

第一版只有一个静态 `apps/desktop/src/renderer/index.html`，里面内联 ~30 行 JS：

```html
<body>
  <pre id="status">loading…</pre>
  <script>
    const refresh = async () => {
      const status = await window.agentos.runtime.getStatus();
      document.getElementById('status').textContent = JSON.stringify(status, null, 2);
    };
    refresh();
    setInterval(refresh, 5000);
  </script>
</body>
```

这一版"UI"只承担一件事：让人类肉眼看到 `database.ready=true && schemaVersion=1`。它**不是**最终前端形态；Vite/Vue/Arco 进入项目时本 ADR 复议（见 `adr_revisit_when` 第 1 条）。

### 6. 关闭语义

- Electron `app` 监听 `'window-all-closed'`；触发时调用 `shutdown(child)` 后 `app.quit()`。
- `shutdown(child)`：① `child.kill('SIGTERM')`；② 等 10000ms；③ 子进程仍存活则 `child.kill('SIGKILL')`；④ 不论结果都让 main 进程 quit，主进程不会因为子进程不死而卡住。
- macOS 的"关窗不退出 dock 仍在"行为本版本**不**特殊处理（§G3 否决理由）。
- main 进程崩溃路径：Node 的 `process.on('exit')` 也调一次 `child.kill('SIGTERM')`，避免 Electron crash 时留下孤儿子进程。

### 7. 烟雾测试（K1）

`apps/desktop/scripts/smoke.ts` 是一个**不依赖 Electron 头**的纯 Node TS 脚本。它执行：

```text
1. spawn dotnet（参数与 §3 一致）
2. 解析 stdout 拿端口（与 §3 一致）
3. 轮询 /api/runtime/status（与 §4 一致）
4. 断言 healthy=true && database.ready=true && database.schemaVersion>=1
5. SIGTERM 子进程
6. 退码 0（成功）/ 1（失败）
```

执行方式：`pnpm --dir apps/desktop run smoke`。

CI 在没有 X server 的 Linux runner 上也能跑；不需要 Xvfb / electron mocha。Electron 真实路径仍存在（`pnpm --dir apps/desktop run dev`），可以手动跑看真实窗口。

### 8. package.json

`apps/desktop/package.json` 形态：

```jsonc
{
  "name": "@dawning/desktop",
  "private": true,
  "version": "0.0.1",
  "type": "module",
  "scripts": {
    "dev": "tsx src/main.ts",      // 直接用 tsx 跑 TS，无需先 build
    "smoke": "tsx scripts/smoke.ts"
  },
  "devDependencies": {
    "electron": "^33",
    "typescript": "~5.6",
    "tsx": "^4",
    "@types/node": "^22"
  }
}
```

不引入 `electron-builder` / `vite` / `vue` / `concurrently` / `wait-on` / `cross-fetch`（Node 22 已内建 `fetch`）。

### 9. 不在本 ADR 范围内

- Vite + Vue + Arco 前端栈、IPC bridge 安全模型超过 §3 暴露 status 之外的部分、自动更新 / 安装包 / electron-builder、托盘 / 多窗口、第二个本地子进程的编排、生产期 self-contained publish 的产物布局：均在复议触发条件中明确指向后续 ADR。

## 影响

**正向影响**：

- ADR-016 决定的 Electron + ASP.NET Core 双运行时**首次有真实活线**；ADR-017 V0 通电的最后一道闸门关闭。
- 进程拓扑、端口、token 三件事一次钉死，后续每个 desktop / IPC / 自动更新工作都站在这个 ADR 上。
- `apps/desktop/scripts/smoke.ts` 是一条**不依赖 Electron 头**的烟雾门禁，未来 ADR-023 / ADR-024 / Application 改动后只需 `pnpm run smoke` 即可验证桌面侧仍通。
- 决策与代码 1:1 对齐：每一条决策都对应 §1–§8 的一段实现；下个人 / 下个 LLM 看到这个 ADR 直接能照写。
- Electron 依赖等级压到最低（仅 4 个 devDeps），install 速度与磁盘占用都在可控范围。
- 关闭语义 G1（关窗即 quit）配合 SIGTERM + 10s SIGKILL 兜底，不会留孤儿子进程；dogfood 阶段心智简单。

**代价 / 风险**：

- 端口握手依赖 ASP.NET Core 默认 stdout 日志行格式（`Now listening on: http://...`）；如果未来 .NET 主版本改了这条日志（极低概率，但发生过），supervisor 需要适配。备选：让 Api 侧自己输出一条 `{"event":"listening",...}` JSON 行，本 ADR 暂不引入。
- env 注入 token 在多用户机的同一进程树内可被同用户其它进程通过 `/proc/<pid>/environ` 读到；本产品 dogfood 阶段是单用户场景，可接受。复议触发条件已写明"通信形态从 HTTP 切到 named pipe / Unix domain socket"。
- `dotnet run` 启动比 self-contained publish 慢（~3s vs ~500ms）；S5b 的 30s 探活超时足够覆盖。打包 ADR 进入项目时 D1 的 prod 分支会切到 publish 产物。
- F2 的 UI 不是产品形态，是脚手架；如果有人把它当成"已经做了 UI"会误解项目进度。本 ADR §5 已经显式说明这是脚手架。
- pnpm 单包不开 workspace，将来引入第二个前端 package 时需要把 root `pnpm-workspace.yaml` 补上；这是一次性的迁移，不阻塞当前。
- macOS dock 行为差异在 G1 下被忽略；macOS 用户关窗后 App 退出（不像 macOS 标准行为）。复议触发条件已留口"多窗口 / 托盘常驻形态出现"。

## 复议触发条件

`adr_revisit_when` 已写入 front matter（见 SCHEMA §4.3.2 / §6.0），本节不重复。

## 相关页面

- [PURPOSE.md](../../PURPOSE.md)：`mvp_desktop_stack` 产品契约。
- [SCHEMA.md](../../SCHEMA.md)：本 ADR 的结构契约。
- [ADR-016 MVP 桌面技术栈：Electron + ASP.NET Core 本地后端](mvp-desktop-stack-electron-aspnetcore.md)：定义双运行时与 localhost + 随机端口 + 启动 token 边界。
- [ADR-017 工程骨架 V0：桌面壳 + DDD 本地后端通电](engineering-skeleton-v0.md)：本 ADR 是 V0 通电最后一道闸门的实现规约。
- [ADR-023 Api 入口立面：AppService 接入与 V0 端点形态](api-entry-facade-and-v0-endpoints.md)：本 ADR 复用 `StartupTokenMiddleware` 与 `/api/runtime/status`。
- [ADR-024 SQLite/Dapper 通电](sqlite-dapper-bootstrap-and-schema-init.md)：本 ADR 复用 `RuntimeStatus.Database.Ready / SchemaVersion` 作为探活成功条件。
- [Rule 实现前必须方案先行](../rules/plan-first-implementation.md)：定义实现前先方案、后确认、再执行。
- [pages/hubs/agent-os.md](../hubs/agent-os.md)：root hub。
