"""Upload one reviewed TerraKnight ZIP to the existing Wiki downloads directory."""
import hashlib
import getpass
import json
import ssl
import sys
import time
import urllib.parse
import urllib.request
import uuid
import zipfile
from pathlib import Path


def main():
    archive = Path(sys.argv[1]).resolve()
    assert archive.name in ('TerraKnight-v4.1.0.zip', 'TerraKnight-v4.1.0-final.zip', 'TerraKnight-v4.1.0-source.zip')
    with zipfile.ZipFile(archive) as z:
        if archive.name.endswith('-source.zip'):
            assert '__manifest__.json' in z.namelist()
            assert not any(n.endswith(('.dll', '.pdb', '.exe')) or n.startswith(('artifacts/', '.git/')) for n in z.namelist())
        else:
            assert 'BepInEx/plugins/TerraKnight/TerraKnight.dll' in z.namelist()
            assert 'BepInEx/TerraKnight/worldatlas.bin' in z.namelist()
        assert not any(n.endswith('.exe') or '..' in n.split('/') for n in z.namelist())
    key = getpass.getpass('BaoTa API key: ').strip()
    assert key
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}),
        urllib.request.HTTPSHandler(context=ssl._create_unverified_context()))
    panel = 'https://123.207.78.205:8888'
    target = '/www/wwwroot/homepage/terraknight-wiki/downloads'

    def auth():
        stamp = str(int(time.time()))
        return {'request_time': stamp, 'request_token': hashlib.md5((stamp + hashlib.md5(key.encode()).hexdigest()).encode()).hexdigest()}

    def call(endpoint, **data):
        req = urllib.request.Request(panel + endpoint, data=urllib.parse.urlencode(data | auth()).encode())
        with opener.open(req, timeout=60) as r:
            return json.load(r)

    listing = call('/files?action=GetDir', path=target)
    assert listing.get('PATH') == target, 'Wiki downloads directory not verified'
    assert not any(f.split(';')[0] == archive.name for f in listing.get('FILES', [])), 'Release file already exists; refusing overwrite'
    payload = archive.read_bytes()
    for offset in range(0, len(payload), 512 * 1024):
        block = payload[offset:offset + 512 * 1024]
        boundary = 'TerraKnight' + uuid.uuid4().hex
        fields = auth() | {'f_path': target, 'f_name': archive.name, 'f_size': str(len(payload)), 'f_start': str(offset)}
        parts = [f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode() for k, v in fields.items()]
        parts += [f'--{boundary}\r\nContent-Disposition: form-data; name="blob"; filename="{archive.name}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode(), block, f'\r\n--{boundary}--\r\n'.encode()]
        req = urllib.request.Request(panel + '/files?action=upload', data=b''.join(parts), headers={'Content-Type': 'multipart/form-data; boundary=' + boundary})
        with opener.open(req, timeout=60) as r:
            result = json.load(r)
        assert (isinstance(result, dict) and result.get('status') is not False) or result == offset + len(block), result
    url = 'https://haoawake.xyz/terraknight-wiki/downloads/' + archive.name
    with opener.open(url + '?verify=' + str(int(time.time())), timeout=90) as r:
        downloaded = r.read()
    assert hashlib.sha256(downloaded).digest() == hashlib.sha256(payload).digest()
    print(json.dumps({'url': url, 'sha256': hashlib.sha256(payload).hexdigest(), 'bytes': len(payload), 'download_verified': True}))


if __name__ == '__main__':
    main()
