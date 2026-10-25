"""Collect both completed isolated suites from one final build, including exact exported values."""
import csv
import hashlib
import json
import shutil
import sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
run=Path(sys.argv[1]).resolve()
build=root/'artifacts/economy-progression-final-build'
out=root/'artifacts/economy-progression'
evidence=out/'evidence'
evidence.mkdir(parents=True,exist_ok=True)
read=lambda p:json.loads(p.read_text('utf-8-sig'))
result=read(run/'result.json')
assert result['Completed'] and not result['TimedOut'] and not result['Failures'],result
assert not result['NewPlayerSaves'],result
save_guard_passed=not result['ChangedPlayerSaves']
if not save_guard_passed:
 observation=read(out/'concurrent-save-observation.json')
 assert observation['concurrent_slot12_save_confirmed'] and observation['isolated_save_redirection_verified']
 assert observation['observed_changed_files']==result['ChangedPlayerSaves']
audit=read(run/'saves/economy-progression.json')
integration=read(run/'saves/weapon-balance.json')
assert audit['Failures']==0 and integration['Failures']==0
assert len(audit['Live'])==18 and len(integration['Live'])==10
final={x['Type']:x['After'] for x in audit['Weapons']}
assert all(x['Damage']==final[x['Type']] for x in integration['Weapons'])
for src,name in [(run/'result.json','result.json'),(run/'AutoTest.log','AutoTest.log'),(run/'saves/economy-progression.json','economy-progression.json'),(run/'saves/weapon-balance.json','weapon-balance.json')]:
 shutil.copy2(src,evidence/name)
for key,filename,fields in [('Weapons','weapons-before-after.csv',['Type','Name','Stage','Class','Before','After','Native','Rarity','UseTime','UseAnimation']),('Prices','prices-before-after.csv',['Type','Name','Stage','Before','After','Sale','IsAmmo','IsArmor','IsAccessory'])]:
 with (out/filename).open('w',encoding='utf-8-sig',newline='') as stream:
  writer=csv.DictWriter(stream,fieldnames=fields,extrasaction='ignore');writer.writeheader();writer.writerows(audit[key])
prices={x['Type']:x for x in audit['Prices']}
weapons={x['Type']:x for x in audit['Weapons']}
changed=[x for x in audit['Weapons'] if x['Before']!=x['After']]
lines=['# 武器与商店修正实测','',f"审计 {len(weapons)} 把武器、{len(prices)} 项物品；调整 {len(changed)} 把武器。专项 {audit['Checks']}/{audit['Checks']}、真实敌人集成 {integration['Checks']}/{integration['Checks']} 通过。",'', '## 基础伤害','', '| 武器 | 修改前 | 修改后 |','|---|---:|---:|']
for t in ['WoodenBow','WoodenSword','IronBow','FlintlockPistol','TerraBlade','Zenith','SDMG','LastPrism']:
 x=weapons[t];lines.append(f"| {x['Name']} | {x['Before']} | {x['After']} |")
lines+=['','## 购买价格（吉欧）','','| 商品 | 修改前 | 修改后 |','|---|---:|---:|']
for t in ['WoodenBow','IronBow','FlintlockPistol','Minishark','MusketBall','WoodenArrow','SteampunkBoiler','SimplePrefabHouse','RandomPrefabHouse','WaterCandle']:
 x=prices[t];lines.append(f"| {x['Name']} | {x['Before']/100:g} | {x['After']/100:g} |")
lines+=['','## 九把武器的假人前后对照','','裸装、无强化、普通弹药；不同阶段使用各自阶段防御，只比较同一武器修改前后。持续魔法测试补满法力，召唤使用裸装栏位。','','| 武器 | 修改前 DPS | 修改后 DPS |','|---|---:|---:|']
for t in dict.fromkeys(x['Type'] for x in audit['Live']):
 a=next(x for x in audit['Live'] if x['Type']==t and x['Before']);b=next(x for x in audit['Live'] if x['Type']==t and not x['Before']);lines.append(f"| {weapons[t]['Name']} | {a['Dps']:.1f} | {b['Dps']:.1f} |")
lines+=['','## 真实敌人回归','','目标被固定位置，用实际武器攻击；包含吸血鬼刀和泰拉刃近身/中距离、火鞭、带电爆破加农炮、天顶剑、星光、骷髅头法书近身/中距离。不是完整敌人AI战斗或完整通关。','','| 武器 | 距离 | 基础伤害 | DPS |','|---|---:|---:|---:|']
for x in integration['Live']:lines.append(f"| {weapons[x['Type']]['Name']} | {x['Distance']:g} | {x['Damage']} | {x['Dps']:.1f} |")
if not save_guard_passed:lines+=['','全局存档快照观察到运行中的另一个游戏在写12号存档；本测试为独立目录的1号槽。来源核对与原始异常记录见 concurrent-save-observation.json、evidence/result.json，未回滚其他游戏的存档。']
(out/'balance-report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
verification={'passed':True,'checks':audit['Checks']+integration['Checks'],'economy_checks':audit['Checks'],'weapon_integration_checks':integration['Checks'],'weapons_audited':len(weapons),'items_audited':len(prices),'weapons_changed':len(changed),'dummy_weapon_types':9,'dummy_before_after_runs':18,'real_enemy_attack_cases':10,'all_exported_weapon_damage_matches_between_suites':True,'runtime_run':str(run),'dll_sha256':sha(build/'TerraKnight.dll'),'global_save_guard_passed':save_guard_passed,'observed_changed_native_player_saves':result['ChangedPlayerSaves'],'native_player_saves_created':[],'isolated_save_redirection_verified':True,'concurrent_save_observation':'concurrent-save-observation.json','full_playthrough_tested':False,'two_client_tested':False,'installed':False,'source_hashes':{f:sha(root/f) for f in ['src/TerraKnight/Items/Shop.cs','src/TerraKnight/Items/WeaponProgression.cs','src/TerraKnight/Items/WeaponBalance.cs','src/TerraKnight/Items/WeaponCalibration.cs','src/TerraKnight/Items/Items.cs','tools/vanilla-data/combat/vanilla-runtime.json']}}
(out/'verification.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in verification.items() if k!='source_hashes'},ensure_ascii=False))
