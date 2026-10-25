"""Render the reviewed Terraria mapping using a completed isolated runtime audit."""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ANALOGUES = {
    "FalseKnight": "史莱姆王", "BigFly": "肉前史莱姆系召唤支线", "Mawlek": "肉前稀有战斗奖励",
    "Hornet": "克苏鲁之眼", "MegaMossCharger": "肉前丛林支线", "GhostAladar": "肉前血月支线",
    "GhostNoEyes": "肉前魔法剑稀有支线", "MantisLord": "骷髅王／地牢", "MageLord": "地牢魔法支线",
    "MegaBeamMiner": "陨石／地狱探索", "DungDefender": "世界吞噬怪", "FlukeMother": "克苏鲁之脑",
    "MegaJellyfish": "肉前海洋支线", "GhostHu": "肉前猩红支线", "GhostMarmu": "困难初期受击反击支线",
    "TraitorLord": "世纪之花", "FalseKnight@5": "独眼巨鹿", "BlackKnight": "血肉墙近战分支",
    "JarCollector": "困难初期法力生存支线", "MimicSpider": "宝箱怪", "GhostGalien": "困难初期低血防护支线",
    "GhostXero": "地牢探索支线", "MageLord@5": "血肉墙魔法分支", "HiveKnight": "蜂王主题＋同期困难装备",
    "Hornet@4": "机械Boss期战斗支线", "InfectedKnight": "血肉墙召唤分支", "InfectedKnight@5": "史莱姆皇后",
    "GhostMarkoth": "机械Boss期射击进阶", "Oblobble": "双子魔眼／机械Boss期", "LobsterLancer": "石巨人",
    "Grimm": "霜月／节日事件", "NightmareGrimm": "光之女皇", "WhiteDefender": "猪龙鱼公爵",
    "GreyPrince": "月前事件支线", "HollowKnight": "月前最终挑战", "NailBros": "日耀／月前近战分支",
    "Paintmaster": "星云／月前魔法分支", "Nailsage": "星璇／月前战斗分支",
    "HollowKnightPrime": "月前终局挑战", "FinalBoss": "月亮领主",
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path)
    parser.add_argument("--weapons-run", type=Path)
    args = parser.parse_args()
    run = args.run.resolve()
    read = lambda p: json.loads((run / p).read_text(encoding="utf-8-sig"))
    result = read("result.json")
    quality = read("saves/loot-quality.json")
    loot = read("saves/loot-balance-runtime.json")
    vanilla = read("vanilla-recipe-check.json")
    combat = read("saves/combat-upgrade.json")
    craft = read("saves/craft-progression-audit.json")
    weapon_run = args.weapons_run.resolve() if args.weapons_run else run
    weapons = json.loads((weapon_run / "saves/weapon-balance.json").read_text(encoding="utf-8-sig"))
    weapon_result = json.loads((weapon_run / "result.json").read_text(encoding="utf-8-sig"))
    assert weapon_result["Completed"] and not weapon_result["TimedOut"] and not weapon_result["Failures"]
    assert weapons["Failures"] == 0 and len(weapons["Live"]) == 19
    item_map = {i["Type"]: i for i in loot["items"]}
    assert all(all(item_map[w["Type"]][k] == w[k] for k in ("Stage", "Damage", "UseTime", "UseAnimation")) for w in weapons["Weapons"])

    assert result["Completed"] and not result["TimedOut"] and not result["Failures"]
    assert result["RestoredSettings"] and not result["ChangedPlayerSaves"] and not result["NewPlayerSaves"]
    assert not quality["Failures"] and not vanilla["failures"]
    assert len(quality["MineralRoutes"]) == 20
    bosses = quality["BossMapping"]
    assert set(ANALOGUES) == {b["Record"] for b in bosses}
    assert len({b["Signature"] for b in bosses}) == len(bosses)
    lines = [
        "# 原版阶段与Boss掉落映射（2026-10-17）", "",
        "这是已实现并通过隔离运行验证的获取路线。以下原版规则来自官方Wiki；空洞Boss与泰拉Boss的对应关系是本Mod的设计映射，不是两款原作共有的设定。", "",
        "## 原版查询依据", "",
        "- [原版流程](https://terraria.wiki.gg/wiki/Guide:Game_progression)：肉前、血肉墙、机械Boss、世纪之花、石巨人、月亮事件与月亮领主。",
        "- [原版矿物](https://terraria.wiki.gg/wiki/Ores)：钴／钯→秘银／山铜→精金／钛金；叶绿矿需要机械Boss阶段的采掘工具。",
        "- [原版宝藏袋](https://terraria.wiki.gg/wiki/Treasure_Bag)：克盾、蠕虫围巾、混乱之脑、骨手套、蜂巢背包、孢子囊、闪亮石等标志性奖励。",
        "- [克苏鲁之盾](https://terraria.wiki.gg/wiki/Shield_of_Cthulhu)：原版在克苏鲁之眼专家袋内保证获得。本Mod按用户指定映射到大黄蜂，普通模式也直接掉落。",
        "- [月亮领主](https://terraria.wiki.gg/wiki/Moon_Lord)：普通模式夜明矿70–90。神居Boss采用这一基础数量，专家／大师继续应用本Mod既有数量加成。", "",
        "## 阶段、活动区域与产物", "",
        "玩家阶段按取得的不同移动能力数量计算，顺序不限；计入蛾翼披风、螳螂爪、水晶之心、帝王之翼、暗影披风，每种只计一次。区域归属、怪物原始路线与实际死亡场景共同决定获取路线。下表能力和地区仅为常规路线示例，不是阶段的固定能力门槛。", "",
        "|阶段|能力进度|主要活动范围|对应原版产物|", "|---|---|---|---|",
        "|0|开局|国王山道、遗忘十字路；苍绿前段可提前探索|铜／锡、铁／铅、银／钨、金／铂金；基础宝石|",
        "|1|蛾翼披风|苍绿之径、真菌荒地、雾之峡谷、呼啸悬崖|魔矿、猩红矿；蜂蜡、坚固化石；保留基础矿|",
        "|2|螳螂爪|泪水之城、水晶山峰、皇家水道、安息之地|陨石、狱石、黑曜石；骨头、暗影鳞片、组织样本|",
        "|3|水晶之心|远古盆地、深巢、野兽巢穴；花园部分路线|钴、钯、秘银、山铜；蜘蛛牙及困难初期装备|",
        "|4|帝王之翼|王国边缘、蜂巢、斗兽场；深渊须另满足王之印记|精金、钛金、叶绿矿；神圣锭与机械灵魂|",
        "|5|暗影披风及后期支线|叛徒领主、白色宫殿、后期梦境、格林剧团、神居|世纪之花／石巨人／事件装备→四柱碎片→夜明矿与月亮领主装备|", "",
        "阶段5压缩了原版多个后期小阶段，具体靠不同Boss的专属奖励、材料组合和配方材料区分。神居是明确的终局奖励区域：真实进入神居战斗后，奖励按终局处理，不继续套早期Boss的原始档位。白宫还需要觉醒梦之钉等原版条件；区域标签不代替这些原版进入条件。", "",
        "普通地区装备上限按当地基础档加一档，最高5档；矿物一般最高到叶绿／精金／钛金档，神居才以夜明矿为主矿。王后花园后期主矿明确为叶绿矿。旧地区不会随全局升阶直接变成终局武器刷取点。", "",
        "地区矿物随阶段更新，保留较低档矿物供合成使用。历史装备候选继续共享原有池的30%份额；普通材料的独立数量范围、稀有材料单件规则、少量鱼饵规则不变。狱石、黑曜石等独立冶炼补给不属于同一个矿物抽取池。", "",
        "## 20种原版可冶炼矿物的同期来源", "",
        "这20种矿石定义本来就存在，本次补的是阶段与地区获取覆盖，不能称作新增20种物品。以下是实际加载物品表后，在同期可用怪物／Boss记录中找到的来源；并非只检查枚举存在。", "",
        "|矿石|阶段|实际来源举例|来源记录数|", "|---|---:|---|---:|",
    ]
    entries = dict(re.findall(r'\["([^"]+)"\] = new Entry \{ En = "[^"]*", Zh = "([^"]+)"',
                              (ROOT / "src/TerraKnight/Items/EnemyDropData.cs").read_text(encoding="utf-8-sig")))
    entries.update({b["Record"]: b["Zh"] for b in bosses})
    areas = {record: region["Area"] for region in quality["Regions"] for record in region["Records"]}
    for ore in sorted(quality["MineralRoutes"], key=lambda r: (r["Stage"], r["Type"])):
        names = "、".join(entries.get(k, k) + ("（" + areas[k] + "）" if k in areas else "") for k in ore["Sources"][:6])
        lines.append(f'|{ore["Name"]} `{ore["Type"]}`|{ore["Stage"]}|{names}|{len(ore["Sources"])}|')
    lines += ["", "另验证七种宝石、黑曜石、坚固化石均有同期来源；补齐水晶山峰红玉／钻石和雾之峡谷翡翠的明确地区路线。神圣锭、蘑菇矿锭、幽灵锭是锭／加工材料，不伪造对应矿石；继续使用原版材料和工作站合成。自动锤炼机、灵气、蘑菇矿锭和幽灵锭统一归入阶段5的世纪之花后加工链，叶绿矿与叶绿装备仍为阶段4。", "",
              "## 每个Boss的独特标志物与原版对照", "",
              "40个正式Boss奖励记录分别有不同的保证掉落标志物，主奖励仍每次给一件；四职业主武器等权，有合格的同档护甲时85%武器／15%护甲，否则给武器。原版专家专属物品在本Mod扩展到普通掉落；专家／大师放入对应宝藏袋。常用合成饰品可保留普通获取路线，不把材料链全部改成Boss独占。", "",
              "克盾、皇家凝胶、蠕虫围巾、混乱之脑、骨手套、骨头头盔、蜂巢背包、孢子囊、闪亮石、翱翔徽章、重力球从普通怪及钓鱼箱装备池中移出，防止标志物被杂兵随机提前供给。", "",
              "表中的‘支线／分支’表示同强度或功能参照，并不声称该Boss完整复制一个泰拉Boss的所有掉落。梦境强化版若有独立记录会单列；共用记录的场景变体继续共用该记录的标志物，但保留各自原有圣物和宝藏袋外观。", "",
              "|空洞Boss／记录|阶段|原版对照|独立保证标志物|实际主武器池|", "|---|---:|---|---|---|"]
    for b in bosses:
        lines.append(f'|{b["Zh"]} `{b["Record"]}`|{b["Stage"]}|{ANALOGUES[b["Record"]]}|{b["SignatureName"]}|'+"、".join(w["Name"] for w in b["Weapons"])+"|")
    lines += ["", "## 探索饰品改为开局小怪获取", "",
              "Boss掉落表移除飞毯、云朵瓶、气球、再生手环、星力手环、花靴、水母项链、宝藏磁铁等基础探索物品，也不再混入普通来源的钴护盾、岩浆石、步枪瞄准镜等低档奖励。这些物品保留普通获取与原版配方用途。",
              "以下新增的明确来源均从阶段0可刷，继续共享普通怪2%／精英5%的饰品池，减少打Boss出探索杂物的情况。飞毯等并非独占于这张表，已有普通掉落来源仍保留。", "",
              "|小怪|物品|阶段|普通难度单件边际率|", "|---|---|---:|---:|"]
    for route in quality["ExplorationRoutes"]:
        lines.append(f'|{route["Zh"]} `{route["Record"]}`|{route["Name"]}|{route["Stage"]}|{route["Chance"]:.3%}|')
    lines += ["", f'开局小爬虫实际20000次Roll获得飞毯{quality["FlyingCarpetsSample"]}件；Boss首杀／重战表均验证不再包含上述基础物品。Boss主武器和护甲候选要求同档，不再用早期物品占用后期Boss的主奖励。', ""]
    lines += ["", "大黄蜂另给30–90魔矿或猩红矿（二选一）；粪虫防御者给80–110魔矿＋20–40暗影鳞片；吸虫之母给80–110猩红矿＋20–40组织样本。蜂巢骑士给17–29蜂蜡，并额外从养蜂人、蜂膝弓、蜜蜂枪中给一件，同时保留本区域对应强度的主装备。石巨人对应的神之驯服者给18–23甲虫鞘翅，其他后期Boss不再统一发鞘翅。", "",
              "叛徒领主映射世纪之花：种子弯刀、维纳斯万能枪、吹叶机、矮人法杖；白色防御者对应猪龙鱼公爵武器；梦魇格林对应光之女皇武器；辐光对应月亮领主武器。不同职业数目不足的原版Boss保留Mod的四职业补足规则。", "",
              "## 神居实杀与跨场景宝藏袋", "",
              "真实神居战斗的主奖励改用彩虹猫之刃、太空海豚机枪、终极棱镜、月亮传送门法杖，护甲候选为日耀、星璇、星云、星尘套。每次Boss击杀另给基础70–90夜明矿；普通怪为40%给1–3、精英为80%给1–3。普通区域的GG前缀例外（皇家水道、苍白潜伏者等）按实际地区识别，不误发神居奖励。", "",
              "夜明矿在击杀时发放，专家／大师宝藏袋保留神居来源标记；带回城镇、存档恢复后再开袋仍开终局装备，不重复发矿。神居重战标记与首次击败标记分开，重战袋不会误显示首次击败。", "",
              "|难度|神居大黄蜂实杀夜明矿|击杀＋城镇开袋后克盾数|宝藏袋数|", "|---|---:|---:|---:|"]
    for sample in quality["GodhomeSamples"]:
        lines.append(f'|{sample["Difficulty"]}|{sample["LunarOre"]}|{sample["Shield"]}|{sample["Bags"]}|')
    lines += ["", "## 验证与边界", "",
              f'运行证据：`{run.relative_to(ROOT)}`；插件SHA-256：`{loot["pluginSha256"]}`。', "",
              f'- 掉落品质及映射：{quality["Checks"]}项、0失败，包含40个Boss标志物、同期矿物／宝石来源、地区装备上限、真实Roll、存档来源标记和城镇实际开袋。',
              f'- 武器参数和19件实际攻击验证：{weapons["Checks"]}项、0失败；证据 `{weapon_run.relative_to(ROOT)}`。已逐项核对本次全部武器的阶段、伤害和使用周期一致，沿用9件基础矿武器和10件商店武器实射；本轮没有修改这些参数。',
              '- 同一套回归还覆盖升阶保留、无未来阶段掉落、四职业材料链、NPC上新与商店独特武器、鱼饵数量、独立材料堆数，以及有材料时在阶段0实际合成后期装备。',
              f'- 原版配方独立比较：0处差异；完整结果见 `{(run / "vanilla-recipe-check.json").relative_to(ROOT)}`。原版材料数量、工作站、环境条件保留，合成不增加阶段门。',
              '- 测试正常完成、无超时，正式玩家存档无改变／新增，游戏设置已恢复。',
              '- 基础矿武器提前到阶段0后，现有阶段预算同步重新计算9件金／铂金／钨武器的伤害；补跑这9件及10件商店武器实射。冲击枪保留原版1/50十倍伤害，实测记录触发次数与穿透上限，短时抽样不强行当作稳定DPS。神居战斗倍率未改；不以此代替全Boss实际战斗难度体验测试。',
              '- 本地实现与报告已更新，尚未发布版本或部署线上Wiki。', ""]
    output = ROOT / "docs/原版阶段与Boss掉落映射-2026-10-17.md"
    output.write_text("\n".join(lines), encoding="utf-8-sig")
    print(output)


if __name__ == "__main__":
    main()
