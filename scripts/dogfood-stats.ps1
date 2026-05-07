#!/usr/bin/env pwsh
# dogfood-stats.ps1 — ad-hoc dogfood telemetry dump for ADR-035.
#
# 不属于任何 ADR；不在产品代码 / 测试范围；仅服务于 ADR-035 §D2 的
# dogfood 量化扫描。dogfood 收敛 / ADR-035 被 supersede 后此脚本可删。
#
# What it dumps (ADR-035 §D2 中能从 SQLite 直接拿到的部分):
#
#   1. inbox 真实捕获数（总 / 今日 / 最近 7 天 / 按天）
#   2. 📒 Save 命中数（= memory_entries WHERE source = 3 InboxAction，ADR-034 路径）
#      Memory 视图直接创建数（= memory_entries WHERE source = 1 UserExplicit）
#   3. memory 状态分布（status 1/2/3/4，按 ADR-033 §决策 G1）
#   4. chat 活跃度（chat_sessions / chat_messages 数）
#
# What it CANNOT dump (V0 不持久化的字段，需手记到 docs/raw/meetings/):
#   - Summarize / Tags 按钮点击次数（ADR-030 / ADR-031 V0 不落库）
#   - Memory 视图打开次数 / 每次停留时长（前端事件，无后端落点）
#   - 「想 save 但没按」「不知道按哪个」「捕获后忘了处理」（反例样本，主观）
#   - 「新任务时 agent 真的引用了 Memory 中某条历史」（PURPOSE.md MVP 第一信号正例样本，主观）
#
# Usage:
#   pwsh ./scripts/dogfood-stats.ps1
#   pwsh ./scripts/dogfood-stats.ps1 -DbPath "C:\custom\path\agentos.db"
#
# Default DB path on Windows comes from
# Dawning.AgentOS.Infrastructure.Hosting.AppDataPathProvider:
#   %LOCALAPPDATA%\dawning-agent-os\agentos.db
#
# 实现选择：使用仓库内已 build 出的 Microsoft.Data.Sqlite.dll
# (Api bin 输出)，无需额外安装 sqlite3 CLI。前置：跑过 `dotnet build`。

[CmdletBinding()]
param(
    [string] $DbPath = (Join-Path $env:LOCALAPPDATA 'dawning-agent-os\agentos.db'),
    [string] $BinDir = (Join-Path $PSScriptRoot '..\src\Dawning.AgentOS.Api\bin\Debug\net10.0')
)

$ErrorActionPreference = 'Stop'

function Format-Section {
    param([string] $Title)
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
}

# --- pre-flight ----------------------------------------------------------

if (-not (Test-Path $DbPath)) {
    Write-Host "DB not found: $DbPath" -ForegroundColor Yellow
    Write-Host "桌面端可能还没启动过；或者你在非默认路径上。用 -DbPath 覆写。" -ForegroundColor Yellow
    exit 1
}

$BinDir = (Resolve-Path $BinDir -ErrorAction SilentlyContinue)
if (-not $BinDir -or -not (Test-Path $BinDir)) {
    Write-Host "Api bin 目录不存在；先跑 'dotnet build Dawning.AgentOS.slnx'。" -ForegroundColor Yellow
    exit 1
}

$sqliteDll = Join-Path $BinDir 'Microsoft.Data.Sqlite.dll'
if (-not (Test-Path $sqliteDll)) {
    Write-Host "找不到 Microsoft.Data.Sqlite.dll: $sqliteDll" -ForegroundColor Yellow
    Write-Host "先跑 'dotnet build Dawning.AgentOS.slnx'。" -ForegroundColor Yellow
    exit 1
}

# 加载 Microsoft.Data.Sqlite + SQLitePCLRaw（同目录依赖会被 CLR 自动解析）。
Add-Type -Path $sqliteDll | Out-Null

# native lib (e_sqlite3.dll) 通常在 runtimes/win-x64/native；CLR 默认不会
# 沿 runtimes/ 走子目录解析，所以手动把它复制 / 加入解析路径。最便捷做法是
# 把 native 文件夹加到 PATH，让 LoadLibrary 能找到。
$nativeDir = Join-Path $BinDir 'runtimes\win-x64\native'
if (Test-Path $nativeDir) {
    $env:PATH = "$nativeDir;$env:PATH"
}

function Invoke-SqliteQuery {
    param(
        [Parameter(Mandatory)] [string] $ConnectionString,
        [Parameter(Mandatory)] [string] $Sql
    )
    $conn = New-Object Microsoft.Data.Sqlite.SqliteConnection($ConnectionString)
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Sql
        $reader = $cmd.ExecuteReader()
        try {
            $columns = @()
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $columns += $reader.GetName($i)
            }

            $rows = @()
            while ($reader.Read()) {
                $row = [ordered] @{}
                for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                    $row[$columns[$i]] = if ($reader.IsDBNull($i)) { $null } else { $reader.GetValue($i) }
                }
                $rows += [pscustomobject] $row
            }
            return $rows
        } finally {
            $reader.Dispose()
        }
    } finally {
        $conn.Dispose()
    }
}

$dbInfo = Get-Item $DbPath
Write-Host "DB:   $($dbInfo.FullName)"
Write-Host "Size: $([math]::Round($dbInfo.Length / 1KB, 1)) KB    LastWriteTime: $($dbInfo.LastWriteTime)"

$cs = "Data Source=$($dbInfo.FullName);Mode=ReadOnly"

