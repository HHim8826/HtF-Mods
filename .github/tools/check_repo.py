#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""專案與文件對不對得上。

CI 上沒有遊戲組件、做不了完整編譯，但這幾件事不需要編譯就查得出來，
而且都是這個 repo 真的踩過、或是上架時一定會踩到的坑：

1. **BepInPlugin 的 GUID 撞號**。兩個 mod 同 GUID 時 BepInEx 會拒載其中一個，
   而且只在遊戲的 log 裡說一句話——複製貼上開新 mod 時很容易發生。
2. **README 的表格漏掉某個 mod**。已經發生過一次（PR #10：根 README 的表格
   漏掉 Economy 合併）。兩份 README 都有「專案 / GUID」的表格，逐列對一次就好。
3. **表格裡列了不存在的專案**。mod 被合併或改名之後留下來的殘影。
4. **Thunderstore 的打包檔缺件或對不上**。每個 mod 都要能包成一個
   Thunderstore 套件，而套件的規則是硬性的：
     - zip 根目錄要有 manifest.json / README.md / icon.png
     - icon.png 必須**剛好** 256x256
     - manifest 的 name 只允許 `a-zA-Z0-9_`（所以 `HtF.Xxx` 不合法，要 `HtF_Xxx`）
     - description 最多 250 字
     - version_number 是三段式，而且**同一個版本號只能上傳一次**
   最後那一條是這裡最重要的檢查：版本號寫在 csproj 的 <Version>，
   [BepInPlugin] 由 Common.props 產生的 ModInfo 帶過去，manifest.json 則是
   另一份字面值——沒有人對帳的話，遲早會用舊號碼把新東西送上去。

