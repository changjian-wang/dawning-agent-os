#!/usr/bin/env python3
"""Detect dead [[wikilinks]] in wiki pages.

Resolution order for `[[target]]`:
1) If `target` has no slash: match any file whose stem == target (with/without .zh-CN).
2) If `target` has slash: textually resolve relative to source file's docs-rel dir,
   collapsing `..` and `.`. Match against docs-relative paths (with/without .zh-CN).
3) Fallback: match the tail stem against any file stem.

Anchors (`#`) and aliases (`|`) are stripped before resolution.
TODO-* targets are skipped.
"""
from __future__ import annotations
import re
import sys
from pathlib import Path
from pathlib import PurePosixPath

DOCS = Path("docs")


def build_index():
    docs_rel = set()
    by_stem: dict[str, list[str]] = {}
    for p in DOCS.rglob("*.md"):
        rel = p.relative_to(DOCS).with_suffix("")
        s = str(rel)
        docs_rel.add(s)
        docs_rel.add(s.replace(".zh-CN", ""))
        stem = p.stem.replace(".zh-CN", "")
        by_stem.setdefault(stem, []).append(s)
    return docs_rel, by_stem


def resolve(target: str, src_file: Path, docs_rel: set, by_stem: dict) -> bool:
    target = target.strip().rstrip("/")
    if not target:
        return True
    t = target.replace(".zh-CN", "")
    if "/" not in t:
        return t in by_stem
    # textual relative resolution
    src_rel = src_file.relative_to(DOCS).parent
    base = PurePosixPath(*src_rel.parts)
    norm = (base / t).as_posix()
    parts: list[str] = []
    for seg in norm.split("/"):
        if seg == "..":
            if parts:
                parts.pop()
        elif seg in ("", "."):
            pass
        else:
            parts.append(seg)
    candidate = "/".join(parts)
    if candidate in docs_rel:
        return True
    tail = t.split("/")[-1]
    return tail in by_stem


def main(scope: str = "docs"):
    docs_rel, by_stem = build_index()
    root = Path(scope)
    pat = re.compile(r"\[\[([^\]|#]+?)(?:#[^\]|]*)?(?:\|[^\]]*)?\]\]")
    # markers that signal "this is an intentional forward-reference, not a bug"
    stub_marker = re.compile(r"待写|规划中|⏳|TODO")
    dead: list[tuple[str, str]] = []
    stubs: list[tuple[str, str]] = []
    for f in root.rglob("*.md"):
        txt = f.read_text(encoding="utf-8")
        # mask fenced code blocks and inline code so [[...]] inside `code` is ignored
        masked = re.sub(r"```.*?```", lambda m: " " * len(m.group(0)), txt, flags=re.DOTALL)
        masked = re.sub(r"`[^`\n]+`", lambda m: " " * len(m.group(0)), masked)
        # split by lines so we can check stub marker on the same line
        lines = masked.splitlines()
        for i, line in enumerate(lines):
            for m in pat.finditer(line):
                target = m.group(1)
                if "TODO" in target:
                    continue
                if resolve(target, f, docs_rel, by_stem):
                    continue
                # check current line + previous line + next line for stub marker
                window = "\n".join(lines[max(0, i - 1) : i + 2])
                if stub_marker.search(window):
                    stubs.append((str(f), target))
                else:
                    dead.append((str(f), target))
    print(f"Scope: {scope}")
    print(f"DEAD wikilinks (must fix): {len(dead)}\n")
    for f, t in dead:
        print(f"  {f}\n    → [[{t}]]")
    print(f"\nKnown stubs (forward refs marked 待写/规划中/⏳, OK to keep): {len(stubs)}")
    for f, t in stubs:
        print(f"  {f}\n    → [[{t}]]")
    sys.exit(1 if dead else 0)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "docs")
