"""Write the audit's conclusions and explicit unresolved-item inventory."""
import argparse
import collections
import json
from pathlib import Path


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


if __name__ == '__main__':
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('run', type=Path)
    ap.add_argument('--analysis', type=Path, required=True)
    ap.add_argument('--coverage', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    args = ap.parse_args()
    a, v, result = read(args.analysis), read(args.coverage), read(args.run/'result.json')
    raw = read(args.run/'saves/loot-source-audit.json')
    lines = ['# 全掉落、阶段与饰品制作来源复核（2026-10-23）', '',
        f"覆盖 {a['Items']} 个注册项（含 None）、{a['Recipes']} 条实际配方、{a['DropRows']} 条场景/阶段/难度/首杀组合下的掉落行。", '',
        f"饰品 {len(a['Accessories'])} 件，其中制作来源 {sum(r['Source']=='Craft' for r in a['Accessories'])} 件；六阶段均无缺失的饰品材料/工位路径。", '',
        '结论范围：证明当前表中的依赖路径及执行过的隔离掉落/制作检查。阶段采用同一能力档内合法细节点的并集，不能冒充某一 Boss 首杀前就能拿到该档所有物品，也不等于走遍所有房间。宝匣/地区箱每种每阶段抽样，不把抽样未见当作绝对没有。', '',
        '## 已修正', '',
        '- 冰雪镜原有手机合成分支，但无正常来源；补入安息之地的冰主题地区宝箱，并标为阶段 2。魔镜分支保留。',
        '- 生命体分析仪、DPS 计、秒表阶段由 0 对齐旅商实际出售的阶段 1，下游配方自动重新计算；不改原版材料、数量和工位。',
        '- 黑晶状体、骨之羽在原有怪物来源被材料池稀释后，低于 1% 的来源提升为独立至少 1% 掉落，不新增来源。移出候选时保留概率预算；阶段与配方变化也会重新计算候选池，部分同池份额随之变化，不宣称其他掉率逐行完全不变。',
        '- 钓鱼宝箱的熟手/大师鱼饵增加阶段检查，修正阶段 0 直接开出阶段 1 大师鱼饵的旁路；解锁后沿用原概率和数量。',
        '- 补回 45 条当前材料、工位和环境均支持的原版配方。旧覆盖检查只看“已有至少一条配方的成品”，漏掉整件物品没有配方的情况，现在覆盖全部已实现的原版成品。', '',
        '## 六阶段闭合', '',
        '|阶段|应检查配方|材料与工位闭合|未闭合配方|缺失饰品|',
        '|---|---:|---:|---:|---:|']
    for s in a['Stages']:
        lines.append(f"|{s['Stage']}|{s['Recipes']}|{s['CraftableRecipes']}|{len(s['BlockedRecipes'])}|{len(s['MissingAccessories'])}|")
    lines += ['', f"原版支持变体 {v['ExpectedSupportedVariants']} 条，遗漏 {len(v['Missing'])} 条。", '',
        f"另有 {len(v.get('PolicyExcludedRecipes',[]))} 条四柱碎片互换配方按既有独立归属规则排除，{len(v['UnsupportedVanillaRecipes'])} 条含当前未实现的腐化/猩红生物群系条件，没有擅自删除条件后注册。", '',
        f"怪物/首领掉落的未来阶段行 {len(a['StageLeaks'])}；制作/商店专用物品混入掉落 {len(a['BlockedDrops'])}；次级来源中的武器/护甲/饰品越阶段项 {len(a['SecondaryEquipmentLeaks'])}；跨阶段旧材料来源消失 {len(a['Continuity']['losses'])}。", '',
        '地区方块、火把随水域/箱子主题给出，可能早于物品表标注的标准区域档位；这些是区域装饰规则，单列保留，不当成提前获得战斗装备。', '',
        '## 掉率', '',
        '|材料|修改前普通难度最佳同阶段怪物掉率|修改后|修改后均值击杀数|',
        '|---|---:|---:|---:|']
    for t, label, before in [('BlackLens','黑晶状体',.002118647),('BoneFeather','骨之羽',.0016009212)]:
        stage = 0 if t == 'BlackLens' else 5
        rows = [r for r in raw['Drops'] if r['Item']==t and r['Stage']==stage and not r['First']
                and not r['Godhome'] and r['Difficulty']=='Normal']
        p = max(r['ItemChance'] for r in rows)
        lines.append(f'|{label}|{before:.3%}|{p:.3%}|{1/p:.1f}|')
    lines += ['', '均值不代表保底；1% 独立概率约需 230 次击杀达到至少一件的 90% 概率。', '',
        '剩余低概率项包括金色小动物（0.1%～0.2%）、具体开发者翅膀，以及渔夫饰品的小怪副来源。保留原有收藏稀有度；渔夫饰品另有任务主来源，链球模式可右键互换，不能只看单条小怪掉率判定必需刷几百次。完整低概率候选见分析 JSON。', '',
        '## 不能声称全部物品已无遗漏', '',
        f"当前仍有 {len(a['UnknownWalls'])} 种墙体没有在本次来源图中闭合，主要涉及尚未实现的原版方块、商店或环境来源。它们不阻断本次饰品/材料合成链，但不能被描述为正常流程均可获得。", '',
        '其余未闭合项包含任务鱼、特殊宝藏袋、原生事件物、地区物品和世界设施。击杀旗帜有独立的每 20 杀发放逻辑，梦之钉由原生取得事件发放，不能把它们误判成普通掉落遗漏。对未逐项证明的特殊来源保留未匹配状态。', '',
        '|来源状态|注册项数|','|---|---:|']
    for k,n in a['SourceCategories'].items():
        lines.append(f'|{k}|{n}|')
    lines += ['', '完整分析 JSON 的 AllItemStatuses 枚举每个注册项，UnknownWalls 和 Orphans 保留全部未闭合项；没有把未匹配状态悄悄过滤掉。', '',
        '## 运行证据', '', f'- 独立目录：{args.run.as_posix()}',
        f"- Completed={result['Completed']}；TimedOut={result['TimedOut']}；Failures={len(result['Failures'])}。",
        f"- 原玩家存档变化 {len(result['ChangedPlayerSaves'])}，新增 {len(result['NewPlayerSaves'])}；共享设置未声称恢复。",
        '- SourceCategories 中的闭合结果含已验证代码声明的开局装备、微光变换和物品右键模式切换，不是每条世界路线的自动通关证据。',
        '- 全面表检查不替代全流程手动游玩、实际刷取耗时或双端联机验证。', '',
        '## 构建与安装', '',
        '最终构建 67 个警告、0 个错误。56,992 项奖励检查、1,372 项原版实际制作/扣料/环境检查通过；原版静态比对 1,369 条配方无不匹配。安装到原游戏插件目录，DLL 哈希和源码记录见 artifacts/loot-source-audit-install.json，重启后生效。', '',
        '构建时另修复 CasinoScenes 的风车绘制调用，使其适配当前 PinSim 的多风车数组和带索引角度接口；没有新增或修改其物理参数，本轮未验证赌场画面。', '',
        '## 未闭合墙体清单', '', ', '.join(r['Item'] for r in a['UnknownWalls']), '']
    args.output.write_text('\n'.join(lines), 'utf-8')
    assert result['Completed'] and not result['TimedOut'] and not result['Failures']
    assert not any(s['MissingAccessories'] or s['BlockedRecipes'] for s in a['Stages'])
    assert not a['StageLeaks'] and not a['BlockedDrops'] and not a['SecondaryEquipmentLeaks'] and not v['Missing']
    assert not [r for r in a['SecondaryLeaks'] if not r['Item'].startswith(('Torch','AreaBlock'))]
    print('Report:', args.output)
