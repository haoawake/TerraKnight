"""Restore missing promotion targets from a specific interrupted deployment backup."""
import argparse
import getpass
import hashlib
import json
import ssl
import time
import urllib.parse
import urllib.request

parser=argparse.ArgumentParser()
parser.add_argument('backup')
args=parser.parse_args()
assert args.backup.startswith('/www/backup/terraknight-atlas-') and '..' not in args.backup
key=getpass.getpass('BaoTa API key (hidden): ').strip()
opener=urllib.request.build_opener(urllib.request.ProxyHandler({}),urllib.request.HTTPSHandler(context=ssl._create_unverified_context()))
def call(action,**fields):
    stamp=str(int(time.time()))
    fields.update(request_time=stamp,request_token=hashlib.md5((stamp+hashlib.md5(key.encode()).hexdigest()).encode()).hexdigest())
    req=urllib.request.Request('https://123.207.78.205:8888/files?action='+action,data=urllib.parse.urlencode(fields).encode())
    with opener.open(req,timeout=45) as response:return json.loads(response.read())
def names(path):
    listing=call('GetDir',path=path,showRow=10000)
    assert listing.get('PATH')==path,listing
    return {r.split(';')[0] for r in listing.get('FILES',[])+listing.get('DIR',[])}
remote='/www/wwwroot/homepage/terraknight-wiki'
for folder in ['', '/assets']:
    live=names(remote+folder)
    saved=names(args.backup+'/before'+folder)
    for name in sorted(saved-live):
        assert name in ['world-map','world-map.js','world-map.css','map.html','search-index.json','wiki.js','index.html','build-info.json'],name
        result=call('MvFile',sfile=args.backup+'/before'+folder+'/'+name,dfile=remote+folder+'/'+name,rename='true')
        assert result.get('status'),result
        print('Restored missing target:',folder+'/'+name,flush=True)
print('Recovery complete',flush=True)
