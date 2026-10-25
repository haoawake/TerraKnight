"""Faithful port of Terraria 1.4.5 Collision.TileCollision / SlopeCollision / WalkDownSlope /
StepDown / StepUp and the player's per-tick order (Player.Update -> SlopeDownMovement, StepDown,
StepUp, DryCollision, SlopingCollision), to check slope/platform behaviour numerically.
Tiles: 'S' solid block (tileSolid), 'P' wood platform (tileSolid + tileSolidTop, frameY 0)."""
import sys
from dataclasses import dataclass

W, H = 20, 42
GRAV = 0.4
MAXFALL = 10.0


@dataclass
class T:
    kind: str  # 'S' or 'P'
    slope: int = 0
    half: bool = False


class World:
    def __init__(self):
        self.t = {}

    def set(self, i, j, kind, slope=0, half=False):
        self.t[(i, j)] = T(kind, slope, half)

    def get(self, i, j):
        return self.t.get((i, j))

    def slope(self, i, j):
        x = self.get(i, j)
        return x.slope if x else 0


class C:  # Collision statics
    up = down = stair = stairFall = False


def solid(t):  # Main.tileSolid (platforms are tileSolid too)
    return t is not None


def solid_top(t):
    return t is not None and t.kind == 'P'


def flag_solid(t):  # tileSolid || (tileSolidTop && frameY == 0)
    return t is not None


def platform(t):
    return t is not None and t.kind == 'P'


def tile_collision(w, px, py, vx, vy, fallThrough=False, fall2=False, gravDir=1, hoik=True):
    C.up = False
    C.down = False
    rx, ry = vx, vy
    ox, oy = vx, vy
    nx, ny = px + vx, py + vy
    num = num2 = num3 = num4 = -1
    v1 = int(px / 16) - 1
    v2 = int((px + W) / 16) + 2
    v3 = int(py / 16) - 1
    v4 = int((py + H) / 16) + 2
    num6 = (v4 + 3) * 16
    for i in range(v1, v2):
        for j in range(v3, v4):
            t = w.get(i, j)
            if not flag_solid(t):
                continue
            tx, ty, th = i * 16, j * 16, 16
            if t.half:
                ty += 8
                th -= 8
            if not (nx + W > tx and nx < tx + 16 and ny + H > ty and ny < ty + th):
                continue
            f2 = f3 = False
            s = t.slope
            if s > 2:
                if s == 3 and py + abs(vx) >= ty and px >= tx:
                    f3 = True
                if s == 4 and py + abs(vx) >= ty and px + W <= tx + 16:
                    f3 = True
            elif s > 0:
                f2 = True
                if s == 1 and py + H - abs(vx) <= ty + th and px >= tx:
                    f3 = True
                if s == 2 and py + H - abs(vx) <= ty + th and px + W <= tx + 16:
                    f3 = True
            if f3:
                continue
            if py + H <= ty:
                C.down = True
                if ((not (solid_top(t) and fallThrough)) or not ((vy <= 1) or fall2)) and num6 > ty:
                    num3, num4 = i, j
                    if th < 16:
                        num4 += 1
                    if num3 != num and not f2:
                        ry = ty - (py + H) + (-0.01 if gravDir == -1 else 0)
                        num6 = ty
            elif px + W <= tx and not solid_top(t):
                if not hoik or i < 1 or (w.slope(i - 1, j) != 2 and w.slope(i - 1, j) != 4):
                    num, num2 = i, j
                    if num2 != num4:
                        rx = tx - (px + W)
                    if num3 == num:
                        ry = oy
            elif px >= tx + 16 and not solid_top(t):
                if not hoik or (w.slope(i + 1, j) != 1 and w.slope(i + 1, j) != 3):
                    num, num2 = i, j
                    if num2 != num4:
                        rx = tx + 16 - px
                    if num3 == num:
                        ry = oy
            elif py >= ty + th and not solid_top(t):
                C.up = True
                num3, num4 = i, j
                ry = ty + th - py + (0.01 if gravDir == 1 else 0)
                if num4 == num2:
                    rx = ox
    return rx, ry


