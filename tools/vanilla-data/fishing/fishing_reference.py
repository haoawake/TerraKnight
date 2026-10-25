"""Reference evaluator for fishing.json (Terraria 1.4.5 fishing).

catch_distribution(ctx) returns the exact probability of every outcome of ONE FishingCheck() call
(i.e. one completed 660-point bite timer), by enumerating the independent rarity/crate/junk rolls and
the biome tie-break rolls and walking the ordered rule list exactly like FishDropRuleList.TryGetItemDropType.
Outcomes: item id (int), "npc:<id>" for blood-moon enemies, None for "no bite".

Seed-specific logic (remix / notTheBees / skyblock) is not modelled; quest fish are modelled via ctx["questFish"].
Run:  python -I fishing_reference.py
"""
import itertools, json, os
from fractions import Fraction as F

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = json.load(open(os.path.join(HERE, "fishing.json"), encoding="utf-8"))
RULES = DATA["catch_rules"]

DEFAULT_CTX = dict(
    FL=50,                      # fishing level AFTER chum, water penalty and luck (as used in FishingCheck)
    hardMode=False, inLava=False, inHoney=False, canFishInLava=False,
    heightLevel=1,              # 0 sky, 1 surface, 2 underground, 3 cavern, 4 underworld (bobber tile)
    oceanPond=False,            # bobber X<380 or X>maxTilesX-380 (the heightLevel<=1 and water>1000 parts are checked here)
    waterTiles=1000, waterNeeded=300,
    cratePotion=False, bloodMoon=False, dayTime=True, combatBookUsed=False,
    chumCaster=False, unlockedSlimeRed=True, downedBoss3=True,
    zones=(),                   # subset of {"corrupt","crimson","hallow","jungle","snow","desert","glowshroom","dungeon","beach"}
    questFish=-1,               # today's quest fish id if catchable (Angler alive, not finished, not carried), else -1
)

