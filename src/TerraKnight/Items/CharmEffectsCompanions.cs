using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The game's own charm companions, kept with Terraria damage (护符设计记录.md):
    /// Dreamshield (38): the game's shield, and a second one opposite it; each strikes for 80% of a nail blow (its melee
    /// bonuses on it), breaking a while after as the game's does.
    /// Weaversong (39): the game's weaverlings, their bites a minion's (a third of a stage's nail blow, the minion damage
    /// and the whips' tags on them).
    /// Grimmchild (40): the game's, its fireballs a minion's too and stronger by its level (0.8 / 1.1 / 1.4 of a stage's
    /// nail blow), thrown every 0.9 s instead of 1.5, straight at the target (no spread), faster, and steered on to it.
    /// Their machines take the enemy's health straight off (SetHP: the game's raw health, a share of a Terraria enemy's
    /// life whatever the stage); with the charm bag on that is a Terraria hit instead.
    /// </summary>
    internal static partial class CharmEffects
    {
        public const int Dreamshield = 38, Weaversong = 39, Grimmchild = 40;
        const float ShieldShare = 0.8f, WeaverShare = 0.33f, GrimmRate = 0.9f, GrimmSpeedUp = 1.4f;
        static readonly float[] GrimmShare = { 0f, 0f, 0.8f, 1.1f, 1.4f, 1.4f };
        public static int DevShieldHits, DevWeaverHits, DevGrimmHits, DevSecondShields, DevGrimmTuned, DevGrimmSteered;
        public static int DevLastCompanionDamage;

        enum Mate { None, Shield, Weaver, Grimm }

        static Mate MateOf(GameObject owner)
        {
            for (var t = owner != null ? owner.transform : null; t != null; t = t.parent)
            {
                string n = t.name;
                if (n.StartsWith("Orbit Shield")) return Mate.Shield;
                if (n.StartsWith("Weaverling")) return Mate.Weaver;
                if (n.StartsWith("Grimmball")) return Mate.Grimm;
                if (n.StartsWith("Knight") || n.StartsWith("_GameManager")) return Mate.None;
            }
            return Mate.None;
        }

        /// <summary>One companion's hit on an enemy at a time (its machine sets the health of the body, its parent, its grandparent).</summary>
        static readonly Dictionary<long, float> mateStruck = new Dictionary<long, float>();

        /// <summary>A companion's machine taking an enemy's health off (SetHP): a Terraria hit instead. True: done here.</summary>
        static bool MateHit(SetHP action)
        {
            if (!Charms.Enabled || Mod == null) return false;
            var owner = action.Fsm?.GameObject;
            var mate = MateOf(owner);
            if (mate == Mate.None) return false;
            var target = action.target.GetSafe(action);
            var hm = target != null ? target.GetComponent<HealthManager>() : null;
            if (hm == null) return true;
            if (hm.isDead) return true;
            long key = ((long)owner.GetInstanceID() << 32) ^ (uint)hm.GetInstanceID();
            if (mateStruck.TryGetValue(key, out float at) && Time.time - at < 0.15f) return true;
            if (mateStruck.Count > 256) mateStruck.Clear();
            mateStruck[key] = Time.time;
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            if (col == null) return true;
            Vector2 from = owner.transform.position;
            float facing = col.bounds.center.x >= from.x ? 1f : -1f;
            if (mate == Mate.Shield)
            {
                var nail = ItemDb.Get(ItemType.KnightNail);
                nail.Damage = NailItem.DamageNow();
                int damage = Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(nail) * ShieldShare));
                ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Melee };
                if (Mod.Melee.HitWith(col, damage, 4f, Equipment.Crit(nail), facing, from)) DevShieldHits++;
                DevLastCompanionDamage = damage;
                return true;
            }
            // A minion's bite or fireball: the minion damage, Soul Eater's mode and the whips' tags on it.
            int level = PlayerData.instance != null ? Mathf.Clamp(PlayerData.instance.grimmChildLevel, 0, 5) : 0;
            float share = mate == Mate.Weaver ? WeaverShare : GrimmShare[level];
            if (share <= 0f) return true;
            int base_ = Mathf.Max(1, Mathf.RoundToInt(StageDamage(share) * (1f + Equipment.Current.MinionDamage) * MinionScale));
            var px = TerraParticles.ToPx(from);
            int dealt = SummonTags.Modify(hm, base_, out bool crit, 1f, false, px, 0);
            ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Summon };
            if (Mod.Melee.HitWith(col, dealt, mate == Mate.Grimm ? 3f : 1f, crit ? 100f : 0f, facing, from, autonomous: true))
            {
                SummonTags.OnHit(hm, base_, px, 1f);
                if (mate == Mate.Weaver) DevWeaverHits++; else DevGrimmHits++;
            }
            DevLastCompanionDamage = dealt;
            return true;
        }

        [HarmonyPatch(typeof(SetHP), nameof(SetHP.OnEnter))]
        static class CompanionHits
        {
            static bool Prefix(SetHP __instance)
            {
                if (!MateHit(__instance)) return true;
                __instance.Finish();
                return false;
            }
        }

        // ------------------------------------------------------------------ the second shield, the Grimmchild's aim

        static float mateLookAt;
        static GameObject orbitShield, grimmchild;
        static int grimmTunedFor;
        static readonly List<Rigidbody2D> grimmBalls = new List<Rigidbody2D>();

        static void TickCompanions(HeroController hero)
        {
            if (!Charms.Enabled || hero == null) return;
            // Found by their tags (no scan of the scene: nothing made a frame).
            if (Time.time >= mateLookAt)
            {
                mateLookAt = Time.time + 0.5f;
                orbitShield = On(Dreamshield) ? Tagged("Orbit Shield", orbitShield) : null;
                grimmchild = On(Grimmchild) ? Tagged("Grimmchild", grimmchild) : null;
                if (orbitShield != null) SecondShield(orbitShield);
                if (grimmchild != null && grimmchild.GetInstanceID() != grimmTunedFor) TuneGrimm(grimmchild);
                if (grimmSpeed != null && grimmSpeedTuned > 0f && grimmSpeed.Value < grimmSpeedTuned * 0.95f) grimmSpeed.Value *= GrimmSpeedUp;
            }
            if (!On(Grimmchild) || grimmControl == null) return;
            // Each fireball the Grimmchild throws (its machine keeps the last in "Flameball"), steered on to the nearest
            // enemy, a turn of at most 240° a second.
            var ball = grimmBall != null ? grimmBall.Value : null;
            if (ball != null && ball != lastBall)
            {
                lastBall = ball;
                var rb = ball.GetComponent<Rigidbody2D>();
                if (rb != null && !grimmBalls.Contains(rb)) grimmBalls.Add(rb);
            }
            for (int i = grimmBalls.Count - 1; i >= 0; i--)
            {
                var rb = grimmBalls[i];
                if (rb == null || !rb.gameObject.activeInHierarchy) { grimmBalls.RemoveAt(i); continue; }
                var v = rb.linearVelocity;
                if (v.sqrMagnitude < 1f) continue;
                var foe = NearestEnemy(rb.position, 320f);
                if (foe == null) continue;
                var want = TargetCentre(foe) - rb.position;
                float step = Mathf.Clamp(Vector2.SignedAngle(v, want), -240f * Time.deltaTime, 240f * Time.deltaTime);
                rb.linearVelocity = Quaternion.Euler(0f, 0f, step) * v;
                if (Mathf.Abs(step) > 0.01f) DevGrimmSteered++;
            }
        }

        static PlayMakerFSM grimmControl;
        static FsmFloat grimmSpeed;
        static float grimmSpeedTuned;
        static FsmGameObject grimmBall;
        static GameObject lastBall;

        static GameObject Tagged(string tag, GameObject known)
        {
            if (known != null && known.activeInHierarchy) return known;
            try { var go = GameObject.FindGameObjectWithTag(tag); return go != null && go.activeInHierarchy ? go : null; }
            catch (UnityException) { return null; }
        }

        /// <summary>The Dreamshield made two-sided: its Shield again, opposite it on the same turning frame.</summary>
        static void SecondShield(GameObject orbit)
        {
            var first = orbit.transform.Find("Shield");
            if (first == null || orbit.transform.Find("Shield 2") != null) return;
            var copy = Object.Instantiate(first.gameObject, orbit.transform);
            copy.name = "Shield 2";
            copy.transform.localPosition = -first.localPosition;
            copy.transform.localRotation = first.localRotation * Quaternion.Euler(0f, 0f, 180f);
            copy.transform.localScale = first.localScale;
            copy.SetActive(true);
            DevSecondShields++;
            Plugin.Log.LogInfo($"[护符] 梦之盾：第二面盾放在对面（{first.localPosition:F2} → {copy.transform.localPosition:F2}）");
        }

        /// <summary>A nail swing (OnNailSwing): the second shield strikes out with the first.</summary>
        static void ShieldsSlash()
        {
            if (orbitShield == null) return;
            var copy = orbitShield.transform.Find("Shield 2");
            if (copy == null) return;
            foreach (var f in copy.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Shield Hit") f.SendEvent("SLASH");
        }

        /// <summary>The Grimmchild quicker and surer: its wait 0.9 s, no spread, its fireballs faster.</summary>
        static void TuneGrimm(GameObject child)
        {
            grimmTunedFor = child.GetInstanceID();
            foreach (var f in child.GetComponents<PlayMakerFSM>())
            {
                if (f.FsmName != "Control") continue;
                grimmControl = f;
                grimmBall = f.FsmVariables.FindFsmGameObject("Flameball");
                foreach (var s in f.FsmStates)
                    foreach (var a in s.Actions)
                    {
                        if (s.Name == "Antic" && a is RandomFloat r) { r.min.Value = GrimmRate; r.max.Value = GrimmRate; }
                        if (s.Name == "Shoot" && a is FireAtTarget fire) fire.spread.Value = 0f;
                    }
                var speed = f.FsmVariables.FindFsmFloat("Flameball Speed");
                if (speed != null) speed.Value *= GrimmSpeedUp;
                grimmSpeed = speed;
                grimmSpeedTuned = speed != null ? speed.Value : 0f;
                DevGrimmTuned++;
                Plugin.Log.LogInfo($"[护符] 格林之子：攻击间隔 {GrimmRate} 秒，不散射，火球速度 {(speed != null ? speed.Value.ToString("F1") : "?")}");
            }
        }
    }
}
