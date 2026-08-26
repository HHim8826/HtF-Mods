#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把七個 mod 打包成可以直接上傳 Thunderstore 的 zip。

    python .github/tools/make_packages.py                     # 建置 + 打包全部
    python .github/tools/make_packages.py --no-build          # 只打包（DLL 要已經建好）
    python .github/tools/make_packages.py --only HtF.Guardian # 只做一個
    python .github/tools/make_packages.py --game-managed "D:\\...\\Managed"

產出在 `dist/`：

    dist/HtF_HudNumbers-1.0.0.zip
    dist/RELEASE_NOTES.md

zip 的版面（Thunderstore 要求那三個檔在**根目錄**，不能包在子資料夾裡）：

    manifest.json
    README.md          <- 套件頁面顯示的就是它
    README_ZH.md       <- 額外的檔案 Thunderstore 不管，但隨包附上比較好
    CHANGELOG.md
    icon.png
    LICENSE
    BepInEx/plugins/<套件名>/<組件>.dll

**打包前一定先跑 check_repo.py。** 版本號寫錯而包出去的後果不是「再改一次」——
Thunderstore 的版本號用掉就是用掉了，同一個號碼不能重傳，只能再開一版。
所以寧可在這裡擋下來。
"""

import argparse
import glob
import io
import json
import os
import re
import shutil
import subprocess
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
MODS = os.path.join(REPO, "mods")

sys.path.insert(0, HERE)
import check_repo                                            # noqa: E402

# 跟著 mod 資料夾一起進 zip 根目錄的檔案
ROOT_FILES = ["manifest.json", "README.md", "README_ZH.md", "CHANGELOG.md", "icon.png"]

ASSEMBLY_NAME = re.compile(r"<AssemblyName>([^<]+)</AssemblyName>")


def read(path):
    with io.open(path, encoding="utf-8-sig") as f:
        return f.read()


def assembly_name(mod, folder):
    m = ASSEMBLY_NAME.search(read(os.path.join(folder, mod + ".csproj")))
    return m.group(1).strip() if m else mod


def find_dll(mod, folder):
    """不寫死 netstandard2.1——目標框架改了也不用回來改這裡。"""
    name = assembly_name(mod, folder)
    hits = glob.glob(os.path.join(folder, "bin", "Release", "*", name + ".dll"))
    if not hits:
        return None
    return max(hits, key=os.path.getmtime)


def build(mod, folder, args):
    cmd = ["dotnet", "build", os.path.join(folder, mod + ".csproj"),
           "-c", "Release", "--nologo"]
    if args.game_managed:
        cmd.append("-p:GameManaged=" + args.game_managed)
    if args.bepinex_core:
        cmd.append("-p:BepInExCore=" + args.bepinex_core)
    # 蓋掉 DeployToProfile 的目的地。打包不該有「順手裝進你的 r2modman」這種副作用
    cmd.append("-p:PluginOut=" + os.path.join(args.out, ".build-deploy", mod))

    print("  建置 %s" % mod)
    proc = subprocess.run(cmd, cwd=REPO, stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, universal_newlines=True)
    if proc.returncode != 0:
        print(proc.stdout)
        return False
    return True


def pack(mod, folder, out_dir):
    manifest = json.loads(read(os.path.join(folder, "manifest.json")))
    pkg, version = manifest["name"], manifest["version_number"]

    dll = find_dll(mod, folder)
    if dll is None:
        print("::error::%s 找不到建好的 DLL（bin/Release/*/）。"
              "拿掉 --no-build，或先自己建置一次。" % mod)
        return None

    zip_path = os.path.join(out_dir, "%s-%s.zip" % (pkg, version))
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        for f in ROOT_FILES:
            z.write(os.path.join(folder, f), f)
        z.write(os.path.join(REPO, "LICENSE"), "LICENSE")
        # arcname 一律用正斜線，Windows 上壓出來才不會變成一個叫
        # "BepInEx\plugins\..." 的單一檔名
        z.write(dll, "BepInEx/plugins/%s/%s" % (pkg, os.path.basename(dll)))

    size = os.path.getsize(zip_path)
    print("  %-34s %6.1f KB" % (os.path.basename(zip_path), size / 1024.0))
    return {"mod": mod, "package": pkg, "version": version,
            "zip": os.path.basename(zip_path),
            "description": manifest.get("description", "")}


def write_notes(packed, out_dir):
    lines = [
        u"上傳到 [Thunderstore（how-to-fish）]"
        u"(https://thunderstore.io/c/how-to-fish/) 的七個套件。",
        u"",
        u"Seven packages for *How to Fish*. Each zip is ready to upload to Thunderstore as-is;",
        u"to install by hand, unzip the `BepInEx/` folder into your game directory.",
        u"",
        u"| 套件 Package | 版本 Version | 檔案 File |",
        u"|---|---|---|",
    ]
    for p in packed:
        lines.append(u"| `%s` | %s | `%s` |" % (p["package"], p["version"], p["zip"]))
    lines += [
        u"",
        u"需要 [BepInEx 5.4.23.5](https://thunderstore.io/c/how-to-fish/p/BepInEx/BepInExPack/)"
        u"（用 mod 管理器安裝時會自動帶）。",
        u"每個套件都是獨立的，可以只裝要的那幾個；誰該裝哪些見各自的 README。",
        u"",
    ]
    path = os.path.join(out_dir, "RELEASE_NOTES.md")
    io.open(path, "w", encoding="utf-8", newline="\n").write(u"\n".join(lines))
    print("  RELEASE_NOTES.md")


def main():
    ap = argparse.ArgumentParser(description="打包 Thunderstore 套件")
    ap.add_argument("--out", default=os.path.join(REPO, "dist"))
    ap.add_argument("--only", action="append", metavar="HtF.Xxx",
                    help="只做這個 mod，可重覆")
    ap.add_argument("--no-build", action="store_true",
                    help="不建置，直接用 bin/Release 底下現成的 DLL")
    ap.add_argument("--game-managed", help="遊戲的 Managed 資料夾")
    ap.add_argument("--bepinex-core", help="BepInEx 的 core 資料夾")
    args = ap.parse_args()

    # 版本號對不上就別包了，見檔頭的說明
    print("檢查 repo …")
    if check_repo.main() != 0:
        print("::error::check_repo.py 有問題，先修好再打包。")
        return 1

    mods = check_repo.discover()
    if args.only:
        unknown = [m for m in args.only if m not in mods]
        if unknown:
            print("::error::mods/ 底下沒有：%s" % ", ".join(unknown))
            return 1
        mods = {m: mods[m] for m in args.only}

    # 只有整批打包才清空。--only 時清掉會把上一輪其他六個 zip 一起帶走，
    # 而「只重包一個」正是最常用 --only 的場合。
    if args.only:
        if not os.path.isdir(args.out):
            os.makedirs(args.out)
    else:
        if os.path.isdir(args.out):
            shutil.rmtree(args.out)
        os.makedirs(args.out)

    print("\n打包 %d 個套件 -> %s" % (len(mods), args.out))
    packed, failed = [], []
    for mod in sorted(mods):
        folder = mods[mod]
        if not args.no_build and not build(mod, folder, args):
            failed.append(mod)
            continue
        info = pack(mod, folder, args.out)
        if info is None:
            failed.append(mod)
        else:
            packed.append(info)

    shutil.rmtree(os.path.join(args.out, ".build-deploy"), ignore_errors=True)

    if failed:
        print("\n::error::失敗：%s" % ", ".join(failed))
        return 1

    # --only 時不寫發布說明：它列的會是「這次包的那一個」，而 dist/ 裡通常
    # 還躺著上一輪的另外六個，寫出去只會誤導。
    if args.only:
        print("\n%d 個套件打包完成（--only，RELEASE_NOTES.md 沒有更新）。" % len(packed))
    else:
        write_notes(packed, args.out)
        print("\n%d 個套件打包完成。" % len(packed))
    return 0


if __name__ == "__main__":
    sys.exit(main())