def _den(a, fl):
    return max(a[0], a[1] // fl)

def _cond(name, c, ctx, s):
    z = ctx["zones"]
    if name.startswith("QuestFish"):
        if name == "QuestFishRemix":
            return False
        return ctx["questFish"] == c["item"]
    return {
        "HardMode": ctx["hardMode"], "EarlyMode": not ctx["hardMode"], "InLava": ctx["inLava"], "InHoney": ctx["inHoney"],
        "Junk": s["junk"], "Crate": s["crate"], "AnyEnemies": False, "CanFishInLava": ctx["canFishInLava"],
        "Dungeon": "dungeon" in z and ctx["downedBoss3"], "Beach": "beach" in z, "Hallow": "hallow" in z,
        "GlowingMushrooms": "glowshroom" in z, "TrueDesert": "desert" in z, "TrueSnow": "snow" in z, "Remix": False,
        "Height1": ctx["heightLevel"] == 1, "Height1And2": ctx["heightLevel"] in (1, 2), "HeightAbove1": ctx["heightLevel"] > 1,
        "HeightAboveAnd1": ctx["heightLevel"] >= 1, "HeightUnder2": ctx["heightLevel"] < 2, "HeightAbove2": ctx["heightLevel"] > 2,
        "Height0": ctx["heightLevel"] == 0, "Height2": ctx["heightLevel"] == 2, "Height3": ctx["heightLevel"] == 3,
        "UnderRockLayer": False, "Corruption": s["corrupt"], "Crimson": s["crimson"], "Jungle": s["jungle"], "Snow": s["snow"],
        "Desert": s["desert"], "RolledHallowDesert": s["infectedDesert"] and "hallow" in z,
        "OriginalOcean": ctx["heightLevel"] <= 1 and ctx["oceanPond"] and ctx["waterTiles"] > 1000,
        "RemixOcean": False, "Ocean": ctx["heightLevel"] <= 1 and ctx["oceanPond"] and ctx["waterTiles"] > 1000,
        "Water1000": ctx["waterTiles"] > 1000, "BloodMoon": ctx["bloodMoon"], "DidNotUseCombatBook": not ctx["combatBookUsed"],
    }[name]

def _rarity(name, s):
    return {
        "Any": True, "Legendary": s["legendary"], "VeryRare": s["veryrare"], "Rare": s["rare"], "Uncommon": s["uncommon"],
        "Common": s["common"], "BombRarityOfNotLegendaryAndNotVeryRareAndUncommon": (not s["legendary"]) and (not s["veryrare"]) and s["uncommon"],
        "UncommonOrCommon": s["uncommon"] or s["common"],
    }[name]

def _walk(ctx, s, mass, out):
    for r in RULES:
        if r["kind"] == "stopper" and any(c["name"] == "AnyEnemies" for c in r["conditions"]):
            continue
        if not all(_cond(c["name"], c, ctx, s) for c in r["conditions"]):
            continue
        if not _rarity(r["rarity"], s):
            continue
        p = F(r["chance"][0], r["chance"][1])
        hit = mass * p
        if r["items"]:
            for it in r["items"]:
                out[it] = out.get(it, 0) + hit / len(r["items"])
        else:
            out[None] = out.get(None, 0) + hit
        mass -= hit
        if mass == 0:
            return
    out[None] = out.get(None, 0) + mass

def catch_distribution(**kw):
    ctx = dict(DEFAULT_CTX, **kw)
    ctx["zones"] = set(ctx["zones"])
    FL = ctx["FL"]
    out = {}
    if FL <= 0:
        return {None: F(1)}
    gate = min(F(1), F((FL + 75) // 2 + 1, 100))
    out[None] = 1 - gate
    # blood moon enemies (Projectile.cs:20927-20958)
    enemy = F(0)
    if not ctx["inLava"] and not ctx["inHoney"] and ctx["bloodMoon"] and not ctx["dayTime"]:
        enemy = F(1, 3 if ctx["chumCaster"] else 6)
        picks = {}
        red = F(0) if ctx["unlockedSlimeRed"] else F(1, 5)
        picks[682] = red
        rest = 1 - red
        if ctx["hardMode"]:
            for n in (620, 621, 586, 587):
                picks[n] = picks.get(n, 0) + rest * F(1, 4) * F(9, 10)
            picks[618] = rest * F(1, 10)
        else:
            for n in (586, 587):
                picks[n] = picks.get(n, 0) + rest * F(1, 2)
        for n, p in picks.items():
            if p:
                out[f"npc:{n}"] = gate * enemy * p
    item_mass = gate * (1 - enemy)
    # independent rolls (Projectile.cs:21635-21697, 20855)
    probs = {
        "common": F(1, _den((2, 150), FL)), "uncommon": F(1, _den((3, 300), FL)), "rare": F(1, _den((4, 1050), FL)),
        "veryrare": F(1, _den((5, 2250), FL)), "legendary": F(1, _den((6, 4500), FL)),
        "crate": F(25 if ctx["cratePotion"] else 10, 100),
        "junk": (F(max(0, 49 - FL), 50) ** 2) if ctx["waterTiles"] < ctx["waterNeeded"] else F(0),
    }
    z = ctx["zones"]
    dungeon = "dungeon" in z and ctx["downedBoss3"]
    corr_opts = [((("corrupt" in z), ("crimson" in z)), F(1))]
    if "corrupt" in z and "crimson" in z:
        corr_opts = [((True, False), F(1, 2)), ((False, True), F(1, 2))]
    snow_opts = [("snow" in z, F(1))]
    if "snow" in z and "jungle" in z:
        snow_opts = [(True, F(1, 2)), (False, F(1, 2))]
    desert = "desert" in z and not dungeon
    inf_opts = [(True, F(1, 2)), (False, F(1, 2))] if desert else [(False, F(1))]
    keys = list(probs)
    for bits in itertools.product((False, True), repeat=len(keys)):
        pm = F(1)
        for k, b in zip(keys, bits):
            pm *= probs[k] if b else 1 - probs[k]
        if pm == 0:
            continue
        for (co, cr), p1 in corr_opts:
            for sn, p2 in snow_opts:
                for inf, p3 in inf_opts:
                    s = dict(zip(keys, bits))
                    s.update(corrupt=co, crimson=cr, snow=sn, jungle="jungle" in z, desert=desert, infectedDesert=inf)
                    _walk(ctx, s, item_mass * pm * p1 * p2 * p3, out)
    return out

def show(title, **kw):
    d = catch_distribution(**kw)
    names = DATA["items"]
    print(f"== {title}")
    for k, v in sorted(d.items(), key=lambda kv: -kv[1]):
        if v < F(1, 100000):
            continue
        if k is None:
            label = "(nothing)"
        elif isinstance(k, str):
            label = k
        else:
            x = names.get(str(k), {})
            label = f"{x.get('en', k)} ({k})"
        print(f"  {float(v):8.4%}  {label}")
    print(f"  total = {float(sum(d.values())):.6f}")

if __name__ == "__main__":
    show("Surface forest pond, FL=30", FL=30)
    show("Cavern pond, FL=100, hardmode", FL=100, heightLevel=3, hardMode=True)
    show("Ocean, FL=60", FL=60, oceanPond=True, zones=["beach"], waterTiles=2000)
    show("Lava with Hotline, FL=80, hardmode", FL=80, inLava=True, canFishInLava=True, heightLevel=4, hardMode=True)
