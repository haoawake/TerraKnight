using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Many of Hollow Knight's props (pots, levers, vines, grass, breakable floors) don't take damage
    /// through HitTaker: they watch for a trigger tagged "Nail Attack" and read the attacker's
    /// "damages_enemy" FSM for the hit direction. This is a stand-in for the nail's slash with
    /// exactly that tag, layer and FSM, switched on over the blade's arc during a swing.
    /// </summary>
    [HarmonyLib.HarmonyPatch]
    internal sealed class NailAttackProxy
    {
        const int HeroAttackLayer = 17;

        /// <summary>The weapons' slash objects (the swing's, the shortswords' stab), so geo deposits can tell them apart from a pickaxe.</summary>
        static readonly HashSet<GameObject> slashes = new HashSet<GameObject>();

        /// <summary>One of the stand-in slashes (a hit of the player's).</summary>
        public static bool IsProxy(GameObject go) => go != null && slashes.Contains(go);

        /// <summary>Every stand-in (the swings' and the blasts').</summary>
        static readonly HashSet<GameObject> all = new HashSet<GameObject>();

        /// <summary>Any stand-in, a swing's or a blast's: a Terraria hit, not the Knight's nail.</summary>
        public static bool IsAnyProxy(GameObject go) => go != null && all.Contains(go);

        /// <summary>
        /// Geo deposits react to anything dealing damage that touches them (ReceivedDamage): a weapon's slash or shot breaks
        /// one as the nail does, a stand-in slash at most once a quarter second (PropHits), as a pickaxe aimed at it does
        /// (the user's, 2026-10-20: "吉欧堆加上一个可以用武器打掉"). Counted here.
        /// </summary>
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ReceivedDamage), nameof(HutongGames.PlayMaker.Actions.ReceivedDamage.DoTriggerEnter2D))]
        [HarmonyLib.HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ReceivedDamage), nameof(HutongGames.PlayMaker.Actions.ReceivedDamage.DoTriggerStay2D))]
        static void CountTouches(Collider2D collisionInfo)
        {
            if (collisionInfo != null && all.Contains(collisionInfo.gameObject)) DevReceivedTouches++;
        }

        /// <summary>Dev: a stand-in come into a ReceivedDamage's trigger.</summary>
        public static int DevReceivedTouches;

        readonly GameObject go;
        readonly BoxCollider2D box;

        /// <summary>The slash's own object.</summary>
        public GameObject Object => go;

        /// <summary>The slash's trigger while it is out (null otherwise).</summary>
        public Collider2D Live => go.activeInHierarchy ? box : null;
        readonly FsmFloat direction;
        readonly FsmInt damageDealt;

        /// <param name="isSwing">The weapon swing's proxy (geo rocks ignore it); an explosion's isn't.</param>
        public NailAttackProxy(Transform parent, bool isSwing = true)
        {
            go = new GameObject(isSwing ? "Terra Nail Attack" : "Terra Blast Attack") { layer = HeroAttackLayer, tag = "Nail Attack" };
            go.transform.SetParent(parent, false);
            if (isSwing) slashes.Add(go);
            all.Add(go);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            // A kinematic body meets static colliders' triggers only so: a geo deposit has no body of its own (the nail's
            // slash is on the Knight's dynamic one), and its ReceivedDamage never heard a stand-in.
            rb.useFullKinematicContacts = true;
            box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;

            // The variables DamageEnemies documents for the slash FSM; props mostly read "direction".
            // A Dreamer in its dream multiplies the damage by "Multiplier" (read as 0 when missing: no hit).
            var fsm = go.AddComponent<PlayMakerFSM>();
            fsm.FsmName = "damages_enemy";
            direction = new FsmFloat("direction");
            damageDealt = new FsmInt("damageDealt");
            fsm.FsmVariables.FloatVariables = new[] { direction, new FsmFloat("magnitudeMult") { Value = 1f }, new FsmFloat("Multiplier") { Value = 1f } };
            fsm.FsmVariables.IntVariables = new[] { damageDealt, new FsmInt("attackType"), new FsmInt("Special Type") };
            fsm.FsmVariables.BoolVariables = new[] { new FsmBool("circleDirection"), new FsmBool("Ignore Invuln"), new FsmBool("moveDirection") };
            go.SetActive(false);
        }

        // The nail's slash is out for about this long; props that count hits while the damager stays
        // inside them would otherwise count a Terraria swing (a third of a second) several times.
        const float SlashTime = 0.1f;
        float until;

        /// <summary>Arms the slash. Props that watch for damage (ReceivedDamage) ignore a damager dealing 0.</summary>
        public void Begin(float facing, int damage)
        {
            direction.Value = facing > 0f ? 0f : 180f;
            damageDealt.Value = Mathf.Max(1, damage);
            until = Time.time + SlashTime;
            if (until > anyOutUntil) anyOutUntil = until;
            go.SetActive(true);
        }

        /// <summary>Until when some stand-in is out.</summary>
        static float anyOutUntil = -10f;

        /// <summary>A stand-in is out, or was a moment ago (what it touched may answer a frame or two late).</summary>
        public static bool OutNow => Time.time <= anyOutUntil + 0.15f;

        public static int DevRecoilsStopped;

        /// <summary>
        /// The Knight's recoil off what its nail hits (HeroController.RecoilLeft and the rest): the
        /// game's hard things (TinkEffect), breakable walls, clashing blades and shielded enemies
        /// answer the nail with it, the stand-ins too. A Terraria weapon never pushes the player
        /// back for what it hits, so while a stand-in is out and the Knight isn't swinging its own
        /// nail (the Nail item), no recoil. The knockback of being hurt is another road (StartRecoil).
        /// </summary>
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(HeroController), nameof(HeroController.RecoilLeft))]
        [HarmonyLib.HarmonyPatch(typeof(HeroController), nameof(HeroController.RecoilRight))]
        [HarmonyLib.HarmonyPatch(typeof(HeroController), nameof(HeroController.RecoilDown))]
        [HarmonyLib.HarmonyPatch(typeof(HeroController), nameof(HeroController.RecoilLeftLong))]
        [HarmonyLib.HarmonyPatch(typeof(HeroController), nameof(HeroController.RecoilRightLong))]
        static bool NoRecoilForTerrariaHits(HeroController __instance)
        {
            if (!OutNow || __instance == null || __instance.cState.attacking) return true;
            DevRecoilsStopped++;
            return false;
        }

        public void Tick(Vector2 center, Vector2 size)
        {
            if (Time.time > until)
            {
                if (go.activeSelf) go.SetActive(false);
                return;
            }
            go.transform.position = new Vector3(center.x, center.y, 0f);
            box.size = size;
        }

        public void End() => go.SetActive(false);
    }
}
