# -*- coding: utf-8 -*-
"""
從 How to Fish 的 Unity 資產抽取指令參數對照表。

用途：物品 ID/名稱、魚餌、配件、NPC ID 等資料不在反編譯的 C# 裡，
      而是在 Unity 資產檔中（GameInfo 用 Resources.LoadAll 在執行期載入）。

環境準備：
    python -m venv venv
    venv/Scripts/python.exe -m pip install UnityPy TypeTreeGeneratorAPI

執行前：
    1. 確認下方 GAME_DATA 路徑正確。
    2. 準備乾淨的 DLL 目錄（見 prepare_dlls()），務必排除
       「Assembly-CSharp - 複製.dll」——它與正本組件名相同，會讓載入器報錯。

執行：
    venv/Scripts/python.exe extract_gamedata.py [輸出.json]

詳細踩坑說明見 AI_CONTEXT.md 第 6 節。
"""

import collections
import glob
import io
import json
import os
import shutil
import struct
import sys

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
from UnityPy.helpers.TypeTreeNode import TypeTreeNode

GAME_DATA = r"D:\SteamLibrary\steamapps\common\How to Fish\How to Fish\How to Fish_Data"
UNITY_VERSION = "6000.4.4f1"
HERE = os.path.dirname(os.path.abspath(__file__))
DLL_DIR = os.path.join(HERE, "_dlls")

# Item 的完整繼承閉包。只列直接子類別會漏掉將近一半的物品
# （53/85）。若遊戲更新新增了 Item 子類別，用下面的方式重算：
#   grep -hoE "^public class \w+ : \w+" Assembly-CSharp/*.cs
# 然後從 "Item" 往下做傳遞閉包。
ITEM_CLASSES = {
    "Albatross", "AttackingFish", "Bird", "BowheadWhale", "Crab", "Creature",
    "DeadPlayer", "Disc", "Explosive", "Fish", "FishingRod", "FishingRodCast",
    "FishingRodCrab", "Item", "Map", "Melee", "Piranha", "Pufferfish", "Radio",
    "RunningFish", "Spidercrab", "Tool", "Weapon",
}
INFO_CLASSES = {"BaitInfo", "AttachmentInfo", "NPC", "SlotInfo", "BoatMotor"}


def prepare_dlls():
    """複製 Managed/ 下的 DLL，排除組件名衝突的備份副本。

    Managed/ 裡常有手動備份，例如「Assembly-CSharp - 複製.dll」或
    「Assembly-CSharp - 1.0.9.dll」。它們的內部組件名與正本相同，
    TypeTreeGenerator 會丟「An item with the same key has already been added」。

    重要：不能只用 try/except 跳過載入失敗的檔案。字典序下
    「Assembly-CSharp - 1.0.9.dll」排在「Assembly-CSharp.dll」之前
    （空格 0x20 < 點 0x2E），那樣會載入舊備份、跳過正本，然後
    「成功」產出錯誤的資料。所以這裡在複製階段就明確排除。
    """
    managed = os.path.join(GAME_DATA, "Managed")
    if not os.path.isdir(managed):
        sys.exit("找不到 Managed 目錄：%s" % managed)

    stems = {os.path.basename(p)[:-4] for p in glob.glob(os.path.join(managed, "*.dll"))}

    def is_backup_of(stem):
        """「Foo - 任何字尾」在「Foo」也存在時，視為備份副本。"""
        if " - " not in stem:
            return None
        base = stem.rsplit(" - ", 1)[0].strip()
        return base if base in stems else None

    if os.path.isdir(DLL_DIR):
        shutil.rmtree(DLL_DIR)
    os.makedirs(DLL_DIR)

    copied, skipped = 0, []
    for src in sorted(glob.glob(os.path.join(managed, "*.dll"))):
        stem = os.path.basename(src)[:-4]
        base = is_backup_of(stem)
        if base:
            skipped.append((stem, base))
            continue
        shutil.copy2(src, DLL_DIR)
        copied += 1

    for stem, base in skipped:
        print("  略過備份副本：%s.dll（與 %s.dll 組件名相同）" % (stem, base))
    print("  複製 %d 個 DLL" % copied)
    if not os.path.exists(os.path.join(DLL_DIR, "Assembly-CSharp.dll")):
        sys.exit("錯誤：Assembly-CSharp.dll 未被複製，抽取無法進行")


def load_env(generator):
    targets = [
        p
        for pat in ("*.assets", "globalgamemanagers", "level*")
        for p in glob.glob(os.path.join(GAME_DATA, pat))
        if not p.endswith((".resS", ".resource"))
    ]
    env = UnityPy.load(*sorted(set(targets)))
    env.typetree_generator = generator
    return env


def flatten(node, acc=None):
    acc = [] if acc is None else acc
    acc.append(node)
    for child in getattr(node, "m_Children", None) or []:
        flatten(child, acc)
    return acc


def truncated_tree(generator, cls, field, assembly="Assembly-CSharp.dll"):
    """把型別樹截斷到 `field` 為止（含其子節點）。

    完整讀取常在後面的複合欄位（例如 LocalizedString）失敗，但目標欄位
    往往在很前面。Unity 序列化是基底類別欄位在前，所以用基底類別的截斷樹
    也能正確讀出子類別實體的該欄位。
    """
    flat = flatten(generator.get_nodes_up(assembly, cls))
    start = next((i for i, n in enumerate(flat)
                  if n.m_Name == field and n.m_Level == 1), None)
    if start is None:
        return None
    end = start + 1
    while end < len(flat) and flat[end].m_Level > 1:
        end += 1
    return TypeTreeNode.from_list([
        TypeTreeNode(n.m_Level, n.m_Type, n.m_Name, 0, 0, m_MetaFlag=n.m_MetaFlag)
        for n in flat[:end]
    ])


