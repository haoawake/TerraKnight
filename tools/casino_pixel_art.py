"""Pixel art for the casino (TerraKnight Resources/CasinoPx_*.png), in Terraria's way: 1-px dark outlines, three to five
shades per material, light from the top left, nothing smooth. Machines at 48x64 for the world (3x4 tiles) and at three
times the detail (144x192) for the casino's window; the reels, the wheel, the pins, the ball, the bars, the effect sprites
and the window's frame and buttons.

usage: python -I pixel_casino.py <terraria item png dir> <out dir>
"""
import math
import os
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
DARK = hexc("08060a")
CLEAR = (0, 0, 0, 0)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), CLEAR)
        self.p = self.img.load()

    def px(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.p[x, y] = c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.p[x, y]
        return CLEAR

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
        """A block of a material: its middle shade, lit top and left, shaded bottom and right, a dark outline."""
        self.rect(x0, y0, x1, y1, ramp[fill])
        for x in range(x0 + 1, x1):
            self.px(x, y0 + 1, ramp[fill + 1])
            self.px(x, y1 - 1, ramp[fill - 1])
        for y in range(y0 + 1, y1):
            self.px(x0 + 1, y, ramp[fill + 1])
            self.px(x1 - 1, y, ramp[fill - 1])
        self.px(x0 + 1, y0 + 1, ramp[min(len(ramp) - 1, fill + 2)])
        if outline:
            self.frame(x0, y0, x1, y1, ramp[0])

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

    def outline(self, c, only_on=None):
        """A 1-px outline round everything opaque (the Terraria sprite's dark edge)."""
        src = self.img.copy().load()
        for y in range(self.h):
            for x in range(self.w):
                if src[x, y][3] != 0:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < self.w and 0 <= yy < self.h and src[xx, yy][3] > 0:
                        self.p[x, y] = c
                        break

    def shade_ball(self, cx, cy, r, ramp):
        """A round thing lit from the top left: its shades in bands."""
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                dx, dy = (x + 0.5 - cx) / r, (y + 0.5 - cy) / r
                if dx * dx + dy * dy > 1:
                    continue
                lit = 0.55 - 0.45 * (dx * 0.7 + dy * 0.7) - 0.25 * (dx * dx + dy * dy)
                k = int(max(0, min(len(ramp) - 1, round(lit * (len(ramp) - 1) + 0.6))))
                self.px(x, y, ramp[k])

    def save(self, name):
        self.img.save(os.path.join(OUT, "CasinoPx_" + name + ".png"))
        print(name, self.img.size)



def tuft(cv, x0, y0, x1, y1, ramp, step):
    """Tufted velvet: creases on a diamond lattice, a button where they cross."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            u, v = (x - x0) % step, (y - y0) % step
            if u == v or u == step - 1 - v:
                cv.px(x, y, ramp[1])
            elif (u + v) % step == step // 2 and u < step // 2:
                cv.px(x, y, ramp[3])
    for y in range(y0 + step // 2, y1, step):
        for x in range(x0 + step // 2, x1, step):
            cv.px(x, y, ramp[4])
            cv.px(x + 1, y + 1, ramp[0])


def grain(cv, x0, y0, x1, y1, ramp, gap):
    """Wood grain: wavy darker lines across, a lighter one now and then."""
    for k, yb in enumerate(range(y0 + 2, y1, gap)):
        for x in range(x0 + 1, x1):
            y = yb + int(round(math.sin(x * 0.21 + k * 1.7) * 1.2))
            if y0 < y < y1:
                cv.px(x, y, ramp[1] if k % 3 else ramp[3])


def rivets(cv, x0, x1, y, ramp, step):
    for x in range(x0, x1 + 1, step):
        cv.px(x, y, ramp[5] if len(ramp) > 5 else ramp[4])
        cv.px(x + 1, y + 1, ramp[1])


def trim(cv, x0, y0, x1, y1, s):
    """A gold band: bevelled, and at the large size a lit top row, a shadow row and rivets."""
    cv.bevel(x0, y0, x1, y1, GOLD, 3)
    if s > 1:
        cv.rect(x0 + 1, y0 + 1, x1 - 1, y0 + 1, GOLD[5])
        cv.rect(x0 + 1, y1 - 1, x1 - 1, y1 - 1, GOLD[1])
        if y1 - y0 >= 6:
            rivets(cv, x0 + 4, x1 - 4, (y0 + y1) // 2, GOLD, 9)

# ---------------------------------------------------------------- machines
# Drawn on a 48x64 design grid, at a scale s (1: the world's, 3: the window's); outlines stay one pixel, so the bigger one
# has its own finer detail.


def lamp(cv, x, y, s, size=1):
    r = max(1.0, size * s * 0.9)
    cv.disc(x, y, r + max(1, s * 0.5), LAMP[0])
    cv.shade_ball(x, y, r, LAMP[1:])
    cv.px(x - r * 0.35, y - r * 0.35, LAMP[4])


def slots(s):
    W, H = 48 * s, 64 * s
    cv = Canvas(W, H)
    S = lambda v: int(round(v * s))
    # Feet.
    for fx in (5, 36):
        cv.bevel(S(fx), S(58), S(fx + 7) - 1, H - 1, GOLD, 3)
    # Cabinet: wine velvet with gold pillars.
    cv.bevel(S(4), S(22), S(44) - 1, S(59) - 1, WINE, 2)
    if s > 1:
        tuft(cv, S(7), S(44), S(41) - 1, S(56) - 1, WINE, 9)
    else:
        for col in range(S(8), S(40), 4):
            for y in range(S(44), S(56)):
                if (y // 2) % 2 == 0:
                    cv.px(col, y, WINE[1])
    for px0 in (4, 41):
        cv.bevel(S(px0), S(22), S(px0 + 3) - 1, S(59) - 1, GOLD, 2)
        if s > 1:
            cv.rect(S(px0) + 2, S(22) + 2, S(px0) + 2, S(59) - 3, GOLD[4])
            for y in range(S(24), S(58), 6):
                cv.px(S(px0) + 4, y, GOLD[5])
    trim(cv, S(4), S(56), S(44) - 1, S(59) - 1, s)
    trim(cv, S(4), S(40), S(44) - 1, S(43) - 1, s)
    # The tray.
    cv.bevel(S(14), S(46), S(34) - 1, S(54) - 1, GOLD, 3)
    cv.rect(S(15) + 1, S(48), S(33) - 2, S(53) - 2, NAVY[0])
    cv.rect(S(15) + 1, S(52) - 1, S(33) - 2, S(53) - 2, GOLD[1])
    # Reel section: navy, a gold frame round three windows.
    cv.bevel(S(6), S(23), S(42) - 1, S(40) - 1, NAVY, 2)
    cv.bevel(S(7), S(25), S(41) - 1, S(39) - 1, GOLD, 3)
    if s > 1:
        cv.rect(S(7) + 1, S(25) + 1, S(41) - 2, S(25) + 1, GOLD[5])
        rivets(cv, S(8), S(40), S(25) + 2, GOLD, 8)
    cv.rect(S(8), S(26), S(40) - 1, S(38) - 1, NAVY[0])
    for x0 in (9, 20, 31):
        cv.rect(S(x0), S(27), S(x0 + 9) - 1, S(38) - 1, IVORY[2])
    # The middle gem.
    gx, gy = S(24), S(41.5)
    for d in range(S(2) + 1):
        cv.rect(gx - d, gy - (S(2) - d), gx + d, gy + (S(2) - d), GREEN[2])
    cv.px(gx - max(0, S(1) - 1), gy - max(0, S(1) - 1), GREEN[4])
    # Dome: navy under a gold arch, green glass in it.
    cx, cy = S(24) - 0.5 * (s > 1), S(23)
    cv.ellipse(cx, cy, S(20), S(16), GOLD[3], top_only=True)
    cv.ellipse(cx, cy, S(19), S(15), NAVY[2], top_only=True)
    cv.ellipse(cx, cy, S(15), S(11), GREEN[1], top_only=True)
    cv.ellipse(cx, cy, S(14), S(10), GREEN[2], top_only=True)
    for k, mx in enumerate((16, 24, 32)):
        for y in range(S(12), S(23)):
            cv.px(S(mx), y, GOLD[3])
    # Stained glass: a sunburst of panes from the bottom middle, alternate panes lighter, gold leading between.
    for y in range(S(12), S(23)):
        for x in range(S(9), S(39)):
            c = cv.get(x, y)
            if c != GREEN[2]:
                continue
            a = math.degrees(math.atan2(cy - y, x - cx))
            pane = int(a // 22.5)
            if s > 1 and abs((a % 22.5) - 11.25) > 10.2:
                cv.px(x, y, GOLD[2])
            elif pane % 2 == 0:
                cv.px(x, y, GREEN[3])
            if s > 1 and (x * 7 + y * 3) % 23 == 0:
                cv.px(x, y, GREEN[4])
    cv.rect(S(4), S(22), S(44) - 1, S(23), GOLD[2])
    # Crescent and its lamp.
    mx, my = S(24) - 0.5 * (s > 1), S(6)
    cv.disc(mx, my, S(5.5), GOLD[3])
    cv.disc(mx, my - S(2), S(4.5), CLEAR)
    cv.disc(mx, my + S(0.5), S(5.5), GOLD[3]) if False else None
    for y in range(int(my - S(6)), int(my + S(6))):
        for x in range(int(mx - S(6)), int(mx + S(6))):
            if cv.get(x, y) == GOLD[3] and cv.get(x - 1, y - 1)[3] == 0:
                cv.px(x, y, GOLD[5])
    lamp(cv, mx, S(8.5), s, 1.6)
    # Lamps along the arch.
    for ang in (160, 132, 48, 20):
        a = math.radians(ang)
        lamp(cv, cx + math.cos(a) * S(17.6), cy - math.sin(a) * S(14), s, 1.3 if s == 1 else 1.5)
    if s > 1:
        cv.ring(cx, cy, S(19) - 1, S(19), GOLD[5]) if False else None
    # The lever.
    for y in range(S(17), S(35)):
        cv.rect(S(45), y, S(46) - 1 + (1 if s > 1 else 0), y, GOLD[3] if y % 3 else GOLD[4])
    cv.bevel(S(44), S(33), S(47) - 1, S(37) - 1, GOLD, 2)
    cv.disc(S(45.5), S(15.5), S(2.3) + 0.5, RED[0])
    cv.shade_ball(S(45.5), S(15.5), S(2.3), RED[1:])
    cv.outline(DARK)
    return cv


def roulette(s):
    W, H = 48 * s, 64 * s
    cv = Canvas(W, H)
    S = lambda v: int(round(v * s))
    # The wheel's centre on a pixel's middle (24.5, 22.5 at the world's size; three times that at the window's).
    cx, cy = 24.5 * s, 22.5 * s
    # Feet and pedestal.
    for fx in (9, 33):
        cv.bevel(S(fx), S(58), S(fx + 6) - 1, H - 1, GOLD, 3)
    cv.bevel(S(8), S(42), S(40) - 1, S(59) - 1, WOOD, 2)
    if s > 1:
        grain(cv, S(8), S(42), S(40) - 1, S(59) - 1, WOOD, 5)
    cv.bevel(S(10), S(45), S(38) - 1, S(56) - 1, WINE, 2)
    if s > 1:
        tuft(cv, S(10) + 2, S(45) + 2, S(38) - 3, S(56) - 3, WINE, 8)
    trim(cv, S(8), S(42), S(40) - 1, S(45) - 1, s)
    trim(cv, S(8), S(56), S(40) - 1, S(59) - 1, s)
    cv.bevel(S(17), S(48), S(31) - 1, S(54) - 1, GOLD, 3)
    cv.rect(S(18) + 1, S(49) + 1, S(30) - 2, S(53) - 2, NAVY[0])
    # The neck: a gold shell.
    cv.ellipse(cx, S(43), S(7), S(4), GOLD[3], top_only=True)
    cv.ellipse(cx, S(43), S(5), S(3), GOLD[4], top_only=True)
    # Scrolls each side of the wheel.
    for side in (-1, 1):
        for k in range(S(10)):
            x = cx + side * (S(20) + math.sin(k / max(1, S(10)) * math.pi) * S(3))
            cv.px(x, cy - S(5) + k, GOLD[3])
            cv.px(x + side, cy - S(5) + k, GOLD[2])
    # The ring: gold, green inlay, gold; the dark bowl the wheel turns in.
    cv.disc(cx, cy, S(21.5), GOLD[2])
    cv.ring(cx, cy, S(19.5), S(21), GOLD[3])
    cv.ring(cx, cy, S(18.5), S(20), GREEN[1])
    for k in range(24):
        a = k / 24 * math.tau
        cv.px(cx + math.cos(a) * S(19.3), cy + math.sin(a) * S(19.3), GREEN[3])
    cv.disc(cx, cy, S(18), GOLD[4])
    cv.disc(cx, cy, S(17.2), NAVY[0])
    # Studs on the ring.
    for k in range(8):
        a = k / 8 * math.tau + math.pi / 8
        lamp(cv, cx + math.cos(a) * S(20.3), cy + math.sin(a) * S(20.3), s, 0.55)
    # The pointer.
    for k in range(S(5)):
        w = (S(5) - k) * 0.6
        cv.rect(cx - w, S(0.5) + k, cx + w, S(0.5) + k, IVORY[3] if k < S(3) else IVORY[2])
    cv.outline(DARK)
    return cv


def pinball(s):
    W, H = 48 * s, 64 * s
    cv = Canvas(W, H)
    S = lambda v: int(round(v * s))
    # Legs.
    for fx, d in ((4, 1), (38, -1)):
        cv.bevel(S(fx), S(57), S(fx + 6) - 1, H - 1, GOLD, 3)
    # Cabinet: dark wood framed in gold.
    cv.bevel(S(3), S(8), S(45) - 1, S(58) - 1, WOOD, 2)
    if s > 1:
        grain(cv, S(3), S(8), S(45) - 1, S(58) - 1, WOOD, 6)
    cv.frame(S(4), S(9), S(44) - 2, S(57) - 2, GOLD[2])
    if s > 1:
        cv.frame(S(4) + 1, S(9) + 1, S(44) - 3, S(57) - 3, GOLD[4])
    # The glass: rounded, teal, a sheen across it.
    gx0, gy0, gx1, gy1 = S(7), S(12), S(37) - 1, S(45) - 1
    cv.bevel(gx0 - 2 if s > 1 else gx0 - 1, gy0 - (2 if s > 1 else 1), gx1 + (2 if s > 1 else 1), gy1 + (2 if s > 1 else 1), GOLD, 3)
    cv.rect(gx0, gy0, gx1, gy1, TEAL[1])
    for y in range(gy0, gy1 + 1):
        for x in range(gx0, gx1 + 1):
            t = (y - gy0) / max(1, gy1 - gy0)
            c = TEAL[2] if t < 0.35 else TEAL[1] if t < 0.8 else TEAL[0]
            cv.px(x, y, c)
            band = (x - gx0) + (y - gy0) * 0.9
            if S(3) <= band < S(3) + max(1, s) and y < gy0 + S(12):
                cv.px(x, y, TEAL[3])
            if S(5.5) <= band < S(5.5) + max(1, s // 2) and y < gy0 + S(9):
                cv.px(x, y, TEAL[3])
    for (x, y) in ((gx0, gy0), (gx1, gy0), (gx0, gy1), (gx1, gy1)):
        cv.px(x, y, GOLD[3])
    # Lamp column on the right.
    cv.bevel(S(39), S(13), S(43) - 1, S(46) - 1, NAVY, 1)
    for k in range(5):
        lamp(cv, S(41) - 0.5 * (s > 1), S(16 + k * 6.5), s, 0.9 if s == 1 else 1.1)
    # The ball in the launcher, the tray.
    cv.disc(S(41), S(50.5), S(2.2) + 0.5, IVORY[0])
    cv.shade_ball(S(41), S(50.5), S(2.2), IVORY[1:])
    cv.bevel(S(10), S(48), S(34) - 1, S(55) - 1, GOLD, 3)
    cv.rect(S(11) + 1, S(49) + 1, S(33) - 2, S(54) - 2, NAVY[0])
    # The crest: a moth of ivory wings, a green gem.
    mx = S(24) - 0.5 * (s > 1)
    cv.ellipse(mx - S(5), S(5), S(6), S(3.5), IVORY[2])
    cv.ellipse(mx + S(5), S(5), S(6), S(3.5), IVORY[2])
    cv.ellipse(mx - S(5), S(4.5), S(4), S(2), IVORY[3])
    cv.ellipse(mx + S(5), S(4.5), S(4), S(2), IVORY[3])
    cv.ellipse(mx, S(6), S(1.6), S(4), GOLD[3])
    cv.disc(mx, S(4), S(1.4), GREEN[3])
    # Corner lamps.
    for lx in (5, 43):
        lamp(cv, S(lx), S(10), s, 1.2)
    cv.outline(DARK)
    return cv


# ---------------------------------------------------------------- the reels
SYMBOLS = ["Item_8", "Item_181", "Item_75", "Item_327", "Item_4956"]


def load_symbol(name):
    im = Image.open(os.path.join(SRC, name + ".png")).convert("RGBA")
    if name == "Item_75":
        im = im.crop((0, 0, im.width, im.height // 8 - 2))
    return im.crop(im.getbbox())


def pixel_fit(im, size):
    """A pixel picture brought within size×size without blurring: whole steps up, or down by keeping, for each new
    pixel, the colour most of its old pixels had (transparent unless half of them were opaque)."""
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


MINI = {
    # 7x7 picture for the world's tiny reels: . clear, letters from the palette below.
    "Item_8": ["...f...", "..fFf..", "..fFf..", "...b...", "...b...", "...b...", "...b..."],
    "Item_181": ["...p...", "..pPp..", ".pPPPp.", "pPPPPPp", ".pPPPp.", "..pPp..", "...p..."],
    "Item_75": ["...y...", "...y...", "yyyYyyy", ".yYYYy.", "..yYy..", ".yy.yy.", "y.....y"],
    "Item_327": ["..gg...", ".g..g..", "..gg...", "...g...", "...gg..", "...g...", "...gg.."],
    "Item_4956": ["......c", ".....cC", "....cC.", "...cC..", "g.cC...", ".gC....", "g.g...."],
}
MINI_COL = {"f": hexc("f08020"), "F": hexc("fff080"), "b": hexc("7a4a20"), "p": hexc("8a3ac8"), "P": hexc("d8a0ff"),
            "y": hexc("e0b020"), "Y": hexc("fff4a0"), "g": hexc("c89020"), "c": hexc("40c8c0"), "C": hexc("e0fff8")}


def reel_strip(cell_w, cell_h, symbol_px, mini=False):
    n = len(SYMBOLS)
    cv = Canvas(cell_w, cell_h * n)
    for y in range(cell_h * n):
        k = (y % cell_h) / cell_h
        shade = IVORY[3] if 0.2 < k < 0.8 else IVORY[2]
        cv.rect(0, y, cell_w - 1, y, shade)
    for i, name in enumerate(SYMBOLS):
        top = i * cell_h
        cv.rect(0, top, cell_w - 1, top, IVORY[1])
        if mini:
            rows = MINI[name]
            ox, oy = (cell_w - 7) // 2, top + (cell_h - 7) // 2
            for y, row in enumerate(rows):
                for x, ch in enumerate(row):
                    if ch != ".":
                        cv.px(ox + x, oy + y, MINI_COL[ch])
        else:
            sym = pixel_fit(load_symbol(name), symbol_px)
            ox, oy = (cell_w - sym.width) // 2, top + (cell_h - sym.height) // 2
            shadow = Image.new("RGBA", sym.size, (0, 0, 0, 0))
            a = sym.split()[3].point(lambda v: 70 if v > 0 else 0)
            shadow.putalpha(a)
            cv.img.alpha_composite(shadow, (ox + 1, oy + 1))
            cv.img.alpha_composite(sym, (ox, oy))
    return cv


def reel_smear(strip):
    """The strip at speed: each column's pixels dragged down into streaks (the pixel way of a blur)."""
    im = strip.img
    w, h = im.size
    src = im.load()
    out = Image.new("RGBA", (w, h), CLEAR)
    o = out.load()
    for x in range(w):
        for y in range(h):
            acc = [0, 0, 0]
            for k in range(6):
                c = src[x, (y - k) % h]
                for i in range(3):
                    acc[i] += c[i]
            o[x, y] = (acc[0] // 6, acc[1] // 6, acc[2] // 6, 255)
    # Back to a few shades, so it stays pixel art.
    q = out.convert("RGB").quantize(colors=10, method=Image.MEDIANCUT).convert("RGBA")
    return q


def reel_shade(w, h):
    """Over the drum: two dark bands at the top and bottom, one light row a quarter down (in steps, not a gradient)."""
    cv = Canvas(w, h)
    for y in range(h):
        t = (y + 0.5) / h
        if t < 0.12 or t > 0.88:
            a = 150
        elif t < 0.22 or t > 0.78:
            a = 80
        elif t < 0.30 or t > 0.70:
            a = 30
        else:
            a = 0
        for x in range(w):
            if a:
                cv.px(x, y, (0, 0, 0, a))
    yy = int(h * 0.27)
    for x in range(1, w - 1):
        cv.px(x, yy, (255, 255, 255, 70))
    return cv


# ---------------------------------------------------------------- the wheel, ball, pins, bars
SECTORS = 65


def wheel(r):
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
            if d > r - max(1, r * 0.06):
                col = GOLD[3]
            elif d > r * 0.70:
                col = GREEN[2] if sector == 0 else RED[2] if sector % 2 == 1 else NAVY[0]
                if d > r * 0.90 and sector != 0:
                    col = RED[3] if sector % 2 == 1 else NAVY[1]
            elif d > r * 0.64:
                col = GOLD[3]
            elif d > r * 0.24:
                col = WOOD[2] if (int(ang // 45) % 2 == 0) else WOOD[3]
            elif d > r * 0.12:
                col = GOLD[3]
            else:
                col = GOLD[5]
            cv.px(x, y, col)
    # Gold dividers between sectors, where there is room for them.
    if r >= 24:
        for s_ in range(SECTORS):
            a = math.radians(s_ * step - step / 2)
            for t in range(int(r * 0.70), int(r * 0.95)):
                cv.px(c + math.sin(a) * t - 0.5, c - math.cos(a) * t - 0.5, GOLD[2])
    # Spokes.
    for k in range(4):
        a = math.radians(45 + 90 * k)
        for t in range(int(r * 0.13), int(r * 0.6)):
            cv.px(c + math.cos(a) * t - 0.5, c + math.sin(a) * t - 0.5, GOLD[4])
    return cv


def ball(r):
    size = int(2 * r + 3)
    cv = Canvas(size, size)
    cv.disc(size / 2, size / 2, r + 0.6, IVORY[0])
    cv.shade_ball(size / 2, size / 2, r, IVORY[1:])
    return cv


def pegs(w, h, rows, cols, dot):
    cv = Canvas(w, h)
    for row in range(rows):
        n = cols - (row % 2)
        y = int(h * (0.08 + row * (0.80 / (rows - 1))))
        for k in range(n):
            x = int(w * (0.07 + (k + (0.5 if row % 2 else 0)) * (0.86 / (cols - 1))))
            if dot == 1:
                cv.px(x, y, GOLD[4])
            else:
                cv.rect(x, y, x + dot - 1, y + dot - 1, GOLD[3])
                cv.px(x, y, GOLD[5])
                cv.rect(x + 1, y + dot, x + dot, y + dot, (0, 0, 0, 90))
    return cv


def bar(w, h, lit):
    cv = Canvas(w, h)
    ramp = GREEN if lit else WINE
    for y in range(h):
        t = (y + 0.5) / h
        col = ramp[4 if lit else 2] if 0.3 < t < 0.6 else ramp[3 if lit else 1]
        cv.rect(0, y, w - 1, y, col)
    cv.rect(0, 0, w - 1, 0, ramp[1 if lit else 0])
    cv.rect(0, h - 1, w - 1, h - 1, ramp[1 if lit else 0])
    return cv


# ---------------------------------------------------------------- effects
def star(n):
    """A pixel sparkle: a cross with a bright middle, its arms fading in steps."""
    cv = Canvas(n, n)
    c = n // 2
    for k in range(c + 1):
        a = [255, 230, 170, 110, 60, 30][min(5, k)]
        for (x, y) in ((c + k, c), (c - k, c), (c, c + k), (c, c - k)):
            cv.px(x, y, (255, 255, 255, a))
    if n >= 7:
        for (x, y) in ((c + 1, c + 1), (c - 1, c - 1), (c + 1, c - 1), (c - 1, c + 1)):
            cv.px(x, y, (255, 255, 255, 140))
    return cv


def geo():
    """Hallownest's geo in pixels: a rounded gold stone with its ridges."""
    rows = ["..ddd...",
            ".dYyyd..",
            "dYyGyyd.",
            "dyGyyGyd",
            "dyyGyyyd",
            ".dyyyGd.",
            "..dyyd..",
            "...dd..."]
    pal = {"d": GOLD[1], "y": GOLD[3], "Y": GOLD[5], "G": GOLD[2]}
    cv = Canvas(8, 8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                cv.px(x, y, pal[ch])
    return cv


def spark():
    cv = Canvas(2, 2)
    cv.rect(0, 0, 1, 1, (255, 255, 255, 255))
    return cv


def smoke():
    rows = ["..1221..",
            ".123321.",
            "12333321",
            "23333332",
            "12333321",
            ".123321.",
            "..1221..",
            "........"]
    cv = Canvas(8, 8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                cv.px(x, y, (255, 255, 255, {"1": 70, "2": 150, "3": 230}[ch]))
    return cv


def halo(n):
    """A lamp's glow in pixels: rings in three steps of alpha."""
    cv = Canvas(n, n)
    c = n / 2
    for y in range(n):
        for x in range(n):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c) / c
            a = 200 if d < 0.35 else 110 if d < 0.65 else 45 if d < 1 else 0
            if a:
                cv.px(x, y, (255, 255, 255, a))
    return cv


# ---------------------------------------------------------------- the window: frame and buttons
def panel():
    """A 9-slice frame, 12-px corners: a gold border with ruby corner studs round deep velvet."""
    n = 36
    cv = Canvas(n, n)
    cv.rect(0, 0, n - 1, n - 1, hexc("120a14"))
    for y in range(n):
        for x in range(n):
            if (x + y) % 4 == 0:
                cv.px(x, y, hexc("170d1a"))
    for i, col in enumerate((DARK, GOLD[1], GOLD[3], GOLD[4], GOLD[2], GOLD[0])):
        cv.frame(i, i, n - 1 - i, n - 1 - i, col)
    for (x, y) in ((1, 1), (n - 8, 1), (1, n - 8), (n - 8, n - 8)):
        cv.bevel(x, y, x + 6, y + 6, GOLD, 3)
        cv.disc(x + 3.5, y + 3.5, 1.8, RED[3])
        cv.px(x + 3, y + 3, RED[4])
    return cv


def button(kind):
    """A 9-slice button, 5-px caps: 0 normal, 1 hover, 2 pressed, 3 selected, 4 disabled."""
    w, h = 24, 14
    cv = Canvas(w, h)
    ramp = {0: NAVY, 1: NAVY, 2: NAVY, 3: GREEN, 4: [hexc(c) for c in ("101014", "24242c", "383844", "4c4c5a", "60606e")]}[kind]
    rim = GOLD if kind != 4 else [hexc(c) for c in ("202024", "3a3a40", "5a5a60", "7a7a82", "9a9aa0", "c0c0c8")]
    fill = 3 if kind == 1 else 1 if kind == 2 else 2
    cv.rect(0, 0, w - 1, h - 1, rim[0])
    cv.rect(1, 1, w - 2, h - 2, rim[2])
    cv.rect(2, 2, w - 3, h - 3, rim[3] if kind != 2 else rim[1])
    cv.rect(3, 3, w - 4, h - 4, ramp[fill])
    top = ramp[min(4, fill + 1)]
    for x in range(3, w - 3):
        cv.px(x, 3 if kind != 2 else h - 4, top)
    cv.px(1, 1, rim[4])
    if kind == 1:
        for x in range(4, w - 4, 3):
            cv.px(x, 4, ramp[4])
    for c in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        cv.px(c[0], c[1], CLEAR)
    return cv


def screen():
    """The keypad's display, 9-slice, 4-px caps: a dark green glass in a gold rim."""
    cv = Canvas(16, 16)
    cv.rect(0, 0, 15, 15, GOLD[1])
    cv.rect(1, 1, 14, 14, GOLD[3])
    cv.rect(2, 2, 13, 13, hexc("04140e"))
    for x in range(3, 13):
        cv.px(x, 3, hexc("0c2a1e"))
    return cv


MARBLES = [("b87333", "6a3a14", "f0b080"), ("c0c8d0", "6a7078", "ffffff"), ("e8c040", "8a6010", "fff4b0"),
           ("d03040", "6a0a14", "ff9aa0"), ("3a6ae0", "142a6a", "a8c8ff"), ("30c070", "0a5028", "b0ffd0")]


def marble(k):
    mid, dark, light = (hexc(c) for c in MARBLES[k])
    cv = Canvas(10, 10)
    cv.disc(5, 5, 4.6, DARK)
    cv.disc(5, 5, 3.9, dark)
    cv.disc(4.6, 4.6, 3.2, mid)
    cv.disc(3.8, 3.6, 1.4, light)
    cv.px(3, 3, (255, 255, 255, 255))
    return cv


for k in range(6):
    marble(k).save("Marble" + str(k))
for s, tag in ((1, "S"), (3, "L")):
    slots(s).save("Slots" + tag)
    roulette(s).save("Roulette" + tag)
    pinball(s).save("Pinball" + tag)
reel_strip(9, 10, 7, mini=True).save("ReelS")
big = reel_strip(27, 27, 24)
big.save("ReelL")
sm = Canvas(27, 27 * 5)
sm.img = reel_smear(big)
sm.save("ReelLBlur")
small = reel_strip(9, 10, 7, mini=True)
sms = Canvas(9, 50)
sms.img = reel_smear(small)
sms.save("ReelSBlur")
reel_shade(9, 11).save("ReelShadeS")
reel_shade(27, 33).save("ReelShadeL")
wheel(16).save("WheelS")
wheel(50).save("WheelL")
ball(1).save("BallS")
ball(3).save("BallL")
pegs(30, 30, 6, 6, 1).save("PegsS")
pegs(90, 90, 9, 8, 2).save("PegsL")
bar(30, 3, True).save("BarLitS")
bar(30, 3, False).save("BarDarkS")
bar(90, 7, True).save("BarLitL")
bar(90, 7, False).save("BarDarkL")
star(5).save("Star5")
star(9).save("Star9")
geo().save("Geo")
spark().save("Spark")
smoke().save("Smoke")
halo(9).save("Halo")
panel().save("Panel")
for k in range(5):
    button(k).save("Button" + str(k))
screen().save("Screen")
