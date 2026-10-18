using System;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Net
{
    // Authorize while the arena is still owned; release may arrive after a dream scene
    // unloads and ownership has changed. A repeated or differently authored release cannot pay.
    internal sealed class BossLifeRewardLedger
    {
        readonly Dictionary<string, (int Slot, string Scene, string Record)> pending = new Dictionary<string, (int, string, string)>();
        readonly HashSet<string> completed = new HashSet<string>();
        public bool Register(string token, int slot, string scene, string record, bool owns)
        {
            if (!owns || !Guid.TryParseExact(token, "N", out _) || completed.Contains(token) || pending.ContainsKey(token)) return false;
            pending[token] = (slot, scene, record);
            return true;
        }
        public bool Complete(string token, int slot, string scene, string record)
        {
            if (!pending.TryGetValue(token, out var p) || p.Slot != slot || p.Scene != scene || p.Record != record) return false;
            pending.Remove(token); completed.Add(token); return true;
        }
    }

    internal static class BossLifeRewards
    {
        const string Initialized = "boss.lifeCrystal.initialized.v1";
        const string ClaimPrefix = "boss.lifeCrystal.claimed.v1.";
        static readonly HashSet<string> received = new HashSet<string>();

        internal static bool Eligible(string record) => TerraMod.Instance != null && Bestiary.KillsOf(record) == 0
            && !TerraMod.Instance.GrantedAbilities.Contains(ClaimPrefix + record);

        // Existing victories do not become repeatable first-kill rewards after updating.
        // Uses the character's existing persisted grant set, not a shared server-wide flag.
        internal static void InitializeClaims(TerraMod mod)
        {
            if (mod == null || mod.GrantedAbilities.Contains(Initialized)) return;
            foreach (var kill in Bestiary.Kills)
                if (kill.Value > 0 && EnemyDrops.IsBoss(kill.Key)) mod.GrantedAbilities.Add(ClaimPrefix + kill.Key);
            mod.GrantedAbilities.Add(Initialized);
        }

        internal static string Begin(string record, string scene)
        {
            if (!EnemyDrops.IsBoss(record)) return null;
            if (!NetSession.Active) return "local:" + Guid.NewGuid().ToString("N");
            if (Enemies.RemoteOwned(scene)) return null;
            string token = Guid.NewGuid().ToString("N");
            Send(0, token, scene, record);
            return token;
        }

        internal static void Complete(string token, string record, string scene, Vector2 at, bool direct)
        {
            if (token == null) return;
            if (token.StartsWith("local:") || !NetSession.Active) { Grant(record, at, direct); return; }
            Send(1, token, scene, record);
        }

        static void Send(byte phase, string token, string scene, string record)
        {
            var packet = NetSession.Mine(Msg.BossLifeReward);
            packet.W.Write(phase); packet.W.Write(token); packet.W.Write(scene ?? ""); packet.W.Write(record);
            NetSession.Send(packet.Done());
        }

        internal static void Receive(int from, BinaryReader reader)
        {
            byte phase = reader.ReadByte();
            string token = reader.ReadString(), scene = reader.ReadString(), record = reader.ReadString();
            if (from != 255 || phase != 2 || !EnemyDrops.IsBoss(record) || !received.Add(token)) return;
            // A player may be elsewhere in the world. Delivery is personal and survives a full
            // inventory through EnemyDrops' existing deferred-overflow path.
            Grant(record, Vector2.zero, true);
        }

        static void Grant(string record, Vector2 at, bool direct)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            InitializeClaims(mod);
            if (!mod.GrantedAbilities.Add(ClaimPrefix + record)) return;
            EnemyDrops.Spill(ItemStack.Of(ItemType.LifeCrystal, 1), at, direct, perPlayer: true);
        }

        internal static void Reset() => received.Clear();
    }
}
