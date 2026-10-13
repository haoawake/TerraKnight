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

        /// <summary>The slash object, so geo deposits can tell it apart from a pickaxe.</summary>
        static GameObject slash;

        /// <summary>
        /// Geo deposits react to anything dealing damage that touches them (ReceivedDamage); a
        /// sword's slash shouldn't get geo out of one, only a pickaxe aimed at it.
        /// </summary>
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ReceivedDamage), nameof(HutongGames.PlayMaker.Actions.ReceivedDamage.DoTriggerEnter2D))]
        [HarmonyLib.HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ReceivedDamage), nameof(HutongGames.PlayMaker.Actions.ReceivedDamage.DoTriggerStay2D))]
        static bool NotOnGeoRocks(HutongGames.PlayMaker.Actions.ReceivedDamage __instance, Collider2D collisionInfo) =>
            slash == null || collisionInfo == null || collisionInfo.gameObject != slash
            || __instance.Owner == null || __instance.Owner.GetComponentInParent<GeoRock>() == null;

        readonly GameObject go;
        readonly BoxCollider2D box;

        /// <summary>The slash's trigger while it is out (null otherwise).</summary>
        public Collider2D Live => go.activeInHierarchy ? box : null;
        readonly FsmFloat direction;
        readonly FsmInt damageDealt;

        /// <param name="isSwing">The weapon swing's proxy (geo rocks ignore it); an explosion's isn't.</param>
        public NailAttackProxy(Transform parent, bool isSwing = true)
        {
            go = new GameObject(isSwing ? "Terra Nail Attack" : "Terra Blast Attack") { layer = HeroAttackLayer, tag = "Nail Attack" };
            go.transform.SetParent(parent, false);
            if (isSwing) slash = go;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;

            // The variables DamageEnemies documents for the slash FSM; props mostly read "direction".
            var fsm = go.AddComponent<PlayMakerFSM>();
            fsm.FsmName = "damages_enemy";
            direction = new FsmFloat("direction");
            damageDealt = new FsmInt("damageDealt");
            fsm.FsmVariables.FloatVariables = new[] { direction, new FsmFloat("magnitudeMult") { Value = 1f } };
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
            go.SetActive(true);
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
