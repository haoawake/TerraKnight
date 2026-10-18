"""Publish only the reviewed atlas; preserve the live encyclopedia and retain rollback copies.

Run in an interactive local console. The API key is prompted without echo and never saved.
"""
from __future__ import annotations
import argparse
import concurrent.futures
import getpass
import hashlib
import io
import json
import ssl
import sys
import time
import traceback
import urllib.parse
import urllib.request
import uuid
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / 'artifacts/wiki-preview/terraknight-wiki'
REMOTE = '/www/wwwroot/homepage/terraknight-wiki'
PUBLIC = 'https://haoawake.xyz/terraknight-wiki/'
RESULT = ROOT / 'artifacts/wiki-map-deploy-result.json'
LOG = ROOT / 'artifacts/wiki-map-deploy.log'


def log(value):
    text = value if isinstance(value, str) else json.dumps(value, ensure_ascii=False)
    print(text, flush=True)
    with LOG.open('a', encoding='utf-8') as output:
        output.write(text + '\n')


def publish(panel, self_signed=False, key_stdin=False):
    log('Checking the live Wiki and all map-linked pages before requesting the key...')
    # Bypass a dead local proxy for this connection only; OS/browser settings stay untouched.
    public_opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    def download(name):
        request = urllib.request.Request(PUBLIC + urllib.parse.quote(name, safe='/') + '?atlas-check=' + str(time.time_ns()), headers={'Cache-Control':'no-cache'})
        for attempt in range(4):
            try:
                with public_opener.open(request, timeout=30) as response:
                    return response.read()
            except (OSError, urllib.error.URLError):
                if attempt == 3:
                    raise
                time.sleep(attempt+1)
    live_info_bytes = download('build-info.json')
    live_info = json.loads(live_info_bytes)
    assert live_info['name'] in ('泰拉圣巢wiki', '空洞泰拉wiki'), 'Unexpected live Wiki identity'
    map_data = json.loads((SITE / 'assets/world-map/map.json').read_text(encoding='utf-8'))
    # Validate all target pages before prompting for the deployment credential.
    def verify_link(name):
        download(name)
    targets = sorted({b['url'] for b in map_data['bosses']} | {b['summon'] for b in map_data['bosses'] if b['summon']})
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        list(pool.map(verify_link, targets))
    live_js = download('assets/wiki.js')
    live_home = download('index.html')
    live_search = json.loads(download('search-index.json'))
    lookup = {}
    for boss in map_data['bosses']:
        lookup.setdefault(boss['url'], []).append({'id':boss['id'], 'name':boss['name']})
    enhancement = r'''
/* BEGIN TERRAKNIGHT WORLD ATLAS */
(() => {
  const base='/terraknight-wiki/';
  const nav=document.querySelector('.topbar nav');
  if(nav && !nav.querySelector('a[href="'+base+'map.html"]')) {
    const a=document.createElement('a');a.href=base+'map.html';a.textContent='世界地图';nav.prepend(a);
  }
  const path=decodeURIComponent(location.pathname).split('/').pop()||'index.html';
  if(path==='index.html') {
    const quick=document.querySelector('.quick-links');
    if(quick && !quick.querySelector('a[href="'+base+'map.html"]')) {const a=document.createElement('a');a.href=base+'map.html';a.textContent='全境地图';quick.append(a);}
  }
  const lookup=__LOOKUP__;
  const article=document.querySelector('article.prose');
  for(const boss of lookup[path]||[]) if(article && !article.querySelector('a[href="'+base+'map.html#boss='+encodeURIComponent(boss.id)+'"]')) {
    const p=document.createElement('p'),a=document.createElement('a');a.href=base+'map.html#boss='+encodeURIComponent(boss.id);a.textContent='在世界地图定位：'+boss.name+' →';p.append(a);article.prepend(p);
  }
})();
/* END TERRAKNIGHT WORLD ATLAS */
'''.replace('__LOOKUP__', json.dumps(lookup, ensure_ascii=False, separators=(',',':')))
    js_text = live_js.decode('utf-8')
    import re
    js_text = re.sub(r'\n?/\* BEGIN TERRAKNIGHT WORLD ATLAS \*/.*?/\* END TERRAKNIGHT WORLD ATLAS \*/\n?', '', js_text, flags=re.S)
    live_search = [row for row in live_search if row['id'] != 'map']
    live_search.append({'id':'map','title':'圣巢全境地图','kind':'指南','category':'玩法指南','stage':'','url':'/terraknight-wiki/map.html','text':'完整逐格地图；缩放、图层切换、48 个 Boss 的房间与入口位置。'})
    # Retain the live release identity, source hashes, counts and notes.
    previous_map = 'world_map' in live_info
    live_info['world_map'] = map_data['source'] | {'bosses':len(map_data['bosses']), 'entrance_markers':sum(b['precision']=='entrance' for b in map_data['bosses'])}
    if not previous_map:
        live_info['pages'] += 1
    files = {p.relative_to(SITE).as_posix():p.read_bytes() for p in (SITE/'assets/world-map').glob('*') if p.is_file()}
    for name in ('map.html','assets/world-map.js','assets/world-map.css'):
        files[name] = (SITE/name).read_bytes()
    files['assets/wiki.js'] = (js_text+enhancement).encode('utf-8')
    # The homepage has a static entry and a fresh shared-script URL for existing readers.
    home_text = live_home.decode('utf-8')
    if 'href="/terraknight-wiki/map.html"' not in home_text:
        home_text, replaced = re.subn(r'(<nav\b[^>]*aria-label="主导航"[^>]*>)', r'\1<a href="/terraknight-wiki/map.html">世界地图</a>', home_text, count=1)
        assert replaced == 1, 'Unexpected live homepage navigation; refusing to guess'
    atlas_version = hashlib.sha256(files['assets/wiki.js']).hexdigest()[:12]
    home_text, script_count = re.subn(r'(/terraknight-wiki/assets/wiki\.js)(?:\?[^"<>]*)?', lambda m:m[1]+'?v='+atlas_version, home_text)
    assert script_count == 1, 'Unexpected live homepage script reference'
    files['index.html'] = home_text.encode('utf-8')
    files['search-index.json'] = json.dumps(live_search, ensure_ascii=False, separators=(',',':')).encode('utf-8')
    files['build-info.json'] = json.dumps(live_info, ensure_ascii=False, indent=2).encode('utf-8')
    zip_buffer = io.BytesIO()
    with zipfile.ZipFile(zip_buffer, 'w', zipfile.ZIP_DEFLATED) as archive:
        directories = {'terraknight-wiki/', 'terraknight-wiki/assets/', 'terraknight-wiki/assets/world-map/'}
        for name in sorted(directories):
            entry = zipfile.ZipInfo(name, time.gmtime()[:6])
            entry.create_system = 3
            entry.external_attr = (0o40755 << 16) | 0x10
            archive.writestr(entry, b'')
        for name, content in sorted(files.items()):
            entry = zipfile.ZipInfo('terraknight-wiki/'+name, time.gmtime()[:6])
            entry.create_system = 3
            entry.external_attr = 0o100644 << 16
            entry.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(entry, content)
    payload = zip_buffer.getvalue()
    (ROOT/'artifacts/terraknight-wiki-map-update.zip').write_bytes(payload)
    log({'prepared':len(files), 'bundle_bytes':len(payload), 'preserved_release':live_info['release']})
    key = (sys.stdin.readline() if key_stdin else getpass.getpass('BaoTa API key (hidden; Enter to deploy atlas): ')).strip()
    if not key:
        raise RuntimeError('No key entered; live site unchanged')
    context = ssl._create_unverified_context() if self_signed else ssl.create_default_context()
    panel_opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPSHandler(context=context))
    def signed(fields):
        timestamp=str(int(time.time()))
        return dict(fields, request_time=timestamp, request_token=hashlib.md5((timestamp+hashlib.md5(key.encode()).hexdigest()).encode()).hexdigest())
    def request(endpoint, body, headers=None):
        req=urllib.request.Request(panel.rstrip('/')+endpoint, data=body, headers=headers or {})
        with panel_opener.open(req, timeout=45) as response:
            return json.loads(response.read().decode('utf-8'))
    def call(action, **fields):
        return request('/files?action='+action, urllib.parse.urlencode(signed(fields)).encode())
    def require(result, context):
        if not isinstance(result, dict) or not result.get('status'):
            raise RuntimeError(context + ': ' + str(result))
    assert call('GetDir',path=REMOTE).get('PATH')==REMOTE, 'Wiki destination not verified'
    previous = call('GetFileBody',path=REMOTE+'/build-info.json')
    assert previous.get('status') and json.loads(previous['data']) == json.loads(live_info_bytes), 'Live Wiki changed while preparing; retry'
    protected = {p:call('GetFileBody',path=p) for p in ['/www/wwwroot/homepage/index.html','/www/wwwroot/homepage/terraknight/index.html',REMOTE+'/enemy-falseknight.html']}
    assert all(v.get('status') for v in protected.values()), 'Protected content cannot be read'
    backup='/www/backup/terraknight-atlas-'+time.strftime('%Y%m%d-%H%M%S',time.gmtime())+'-'+uuid.uuid4().hex[:6]
    require(call('CreateDir',path=backup),'Create backup directory')
    for directory in (backup+'/staging',backup+'/before',backup+'/before/assets',backup+'/failed',backup+'/failed/assets'):
        require(call('CreateDir',path=directory),'Create staging directory')
    log({'backup':backup,'upload_bytes':len(payload)})
    for offset in range(0,len(payload),65536):
        fields=signed({'f_path':backup,'f_name':'map.zip','f_size':str(len(payload)),'f_start':str(offset)})
        boundary='AtlasBoundary'+uuid.uuid4().hex
        chunks=[f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode() for name,value in fields.items()]
        block=payload[offset:offset+65536]
        chunks += [f'--{boundary}\r\nContent-Disposition: form-data; name="blob"; filename="map.zip"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode(),block,f'\r\n--{boundary}--\r\n'.encode()]
        response=request('/files?action=upload',b''.join(chunks),{'Content-Type':'multipart/form-data; boundary='+boundary})
        assert ((isinstance(response,dict) and response.get('status') is not False) or response==offset+len(block)), 'Upload failed'
    log({'upload_complete':True})
    require(call('UnZip',sfile=backup+'/map.zip',dfile=backup+'/staging',type='zip',password='',coding='UTF-8'),'Extract map package')
    staged=backup+'/staging/terraknight-wiki'
    listings={}
    for name,content in files.items():
        folder,_,filename=name.rpartition('/')
        if folder not in listings:
            listings[folder]=call('GetDir',path=staged+('/'+folder if folder else ''),showRow=1000)
        records=[f.split(';') for f in listings[folder].get('FILES',[]) if f.split(';')[0]==filename]
        actual=[int(f[1]) for f in records]
        assert actual==[len(content)], 'Staged size mismatch: '+name
        assert int(records[0][3],8) & 0o044 == 0o044, 'Staged file is not web-readable: '+name
        if name in ('map.html','assets/world-map.js','assets/world-map.css','assets/wiki.js','build-info.json'):
            body=call('GetFileBody',path=staged+'/'+name)
            assert body.get('status') and body['data'].replace('\r\n','\n')==content.decode().replace('\r\n','\n'), 'Staged text mismatch: '+name
    # A concurrent editor must not have changed shared navigation while the atlas uploaded.
    assert download('assets/wiki.js')==live_js and download('build-info.json')==live_info_bytes and download('index.html')==live_home, 'Live content changed; retry deployment'
    promote=['assets/world-map','assets/world-map.js','assets/world-map.css','map.html','search-index.json','assets/wiki.js','index.html','build-info.json']
    log({'staging_verified':True,'files':len(files)})
    changed=[]
    try:
        for name in promote:
            folder,_,filename=name.rpartition('/')
            listing=call('GetDir',path=REMOTE+('/'+folder if folder else ''),showRow=10000)
            exists=filename in [f.split(';')[0] for f in listing.get('FILES',[])+listing.get('DIR',[])]
            # Track intent before the request: an interrupt may arrive after the
            # server moved the file but before the client received its reply.
            changed.append((name,exists,False))
            if exists:
                require(call('MvFile',sfile=REMOTE+'/'+name,dfile=backup+'/before/'+name,rename='true'),'Back up '+name)
            require(call('MvFile',sfile=staged+'/'+name,dfile=REMOTE+'/'+name,rename='true'),'Publish '+name)
            changed[-1]=(name,exists,True)
        log({'promoted':True,'verifying_public_files':len(files)})
        def verify(item):
            name,expected=item
            try:
                actual = download(name)
            except Exception as error:
                raise RuntimeError('Published fetch failed: '+name+': '+str(error)) from error
            assert hashlib.sha256(actual).digest()==hashlib.sha256(expected).digest(), 'Published hash mismatch: '+name
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            for checked,_ in enumerate(pool.map(verify,files.items()),1):
                if checked % 100 == 0 or checked == len(files):
                    log({'public_files_verified':checked,'total':len(files)})
        for path,before in protected.items():
            after=call('GetFileBody',path=path)
            assert after.get('status')==before.get('status') and after.get('data')==before.get('data'), 'Protected page changed: '+path
    except BaseException:
        for name,existed,published in reversed(changed):
            folder,_,filename=name.rpartition('/')
            def has_target(root):
                listing=call('GetDir',path=root+('/'+folder if folder else ''),showRow=10000)
                return filename in [f.split(';')[0] for f in listing.get('FILES',[])+listing.get('DIR',[])]
            saved=has_target(backup+'/before')
            if has_target(REMOTE) and (saved or not existed):
                require(call('MvFile',sfile=REMOTE+'/'+name,dfile=backup+'/failed/'+name,rename='true'),'Preserve failed '+name)
            if saved:
                require(call('MvFile',sfile=backup+'/before/'+name,dfile=REMOTE+'/'+name,rename='true'),'Restore '+name)
        log({'rolled_back':True,'backup':backup})
        raise
    result={'deployed':True,'url':PUBLIC+'map.html','verified_files':len(files),'backup':backup,
            'map':live_info['world_map'],'preserved_release':live_info['release'],'protected_pages_unchanged':True}
    RESULT.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    log(result)


if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--panel',default='https://123.207.78.205:8888')
    parser.add_argument('--self-signed',action='store_true')
    parser.add_argument('--key-stdin',action='store_true')
    args=parser.parse_args()
    exit_code=0
    LOG.write_text('Atlas deployment started: '+time.strftime('%Y-%m-%d %H:%M:%S UTC',time.gmtime())+'\n',encoding='utf-8')
    RESULT.write_text(json.dumps({'deployed':False,'state':'preparing'}),encoding='utf-8')
    try:
        publish(args.panel,args.self_signed,args.key_stdin)
    except BaseException as error:
        exit_code=1
        # Credentials never appear in requests, errors or logs as plaintext.
        result={'deployed':False,'error':str(error)}
        RESULT.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        log(result)
        print(traceback.format_exc())
    if not args.key_stdin:
        input('\nFinished. Press Enter to close this window. ')
    sys.exit(exit_code)
