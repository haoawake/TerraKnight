using System.IO;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// Player against player, Terraria's way: only between two players who both have PvP on, and
    /// not on the same team (team "none" hits everyone with PvP on). Each game checks the others'
    /// blows against its own player, so a hit is where its victim saw it: a swing's blade box (once
    /// a swing) and their projectiles' boxes. The damage is the weapon's as its owner rolled it
    /// (±15 %, a crit doubled: Main.DamageVar), less half the victim's defense
    /// (CalculateDamagePlayersTakeInPVP), taken through the game's own hurt (knockback, immunity,
    /// death). Everyone sees the number over the victim (PlayerHurtV2, 117); a death by another
    /// player reads "{0}, killed by {1}'s {2}." (DeathSource.Player) in everyone's chat.
    /// </summary>
    internal static class PvpHits
    {
        static GameObject source;
        static int lastAttacker = -1;
        static string lastWeapon;
        static float lastHitAt = -10f;

        public static int DevHits;

        /// <summary>Whether a player's blows can hurt this one now.</summary>
        public static bool Hostile(RemotePlayer by) =>
            NetSession.Pvp && by != null && by.Pvp && (by.Team == 0 || by.Team != NetSession.Team);

        public static void Tick()
        {
            if (!NetSession.Pvp) return;
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || hero.cState.invulnerable || !GameHooks.PlayerHasControl()) return;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var me = new Rect(b.min, b.size);
            foreach (var rp in NetSession.Players.Values)
            {
                if (!Hostile(rp) || !rp.HasState || !rp.Visible) continue;
                var s = rp.Latest;
                if (!s.Melee || s.MeleeSerial == rp.MeleeHitSerial) continue;
                if (!NetScenes.ToWorld(s.Scene, s.MeleeBox.position, out var at)) continue;
                if (!new Rect(at, s.MeleeBox.size).Overlaps(me)) continue;
                rp.MeleeHitSerial = s.MeleeSerial;
                string weapon = ItemDb.Has(s.MeleeItem) ? ItemDb.Get(s.MeleeItem).Name : null;
                Hurt(rp, s.MeleeDamage, s.MeleeCrit, rp.Center, weapon);
                return;
            }
            TerraProjectiles.Instance?.NetPvpCheck(me);
        }

        /// <summary>A remote player's blow landing on this one.</summary>
        public static void Hurt(RemotePlayer by, int damage, float critChance, Vector2 from, string weapon)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || hero.cState.invulnerable || damage <= 0) return;
            bool crit = Random.value * 100f < critChance;
            int rolled = Mathf.Max(1, Mathf.RoundToInt(damage * Random.Range(0.85f, 1.15f)));
            if (crit) rolled *= 2;
            if (source == null)
            {
                source = new GameObject("Terra PvP Hit");
                Object.DontDestroyOnLoad(source);
            }
            source.transform.position = new Vector3(from.x, from.y, 0f);
            lastAttacker = by.Slot;
            lastWeapon = weapon;
            lastHitAt = Time.time;
            int before = TerraMod.Instance.Stats.Life;
            // The game's hurt (immunity, knockback, death), for Terraria damage less half the defense (TakeDirect).
            Player.PlayerHooks.HurtPlayer(hero, rolled, from.x < hero.transform.position.x ? 1 : -1, source);
            int taken = before - TerraMod.Instance.Stats.Life;
            if (taken <= 0) return;
            DevHits++;
            var p = NetSession.Mine(Msg.PlayerHurt);
            p.W.Write((byte)by.Slot);
            p.W.Write((short)taken);
            p.W.Write(crit);
            NetSession.Send(p.Done());
        }

        /// <summary>PlayerHurtV2: another player was hurt; their number over them.</summary>
        public static void ReceiveHurt(int victim, BinaryReader r)
        {
            int attacker = r.ReadByte();
            int damage = r.ReadInt16();
            bool crit = r.ReadBoolean();
            var rp = NetSession.Player(victim);
            if (rp == null || !rp.Visible) return;
            UI.CombatText.Show(new Vector3(rp.Center.x, rp.Center.y + 0.9f, 0f), damage.ToString(), crit ? UI.CombatText.EnemyCrit : UI.CombatText.PlayerHurt, crit);
            if (attacker == NetSession.MySlot) TerraMod.Instance?.PlaySound("Player_Hit_" + Random.Range(0, 3), 0.6f);
        }

        /// <summary>The DeathTextGeneric lines Terraria picks one of (Language.RandomFromCategory).</summary>
        static readonly string[] generic =
        {
            "Slain", "Eviscerated", "Murdered", "FaceTornOff", "EntrailsRippedOut", "Destroyed", "SkullCrushed", "Massacred", "Impaled", "TornInHalf",
            "Decapitated", "ArmTornOff", "InnardsBecameOutards", "Dissected", "ExtremitiesDetached", "Mangled", "Ruptured", "PileOfFlesh",
        };

        /// <summary>The local player died (TerraHurt): Lang.CreateDeathMessage in everyone's chat.</summary>
        public static void OnLocalDeath()
        {
            if (!NetSession.Active) return;
            string name = NetSession.MyName ?? "?";
            var line = NetText.Key("DeathTextGeneric." + generic[Random.Range(0, generic.Length)], name, "");
            bool pvp = lastAttacker >= 0 && Time.time - lastHitAt < 2f && NetSession.Player(lastAttacker) != null;
            if (pvp)
            {
                // DeathSource.Player: "{0}, killed by {1}'s {2}."
                line = NetText.Key("DeathSource.Player", line, NetSession.Player(lastAttacker).Name, lastWeapon ?? "");
            }
            lastAttacker = -1;
            var p = NetSession.Mine(Msg.PlayerDeath);
            p.W.Write(pvp);
            line.Write(p.W);
            NetSession.Send(p.Done());
        }
    }
}