# --- 1. inbox capture rate ----------------------------------------------

Format-Section 'inbox 捕获 (ADR-035 §D2 量化 #1, 目标 ≥ 30 条 / 7 天)'

Invoke-SqliteQuery -ConnectionString $cs -Sql @"
SELECT
    'total_all_time' AS metric, COUNT(*) AS value FROM inbox_items
UNION ALL
SELECT 'today_utc', COUNT(*) FROM inbox_items
    WHERE substr(captured_at_utc, 1, 10) = strftime('%Y-%m-%d', 'now')
UNION ALL
SELECT 'last_7_days_utc', COUNT(*) FROM inbox_items
    WHERE captured_at_utc >= strftime('%Y-%m-%dT%H:%M:%fZ', 'now', '-7 days');
"@ | Format-Table -AutoSize

Format-Section 'inbox 按天分布 (UTC, 最近 14 天)'

Invoke-SqliteQuery -ConnectionString $cs -Sql @"
SELECT
    substr(captured_at_utc, 1, 10) AS day_utc,
    COUNT(*) AS captures
FROM inbox_items
WHERE captured_at_utc >= strftime('%Y-%m-%dT%H:%M:%fZ', 'now', '-14 days')
GROUP BY day_utc
ORDER BY day_utc DESC;
"@ | Format-Table -AutoSize

# --- 2. memory entries (Save / direct create) ---------------------------

Format-Section 'memory_entries 写入路径分布 (ADR-035 §D2 量化 #2)'

Invoke-SqliteQuery -ConnectionString $cs -Sql @"
SELECT
    CASE source
        WHEN 1 THEN '1_user_explicit_memory_pane_create'
        WHEN 2 THEN '2_conversation_(reserved_v0)'
        WHEN 3 THEN '3_inbox_action_save_button'
        WHEN 4 THEN '4_correction_(reserved_v0)'
        ELSE 'other'
    END AS source_kind,
    COUNT(*) AS entries
FROM memory_entries
GROUP BY source
ORDER BY source;
"@ | Format-Table -AutoSize

Format-Section 'memory_entries 状态分布 (ADR-033 §G1)'

Invoke-SqliteQuery -ConnectionString $cs -Sql @"
SELECT
    CASE status
        WHEN 1 THEN '1_active'
        WHEN 2 THEN '2_corrected'
        WHEN 3 THEN '3_archived'
        WHEN 4 THEN '4_soft_deleted'
        ELSE 'other'
    END AS status_kind,
    COUNT(*) AS entries
FROM memory_entries
GROUP BY status
ORDER BY status;
"@ | Format-Table -AutoSize

# --- 3. derived rate: save click rate -----------------------------------

Format-Section '派生指标: 📒 Save 命中率 (= inbox_action 写入数 / 总捕获数)'

Invoke-SqliteQuery -ConnectionString $cs -Sql @"
SELECT
    (SELECT COUNT(*) FROM inbox_items)                                AS inbox_total,
    (SELECT COUNT(*) FROM memory_entries WHERE source = 3)            AS save_hits,
    CASE WHEN (SELECT COUNT(*) FROM inbox_items) = 0 THEN 'n/a'
         ELSE printf('%.1f%%',
              100.0 * (SELECT COUNT(*) FROM memory_entries WHERE source = 3)
                    / (SELECT COUNT(*) FROM inbox_items))
    END                                                                AS save_hit_rate;
"@ | Format-Table -AutoSize

Write-Host "  ADR-035 复议触发: dogfood 期间 Save 真实点击率 < 5% → C/D 方向变强" -ForegroundColor DarkGray

# --- 4. chat activity ---------------------------------------------------

Format-Section 'chat 活跃度'

Invoke-SqliteQuery -ConnectionString $cs -Sql @"
SELECT
    'sessions_total'     AS metric, COUNT(*) AS value FROM chat_sessions
UNION ALL
SELECT 'messages_total',     COUNT(*) FROM chat_messages
UNION ALL
SELECT 'messages_user',      COUNT(*) FROM chat_messages WHERE role = 1
UNION ALL
SELECT 'messages_assistant', COUNT(*) FROM chat_messages WHERE role = 2;
"@ | Format-Table -AutoSize

Format-Section 'chat 按天分布 (UTC, 最近 7 天)'

Invoke-SqliteQuery -ConnectionString $cs -Sql @"
SELECT
    substr(created_at_utc, 1, 10) AS day_utc,
    COUNT(*) AS messages
FROM chat_messages
WHERE created_at_utc >= strftime('%Y-%m-%dT%H:%M:%fZ', 'now', '-7 days')
GROUP BY day_utc
ORDER BY day_utc DESC;
"@ | Format-Table -AutoSize

# --- footer -------------------------------------------------------------

Write-Host ""
Write-Host "提醒: 以下指标脚本无法获取，请手记到 docs/raw/meetings/ 的 dogfood 笔记:" -ForegroundColor Yellow
Write-Host "  - Summarize / Tags 按钮点击次数（V0 不落库）" -ForegroundColor DarkGray
Write-Host "  - Memory 视图打开次数 / 每次停留时长（前端事件）" -ForegroundColor DarkGray
Write-Host "  - 反例: 想 save 但没按 / 不知道按哪个 / 捕获后忘了处理" -ForegroundColor DarkGray
Write-Host "  - 正例: 新任务时 agent 引用了 Memory 中某条历史" -ForegroundColor DarkGray
