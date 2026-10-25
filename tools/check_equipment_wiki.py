"""Verify restored armour attributes on all local and actually served Wiki pages."""
import argparse
import json
import re
import urllib.request
from pathlib import Path
from bs4 import BeautifulSoup

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('run',type=Path)
p.add_argument('--wiki-root',type=Path,default=Path('artifacts/wiki-preview/terraknight-wiki'))
p.add_argument('--source-root',type=Path,required=True)
p.add_argument('--base-url',default='http://127.0.0.1:8107/terraknight-wiki/')
a=p.parse_args()
read=lambda f:json.loads(f.read_text(encoding='utf-8-sig'))
result=read(a.run/'result.json')
assert result['Completed'] and not result['Failures'] and not result['TimedOut']
audit=read(a.run/'saves/armor-restore.json')
assert audit['Failures']==0
compact=lambda text:re.sub(r'\s+','',text or '')
checked=[]
for row in audit['Items']:
    slug='item-'+row['Type'].lower()+'.html'
    local=(a.wiki_root/slug).read_bytes()
    with urllib.request.urlopen(a.base_url+slug,timeout=10) as response:
        served=response.read()
    assert served==local, 'Stale served page: '+slug
    soup=BeautifulSoup(served.decode('utf-8'),'html.parser')
    stats=next(p.get_text(' ',strip=True) for p in soup.select('main p') if '基础属性' in p.get_text())
    defense=re.search(r'防御\s+(\d+)',stats)
    assert defense and int(defense[1])==row['Defense'],(slug,row['Defense'],stats)
    text=compact(soup.main.get_text())
    assert compact(row['Tooltip']) in text,(slug,row['Tooltip'])
    checked.append({'Type':row['Type'],'Defense':row['Defense']})

source=(a.source_root/'src/TerraKnight/Items/Equipment.cs').read_text(encoding='utf-8-sig')
source=source.split('static string OriginalSetBonusText',1)[1]
sets={}
for labels,quoted in re.findall(r'((?:case ArmorSet\.\w+:\s*)+)return\s+("(?:\\.|[^"\\])*")',source):
    value=json.loads(quoted)
    for enum in re.findall(r'case ArmorSet\.(\w+)',labels):
        sets[enum]=value
with urllib.request.urlopen(a.base_url+'systems.html',timeout=10) as response:
    systems=response.read()
assert systems==(a.wiki_root/'systems.html').read_bytes()
system_text=compact(BeautifulSoup(systems.decode('utf-8'),'html.parser').main.get_text())
for enum,description in sets.items():
    assert compact(description) in system_text,(enum,description)
Path('artifacts/equipment-wiki-check.json').write_text(json.dumps({'ArmorPages':len(checked),'SetDescriptions':len(sets),'Failures':[],'Pages':checked},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(checked)} armour pages and {len(sets)} set descriptions match current source/runtime and served bytes')
