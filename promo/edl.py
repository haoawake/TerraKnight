"""The trailer's edit, shared by make_promo.py (the render) and make_jianying.py (the JianyingPro draft).

Every shot is cut on the music's grid: times are counted in beats of the 140 BPM track (beats.json),
so a cut always lands on a beat and a section change on the drop. A shot takes its frames from one
of the recorded clips (clips/<name>.mp4, 1920x1200; the second set at 50 fps, the first at 30),
starting at `src` seconds, played at `speed`; `crop` is where the 16:9 frame sits in the clip: centre
(cx, cy) as fractions of the clip and zoom z (1 = the clip's full width), from the shot's start to its
end. `fx` is how the shot comes in: cut, punch (a zoom kick and a flash), impact (the drops: a big
flash, a shake and a colour split), whip (a slide with motion blur), fade (a crossfade from the shot
before). No two shots use the same stretch of a clip (checked below).

The sections (beats): intro 0-16, groove 16-32, drop 32-80, breakdown 80-112, drop 112-160, title 160 on.
The first cut (one minute, 30 fps clips) is edl_v1.py.
"""
import json, os

HERE = os.path.dirname(os.path.abspath(__file__))
BEATS = json.load(open(os.path.join(HERE, 'beats.json')))
BEAT = BEATS['beat']
BAR = BEATS['bar']
SECTIONS = BEATS['sections']
LENGTH = BEATS['length']
FPS = 50
W, H = 1920, 1080

# The HUD (hotbar, hearts) kept at the top of a full-width crop: 1080 of the clip's 1200 rows, from row 20.
TOP = (20 + 540) / 1200


def b(n):
    """n beats from the start, in seconds."""
    return n * BEAT


def shot(beat0, beats, clip, src, speed=1.0, crop=None, fx='cut'):
    return dict(t0=b(beat0), t1=b(beat0 + beats), clip=clip, src=src, speed=speed,
                crop=crop or ((0.5, TOP, 1.0), (0.5, TOP, 1.0)), fx=fx)


def push(cx, cy, z0, z1, cx1=None, cy1=None):
    return ((cx, cy, z0), (cx if cx1 is None else cx1, cy if cy1 is None else cy1, z1))


def beats_of(start, clips, beats=1, fx_every=4, zoom=0.06):
    """A run of one-beat cuts: (clip, src, cx, cy, z) each, a punch on every bar's first."""
    return [shot(start + i * beats, beats, clip, src, 1.0, push(cx, cy, z, z + zoom), 'punch' if i % fx_every == 0 else 'cut')
            for i, (clip, src, cx, cy, z) in enumerate(clips)]


