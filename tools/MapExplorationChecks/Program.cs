using System;
using TerraKnight.UI;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS " + message);
}

Check(MapExploration.Alpha(false, false) == 0, "unbought unexplored map is invisible");
Check(MapExploration.Alpha(false, true) == 77, "bought unexplored map is 70% transparent");
Check(MapExploration.Alpha(true, false) == 255, "exploration is opaque without buying a map");
Check(MapExploration.Alpha(true, true) == 255, "exploration makes bought map opaque");

const int w = 100, h = 80;
var seen = new bool[w * h];
bool Mark(int x, int y)
{
    CheckBounds(x, y);
    int i = y * w + x;
    bool first = !seen[i];
    seen[i] = true;
    return first;
}
void CheckBounds(int x, int y)
{
    if (x < 0 || y < 0 || x >= w || y >= h) throw new Exception("out of bounds");
}
bool Empty(int x, int y) { CheckBounds(x, y); return false; }
int opened = MapExploration.Reveal(w, h, 50, 40, Empty, Mark);
Check(MapExploration.Radius == 18 && MapExploration.RadiusY == 23, "exploration uses the intended 18 by 23 cell radii");
Check(opened == 1295, "entry reveals only the local upright ellipse, not the whole room");
Check(seen[63 * w + 50] && !seen[64 * w + 50], "vertical reach ends at 23 cells");
Check(!seen[40 * w + 69], "cell beyond horizontal reach stays unexplored");
Check(MapExploration.Reveal(w, h, 50, 40, Empty, Mark) == 0, "standing still never expands exploration");
Check(MapExploration.Reveal(w, h, 51, 40, Empty, Mark) > 0 && seen[40 * w + 69], "walking reveals the next section of path");
Check(MapExploration.Alpha(seen[40 * w + 69], true) == 255, "newly walked purchased cell changes from 77 to 255");

Array.Clear(seen);
bool Wall(int x, int y) { CheckBounds(x, y); return x == 53; }
MapExploration.Reveal(w, h, 50, 40, Wall, Mark);
Check(seen[40 * w + 53], "first solid wall is mapped");
Check(!seen[40 * w + 54], "wall blocks exploration of the corridor behind it");
MapExploration.Reveal(w, h, 56, 40, Wall, Mark);
Check(seen[40 * w + 54], "entering the corridor discovers it from the other side");
Check(MapExploration.Reveal(w, h, -1, 40, Empty, Mark) == 0, "transition position outside the scene reveals nothing");
Check(MapExploration.Reveal(w, h, 100, 40, Empty, Mark) == 0, "position past the far scene boundary reveals nothing");
Array.Clear(seen);
Check(MapExploration.Reveal(w, h, 0, 0, Empty, Mark) > 0, "exploration at scene corners clips safely");
Check(!seen[w * h - 1], "loading a scene cannot reveal its opposite corner");
Console.WriteLine($"{checks} checks passed.");
