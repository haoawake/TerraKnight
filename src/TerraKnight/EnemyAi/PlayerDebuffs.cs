using HarmonyLib;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.EnemyAi
{
    /// <summary>
    /// The Terraria debuffs the new AI's attacks put on the player, with Terraria's own effects (Player.UpdateBuffs,
    /// UpdateLifeRegen, the CCed controls): On Fire! 24 (lifeRegen -8), Bleeding 30 (no regeneration), Confused 31 (left
    /// and right swapped), Slow 32 (move speed ×0.5), Silenced 35 (no mana items), Broken Armor 36 (defense halved),
    /// Darkness 22 (darker), Chilled 46 (×0.75), Venom 70 (-30), Electrified 144 (-8, -40 while moving), Webbed 149 (no
    /// control: no running, jumping or flying; he drops). The gear that keeps a debuff off keeps these off too
    /// (PlayerBuffs.Add's immunities). Poisoned 20 is Player.Poison's.
    /// </summary>
    [HarmonyPatch]
    internal static class PlayerDebuffs
    {
        public const int Darkness = 22, OnFire = 24, Bleeding = 30, Confused = 31, Slow = 32, Silenced = 35, BrokenArmor = 36,
            Chilled = 46, Venom = 70, Electrified = 144, Webbed = 149, Poisoned = 20;

        public static int DevApplied, DevLost;
        static float carry, shown;
        static int sum;

        /// <summary>A debuff from an enemy's attack (its time made no shorter than it is).</summary>
        public static void Apply(int buff, int ticks)
        {
            if (buff <= 0 || ticks <= 0) return;
            bool had = PlayerBuffs.Has(buff);
            PlayerBuffs.Add(buff, ticks);
            if (!PlayerBuffs.Has(buff)) return;
            DevApplied++;
            if (!had && buff != Slow) Plugin.Log.LogInfo($"[敌人AI] 玩家中了减益 {buff}，{ticks / 60f:0.#} 秒");
            if (buff == Webbed && !had)
            {
                // CCed (Pl:26998): off the mount; the hooks let go too (a web holds him where he is).
                Player.GrapplingHook.Instance?.ReleaseAll();
                if (Mounts.Riding) Mounts.Dismount();
            }
        }

        static bool Has(int buff) => TerraMod.Instance != null && PlayerBuffs.Has(buff);

        /// <summary>Player.strongestMoveSpeedDebuff: the strongest of Slow and Chilled, on run speed and acceleration.</summary>
        public static float MoveSpeedMult => Has(Slow) ? 0.5f : Has(Chilled) ? 0.75f : 1f;

        /// <summary>Broken Armor halves the defense (Pl:26217).</summary>
        public static int Defense(int defense) => Has(BrokenArmor) ? defense / 2 : defense;

        /// <summary>Darkness: the open air's light dimmed.</summary>
        public static float LightFactor => Has(Darkness) ? 0.55f : 1f;

        /// <summary>Silenced: no item that costs mana.</summary>
        public static bool IsSilenced => Has(Silenced);

        /// <summary>No natural regeneration while burning, bleeding, envenomed or electrified.</summary>
        public static bool StopsRegen => Has(OnFire) || Has(Bleeding) || Has(Venom) || Has(Electrified);

        // A pull towards a point (the Gluttonous Husk's inhale, the Little Weaver's silk), or a push from one.
        static Vector2 pullAt;
        static float pullUntil, pullSpeed;

        /// <summary>Draws the player towards a point (world units) at up to <paramref name="speed"/> units a second for a while; a negative speed pushes.</summary>
        public static void Pull(Vector2 at, float speed, float seconds)
        {
            pullAt = at;
            pullSpeed = speed;
            pullUntil = Time.time + seconds;
        }

        /// <summary>Each frame in play: the life the burning, the venom and the electricity take.</summary>
        public static void Tick(HeroController hero, TerraMod mod)
        {
            if (mod == null || hero == null) return;
            var stats = mod.Stats;
            float perSecond = 0f;
            if (Has(OnFire)) perSecond += 4f;
            if (Has(Venom)) perSecond += 15f;
            if (Has(Electrified))
            {
                var rb = hero.GetComponent<Rigidbody2D>();
                perSecond += rb != null && Mathf.Abs(rb.linearVelocity.x) > 0.5f ? 20f : 4f;
            }
            if (perSecond <= 0f || stats.IsDead || PlayerData.instance == null || PlayerData.instance.isInvincible)
            {
                carry = 0f;
                return;
            }
            carry += perSecond * Time.deltaTime;
            int whole = (int)carry;
            if (whole <= 0) return;
            carry -= whole;
            stats.Life = Mathf.Max(0, stats.Life - whole);
            sum += whole;
            DevLost += whole;
            if (Time.time >= shown)
            {
                shown = Time.time + 0.5f;
                UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, sum.ToString(), UI.CombatText.PlayerHurt);
                sum = 0;
            }
            if (stats.IsDead) hero.TakeDamage(hero.gameObject, GlobalEnums.CollisionSide.bottom, 1, 1);
        }

        /// <summary>Confused: left is right (Pl:25188).</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "Move")]
        static void Swap(ref float move_direction)
        {
            if (Has(Confused)) move_direction = -move_direction;
        }

        /// <summary>After the hero's step: a web holds him (no way but down), a pull draws him.</summary>
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void Hold(HeroController __instance)
        {
            if (TerraMod.Instance == null || __instance == null || __instance.cState.dead || __instance.cState.transitioning) return;
            var rb = __instance.GetComponent<Rigidbody2D>();
            if (rb == null) return;
            if (Has(Webbed))
            {
                var v = rb.linearVelocity;
                rb.linearVelocity = new Vector2(v.x * 0.2f, Mathf.Min(0f, v.y));
                return;
            }
            if (Time.time < pullUntil && __instance.acceptingInput)
            {
                Vector2 to = pullAt - (Vector2)__instance.transform.position;
                if (to.magnitude < 0.6f) { pullUntil = 0f; return; }
                // Up to the pull's speed along the line (away from it for a push), a quarter second to reach it.
                var dir = to.normalized * Mathf.Sign(pullSpeed);
                float speed = Mathf.Abs(pullSpeed), along = Vector2.Dot(rb.linearVelocity, dir);
                if (along < speed) rb.linearVelocity += dir * Mathf.Min(speed - along, speed * 4f * Time.fixedDeltaTime);
            }
        }
    }
}
