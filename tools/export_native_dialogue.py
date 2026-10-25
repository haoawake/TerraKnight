"""Read the installed game's language assets for the narrative audit (no game writes)."""
import base64
import html
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import UnityPy
from Crypto.Cipher import AES
from Crypto.Util.Padding import unpad

ROOT = Path(__file__).resolve().parents[1]
env = UnityPy.load(r'F:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\resources.assets')
result = {}
for obj in env.objects:
    if obj.type.name != 'TextAsset':
        continue
    asset = obj.read()
    if not asset.m_Name.startswith(('ZH_', 'EN_')) or asset.m_Name.startswith('ZH_TW_'):
        continue
    data = unpad(AES.new(b'UKu52ePUBwetZ9wNX88o54dnfKRu0T1l', AES.MODE_ECB).decrypt(base64.b64decode(asset.m_Script)), 16)
    root = ET.fromstring(data.decode('utf-8-sig'))
    result[asset.m_Name] = {e.attrib['name']: html.unescape(''.join(e.itertext()).strip()) for e in root.iter('entry')}
out = ROOT / 'artifacts/native-dialogue-source.json'
out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print({k: len(v) for k, v in result.items() if k.startswith('ZH_')})