SHOTS = [
    # Intro: the save profiles, the creative panel and the four difficulties; then the portal in King's Pass.
    shot(0, 8, 'menu_difficulty', 1.4, 1.85, push(0.62, 0.6, 1.05, 1.45, 0.5, 0.47)),
    shot(8, 8, 'menu_difficulty', 11.9, 1.0, push(0.5, 0.66, 1.5, 1.75), 'fade'),
    # The groove: Dirtmouth's townsfolk, the Merchant's chat and shop, stations set down.
    shot(16, 4, 'town_walk', 0.3, 1.2, push(0.5, 0.55, 1.35, 1.45, 0.55), 'punch'),
    shot(20, 4, 'npc_shop', 1.2, 1.15, push(0.47, 0.16, 2.6, 2.7), 'whip'),
    shot(24, 4, 'npc_shop', 3.6, 2.0, push(0.19, 0.24, 2.3, 2.4), 'punch'),
    shot(28, 4, 'stations', 0.6, 1.35, push(0.47, 0.64, 2.2, 2.4), 'whip'),
    # Drop 1: each class against a Master Mode boss, the loot, a run of weapons.
    shot(32, 4, 'fight_melee_gruz', 0.0, 1.0, push(0.5, 0.5, 1.15, 1.05), 'impact'),
    shot(36, 4, 'fight_melee_gruz', 1.72, 0.8, push(0.55, 0.5, 1.2, 1.1), 'punch'),
    shot(40, 4, 'fight_ranged_vengefly', 0.0, 1.0, push(0.45, 0.5, 1.15, 1.05), 'whip'),
    shot(44, 4, 'fight_ranged_vengefly', 1.72, 0.85, push(0.45, 0.5, 1.2, 1.1), 'punch'),
    shot(48, 4, 'fight_magic_uumuu', 1.0, 1.0, push(0.45, 0.45, 1.1, 1.0), 'whip'),
    shot(52, 4, 'fight_magic_uumuu', 12.0, 1.0, push(0.4, 0.45, 1.2, 1.1), 'punch'),
    shot(56, 4, 'fight_summon_moss', 0.6, 1.0, push(0.5, 0.55, 1.2, 1.1), 'whip'),
    shot(60, 4, 'fight_summon_moss', 4.2, 1.0, push(0.5, 0.55, 1.25, 1.15), 'punch'),
    shot(64, 8, 'loot_drops', 7.4, 1.0, push(0.55, 0.6, 1.45, 1.6), 'whip'),
] + beats_of(72, [
    ('weapons_run', 2.4, 0.78, 0.68, 2.0), ('weapons_run', 4.1, 0.78, 0.68, 2.0), ('weapons_run', 7.4, 0.75, 0.68, 2.0),
    ('weapons_run', 9.0, 0.75, 0.68, 2.0), ('weapons_run', 10.7, 0.75, 0.68, 2.0), ('weapons_run', 14.1, 0.75, 0.68, 2.0),
    ('weapons_run', 17.4, 0.75, 0.68, 2.0), ('weapons_run', 22.2, 0.7, 0.68, 2.0),
]) + [
    # The breakdown: the bestiary, the difficulty's effect on it, crafting, loadouts, the map and a pylon, the hook.
    shot(80, 4, 'bestiary', 1.0, 2.0, push(0.43, 0.52, 1.5, 1.6), 'fade'),
    shot(84, 4, 'bestiary', 4.5, 2.0, push(0.5, 0.52, 1.6, 1.7), 'fade'),
    shot(88, 4, 'bestiary_rates', 1.0, 1.2, push(0.56, 0.5, 2.0, 2.1), 'fade'),
    shot(92, 4, 'crafting', 1.6, 2.1, push(0.14, 0.66, 2.6, 2.7), 'fade'),
    shot(96, 4, 'loadouts', 0.4, 3.2, push(0.74, 0.32, 2.0, 2.1), 'fade'),
    shot(100, 4, 'map_pylon2_click', 0.9, 2.5, push(0.5, TOP, 1.0, 1.1), 'fade'),
    shot(104, 4, 'map_pylon2', 3.4, 1.4, push(0.5, 0.6, 1.4, 1.5), 'fade'),
    shot(108, 4, 'hook_swing', 1.2, 1.3, push(0.5, 0.45, 1.3, 1.4), 'fade'),
    # Drop 2: straight through Hallownest, then everything at once.
    shot(112, 8, 'mining_dirtmouth', 0.3, 1.9, push(0.5, 0.5, 1.25, 1.35), 'impact'),
    shot(120, 6, 'pain_route', 0.6, 2.0, push(0.5, 0.5, 1.1, 1.2), 'whip'),
    shot(126, 6, 'pain_route', 20.0, 2.6, push(0.5, 0.5, 1.15, 1.25), 'punch'),
    shot(132, 4, 'gravity_ceiling', 0.5, 1.8, push(0.5, 0.45, 1.9, 2.1), 'whip'),
] + beats_of(136, [
    ('fight_hive', 1.2, 0.45, 0.5, 1.3), ('fight_dung', 1.2, 0.45, 0.5, 1.3), ('fight_vessel', 0.6, 0.5, 0.5, 1.3), ('fight_mawlek', 1.0, 0.5, 0.55, 1.4),
    ('fight_soulwarrior', 0.5, 0.45, 0.5, 1.3), ('fight_magic_uumuu', 6.0, 0.45, 0.45, 1.3), ('fight_hive', 3.2, 0.5, 0.5, 1.3), ('fight_dung', 4.2, 0.4, 0.5, 1.3),
    ('fight_vessel', 2.6, 0.5, 0.5, 1.3), ('fight_mawlek', 3.6, 0.5, 0.55, 1.4), ('weapons_run', 5.6, 0.75, 0.68, 2.0), ('weapons_run', 19.1, 0.75, 0.68, 2.0),
    ('fight_magic_uumuu', 9.0, 0.45, 0.45, 1.3), ('fight_hive', 5.2, 0.5, 0.5, 1.3), ('fight_dung', 7.6, 0.45, 0.55, 1.3), ('fight_vessel', 4.6, 0.5, 0.5, 1.3),
]) + [
    # The last two bars slowed down, pushing in, into the title on the final hit.
    shot(152, 8, 'fight_hive', 6.9, 0.55, push(0.5, 0.55, 1.25, 1.6), 'punch'),
]
# The title card, made by make_promo.py, from the final hit to the end.
END_CARD = dict(t0=SECTIONS['outro'], t1=LENGTH)

