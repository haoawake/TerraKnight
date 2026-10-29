using System.Collections.Generic;
using System.IO;
using TerraKnight.Effects;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The projectiles in a LAN game (SyncProjectile 27, KillProjectile 29). Each player's game
    /// runs its own projectiles, against its own enemies (or the scene owner's, through their
    /// puppets); twenty times a second it sends what they look like now: kind, place in the
    /// scene's frame, speed, turn, frame, fade and their AI values. The others draw those as
    /// ghosts: the projectile's own drawing, moved on along its speed between two snapshots, held
    /// by its owner's hand where the drawing asks for the holder. Ghosts hurt nobody but, with PvP
    /// on, the local player (each once a second at most, the immunity a player hit has).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static int nextNetId;

        sealed class Ghost
        {
            public Proj P;
            public string Scene;
            public float Seen;
            public int ParentId;
            public float HitAt = -10f;
        }

        readonly Dictionary<int, Dictionary<int, Ghost>> ghosts = new Dictionary<int, Dictionary<int, Ghost>>();

        /// <summary>While a ghost is drawn: its owner's centre (Terraria px), for the drawings that hang off the holder (Holder).</summary>
        static Vector2? ghostHolder;

        public int DevGhosts
        {
            get
            {
                int n = 0;
                foreach (var g in ghosts.Values) n += g.Count;
                return n;
            }
        }

        public void NetClearGhosts() => ghosts.Clear();

        public void NetDropGhosts(int slot) => ghosts.Remove(slot);

        const int FReturning = 1, FChannel = 2, FFree = 4, FSpent = 8, FStopped = 16, FOffHand = 32, FDual = 64, FMinionShot = 128;

        /// <summary>The live projectiles, as a snapshot for the others.</summary>
        public void NetSend()
        {
            string scene = NetScenes.Current;
            if (string.IsNullOrEmpty(scene) || !NetScenes.Shift(scene, out var shift)) return;
            var p = NetSession.Mine(Msg.ProjectileSnapshot);
            p.W.Write(scene);
            long countAt = p.Length;
            p.W.Write((ushort)0);
            int n = 0;
            foreach (var q in live)
            {
                if (q.Dead || q.Delay > 0 || q.Ticks == 0) continue;
                if (p.Length > 60000) break;
                if (q.NetId == 0) q.NetId = ++nextNetId;
                var w = p.W;
                w.Write(q.NetId);
                w.Write((ushort)q.Kind);
                var local = q.Pos - shift;
                w.Write(local.x);
                w.Write(local.y);
                w.Write(q.Vel.x);
                w.Write(q.Vel.y);
                w.Write(q.Rot);
                w.Write(q.TRot);
                w.Write(q.Scale);
                w.Write(q.Alpha);
                w.Write(q.Ai0);
                w.Write(q.Ai1);
                w.Write(q.LocalAi0);
                w.Write(q.LocalAi1);
                w.Write(q.Timer2);
                w.Write((ushort)Mathf.Clamp(q.Frame, 0, ushort.MaxValue));
                w.Write((sbyte)(q.Dir < 0f ? -1 : 1));
                w.Write(q.Ticks);
                w.Write(q.Anim);
                int f = (q.Returning ? FReturning : 0) | (q.Channel ? FChannel : 0) | (q.Free ? FFree : 0) | (q.Spent ? FSpent : 0) | (q.Stopped ? FStopped : 0)
                        | (q.OffHand ? FOffHand : 0) | (q.Dual ? FDual : 0) | (q.MinionShot ? FMinionShot : 0);
                w.Write((byte)f);
                w.Write(q.Damage);
                w.Write((byte)Mathf.Clamp(Mathf.RoundToInt(q.Crit), 0, 255));
                w.Write(q.HitSize.x);
                w.Write(q.HitSize.y);
                var point = q.Point - TerraParticles.ToPx(shift) + TerraParticles.ToPx(Vector2.zero);
                w.Write(point.x);
                w.Write(point.y);
                w.Write((byte)Mathf.Clamp(q.Extra, 0, 255));
                w.Write((ushort)q.Item);
                w.Write(q.Parent != null ? q.Parent.NetId : 0);
                w.Write(q.Remade);
                n++;
            }
            var data = p.Done();
            data[countAt] = (byte)n;
            data[countAt + 1] = (byte)(n >> 8);
            NetSession.Send(data);
        }

        public void NetReceive(int slot, BinaryReader r)
        {
            string scene = r.ReadString();
            int n = r.ReadUInt16();
            bool here = NetScenes.Shift(scene, out var shift);
            if (!ghosts.TryGetValue(slot, out var mine)) ghosts[slot] = mine = new Dictionary<int, Ghost>();
            float now = Time.unscaledTime;
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                var kind = (ProjKind)r.ReadUInt16();
                var pos = new Vector2(r.ReadSingle(), r.ReadSingle());
                var vel = new Vector2(r.ReadSingle(), r.ReadSingle());
                float rot = r.ReadSingle(), trot = r.ReadSingle(), scale = r.ReadSingle(), alpha = r.ReadSingle();
                float ai0 = r.ReadSingle(), ai1 = r.ReadSingle(), lai0 = r.ReadSingle(), lai1 = r.ReadSingle(), timer2 = r.ReadSingle();
                int frame = r.ReadUInt16();
                int dir = r.ReadSByte();
                int ticks = r.ReadInt32();
                int anim = r.ReadInt32();
                int f = r.ReadByte();
                int damage = r.ReadInt32();
                int crit = r.ReadByte();
                var hitSize = new Vector2(r.ReadSingle(), r.ReadSingle());
                var point = new Vector2(r.ReadSingle(), r.ReadSingle());
                int extra = r.ReadByte();
                var item = (ItemType)r.ReadUInt16();
                int parent = r.ReadInt32();
                bool remade = r.ReadBoolean();
                if (!here || !specs.TryGetValue(kind, out var spec)) continue;
                if (!mine.TryGetValue(id, out var g) || g.P.Kind != kind)
                {
                    g = new Ghost { P = new Proj { Kind = kind, Spec = spec } };
                    mine[id] = g;
                }
                var q = g.P;
                q.Remade = remade;
                g.Scene = scene;
                g.Seen = now;
                g.ParentId = parent;
                q.Pos = pos + shift;
                q.Vel = vel;
                q.Rot = rot;
                q.TRot = trot;
                q.Scale = scale;
                q.Alpha = alpha;
                q.Ai0 = ai0;
                q.Ai1 = ai1;
                q.LocalAi0 = lai0;
                q.LocalAi1 = lai1;
                q.Timer2 = timer2;
                q.Frame = frame;
                q.Dir = dir;
                q.Ticks = ticks;
                q.Anim = anim;
                q.Returning = (f & FReturning) != 0;
                q.Channel = (f & FChannel) != 0;
                q.Free = (f & FFree) != 0;
                q.Spent = (f & FSpent) != 0;
                q.Stopped = (f & FStopped) != 0;
                q.OffHand = (f & FOffHand) != 0;
                q.Dual = (f & FDual) != 0;
                q.MinionShot = (f & FMinionShot) != 0;
                q.Damage = damage;
                q.Crit = crit;
                q.HitSize = hitSize;
                q.Point = point + TerraParticles.ToPx(shift) - TerraParticles.ToPx(Vector2.zero);
                q.Extra = extra;
                q.Item = item;
                q.NetId = id;
            }
            // Gone from the owner's game: gone here.
            List<int> drop = null;
            foreach (var kv in mine)
                if (kv.Value.Seen < now - 1e-4f && kv.Value.Seen != now) (drop ?? (drop = new List<int>())).Add(kv.Key);
            if (drop != null) foreach (var id in drop) mine.Remove(id);
            foreach (var g in mine.Values) g.P.Parent = g.ParentId != 0 && mine.TryGetValue(g.ParentId, out var par) ? par.P : null;
        }

        /// <summary>Between snapshots, on Terraria's tick: a ghost flies on along its speed (one held by its owner stays put), its trail recorded.</summary>
        void TickGhosts()
        {
            foreach (var mine in ghosts.Values)
                foreach (var g in mine.Values)
                {
                    var q = g.P;
                    var s = q.Spec;
                    for (int u = 0; u <= q.Extra; u++)
                    {
                        if (s.TrailLength > 0 && s.Ai != Ai.Missile)
                        {
                            q.Trail.Insert(0, q.Pos);
                            if (q.Trail.Count > s.TrailLength) q.Trail.RemoveAt(q.Trail.Count - 1);
                        }
                        if (!s.OwnMove && !s.Held && s.Ai != Ai.Spear && s.Ai != Ai.Vine && !q.Stopped) q.Pos += q.Vel * Px;
                    }
                    q.Ticks++;
                }
        }

        /// <summary>The ghosts, drawn as their kind is (after the player's own projectiles).</summary>
        void DrawGhosts(FxBatch fx)
        {
            if (ghosts.Count == 0) return;
            try
            {
                foreach (var kv in ghosts)
                {
                    var owner = NetSession.Player(kv.Key);
                    ghostHolder = owner != null && owner.Visible ? TerraParticles.ToPx(owner.Center) : (Vector2?)null;
                    foreach (var g in kv.Value.Values)
                    {
                        try { using var edition = new WeaponRefitting.Scope(g.P.Remade); DrawOne(fx, g.P); }
                        catch (System.Exception) { }
                    }
                }
            }
            finally
            {
                ghostHolder = null;
            }
        }

        /// <summary>PvP: the hostile players' ghosts against the local player's box (Projectile.Damage's player check).</summary>
        public void NetPvpCheck(Rect hero)
        {
            foreach (var kv in ghosts)
            {
                var owner = NetSession.Player(kv.Key);
                if (!PvpHits.Hostile(owner)) continue;
                foreach (var g in kv.Value.Values)
                {
                    var q = g.P;
                    var s = q.Spec;
                    if (q.Damage <= 0 || q.MinionShot || Time.unscaledTime - g.HitAt < 1f) continue;
                    var size = (q.HitSize != Vector2.zero ? q.HitSize : new Vector2((int)(s.W * s.Scale), (int)(s.H * s.Scale))) * Px;
                    var box = new Rect(q.Pos - size * 0.5f, size);
                    if (!box.Overlaps(hero)) continue;
                    g.HitAt = Time.unscaledTime;
                    PvpHits.Hurt(owner, q.Damage, q.Crit, q.Pos, null);
                    return;
                }
            }
        }
    }
}