class Resolver:
    """解析 MonoBehaviour 的固定表頭與 PPtr。

    被剝離型別樹的 MonoBehaviour 無法用 parse_as_object()，但前 32 bytes
    的佈局是固定的：
        0  : m_GameObject PPtr (int fileID + int64 pathID = 12 bytes)
        12 : m_Enabled (1 byte) + 3 bytes padding
        16 : m_Script PPtr (12 bytes)
        28 : m_Name (int 長度 + 字元)
    """

    def __init__(self, env):
        self.index = collections.defaultdict(dict)
        for obj in env.objects:
            self.index[getattr(obj.assets_file, "name", "?")][obj.path_id] = obj
        self._script_cache = {}

    @staticmethod
    def header(obj):
        raw = obj.get_raw_data()
        if len(raw) < 32:
            return None
        return (struct.unpack_from("<iq", raw, 0),   # m_GameObject
                struct.unpack_from("<iq", raw, 16))  # m_Script

    def deref(self, owner, file_id, path_id):
        assets = owner.assets_file
        if file_id == 0:
            target = getattr(assets, "name", "?")
        else:
            externals = assets.externals
            if file_id - 1 >= len(externals):
                return None
            target = os.path.basename(externals[file_id - 1].path)
        return self.index.get(target, {}).get(path_id)

    def script_class(self, owner, key):
        if key not in self._script_cache:
            script = self.deref(owner, *key)
            name = None
            if script is not None:
                try:
                    name = script.parse_as_object().m_ClassName
                except Exception:
                    pass
            self._script_cache[key] = name
        return self._script_cache[key]

    def gameobject_name(self, owner, key):
        go = self.deref(owner, *key)
        if go is None:
            return None
        try:
            return go.parse_as_object().m_Name
        except Exception:
            return None


def resource_paths():
    """從 globalgamemanagers 的 ResourceManager 讀 Resources 路徑對照。

    player build 的 env.container 是空的，這是唯一可靠來源。
    Resources.LoadAll 的回傳順序假定與此容器順序一致（未經 Unity 正式保證）。
    """
    env = UnityPy.load(os.path.join(GAME_DATA, "globalgamemanagers"))
    for obj in env.objects:
        if obj.type.name != "ResourceManager":
            continue
        return [(path, ptr.get("m_PathID"))
                for path, ptr in obj.read_typetree().get("m_Container", [])]
    return None


def main():
    out_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "gamedata.json")

    print("[1/5] 準備 DLL")
    prepare_dlls()

    print("[2/5] 從 DLL 重建型別樹")
    generator = TypeTreeGenerator(UNITY_VERSION)
    generator.load_local_dll_folder(DLL_DIR)

    print("[3/5] 載入資產檔")
    env = load_env(generator)
    resolver = Resolver(env)
    print("  索引 %d 個物件" % sum(len(v) for v in resolver.index.values()))

    print("[4/5] 抽取")
    id_tree = truncated_tree(generator, "Item", "_id")
    if id_tree is None:
        sys.exit("無法建立 Item._id 的截斷型別樹")

    items, infos, seen = [], collections.defaultdict(list), set()
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        head = Resolver.header(obj)
        if not head:
            continue
        go_key, script_key = head
        cls = resolver.script_class(obj, script_key)

        if cls in ITEM_CLASSES:
            try:
                item_id = obj.read_typetree(id_tree, check_read=False).get("_id")
            except Exception:
                item_id = None
            name = resolver.gameobject_name(obj, go_key)
            if (name, item_id) in seen:
                continue
            seen.add((name, item_id))
            items.append({
                "class": cls,
                "name": name,
                # GameInfo.cs:725 的查找鍵：去空格、全小寫
                "spawn_key": (name or "").replace(" ", "").lower(),
                "id": item_id,
            })
        elif cls in INFO_CLASSES:
            try:
                data = obj.read_typetree(
                    generator.get_nodes_up("Assembly-CSharp.dll", cls), check_read=False)
            except Exception:
                data = {}
            record = {k: v for k, v in data.items()
                      if isinstance(v, (int, float, str, bool)) or v is None}
            record["_gameobject"] = resolver.gameobject_name(obj, go_key)
            infos[cls].append(record)

    items.sort(key=lambda r: (r["id"] is None, r["id"] or 0))
    with_id = sum(1 for r in items if r["id"] is not None)
    print("  物品 %d 個（%d 個有 ID）" % (len(items), with_id))
    for cls, rows in sorted(infos.items()):
        print("  %s: %d" % (cls, len(rows)))

    print("[5/5] 寫入 %s" % out_path)
    io.open(out_path, "w", encoding="utf-8").write(json.dumps(
        {"items": items, "infos": infos, "resources": resource_paths()},
        ensure_ascii=False, indent=1, default=str))

    if with_id < len(items):
        print("\n注意：有 %d 個物品讀不到 ID。若比例偏高，"
              "檢查 Item 的欄位順序是否變動（見 truncated_tree 的說明）。"
              % (len(items) - with_id))


if __name__ == "__main__":
    main()