# Captions: (from beat, to beat, kind, lines). kind: title (mid-screen), class (a class card, top
# left), caption (the story, centred low), badge (top centre).
TEXTS = [
    (1, 7.6, 'caption', ['当泰拉瑞亚', 'WHEN TERRARIA']),
    (9, 15.6, 'caption', ['闯进空洞骑士', 'CRASHES INTO HOLLOW KNIGHT']),
    (16, 23.6, 'caption', ['德特茅斯住满了泰拉瑞亚的 NPC', "TERRARIA'S TOWNSFOLK MOVED INTO DIRTMOUTH"]),
    (24, 31.6, 'caption', ['对话、买卖、放家具，全是原版手感', 'TALK, TRADE AND BUILD, JUST LIKE TERRARIA']),
    (32, 64, 'badge', ['大师模式', 'MASTER MODE']),
    (32, 39.6, 'class', ['近战', 'MELEE', '泰拉悠悠球 · 彩虹猫之刃', '格鲁兹之母  GRUZ MOTHER']),
    (40, 47.6, 'class', ['远程', 'RANGED', '幻影弓 · 电圈发射器', '反击蝇之王  VENGEFLY KING']),
    (48, 55.6, 'class', ['魔法', 'MAGIC', '星云烈焰 · 利刃台风', '乌姆  UUMUU']),
    (56, 63.6, 'class', ['召唤', 'SUMMONER', '星尘细胞 · 月亮传送门 · 七彩水晶', '大型苔藓冲锋者  MASSIVE MOSS CHARGER']),
    (64, 71.6, 'caption', ['大师模式：掉落更多、更好', 'MASTER MODE DROPS MORE, AND BETTER']),
    (72, 79.8, 'caption', ['两百多件泰拉瑞亚物品', 'OVER 200 TERRARIA ITEMS']),
    (80, 87.6, 'caption', ['怪物图鉴：数值和掉落一目了然', 'THE BESTIARY: STATS AND DROPS']),
    (88, 91.8, 'caption', ['普通 → 大师：怪更强，奖励更丰厚', 'FROM NORMAL TO MASTER: TOUGHER FOES, RICHER LOOT']),
    (92, 99.6, 'caption', ['合成栏和四套配装，和原版一模一样', 'CRAFTING AND FOUR LOADOUTS, AS IN TERRARIA']),
    (100, 107.6, 'caption', ['泰拉瑞亚的地图 · 晶塔传送', 'THE MAP AND THE PYLONS']),
    (108, 111.8, 'caption', ['月钩', 'THE LUNAR HOOK']),
    (112, 119.6, 'caption', ['耀斑镐：从德特茅斯一路挖到十字路', 'A PICKAXE STRAIGHT DOWN TO THE CROSSROADS']),
    (120, 131.6, 'caption', ['苦痛之路？飞过去', 'PATH OF PAIN? JUST FLY THROUGH IT']),
    (132, 135.8, 'caption', ['重力球：天花板也是地面', 'GRAVITY GLOBE: THE CEILING IS A FLOOR']),
]


# The Chinese cut (PROMO_LANG=zh): the same edit over the clips recorded with the game in Chinese
# (zh_<clip>, suites promoA_zh/B_zh/C_zh), with the in-points that moved (no fight plays out the same
# twice): ZH_SHOT maps a shot's (clip, src) to what changes (src, speed, crop); ZH_CLIP a clip to the one
# standing in for it (the map and the pylon's arrival are one clip there).
LANG = os.environ.get('PROMO_LANG', '')
ZH_CLIP = {'map_pylon2_click': 'map_pylon2'}
ZH_SHOT = {
    ('map_pylon2_click', 0.9): dict(src=0.4, speed=1.75),
    ('map_pylon2', 3.4): dict(src=4.1, speed=1.0),
    ('stations', 0.6): dict(crop=push(0.48, 0.7, 2.2, 2.4)),
    ('fight_hive', 6.9): dict(src=8.8),
    ('fight_dung', 7.6): dict(src=7.9),
    ('fight_vessel', 4.6): dict(src=5.5),
    ('fight_mawlek', 3.6): dict(src=4.6),
}


def _localise():
    if LANG != 'zh':
        return
    for s in SHOTS:
        s.update(ZH_SHOT.get((s['clip'], round(s['src'], 3)), {}))
        s['clip'] = 'zh_' + ZH_CLIP.get(s['clip'], s['clip'])


def _check():
    """The shots follow on without gaps, and no two take the same stretch of a clip."""
    prev = 0.0
    used = {}
    for s in SHOTS:
        assert abs(s['t0'] - prev) < 1e-6, (s, prev)
        prev = s['t1']
        a = s['src']
        z = a + (s['t1'] - s['t0']) * s['speed']
        for a2, z2 in used.get(s['clip'], []):
            assert z <= a2 + 1e-6 or a >= z2 - 1e-6, f"{s['clip']} {a:.2f}-{z:.2f} overlaps {a2:.2f}-{z2:.2f}"
        used.setdefault(s['clip'], []).append((a, z))
    assert abs(prev - END_CARD['t0']) < 1e-3, (prev, END_CARD['t0'])


_localise()
_check()
