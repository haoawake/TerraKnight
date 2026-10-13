"""Renders the trailer: the shots of edl.py cut to music.wav, with the transitions, captions and the
title card, frame by frame (1920x1080, 30 fps) into TerraKnight_promo.mp4.

    python make_promo.py                  the whole trailer
    python make_promo.py --range 30 40    only 30 s to 40 s (a quick look), into preview.mp4
"""
import argparse, math, os, re, subprocess, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import edl
from tools import ffmpeg

HERE = os.path.dirname(os.path.abspath(__file__))
FF = ffmpeg()
CLIPS = os.path.join(HERE, 'clips')
W, H, FPS = edl.W, edl.H, edl.FPS
SW, SH = 1920, 1200  # the clips
SERIF = 'C:/Windows/Fonts/NotoSerifSC-VF.ttf'
SANS = 'C:/Windows/Fonts/NotoSansSC-VF.ttf'
CINZEL = os.path.join(HERE, 'assets', 'Cinzel.ttf')
LOGO = os.path.join(HERE, '..', 'src', 'TerraKnight', 'Resources', 'title_zh.png')
# Where the beat pulses, and how strongly: the groove lightly, the drops fully.
DROPS = [(edl.SECTIONS['build'], edl.SECTIONS['drop1'], 0.5), (edl.SECTIONS['drop1'], edl.SECTIONS['break'], 1.0), (edl.SECTIONS['drop2'], edl.SECTIONS['outro'], 1.0)]


def font(path, size, style=None):
    f = ImageFont.truetype(path, size)
    if style:
        f.set_variation_by_name(style)
    return f


def ease_out(x):
    x = min(1.0, max(0.0, x))
    return 1 - (1 - x) ** 3


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


# ---------------------------------------------------------------- the clips

class Reader:
    """One clip's frames from a point on, decoded by ffmpeg as they're asked for (in order)."""

    def __init__(self, clip, start):
        self.p = subprocess.Popen([FF, '-loglevel', 'quiet', '-ss', f'{start:.3f}', '-i', os.path.join(CLIPS, clip + '.mp4'),
                                   '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-'], stdout=subprocess.PIPE, bufsize=SW * SH * 3 * 2)
        self.n = -1
        self.frame = np.zeros((SH, SW, 3), np.uint8)

    def get(self, n):
        while self.n < n:
            buf = self.p.stdout.read(SW * SH * 3)
            if len(buf) < SW * SH * 3:
                self.n = n  # past the clip's end: its last frame held
                break
            self.frame = np.frombuffer(buf, np.uint8).reshape(SH, SW, 3)
            self.n += 1
        return self.frame

    def close(self):
        try:
            self.p.stdout.close()
            self.p.kill()
        except Exception:
            pass


readers = {}
clip_fps = {}


def fps_of(clip):
    """A clip's frame rate (the first set was recorded at 30, the second at 50)."""
    if clip not in clip_fps:
        out = subprocess.run([FF, '-i', os.path.join(CLIPS, clip + '.mp4')], stderr=subprocess.PIPE, text=True, errors='replace').stderr
        m = re.search(r'(\d+(?:\.\d+)?) fps', out)
        clip_fps[clip] = float(m.group(1)) if m else 30.0
    return clip_fps[clip]


def source(i, t):
    """Shot i's clip frame at time t of the trailer (past the shot's end it plays on, for a transition)."""
    s = edl.SHOTS[i]
    if i not in readers:
        readers[i] = Reader(s['clip'], s['src'])
    return readers[i].get(int((t - s['t0']) * s['speed'] * fps_of(s['clip']) + 1e-6))


def framed(i, t, zoom=1.0, dx=0.0, dy=0.0):
    """Shot i at time t: its crop of the clip (eased from the shot's start to its end), scaled to 1920x1080."""
    s = edl.SHOTS[i]
    u = smooth((t - s['t0']) / (s['t1'] - s['t0']))
    (cx0, cy0, z0), (cx1, cy1, z1) = s['crop']
    cx, cy, z = cx0 + (cx1 - cx0) * u, cy0 + (cy1 - cy0) * u, (z0 + (z1 - z0) * u) * zoom
    cw = SW / z
    ch = cw * 9 / 16
    if ch > SH:
        ch, cw = SH, SH * 16 / 9
    x0 = min(max(cx * SW - cw / 2 + dx, 0), SW - cw)
    y0 = min(max(cy * SH - ch / 2 + dy, 0), SH - ch)
    img = Image.fromarray(source(i, t)).resize((W, H), Image.BILINEAR, box=(x0, y0, x0 + cw, y0 + ch))
    return np.asarray(img, np.float32)


