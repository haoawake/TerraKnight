"""Generate a report only from a completed lootquality isolated run."""
import argparse
import json
from pathlib import Path


def main():
    p = argparse.ArgumentParser()
    p.add_argument("run", type=Path)
    p.add_argument("--weapons-run", type=Path)
    a = p.parse_args()
    root = Path(__file__).resolve().parents[1]
    run = a.run.resolve()
    read = lambda name: json.loads((run / name).read_text(encoding="utf-8-sig"))
    result = read("result.json")
    quality = read("saves/loot-quality.json")
    craft = read("saves/craft-progression-audit.json")
    combat = read("saves/combat-upgrade.json")
    loot = read("saves/loot-balance-runtime.json")
    vanilla = read("vanilla-recipe-check.json")
    assert not vanilla["failures"]
    weapon_run = a.weapons_run.resolve() if a.weapons_run else run
    weapon_result = json.loads((weapon_run / "result.json").read_text(encoding="utf-8-sig"))
    assert weapon_result["Completed"] and not weapon_result["Failures"] and not weapon_result["TimedOut"]
    assert weapon_result["RestoredSettings"] and not weapon_result["ChangedPlayerSaves"] and not weapon_result["NewPlayerSaves"]
    weapons = json.loads((weapon_run / "saves/weapon-balance.json").read_text(encoding="utf-8-sig"))
    assert result["Completed"] and not result["TimedOut"] and not result["Failures"]
    assert result["RestoredSettings"] and not result["ChangedPlayerSaves"] and not result["NewPlayerSaves"]
    assert quality["Failures"] == craft["Failures"] == combat["Failures"] == weapons["Failures"] == 0
    assert not loot["summary"]["Failures"]
    items = {i["Type"]: i for i in loot["items"]}
    assert all(tuple(items[w["Type"]][k] for k in ("Stage","Damage","UseTime","UseAnimation"))==tuple(w[k] for k in ("Stage","Damage","UseTime","UseAnimation")) for w in weapons["Weapons"]), "Weapon measurements are stale"
    name = lambda t: items[t]["Name"]
    lines = ["# 武器掉率、材料数量与NPC商店调整", "",
        f"证据：`{run.relative_to(root)}`。本地修改及隔离实测，未发布。插件SHA-256：`{loot['pluginSha256']}`。", "",
        "## 武器掉率", "",
        "普通主路线怪物的基础武器边际掉率由约2%提高到约7%；普通精英由约6%提高到约16.8%。装备总抽取概率提高，并将池内更多份额给武器。独立配方输入装备及不同地区候选组成会使实际总率有差别，下表列出实际Roll抽样结果。",
        "Boss仍每次保证一件主力装备，有护甲候选时武器份额由75%提升到85%，护甲由25%改为15%；四职业等权。饰品保持独立有界池；各Boss另有一件独立保证掉落的标志物。",
        "升阶仍保留旧掉落：有补回历史候选时，当前候选共享70%、补回候选共享30%；同池只抽一次。", "",
        "|怪物记录|阶段|抽样击杀|武器件数|武器实际率|每1000杀鱼饵数|最大单次鱼饵数|非Boss最大材料堆|",
        "|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for r in quality["Samples"]:
        lines.append(f'|{r["Record"]}|{r["Stage"]}|{r["Kills"]}|{r["Weapons"]}|{r["Weapons"]/r["Kills"]:.2%}|{r["Bait"]*1000/r["Kills"]:.1f}|{r["MaxBait"]}|{r["MaxResourceStack"]}|')
    if quality.get("Regions"):
        lines += ["", "## 地区差异", "",
            "掉落按死亡时的实际地区生成，并按地区隔离缓存；同一种怪出现在不同地区也会使用不同的当地池。普通战利品保留少量共通补给、原有配方输入和旧阶段候选，各地区另外有突出的材料组合、不同的四职业武器选项和护甲部位。变体装备按稳定地区分配，不强求名称或生态主题匹配。",
            "阶段门槛只管材料能否获取；所有地区特产继续使用稀有度数量规则，不增加独立大堆抽取。世界Boss使用各自阶段奖励；实际神居战斗使用月亮领主档主装备和独立夜明矿奖励。普通地区设置装备强度上限，23份地区配置中没有普通可击杀怪物的区域不强行制造刷怪来源。", "",
            "以下是运行时存在普通掉落来源的地区组合；材料到其获取阶段才加入，并非初入该地区就全部解锁。这张表只展示固定地区特色；随阶段变化的主矿、Boss对照及实际神居奖励见《原版阶段与Boss掉落映射-2026-10-17.md》。", "",
            "|地区|当地突出材料|", "|---|---|",
        ]
        for r in quality["Regions"]:
            if r["Profile"]:
                lines.append(f'|{r["Area"]}|'+"、".join(f'{i["Name"]}（阶段{i["Stage"]}）' for i in r["Profile"])+"|")
        lines += ["", "地区差异同时对苍绿之径/真菌荒地、泪水之城/水晶山峰、王国边缘/深巢做掉落集合交并比检查；另用同一小爬虫在两个实际场景各2000次Roll验证地区传递和缓存隔离。", ""]
        sets={r["Area"]:{i["Type"] for i in r["Items"]} for r in quality["Regions"]}
        lines += ["阶段5的清单交并比（含保留的旧阶段物品，排除治疗、木材和石块；这是品种重合度，不是击杀出货概率）：", "",
                  "|地区组合|品种交并比|", "|---|---:|"]
        for left,right in [("苍绿之径","真菌荒地"),("泪水之城","水晶山峰"),("王国边缘","深巢")]:
            lines.append(f"|{left} / {right}|{len(sets[left]&sets[right])/len(sets[left]|sets[right]):.1%}|")
        lines.append("")
    lines += ["", "## 鱼饵与杂物", "",
        "地区附带鱼饵：普通怪从1/6、精英从1/3，改为1/200和1/100；数量从1～2改为固定1。删除每只怪额外1/150掉金色虫子的通用抽取，金色虫子仍有指定怪物来源，概率降到原先的十分之一。苔藓附带掉落从1/12降为1/50。",
        "原始掉落表中的鱼饵移到独立稀疏池（普通0.5%、精英1%，每池总概率），固定1只，禁止混入大堆材料池。箱子、渔夫奖励等主动获取鱼饵的渠道继续保留。", "",
        "## 材料数量", "",
        "原问题：把所有候选材料的总期望产量当成任意一种材料的堆数。现在每种材料独立决定数量，不再共享总堆数。下表是通用材料池在普通难度的数量；高难度沿用原来的适量数量加成。", "",
        "|通用材料分类|普通怪|精英|Boss材料池|", "|---|---|---|---|",
        "|稀有度0及以下|2～5|3～7|5～12|", "|稀有度1|1～3|2～4|3～8|",
        "|稀有度2～4|1～2|1～3|2～6|", "|稀有度5以上|1|1～2|2～4|",
        "|常规矿石（冶炼消耗较大）|2～4|3～6|6～12|", "|稀有度5以上矿石|1～3|2～5|4～8|",
        "|黑晶状体、英雄断剑、海龟壳、冰霜核等稀有升级件|1|1|1～2|", "",
        "保护制作链的小批量专门来源：狱石普通65%、2～4块，精英必出3～6；黑曜石普通60%、1～3块，精英必出2～4。秘银/山铜、蜘蛛牙及灵魂沿用已有的频繁小批量来源。Boss的独立碎片、神圣锭、机械灵魂奖励包保留其配方用途，不套用普通怪材料池。", "",
        "|验证路线|200轮平均击杀|P95击杀|最大击杀|", "|---|---:|---:|---:|",
    ]
    for r in craft["Routes"]:
        if "Route" in r:
            lines.append(f'|{r["Route"]}|{r["MeanKills"]:.1f}|{r["P95"]}|{r["Maximum"]}|')
    lines += ["", "## NPC商店按阶段上新", "",
        "每阶段均检查有新的可购武器，旧货继续保留；这里指武器相关商店的整体供给，护士等服务NPC不强行出售武器。所有常规商店武器均检查物品阶段、上架阶段以及真实40格商店中是否可见。",
        "旅商只从已经解锁的物品中选货，同一天转阶段也会重新生成库存；每次至少带一件当前可售最高阶段的武器，其余保持随机。灰冲击枪调整到阶段1，代码2与左轮手枪按阶段4处理，脉冲弓按阶段5处理。",
        "以下为运行时各阶段新增的可售武器目录；旅商是可选货池，不保证当天全部出现，月相限定商品仍遵守原条件。另对实际40格库存做了可见性检查。DPS是裸装、不含弹药的估算，不能据此把枪与无弹药武器直接比较。", "",
        "|阶段|NPC|新品|售价（吉欧）|基础DPS估算|商店特色|", "|---|---|---|---:|---:|---|",
    ]
    seen = set()
    for r in quality.get("Catalogue", quality["Shops"]):
        if not r["New"]:
            continue
        key = (r["Stage"], r["Npc"], r["Type"])
        if key in seen:
            continue
        seen.add(key)
        lines.append(f'|{r["Stage"]}|{r["NpcName"]}|{r["Name"]}|{r["Price"]/100:g}|{r["Dps"]:.1f}|{"是" if r["Exclusive"] else ""}|')
    lines += ["", "商店特色武器从普通击杀及随机钓鱼箱装备候选中移出，保留NPC购买用途：", "",
        "|阶段|武器|商店|", "|---|---|---|",
    ]
    signatures = [(0, "Katana", "商人；旅商后续亦可能出售"), (1, "Code1", "骷髅商人、旅商"),
                  (1, "ZapinatorGray", "旅商"), (4, "Code2", "骷髅商人、旅商"),
                  (4, "Gatligator", "旅商"), (4, "ZapinatorOrange", "旅商"),
                  (4, "SlapHand", "骷髅商人"), (5, "PulseBow", "旅商")]
    for stage, t, who in signatures:
        lines.append(f"|{stage}|{name(t)}|{who}|")
    lines += ["", "## NPC及基础矿物武器实测与验证边界", "",
        "以下走真实攻击和伤害路径，固定靶、裸装、测试弹药、持续补蓝，不代表移动Boss或整套装备输出。冲击枪保留原版每次命中1/50触发十倍伤害的机制，短时DPS受抽样影响；回归记录实际十倍触发，并验证单发穿透次数上限，不能用普通武器的平均DPS阈值判定随机枪故障。", "",
        "|武器|阶段|距离（游戏单位）|实测DPS|", "|---|---:|---:|---:|",
    ]
    for r in weapons["Live"]:
        lines.append(f'|{name(r["Type"])}|{r["Stage"]}|{r["Distance"]:g}|{r["Dps"]:.1f}|')
    lines += ["", f'- 成长、阶段及合成检查：{combat["Checks"]}项，0失败。',
        f'- 掉落品质及NPC检查：{quality["Checks"]}项，0失败；6种怪物各2万次实际抽取，另有掉落回归的四难度各10万次抽取。',
        f'- 掉落回归：{loot["summary"]["Checks"]}项，0失败。',
        f'- 制作与200轮采集路线回归：{craft["Checks"]}项，0失败。',
        f'- 独立原版配方比对：{vanilla["checked"]}条、0差异；自定义售卖袋借用原版贴图，按自定义物品识别。',
        f'- 全武器参数及商店／基础矿物武器实际命中：{weapons["Checks"]}项，0失败。',
        f'- 商店及基础矿物武器射击证据：`{weapon_run.relative_to(root)}`；已核对本次与该次的全部武器阶段、基础伤害和使用周期相同。',
        '- 无超时，正式存档无变化，设置恢复。合成依然只检查材料、工作站和环境，不设置阶段门槛。',
        '- 未验证所有玩家整场探索的掉落总量、全部NPC在所有月相/日期下的手动购买，以及全部装备组合的Boss通关；抽样数量是统计证据，不是每趟探索固定奖励。', "",
    ]
    out = root / "docs/掉落品质与NPC商店调整-2026-10-17.md"
    out.write_text("\n".join(lines), encoding="utf-8")
    print("Report generated; all input runs passed.")


if __name__ == "__main__":
    main()
