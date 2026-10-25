using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>What the early bosses' kits share: a lob's flight worked out, a liquid's move, the game's own actions switched off.</summary>
    internal static class Batch1Helpers
    {
        /// <summary>A liquid lob's fall (tiles a second²): Hostiles.Shot's gravity is this times half a unit.</summary>
        public const float LobGravity = 60f;

        /// <summary>The speed (units a second) a lob leaves at to be at a point after so many seconds, under <see cref="LobGravity"/>.</summary>
        public static Vector2 Lob(Vector2 from, Vector2 to, float seconds)
        {
            float g = LobGravity * 0.5f;
            seconds = Mathf.Max(0.2f, seconds);
            var d = to - from;
            return new Vector2(d.x / seconds, (d.y + 0.5f * g * seconds * seconds) / seconds);
        }

        /// <summary>A lob's time over a distance: longer the farther, within reason.</summary>
        public static float LobTime(Vector2 from, Vector2 to) => Mathf.Clamp(0.55f + Vector2.Distance(from, to) * 0.035f, 0.6f, 1.15f);

        /// <summary>An infection lob that leaves a pool where it lands (on the room's ground or on the player's blocks: a liquid).</summary>
        public static Move Spit(BossKit kit, bool pool, float size = 1.4f)
        {
            var m = kit.NewMove(1, Look.Infection);
            m.Gravity = LobGravity;
            m.Size = size;
            m.Life = 4f;
            m.Width = 3f;
            m.Land = pool ? Land.Pool : Land.None;
            return m;
        }

        /// <summary>The game's own actions of a type in a state switched off (the kit throws instead); whether any was.</summary>
        public static bool Disable<T>(FsmState s) where T : FsmStateAction
        {
            if (s == null) return false;
            bool any = false;
            foreach (var a in s.Actions)
                if (a is T) { a.Enabled = false; any = true; }
            return any;
        }

        /// <summary>The first of a state's actions of a type, or null.</summary>
        public static T First<T>(FsmState s) where T : FsmStateAction
        {
            if (s == null) return null;
            foreach (var a in s.Actions) if (a is T t) return t;
            return null;
        }

        /// <summary>A unit vector at an angle (degrees).</summary>
        public static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        /// <summary>Where a line from a point at an angle meets the room's own ground (or a long way off).</summary>
        public static Vector2 RayEnd(Vector2 from, float degrees, float reach = 40f)
        {
            var dir = Dir(degrees);
            var hit = Hostiles.NativeRay(from, dir, reach);
            return hit.collider != null ? hit.point : from + dir * reach;
        }
    }
}
