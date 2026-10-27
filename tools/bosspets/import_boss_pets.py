"""The 48 boss pets (空洞骑士首领宠物素材包: the specs; GPT's results-批次*.zip: the art) into the mod: each pet's sheet as
Resources/hk_BossPet_<Name>.png and its item's icon as hk_BossPetItem_<Name>.png (Name: the record without its '@'), the
table src/TerraKnight/Items/BossPetsData.cs, the names' English lines in Resources/lang.json; prints the enum block.

Usage: python -I import_boss_pets.py <folder the result zips were extracted under (each pet's folder holds pet.png)>
"""
import glob
import json
import os
import shutil
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PACK = os.path.join(ROOT, "空洞骑士首领宠物素材包")
RES = os.path.join(ROOT, "src", "TerraKnight", "Resources")
DATA = os.path.join(ROOT, "src", "TerraKnight", "Items", "BossPetsData.cs")
LANG = os.path.join(RES, "lang.json")

# The pets whose boss has no table of its own: its record, and the scenes where the fight is that one.
VARIANTS = {
    "SistersOfBattle": ("MantisLord", ["GG_Mantis_Lords_V"]),
    "WingedNosk": ("MimicSpider", ["GG_Nosk_Hornet"]),
    "EnragedGuardian": ("MegaBeamMiner", ["Mines_32", "GG_Crystal_Guardian_2"]),
    "AbsoluteRadiance": ("FinalBoss", ["GG_Radiance"]),
}
TEMPLATE = {"ground": "Ground", "flyer": "Flyer", "anchor": "Anchor"}


def cs(s):
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def main():
    results = sys.argv[1]
    man = json.load(open(os.path.join(PACK, "manifest.json"), encoding="utf-8"))
    dirs = {os.path.basename(os.path.dirname(p)): os.path.dirname(p) for p in glob.glob(os.path.join(results, "**", "pet.png"), recursive=True)}
    rows, enum, lang = [], [], {}
    for m in man:
        d = dirs[m["folder"]]
        name = m["key"].replace("@", "")
        shutil.copyfile(os.path.join(d, "pet.png"), os.path.join(RES, f"hk_BossPet_{name}.png"))
        shutil.copyfile(os.path.join(d, "item_icon.png"), os.path.join(RES, f"hk_BossPetItem_{name}.png"))
        record, scenes = VARIANTS.get(m["key"], (m["key"], None))
        # A variant listed without a stage: its record's.
        stage = m["stage"] if m["stage"] is not None else next((x["stage"] for x in man if x["key"] == record and x["stage"] is not None), 5)
        enum.append("BossPet" + name)
        scene_cs = "null" if scenes is None else "new[] { " + ", ".join(cs(s) for s in scenes) + " }"
        rows.append(f"                new Row(ItemType.BossPet{name}, {cs(name)}, {cs(record)}, {scene_cs}, Kind.{TEMPLATE[m['template']]}, "
                    f"{m['cell_w']}, {m['cell_h']}, {m['frame_count']}, {'true' if m['light_pet'] else 'false'}, {stage}, {cs(m['pet_zh'])}, {cs(m['item_zh'])}),")
        lang[m["item_zh"]] = {"en-US": m["item_en"]}
        lang[m["pet_zh"]] = {"en-US": m["pet_en"]}
        lang["召唤" + m["pet_zh"]] = {"en-US": "Summons a " + m["pet_en"]}
        lang[m["pet_zh"] + "正跟着你"] = {"en-US": m["pet_en"] + " is following you"}
    with open(DATA, "w", encoding="utf-8", newline="\n") as f:
        f.write("// <generated> by tools/bosspets/import_boss_pets.py from 空洞骑士首领宠物素材包 and its results: edit there. </generated>\n")
        f.write("namespace TerraKnight.Items\n{\n    internal static partial class BossPets\n    {\n")
        f.write("        /// <summary>Each boss's pet (a class of its own: ready whenever ItemDb.Build asks).</summary>\n")
        f.write("        internal static class Table\n        {\n            internal static readonly Row[] Rows =\n            {\n")
        f.write("\n".join(rows) + "\n")
        f.write("            };\n        }\n    }\n}\n")
    t = open(LANG, encoding="utf-8").read()
    data = json.loads(t)
    body = t.rstrip().rstrip("}").rstrip()
    added = 0
    for k, v in lang.items():
        if k in data:
            continue
        body += ",\n " + json.dumps(k, ensure_ascii=False) + ": " + json.dumps(v, ensure_ascii=False)
        added += 1
    t2 = body + "\n}\n"
    json.loads(t2)
    open(LANG, "w", encoding="utf-8").write(t2)
    print("lang lines added", added)
    print("        // Append-only: the bosses' own pets (Items/BossPets.cs, tools/bosspets/import_boss_pets.py, 2026-10-26).")
    for i in range(0, len(enum), 6):
        print("        " + ", ".join(enum[i:i + 6]) + ",")


if __name__ == "__main__":
    main()
