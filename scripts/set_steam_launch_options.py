#!/usr/bin/env python3
"""Update FM26 LaunchOptions in Steam localconfig.vdf (macOS)."""
from __future__ import annotations

import re
import shutil
import sys
from pathlib import Path


def make_launch_value(game_dir: str) -> str:
    launcher = f"{game_dir.rstrip('/')}/run_bepinex_arm64.sh"
    return f'\\"{launcher}\\" %command%'


def update_launch_options(content: str, app_id: str, value: str) -> tuple[str, bool]:
    lines = content.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    changed = False

    app_re = re.compile(rf'^[ \t]*"{re.escape(app_id)}"[ \t]*$')

    while i < len(lines):
        line = lines[i]
        if not app_re.match(line):
            out.append(line)
            i += 1
            continue

        # Peek ahead: only patch library app blocks (have Playtime/LastPlayed),
        # not controller_config stubs that also use the app id.
        block_end = i + 1
        depth = 0
        block_text = ""
        if block_end < len(lines) and "{" in lines[block_end]:
            depth = 1
            block_end += 1
            while block_end < len(lines) and depth > 0:
                block_text += lines[block_end]
                depth += lines[block_end].count("{") - lines[block_end].count("}")
                block_end += 1
        if not re.search(r'"(Playtime|LastPlayed)"', block_text):
            out.append(line)
            i += 1
            continue

        out.append(line)
        i += 1
        if i >= len(lines):
            break
        out.append(lines[i])
        i += 1

        depth = 1
        key_indent: str | None = None
        found = False

        while i < len(lines) and depth > 0:
            cur = lines[i]
            if depth == 1 and '"LaunchOptions"' in cur:
                m = re.match(r'^(\s*)"LaunchOptions"\s+"(.*)"\s*$', cur)
                if m:
                    out.append(f'{m.group(1)}"LaunchOptions"\t\t"{value}"\n')
                    changed = True
                    found = True
                    i += 1
                    continue

            if depth == 1 and cur.strip() == "}" and not found:
                indent = key_indent or re.match(r"^(\s*)", line).group(1) + "\t"
                out.append(f'{indent}"LaunchOptions"\t\t"{value}"\n')
                changed = True
                found = True

            out.append(cur)
            if depth == 1 and re.match(r'^\s*"[^"]+"\s+"', cur):
                key_indent = re.match(r"^(\s*)", cur).group(1)
            depth += cur.count("{") - cur.count("}")
            i += 1

    return "".join(out), changed


def main() -> int:
    if len(sys.argv) != 4:
        print(f"usage: {sys.argv[0]} <app_id> <game_dir> <localconfig.vdf>", file=sys.stderr)
        return 2

    app_id, game_dir, cfg_path = sys.argv[1], sys.argv[2], Path(sys.argv[3])
    if not cfg_path.is_file():
        print(f"skip: {cfg_path} (not found)", file=sys.stderr)
        return 0

    value = make_launch_value(game_dir)
    original = cfg_path.read_text(encoding="utf-8", errors="replace")
    updated, changed = update_launch_options(original, app_id, value)

    if not changed:
        print(f"skip: {cfg_path} (app {app_id} block not found)", file=sys.stderr)
        return 0

    if updated == original:
        print(f"unchanged: {cfg_path}")
        return 0

    backup = cfg_path.with_suffix(cfg_path.suffix + ".bak")
    shutil.copy2(cfg_path, backup)
    cfg_path.write_text(updated, encoding="utf-8")
    print(f"updated: {cfg_path}")
    print(f"  LaunchOptions = \"{value}\"")
    print(f"  backup: {backup}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
