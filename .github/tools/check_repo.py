#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""專案與文件對不對得上。

CI 上沒有遊戲組件、做不了完整編譯，但這幾件事不需要編譯就查得出來，
而且都是這個 repo 真的踩過的坑：

1. **BepInPlugin 的 GUID 撞號**。兩個 mod 同 GUID 時 BepInEx 會拒載其中一個，
   而且只在遊戲的 log 裡說一句話——複製貼上開新 mod 時很容易發生。
2. **README 的表格漏掉某個 mod**。已經發生過一次（PR #10：根 README 的表格
   漏掉 Economy 合併）。兩份 README 都有「專案 / GUID」的表格，逐列對一次就好。
3. **表格裡列了不存在的專案**。mod 被合併或改名之後留下來的殘影。

檢查只看**表格列**（`| `HtF.Xxx` | ...`），不看內文——內文本來就會提到
已經被合併掉的 `HtF.FishingEcology`（叫使用者去刪舊資料夾），那不是錯誤。
"""

import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MODS = os.path.join(ROOT, "mods")
DOCS = ["README.md", os.path.join("mods", "README.md")]

# 表格列開頭的 `| `HtF.Xxx` |`
ROW = re.compile(r"^\|\s*`(HtF\.[A-Za-z0-9]+)`\s*\|", re.MULTILINE)
GUID = re.compile(r'const\s+string\s+Guid\s*=\s*"([^"]+)"')

problems = []


def err(msg):
    problems.append(msg)
    print("::error::" + msg)


def read(path):
    with io.open(path, encoding="utf-8-sig") as f:
        return f.read()


def discover():
    """mods/ 底下每個有同名 .csproj 的資料夾就是一個 mod。"""
    found = {}
    for name in sorted(os.listdir(MODS)):
        d = os.path.join(MODS, name)
        if not os.path.isdir(d):
            continue
        proj = os.path.join(d, name + ".csproj")
        if os.path.isfile(proj):
            found[name] = d
        elif name != "Shared":
            err("mods/%s 沒有同名的 %s.csproj，建置腳本會漏掉它" % (name, name))
    return found


def guid_of(name, path):
    """Plugin.cs 可能在專案根目錄，也可能在 src/ 底下。"""
    for rel in ("Plugin.cs", os.path.join("src", "Plugin.cs")):
        p = os.path.join(path, rel)
        if not os.path.isfile(p):
            continue
        m = GUID.search(read(p))
        if m:
            return m.group(1)
        err("%s 的 %s 找不到 `const string Guid`" % (name, rel))
        return None
    err("%s 找不到 Plugin.cs" % name)
    return None


def main():
    mods = discover()
    if not mods:
        err("mods/ 底下一個專案都沒有，檢查腳本的路徑假設壞了")
        return 1

    # 1. GUID 唯一
    guids = {}
    for name, path in mods.items():
        g = guid_of(name, path)
        if not g:
            continue
        if g in guids:
            err("GUID 撞號：%s 和 %s 都是 `%s`，BepInEx 會拒載其中一個"
                % (guids[g], name, g))
        else:
            guids[g] = name

    # 2/3. 兩份 README 的表格要和實際的專案一致
    for doc in DOCS:
        path = os.path.join(ROOT, doc)
        if not os.path.isfile(path):
            err("找不到 %s" % doc)
            continue

        text = read(path)
        listed = set(ROW.findall(text))

        for name in sorted(set(mods) - listed):
            err("%s 的表格漏了 `%s`" % (doc, name))
        for name in sorted(listed - set(mods)):
            err("%s 的表格列了 `%s`，但 mods/ 底下沒有這個專案" % (doc, name))

        for g, name in sorted(guids.items()):
            if name in listed and ("`%s`" % g) not in text:
                err("%s 沒有提到 %s 的 GUID `%s`" % (doc, name, g))

    if problems:
        print("")
        print("%d 個問題。" % len(problems))
        return 1

    print("%d 個 mod，GUID 沒有撞號，兩份 README 的表格都對得上。" % len(mods))
    return 0


if __name__ == "__main__":
    sys.exit(main())
