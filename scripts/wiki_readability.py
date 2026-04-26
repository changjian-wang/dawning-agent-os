#!/usr/bin/env python3
"""Readability audit for wiki pages.

Scans:
- Page length (lines & H2-section count)
- TL;DR presence (a `>` blockquote within first 10 lines after H1)
- H1 presence and uniqueness
- H2/H3 structure depth (>4 levels nested = too deep)
- Average paragraph length
- Code block / table / mermaid usage signals
- Missing "## 来源" / "## 交叉引用" sections (per SCHEMA §3.2)
- Long flat lists (>30 consecutive bullets without subheaders)
"""
from __future__ import annotations
import re
from pathlib import Path
from collections import Counter

ROOT = Path("docs")
WIKI_TOPS = {"entities","concepts","comparisons","decisions","synthesis","frameworks","readings"}

def split_fm(txt):
    if not txt.startswith("---"):
        return None, txt
    end = txt.find("\n---", 3)
    if end < 0: return None, txt
    return txt[3:end], txt[end+4:]

def audit(p: Path):
    txt = p.read_text(encoding="utf-8", errors="replace")
    fm, body = split_fm(txt)
    body = body.lstrip("\n")
    lines = body.splitlines()
    issues = []

    # Length
    nl = len(lines)
    # Skip lines inside fenced code blocks for header detection
    in_code = False
    code_mask = []
    for l in lines:
        if l.startswith("```") or l.startswith("~~~"):
            in_code = not in_code
            code_mask.append(True)  # the fence itself is "code-ish"
        else:
            code_mask.append(in_code)
    h2 = [i for i,l in enumerate(lines) if not code_mask[i] and re.match(r"^##\s+", l)
          and "TOC-AUTOGEN" not in l and "XREF-STUB" not in l and "SRC-STUB" not in l]
    h3 = [i for i,l in enumerate(lines) if not code_mask[i] and re.match(r"^###\s+", l)]
    h4plus = [i for i,l in enumerate(lines) if not code_mask[i] and re.match(r"^####+\s+", l)]
    h1 = [i for i,l in enumerate(lines) if not code_mask[i] and re.match(r"^#\s+", l)]

    # 阈值说明:wiki 页本就承担综合/对比职能,长页是合理产物。
    # 阈值定在"明显过长、TOC 也救不回来"的程度,而非"看起来长"。
    if nl > 900:
        issues.append(("L1-超长", f"{nl} 行 (>900) — 建议做内容索引化"))
    elif nl > 700:
        issues.append(("L2-偏长", f"{nl} 行 (>700)"))

    if len(h2) > 22:
        issues.append(("L1-H2 过多", f"{len(h2)} 个 (>22) — 建议拆分"))
    elif len(h2) > 16:
        issues.append(("L2-H2 偏多", f"{len(h2)} 个 (>16)"))

    if len(h1) == 0:
        issues.append(("L1-缺 H1", "无 # 标题"))
    elif len(h1) > 1:
        issues.append(("L1-多个 H1", f"{len(h1)} 个 H1"))

    # TL;DR / blockquote summary within first 15 lines after H1
    if h1:
        head_window = lines[h1[0]+1:h1[0]+16]
        has_summary = any(l.strip().startswith(">") for l in head_window)
        if not has_summary:
            issues.append(("L2-缺 TL;DR", "首屏无 > 摘要块"))

    # Cross-ref section
    has_xref = any(re.match(r"^##+\s+(交叉引用|相关|See also|Related)", l) for l in lines)
    if not has_xref and nl > 200:
        issues.append(("L3-缺交叉引用", "长页未含 ## 交叉引用"))

    # Sources section (only require for non-readings; readings tend to embed inline)
    has_src = any(re.match(r"^##+\s+(来源|参考|References|Sources)", l) for l in lines)
    if not has_src and nl > 200:
        issues.append(("L3-缺来源章节", "长页未含 ## 来源/参考"))

    # H4+ depth
    if len(h4plus) > 5:
        issues.append(("L2-嵌套过深", f"{len(h4plus)} 个 H4+ 标题"))

    # Long flat list
    bullet = re.compile(r"^\s*[-*+]\s+")
    streak = 0; max_streak = 0
    for l in lines:
        if bullet.match(l):
            streak += 1
            max_streak = max(max_streak, streak)
        elif l.strip() == "":
            pass
        else:
            streak = 0
    if max_streak > 30:
        issues.append(("L2-长平铺列表", f"连续 {max_streak} 个 bullet"))

    # Paragraph length: any paragraph > 20 lines without break
    para_lines = 0; max_para = 0
    in_code = False
    for l in lines:
        if l.startswith("```"):
            in_code = not in_code
            para_lines = 0; continue
        if in_code:
            continue
        if l.strip() == "" or re.match(r"^#+\s|^\|", l) or bullet.match(l):
            max_para = max(max_para, para_lines)
            para_lines = 0
        else:
            para_lines += 1
    max_para = max(max_para, para_lines)
    if max_para > 15:
        issues.append(("L3-段落过长", f"最长段 {max_para} 行"))

    return nl, len(h2), issues

def main():
    rows = []
    for p in ROOT.rglob("*.md"):
        if str(p).startswith("docs/raw/"): continue
        rel_parts = p.relative_to(ROOT).parts
        if not rel_parts or rel_parts[0] not in WIKI_TOPS: continue
        if p.name in {"README.md","README.zh-CN.md"}: continue
        nl, h2, issues = audit(p)
        if issues:
            rows.append((str(p), nl, h2, issues))

    # bucket by severity
    by_sev = {"L1":[], "L2":[], "L3":[]}
    for path, nl, h2, issues in rows:
        for code, msg in issues:
            sev = code.split("-")[0]
            by_sev[sev].append((path, code, msg))

    for sev in ("L1","L2","L3"):
        items = by_sev[sev]
        title = {"L1":"阻断","L2":"重要","L3":"提示"}[sev]
        print("="*60); print(f"{sev} {title} — {len(items)} 项"); print("="*60)
        # group by file
        by_file = {}
        for p,c,m in items:
            by_file.setdefault(p, []).append((c,m))
        for p in sorted(by_file):
            print(f"\n  {p}")
            for c,m in by_file[p]:
                print(f"    [{c}] {m}")

    # Top 20 longest pages summary
    print("\n"+"="*60); print("超长页 Top 20 (按行数)"); print("="*60)
    sized = sorted(((nl,h2,p) for p,nl,h2,_ in rows), reverse=True)[:20]
    for nl,h2,p in sized:
        print(f"  {nl:5d} 行  H2={h2:3d}  {p}")

if __name__ == "__main__":
    main()
