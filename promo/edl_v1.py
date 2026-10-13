"""The trailer's edit, shared by make_promo.py (the render) and make_jianying.py (the JianyingPro draft).

Every shot is cut on the music's grid: times are counted in beats of the 140 BPM track (beats.json),
so a cut always lands on a beat and a section change on the drop. A shot takes its frames from one
of the recorded clips (clips/<name>.mp4, 1920x1200 at 30 fps), starting at `src` seconds, played
at `speed`; `crop` is where the 16:9 frame sits in the clip: centre (cx, cy) as fractions of the
clip and zoom z (1 = the clip's full width), from the shot's start to its end. `fx` is how the shot
comes in: cut, punch (a zoom kick and a flash), impact (the drops: a big flash, a shake and a colour
split), whip (a slide with motion blur), fade (a crossfade from the shot before).
"""
import json, os

HERE = os.path.dirname(os.path.abspath(__file__))
BEATS = json.load(open(os.path.join(HERE, 'beats.json')))
BEAT = BEATS['beat']
BAR = BEATS['bar']
SECTIONS = BEATS['sections']
LENGTH = BEATS['length']
FPS = 30
W, H = 1920, 1080

# The HUD (hotbar, hearts) kept at the top of a full-width crop: 1080 of the clip's 1200 rows, from row 20.
TOP = (20 + 540) / 1200


def b(n):
    """n beats from the start, in seconds."""
    return n * BEAT


def shot(beat0, beats, clip, src, speed=1.0, crop=((0.5, TOP, 1.0), (0.5, TOP, 1.0)), fx='cut'):
    return dict(t0=b(beat0), t1=b(beat0 + beats), clip=clip, src=src, speed=speed, crop=crop, fx=fx)


def push(cx, cy, z0, z1, cx1=None, cy1=None):
    return ((cx, cy, z0), (cx if cx1 is None else cx1, cy if cy1 is None else cy1, z1))


FULL = ((0.5, TOP, 1.0), (0.5, TOP, 1.0))

SHOTS = [
    # Intro (bars 0-4, letterboxed): the map of all Hallownest, then Dirtmouth.
    shot(0, 8, 'map_pylon', 0.0, 0.7, push(0.5, 0.5, 1.0, 1.12)),
    shot(8, 8, 'stations', 0.0, 0.8, push(0.5, 0.62, 1.05, 1.45, 0.47, 0.66), 'fade'),
    # Drop 1 (bars 4-16): every class against a Master Mode boss.
    shot(16, 4, 'melee_soulwarrior', 0.0, 1.0, push(0.45, 0.5, 1.1, 1.0), 'impact'),
    shot(20, 4, 'melee_soulwarrior', 1.75, 1.0, push(0.5, 0.5, 1.15, 1.05), 'punch'),
    shot(24, 4, 'ranged_dungdefender', 1.0, 1.0, push(0.55, 0.5, 1.1, 1.0), 'whip'),
    shot(28, 4, 'ranged_dungdefender', 6.5, 0.65, push(0.6, 0.55, 1.2, 1.1), 'punch'),
    shot(32, 4, 'magic_brokenvessel', 1.6, 1.0, push(0.45, 0.5, 1.1, 1.0), 'whip'),
    shot(36, 4, 'magic_brokenvessel', 4.0, 1.0, push(0.5, 0.5, 1.2, 1.1), 'punch'),
    shot(40, 4, 'summon_mawlek', 0.3, 1.0, push(0.48, 0.5, 1.25, 1.15), 'whip'),
    shot(44, 4, 'summon_mawlek', 3.6, 1.0, push(0.5, 0.55, 1.25, 1.1), 'punch'),
    shot(48, 4, 'melee_hiveknight', 9.0, 1.0, push(0.4, 0.55, 1.15, 1.05), 'whip'),
    shot(52, 4, 'melee_hiveknight', 13.0, 1.0, push(0.42, 0.55, 1.2, 1.1), 'punch'),
    shot(56, 1, 'melee_hiveknight', 16.5, 1.0, push(0.45, 0.55, 1.25, 1.2), 'punch'),
    shot(57, 1, 'magic_brokenvessel', 0.6, 1.0, push(0.4, 0.45, 1.3, 1.2), 'cut'),
    shot(58, 1, 'melee_hiveknight', 18.0, 1.0, push(0.45, 0.55, 1.25, 1.2), 'cut'),
    shot(59, 1, 'ranged_dungdefender', 3.2, 1.0, push(0.6, 0.5, 1.3, 1.2), 'cut'),
    shot(60, 4, 'melee_hiveknight', 20.9, 0.85, push(0.5, 0.6, 1.2, 1.4), 'punch'),
    # Break (bars 16-20): what else came over: building, crafting, the map and the pylons.
    shot(64, 4, 'stations', 0.6, 1.35, push(0.47, 0.64, 2.2, 2.4), 'fade'),
    shot(68, 4, 'stations', 3.6, 1.0, push(0.3, 0.42, 1.6, 1.7), 'fade'),
    shot(72, 4, 'map_pylon', 2.6, 1.0, push(0.5, 0.5, 1.05, 1.15), 'fade'),
    shot(76, 4, 'map_pylon', 7.0, 1.0, push(0.45, 0.45, 1.3, 1.55), 'fade'),
    # Drop 2 (bars 20-32): straight through Hallownest, then everything at once.
    shot(80, 12, 'mining_dirtmouth', 0.2, 1.45, push(0.5, 0.5, 1.25, 1.35), 'impact'),
    shot(92, 12, 'flight_pathofpain', 0.0, 1.0, push(0.5, 0.5, 1.1, 1.25), 'whip'),
    shot(104, 4, 'gravity_ceiling', 0.5, 1.8, push(0.5, 0.45, 1.9, 2.1), 'punch'),
] + [
    # Four bars with a cut on every beat.
    shot(108 + i, 1, clip, src, 1.0, push(cx, cy, z, z + 0.08), 'punch' if i % 4 == 0 else 'cut')
    for i, (clip, src, cx, cy, z) in enumerate([
        ('melee_soulwarrior', 0.3, 0.4, 0.5, 1.3), ('ranged_dungdefender', 1.6, 0.55, 0.5, 1.3),
        ('magic_brokenvessel', 1.9, 0.45, 0.45, 1.3), ('summon_mawlek', 1.0, 0.5, 0.5, 1.4),
        ('melee_hiveknight', 11.0, 0.42, 0.55, 1.3), ('ranged_dungdefender', 4.0, 0.6, 0.5, 1.3),
        ('magic_brokenvessel', 1.2, 0.45, 0.45, 1.3), ('flight_pathofpain', 2.2, 0.5, 0.5, 1.3),
        ('melee_soulwarrior', 2.4, 0.5, 0.5, 1.3), ('summon_mawlek', 2.7, 0.5, 0.5, 1.4),
        ('magic_brokenvessel', 2.6, 0.5, 0.45, 1.3), ('gravity_ceiling', 2.2, 0.5, 0.45, 1.9),
        ('melee_hiveknight', 15.0, 0.45, 0.55, 1.3), ('ranged_dungdefender', 6.0, 0.6, 0.5, 1.3),
        ('melee_hiveknight', 17.4, 0.45, 0.55, 1.3), ('melee_hiveknight', 21.6, 0.5, 0.6, 1.3),
    ])
] + [
    # The last bar slowed down, pushing in, into the title on the final hit.
    shot(124, 4, 'summon_mawlek', 4.0, 0.5, push(0.5, 0.55, 1.3, 1.6), 'punch'),
]
# Outro (bar 32 on): the title card, made by make_promo.py, from the final hit to the end.
END_CARD = dict(t0=SECTIONS['outro'], t1=LENGTH)

