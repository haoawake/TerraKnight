import sys
sys.path.insert(0, r"D:\ClaudeTemp\claude\e------------github-----projects---\e332c8d1-bad4-4402-a7e3-1d6b4d5e6526\scratchpad\hammer")
import sim
from sim import World, Player, H, W

def door(slope, rows, corridor, d, speed, down=False, start_phase=0):
    w = World()
    sim.ground(w, 30, 0, 80)
    if corridor: sim.ground(w, 30 - corridor - 1, 0, 80)
    for k in range(rows): w.set(40, 29 - k, 'P', slope)
    x0 = (30 * 16 + start_phase) if d > 0 else (50 * 16 + start_phase)
    p = Player(w, x0, 30 * 16 - H)
    for k in range(300): p.tick(run=d, down=down, speed=speed)
    passed = p.x > 41 * 16 + 2 if d > 0 else p.x + W < 40 * 16 - 2
    return passed

print("3-tall doorway (ceiling), 3 stacked platforms; pass? per speed/phase")
for slope in (1, 2):
    for d in (1, -1):
        res = []
        for speed in (1.0, 2.0, 3.0, 4.5, 6.0, 9.0):
            ok = [door(slope, 3, 3, d, speed, start_phase=ph) for ph in range(0, 16, 1)]
            res.append(f"v{speed}:{sum(ok)}/16")
        print(f" slope {slope} {'->' if d>0 else '<-'}: " + " ".join(res))
print("same, holding Down:")
for slope in (1, 2):
    for d in (1, -1):
        ok = [door(slope, 3, 3, d, 3.0, down=True, start_phase=ph) for ph in range(16)]
        print(f" slope {slope} {'->' if d>0 else '<-'}: {sum(ok)}/16")
print("open (no ceiling), 6 stacked platforms:")
for slope in (1, 2):
    for d in (1, -1):
        res = []
        for speed in (1.0, 3.0, 6.0, 9.0):
            ok = [door(slope, 6, None, d, speed, start_phase=ph) for ph in range(16)]
            res.append(f"v{speed}:{sum(ok)}/16")
        print(f" slope {slope} {'->' if d>0 else '<-'}: " + " ".join(res))

# Stair -> flat in vanilla: '\' stair (10+k, 25+k) k=0..4, top flat row (4..9,25), bottom flat row (15..22,30); deep ground.
def stair(d):
    w = World()
    sim.ground(w, 45, 0, 40)
    for k in range(5): w.set(10 + k, 25 + k, 'P', 1)
    for i in range(4, 10): w.set(i, 25, 'P', 0)
    for i in range(15, 23): w.set(i, 30, 'P', 0)
    if d > 0: p = Player(w, 5 * 16, 25 * 16 - H)
    else: p = Player(w, 21 * 16, 30 * 16 - H)
    tr = []
    for k in range(120):
        p.tick(run=d, speed=3.0)
        tr.append((k, p.x, p.y + H))
    return tr
for d in (1, -1):
    tr = stair(d)
    bad = [t for t in tr if t[2] > 30 * 16 + 1]
    print("stair walk", "down(->)" if d > 0 else "up(<-)", "fell below bottom row:", bool(bad), "final feet", round(tr[-1][2], 1), "x", round(tr[-1][1], 1))
    for k, x, f in tr:
        if (d < 0 and 8*16 < x < 11*16+8) or (d > 0 and 13*16 < x < 16*16): print(f"   t{k} L={x:.1f} feet={f:.1f}")
