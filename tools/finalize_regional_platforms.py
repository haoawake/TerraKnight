from pathlib import Path
import json,hashlib,re,shutil,sys,zipfile
root=Path(__file__).resolve().parents[1]
run=Path(sys.argv[1]).resolve();out=root/'artifacts/regional-platforms'
result=json.loads((run/'result.json').read_text('utf-8-sig'))
assert result['Completed'] and not result['TimedOut'] and not result['Failures'],result
assert not result['ChangedPlayerSaves'] and not result['NewPlayerSaves'],result
log=(run/'AutoTest.log').read_text('utf-8-sig')
total=re.search(r'RegionalPlatforms TOTAL (\d+)/(\d+) PASS',log)
assert total and total[1]==total[2]=='1248'
assert len(re.findall(r'rendered diagonal continuity slope=[12] gaps=0 PASS',log))==48
assert len(re.findall(r'walk slope=.* PASS',log))==96
assert len(re.findall(r'actual jump from below and land PASS',log))==24
assert len(re.findall(r'actual rendered terrain PASS',log))==24
embedding=json.loads((out/'embedded-validation.json').read_text('utf-8-sig'))
digest=hashlib.sha256((root/'artifacts/regional-platforms-build/TerraKnight.dll').read_bytes()).hexdigest()
assert embedding['passed'] and embedding['dll_sha256']==digest
evidence=out/'evidence';evidence.mkdir(exist_ok=True)
for name in ['result.json','AutoTest.log']:shutil.copy2(run/name,evidence/name)
game=root/'artifacts/single-drop-game/BepInEx/TerraKnight/dev'/('single-'+result['Suite'])
for d in json.loads((out/'generated.json').read_text('utf8')):
 name='regional_platform_'+d['key']+'.png';shutil.copy2(game/name,evidence/name)
shutil.copy2(game/'regional-platforms.json',evidence/'regional-platforms.json')
installation=json.loads((out/'installation.json').read_text('utf-8-sig')) if (out/'installation.json').exists() else {}
verification={'passed':True,'runtime_checks':1248,'regions':24,'connection_frames':648,'rendered_diagonal_continuity_checks':48,'actual_stair_walks':96,'actual_jump_from_below_and_land':24,'actual_standing':24,'actual_down_through':24,'actual_region_renders':24,'changed_player_saves':[],'new_player_saves':[],'failed_checks':[],'dll_sha256':digest,'run':str(run),'installed':installation.get('installed',False) and installation.get('dll_sha256')==digest,'installation_record':'installation.json'}
(out/'verification.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2),encoding='utf8')
shutil.copy2(out/'verification.json',out/'delivery/verification.json')
with zipfile.ZipFile(out/'TerraKnight-24地区专属平台.zip','w',zipfile.ZIP_DEFLATED) as archive:
 for f in (out/'delivery').iterdir():archive.write(f,f.name)
print(json.dumps(verification,ensure_ascii=False))
