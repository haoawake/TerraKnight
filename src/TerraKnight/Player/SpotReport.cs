using System.Text;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Player
{
    /// <summary>
    /// To find a place the player is talking about. F8 writes what is at the player's spot to the
    /// log: the scene and position, what the cursor is on (the cell and its kind, every collider
    /// under it and what it is: its components and FSMs), what touches the hero on either side,
    /// the cells round the cursor, and what the held item would act on there. The position is
    /// also logged every few seconds while the player moves. Lines start "[现场]" and "[位置]".
    /// </summary>
    internal static class SpotReport
    {
        static float next;
        static Vector2 last = new Vector2(-9999f, -9999f);
        public static int DevReports;

        public static void Tick(HeroController hero, Vector2 mouse)
        {
            if (hero == null) return;
            var kb = Keyboard.current;
            if (kb != null && kb.f8Key.wasPressedThisFrame) Report(hero, mouse);
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 3f;
            Vector2 p = hero.transform.position;
            if ((p - last).sqrMagnitude < 4f) return;
            last = p;
            Plugin.Log.LogInfo($"[位置] {GameManager.instance?.sceneName} {p.x:F1},{p.y:F1}");
        }

        static string Describe(Collider2D c)
        {
            var sb = new StringBuilder();
            sb.Append($"{Path(c.transform)}（{c.GetType().Name} 层{c.gameObject.layer}{(c.isTrigger ? " 触发器" : "")}{(c.enabled ? "" : " 未启用")} {c.bounds.min.x:F2},{c.bounds.min.y:F2}..{c.bounds.max.x:F2},{c.bounds.max.y:F2}）组件:");
            for (var t = c.transform; t != null; t = t.parent)
            {
                foreach (var comp in t.GetComponents<Component>())
                {
                    if (comp == null || comp is Transform || comp is Collider2D || comp is Renderer) continue;
                    sb.Append(' ').Append(comp is PlayMakerFSM f ? "FSM'" + f.FsmName + "'@" + f.ActiveStateName : comp.GetType().Name);
                }
                if (t != c.transform) sb.Append(" ←").Append(t.name);
                if (t.parent != null && t.parent.parent == null) break;
            }
            return sb.ToString();
        }

        static string Path(Transform t)
        {
            string p = t.name;
            for (var u = t.parent; u != null; u = u.parent) p = u.name + "/" + p;
            return p;
        }

        static void Report(HeroController hero, Vector2 mouse)
        {
            DevReports++;
            var mod = TerraMod.Instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var b = col.bounds;
            var sb = new StringBuilder();
            sb.Append($"[现场] 第 {DevReports} 次：场景 {GameManager.instance?.sceneName}，主角 {hero.transform.position.x:F2},{hero.transform.position.y:F2}（碰撞盒 {b.min.x:F2},{b.min.y:F2}..{b.max.x:F2},{b.max.y:F2}，着地 {hero.cState.onGround}），鼠标 {mouse.x:F2},{mouse.y:F2}");
            var cell = CellGrid.WorldToCell(mouse);
            var r = TerraWorld.RegionAt(cell.x, cell.y);
            sb.Append($"\n  鼠标格 ({cell.x},{cell.y})：{TerraWorld.Get(cell.x, cell.y)}，实心 {TerraWorld.IsSolid(cell.x, cell.y)}，手放碰撞体 {(r != null && r.Grid.IsArt(cell.x - r.CellOffset.x, cell.y - r.CellOffset.y))}，能挖 {TerraWorld.CanDig(cell.x, cell.y)}，场景 {r?.SceneName ?? "无"}");
            if (mod != null)
            {
                var held = mod.Inventory.Current;
                if (!held.IsEmpty)
                {
                    bool usable = mod.DevCanUse(held.Type, mouse, out var target, out bool smart);
                    sb.Append($"\n  手里 {held.Def.Name}：{(usable ? $"能用在格 ({target.x},{target.y}){(smart ? "（智能光标）" : "")}" : "这里没有能挖/放的格子")}，智能光标 {(mod.SmartCursor ? "开" : "关")}");
                }
            }
            sb.Append("\n  鼠标下的碰撞体：");
            foreach (var c in Physics2D.OverlapPointAll(mouse)) sb.Append("\n    ").Append(Describe(c));
            foreach (var (name, x) in new[] { ("左边", b.min.x - 0.08f), ("右边", b.max.x + 0.08f) })
            {
                sb.Append($"\n  贴着主角{name}的：");
                foreach (var c in Physics2D.OverlapBoxAll(new Vector2(x, b.center.y), new Vector2(0.14f, b.size.y * 0.9f), 0f))
                    if (c != col) sb.Append("\n    ").Append(Describe(c));
            }
            sb.Append("\n  鼠标周围的格子（# 原地形，a 手放碰撞体，b 放的方块，. 空，? 不在场景里，中间是鼠标格）：");
            for (int y = cell.y + 4; y >= cell.y - 4; y--)
            {
                sb.Append("\n    ");
                for (int x = cell.x - 8; x <= cell.x + 8; x++)
                {
                    var rr = TerraWorld.RegionAt(x, y);
                    var k = TerraWorld.Get(x, y);
                    char ch = rr == null ? '?' : k == CellKind.Empty ? '.' : rr.Grid.IsArt(x - rr.CellOffset.x, y - rr.CellOffset.y) ? 'a' : k == CellKind.Original ? '#' : 'b';
                    sb.Append(x == cell.x && y == cell.y ? char.ToUpper(ch == '.' ? 'o' : ch) : ch);
                }
            }
            Plugin.Log.LogInfo(sb.ToString());
            mod?.PlaySound("Menu_Tick", 0.7f);
        }
    }
}
