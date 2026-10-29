using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// The Rod of Discord and the Rod of Harmony (Player.ItemCheck_UseTeleportRod): to the cursor, kept
    /// within the view and the room, the player's feet where the cursor's height less his own is
    /// (gravDir 1), nowhere his box would be in rock. Each end shimmers (TeleportEffect style 1, purple
    /// dust 164). The Rod of Discord gives Chaos State (88, 6 s); used again meanwhile it takes a seventh
    /// of the player's most life, past his defense, and may kill him. Either rod is used once per its use time (20
    /// ticks), however it is used: the item, or the 混沌传送 key (the user's 10-27 "传送杖快捷键没传送cd？": the key went
    /// every press, the Rod of Harmony's with nothing at all between).
    /// </summary>
    internal static class TeleportRods
    {
        public const int ChaosState = 88;
        const float Px = TerraParticles.Px;
        static readonly bool defined = Define();
        public static int DevTeleports, DevChaosHurts;

        /// <summary>When a rod may teleport again (unscaled time): its use time after the last teleport by either way.</summary>
        static float readyAt;
        public static bool Ready => DevNoGate || Time.unscaledTime >= readyAt;
        /// <summary>Dev (suite rodkey): no use time between teleports (as before), to compare.</summary>
        internal static bool DevNoGate;

        static bool Define()
        {
            PlayerBuffs.Define(ChaosState, "混沌状态", "使用混沌传送杖将消耗生命值", debuff: true);
            return true;
        }

        /// <summary>One use at the cursor, its use time (s) the wait before the next; whether the player went.</summary>
        public static bool Use(TerraMod mod, HeroController hero, Vector2 cursor, bool discord, float useTime = 20f / 60f)
        {
            if (!defined || hero == null || mod == null || !Ready) return false;
            var box = hero.GetComponent<BoxCollider2D>();
            if (box == null) return false;
            var b = box.bounds;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam != null)
            {
                // LimitPointToPlayerReachableArea: inside the view.
                float h = cam.orthographicSize, w = h * cam.aspect;
                Vector2 c = cam.transform.position;
                cursor.x = Mathf.Clamp(cursor.x, c.x - w + b.extents.x, c.x + w - b.extents.x);
                cursor.y = Mathf.Clamp(cursor.y, c.y - h + b.size.y, c.y + h);
            }
            // The cursor is the top of where he lands: his box hangs under it.
            var centre = new Vector2(cursor.x, cursor.y - b.extents.y);
            var region = TerrainSystem.Current?.Region;
            if (region == null) return false;
            var r = region.CellRect;
            const float cs = CellGrid.CellSize;
            if (centre.x - b.extents.x < r.xMin * cs + 1f || centre.x + b.extents.x > r.xMax * cs - 1f || centre.y - b.extents.y < r.yMin * cs + 1f) return false;
            if (Blocked(centre, b.size)) return false;
            Vector2 offset = (Vector2)hero.transform.position - (Vector2)b.center;
            Shimmer(b.center);
            hero.transform.position = centre + offset;
            DelicateFlower.OnTeleport();
            var rb = hero.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Min(rb.linearVelocity.y, 0f));
            Shimmer(centre);
            DevTeleports++;
            // A hair under the use time: the item's own next use (its use time later) is never refused.
            readyAt = Time.unscaledTime + useTime * 0.9f;
            if (!discord) return true;
            if (PlayerBuffs.Has(ChaosState))
            {
                var stats = mod.Stats;
                int damage = Mathf.Max(1, stats.LifeMax / 7);
                stats.Life = Mathf.Max(0, stats.Life - damage);
                TerraHurt.LastDamage = damage;
                UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, damage.ToString(), UI.CombatText.PlayerHurt);
                DevChaosHurts++;
                if (stats.IsDead) hero.TakeDamage(hero.gameObject, GlobalEnums.CollisionSide.bottom, 1, 1);
                else mod.PlaySound("Player_Hit_" + Random.Range(0, 3));
            }
            PlayerBuffs.Add(ChaosState, 360);
            return true;
        }

        /// <summary>Where ToPoint tries (world units off the point): the point, then rings round it, up first.</summary>
        static readonly Vector2[] near = Near();

        static Vector2[] Near()
        {
            var list = new System.Collections.Generic.List<Vector2> { Vector2.zero };
            for (int ring = 1; ring <= 6; ring++)
            {
                float d = ring * 0.25f;
                list.Add(new Vector2(0f, d));
                list.Add(new Vector2(0f, -d));
                list.Add(new Vector2(d, 0f));
                list.Add(new Vector2(-d, 0f));
                list.Add(new Vector2(d, d));
                list.Add(new Vector2(-d, d));
                list.Add(new Vector2(d, -d));
                list.Add(new Vector2(-d, -d));
            }
            return list.ToArray();
        }

        /// <summary>
        /// The player's middle to a point (world units), or to the nearest place by it his box fits, within the room (the
        /// Shade Wraith's right click, to its ghost). Whether he went; nothing shimmers (the caller draws its own).
        /// </summary>
        public static bool ToPoint(HeroController hero, Vector2 point)
        {
            var box = hero != null ? hero.GetComponent<BoxCollider2D>() : null;
            var region = TerrainSystem.Current?.Region;
            if (box == null || region == null) return false;
            var b = box.bounds;
            var r = region.CellRect;
            const float cs = CellGrid.CellSize;
            foreach (var off in near)
            {
                var centre = point + off;
                if (centre.x - b.extents.x < r.xMin * cs + 1f || centre.x + b.extents.x > r.xMax * cs - 1f || centre.y - b.extents.y < r.yMin * cs + 1f) continue;
                if (Blocked(centre, b.size)) continue;
                hero.transform.position = centre + ((Vector2)hero.transform.position - (Vector2)b.center);
                DelicateFlower.OnTeleport();
                var rb = hero.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return true;
            }
            return false;
        }

        /// <summary>Collision.SolidCollision for the player's box: the grid's rock, or the scene's own solid colliders.</summary>
        static bool Blocked(Vector2 centre, Vector2 size)
        {
            var lo = CellGrid.WorldToCell(centre - size * 0.5f);
            var hi = CellGrid.WorldToCell(centre + size * 0.5f - new Vector2(0.001f, 0.001f));
            for (int y = lo.y; y <= hi.y; y++)
                for (int x = lo.x; x <= hi.x; x++)
                    if (TerraWorld.IsSolid(x, y)) return true;
            foreach (var col in Physics2D.OverlapBoxAll(centre, size * 0.95f, 0f, 1 << ChunkColliders.TerrainLayer))
                if (!col.isTrigger && !col.usedByEffector && col.enabled) return true;
            return false;
        }

        /// <summary>TeleportEffect style 1: purple dust over the player's box.</summary>
        static void Shimmer(Vector2 centre)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var tl = TerraParticles.ToPx(centre) - new Vector2(10f, 21f);
            for (int i = 0; i < 50; i++)
            {
                int d = p.NewDust(tl, 20, 42, 164, 0f, 0f, 150, 1.1f);
                if (d >= 0) p[d].NoGravity = true;
            }
        }
    }
}