def slope_collision(w, px, py, vx, vy, gravity=0.0, fall=False):
    C.stair = False
    C.stairFall = False
    bits = [False] * 8
    y = py
    y2 = py
    v2x, v2y = px, py
    v3x, v3y = vx, vy
    a1 = int(px / 16) - 1
    a2 = int((px + W) / 16) + 2
    a3 = int(py / 16) - 1
    a4 = int((py + H) / 16) + 2
    for i in range(a1, a2):
        for j in range(a3, a4):
            t = w.get(i, j)
            if not flag_solid(t):
                continue
            tx, ty, n2 = i * 16, j * 16, 16
            if t.half:
                ty += 8
                n2 -= 8
            if not (px + W > tx and px < tx + 16 and py + H > ty and py < ty + n2):
                continue
            f2 = True
            if platform(t):
                if vy < 0:
                    f2 = False
                if py + H < j * 16 or py + H - (1 + abs(vx)) > j * 16 + 16:
                    f2 = False
                if ((t.slope == 1 and vx >= 0) or (t.slope == 2 and vx <= 0)) and (py + H) / 16 - 1 == j:
                    f2 = False
            if not f2:
                continue
            f3 = fall and platform(t)
            s = t.slope
            tx, ty = i * 16, j * 16
            if not (px + W > tx and px < tx + 16 and py + H > ty and py < ty + 16):
                continue
            n4 = 0.0
            if s in (3, 4):
                n4 = px - tx if s == 3 else tx + 16 - (px + W)
                if n4 >= 0:
                    if py <= ty + 16 - n4:
                        n5 = ty + 16 - py - n4
                        if py + n5 > y2:
                            v2y = py + n5
                            y2 = v2y
                            if v3y < 0.0101:
                                v3y = 0.0101
                            bits[s] = True
                elif py > ty:
                    n6 = ty + 16
                    if v2y < n6:
                        v2y = n6
                        if v3y < 0.0101:
                            v3y = 0.0101
            if s not in (1, 2):
                continue
            n4 = px - tx if s == 1 else tx + 16 - (px + W)
            if n4 >= 0:
                if not (py + H >= ty + n4):
                    continue
                n7 = ty - (py + H) + n4
                if not (py + n7 < y):
                    continue
                if f3:
                    C.stairFall = True
                    continue
                C.stair = platform(t)
                v2y = py + n7
                y = v2y
                if v3y > 0:
                    v3y = 0
                bits[s] = True
                continue
            if platform(t) and not (py + H - 4 - abs(vx) <= ty):
                if f3:
                    C.stairFall = True
                continue
            n8 = ty - H
            if not (v2y > n8):
                continue
            if f3:
                C.stairFall = True
                continue
            C.stair = platform(t)
            v2y = n8
            if v3y > 0:
                v3y = 0
    dvx, dvy = v2x - px, v2y - py
    r5x, r5y = tile_collision(w, px, py, dvx, dvy)
    if r5y > dvy:
        n9 = dvy - r5y
        v2y = py + r5y
        if bits[1]:
            v2x = px - n9
        if bits[2]:
            v2x = px + n9
        v3x = v3y = 0
        C.up = False
    elif r5y < dvy:
        n10 = r5y - dvy
        v2y = py + r5y
        if bits[3]:
            v2x = px - n10
        if bits[4]:
            v2x = px + n10
        v3x = v3y = 0
    return v2x, v2y, v3x, v3y