# Captions: (from beat, to beat, kind, lines). kind: title (the intro's, mid-screen), class (a class
# card, top left), caption (the story, centred low), badge (top centre).
TEXTS = [
    (1, 7.5, 'title', ['当泰拉瑞亚', 'WHEN TERRARIA']),
    (9, 15.5, 'title', ['闯进空洞骑士', 'CRASHES INTO HOLLOW KNIGHT']),
    (16, 64, 'badge', ['大师模式', 'MASTER MODE']),
    (16, 23.6, 'class', ['近战', 'MELEE', '天顶剑  ZENITH', '灵魂战士  SOUL WARRIOR']),
    (24, 31.6, 'class', ['远程', 'RANGED', '太空海豚机枪  S.D.M.G.', '粪虫防御者  DUNG DEFENDER']),
    (32, 39.6, 'class', ['魔法', 'MAGIC', '终极棱镜 · 月耀  LAST PRISM', '残破容器  BROKEN VESSEL']),
    (40, 47.6, 'class', ['召唤', 'SUMMONER', '星尘之龙 · 泰拉棱镜', '躁郁的毛里克  BROODING MAWLEK']),
    (48, 55.6, 'class', ['近战', 'MELEE', '狂星之怒  STAR WRATH', '蜂巢骑士  HIVE KNIGHT']),
    (64, 71.6, 'caption', ['工作台 · 熔炉 · 铁砧，合成全照搬', 'BUILD AND CRAFT, JUST LIKE TERRARIA']),
    (72, 79.6, 'caption', ['泰拉瑞亚的地图 · 晶塔传送', 'THE MAP AND THE PYLONS CAME TOO']),
    (80, 91.6, 'caption', ['耀斑镐：从德特茅斯直接挖到十字路', 'A PICKAXE STRAIGHT DOWN TO THE CROSSROADS']),
    (92, 103.6, 'caption', ['苦痛之路？飞过去就好', 'PATH OF PAIN? JUST FLY OVER IT']),
    (104, 107.8, 'caption', ['重力球：天花板也是地面', 'GRAVITY GLOBE: THE CEILING IS A FLOOR']),
]
