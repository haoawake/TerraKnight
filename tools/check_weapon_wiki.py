"""Compare every weapon page, including bytes actually served, with the game audit."""
import argparse
import json
import re
import urllib.request
from pathlib import Path
from bs4 import BeautifulSoup

p=argparse.ArgumentParser()
p.add_argument('run',type=Path)
p.add_argument('--wiki-root',type=Path,default=Path('artifacts/wiki-preview/terraknight-wiki'))
p.add_argument('--base-url',default='http://127.0.0.1:8107/terraknight-wiki/')
p.add_argument('--output',type=Path,default=Path('artifacts/weapon-wiki-check.json'))
a=p.parse_args()
read=lambda f:json.loads(f.read_text(encoding='utf-8-sig'))
result=read(a.run/'result.json')
assert result['Completed'] and not result['Failures'] and not result['TimedOut']
data=read(a.run/'saves/weapon-balance.json')
checked=[]
for row in data['Weapons']:
    slug='item-'+row['Type'].lower()+'.html'
    local=(a.wiki_root/slug).read_bytes()
    with urllib.request.urlopen(a.base_url+slug,timeout=10) as response:
        assert response.status==200
        served=response.read()
    assert served==local, 'Stale served page: '+slug
    soup=BeautifulSoup(served.decode('utf-8'),'html.parser')
    stats=next((p.get_text(' ',strip=True) for p in soup.select('main p') if '基础属性' in p.get_text()),'')
    damage=re.search(r'伤害\s+(\d+)(?:[；。\s]|$)',stats)
    assert damage and int(damage[1])==row['Damage'], (slug,row['Damage'],stats)
    assert '\ufffd' not in soup.h1.get_text(), 'Corrupted title: '+slug
    checked.append({'Type':row['Type'],'Damage':row['Damage'],'Url':a.base_url+slug})
a.output.write_text(json.dumps({'Checked':len(checked),'Failures':[],'Pages':checked},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(checked)} weapon pages match actual game damage and served bytes')