def walk_down_slope(w, px, py, vx, vy, gravity):
    if vy != gravity:
        return px, py, vx, vy
    a = int(px / 16)
    b = int((px + W) / 16)
    c = int((py + H + 4) / 16)
    num = (c + 3) * 16
    n2 = n3 = -1
    n4 = 2 if vx < 0 else 1
    for i in range(a, b + 1):
        for j in range(c, c + 2):
            t = w.get(i, j)
            if t is None:
                continue
            n5 = j * 16 + (8 if t.half else 0)
            # Rectangle(i*16, j*16-17, 16, 16).Intersects(player rect (int))
            rx, ry = i * 16, j * 16 - 17
            ix, iy = int(px), int(py)
            if not (rx < ix + W and ix < rx + 16 and ry < iy + H and iy < ry + 16) or not (n5 <= num):
                continue
            if num == n5:
                if t.slope == 0:
                    continue
                if n2 != -1 and n3 != -1 and w.slope(n2, n3) != 0:
                    if t.slope == n4:
                        num, n2, n3 = n5, i, j
                else:
                    num, n2, n3 = n5, i, j
            else:
                num, n2, n3 = n5, i, j
    if n2 != -1 and n3 != -1 and w.slope(n2, n3) > 0:
        s = w.slope(n2, n3)
        tx, ty = n2 * 16, n3 * 16
        if s == 2:
            n9 = tx + 16 - (px + W)
            if py + H >= ty + n9 and vx < 0:
                vy += abs(vx)
        elif s == 1:
            n9 = px - tx
            if py + H >= ty + n9 and vx > 0:
                vy += abs(vx)
    return px, py, vx, vy


def float_intersect(r1x, r1y, r1w, r1h, r2x, r2y, r2w, r2h):
    return not (r1x > r2x + r2w or r1x + r1w < r2x or r1y > r2y + r2h or r1y + r1h < r2y)


