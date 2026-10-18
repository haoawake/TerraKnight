"""Narrowly scoped BaoTa API client. Secrets are read from stdin/getpass only."""
import argparse
import getpass
import hashlib
import json
import re
import ssl
import sys
import time
import uuid
import zipfile
import urllib.parse
import urllib.request
from pathlib import Path


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--panel', required=True)
    p.add_argument('--key-stdin', action='store_true')
    p.add_argument('--self-signed', action='store_true')
    p.add_argument('action', choices=['inspect', 'deploy', 'homepage', 'theme', 'illustrations', 'update-check', 'update'])
    p.add_argument('--bundle', type=Path)
    a = p.parse_args()
    key = sys.stdin.readline().strip() if a.key_stdin else getpass.getpass('BaoTa API key: ')
    if not key:
        raise SystemExit('Missing API key')
    context = ssl._create_unverified_context() if a.self_signed else ssl.create_default_context()

    def call(endpoint, **data):
        timestamp = str(int(time.time()))
        data.update(request_time=timestamp, request_token=hashlib.md5(
            (timestamp + hashlib.md5(key.encode()).hexdigest()).encode()).hexdigest())
        req = urllib.request.Request(a.panel.rstrip('/') + endpoint,
            data=urllib.parse.urlencode(data).encode(), headers={'User-Agent': 'TerraKnightWiki/1.0'})
        with urllib.request.urlopen(req, context=context, timeout=45) as response:
            return json.loads(response.read().decode('utf-8'))

    if a.action == 'update-check':
        result = call('/files?action=GetFileBody', path='/www/server/panel/class/files.py')
        if not result.get('status'):
            raise SystemExit('Unable to inspect directory move API')
        source = result['data']
        match = re.search(r'    def MvFile\(', source)
        if not match:
            raise SystemExit('Move API not found')
        end = source.find('\n    def ', match.end())
        print(source[match.start():end])
        print(json.dumps({'wiki':call('/files?action=GetDir', path='/www/wwwroot/homepage/terraknight-wiki')}))
    elif a.action == 'inspect':
        sites = call('/data?action=getData', table='sites', limit=100, p=1)
        if isinstance(sites, dict) and isinstance(sites.get('data'), list):
            print(json.dumps({'sites': [{k: row.get(k) for k in ('id', 'name', 'path', 'status', 'ps')}
                                      for row in sites['data']]}, ensure_ascii=True))
            domains = call('/data?action=getData', table='domain', limit=100, p=1)
            print(json.dumps({'domains': domains}, ensure_ascii=True))
            print(json.dumps({'nginx': call('/files?action=GetFileBody', path='/www/server/panel/vhost/nginx/123.207.78.205.conf')}, ensure_ascii=True))
            print(json.dumps({'root': call('/files?action=GetDir', path='/www/wwwroot/homepage')}, ensure_ascii=True))
            print(json.dumps({'homepage': call('/files?action=GetFileBody', path='/www/wwwroot/homepage/index.html')}, ensure_ascii=True))
        else:
            print(json.dumps(sites, ensure_ascii=True))
    elif a.action in ('deploy', 'illustrations', 'update'):
        if not a.bundle or not a.bundle.is_file():
            raise SystemExit('Reviewed ZIP bundle is required')
        with zipfile.ZipFile(a.bundle) as archive:
            illustration_files = {'assets/wiki.css', 'assets/wiki.js', 'assets/illustrations/items.webp',
                                  'assets/illustrations/enemies.webp', 'assets/illustrations/manifest.json'}
            if a.action == 'illustrations' and set(archive.namelist()) != illustration_files:
                raise SystemExit('Illustration bundle must contain exactly the reviewed five files')
            for name in archive.namelist():
                if (a.action in ('deploy','update') and not name.startswith('terraknight-wiki/')) or '..' in name.split('/') or '\\' in name:
                    raise SystemExit('Bundle must contain only the new terraknight-wiki directory')
            expected_build = archive.read('terraknight-wiki/build-info.json').decode('utf-8') if a.action == 'update' else None
        listing = call('/files?action=GetDir', path='/www/wwwroot/homepage')
        if listing.get('PATH') != '/www/wwwroot/homepage':
            raise SystemExit('Unable to verify destination')
        if a.action == 'deploy' and any(value.split(';')[0] == 'terraknight-wiki' for value in listing.get('DIR', []) + listing.get('FILES', [])):
            raise SystemExit('Target already exists; refusing to overwrite it')
        if a.action == 'update':
            target_path = '/www/wwwroot/homepage/terraknight-wiki'
            if call('/files?action=GetDir', path=target_path).get('PATH') != target_path:
                raise SystemExit('Existing Wiki target not verified')
            previous_build = call('/files?action=GetFileBody', path=target_path+'/build-info.json')
            if not previous_build.get('status') or json.loads(previous_build['data']).get('name') not in ('泰拉圣巢wiki', '空洞泰拉wiki'):
                raise SystemExit('Existing Wiki identity not verified')
            protected_paths = ['/www/wwwroot/homepage/index.html','/www/wwwroot/homepage/terraknight/index.html']
            protected = {path:call('/files?action=GetFileBody',path=path) for path in protected_paths}
        backup = '/www/backup/terraknight-wiki-' + time.strftime('%Y%m%d-%H%M%S', time.gmtime())
        result = call('/files?action=CreateDir', path=backup)
        if not result.get('status'):
            raise SystemExit(json.dumps(result))
        if a.action == 'illustrations':
            target = call('/files?action=GetDir', path='/www/wwwroot/homepage/terraknight-wiki/assets')
            if target.get('PATH') != '/www/wwwroot/homepage/terraknight-wiki/assets':
                raise SystemExit('Could not verify wiki assets directory')
            if any(value.split(';')[0] == 'illustrations' for value in target.get('DIR', [])):
                raise SystemExit('Illustrations already exist; refusing unreviewed replacement')
            for name in ('wiki.css', 'wiki.js'):
                remote = '/www/wwwroot/homepage/terraknight-wiki/assets/' + name
                before = call('/files?action=GetFileBody', path=remote)
                result = call('/files?action=CopyFile', sfile=remote, dfile=backup + '/' + name)
                copied = call('/files?action=GetFileBody', path=backup + '/' + name)
                if not before.get('status') or not result.get('status') or copied.get('data') != before.get('data'):
                    raise SystemExit('Could not verify pre-update backup for ' + name)
        payload = a.bundle.read_bytes()
        print(json.dumps({'backup_directory': backup, 'total_bytes': len(payload)}), flush=True)
        for offset in range(0, len(payload), 512 * 1024):
            timestamp = str(int(time.time()))
            form = {'request_time': timestamp, 'request_token': hashlib.md5(
                (timestamp + hashlib.md5(key.encode()).hexdigest()).encode()).hexdigest(),
                'f_path': backup, 'f_name': 'site.zip', 'f_size': str(len(payload)), 'f_start': str(offset)}
            boundary = 'WikiBoundary' + uuid.uuid4().hex
            chunks = []
            for name, value in form.items():
                chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
            block = payload[offset:offset + 512 * 1024]
            chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="blob"; filename="site.zip"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode())
            chunks.append(block)
            chunks.append(f'\r\n--{boundary}--\r\n'.encode())
            request = urllib.request.Request(a.panel.rstrip('/') + '/files?action=upload', data=b''.join(chunks),
                                            headers={'Content-Type': 'multipart/form-data; boundary=' + boundary})
            with urllib.request.urlopen(request, context=context, timeout=45) as response:
                result = json.loads(response.read().decode('utf-8'))
            if not ((isinstance(result, dict) and result.get('status') is not False)
                    or (isinstance(result, int) and result == offset + len(block))):
                raise SystemExit('Upload did not succeed: ' + json.dumps(result))
            print(json.dumps({'uploaded': offset + len(block), 'response': result}), flush=True)
        destination = backup + '/staging' if a.action == 'update' else '/www/wwwroot/homepage/terraknight-wiki' if a.action == 'illustrations' else '/www/wwwroot/homepage'
        if a.action == 'update' and not call('/files?action=CreateDir', path=destination).get('status'):
            raise SystemExit('Could not create staging directory')
        result = call('/files?action=UnZip', sfile=backup + '/site.zip', dfile=destination,
                      type='zip', password='', coding='UTF-8')
        print(json.dumps({'extract': result}), flush=True)
        if a.action == 'update':
            staged = destination+'/terraknight-wiki'
            if not result.get('status'):
                raise SystemExit('Staging extraction failed; live site unchanged')
            with zipfile.ZipFile(a.bundle) as archive:
                for name in ('build-info.json','index.html','search-index.json','assets/illustrations/manifest.json','item-bosssigilfalseknight.html'):
                    local = archive.read('terraknight-wiki/'+name)
                    if len(local) > 256*1024:
                        # GetFileBody cuts a large file short: its size from the listing instead (the
                        # published bytes are compared afterwards by wiki/verify_release.py).
                        folder, _, base = (staged+'/'+name).rpartition('/')
                        listing = call('/files?action=GetDir',path=folder,search=base,showRow=500)
                        sizes = [int(f.split(';')[1]) for f in listing.get('FILES',[]) if f.split(';')[0] == base]
                        if sizes != [len(local)]:
                            raise SystemExit('Staged content verification failed: '+name)
                        continue
                    remote = call('/files?action=GetFileBody',path=staged+'/'+name)
                    if not remote.get('status') or remote.get('data') != local.decode('utf-8'):
                        raise SystemExit('Staged content verification failed: '+name)
            saved = backup+'/live-before'
            moved = call('/files?action=MvFile',sfile=target_path,dfile=saved,rename='true')
            if not moved.get('status'):
                raise SystemExit('Could not back up existing Wiki; update stopped')
            try:
                copied = call('/files?action=GetFileBody',path=saved+'/build-info.json')
                if copied.get('data') != previous_build['data']:
                    raise RuntimeError('Backup verification failed')
                promoted = call('/files?action=MvFile',sfile=staged,dfile=target_path,rename='true')
                if not promoted.get('status'):
                    raise RuntimeError('Could not promote staged Wiki')
            except Exception:
                restored = call('/files?action=MvFile',sfile=saved,dfile=target_path,rename='true')
                print(json.dumps({'rollback':restored}),flush=True)
                raise
            for path, before in protected.items():
                after = call('/files?action=GetFileBody',path=path)
                if (before.get('status'),before.get('data')) != (after.get('status'),after.get('data')):
                    raise SystemExit('Protected page changed: '+path)
            print(json.dumps({'updated':True,'backup':saved,'build':json.loads(expected_build),'protected_pages_unchanged':True}),flush=True)
    elif a.action == 'theme':
        source = Path(__file__).resolve().parents[1] / 'wiki/assets'
        backup = '/www/backup/terraknight-theme-' + time.strftime('%Y%m%d-%H%M%S', time.gmtime())
        result = call('/files?action=CreateDir', path=backup)
        if not result.get('status'):
            raise SystemExit('Could not create theme backup')
        for name in ('wiki.css', 'wiki.js'):
            remote = '/www/wwwroot/homepage/terraknight-wiki/assets/' + name
            current = call('/files?action=GetFileBody', path=remote)
            if not current.get('status'):
                raise SystemExit('Could not read current ' + name)
            result = call('/files?action=CopyFile', sfile=remote, dfile=backup + '/' + name)
            if not result.get('status'):
                raise SystemExit('Could not back up ' + name)
            copied = call('/files?action=GetFileBody', path=backup + '/' + name)
            if copied.get('data') != current.get('data'):
                raise SystemExit('Backup verification failed: ' + name)
        for name in ('wiki.css', 'wiki.js'):
            remote = '/www/wwwroot/homepage/terraknight-wiki/assets/' + name
            content = (source / name).read_text(encoding='utf-8')
            result = call('/files?action=SaveFileBody', path=remote, data=content, encoding='utf-8')
            if not result.get('status'):
                raise SystemExit('Theme update failed: ' + name + '; backup: ' + backup)
            actual = call('/files?action=GetFileBody', path=remote)
            if actual.get('data') != content:
                raise SystemExit('Theme verification failed: ' + name)
        print(json.dumps({'theme_updated': True, 'backup': backup}))
    elif a.action == 'homepage':
        current = call('/files?action=GetFileBody', path='/www/wwwroot/homepage/index.html')
        if not current.get('status') or not isinstance(current.get('data'), str):
            raise SystemExit('Could not read homepage')
        text = current['data']
        marker = '    <!-- 以后加新游戏只需复制此模板 -->'
        card_start = '<a class="game-card" href="/terraknight-wiki/">'
        renaming = card_start in text
        if renaming:
            # The card there already: only its title, when it still has the old name.
            if text.count(card_start) != 1:
                raise SystemExit('Homepage layout changed; refusing automatic edit')
            start = text.index(card_start)
            end = text.index('</a>', start)
            if '<h3>空洞泰拉wiki</h3>' in text[start:end]:
                print('Wiki homepage card already present')
                return
            if text[start:end].count('<h3>泰拉圣巢wiki</h3>') != 1:
                raise SystemExit('Homepage card changed; refusing automatic edit')
        elif text.count(marker) != 1:
            raise SystemExit('Homepage layout changed; refusing automatic edit')
        # Only add a card after the published wiki can be fetched through valid HTTPS.
        with urllib.request.urlopen('https://haoawake.xyz/terraknight-wiki/build-info.json', timeout=25) as response:
            live = json.loads(response.read().decode('utf-8'))
        if live.get('name') not in ('泰拉圣巢wiki', '空洞泰拉wiki'):
            raise SystemExit('Published wiki validation failed')
        backup = '/www/backup/terraknight-homepage-' + time.strftime('%Y%m%d-%H%M%S', time.gmtime()) + '.html'
        result = call('/files?action=CopyFile', sfile='/www/wwwroot/homepage/index.html', dfile=backup)
        if not result.get('status'):
            raise SystemExit('Homepage backup failed: ' + json.dumps(result))
        copied = call('/files?action=GetFileBody', path=backup)
        if copied.get('data') != text:
            raise SystemExit('Homepage backup contents did not match')
        card = '''    <a class="game-card" href="/terraknight-wiki/">
      <div class="card-icon" style="background:linear-gradient(135deg,#17382c,#86a780);"><img src="/terraknight-wiki/assets/crest.svg" width="54" height="64" alt=""></div>
      <div class="card-info">
        <h3>空洞泰拉wiki</h3>
        <p>全物品获取、怪物血量、合成配方与阶段解锁百科</p>
      </div>
    </a>

'''
        if renaming:
            updated = text[:start] + text[start:end].replace('<h3>泰拉圣巢wiki</h3>', '<h3>空洞泰拉wiki</h3>') + text[end:]
        else:
            updated = text.replace(marker, card + marker)
        result = call('/files?action=SaveFileBody', path='/www/wwwroot/homepage/index.html', data=updated, encoding='utf-8')
        if not result.get('status'):
            raise SystemExit('Homepage write failed: ' + json.dumps(result))
        verified = call('/files?action=GetFileBody', path='/www/wwwroot/homepage/index.html')
        if verified.get('data') != updated:
            raise SystemExit('Homepage verification failed; backup at ' + backup)
        print(json.dumps({'homepage_card': 'renamed' if renaming else 'added', 'backup': backup}))


if __name__ == '__main__':
    main()
