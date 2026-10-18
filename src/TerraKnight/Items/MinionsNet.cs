using System.Collections.Generic;
using System.IO;
using TerraKnight.Effects;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Minions and sentries in a LAN game: the owner's game runs them, the others draw them. Twenty
    /// times a second each one's kind, place (in the scene's frame), speed, frame, turn and AI values
    /// go out; the others keep ghosts of them, moved on along their speed between snapshots with
    /// their trails, and drawn by the minions' own drawing as if their owner were that player.
    /// </summary>
    internal sealed partial class Minions
    {
        readonly Dictionary<int, Dictionary<int, M>> ghosts = new Dictionary<int, Dictionary<int, M>>();
        readonly Dictionary<int, Dictionary<int, int>> ghostLeaders = new Dictionary<int, Dictionary<int, int>>();
        readonly Dictionary<int, List<M>> ghostLists = new Dictionary<int, List<M>>();

        public void NetClearGhosts()
        {
            ghosts.Clear();
            ghostLeaders.Clear();
            ghostLists.Clear();
        }

        public void NetDropGhosts(int slot)
        {
            ghosts.Remove(slot);
            ghostLeaders.Remove(slot);
            ghostLists.Remove(slot);
        }

        public int DevGhosts
        {
            get
            {
                int n = 0;
                foreach (var g in ghosts.Values) n += g.Count;
                return n;
            }
        }

        const int FLatched = 1, FCarried = 2, FHasTarget = 4, FFriendly = 8;

        public void NetSend()
        {
            string scene = NetScenes.Current;
            if (string.IsNullOrEmpty(scene) || !NetScenes.Shift(scene, out var shift)) return;
            // Nothing to send but once more after the last one went (the others drop their ghosts).
            if (live.Count == 0 && !hadMinions) return;
            hadMinions = live.Count > 0;
            var shiftPx = TerraParticles.ToPx(shift);
            var p = NetSession.Mine(Msg.MinionSnapshot);
            p.W.Write(scene);
            long countAt = p.Length;
            p.W.Write((ushort)0);
            int n = 0;
            foreach (var m in live)
            {
                if (m.Dead || p.Length > 60000) continue;
                var w = p.W;
                w.Write(m.Serial);
                w.Write((byte)m.Kind);
                var local = m.Pos - shiftPx;
                w.Write(local.x);
                w.Write(local.y);
                w.Write(m.Vel.x);
                w.Write(m.Vel.y);
                w.Write((short)m.W);
                w.Write((short)m.H);
                w.Write(m.Ai0);
                w.Write(m.Ai1);
                w.Write(m.Ai2);
                w.Write(m.LocalAi0);
                w.Write(m.LocalAi1);
                w.Write(m.LocalAi2);
                w.Write((short)m.Frame);
                w.Write((short)m.FrameCounter);
                w.Write(m.Rot);
                w.Write((sbyte)m.Dir);
                w.Write((sbyte)m.SpriteDir);
                w.Write(m.Alpha);
                w.Write(m.Scale);
                w.Write(m.GfxOffY);
                w.Write((byte)((m.Latched ? FLatched : 0) | (m.Carried ? FCarried : 0) | (m.HasTarget ? FHasTarget : 0) | (m.Friendly ? FFriendly : 0)));
                w.Write(m.Leader != null ? m.Leader.Serial : -1);
                w.Write((short)m.PetJoy);
                w.Write((byte)Mathf.Clamp(m.MinionPos, 0, 255));
                n++;
            }
            var data = p.Done();
            data[countAt] = (byte)n;
            data[countAt + 1] = (byte)(n >> 8);
            NetSession.Send(data);
        }

        /// <summary>Whether the last snapshot sent had minions (one more, empty, tells the others they are gone).</summary>
        bool hadMinions;

        public void NetReceive(int slot, BinaryReader r)
        {
            string scene = r.ReadString();
            int n = r.ReadUInt16();
            bool here = NetScenes.Shift(scene, out var shift);
            var shiftPx = TerraParticles.ToPx(shift);
            if (!ghosts.TryGetValue(slot, out var mine)) ghosts[slot] = mine = new Dictionary<int, M>();
            if (!ghostLeaders.TryGetValue(slot, out var leaders)) ghostLeaders[slot] = leaders = new Dictionary<int, int>();
            var seen = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                int serial = r.ReadInt32();
                var kind = (Kind)r.ReadByte();
                var pos = new Vector2(r.ReadSingle(), r.ReadSingle());
                var vel = new Vector2(r.ReadSingle(), r.ReadSingle());
                int w = r.ReadInt16(), h = r.ReadInt16();
                float ai0 = r.ReadSingle(), ai1 = r.ReadSingle(), ai2 = r.ReadSingle(), l0 = r.ReadSingle(), l1 = r.ReadSingle(), l2 = r.ReadSingle();
                int frame = r.ReadInt16(), counter = r.ReadInt16();
                float rot = r.ReadSingle();
                int dir = r.ReadSByte(), sdir = r.ReadSByte();
                float alpha = r.ReadSingle(), scale = r.ReadSingle(), gfx = r.ReadSingle();
                int f = r.ReadByte();
                int leader = r.ReadInt32();
                int pet = r.ReadInt16();
                int minionPos = r.ReadByte();
                if (!here || !specs.TryGetValue(kind, out var s)) continue;
                seen.Add(serial);
                if (!mine.TryGetValue(serial, out var m) || m.Kind != kind)
                {
                    m = new M { Kind = kind, S = s, Serial = serial };
                    mine[serial] = m;
                    for (int k = 0; k < m.OldPosRing.Length; k++) m.OldPosRing[k] = pos + shiftPx;
                }
                m.Pos = pos + shiftPx;
                m.Vel = vel;
                m.W = w;
                m.H = h;
                m.Ai0 = ai0;
                m.Ai1 = ai1;
                m.Ai2 = ai2;
                m.LocalAi0 = l0;
                m.LocalAi1 = l1;
                m.LocalAi2 = l2;
                m.Frame = frame;
                m.FrameCounter = counter;
                m.Rot = rot;
                m.Dir = dir;
                m.SpriteDir = sdir;
                m.Alpha = alpha;
                m.Scale = scale;
                m.GfxOffY = gfx;
                m.Latched = (f & FLatched) != 0;
                m.Carried = (f & FCarried) != 0;
                m.HasTarget = (f & FHasTarget) != 0;
                m.Friendly = (f & FFriendly) != 0;
                m.PetJoy = pet;
                m.MinionPos = minionPos;
                leaders[serial] = leader;
            }
            List<int> gone = null;
            foreach (var k in mine.Keys)
                if (!seen.Contains(k)) (gone ?? (gone = new List<int>())).Add(k);
            if (gone != null)
                foreach (var k in gone)
                {
                    mine.Remove(k);
                    leaders.Remove(k);
                }
            foreach (var m in mine.Values) m.Leader = leaders.TryGetValue(m.Serial, out int l) && l >= 0 && mine.TryGetValue(l, out var lm) ? lm : null;
            if (!ghostLists.TryGetValue(slot, out var list)) ghostLists[slot] = list = new List<M>();
            list.Clear();
            list.AddRange(mine.Values);
        }

        /// <summary>On Terraria's tick: ghosts on along their speed, their old places recorded (the trails).</summary>
        void TickGhosts()
        {
            foreach (var list in ghostLists.Values)
                foreach (var m in list)
                {
                    for (int k = m.OldPosRing.Length - 1; k > 0; k--)
                    {
                        m.OldPosRing[k] = m.OldPosRing[k - 1];
                        m.OldRot[k] = m.OldRot[k - 1];
                        m.OldSpriteDir[k] = m.OldSpriteDir[k - 1];
                    }
                    m.OldPosRing[0] = m.Pos;
                    m.OldRot[0] = m.Rot;
                    m.OldSpriteDir[0] = m.SpriteDir;
                    if (!m.Latched && !m.Carried) m.Pos += m.Vel * (1 + m.Extra);
                }
        }

        void DrawGhosts(FxBatch fx)
        {
            if (ghostLists.Count == 0) return;
            var keep = live;
            bool keepOwner = hasOwner;
            try
            {
                foreach (var kv in ghostLists)
                {
                    var owner = NetSession.Player(kv.Key);
                    if (owner == null || !owner.Visible || kv.Value.Count == 0) continue;
                    var size = owner.Latest.BoxSize;
                    var min = owner.Center - size * 0.5f;
                    pBox = new Rect(min.x / Px, -(min.y + size.y) / Px, size.x / Px, size.y / Px);
                    pCenter = pBox.center;
                    pDir = owner.Latest.FacingRight ? 1 : -1;
                    hasOwner = true;
                    live = kv.Value;
                    try { DrawMinions(fx, true); }
                    catch (System.Exception) { }
                }
            }
            finally
            {
                live = keep;
                hasOwner = keepOwner;
                RefreshOwner();
            }
        }
    }
}
