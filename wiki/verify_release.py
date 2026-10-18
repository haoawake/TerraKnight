"""Check published bytes against the reviewed build, including every boss seal's page."""
import concurrent.futures
import hashlib
import json
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'artifacts/wiki-preview/terraknight-wiki'

def main():
    origin = sys.argv[1] if len(sys.argv)>1 else 'http://127.0.0.1:8098'
    paths = ['build-info.json','index.html','search-index.json','enemies.html','recipes.html','audit.html',
             'enemy-falseknight.html','enemy-finalboss.html','enemy-hornet-4.html','item-rodofdiscord.html',
             'item-copperwatch.html','assets/wiki.js','assets/wiki.css','assets/illustrations/manifest.json',
             'assets/illustrations/items.webp','assets/illustrations/enemies.webp','downloads/encyclopedia.md']
    seals = sorted(p.name for p in OUT.glob('item-bosssigil*.html'))
    # Every boss seal's page (39: the God Tamer's place left in the item list has none).
    assert len(seals) >= 39
    paths += seals
    build = json.loads((OUT/'build-info.json').read_text(encoding='utf-8'))
    token = build['source_sha256'][:12]
    def check(path):
        request = urllib.request.Request(origin+'/terraknight-wiki/'+path+'?release='+token,headers={'Cache-Control':'no-cache'})
        with urllib.request.urlopen(request,timeout=180) as response:
            actual = response.read()
        expected = (OUT/path).read_bytes()
        assert hashlib.sha256(actual).digest()==hashlib.sha256(expected).digest(), 'Published content mismatch: '+path
        return path
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        checked = list(pool.map(check, paths))
    print(json.dumps({'origin':origin,'matching_files':len(checked),'boss_seals':len(seals),'build':build},ensure_ascii=False))

if __name__=='__main__':
    main()
