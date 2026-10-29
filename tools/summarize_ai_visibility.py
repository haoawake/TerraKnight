"""Aggregate complete AI inventories and explicit per-case runtime evidence; never count skipped cases as passes."""
import argparse
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("runs", nargs="+")
    parser.add_argument("--output", default="artifacts/all-ai-visibility-review")
    parser.add_argument("--sheets", action="store_true")
    args = parser.parse_args()
    output = ROOT / args.output
    output.mkdir(parents=True, exist_ok=True)
    attempts = {}
    inventory = set()
    roster = (ROOT / "src/TerraKnight/Items/EnemyDropData.cs").read_text(encoding="utf-8-sig")
    names = dict(re.findall(r'\["([^"]+)"\]\s*=\s*new Entry \{ En = .*? Zh = "([^"]*)"', roster))
    for run_name in args.runs:
        run = ROOT / "artifacts" / run_name
        checkpoint = run / "saves/all-ai-visibility.json"
        if not checkpoint.exists():
            print(f"MISSING CHECKPOINT {run_name}")
            continue
        data = json.loads(checkpoint.read_text(encoding="utf-8-sig"))
        inventory.update(("common", key) for key in data["CommonInventory"])
        inventory.update(("boss", key) for key in data["BossKeys"])
        for row in data["Rows"]:
            row = dict(row, Run=run_name)
            attempts.setdefault((row["Category"], row["Key"]), []).append(row)
    rows = []
    for category, key in sorted(inventory):
        history = attempts.get((category, key), [])
        # The latest completed attempt determines status. Preserve all older attempts in the manifest.
        completed = [a for a in history if a["Status"] not in ("pending", "running")]
        row = dict((completed or history or [dict(Category=category, Key=key, Status="not-run")])[-1])
        row["Attempts"] = history
        row["Name"] = names.get(key.split("#")[0].split("@")[0], row.get("Name") or key)
        if row["Status"] == "visible" and row.get("Observed", 0) < 2:
            row["Review"] = "short-lived: validate native death/spawn separately"
        elif row["Status"] == "visible" and row.get("LongestHiddenAfterShown", row.get("LongestHidden", 0)) > (8 if category == "boss" else 3):
            row["Review"] = "long hidden interval: inspect native states and retry"
        else:
            row["Review"] = ""
        if row["Key"] == "HiveKnight" and row["Status"] == "visible":
            hidden_states = [s.split(" | ")[0] for s in row.get("States", []) if "/render=False" in s]
            if hidden_states and all("Control=Sleep/" in s for s in hidden_states):
                row["Review"] = "Native Sleep intro; subsequent visible combat states verified"
        if row["Key"] == "BurstingZombie" and row["Status"] == "visible" and row.get("EarlyExit"):
            row["Review"] = "Native suicide attack: Antic/Running visible; finite lifetime, not a full 10-second live observation"
        rows.append(row)
    counts = Counter(row["Status"] for row in rows)
    (output / "coverage.json").write_text(json.dumps(dict(Counts=counts, Rows=rows), ensure_ascii=False, indent=2), encoding="utf-8")
    lines = ["# 全量新 AI 敌人显现核查", "", f"清单由运行时注册表和首领场地表生成：{sum(c == 'common' for c, k in inventory)} 类普通/精英，{sum(c == 'boss' for c, k in inventory)} 个首领场地或变体入口。", "",
             "这是显现与状态恢复核查，有限观察窗不代表已穷尽所有招式、自然遭遇、击杀阶段和联机流程。", "",
             "统计：" + ", ".join(f"{k}={v}" for k, v in sorted(counts.items())), "",
             "|类别|中文名|记录|结果|观察秒|最长隐藏秒|不可见接管采样|AI 异常|复核说明|证据目录|", "|---|---|---|---|---:|---:|---:|---:|---|---|"]
    for row in rows:
        lines.append("|" + "|".join(str(x) for x in [row["Category"], row["Name"], row["Key"], row["Status"], round(row.get("Observed", 0), 1),
            round(row.get("LongestHidden", 0), 1), row.get("InvisibleTakeovers", ""), row.get("Failures", ""), row["Review"], row.get("Run", "")]) + "|")
    (output / "report.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps(dict(counts), ensure_ascii=False))
    for row in rows:
        if row["Status"] not in ("visible", "pending", "running", "not-run") or row["Review"]:
            print(row["Category"], row["Key"], row["Status"], row["Review"], row.get("Diagnostics", []))
    if args.sheets:
        from PIL import Image, ImageDraw
        screenshots = []
        for row in rows:
            if not row.get("Snapshot"):
                continue
            folder = ROOT / "artifacts" / row["Run"] / "runtime/BepInEx/TerraKnight/dev/single-aivisall"
            found = list(folder.glob("*" + row["Snapshot"] + ".png"))
            if found:
                screenshots.append((row, found[0]))
        for offset in range(0, len(screenshots), 20):
            group = screenshots[offset:offset + 20]
            sheet = Image.new("RGB", (1280, 820), "#222222")
            draw = ImageDraw.Draw(sheet)
            for i, (row, path) in enumerate(group):
                x, y = (i % 4) * 320, (i // 4) * 164
                with Image.open(path) as source:
                    thumb = source.convert("RGB").resize((320, 144))
                sheet.paste(thumb, (x, y + 20))
                draw.text((x + 3, y + 3), row["Category"] + "/" + row["Key"], fill="white")
            sheet.save(output / f"contact-{offset // 20 + 1:02d}.png")
        print("Screenshots:", len(screenshots))


if __name__ == "__main__":
    main()
