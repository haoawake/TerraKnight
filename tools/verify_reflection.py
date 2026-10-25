"""Compare the live mod catalogue with independently exported installed Terraria projectile defaults."""
import argparse
import json
import re
from pathlib import Path


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--vanilla", required=True, type=Path)
    parser.add_argument("--run", required=True, type=Path)
    args = parser.parse_args()
    vanilla = read(args.vanilla)
    eligible = {r["Id"] for r in vanilla["Rows"]
                if r["AiStyle"] in (1, 2, 8, 21, 24, 28, 29, 131) or r["Id"] in (728, 955)}
    # Reviewed gameplay identities differing from their art: dust-only Imp fireball,
    # and Lightning Strike, whose item 6155 shoots 1117 despite this mod's borrowed 731 art.
    identities = {"ImpFireball": 376, "LightningStrikeBolt": 1117}
    catalogue = read(args.run / "saves/reflection-catalog.json")
    failures = []
    for row in catalogue:
        texture = re.fullmatch(r"Projectile_(\d+)", row["Texture"] or "")
        original = identities.get(row["Kind"], int(texture[1]) if texture else -1)
        expected = original in eligible
        if row["Reflects"] != expected:
            failures.append({"Kind": row["Kind"], "ExpectedId": original,
                             "Expected": expected, "Actual": row["Reflects"]})
    result = {"TerrariaVersion": vanilla["AssemblyVersion"], "VanillaTypes": vanilla["Count"],
              "EligibleAiTypes": len(eligible), "ModRegisteredTypes": len(catalogue),
              "ModReflectableTypes": sum(r["Reflects"] for r in catalogue), "Failures": failures,
              "Scope": "Catalogue identity/AI eligibility, not every type's full combat behaviour."}
    (args.run / "reflection-type-audit.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))
    if failures:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