檢查只看**表格列**（`| `HtF.Xxx` | ...`），不看內文——內文本來就會提到
已經被合併掉的 `HtF.FishingEcology`（叫使用者去刪舊資料夾），那不是錯誤。
"""

import io
import json
import os
import re
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MODS = os.path.join(ROOT, "mods")
DOCS = ["README.md", os.path.join("mods", "README.md")]

# 表格列開頭的 `| `HtF.Xxx` |`
ROW = re.compile(r"^\|\s*`(HtF\.[A-Za-z0-9]+)`\s*\|", re.MULTILINE)
GUID = re.compile(r'const\s+string\s+Guid\s*=\s*"([^"]+)"')
PROJ_VERSION = re.compile(r"<Version>([^<]+)</Version>")
PROJ_PLUGIN_NAME = re.compile(r"<PluginName>([^<]+)</PluginName>")
BEPIN_ATTR = re.compile(r"\[BepInPlugin\(([^\]]*)\)\]")

# Thunderstore 的規則，出自 https://wiki.thunderstore.io/mods/creating-a-package
TS_NAME = re.compile(r"^[A-Za-z0-9_]{1,128}$")
TS_VERSION = re.compile(r"^\d+\.\d+\.\d+$")
TS_DESC_MAX = 250

# zip 根目錄要有的檔案。
PACKAGE_FILES = ["manifest.json", "README.md", "CHANGELOG.md", "README_ZH.md", "icon.png"]

# icon.png 的尺寸不對就是上傳被打回，而且錯的是一個純數字、肉眼看不出來。
ICON_SIZE = (256, 256)
PNG_MAGIC = bytes([137, 80, 78, 71, 13, 10, 26, 10])   # PNG 檔頭

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
    p = plugin_cs(path)
    if p is None:
        err("%s 找不到 Plugin.cs" % name)
        return None
    m = GUID.search(read(p))
    if m:
        return m.group(1)
    err("%s 的 Plugin.cs 找不到 `const string Guid`" % name)
    return None


def plugin_cs(path):
    for rel in ("Plugin.cs", os.path.join("src", "Plugin.cs")):
        p = os.path.join(path, rel)
        if os.path.isfile(p):
            return p
    return None


def package_name(mod):
    """`HtF.HudNumbers` -> `HtF_HudNumbers`。Thunderstore 的 name 不收點號。"""
    return mod.replace(".", "_")


def check_project(name, path):
    """csproj 的 <Version> / <PluginName>，回傳版本號給 manifest 對帳。"""
    proj = os.path.join(path, name + ".csproj")
    text = read(proj)

    if not PROJ_PLUGIN_NAME.search(text):
        err("%s.csproj 少了 <PluginName>，Common.props 產生 ModInfo 時會失敗" % name)

    m = PROJ_VERSION.search(text)
    if not m:
        err("%s.csproj 少了 <Version>" % name)
        return None

    version = m.group(1).strip()
    if not TS_VERSION.match(version):
        err("%s.csproj 的 <Version> 是 `%s`，Thunderstore 只收三段式 x.y.z"
            % (name, version))
    return version


def check_attribute(name, path):
    """[BepInPlugin] 不可以再出現版本字面值——那是 csproj 的工作。"""
    p = plugin_cs(path)
    if p is None:
        return
    m = BEPIN_ATTR.search(read(p))
    if not m:
        err("%s 找不到 [BepInPlugin(...)]" % name)
        return
    args = m.group(1)
    if "ModInfo.Name" not in args or "ModInfo.Version" not in args:
        err("%s 的 [BepInPlugin(%s)] 沒有用 ModInfo.Name / ModInfo.Version，"
            "版本號會多出一份字面值要維護（見 mods/Common.props）" % (name, args.strip()))


def check_manifest(name, path, version):
    """Thunderstore 的 manifest.json。規則是硬的，寫錯就是上傳被打回。"""
    p = os.path.join(path, "manifest.json")
    if not os.path.isfile(p):
        err("mods/%s 沒有 manifest.json，包不成 Thunderstore 套件" % name)
        return

    try:
        data = json.loads(read(p))
    except ValueError as e:
        err("mods/%s/manifest.json 不是合法的 JSON：%s" % (name, e))
        return

    expected = package_name(name)
    got = data.get("name")
    if got != expected:
        err("mods/%s/manifest.json 的 name 是 `%s`，應該是 `%s`" % (name, got, expected))
    elif not TS_NAME.match(got):
        err("mods/%s/manifest.json 的 name `%s` 不合法：Thunderstore 只收 a-zA-Z0-9_"
            % (name, got))

    got_version = data.get("version_number")
    if version is not None and got_version != version:
        err("mods/%s：manifest.json 的 version_number 是 `%s`，"
            "但 %s.csproj 的 <Version> 是 `%s`。Thunderstore 不收重覆的版本號，"
            "送錯了只能再開一版" % (name, got_version, name, version))

    desc = data.get("description")
    if not desc:
        err("mods/%s/manifest.json 少了 description" % name)
    elif len(desc) > TS_DESC_MAX:
        err("mods/%s/manifest.json 的 description 有 %d 字，Thunderstore 上限 %d"
            % (name, len(desc), TS_DESC_MAX))

    if "website_url" not in data:
        err("mods/%s/manifest.json 少了 website_url（不用的話也要留成空字串）" % name)

    deps = data.get("dependencies")
    if not isinstance(deps, list) or any(not isinstance(d, str) for d in deps):
        err("mods/%s/manifest.json 的 dependencies 要是字串陣列" % name)


def png_size(path):
    """只讀 IHDR，不用 Pillow——CI 不該為了量一張圖多裝一個套件。"""
    with open(path, "rb") as f:
        head = f.read(24)
    if len(head) < 24 or head[:8] != PNG_MAGIC or head[12:16] != b"IHDR":
        return None
    return struct.unpack(">II", head[16:24])


def check_package_files(name, path):
    for f in PACKAGE_FILES:
        p = os.path.join(path, f)
        if not os.path.isfile(p):
            hint = "，用 .github/tools/make_icons.py 產生" if f == "icon.png" else ""
            err("mods/%s 少了 %s%s" % (name, f, hint))
            continue
        if f != "icon.png":
            continue
        size = png_size(p)
        if size is None:
            err("mods/%s/icon.png 不是 PNG（Thunderstore 只收 PNG）" % name)
        elif size != ICON_SIZE:
            err("mods/%s/icon.png 是 %dx%d，Thunderstore 要求剛好 %dx%d"
                % (name, size[0], size[1], ICON_SIZE[0], ICON_SIZE[1]))


def main():
    mods = discover()
    if not mods:
        err("mods/ 底下一個專案都沒有，檢查腳本的路徑假設壞了")
        return 1

    # 1. GUID 唯一
    guids = {}
    for name, path in sorted(mods.items()):
        g = guid_of(name, path)
        if not g:
            continue
        if g in guids:
            err("GUID 撞號：%s 和 %s 都是 `%s`，BepInEx 會拒載其中一個"
                % (guids[g], name, g))
        else:
            guids[g] = name

    # 2. 版本號的三個落點要對上，打包檔要齊
    for name, path in sorted(mods.items()):
        version = check_project(name, path)
        check_attribute(name, path)
        check_manifest(name, path, version)
        check_package_files(name, path)

    # 3/4. 兩份 README 的表格要和實際的專案一致
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

        for name in sorted(mods):
            pkg = package_name(name)
            if name in listed and ("`%s`" % pkg) not in text:
                err("%s 沒有提到 %s 的 Thunderstore 套件名 `%s`" % (doc, name, pkg))

    if problems:
        print("")
        print("%d 個問題。" % len(problems))
        return 1

    print("%d 個 mod，GUID 沒有撞號，版本號三處一致，打包檔齊全，"
          "兩份 README 的表格都對得上。" % len(mods))
    return 0


if __name__ == "__main__":
    sys.exit(main())
