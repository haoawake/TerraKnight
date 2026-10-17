"""Builds the trailer as a JianyingPro draft ("空洞泰拉宣传片") with the jianying-editor skill, from the
same edit as the render (edl.py): every shot on the video track with its speed, its crop as the
segment's scale and position (pushing in by keyframes), Jianying transitions where the render has
its own, the captions as text segments, music.wav, and the title card (endcard.mp4, rendered by
make_promo.py --endcard) at the end. Open the draft in JianyingPro to adjust it or export it.

    python make_promo.py --endcard
    python make_jianying.py
"""
import os
import sys

current_dir = os.path.dirname(os.path.abspath(__file__))
env_root = os.getenv("JY_SKILL_ROOT", "").strip()
skill_candidates = [
    env_root,
    os.path.join(current_dir, ".claude", "skills", "jianying-editor"),
    os.path.join(current_dir, "skills", "jianying-editor"),
    os.path.abspath(".claude/skills/jianying-editor"),
    os.path.expanduser("~/.claude/skills/jianying-editor"),
]
scripts_path = None
for p in skill_candidates:
    if p and os.path.exists(os.path.join(p, "scripts", "jy_wrapper.py")):
        scripts_path = os.path.join(os.path.abspath(p), "scripts")
        break
if not scripts_path:
    raise ImportError("Could not find jianying-editor/scripts/jy_wrapper.py (set JY_SKILL_ROOT)")
if scripts_path not in sys.path:
    sys.path.insert(0, scripts_path)

from jy_wrapper import JyProject  # noqa: E402
import pyJianYingDraft as draft  # noqa: E402
from pyJianYingDraft import trange  # noqa: E402

sys.path.insert(0, current_dir)
import edl  # noqa: E402

US = 1_000_000
SW, SH = 1920, 1200
FIT = 1080 / SH  # Jianying fits the 16:10 clips to the canvas's height

# The render's transitions, as Jianying's own (on the shot before the cut), and their lengths.
TRANSITIONS = {'impact': ('闪白', 0.3), 'punch': ('推近', 0.2), 'whip': ('横向模糊', 0.25), 'fade': ('叠化', 0.3)}


def placement(cx, cy, z):
    """A crop of the clip (centre as fractions, zoom) as Jianying's scale and position (half-canvas units)."""
    return z / FIT, -(cx * SW - SW / 2) * z / (edl.W / 2), (cy * SH - SH / 2) * z / (edl.H / 2)


def main():
    project = JyProject("空洞泰拉宣传片" + ("（中文版）" if edl.LANG == 'zh' else ""), width=edl.W, height=edl.H, overwrite=True)
    project._ensure_track(draft.TrackType.video, "VideoTrack")
    materials = {}
    segments = []
    for s in edl.SHOTS:
        path = os.path.join(current_dir, "clips", s['clip'] + ".mp4")
        if path not in materials:
            materials[path] = draft.VideoMaterial(project._stage_local_asset(path))
        mat = materials[path]
        t0 = round(s['t0'] * US)
        dur = round(s['t1'] * US) - t0
        src = round(s['src'] * US)
        src_dur = min(round(dur * s['speed']), mat.duration - src)
        (cx0, cy0, z0), (cx1, cy1, z1) = s['crop']
        k0, x0, y0 = placement(cx0, cy0, z0)
        k1, x1, y1 = placement(cx1, cy1, z1)
        seg = draft.VideoSegment(mat, trange(t0, dur), source_timerange=trange(src, src_dur), volume=0.0,
                                 clip_settings=draft.ClipSettings(scale_x=k0, scale_y=k0, transform_x=x0, transform_y=y0))
        # The push-in: scale and position from the shot's start to its end.
        for prop, a, b in ((draft.KeyframeProperty.uniform_scale, k0, k1), (draft.KeyframeProperty.position_x, x0, x1),
                           (draft.KeyframeProperty.position_y, y0, y1)):
            seg.add_keyframe(prop, 0, a)
            seg.add_keyframe(prop, dur - 1, b)
        project.script.add_segment(seg, "VideoTrack")
        segments.append(seg)

    # Jianying's transitions sit on the shot before the one coming in.
    for i, s in enumerate(edl.SHOTS[1:], 1):
        if s['fx'] in TRANSITIONS:
            name, d = TRANSITIONS[s['fx']]
            project.add_transition_simple(name, segments[i - 1], duration=f"{d}s")

    # The title card, rendered.
    card = os.path.join(current_dir, "endcard.mp4")
    if os.path.exists(card):
        mat = draft.VideoMaterial(project._stage_local_asset(card))
        t0 = segments[-1].target_timerange.end
        project.script.add_segment(draft.VideoSegment(mat, trange(t0, mat.duration), volume=0.0), "VideoTrack")
    else:
        print("endcard.mp4 missing: run make_promo.py --endcard first")

    # Captions.
    for b0, b1, kind, lines in edl.TEXTS:
        start, length = f"{edl.b(b0):.3f}s", f"{edl.b(b1) - edl.b(b0):.3f}s"
        if kind == 'class':
            text = f"{lines[0]}  {lines[1]}\n{lines[2]}\nVS  {lines[3]}"
            style = draft.TextStyle(size=7.0, bold=True, color=(1.0, 1.0, 1.0), align=0)
            pos = draft.ClipSettings(transform_x=-0.62, transform_y=0.62)
            anim = ('向右滑动', '向左滑动')
        elif kind == 'badge':
            text = f"{lines[0]}  {lines[1]}"
            style = draft.TextStyle(size=4.5, bold=True, color=(1.0, 0.36, 0.32), align=1)
            pos = draft.ClipSettings(transform_y=0.9)
            anim = ('弹入', '弹出')
        elif kind == 'title':
            text = "\n".join(lines)
            style = draft.TextStyle(size=10.0, bold=True, color=(1.0, 1.0, 1.0), align=1, letter_spacing=4)
            pos = draft.ClipSettings(transform_y=0.0)
            anim = ('开幕', '向上溶解')
        else:
            text = "\n".join(lines)
            style = draft.TextStyle(size=6.5, bold=True, color=(1.0, 1.0, 1.0), align=1)
            pos = draft.ClipSettings(transform_y=-0.72)
            anim = ('向上滑动', '向上溶解')
        project.add_text_simple(text, start, length, track_name="Text_" + kind, font=draft.FontType.思源粗宋, style=style,
                                clip_settings=pos, anim_in=anim[0], anim_out=anim[1])

    music = draft.AudioMaterial(project._stage_local_asset(os.path.join(current_dir, "music.wav")))
    project._ensure_track(draft.TrackType.audio, "Music")
    project.script.add_segment(draft.AudioSegment(music, trange(0, music.duration)), "Music")
    result = project.save()
    print(result)


if __name__ == "__main__":
    main()
