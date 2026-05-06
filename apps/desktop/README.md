# @dawning/desktop

Dawning Agent OS 桌面壳。Electron 主进程负责拉起 `dotnet` 子进程作为本地 Api host，等其就绪后开窗。

> 决策与边界见 [`docs/pages/adrs/desktop-process-supervisor-electron-dotnet-child.md`](../../docs/pages/adrs/desktop-process-supervisor-electron-dotnet-child.md)（ADR-025）。本文件只做"怎么跑"。

## 前置

- macOS 或 Linux（Windows 路径未在 S5b 验证）。
- Node 22+ 与 pnpm 10+。
- .NET 10 SDK，`dotnet` 在 PATH 中。

## 烟雾验证（推荐先跑这个）

```bash
cd apps/desktop
pnpm install
pnpm run smoke
```

`smoke` 是不依赖 Electron 头的纯 Node 脚本：

1. spawn `dotnet run` 起 Api host；
2. 解析 stdout 的 `Now listening on:` 行拿端口；
3. 带 `X-Startup-Token` 轮询 `/api/runtime/status`；
4. 断言 `healthy=true && database.ready=true && database.schemaVersion>=1`；
5. SIGTERM 子进程并按断言结果退码。

成功输出形如：

```text
[smoke] spawned dotnet (pid=...)
[smoke] api listening on http://127.0.0.1:51234
[smoke] status ready (schemaVersion=1)
[smoke] PASS
```

## 真实 Electron 路径（手动）

```bash
cd apps/desktop
pnpm install
pnpm run dev
```

会启动 Electron 主进程，spawn dotnet，等就绪后开一个窗口，渲染 `/api/runtime/status` 的 JSON。关窗即退出。

## 类型检查

```bash
pnpm run typecheck
```

## 不在范围内

- 真实前端栈（Vite/Vue/Arco）：见 ADR-025 §9，等下一个 ADR。
- 安装包 / 自动更新（electron-builder）：见 ADR-025 复议触发条件第 2 条。
- Windows 行为：未在 S5b 阶段验证；ADR-025 §3 / §6 已经声明意图，但 CI / 手测都跑在 macOS。
