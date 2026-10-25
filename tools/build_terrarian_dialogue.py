"""Compile reviewed, keyed identity adaptations. Never replace narrative nouns at runtime."""
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
native = json.loads((ROOT / 'artifacts/native-dialogue-source.json').read_text('utf-8'))
entries = {}
for line in (ROOT / 'docs/泰拉旅人台词.tsv').read_text('utf-8').splitlines():
    if not line or line.startswith('#'):
        continue
    identity, zh, en = line.split('\t')
    sheet, key = identity.split('/', 1)
    assert key in native['ZH_' + sheet], identity
    assert identity not in entries, identity
    entries[identity] = {'Zh': zh, 'En': en, 'Reason': 'authored'}

# Only these inspected conversations use the diminutive for the player. Do not
# rewrite a speaker's child, companions, enemies, or an object called a little thing.
address_keys = '''
Nailmasters/SHEO_MEET
CP2/GRIMM_RITUAL_COMPLETE
Minor NPC/JINN_ACCEPT
Minor NPC/CLOTH_DEEPNEST_MEET
Minor NPC/CLOTH_TRAMWAY_DREAM_HAPPY
Minor NPC/XUN_HAVE_FLOWER
Minor NPC/XUN_MEET
Minor NPC/WHITE_DEFENDER_OUTRO_5
Minor NPC/TISO_DREAM_TOWN
Minor NPC/BIGCAT_INTRO
Minor NPC/CLOTH_FUNG_MEET
Minor NPC/XUN_HAVE_FLOWER2
Minor NPC/CLOTH_TRAMWAY_MEET
Minor NPC/GIRAFFE_DREAM
Elderbug/ELDERBUG_BLACK_EGG_OPENED
Banker/BANKER_DREAM_SPA
Hunter/HUNTER_MEET
Hunter/HUNTER_ENTRIES_COMPLETED
Hunter/HUNTER_CONVO_1
Hunter/HUNTER_CONVO_2
Stag/STAG_DEEPNEST
Stag/STAG_HOPE_1
Stag/STAG_REMEMBER_3
Stag/STAG_RUINS2
Stag/STAG_TRAVEL_1
Stag/STAG_HIDDEN
Stag/STAG_MEET
Ghosts/100NAIL_TALK
Ghosts/PERPETOS_REPEAT
Ghosts/GRAVEDIGGER_REPEAT
Ghosts/MAGNUS_REPEAT
Ghosts/ATRA_TALK
Ghosts/CASPIAN_TALK
Ghosts/GARRO_TALK
'''.strip().splitlines()
for identity in address_keys:
    sheet, key = identity.split('/', 1)
    assert identity not in entries
    zh, en = (native[lang + '_' + sheet][key] for lang in ('ZH', 'EN'))
    zh = zh.replace('小家伙的小步子', '泰拉旅人的脚步').replace('你长得这么小巧', '你来自异乡')
    zh = zh.replace('小家伙', '泰拉旅人').replace('小东西', '泰拉旅人').replace('小虫子', '泰拉战士')
    en = re.sub(r'\b(?:little|small|tiny) (?:one|thing|creature|bug|friend|warrior|grub|squib|wanderer|traveller|it)\b', 'Terrarian traveller', en, flags=re.I)
    en = en.replace('so tiny a creature', 'a traveller from so far away').replace('The tiny steps of a Terrarian traveller', 'The footsteps of a Terrarian traveller')
    en = re.sub(r'\bso small\b', 'from so far away', en, flags=re.I)
    assert zh != native['ZH_' + sheet][key], identity
    entries[identity] = {'Zh': zh, 'En': en, 'Reason': 'reviewed player address'}

# Two diary versions differ only in the wording of Zote's invented trophy.
entries['Minor NPC/BRETTA_DIARY_ZOTE'] = dict(entries['Minor NPC/BRETTA_DIARY_4'], Reason='authored diary variant')

# NPCs sometimes fetch the same conversation through a content-pack sheet.
# Propagate by exact key AND exact original text; never by a substring match.
for identity, row in list(entries.items()):
    sheet, key = identity.split('/', 1)
    for other in ('CP2', 'CP3', 'Minor NPC', 'Enemy Dreams'):
        alias = other + '/' + key
        if alias not in entries and native.get('ZH_' + other, {}).get(key) == native['ZH_' + sheet][key]:
            entries[alias] = dict(row, Reason='exact native alias of ' + identity)

out = ROOT / 'src/TerraKnight/Resources/terrarian-dialogue.json'
out.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + '\n', 'utf-8')
print(f'{len(entries)} keyed overrides; sheets: {dict(Counter(k.split("/")[0] for k in entries))}')
review = ['# 泰拉旅人：已接入的原生文本改写', '',
          '玩家是来自泰拉的旅人。历史角色、旧王的造物和容器仍保留自己的身份。', '',
          '中文与英文为本轮定稿；繁体中文暂用同一中文文本，其他语言对这些条目使用英文，避免恢复错误身份。', '']
for sheet in sorted({k.split('/')[0] for k in entries}):
    review += ['## ' + sheet, '']
    for identity, row in entries.items():
        if identity.split('/')[0] != sheet:
            continue
        review += ['**' + identity.split('/', 1)[1] + '**', '', row['Zh'].replace('<page>', '\n\n').replace('<br>', '  \n'), '']
(ROOT / 'docs/泰拉旅人原生文本改写全文.md').write_text('\n'.join(review), 'utf-8')

# Emit every native conversation into the review ledger, including preserved
# historical statements: an occurrence of "Vessel" alone is not player identity.
sheets = {'Banker','CP2','CP3','Charm Slug','Cornifer','Dream Witch','Dreamers',
          'Elderbug','Enemy Dreams','Ghosts','Hornet','Hunter','Iselda','Jiji',
          'Lore Tablets','Minor NPC','Nailmasters','Nailsmith','Quirrel',
          'Relic Dealer','Shaman','Sly','Stag','Zote','Backer Messages','Prompts','General','UI'}
ledger = []
for sheet in sorted(sheets):
    for key, original in native['ZH_' + sheet].items():
        identity = sheet + '/' + key
        row = entries.get(identity)
        ledger.append({'Id': identity, 'Status': 'adapted' if row else 'retained',
                       'Original': original, 'Text': row['Zh'] if row else original})
(ROOT / 'artifacts/terrarian-dialogue-ledger.json').write_text(json.dumps(ledger, ensure_ascii=False, indent=2), 'utf-8')
suspect = re.compile('小骑士|小鬼魂|苍白的家伙|你的外壳|你的种族|你是容器|你是空洞骑士|你出生的|你那种.*虚无|你.*无脸|你.*尖角|你.*没有灵魂')
remaining = [r for r in ledger if suspect.search(r['Text'])]
(ROOT / 'artifacts/terrarian-dialogue-remaining.json').write_text(json.dumps(remaining, ensure_ascii=False, indent=2), 'utf-8')
print(f'{len(ledger)} source records catalogued; {len(remaining)} identity candidates require review')
