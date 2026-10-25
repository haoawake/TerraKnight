"""Pixel art for the casino's full-screen machines (TerraKnight Resources/CasinoPx_*.png): each machine about 270 px tall,
drawn on screen at a whole number of screen pixels a pixel so it fills the screen's height; the parts that move (the
slot's lever, reels, the wheel and its ball, the pinball's baffles and plunger) are their own pictures.

Coordinates of every part the game moves or lights are in Items/CasinoScenes.cs, and the pinball's geometry in
Items/CasinoPinSim.cs (its marbles' physics); keep them in step.

usage: python -I casino_scene_art.py <terraria item png dir> <out dir>
"""
import math
import os
import random
import sys

from PIL import Image

SRC, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)


def hexc(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


GOLD = [hexc(c) for c in ("3a2208", "6a4012", "a8701e", "dca434", "f8d870", "fff6c8")]
NAVY = [hexc(c) for c in ("0a0e1c", "141c34", "1e2a4c", "2c3c68", "40558e")]
WINE = [hexc(c) for c in ("2a070e", "4a0e1a", "721828", "9e2a3a", "c8485a")]
WOOD = [hexc(c) for c in ("1e100a", "3a2014", "5a321e", "7c4a2a", "9c6438")]
TEAL = [hexc(c) for c in ("06201e", "0c3430", "145048", "227064", "3c9a86", "8ee0c8")]
IVORY = [hexc(c) for c in ("6e5e44", "a8987a", "dccfb0", "f2ead2", "fffcf0")]
LAMP = [hexc(c) for c in ("6a2a06", "b85a10", "f09a28", "ffd060", "fff4c0")]
RED = [hexc(c) for c in ("3a0606", "7a1010", "c02424", "f05050", "ffa0a0")]
GREEN = [hexc(c) for c in ("06200e", "0e4020", "177034", "28a050", "70e090")]
FELT = [hexc(c) for c in ("06281a", "0c3a26", "125034", "1a6a44", "2a8a5a")]
GREY = [hexc(c) for c in ("101014", "24242c", "383844", "4c4c5a", "60606e", "8a8a98")]
DARK = hexc("08060a")
CLEAR = (0, 0, 0, 0)


class Canvas:
    def __init__(self, w, h, fill=CLEAR):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), fill)
        self.p = self.img.load()

    def px(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            if len(c) == 4 and c[3] < 255 and c[3] > 0:
                b = self.p[x, y]
                a = c[3] / 255
                self.p[x, y] = (int(b[0] * (1 - a) + c[0] * a), int(b[1] * (1 - a) + c[1] * a), int(b[2] * (1 - a) + c[2] * a), max(b[3], c[3]))
            else:
                self.p[x, y] = c

    def get(self, x, y):
        return self.p[x, y] if 0 <= x < self.w and 0 <= y < self.h else CLEAR

    def rect(self, x0, y0, x1, y1, c):
        for y in range(int(y0), int(y1) + 1):
            for x in range(int(x0), int(x1) + 1):
                self.px(x, y, c)

    def frame(self, x0, y0, x1, y1, c):
        for x in range(int(x0), int(x1) + 1):
            self.px(x, y0, c)
            self.px(x, y1, c)
        for y in range(int(y0), int(y1) + 1):
            self.px(x0, y, c)
            self.px(x1, y, c)

    def bevel(self, x0, y0, x1, y1, ramp, fill=2, outline=True):
        self.rect(x0, y0, x1, y1, ramp[fill])
        for x in range(x0 + 1, x1):
            self.px(x, y0 + 1, ramp[fill + 1])
            self.px(x, y1 - 1, ramp[fill - 1])
        for y in range(y0 + 1, y1):
            self.px(x0 + 1, y, ramp[fill + 1])
            self.px(x1 - 1, y, ramp[fill - 1])
        if outline:
            self.frame(x0, y0, x1, y1, ramp[0])

    def trim(self, x0, y0, x1, y1, rivet=9):
        """A gold band: lit top row, shadow row, rivets along its middle."""
        self.bevel(x0, y0, x1, y1, GOLD, 3)
        self.rect(x0 + 1, y0 + 1, x1 - 1, y0 + 1, GOLD[5])
        self.rect(x0 + 1, y1 - 1, x1 - 1, y1 - 1, GOLD[1])
        if y1 - y0 >= 5 and rivet:
            for x in range(x0 + 4, x1 - 2, rivet):
                self.px(x, (y0 + y1) // 2, GOLD[5])
                self.px(x + 1, (y0 + y1) // 2 + 1, GOLD[1])

    def disc(self, cx, cy, r, c):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                    self.px(x, y, c)

    def ring(self, cx, cy, r0, r1, c):
        for y in range(int(cy - r1 - 1), int(cy + r1 + 2)):
            for x in range(int(cx - r1 - 1), int(cx + r1 + 2)):
                d = (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2
                if r0 * r0 <= d <= r1 * r1:
                    self.px(x, y, c)

    def ellipse(self, cx, cy, rx, ry, c, top_only=False):
        for y in range(int(cy - ry - 1), int(cy + ry + 2)):
            if top_only and y + 0.5 > cy:
                continue
            for x in range(int(cx - rx - 1), int(cx + rx + 2)):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1:
                    self.px(x, y, c)

    def shade_ball(self, cx, cy, r, ramp):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                dx, dy = (x + 0.5 - cx) / r, (y + 0.5 - cy) / r
                if dx * dx + dy * dy > 1:
                    continue
                lit = 0.55 - 0.45 * (dx * 0.7 + dy * 0.7) - 0.25 * (dx * dx + dy * dy)
                k = int(max(0, min(len(ramp) - 1, round(lit * (len(ramp) - 1) + 0.6))))
                self.px(x, y, ramp[k])

    def tuft(self, x0, y0, x1, y1, ramp, step):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                u, v = (x - x0) % step, (y - y0) % step
                if u == v or u == step - 1 - v:
                    self.px(x, y, ramp[1])
        for y in range(y0 + step // 2, y1, step):
            for x in range(x0 + step // 2, x1, step):
                self.px(x, y, ramp[4])
                self.px(x + 1, y + 1, ramp[0])

    def grain(self, x0, y0, x1, y1, ramp, gap):
        for k, yb in enumerate(range(y0 + 2, y1, gap)):
            for x in range(x0 + 1, x1):
                y = yb + int(round(math.sin(x * 0.17 + k * 1.7) * 1.2))
                if y0 < y < y1:
                    self.px(x, y, ramp[1] if k % 3 else ramp[3])

    def outline(self, c):
        src = self.img.copy().load()
        for y in range(self.h):
            for x in range(self.w):
                if src[x, y][3] != 0:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < self.w and 0 <= yy < self.h and src[xx, yy][3] > 200:
                        self.p[x, y] = c
                        break

    def socket(self, cx, cy, r):
        """A lamp's socket (dark glass in a gold ring), lit by the game."""
        self.disc(cx, cy, r + 1.2, GOLD[1])
        self.ring(cx, cy, r, r + 1, GOLD[3])
        self.disc(cx, cy, r, hexc("2a1a10"))
        self.px(cx - r * 0.4, cy - r * 0.4, hexc("5a3a20"))

    def save(self, name):
        self.img.save(os.path.join(OUT, "CasinoPx_" + name + ".png"))
        print(name, self.img.size)


def lamp(cv, x, y, r):
    cv.disc(x, y, r + 1, LAMP[0])
    cv.shade_ball(x, y, r, LAMP[1:])


# ---------------------------------------------------------------- the room behind
def background():
    """The casino's wall: deep red damask in a 32-px tile, a gold thread through it."""
    cv = Canvas(32, 32, WINE[1])
    for y in range(32):
        for x in range(32):
            u, v = (x - 16) / 16, (y - 16) / 16
            d = abs(u) + abs(v)
            if 0.55 < d < 0.68 or (abs(u) < 0.1 and abs(v) < 0.35) or (abs(v) < 0.1 and abs(u) < 0.35):
                cv.px(x, y, WINE[2])
            if 0.85 < d < 0.92:
                cv.px(x, y, hexc("5a1420"))
    for k in range(0, 32, 4):
        cv.px(k, 0, hexc("6a2a14"))
    cv.save("BgWall")
    fl = Canvas(32, 16, hexc("2a0e12"))
    for y in range(16):
        for x in range(32):
            if (x + y * 2) % 8 == 0 or (x - y * 2) % 8 == 0:
                fl.px(x, y, hexc("4a1a10"))
            if (x // 8 + y // 8) % 2 == 0 and (x + y) % 8 == 4:
                fl.px(x, y, GOLD[1])
    fl.save("BgFloor")
    # A light bulb of the room's far lights, soft in pixels.
    b = Canvas(9, 9)
    for y in range(9):
        for x in range(9):
            d = math.hypot(x - 4, y - 4)
            a = 230 if d < 1.2 else 140 if d < 2.4 else 60 if d < 3.6 else 20 if d < 4.5 else 0
            if a:
                b.px(x, y, (255, 255, 255, a))
    b.save("Bokeh")


# ---------------------------------------------------------------- the slot
SYMBOLS = ["Item_8", "Item_181", "Item_75", "Item_327", "Item_4956"]
STRIPS = [(0, 1, 2, 3, 4), (0, 1, 2, 4, 3), (0, 2, 1, 3, 4)]
CELL_W, CELL_H = 36, 32


def load_symbol(name):
    im = Image.open(os.path.join(SRC, name + ".png")).convert("RGBA")
    if name == "Item_75":
        im = im.crop((0, 0, im.width, im.height // 8 - 2))
    return im.crop(im.getbbox())


def pixel_fit(im, size):
    if im.width <= size and im.height <= size:
        k = max(1, min(size // im.width, size // im.height))
        return im.resize((im.width * k, im.height * k), Image.NEAREST)
    k = max(im.width, im.height) / size
    w, h = max(1, int(im.width / k)), max(1, int(im.height / k))
    src = im.load()
    out = Image.new("RGBA", (w, h), CLEAR)
    o = out.load()
    for y in range(h):
        for x in range(w):
            counts = {}
            n = opaque = 0
            for yy in range(int(y * k), max(int(y * k) + 1, int((y + 1) * k))):
                for xx in range(int(x * k), max(int(x * k) + 1, int((x + 1) * k))):
                    c = src[min(xx, im.width - 1), min(yy, im.height - 1)]
                    n += 1
                    if c[3] > 0:
                        opaque += 1
                        counts[c] = counts.get(c, 0) + 1
            if opaque * 2.5 >= n and counts:
                o[x, y] = max(counts, key=counts.get)
    return out


def symbol_tile(i, size=26):
    return pixel_fit(load_symbol(SYMBOLS[i]), size)


def reel_big(order):
    """A reel's strip: its five symbols in its own order, each in a 36x32 cell on ivory, top to bottom."""
    n = len(order)
    cv = Canvas(CELL_W, CELL_H * n)
    for y in range(CELL_H * n):
        k = (y % CELL_H) / CELL_H
        cv.rect(0, y, CELL_W - 1, y, IVORY[3] if 0.15 < k < 0.85 else IVORY[2])
    for i, sym in enumerate(order):
        top = i * CELL_H
        cv.rect(2, top, CELL_W - 3, top, IVORY[1])
        tile = symbol_tile(sym)
        ox, oy = (CELL_W - tile.width) // 2, top + (CELL_H - tile.height) // 2
        sh = Image.new("RGBA", tile.size, CLEAR)
        sh.putalpha(tile.split()[3].point(lambda v: 60 if v > 0 else 0))
        cv.img.alpha_composite(sh, (ox + 1, oy + 1))
        cv.img.alpha_composite(tile, (ox, oy))
    return cv


def smear(cv):
    im = cv.img
    w, h = im.size
    src = im.load()
    out = Image.new("RGBA", (w, h))
    o = out.load()
    for x in range(w):
        for y in range(h):
            acc = [0, 0, 0]
            for k in range(8):
                c = src[x, (y - k) % h]
                for i in range(3):
                    acc[i] += c[i]
            o[x, y] = (acc[0] // 8, acc[1] // 8, acc[2] // 8, 255)
    q = out.convert("RGB").quantize(colors=12, method=Image.MEDIANCUT).convert("RGBA")
    c2 = Canvas(w, h)
    c2.img = q
    return c2


def window_shade():
    """Over the 3x3 window's 96 px: the drums turning away at the top and bottom (bands), a glass glint."""
    w, h = 116, 96
    cv = Canvas(w, h)
    for y in range(h):
        t = (y + 0.5) / h
        a = 150 if t < 0.06 or t > 0.94 else 90 if t < 0.12 or t > 0.88 else 40 if t < 0.2 or t > 0.8 else 0
        if a:
            for x in range(w):
                cv.px(x, y, (0, 0, 0, a))
    for k in range(18):
        cv.px(8 + k, 6 + k // 3, (255, 255, 255, 50))
        cv.px(9 + k, 6 + k // 3, (255, 255, 255, 30))
    return cv


def slot_cabinet():
    W, H = 220, 270
    cv = Canvas(W, H)
    # Feet and base.
    for fx in (22, 176):
        cv.bevel(fx, 260, fx + 21, 269, GOLD, 3)
    cv.trim(8, 254, 211, 261)
    # The tray.
    cv.bevel(8, 236, 211, 254, WINE, 2)
    cv.tuft(10, 238, 209, 252, WINE, 7)
    cv.bevel(56, 239, 163, 253, GOLD, 3)
    cv.rect(59, 242, 160, 250, NAVY[0])
    cv.rect(59, 248, 160, 250, GOLD[1])
    # The deck: dark wood framed gold, its LCDs and buttons drawn by the game.
    cv.bevel(6, 204, 213, 236, WOOD, 2)
    cv.grain(6, 204, 213, 236, WOOD, 5)
    cv.trim(6, 202, 213, 206)
    for (x0, x1) in ((16, 78), (84, 136), (142, 204)):
        cv.bevel(x0 - 2, 209, x1 + 2, 223, GOLD, 2)
        cv.rect(x0, 211, x1, 221, hexc("04140e"))
    # The body round the reels: navy, velvet sides, gold pillars.
    cv.bevel(8, 88, 211, 204, NAVY, 2)
    for y in range(90, 203):
        for x in range(9, 211):
            if (x * 3 + y * 5) % 23 == 0:
                cv.px(x, y, NAVY[3])
    for side in ((10, 40), (179, 209)):
        cv.bevel(side[0], 94, side[1], 198, WINE, 2)
        cv.tuft(side[0] + 2, 96, side[1] - 2, 196, WINE, 8)
    for px0 in (8, 42, 175, 209):
        cv.bevel(px0 - 1, 88, px0 + 2, 204, GOLD, 2)
    # The reel window: a heavy gold frame, three reels behind glass, mullions between.
    cv.bevel(46, 94, 173, 201, GOLD, 3)
    cv.rect(47, 95, 172, 95, GOLD[5])
    cv.bevel(49, 97, 170, 198, GOLD, 2)
    cv.rect(52, 100, 167, 195, IVORY[2])
    for mx in (88, 128):
        cv.bevel(mx, 98, mx + 3, 197, GOLD, 3)
    # Payline lamps each side (lit by the game).
    for y in (104, 116, 148, 180, 192):
        cv.socket(35, y, 3)
        cv.socket(184, y, 3)
    # The marquee: the paytable behind glass (written by the game), lamps along its foot.
    cv.trim(4, 82, 215, 89)
    for x in range(14, 210, 16):
        cv.socket(x, 85.5, 1.6)
    cv.bevel(10, 16, 209, 82, GOLD, 3)
    cv.rect(11, 17, 208, 17, GOLD[5])
    cv.rect(15, 21, 204, 77, hexc("0a0a1e"))
    random.seed(4)
    for _ in range(140):
        cv.px(random.randint(16, 203), random.randint(22, 76), random.choice([NAVY[2], NAVY[3], hexc("3a2a5a")]))
    for _ in range(18):
        x, y = random.randint(18, 200), random.randint(24, 74)
        cv.px(x, y, IVORY[3])
    # The crown: the crescent and its lamp, scrolls each side.
    cv.ellipse(110, 16, 60, 12, GOLD[3], top_only=True)
    cv.ellipse(110, 16, 57, 10, NAVY[2], top_only=True)
    cv.disc(110, 9, 9, GOLD[3])
    cv.disc(110, 5, 7.5, CLEAR)
    for y in range(0, 18):
        for x in range(100, 121):
            if cv.get(x, y) == GOLD[3] and cv.get(x - 1, y - 1)[3] == 0:
                cv.px(x, y, GOLD[5])
    cv.socket(110, 12, 3)
    for sx in (66, 154):
        cv.socket(sx, 11, 2.2)
    cv.outline(DARK)
    return cv


def lever():
    rod = Canvas(6, 70)
    for y in range(70):
        rod.rect(0, y, 5, y, GOLD[2])
        rod.px(1, y, GOLD[4])
        rod.px(2, y, GOLD[5] if y % 7 else GOLD[4])
        rod.px(4, y, GOLD[1])
    rod.frame(0, 0, 5, 69, GOLD[0])
    rod.save("LeverRod")
    knob = Canvas(18, 18)
    knob.disc(9, 9, 8.6, RED[0])
    knob.shade_ball(9, 9, 7.6, RED[1:])
    knob.px(6, 5, (255, 255, 255, 255))
    knob.px(5, 6, (255, 220, 220, 255))
    knob.save("LeverKnob")
    base = Canvas(18, 30)
    base.bevel(2, 0, 15, 29, GOLD, 3)
    base.rect(6, 4, 11, 25, NAVY[0])
    base.disc(9, 22, 4, GOLD[2])
    base.disc(9, 22, 2.4, GOLD[4])
    base.save("LeverBase")


# ---------------------------------------------------------------- push buttons, LCD
def push(name, ramp, w=24, h=12):
    for down in (0, 1):
        cv = Canvas(w, h)
        cv.bevel(0, 0, w - 1, h - 1, GOLD, 2)
        top = 2 + down
        cv.rect(2, top, w - 3, h - 3, ramp[2 - down])
        cv.rect(2, top, w - 3, top, ramp[min(4, 4 - down)])
        if not down:
            cv.rect(2, h - 3, w - 3, h - 3, ramp[0])
            for x in range(4, w - 4, 3):
                cv.px(x, top + 1, ramp[3])
        cv.save(name + ("Down" if down else ""))


# ---------------------------------------------------------------- the roulette
SECTORS = 65


def wheel_big(r=74):
    size = 2 * r + 1
    cv = Canvas(size, size)
    c = r + 0.5
    step = 360 / SECTORS
    for y in range(size):
        for x in range(size):
            dx, dy = x + 0.5 - c, y + 0.5 - c
            d = math.hypot(dx, dy)
            if d > r + 0.3:
                continue
            ang = (math.degrees(math.atan2(dx, -dy)) + step / 2) % 360
            sector = int(ang // step)
            frac = (ang % step) / step
            if d > r - 2:
                col = GOLD[3]
            elif d > r * 0.70:
                col = GREEN[2] if sector == 0 else RED[2] if sector % 2 == 1 else NAVY[0]
                if d > r * 0.92 and sector != 0:
                    col = RED[3] if sector % 2 == 1 else NAVY[1]
                if frac < 0.12:
                    col = GOLD[2]
            elif d > r * 0.66:
                col = GOLD[3]
            elif d > r * 0.26:
                col = WOOD[2] if int(ang // 45) % 2 == 0 else WOOD[3]
                if abs(d - r * 0.46) < 0.6:
                    col = WOOD[1]
            elif d > r * 0.24:
                col = GOLD[3]
            elif d > r * 0.22:
                col = GOLD[1]
            else:
                col = NAVY[0]
            cv.px(x, y, col)
    # The turret's spokes, out from the display's rim.
    for k in range(4):
        a = math.radians(45 + 90 * k)
        for t in range(int(r * 0.27), int(r * 0.6)):
            cv.px(c + math.cos(a) * t - 0.5, c + math.sin(a) * t - 0.5, GOLD[4])
        tip = (c + math.cos(a) * r * 0.6 - 0.5, c + math.sin(a) * r * 0.6 - 0.5)
        cv.disc(tip[0] + 0.5, tip[1] + 0.5, 2.2, GOLD[5])
    return cv


def wheel_bowl():
    """The bowl the wheel turns in: a gold rim with studs, a dark wooden ring, the ivory pointer at the top."""
    S = 180
    cv = Canvas(S, S)
    c = 90
    cv.disc(c, c, 88, GOLD[2])
    cv.ring(c, c, 85, 88, GOLD[3])
    cv.ring(c, c, 87, 88, GOLD[4])
    cv.disc(c, c, 84, WOOD[1])
    for y in range(S):
        for x in range(S):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c)
            if 76 < d < 84 and (int(math.degrees(math.atan2(y - c, x - c))) // 6) % 2 == 0:
                cv.px(x, y, WOOD[2])
    cv.ring(c, c, 75.5, 76.5, GOLD[3])
    cv.disc(c, c, 75.4, CLEAR)
    for k in range(16):
        a = k / 16 * math.tau
        cv.socket(c + math.cos(a) * 86, c + math.sin(a) * 86, 1.8)
    # The pointer.
    for k in range(12):
        w = (12 - k) * 0.55
        cv.rect(c - w, 2 + k, c + w, 2 + k, IVORY[3] if k < 8 else IVORY[2])
    cv.frame(c - 7, 1, c + 7, 1, GOLD[3])
    cv.outline(DARK)
    return cv


def table():
    W, H = 300, 92
    cv = Canvas(W, H)
    cv.bevel(0, 0, W - 1, H - 1, WOOD, 2)
    cv.grain(0, 0, W - 1, H - 1, WOOD, 5)
    cv.trim(4, 4, W - 5, 9)
    cv.bevel(8, 10, W - 9, 64, FELT, 2)
    for y in range(12, 63):
        for x in range(10, W - 10):
            if (x + y) % 5 == 0:
                cv.px(x, y, FELT[3])
    # The three pads: chalk borders on the felt.
    for (x0, x1, col) in ((16, 112, RED), (122, 178, GREEN), (188, 284, NAVY)):
        cv.frame(x0, 16, x1, 58, IVORY[3])
        cv.frame(x0 + 1, 17, x1 - 1, 57, IVORY[1])
        cv.rect(x0 + 6, 22, x1 - 6, 34, col[2] if col is not NAVY else NAVY[1])
        cv.frame(x0 + 6, 22, x1 - 6, 34, col[1] if col is not NAVY else NAVY[0])
    cv.trim(4, 64, W - 5, 69)
    # The rail where the chips sit.
    cv.bevel(8, 70, W - 9, 88, WINE, 2)
    cv.tuft(10, 72, W - 11, 86, WINE, 6)
    cv.outline(DARK)
    return cv


CHIPS = [("e8e0d0", "8a8070"), ("d03040", "6a0a14"), ("3a6ae0", "142a6a"), ("28a050", "0a4020"), ("181818", "e8c040"), ("8a3ac8", "3a0a6a")]


def chip(k):
    face, edge = (hexc(c) for c in CHIPS[k])
    cv = Canvas(14, 14)
    cv.disc(7, 7, 6.8, DARK)
    cv.disc(7, 7, 6, edge)
    for a in range(0, 360, 45):
        r = math.radians(a)
        cv.px(7 + math.cos(r) * 5, 7 + math.sin(r) * 5, (255, 255, 255, 255))
    cv.disc(7, 7, 4, face)
    cv.ring(7, 7, 3, 3.6, edge)
    cv.px(5, 5, (255, 255, 255, 200))
    return cv


def ball(r):
    size = int(2 * r + 3)
    cv = Canvas(size, size)
    cv.disc(size / 2, size / 2, r + 0.7, IVORY[0])
    cv.shade_ball(size / 2, size / 2, r, IVORY[1:])
    return cv


# ---------------------------------------------------------------- the pinball
# The geometry the game's marbles move in (Items/CasinoPinSim.cs; keep the two in step): the lane up the right (x 179..193)
# to its top at y 40, the hood over it (a quarter round about 172,40, radius 23) turning a marble over into the field; the
# funnel under it, a rail from each wall down to the mouth in the middle (its two short walls at x 87 and 99, y 44..52);
# three windmills (their own picture, Mill, turned by the game about their hubs; their axle plates here); the baffles and
# the cup baffle (drawn by the game); six rows of pins 11 px apart from y 108 (the even rows' pins on 16 + 14j out to both
# walls, the odd rows' on 23 + 14j); eleven channels 14 px wide from x 16, their dividers' caps at y 184; the launcher's
# handle under the lane; the hopper of loaded marbles along the foot.
PIN_ROWS = [108 + 11 * i for i in range(6)]
FIELD_X0, FIELD_X1, CHANNELS = 16, 170, 11
FUNNEL_L, FUNNEL_R = (15, 30, 87, 44), (176, 29, 99, 44)
MOUTH = (87, 99, 44, 52)
MILLS = [(93, 62), (70, 73), (116, 73)]
HOOD = (172, 40, 23)
CAP_Y = 184


def rail(cv, x0, y0, x1, y1):
    n = int(max(abs(x1 - x0), abs(y1 - y0)))
    for i in range(n + 1):
        x, y = x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n
        cv.px(x, y + 2, (0, 0, 0, 90))
        cv.px(x, y - 1, GOLD[5])
        cv.px(x, y, GOLD[3])
        cv.px(x, y + 1, GOLD[1])


def pin_cabinet():
    W, H = 210, 270
    cv = Canvas(W, H)
    for fx in (12, 176):
        cv.bevel(fx, 262, fx + 21, 269, GOLD, 3)
    cv.bevel(2, 10, W - 3, 263, WOOD, 2)
    cv.grain(2, 10, W - 3, 263, WOOD, 6)
    cv.frame(4, 12, W - 5, 261, GOLD[2])
    cv.frame(5, 13, W - 6, 260, GOLD[4])
    # The field: teal glass in gold.
    cv.bevel(12, 14, 174, 236, GOLD, 3)
    for y in range(17, 234):
        for x in range(15, 172):
            t = (y - 17) / 217
            col = TEAL[2] if t < 0.3 else TEAL[1] if t < 0.75 else TEAL[0]
            if (x * 7 + y * 3) % 31 == 0:
                col = TEAL[3]
            cv.px(x, y, col)
    # The hood over the lane's top: glass inside its quarter round, its rail along it.
    hx, hy, hr = HOOD
    for y in range(17, hy + 1):
        for x in range(hx - 1, hx + hr + 2):
            d = math.hypot(x + 0.5 - hx, y + 0.5 - hy)
            if d < hr - 0.5:
                cv.px(x, y, TEAL[2] if (x * 7 + y * 3) % 31 else TEAL[3])
            elif d < hr + 1.5:
                cv.px(x, y, GOLD[4] if d < hr + 0.5 else GOLD[2])
    # The funnel's rails and its mouth's two walls.
    rail(cv, *FUNNEL_L)
    rail(cv, *FUNNEL_R)
    ml, mr, mt, mf = MOUTH
    for x in (ml, mr):
        cv.rect(x - 1, mt, x, mf, GOLD[3])
        cv.px(x - 1, mt, GOLD[5])
        cv.px(x, mf, GOLD[1])
    # The windmills' axle plates (each windmill turns over its own).
    for mx, my in MILLS:
        cv.disc(mx, my, 3.2, GOLD[1])
    # The pins.    # The pins.
    for i, y in enumerate(PIN_ROWS):
        xs = [FIELD_X0 + 14 * j for j in range(0, 12)] if i % 2 == 0 else [23 + 14 * j for j in range(11)]
        for x in xs:
            cv.px(x + 1, y + 2, (0, 0, 0, 90))
            cv.rect(x - 1, y - 1, x + 1, y + 1, GOLD[3])
            cv.px(x - 1, y - 1, GOLD[5])
            cv.px(x + 1, y + 1, GOLD[1])
    # The channels: dividers with round caps, floors.
    for k in range(CHANNELS + 1):
        x = FIELD_X0 + 14 * k
        cv.rect(x - 1, CAP_Y, x, 233, GOLD[3])
        cv.px(x, CAP_Y + 1, GOLD[1])
        cv.disc(x - 0.5, CAP_Y, 2.2, GOLD[3])
        cv.px(x - 1, CAP_Y - 1, GOLD[5])
    for k in range(CHANNELS):
        x0, x1 = FIELD_X0 + 14 * k + 1, FIELD_X0 + 14 * (k + 1) - 2
        cv.rect(x0, 225, x1, 233, NAVY[1])
        cv.rect(x0, 225, x1, 225, NAVY[3])
    # The lane on the right, from its top under the hood down to the launcher (its left wall the divider: out of the hood
    # into the field, never back).
    cv.bevel(176, 38, 196, 238, GOLD, 3)
    cv.rect(179, 41, 193, 236, NAVY[0])
    for y in range(42, 236, 6):
        cv.px(186, y, NAVY[2])
    # Its top open into the hood.
    for y in range(38, 41):
        for x in range(179, 194):
            cv.px(x, y, TEAL[2])
    # The launcher's housing below the lane: a slot the handle is drawn out of.
    cv.bevel(174, 236, 198, 262, GOLD, 2)
    cv.rect(181, 239, 191, 259, NAVY[0])
    for y in range(240, 259, 3):
        cv.px(183, y, NAVY[2])
        cv.px(189, y, NAVY[2])
    # The hopper along the foot: loaded marbles wait here (drawn by the game).
    cv.bevel(10, 238, 172, 262, GOLD, 3)
    cv.rect(13, 241, 169, 259, hexc("0a0a14"))
    for x in range(14, 169, 2):
        cv.px(x, 257, NAVY[2])
    # The crest.
    cv.ellipse(93, 7, 14, 7, IVORY[2])
    cv.ellipse(117, 7, 14, 7, IVORY[2])
    cv.ellipse(93, 6, 10, 4, IVORY[3])
    cv.ellipse(117, 6, 10, 4, IVORY[3])
    cv.ellipse(105, 9, 3, 9, GOLD[3])
    cv.disc(105, 5, 2.6, GREEN[3])
    cv.socket(30, 11, 3)
    cv.socket(150, 11, 3)
    cv.outline(DARK)
    return cv


MARBLES = [("b87333", "6a3a14", "f0b080"), ("c0c8d0", "6a7078", "ffffff"), ("e8c040", "8a6010", "fff4b0"),
           ("d03040", "6a0a14", "ff9aa0"), ("3a6ae0", "142a6a", "a8c8ff"), ("30c070", "0a5028", "b0ffd0")]
RAINBOW = ["e83a3a", "f0902a", "f0e040", "40c860", "3a90f0", "9a50e0"]


def mill():
    """The windmill under the gate, 22 px with its hub at 11,11 (the game turns it): four ivory blades on a gold hub."""
    cv = Canvas(22, 22)
    for k in range(4):
        a = k * math.pi / 2
        for t in range(2, 10):
            x, y = 11 + math.cos(a) * t, 11 + math.sin(a) * t
            nx, ny = -math.sin(a), math.cos(a)
            cv.px(x - 0.5, y - 0.5, IVORY[3])
            cv.px(x - 0.5 + nx, y - 0.5 + ny, IVORY[1] if t < 9 else GOLD[2])
        cv.disc(11 + math.cos(a) * 9, 11 + math.sin(a) * 9, 1.3, RED[3])
    cv.disc(11, 11, 2.6, GOLD[3])
    cv.disc(10.6, 10.6, 1.1, GOLD[5])
    cv.outline(DARK)
    return cv


def marble_small(k):
    """A marble in the field, 8 px (radius 3.5 in the game's physics); 6 is the all-in marble, in rainbow bands."""
    cv = Canvas(8, 8)
    cv.disc(4, 4, 3.9, DARK)
    if k < 6:
        mid, dark, light = (hexc(c) for c in MARBLES[k])
        cv.disc(4, 4, 3.2, dark)
        cv.disc(3.7, 3.7, 2.5, mid)
        cv.px(3, 2, light)
        cv.px(2, 3, light)
        cv.px(2, 2, (255, 255, 255, 255))
    else:
        for y in range(8):
            for x in range(8):
                if math.hypot(x + 0.5 - 4, y + 0.5 - 4) < 3.2:
                    cv.px(x, y, hexc(RAINBOW[min(5, max(0, (x + y - 1) // 2))]))
        cv.px(2, 2, (255, 255, 255, 255))
    return cv


def marble_rainbow():
    """The all-in marble in the tray and hopper (10 px, like the others)."""
    cv = Canvas(10, 10)
    cv.disc(5, 5, 4.6, DARK)
    for y in range(10):
        for x in range(10):
            if math.hypot(x + 0.5 - 5, y + 0.5 - 5) < 3.9:
                cv.px(x, y, hexc(RAINBOW[min(5, max(0, (x + y - 2) * 6 // 14))]))
    cv.disc(3.8, 3.6, 1.2, (255, 255, 255, 200))
    cv.px(3, 3, (255, 255, 255, 255))
    return cv


def baffle():
    cv = Canvas(22, 5)
    cv.bevel(0, 0, 21, 4, GOLD, 3)
    cv.rect(1, 1, 20, 1, GOLD[5])
    return cv


def cup(rainbow):
    """The cup baffle, 32x8 (drawn at the cup's x - 16, its bar's middle on row 3): a gold bar each side, a cup between them
    (x 10..21) a marble drops into; the cup gold (three times) or in rainbow bands (two more marbles)."""
    cv = Canvas(32, 8)
    for x0, x1 in ((0, 9), (22, 31)):
        cv.bevel(x0, 1, x1, 5, GOLD, 3)
        cv.rect(x0 + 1, 2, x1 - 1, 2, GOLD[5])
    for y in range(2, 8):
        half = 6 - max(0, y - 4) * 1.2
        for x in range(10, 22):
            dx = abs(x + 0.5 - 16)
            if dx > half:
                continue
            if rainbow:
                col = hexc(RAINBOW[min(5, max(0, int((x - 10) / 2)))])
            else:
                col = GOLD[4] if dx > half - 1.5 or y >= 6 else GOLD[2]
            cv.px(x, y, col if dx > half - 1.5 or y >= 6 else (col[0] // 3, col[1] // 3, col[2] // 3, 255))
    cv.outline(DARK)
    return cv


def plunger():
    knob = Canvas(16, 10)
    knob.bevel(0, 3, 15, 9, RED, 2)
    knob.rect(1, 4, 14, 4, RED[4])
    knob.rect(6, 0, 9, 3, GREY[4])
    knob.save("PlungerKnob")
    spring = Canvas(10, 24)
    for y in range(24):
        x = int(round(4.5 + 4 * math.sin(y * math.pi / 3)))
        spring.px(x, y, GOLD[4])
        spring.px(x + (1 if x < 5 else -1), y, GOLD[2])
    spring.save("Spring")
    rod = Canvas(4, 30)
    rod.rect(0, 0, 3, 29, GREY[4])
    rod.rect(1, 0, 1, 29, GREY[5])
    rod.save("PlungerRod")
    tip = Canvas(8, 4)
    tip.bevel(0, 0, 7, 3, GREY, 3)
    tip.save("PlungerTip")


def bowl():
    """A dish of marbles of one value (its marbles drawn by the game)."""
    cv = Canvas(40, 22)
    cv.ellipse(20, 12, 19, 9, GOLD[2])
    cv.ellipse(20, 11, 18, 8, GOLD[4])
    cv.ellipse(20, 12, 16, 6.5, NAVY[0])
    cv.ellipse(20, 13, 15, 5, NAVY[1])
    cv.outline(DARK)
    return cv


def lcd():
    cv = Canvas(12, 12)
    cv.rect(0, 0, 11, 11, GOLD[1])
    cv.rect(1, 1, 10, 10, GOLD[3])
    cv.rect(2, 2, 9, 9, hexc("04140e"))
    cv.rect(3, 3, 8, 3, hexc("0c2a1e"))
    return cv


def glow_lamp():
    """A lit lamp in pixels (white, tinted by the game): a bright middle, two rings round it."""
    cv = Canvas(11, 11)
    for y in range(11):
        for x in range(11):
            d = math.hypot(x - 5, y - 5)
            a = 255 if d < 1.5 else 200 if d < 2.6 else 110 if d < 3.8 else 40 if d < 5.2 else 0
            if a:
                cv.px(x, y, (255, 255, 255, a))
    return cv


background()
slot_cabinet().save("SlotCab")
lever()
for i, order in enumerate(STRIPS):
    s = reel_big(order)
    s.save("ReelBig%d" % i)
    smear(s).save("ReelBigBlur%d" % i)
window_shade().save("WindowShade")
for k in range(5):
    symbol_tile(k, 16).save if False else None
    t = Canvas(16, 16)
    tile = symbol_tile(k, 16)
    t.img.alpha_composite(tile, ((16 - tile.width) // 2, (16 - tile.height) // 2))
    t.save("Sym%d" % k)
push("PushRed", RED)
push("PushGold", [GOLD[0], GOLD[1], GOLD[2], GOLD[3], GOLD[4]])
push("PushBlue", NAVY)
push("PushGrey", GREY)
lcd().save("Lcd")
glow_lamp().save("LampLit")
wheel_big().save("WheelBig")
wheel_bowl().save("WheelBowl")
table().save("Table")
for k in range(6):
    chip(k).save("Chip%d" % k)
ball(2.6).save("BallBig")
pin_cabinet().save("PinCab")
mill().save("Mill")
for k in range(7):
    marble_small(k).save("MarbleS%d" % k)
marble_rainbow().save("Marble6")
baffle().save("Baffle")
cup(False).save("CupGold")
cup(True).save("CupRainbow")
plunger()
bowl().save("Bowl")
