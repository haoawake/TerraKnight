using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Two of the game's unkillable things made killable (user 2026-10-21: "改为可击杀，然后加皮德也应该能够击杀"):
    /// <list type="bullet">
    /// <item>The White Palace's Wingmoulds ("White Palace Fly"): its Control FSM's Idle and Stationary set its health to 999
    /// each time it settles back after a hit (SetHP). The first SetHP gives it its own health instead (the monster table's,
    /// <see cref="WingmouldHp"/>), the later ones none, so the hits add up and it dies as any enemy does.</item>
    /// <item>Deepnest's Garpede ("Big Centipede"): no HealthManager at all, only its head's trigger hurting on touch and a
    /// TinkEffect sending the nail back. It gets one (the game's own hit sparks and audio copied from an enemy loaded in
    /// memory), <see cref="GarpedeHp"/> health, its head on the enemies' layer (the shots look there) and the tink taken
    /// off; struck down, its head, body and tail break up in dust and it goes until the room is entered again.</item>
    /// </list>
    /// </summary>
    [HarmonyPatch]
    internal static class KillableObstacles
    {
        /// <summary>The Wingmould's health (EnemyDropData's PalaceFly).</summary>
        public const int WingmouldHp = 10;
        /// <summary>The Garpede's: Deepnest's toughest common enemies' (the Stalking Devout's, the Hive Guardian's).</summary>
        public const int GarpedeHp = 100;

        public static int DevWingmouldsSet, DevGarpedesSet, DevGarpedesKilled;

        static bool IsWingmould(GameObject go) => go != null && go.name.StartsWith("White Palace Fly");

        static readonly HashSet<int> wingmouldsGiven = new HashSet<int>();

        /// <summary>
        /// SetHP on a Wingmould: its first gives it <see cref="WingmouldHp"/>; later ones (it settling back after a hit)
        /// leave its health as the hits made it.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SetHP), nameof(SetHP.OnEnter))]
        static bool BeforeSetHP(SetHP __instance)
        {
            var go = __instance.Fsm?.GetOwnerDefaultTarget(__instance.target);
            if (!IsWingmould(go)) return true;
            var hm = go.GetComponent<HealthManager>();
            if (hm != null && wingmouldsGiven.Add(hm.GetInstanceID()))
            {
                hm.hp = WingmouldHp;
                // The 999 it may have been seen with is no maximum to measure its life by.
                EnemyStats.Forget(hm);
                DevWingmouldsSet++;
            }
            __instance.Finish();
            return false;
        }

        // ------------------------------------------------------------------ the Garpede

        static float scanAt;

        /// <summary>Once a second in play: any Garpede loaded and not yet made killable is.</summary>
        public static void Tick()
        {
            if (Time.unscaledTime < scanAt) return;
            scanAt = Time.unscaledTime + 1f;
            foreach (var c in Object.FindObjectsByType<BigCentipede>(FindObjectsSortMode.None))
                if (c != null && c.GetComponent<HealthManager>() == null) MakeKillable(c.gameObject);
        }

        static readonly string[] copied =
        {
            "audioPlayerPrefab", "regularInvincibleAudio", "blockHitPrefab", "strikeNailPrefab", "slashImpactPrefab", "fireballHitPrefab",
            "sharpShadowImpactPrefab", "corpseSplatPrefab", "enemyDeathSwordAudio", "enemyDamageAudio", "smallGeoPrefab", "mediumGeoPrefab", "largeGeoPrefab",
        };

        /// <summary>An enemy's HealthManager in memory with the game's hit prefabs set (the Shade's, a pooled enemy's...).</summary>
        static HealthManager Template()
        {
            var strike = AccessTools.Field(typeof(HealthManager), "strikeNailPrefab");
            foreach (var hm in Resources.FindObjectsOfTypeAll<HealthManager>())
                if (hm != null && strike.GetValue(hm) is GameObject g && g != null) return hm;
            return null;
        }

        static void MakeKillable(GameObject head)
        {
            var template = Template();
            if (template == null) return;
            // Its touch hurts as before; the nail no longer bounces off it.
            var tink = head.GetComponent<TinkEffect>();
            if (tink != null) Object.Destroy(tink);
            head.layer = 11;
            var hm = head.AddComponent<HealthManager>();
            foreach (var name in copied)
            {
                var f = AccessTools.Field(typeof(HealthManager), name);
                if (f != null) f.SetValue(hm, f.GetValue(template));
            }
            hm.hp = GarpedeHp;
            hm.OnDeath += () => BreakUp(head);
            DevGarpedesSet++;
            Plugin.Log.LogInfo($"[可击杀] {head.scene.name} 的加皮德（{head.name}）可以打死了：血 {GarpedeHp}");
        }

        /// <summary>Struck down: dust off its head, each body piece and its tail, its sound stopped, the whole of it gone.</summary>
        static void BreakUp(GameObject head)
        {
            if (head == null) return;
            DevGarpedesKilled++;
            var p = TerraParticles.Instance;
            foreach (var r in head.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (p != null)
                {
                    var corner = TerraParticles.ToPx(new Vector2(b.min.x, b.max.y));
                    int w = Mathf.Max(8, Mathf.RoundToInt(b.size.x / TerraParticles.Px)), h = Mathf.Max(8, Mathf.RoundToInt(b.size.y / TerraParticles.Px));
                    for (int i = 0; i < 14; i++)
                    {
                        int d = p.NewDust(corner, w, h, 1, Random.Range(-2f, 2f), Random.Range(-3f, 0.5f), 0, Random.Range(1.1f, 1.6f));
                        if (d >= 0) { ref var dust = ref p[d]; dust.Velocity *= 1.2f; }
                    }
                }
            }
            var audio = head.GetComponent<AudioSource>();
            if (audio != null) audio.Stop();
            TerraMod.Instance?.PlaySound("NPC_Killed_1", 0.9f);
            Plugin.Log.LogInfo($"[可击杀] 打死了 {head.scene.name} 的加皮德（{head.name}）");
            head.SetActive(false);
        }

        public static void Clear() => wingmouldsGiven.Clear();
    }
}
