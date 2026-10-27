using System.Collections.Generic;
using System.IO;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// What a player's game shows of its own that the others should see too (Terraria's CombatText over the network,
    /// NetMessage 81/82): the floating numbers (a blow's damage on an enemy, a player hurt, healed or given mana), and the
    /// dust and gores the player's movement, accessories, weapons and hits throw off (the Cloud in a Bottle's puffs,
    /// rocket boots, a sword's sparks, the blood of a hit). Each sent as it is made, in its scene's own frame, to whoever
    /// has that scene loaded; there it is made again as it was and goes on the same way.
    /// </summary>
    internal static partial class NetFx
    {
        const byte KText = 1, KDust = 2, KGore = 3;
        /// <summary>A player's Knight's own spawns (SceneMirror.SendMine): no scene first, each entry carries its own.</summary>
        internal const byte KSpawns = 4;

        /// <summary>While one of these sent by another player is being made here: not sent on again.</summary>
        public static bool Applying;

        public static int DevTextsSent, DevTextsShown, DevDustSent, DevDustShown, DevGoresSent, DevGoresShown;

        static readonly Dictionary<string, (float At, bool Yes)> watched = new Dictionary<string, (float, bool)>();

        /// <summary>Another player has this scene loaded (looked at again twice a second).</summary>
        static bool Watched(string scene)
        {
            if (!NetSession.Active || NetSession.State != NetSession.Phase.Ready || string.IsNullOrEmpty(scene)) return false;
            if (watched.TryGetValue(scene, out var w) && Time.unscaledTime - w.At < 0.5f) return w.Yes;
            bool yes = false;
            foreach (var p in NetSession.Players.Values)
                if (p.Loaded.Contains(scene) || p.In.Contains(scene) || (p.HasState && p.Latest.Scene == scene)) { yes = true; break; }
            watched[scene] = (Time.unscaledTime, yes);
            return yes;
        }

        static string SceneAt(Vector2 world)
        {
            var cell = World.CellGrid.WorldToCell(world);
            return World.TerraWorld.RegionAt(cell.x, cell.y)?.SceneName ?? NetScenes.Current;
        }

        /// <summary>Whether what this game makes now is worth sending (someone else has the player's scene).</summary>
        public static bool Sending => !Applying && Watched(NetScenes.Current);

        // ------------------------------------------------------------------ the numbers

        /// <summary>The numbers others see too: damage, hurt, healing, mana (not the notices and pickups, which are the player's own).</summary>
        static bool Shared(Color c) => c == UI.CombatText.EnemyHurt || c == UI.CombatText.EnemyCrit || c == UI.CombatText.PlayerHurt
            || c == UI.CombatText.Heal || c == UI.CombatText.Mana;

        /// <summary>A number just shown here (UI.CombatText.Show): to the others with that scene.</summary>
        public static void Text(Vector3 world, string text, Color color, bool big)
        {
            if (Applying || !Shared(color) || string.IsNullOrEmpty(text)) return;
            string scene = SceneAt(world);
            if (!Watched(scene)) return;
            var local = NetScenes.ToLocal(scene, world);
            var p = NetSession.Mine(Msg.NetFx);
            p.W.Write(KText);
            p.W.Write(scene);
            p.W.Write(local.x);
            p.W.Write(local.y);
            p.W.Write(text);
            p.W.Write((byte)(color.r * 255f));
            p.W.Write((byte)(color.g * 255f));
            p.W.Write((byte)(color.b * 255f));
            p.W.Write(big);
            NetSession.Send(p.Done());
            DevTextsSent++;
        }

        // ------------------------------------------------------------------ dust and gores

        static readonly List<TerraParticles.Dust> dust = new List<TerraParticles.Dust>();
        static readonly List<TerraParticles.GoreState> gores = new List<TerraParticles.GoreState>();
        const int MaxDustPerFrame = 48, MaxGoresPerFrame = 24;

        /// <summary>The dust made here this frame, as its makers left it (TerraParticles, at the end of the frame).</summary>
        public static void QueueDust(TerraParticles.Dust d)
        {
            if (dust.Count < MaxDustPerFrame) dust.Add(d);
        }

        public static void QueueGore(TerraParticles.GoreState g)
        {
            if (gores.Count < MaxGoresPerFrame) gores.Add(g);
        }

        /// <summary>The frame's dust and gores, in the player's scene's own frame, to the others.</summary>
        public static void Flush()
        {
            if (dust.Count == 0 && gores.Count == 0) return;
            string scene = NetScenes.Current;
            if (!Watched(scene) || !NetScenes.Shift(scene, out var shift))
            {
                dust.Clear();
                gores.Clear();
                return;
            }
            if (dust.Count > 0)
            {
                var p = NetSession.Mine(Msg.NetFx);
                p.W.Write(KDust);
                p.W.Write(scene);
                p.W.Write((ushort)dust.Count);
                foreach (var d in dust)
                {
                    var at = TerraParticles.ToWorld(d.Position) - shift;
                    p.W.Write((ushort)d.Type);
                    p.W.Write(at.x); p.W.Write(at.y);
                    p.W.Write(d.Velocity.x); p.W.Write(d.Velocity.y);
                    p.W.Write(d.Scale); p.W.Write(d.Rotation); p.W.Write(d.FadeIn);
                    p.W.Write((byte)Mathf.Clamp(d.Alpha, 0, 255));
                    p.W.Write((byte)((d.NoGravity ? 1 : 0) | (d.Additive ? 2 : 0)));
                    p.W.Write((byte)d.Variant);
                    var c = (Color32)d.Color;
                    p.W.Write(c.r); p.W.Write(c.g); p.W.Write(c.b); p.W.Write(c.a);
                }
                NetSession.Send(p.Done());
                DevDustSent += dust.Count;
                dust.Clear();
            }
            if (gores.Count > 0)
            {
                var p = NetSession.Mine(Msg.NetFx);
                p.W.Write(KGore);
                p.W.Write(scene);
                p.W.Write((ushort)gores.Count);
                foreach (var g in gores)
                {
                    var at = TerraParticles.ToWorld(g.Position) - shift;
                    p.W.Write((ushort)g.Type);
                    p.W.Write(at.x); p.W.Write(at.y);
                    p.W.Write(g.Velocity.x); p.W.Write(g.Velocity.y);
                    p.W.Write(g.Scale); p.W.Write(g.Rotation);
                    p.W.Write((byte)Mathf.Clamp(g.Alpha, 0, 255));
                    p.W.Write(g.Falls);
                    p.W.Write((short)g.TimeLeft);
                }
                NetSession.Send(p.Done());
                DevGoresSent += gores.Count;
                gores.Clear();
            }
        }

        // ------------------------------------------------------------------ the others'

        public static void Receive(int from, BinaryReader r)
        {
            byte kind = r.ReadByte();
            if (kind == KSpawns) { SceneMirror.ReceiveMine(from, r); return; }
            string scene = r.ReadString();
            bool here = NetScenes.Shift(scene, out var shift);
            var pp = TerraParticles.Instance;
            Applying = true;
            try
            {
                if (kind == KSound) ReceiveSound(r, scene, here, shift);
                else if (kind == KMusic) ReceiveMusic(r, scene);
                else if (kind == KText)
                {
                    var at = new Vector2(r.ReadSingle(), r.ReadSingle()) + shift;
                    string text = r.ReadString();
                    var color = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), 255);
                    bool big = r.ReadBoolean();
                    if (!here) return;
                    UI.CombatText.Show(at, text, Exact(color), big);
                    DevTextsShown++;
                }
                else if (kind == KDust)
                {
                    int n = r.ReadUInt16();
                    for (int i = 0; i < n; i++)
                    {
                        var d = new TerraParticles.Dust { Active = true, Type = r.ReadUInt16() };
                        var at = new Vector2(r.ReadSingle(), r.ReadSingle()) + shift;
                        d.Position = TerraParticles.ToPx(at);
                        d.Velocity = new Vector2(r.ReadSingle(), r.ReadSingle());
                        d.Scale = r.ReadSingle(); d.Rotation = r.ReadSingle(); d.FadeIn = r.ReadSingle();
                        d.Alpha = r.ReadByte();
                        byte flags = r.ReadByte();
                        d.NoGravity = (flags & 1) != 0;
                        d.Additive = (flags & 2) != 0;
                        d.Variant = r.ReadByte();
                        d.Color = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                        if (here && pp != null) { pp.NetDust(d); DevDustShown++; }
                    }
                }
                else if (kind == KGore)
                {
                    int n = r.ReadUInt16();
                    for (int i = 0; i < n; i++)
                    {
                        var g = new TerraParticles.GoreState { Active = true, Type = r.ReadUInt16() };
                        var at = new Vector2(r.ReadSingle(), r.ReadSingle()) + shift;
                        g.Position = TerraParticles.ToPx(at);
                        g.Velocity = new Vector2(r.ReadSingle(), r.ReadSingle());
                        g.Scale = r.ReadSingle(); g.Rotation = r.ReadSingle();
                        g.Alpha = r.ReadByte();
                        g.Falls = r.ReadBoolean();
                        g.TimeLeft = r.ReadInt16();
                        if (here && pp != null) { pp.NetGore(g); DevGoresShown++; }
                    }
                }
            }
            finally { Applying = false; }
        }

        /// <summary>The number's colour as the sender had it (one of CombatText's own, so the DPS meter and the rest know it).</summary>
        static Color Exact(Color32 c)
        {
            foreach (var k in new[] { UI.CombatText.EnemyHurt, UI.CombatText.EnemyCrit, UI.CombatText.PlayerHurt, UI.CombatText.Heal, UI.CombatText.Mana })
            {
                var b = (Color32)k;
                if (Mathf.Abs(b.r - c.r) <= 1 && Mathf.Abs(b.g - c.g) <= 1 && Mathf.Abs(b.b - c.b) <= 1) return k;
            }
            return c;
        }

        public static void Reset()
        {
            dust.Clear();
            gores.Clear();
            watched.Clear();
            Applying = false;
        }
    }
}