def beat_pulse(t):
    """In the drops, a kick of zoom and light on every beat, bigger on a bar's first."""
    for a, z, gain in DROPS:
        if a <= t < z:
            k = (t - a) / edl.BEAT
            n = math.floor(k + 1e-6)
            dt = (k - n) * edl.BEAT
            strong = n % 4 == 0
            return 1 + gain * (0.022 if strong else 0.012) * math.exp(-dt * 14), gain * (0.07 if strong else 0.03) * math.exp(-dt * 16)
    return 1.0, 0.0


def hblur(img, amount):
    """A horizontal smear (the whip's motion blur), amount in pixels."""
    k = int(amount / 12)
    if k < 1:
        return img
    acc = img.copy()
    for j in range(1, k + 1):
        acc += np.roll(img, j * 12, axis=1)
    return acc / (k + 1)


def shot_frame(i, t):
    s = edl.SHOTS[i]
    local = t - s['t0']
    fx = s['fx']
    zoom, light = beat_pulse(t)
    flash, split, dx, dy = 0.0, 0.0, 0.0, 0.0
    if fx == 'punch':
        zoom *= 1 + 0.09 * (1 - min(1, local / 0.2)) ** 2
        flash = 0.35 * max(0.0, 1 - local / 0.18)
    elif fx == 'impact':
        zoom *= 1 + 0.16 * (1 - min(1, local / 0.4)) ** 2
        flash = max(0.0, 1 - local / 0.45) ** 1.5
        a = 22 * max(0.0, 1 - local / 0.55)
        dx, dy = a * math.sin(local * 97), a * math.cos(local * 71)
        split = 16 * max(0.0, 1 - local / 0.45)
    elif fx == 'cut':
        zoom *= 1 + 0.035 * max(0.0, 1 - local / 0.12)
    img = framed(i, t, zoom, dx, dy)
    if fx == 'whip' and local < 0.2 and i > 0:
        # This shot comes in from the right as the one before slides out to the left.
        p = ease_out(local / 0.2)
        prev = framed(i - 1, t)
        off = int((1 - p) * W)
        out = img.copy()
        if off > 0:
            out[:, off:] = img[:, :W - off]
            out[:, :off] = prev[:, W - off:]
        img = hblur(out, 140 * (1 - p))
    if fx == 'fade' and local < 0.3 and i > 0:
        a = smooth(local / 0.3)
        img = framed(i - 1, t) * (1 - a) + img * a
    if split > 0.5:
        o = int(split)
        img[:, :, 0] = np.roll(img[:, :, 0], o, axis=1)
        img[:, :, 2] = np.roll(img[:, :, 2], -o, axis=1)
    if light:
        img *= 1 + light
    if flash:
        img += (255 - img) * flash
    return img


# ---------------------------------------------------------------- grading

yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
r2 = ((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2
VIGNETTE = (1 - 0.32 * np.clip(r2 - 0.25, 0, None))[..., None]


def grade(img):
    g = img.mean(axis=2, keepdims=True)
    img = g + (img - g) * 1.15        # a little more colour
    img = (img - 128) * 1.07 + 132    # and contrast
    return img * VIGNETTE


# ---------------------------------------------------------------- captions

def spaced(draw, xy, text, f, fill, tracking):
    x, y = xy
    for ch in text:
        draw.text((x, y), ch, font=f, fill=fill)
        x += draw.textlength(ch, font=f) + tracking


def spaced_width(text, f, tracking):
    d = ImageDraw.Draw(Image.new('L', (1, 1)))
    return sum(d.textlength(ch, font=f) for ch in text) + tracking * (len(text) - 1)


def with_shadow(layer, blur=8, alpha=0.8, offset=3):
    a = np.asarray(layer)[..., 3].astype(np.float32) / 255
    sh = Image.fromarray((a * 255 * alpha).astype(np.uint8)).filter(ImageFilter.GaussianBlur(blur))
    out = Image.new('RGBA', layer.size, (0, 0, 0, 0))
    out.paste((0, 0, 0, 255), (0, offset), sh)
    out.alpha_composite(layer)
    return out


def caption_card(zh, en):
    f1, f2 = font(SERIF, 66, 'Bold'), font(CINZEL, 30, 'Bold')
    layer = Image.new('RGBA', (W, 200), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    w1 = d.textlength(zh, font=f1)
    d.text(((W - w1) / 2, 20), zh, font=f1, fill=(255, 255, 255, 255))
    w2 = spaced_width(en, f2, 6)
    spaced(d, ((W - w2) / 2, 118), en, f2, (232, 212, 160, 255), 6)
    return with_shadow(layer), (0, H - 250)


def class_card(zh, en, weapon, boss):
    f1, f2 = font(SERIF, 124, 'Black'), font(CINZEL, 46, 'Bold')
    f3, f4 = font(SANS, 36, 'Medium'), font(SANS, 34, 'Bold')
    layer = Image.new('RGBA', (1100, 330), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.text((0, 0), zh, font=f1, fill=(255, 255, 255, 255))
    x = d.textlength(zh, font=f1) + 26
    spaced(d, (x, 66), en, f2, (232, 212, 160, 255), 10)
    d.rectangle((4, 172, 520, 175), fill=(232, 212, 160, 230))
    d.text((4, 190), weapon, font=f3, fill=(240, 240, 240, 255))
    d.text((4, 246), 'VS  ' + boss, font=f4, fill=(255, 120, 110, 255))
    return with_shadow(layer, 10, 0.85), (80, 96)


def title_card(zh, en):
    f1, f2 = font(SERIF, 92, 'Bold'), font(CINZEL, 36, 'Bold')
    layer = Image.new('RGBA', (W, 230), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    w1 = spaced_width(zh, f1, 14)
    spaced(d, ((W - w1) / 2, 10), zh, f1, (255, 255, 255, 255), 14)
    w2 = spaced_width(en, f2, 12)
    spaced(d, ((W - w2) / 2, 150), en, f2, (232, 212, 160, 255), 12)
    return with_shadow(layer, 12, 0.9), (0, H // 2 - 115)


def badge_card(zh, en):
    f1, f2 = font(SERIF, 36, 'Black'), font(CINZEL, 22, 'Bold')
    layer = Image.new('RGBA', (520, 100), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    w1 = d.textlength(zh, font=f1)
    w2 = spaced_width(en, f2, 8)
    d.text(((520 - w1) / 2, 6), zh, font=f1, fill=(255, 90, 80, 255))
    spaced(d, ((520 - w2) / 2, 58), en, f2, (255, 170, 150, 255), 8)
    return with_shadow(layer, 6, 0.9), ((W - 520) // 2, 22)


CARDS = []
for b0, b1, kind, lines in edl.TEXTS:
    img, pos = {'caption': caption_card, 'class': class_card, 'badge': badge_card, 'title': title_card}[kind](*lines)
    CARDS.append((edl.b(b0), edl.b(b1), kind, np.asarray(img, np.float32), pos))


def overlay(img, t):
    for t0, t1, kind, card, (x, y) in CARDS:
        if not (t0 <= t < t1):
            continue
        a = min(smooth((t - t0) / 0.22), smooth((t1 - t) / 0.2))
        if kind == 'class':
            x += int(-60 * (1 - ease_out((t - t0) / 0.3)))
        elif kind in ('caption', 'title'):
            y += int(18 * (1 - ease_out((t - t0) / 0.3)))
        h, w = card.shape[:2]
        x0, y0 = max(x, 0), max(y, 0)
        x1, y1 = min(x + w, W), min(y + h, H)
        if x1 <= x0 or y1 <= y0:
            continue
        c = card[y0 - y:y1 - y, x0 - x:x1 - x]
        al = c[..., 3:4] / 255 * a
        img[y0:y1, x0:x1] = img[y0:y1, x0:x1] * (1 - al) + c[..., :3] * al
    return img


# ---------------------------------------------------------------- the title card

rng = np.random.default_rng(3)
cy_, cx_ = H * 1.05, W / 2
bg = np.zeros((H, W, 3), np.float32)
d2 = ((xx - cx_) / (W * 0.62)) ** 2 + ((yy - cy_) / (H * 0.95)) ** 2
glow = np.exp(-d2 * 1.6)[..., None]
beam = (np.exp(-((xx - W / 2) / (W * 0.16)) ** 2) * np.clip(yy / H, 0, 1) ** 1.5)[..., None]
END_BG = np.array([4, 7, 18], np.float32) + np.array([38, 72, 150], np.float32) * glow + np.array([30, 50, 110], np.float32) * beam
PARTICLES = [dict(x=rng.uniform(0, W), y=rng.uniform(0, H), v=rng.uniform(12, 45), s=rng.uniform(1.2, 4.5),
                  b=rng.uniform(0.25, 1.0), ph=rng.uniform(0, 6.28)) for _ in range(110)]


def sprite(sig):
    r = int(sig * 4) + 1
    g = np.mgrid[-r:r + 1, -r:r + 1].astype(np.float32)
    return np.exp(-(g[0] ** 2 + g[1] ** 2) / (2 * sig * sig))


SPRITES = {}
logo_src = Image.open(LOGO).convert('RGBA')
LW = 1000
logo_base = logo_src.resize((LW, int(logo_src.height * LW / logo_src.width)), Image.LANCZOS)
logo_glow = np.asarray(logo_base.split()[3].filter(ImageFilter.GaussianBlur(18)), np.float32) / 255


def text_layer(text, f, tracking, fill):
    w = int(spaced_width(text, f, tracking)) + 40
    layer = Image.new('RGBA', (w, f.size + 60), (0, 0, 0, 0))
    spaced(ImageDraw.Draw(layer), (20, 20), text, f, fill, tracking)
    return layer


EN_FONT = font(CINZEL, 118, 'Black')
SUB = text_layer('TERRARIA  ×  HOLLOW KNIGHT', font(CINZEL, 34, 'Bold'), 10, (176, 196, 236, 255))


def paste(img, layer, cx, cy, alpha, glow_color=None, glow_amount=0.0):
    a = np.asarray(layer, np.float32)
    h, w = a.shape[:2]
    x, y = int(cx - w / 2), int(cy - h / 2)
    x0, y0, x1, y1 = max(x, 0), max(y, 0), min(x + w, W), min(y + h, H)
    c = a[y0 - y:y1 - y, x0 - x:x1 - x]
    al = c[..., 3:4] / 255 * alpha
    if glow_color is not None and glow_amount > 0:
        g = np.asarray(layer.split()[3].filter(ImageFilter.GaussianBlur(14)), np.float32)[y0 - y:y1 - y, x0 - x:x1 - x, None] / 255
        img[y0:y1, x0:x1] += g * np.array(glow_color, np.float32) * glow_amount * alpha
    img[y0:y1, x0:x1] = img[y0:y1, x0:x1] * (1 - al) + c[..., :3] * al


def end_card(t):
    img = END_BG.copy()
    for p in PARTICLES:
        y = (p['y'] - p['v'] * t) % H
        x = p['x'] + 14 * math.sin(t * 0.6 + p['ph'])
        sig = p['s']
        key = round(sig, 1)
        if key not in SPRITES:
            SPRITES[key] = sprite(key)
        sp = SPRITES[key]
        r = sp.shape[0] // 2
        xi, yi = int(x), int(y)
        if r <= xi < W - r and r <= yi < H - r:
            tw = 0.6 + 0.4 * math.sin(t * 2.2 + p['ph'] * 3)
            img[yi - r:yi + r + 1, xi - r:xi + r + 1] += sp[..., None] * np.array([150, 190, 255], np.float32) * p['b'] * tw
    # The logo: in on the hit, settling from a little large; a light sweeps over it later.
    s = 1.16 - 0.16 * ease_out(t / 0.8)
    la = smooth(t / 0.3)
    logo = logo_base if s == 1.0 else logo_base.resize((int(LW * s), int(logo_base.height * s)), Image.BILINEAR)
    lcx, lcy = W / 2, 400
    gh, gw = logo_glow.shape
    gx, gy = int(lcx - gw / 2), int(lcy - gh / 2)
    img[gy:gy + gh, gx:gx + gw] += logo_glow[..., None] * np.array([90, 140, 255], np.float32) * (0.45 + 0.1 * math.sin(t * 2)) * la
    paste(img, logo, lcx, lcy, la)
    if 1.5 < t < 2.7:
        u = (t - 1.5) / 1.2
        la_ = np.asarray(logo_base.split()[3], np.float32) / 255
        lx = np.arange(gw, dtype=np.float32)[None, :]
        ly = np.arange(gh, dtype=np.float32)[:, None]
        band = np.exp(-(((lx + ly * 0.5) - (-300 + u * (gw + 600))) / 70) ** 2)
        img[gy:gy + gh, gx:gx + gw] += (band * la_)[..., None] * 140
    # The English name, letters closing up as it comes in, then the line under it.
    if t > 0.45:
        u = ease_out((t - 0.45) / 0.9)
        en = text_layer('TERRAKNIGHT', EN_FONT, 18 + 40 * (1 - u), (238, 242, 255, 255))
        paste(img, en, W / 2, 770, smooth((t - 0.45) / 0.5), (80, 130, 255), 0.9)
    if t > 1.1:
        paste(img, SUB, W / 2, 880, smooth((t - 1.1) / 0.5))
    img += (255 - img) * max(0.0, 1 - t / 0.5) ** 1.5
    return img


# ---------------------------------------------------------------- the whole

def frame_at(t):
    if t >= edl.END_CARD['t0']:
        img = end_card(t - edl.END_CARD['t0'])
        fade = smooth((edl.LENGTH - t) / 1.3)
        return np.clip(img * fade, 0, 255).astype(np.uint8)
    i = next(k for k, s in enumerate(edl.SHOTS) if s['t0'] <= t < s['t1'])
    for k in list(readers):
        if k < i - 1:
            readers.pop(k).close()
    img = grade(shot_frame(i, t))
    img = overlay(img, t)
    if t < 0.7:
        img *= smooth(t / 0.7)  # out of black
    return np.clip(img, 0, 255).astype(np.uint8)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--range', nargs=2, type=float)
    ap.add_argument('--endcard', action='store_true', help='only the title card, silent, into endcard.mp4 (for the Jianying draft)')
    a = ap.parse_args()
    if a.endcard:
        t_from, t_to = edl.END_CARD['t0'], edl.LENGTH
        out = os.path.join(HERE, 'endcard.mp4')
    else:
        t_from, t_to = a.range if a.range else (0.0, edl.LENGTH)
        suffix = '_zh' if edl.LANG == 'zh' else ''
        out = os.path.join(HERE, ('preview' if a.range else 'TerraKnight_promo') + suffix + '.mp4')
    n0, n1 = int(round(t_from * FPS)), int(round(t_to * FPS))
    audio = [] if a.endcard else ['-ss', f'{t_from:.3f}', '-t', f'{t_to - t_from:.3f}', '-i', os.path.join(HERE, 'music.wav')]
    cmd = [FF, '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{W}x{H}', '-r', str(FPS), '-i', '-',
           *audio, '-c:v', 'libx264', '-preset', 'slow', '-crf', '17', '-pix_fmt', 'yuv420p',
           *([] if a.endcard else ['-c:a', 'aac', '-b:a', '256k', '-shortest']), '-movflags', '+faststart', out]
    enc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for n in range(n0, n1):
        t = n / FPS
        if t >= edl.END_CARD['t0']:
            for k in list(readers):
                readers.pop(k).close()
        enc.stdin.write(frame_at(t).tobytes())
        if n % 150 == 0:
            print(f'{t:5.1f}s', flush=True)
    enc.stdin.close()
    enc.wait()
    for r in readers.values():
        r.close()
    print('wrote', out)


if __name__ == '__main__':
    main()