def step_down(w, px, py, vx, vy):
    """Collision.StepDown (no water walk). Returns new py and gfx delta."""
    nx = px + vx
    ny = (int((py + H) // 16)) * 16 - H
    a = int(nx / 16)
    b = int((nx + W) / 16)
    c = int((ny + H + 4) / 16)
    n4 = H // 16 + (1 if H % 16 else 0)
    n5 = (c + n4) * 16
    for i in range(a, b + 1):
        for j in range(c, c + 2):
            t = w.get(i, j)
            if t is not None:
                n9 = j * 16 + (8 if t.half else 0)
                if float_intersect(i * 16, j * 16 - 17, 16, 16, px, py, W, H) and n9 < n5:
                    n5 = n9
    n10 = n5 - (py + H)
    if 7 < n10 < 17:
        return n5 - H
    return py


def nactive(t):
    return t is not None


def step_up(w, px, py, vx, vy, holdsMatching=False):
    """Collision.StepUp, gravDir 1."""
    d = -1 if vx < 0 else (1 if vx > 0 else 0)
    vxx = px + vx
    vyy = py
    n2 = int((vxx + W // 2 + (W // 2 + 1) * d) / 16)
    n3 = int((vyy + H - 1) / 16)
    n4 = H // 16 + (1 if H % 16 else 0)
    flag = True
    for j in range(2, n4 + 1):
        t = w.get(n2, n3 - j)
        flag = flag and (not nactive(t) or not solid(t) or solid_top(t))
    t = w.get(n2 - d, n3 - n4)
    flag2 = not nactive(t) or not solid(t) or solid_top(t)
    t = w.get(n2, n3 - 1)
    t2 = w.get(n2, n3 - (n4 + 1))
    flag3 = (not nactive(t) or not solid(t) or solid_top(t)
             or (t.slope == 1 and px + W // 2 > n2 * 16)
             or (t.slope == 2 and px + W // 2 < n2 * 16 + 16)
             or (t.half and (not nactive(t2) or not solid(t2) or solid_top(t2))))
    t = w.get(n2, n3)
    t2 = w.get(n2, n3 - 1)
    top_slope = t is not None and t.slope in (1, 2)
    flag4 = ((nactive(t) and ((not top_slope) or (t.slope == 1 and px + W // 2 < n2 * 16) or (t.slope == 2 and px + W // 2 > n2 * 16 + 16))
              and ((not top_slope) or py + H > n3 * 16)
              and ((solid(t) and not solid_top(t)) or (holdsMatching and (solid_top(t) or platform(t)) and (not solid(t2) or not nactive(t2)))))
             or (t2 is not None and t2.half))
    flag4 = flag4 and (not solid_top(t) or not solid_top(t2))
    if not (n2 * 16 < vxx + W and n2 * 16 + 16 > vxx):
        return py
    if not (flag4 and flag3 and flag and flag2):
        return py
    n5 = n3 * 16
    if w.get(n2, n3 - 1) is not None and w.get(n2, n3 - 1).half:
        n5 -= 8
    elif t is not None and t.half:
        n5 += 8
    if not (n5 < vyy + H):
        return py
    n6 = vyy + H - n5
    if n6 <= 16.1:
        return n5 - H
    return py


class Player:
    def __init__(self, w, x, y):
        self.w = w
        self.x, self.y = float(x), float(y)
        self.vx = self.vy = 0.0
        self.stairFall = False

    def tick(self, run=0, down=False, upkey=False, speed=3.0):
        w = self.w
        if run:
            self.vx = run * speed
        else:
            self.vx = 0.0
        self.vy = min(self.vy + GRAV, MAXFALL)
        # SlopeDownMovement
        self.x, self.y, self.vx, self.vy = walk_down_slope(w, self.x, self.y, self.vx, self.vy, GRAV)
        if self.vy == GRAV:
            self.y = step_down(w, self.x, self.y, self.vx, self.vy)
        if self.vy >= GRAV and not down:
            self.y = step_up(w, self.x, self.y, self.vx, self.vy, upkey)
        fallThrough = down
        # DryCollision (|v| <= 16 path)
        rx, ry = tile_collision(w, self.x, self.y, self.vx, self.vy, fallThrough, False, 1, True)
        self.vx, self.vy = rx, ry
        self.x += rx
        self.y += ry
        # SlopingCollision
        if down:
            self.stairFall = True
        nx, ny, nvx, nvy = slope_collision(w, self.x, self.y, self.vx, self.vy, GRAV, self.stairFall)
        if C.stairFall:
            self.stairFall = True
        elif not fallThrough:
            self.stairFall = False
        self.x, self.y, self.vx, self.vy = nx, ny, nvx, nvy
        if C.up:
            self.vy = 0.01


def run(w, x, y, dir_, ticks, label, down_ticks=(), log_every=0):
    p = Player(w, x, y)
    trace = []
    for k in range(ticks):
        p.tick(run=dir_, down=k in down_ticks)
        trace.append((k, round(p.x, 2), round(p.y + H, 2)))
    return p, trace


def ground(w, j, i0, i1):
    for i in range(i0, i1 + 1):
        w.set(i, j, 'S')


def scenario_column(slope, ceiling_rows, column_rows, dir_):
    """Floor top at row 30 (solid row 30); column of platforms at i=30 in rows 30-column_rows..29.
    ceiling: solid row at 30 - ceiling_rows - 1 (corridor height ceiling_rows tiles), or None."""
    w = World()
    ground(w, 30, 0, 60)
    if ceiling_rows:
        ground(w, 30 - ceiling_rows - 1, 0, 60)
    for k in range(column_rows):
        w.set(30, 29 - k, 'P', slope)
    start_x = 20 * 16 if dir_ > 0 else 40 * 16
    p, tr = run(w, start_x, 30 * 16 - H, dir_, 260, "")
    passed = p.x > 31 * 16 + 4 if dir_ > 0 else p.x + W < 30 * 16 - 4
    peak = min(t[2] for t in tr)
    # how high did the feet go (px above floor)
    return passed, 30 * 16 - peak, p.x, p.y + H, tr


def main():
    out = []
    for slope in (1, 2):
        for ceiling in (None, 3, 4, 5):
            for rows in (1, 2, 3, 6):
                if ceiling and rows > ceiling:
                    continue
                for d in (1, -1):
                    passed, lift, fx, fy, tr = scenario_column(slope, ceiling, rows, d)
                    sym = 'BS' if slope == 1 else '/'
                    out.append(f"slope={slope} ({sym}) corridor={ceiling or 'open'} rows={rows} dir={'->' if d > 0 else '<-'}: passed={passed} maxFeetLift={lift:.1f}px end x={fx:.1f} feet={fy:.1f}")
    print("\n".join(out))


if __name__ == "__main__":
    main()
