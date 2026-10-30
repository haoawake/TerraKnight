"""Validate the exported map and its integration without changing runtime snapshots."""
import json
from pathlib import Path
from urllib.parse import unquote, urlsplit
from PIL import Image
from check import Links

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / 'artifacts/wiki-preview/terraknight-wiki'
DATA = SITE / 'assets/world-map'


def main():
    atlas = json.loads((DATA / 'map.json').read_text(encoding='utf-8'))
    assert atlas['schema'] == 2 and atlas['render_mode'] == 'solid-open'
    rooms = {r['id']: r for r in atlas['rooms']}
    # Hallownest's 379 rooms and Godhome's (72 with terrain of their own) as the game's atlas has them now.
    assert len(rooms) == atlas['source']['rooms'] >= 379
    assert sum(r['w']*r['h'] for r in rooms.values()) == atlas['source']['cells']
    for name, room in rooms.items():
        from PIL import ImageChops
        with Image.open(DATA / (name + '.png')) as solid, Image.open(DATA / (name + '-open.png')) as opened:
            assert solid.size == opened.size == (room['w'], room['h'])
            a, b = solid.getchannel('A'), opened.getchannel('A')
            assert ImageChops.multiply(a, b).getbbox() is None, name
            assert ImageChops.lighter(a, b).getextrema() == (255, 255), name
    bosses = {b['id']: b for b in atlas['bosses']}
    assert len(bosses) == 48
    assert bosses['Hornet']['scene'] == 'Fungus1_04'
    assert bosses['Hornet@4']['scene'] == 'Deepnest_East_Hornet'
    # Godhome's terrain mapped: the Pale Lurker stands in her own arena (GG_Lurker), the stands' marker only without it.
    assert bosses['PaleLurker']['anchor'] in ('GG_Lurker', 'Room_Colosseum_Spectate')
    assert bosses['AbsoluteRadiance']['scene'] == 'GG_Radiance'
    assert bosses['EnragedGuardian']['stage'] == 2
    for boss in bosses.values():
        room = rooms[boss['anchor']]
        # The room as drawn, stretched (prepare_world_map: dw, dh).
        assert room['x'] <= boss['x'] <= room['x']+room.get('dw', room['w'])
        assert room['y'] <= boss['y'] <= room['y']+room.get('dh', room['h'])
        assert (boss['precision']=='room') == (boss['scene']==boss['anchor'])
        assert (SITE / boss['url']).is_file(), boss
        if boss['summon']:
            assert (SITE / boss['summon']).is_file(), boss
        assert 'map.html#boss=' in (SITE / boss['url']).read_text(encoding='utf-8'), boss
    pages = {}
    for path in SITE.glob('*.html'):
        parsed = Links()
        parsed.feed(path.read_text(encoding='utf-8'))
        pages[path.resolve()] = parsed
    links = 0
    for path, parsed in pages.items():
        for href in parsed.links:
            url = urlsplit(href)
            if url.scheme or url.netloc:
                continue
            dest = SITE.parent / unquote(url.path).lstrip('/') if url.path.startswith('/') else path.parent / unquote(url.path) if url.path else path
            if dest.is_dir():
                dest = dest / 'index.html'
            assert dest.is_file(), (path.name, href)
            if url.fragment and dest.resolve() in pages and not (dest.name=='map.html' and url.fragment.startswith('boss=')):
                assert unquote(url.fragment) in pages[dest.resolve()].ids, (path.name, href)
            links += 1
    result = dict(rooms=len(rooms), cells=atlas['source']['cells'], bosses=len(bosses),
                  room_markers=sum(b['precision']=='room' for b in bosses.values()),
                  entrance_markers=sum(b['precision']=='entrance' for b in bosses.values()),
                  summons=sum(bool(b['summon']) for b in bosses.values()), checked_links=links,
                  atlas_sha256=atlas['source']['atlas_sha256'])
    (ROOT / 'artifacts/wiki-world-map-check.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
