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
    /// the cells round the cursor, what the held item would act on there, and the hero against the platforms built
    /// near him (collision, his body, the cells round his feet). The position is
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
            if (kb != null && kb.f8Key.wasPressedThisFrame && !Net.NetChat.BlocksKeys) Report(hero, mouse);
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

        /// <summary>
        /// The hero against the platforms built (for a platform that can't be stood on, placed on, or holds his head): each
        /// platform composite near him (collision with him ignored or not, switched off or suspended), his body, the stair
        /// walker's state, what overlaps his box, and the cells round his feet (p platform, s stair, b block, # ground).
        /// </summary>
        static void Platforms(HeroController hero, BoxCollider2D col, StringBuilder sb)
        {
            var b = col.bounds;
            var rb = hero.GetComponent<Rigidbody2D>();
            sb.Append("\n  平台：");
            int near = 0;
            foreach (var p in ChunkColliders.AllPlatforms)
            {
                if (p == null || Vector2.Distance(p.bounds.center, b.center) > 30f) continue;
                near++;
                bool ignored = Physics2D.GetIgnoreCollision(col, p);
                bool off = !p.enabled || !p.gameObject.activeInHierarchy;
                if (ignored || off || p.bounds.Intersects(b))
                    sb.Append($"\n    {Path(p.transform)} {p.bounds.min.x:F2},{p.bounds.min.y:F2}..{p.bounds.max.x:F2},{p.bounds.max.y:F2}：忽略主角 {ignored}，关着 {off}，形状 {p.shapeCount}，压着主角 {p.bounds.Intersects(b)}");
            }
            sb.Append($"\n    附近平台组 {near}；主角刚体 {(rb != null ? $"{rb.bodyType}/模拟 {rb.simulated}/重力 {rb.gravityScale:F2}/速度 {rb.linearVelocity.x:F1},{rb.linearVelocity.y:F1}" : "无")}，碰撞体 开 {col.enabled} 触发 {col.isTrigger} 层 {hero.gameObject.layer}，与地形层忽略 {Physics2D.GetIgnoreLayerCollision(hero.gameObject.layer, ChunkColliders.TerrainLayer)}");
            sb.Append($"\n    可操作 {GameHooks.PlayerHasControl()}，坐着 {PlayerData.instance?.atBench}，过场 {hero.cState.transitioning}，按下 {TerraMovement.DownHeld}，在绳上 {TerraMovement.OnRope}，台阶 {StairCollision.DevState} {StairCollision.DevLedge}");
            sb.Append("\n    压着主角的：");
            foreach (var c in Physics2D.OverlapBoxAll(b.center, b.size * 0.98f, 0f))
                if (c != col && !c.isTrigger) sb.Append("\n      ").Append(Describe(c));
            var feet = CellGrid.WorldToCell(new Vector2(b.center.x, b.min.y + 0.01f));
            sb.Append("\n    主角周围的格子（p 平台，s 锤过的平台，b 放的方块，# 原地形，. 空，大写是脚所在的格）：");
            for (int y = feet.y + 4; y >= feet.y - 2; y--)
            {
                sb.Append("\n      ");
                for (int x = feet.x - 4; x <= feet.x + 4; x++)
                {
                    var k = TerraWorld.Get(x, y);
                    char ch = TerraWorld.IsPlatform(x, y) ? (TerraWorld.GetSlope(x, y) == 0 ? 'p' : 's') : k == CellKind.Empty ? '.' : k == CellKind.Original ? '#' : 'b';
                    sb.Append(x == feet.x && y == feet.y ? char.ToUpper(ch == '.' ? 'o' : ch) : ch);
                }
            }
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
            Platforms(hero, col, sb);
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
            Enemies(b.center, mouse, sb);
            Plugin.Log.LogInfo(sb.ToString());
            mod?.PlaySound("Menu_Tick", 0.7f);
        }

        /// <summary>
        /// The live enemies within 15 units of the hero or the cursor, nearest the cursor first: their body's place, how
        /// much of it is in solid cells, and their body's motion (an enemy said to be stuck in a wall is found by it).
        /// </summary>
        static void Enemies(Vector2 hero, Vector2 mouse, StringBuilder sb)
        {
            var near = new System.Collections.Generic.List<(float d, string line)>();
            foreach (var info in Items.EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                var col = hm.GetComponent<Collider2D>();
                Bounds eb = col != null ? col.bounds : new Bounds(hm.transform.position, Vector3.one);
                float dm = Vector2.Distance(eb.center, mouse), dh = Vector2.Distance(eb.center, hero);
                if (Mathf.Min(dm, dh) > 15f) continue;
                var rb = hm.GetComponent<Rigidbody2D>();
                float share = EnemyUnstick.SolidShare(eb);
                near.Add((dm, $"{hm.name}（{info.Name}）身体 {eb.min.x:F2},{eb.min.y:F2}..{eb.max.x:F2},{eb.max.y:F2}，中心实心 {TerraWorld.IsSolidAt(eb.center)}，压在实心格上 {share:P0}，" +
                    $"刚体 {(rb != null ? $"{rb.bodyType} 速度 {rb.linearVelocity.x:F1},{rb.linearVelocity.y:F1}" : "无")}，离鼠标 {dm:F1}"));
            }
            near.Sort((a, c) => a.d.CompareTo(c.d));
            sb.Append($"\n  附近的敌人（{near.Count} 只，离鼠标近的在前）：");
            foreach (var (_, line) in near) sb.Append("\n    ").Append(line);
        }
    }
}
