#!/usr/bin/env bash
# 把 docs/ 下所有 .mmd 渲染成同名 .png（放在相邻 ../images/ 或原目录的 generated/ 下）
# 依赖：pnpm add -g @mermaid-js/mermaid-cli

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG="${ROOT}/scripts/mermaid-config.json"
PUPPETEER_CFG="${ROOT}/scripts/puppeteer-config.json"

if ! command -v mmdc >/dev/null 2>&1; then
  echo "❌ mmdc 未安装。请先运行：pnpm add -g @mermaid-js/mermaid-cli" >&2
  exit 1
fi

# 自动探测 chrome-headless-shell：bundled puppeteer 版本与本机已安装版本可能不一致，
# 用最新已下载的版本兜底，避免 "Could not find Chrome (ver. X)" 报错。
if [[ -z "${PUPPETEER_EXECUTABLE_PATH:-}" ]]; then
  CHS_DIR="${HOME}/.cache/puppeteer/chrome-headless-shell"
  if [[ -d "$CHS_DIR" ]]; then
    detected="$(ls -d "$CHS_DIR"/*/chrome-headless-shell-mac-x64/chrome-headless-shell 2>/dev/null | sort -V | tail -1 || true)"
    if [[ -n "$detected" && -x "$detected" ]]; then
      export PUPPETEER_EXECUTABLE_PATH="$detected"
      echo "ℹ️  PUPPETEER_EXECUTABLE_PATH=$PUPPETEER_EXECUTABLE_PATH"
    fi
  fi
fi

# 默认：扫描 docs/frameworks 和 docs/images/**/diagrams
SCAN_DIRS=(
  "${ROOT}/frameworks"
)

count=0
while IFS= read -r -d '' mmd; do
  dir="$(dirname "$mmd")"
  name="$(basename "$mmd" .mmd)"
  out="${dir}/${name}.png"

  # 只在源文件比输出新时重新渲染
  if [[ -f "$out" && "$out" -nt "$mmd" ]]; then
    echo "⏭  skip (up-to-date): ${mmd#$ROOT/}"
    continue
  fi

  echo "🖼  render: ${mmd#$ROOT/} → ${out#$ROOT/}"
  mmdc -i "$mmd" -o "$out" \
       -c "$CONFIG" \
       -p "$PUPPETEER_CFG" \
       -b white \
       -s 2 \
       --quiet
  count=$((count + 1))
done < <(find "${SCAN_DIRS[@]}" -name "*.mmd" -type f -print0)

echo ""
echo "✅ 渲染完成：$count 个文件"
