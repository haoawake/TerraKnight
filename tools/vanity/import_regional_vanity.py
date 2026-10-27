"""The 24 Hallownest areas' costumes (output/regional-vanity-native: each a vanilla vanity set recoloured, its pixels'
alpha the vanilla sheet's) into the mod: every sheet as Resources/hk_RegionVanity_<Key>_<vanilla name>.png (Armor_Head_268,
Armor_237, Armor_Legs_222, Acc_Back_17, Item_5051, ...), and the sheets the set draws that the pack left in vanilla colours
(a robe's coat tails, a cloak's other half, a woman's legs) recoloured here by the pack's own colour map (each vanilla
colour to one new one; a colour the map lacks follows its nearest mapped one, keeping its own difference in lightness).
Also writes src/TerraKnight/Items/RegionVanityData.cs and prints the enum block.

Usage: python -I import_regional_vanity.py <vanilla png dir (XnbDump output)> [--preview <png>]
"""
import colorsys
import json
import os
import sys
from collections import Counter, defaultdict

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PACK = os.path.join(ROOT, "output", "regional-vanity-native")
RES = os.path.join(ROOT, "src", "TerraKnight", "Resources")
DATA = os.path.join(ROOT, "src", "TerraKnight", "Items", "RegionVanityData.cs")

# Sheets a set draws beyond the pack's (VanityLooks: BodyExtension, BodyCapeBoth, MatchLegs for a woman), and whose map
# recolours them: the body's or the legs'.
DERIVED = {
    "KingsPass": [("Armor_Legs_223", "Body")],          # TimelessTravelerRobe's coat tails (BodyExtension 237)
    "GrimmTroupe": [("Acc_Front_7", "Body"), ("Acc_Back_20", "Body")],  # VampireShirt's cloak (BodyCapeBoth 85)
    "Deepnest": [("Acc_Front_9", "Body"), ("Acc_Back_30", "Body")],     # PlaguebringerChestplate's (BodyCapeBoth 235)
    "Abyss": [("Armor_Legs_192", "Legs")],              # SuperHeroTights, a woman's (MatchLegs 191)
    "RestingGrounds": [("Armor_Legs_198", "Legs")],     # FuneralPants, a woman's (MatchLegs 197)
}


def flat(name):
    return name.replace("Armor/", "")


def colour_map(src, dst):
    m = defaultdict(Counter)
    for p, q in zip(src.getdata(), dst.getdata()):
        if p[3] > 0:
            m[p[:3]][q[:3]] += 1
    return {k: v.most_common(1)[0][0] for k, v in m.items()}


def hls(c):
    return colorsys.rgb_to_hls(c[0] / 255, c[1] / 255, c[2] / 255)


def nearest(c, keys):
    h, l, s = hls(c)

    def dist(k):
        h2, l2, s2 = hls(k)
        dh = min(abs(h - h2), 1 - abs(h - h2)) * (min(s, s2) * 2)
        return dh * dh + (l - l2) ** 2 + 0.5 * (s - s2) ** 2

    return min(keys, key=dist)


def recolour(img, m):
    out = img.copy()
    px = out.load()
    cache = {}
    w, h = out.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            c = (r, g, b)
            if c not in cache:
                if c in m:
                    cache[c] = m[c]
                else:
                    s0 = nearest(c, list(m))
                    h1, l1, s1 = hls(c)
                    h2, l2, s2 = hls(s0)
                    h3, l3, s3 = hls(m[s0])
                    l = min(1, max(0, l3 + (l1 - l2)))
                    s = min(1, max(0, s3 * (s1 / s2) if s2 > 0.02 else s3))
                    rr, gg, bb = colorsys.hls_to_rgb(h3, l, s)
                    cache[c] = (round(rr * 255), round(gg * 255), round(bb * 255))
            px[x, y] = cache[c] + (a,)
    return out


def main():
    vanilla = sys.argv[1]
    preview = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
    rows, enum, shots = [], [], []
    for d in sorted(n for n in os.listdir(PACK) if n[:2].isdigit()):
        src = json.load(open(os.path.join(PACK, d, "source.json"), encoding="utf-8"))
        key = src["key"]
        native = os.path.join(PACK, d, "native")
        maps = {}
        for e in src["entries"]:
            kind = e["kind"]
            path = os.path.join(native, f"{key}_{kind}.png")
            img = Image.open(path).convert("RGBA")
            img.save(os.path.join(RES, f"hk_RegionVanity_{key}_{flat(e['source'])}.png"))
            if kind in ("Head", "Body", "Legs"):
                rows.append((src["index"] - 1, f"RegionVanity{key}{kind}", e["item"]))
                enum.append(f"RegionVanity{key}{kind}")
                base = Image.open(os.path.join(vanilla, e["source"] + ".png")).convert("RGBA")
                maps[kind] = colour_map(base, img)
        for name, by in DERIVED.get(key, []):
            base = Image.open(os.path.join(vanilla, name + ".png")).convert("RGBA")
            made = recolour(base, maps[by])
            made.save(os.path.join(RES, f"hk_RegionVanity_{key}_{name}.png"))
            shots.append((key + " " + name, base, made))
    with open(DATA, "w", encoding="utf-8", newline="\n") as f:
        f.write("// <generated> by tools/vanity/import_regional_vanity.py from output/regional-vanity-native: edit there. </generated>\n")
        f.write("namespace TerraKnight.Items\n{\n    internal static partial class ItemDb\n    {\n")
        f.write("        /// <summary>Each area's costume piece (World.AreaBlocks.All's index) and the vanilla piece it recolours (a class of\n")
        f.write("        /// its own: ready whenever Build asks, whatever order ItemDb's fields start in).</summary>\n")
        f.write("        static (int Area, ItemType Made, ItemType From)[] regionVanity => RegionVanityTable.Rows;\n\n")
        f.write("        static class RegionVanityTable\n        {\n")
        f.write("            internal static readonly (int Area, ItemType Made, ItemType From)[] Rows =\n            {\n")
        for a, made, frm in rows:
            f.write(f"                ({a}, ItemType.{made}, ItemType.{frm}),\n")
        f.write("            };\n        }\n    }\n}\n")
    print("        // Append-only: the 24 areas' costumes (Items/RegionVanity.cs, tools/vanity/import_regional_vanity.py, 2026-10-25).")
    for i in range(0, len(enum), 6):
        print("        " + ", ".join(enum[i:i + 6]) + ",")
    if preview and shots:
        # Frame 0 of each derived sheet: vanilla over recoloured, 4x.
        cells = []
        for label, a, b in shots:
            fa, fb = a.crop((0, 0, 40, 56)), b.crop((0, 0, 40, 56))
            cell = Image.new("RGBA", (80, 56), (60, 60, 70, 255))
            cell.alpha_composite(fa, (0, 0))
            cell.alpha_composite(fb, (40, 0))
            cells.append(cell.resize((320, 224), Image.NEAREST))
        sheet = Image.new("RGBA", (320, 224 * len(cells)), (0, 0, 0, 255))
        for i, c in enumerate(cells):
            sheet.alpha_composite(c, (0, 224 * i))
        sheet.save(preview)
        print("preview", preview, [s[0] for s in shots])


if __name__ == "__main__":
    main()
