using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade magic weapons' things left in the world (MageThing, MageMech): each rides a projectile of kind MageThing that
    /// stands where the thing is, strikes with its box while it Hurts (each enemy once in HitEvery ticks), and ends with it.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterMageThing()
        {
            specs[ProjKind.MageThing] = new Spec
            {
                Ai = Ai.Custom, W = 16, H = 16, Time = 36000, Penetrate = -1, TileCollide = false, OwnMove = true, Look = Look.Invisible,
                LocalCooldown = 20,
                CanDamage = p => p.Thing != null && p.Thing.Hurts && !p.Thing.Done,
                HitArea = p => (p.Thing != null ? p.Thing.Pos : p.Pos, p.Thing != null ? p.Thing.Size : new Vector2(0.5f, 0.5f)),
                Update = (t, p) => StepThing(p),
                Draw = (t, fx, p, px) => { if (p.Thing != null && !p.Thing.Done) p.Thing.Draw(fx, TerraParticles.ToPx(p.Thing.Pos)); },
                OnHit = (t, p, hm) =>
                {
                    if (p.Thing == null) return;
                    p.Cooldown = Mathf.Max(1, p.Thing.HitEvery);
                    p.Thing.Hit(hm);
                },
                OnKill = (t, p) => { if (p.Thing != null && !p.Thing.Done) { p.Thing.Done = true; p.Thing.End(); } },
            };
        }

        /// <summary>A thing's tick (on the first update of each): its own step, then its move by Vel; its end when Done or old.</summary>
        static void StepThing(Proj p)
        {
            var th = p.Thing;
            if (th == null) { p.Dead = true; return; }
            if (!th.Done)
            {
                th.Age++;
                th.Tick();
                th.Pos += th.Vel;
                th.Damage = Mathf.Max(0, th.Damage);
                p.Damage = th.Damage;
                p.Knockback = th.Knockback;
                p.Crit = th.Crit;
            }
            p.Pos = th.Pos;
            p.Vel = Vector2.zero;
            p.TimeLeft = 36000;
            if (th.Done || th.Age >= th.Life)
            {
                if (!th.Done)
                {
                    th.Done = true;
                    th.End();
                }
                p.Thing = null;
                p.Dead = true;
            }
        }

        /// <summary>A thing set going: its projectile made where it is, of the weapon's class.</summary>
        public void SpawnThing(MageThing thing)
        {
            SpawnClass = thing.Def != null ? thing.Def.Class : DamageClass.Magic;
            try
            {
                var p = Spawn(ProjKind.MageThing, thing.Pos, Vector2.zero, thing.Damage, thing.Knockback, thing.Crit);
                if (p == null) return;
                p.Thing = thing;
                p.Cooldown = Mathf.Max(1, thing.HitEvery);
            }
            finally { SpawnClass = DamageClass.None; }
        }

        /// <summary>The live things of a key, oldest first.</summary>
        public List<MageThing> DevThings(string key)
        {
            var list = new List<MageThing>();
            foreach (var p in live)
                if (!p.Dead && p.Thing != null && !p.Thing.Done && p.Thing.Key == key) list.Add(p.Thing);
            return list;
        }

        /// <summary>An enemy's top middle (world): over its body's box.</summary>
        internal static Vector2 EnemyTop(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>();
            return col != null ? new Vector2(col.bounds.center.x, col.bounds.max.y) : (Vector2)hm.transform.position + Vector2.up;
        }
    }
}
