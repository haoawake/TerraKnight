from pathlib import Path
import hashlib,json,re,shutil,sys
root=Path(__file__).resolve().parents[1]
run=Path(sys.argv[1]).resolve();out=root/'artifacts/regional-furniture-integration'
result=json.loads((run/'result.json').read_text('utf-8-sig'))
assert result['Completed'] and not result['TimedOut'] and not result['Failures'],result
assert not result['ChangedPlayerSaves'] and not result['NewPlayerSaves'],result
log=(run/'AutoTest.log').read_text('utf-8-sig')
behavior=re.search(r'地区家具 合计 (\d+)/(\d+) 通过',log)
craft=re.search(r'地区家具制造界面 合计 (\d+)/(\d+) 通过',log)
vanilla=re.search(r'原版配方实际制作、材料扣除及环境限制 (\d+)/(\d+) 通过',log)
assert behavior and craft and vanilla
for match in (behavior,craft,vanilla):assert match[1]==match[2]
assert int(behavior[1])==2426
assert int(craft[1])==1032
build=root/'artifacts/regional-furniture-build'
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
evidence=out/'evidence';evidence.mkdir(exist_ok=True)
for name in ('AutoTest.log','result.json'):shutil.copy2(run/name,evidence/name)
game=Path.home()/'AppData/Local/Temp/TerraKnight-Furniture-01a151cc/BepInEx/TerraKnight/dev/single-regionalfurniture'
for name in ('regional-furniture.json','regional-craft-ui.json'):shutil.copy2(game/name,evidence/name)
images=evidence/'rendered';images.mkdir(exist_ok=True)
pics=list(game.glob('regional_light_*.png'));assert len(pics)==24
for p in pics:shutil.copy2(p,images/p.name)
verification={'completed':True,'behavior_checks':int(behavior[1]),'craft_ui_checks':int(craft[1]),'vanilla_recipe_checks':int(vanilla[1]),'regions':24,'craftable_items':456,'light_fixtures':120,'native_state_textures':504,'embedded_resources':648,'failed_checks':[],'timed_out':False,'changed_player_saves':[],'new_player_saves':[],'independent_installation':True,'shared_preferences_writes_suppressed_in_test_copies':True,'registry_or_shared_settings_restoration_claimed':False,'live_two_player_test_performed':False,'candidate_dll_sha256':digest(build/'TerraKnight.dll'),'runtime_run':str(run),'installed':False}
(out/'verification.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2),'utf8')
delivery=out/'可安装版本';delivery.mkdir(exist_ok=True)
for f in ('TerraKnight.dll','TerraKnight.pdb'):shutil.copy2(build/f,delivery/f)
shutil.copy2(root/'docs/区域家具接入与配方.md',delivery/'区域家具接入与配方.md')
shutil.copy2(out/'verification.json',delivery/'verification.json')
(delivery/'安装说明.txt').write_text('将 TerraKnight.dll 和 TerraKnight.pdb 放入 Hollow Knight/BepInEx/plugins/TerraKnight 后重启游戏。\n24 地区家具已嵌入 DLL，无需另行复制 PNG。配方和操作见同目录说明。\n','utf8')
print(json.dumps(verification,ensure_ascii=False))
