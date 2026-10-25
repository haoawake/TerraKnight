using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TerraKnight.EnemyAi
{
    /// <summary>
    /// The new enemy AI in a LAN game whose players share a scene's enemies. The scene's owner runs the brains; the
    /// others get each attack as Net.EnemyAttacks sends it (its hitbox, at the owner's timing and place) with, after it,
    /// what this AI adds: a shot's look and flight (drawn there and carried on between messages), an area's shape, its
    /// warning and whether it hurts yet (warnings and webs come too, with no live hitbox), and the debuff, hook cut and
    /// knock-off its touch carries, which each player's own game puts on its own player. A pull goes to the player it
    /// pulls (Msg.EnemyAi). A game from before this sends none of it and reads past it.
    /// </summary>
    internal static class NetAi
    {
        const byte None = 0, ShotKind = 1, ZoneKind = 2, BodyKind = 3;
        const byte FHeavy = 1, FCut = 2, FHurts = 4;

        // ------------------------------------------------------------------ the scene's owner

        /// <summary>After a collider's hitbox and picture (Net.EnemyAttacks.SendCollider): what this AI adds to it.</summary>
        public static void WriteExtra(BinaryWriter w, Collider2D c)
        {
            var a = c != null ? c.GetComponent<Hostiles.Attack>() : null;
            if (a is Hostiles.Shot s)
            {
                w.Write(ShotKind);
                w.Write((byte)s.Data.Look);
                w.Write(s.Velocity.x); w.Write(s.Velocity.y);
                w.Write(s.Gravity); w.Write(s.Radius);
                Flags(w, s.Data.Debuff, s.Data.DebuffTicks, false, s.Cuts, s.Hurts);
                return;
            }
            if (a is Hostiles.Zone z)
            {
                w.Write(ZoneKind);
                w.Write((byte)z.LookOf);
                w.Write((byte)z.Form);
                w.Write(z.Live); w.Write(z.Unseen);
                w.Write(z.Width); w.Write(z.Height); w.Write(z.Radius); w.Write(z.Length); w.Write(z.Angle);
                Flags(w, z.Data.Debuff, z.Data.DebuffTicks, false, z.Cuts, z.Hurts);
                return;
            }
            var brain = c != null ? c.GetComponentInParent<Brain>() : null;
            if (brain != null && brain.TouchFlags(out int d, out int ticks, out bool heavy, out bool cut))
            {
                w.Write(BodyKind);
                Flags(w, d, ticks, heavy, cut, true);
                return;
            }
            w.Write(None);
        }

        static void Flags(BinaryWriter w, int debuff, int ticks, bool heavy, bool cut, bool hurts)
        {
            w.Write((short)debuff);
            w.Write((short)Mathf.Clamp(ticks, 0, short.MaxValue));
            w.Write((byte)((heavy ? FHeavy : 0) | (cut ? FCut : 0) | (hurts ? FHurts : 0)));
        }

        static readonly List<Collider2D> unarmed = new List<Collider2D>();

        /// <summary>This AI's areas in a scene that have no live hitbox now (warnings, webs, honey): sent for their pictures.</summary>
        public static List<Collider2D> Unarmed(string scene)
        {
            unarmed.Clear();
            foreach (var a in Hostiles.Attack.All)
                if (a != null && a.Col != null && !a.Hurts && !(a is Hostiles.Zone z && z.MarkOnly) && a.gameObject.scene.name == scene) unarmed.Add(a.Col);
            return unarmed;
        }

        /// <summary>A pull on another player (the Gluttonous Husk's inhale, the Little Weaver's silk): his game pulls him.</summary>
        public static void SendPull(string scene, int slot, Vector2 from, float speed, float seconds)
        {
            if (slot < 0 || !Net.NetScenes.Shift(scene, out var shift)) return;
            var p = Net.NetSession.Mine(Net.Msg.EnemyAi);
            p.W.Write(scene);
            p.W.Write((byte)1);
            p.W.Write((byte)slot);
            var at = from - shift;
            p.W.Write(at.x); p.W.Write(at.y);
            p.W.Write(speed); p.W.Write(seconds);
            Net.NetSession.Send(p.Done());
        }

        // ------------------------------------------------------------------ the others

        public static void Receive(int owner, BinaryReader r)
        {
            string scene = r.ReadString();
            byte op = r.ReadByte();
            if (op != 1) return;
            int slot = r.ReadByte();
            var at = new Vector2(r.ReadSingle(), r.ReadSingle());
            float speed = r.ReadSingle(), seconds = r.ReadSingle();
            if (slot != Net.NetSession.MySlot || !Net.NetScenes.Shift(scene, out var shift)) return;
            PlayerDebuffs.Pull(at + shift, speed, seconds);
        }

        /// <summary>After a ghost's hitbox and picture (Net.EnemyAttacks.Receive): this AI's part of it, if the owner sent one.</summary>
        public static void ReadExtra(BinaryReader r, GameObject ghost, Collider2D col, Vector2 at)
        {
            if (ghost == null) return;
            var view = ghost.GetComponent<GhostView>();
            if (r.BaseStream.Position >= r.BaseStream.Length)
            {
                if (view != null) view.Kind = None;
                return;
            }
            byte kind = r.ReadByte();
            if (kind == None)
            {
                if (view != null) view.Kind = None;
                return;
            }
            if (view == null) view = ghost.AddComponent<GhostView>();
            view.Kind = kind;
            view.Col = col;
            if (kind == ShotKind)
            {
                view.Style = Hostiles.StyleOf((Look)r.ReadByte());
                view.Velocity = new Vector2(r.ReadSingle(), r.ReadSingle());
                view.Gravity = r.ReadSingle();
                view.Radius = r.ReadSingle();
            }
            else if (kind == ZoneKind)
            {
                view.Style = Hostiles.StyleOf((Look)r.ReadByte());
                view.Shape = (Hostiles.Zone.Shape)r.ReadByte();
                view.Live = r.ReadBoolean();
                view.Unseen = r.ReadBoolean();
                view.Width = r.ReadSingle(); view.Height = r.ReadSingle(); view.ZoneRadius = r.ReadSingle();
                view.Length = r.ReadSingle(); view.Angle = r.ReadSingle();
            }
            view.Debuff = r.ReadInt16();
            view.Ticks = r.ReadInt16();
            byte f = r.ReadByte();
            view.Heavy = (f & FHeavy) != 0;
            view.Cut = (f & FCut) != 0;
            view.Hurts = (f & FHurts) != 0;
            view.Base = at;
            view.BaseAt = Time.time;
            // A warning or a web: drawn, never a hitbox (Net.EnemyAttacks.Tick hurts only through an enabled collider).
            if (kind != BodyKind && col != null) col.enabled = view.Hurts;
        }

        /// <summary>A ghost's part from this AI: drawn, carried along its flight, and what its touch puts on this game's player.</summary>
        internal sealed class GhostView : MonoBehaviour
        {
            public byte Kind;
            public Collider2D Col;
            public Hostiles.Style Style;
            public Vector2 Velocity, Base;
            public float Gravity, Radius, BaseAt;
            public Hostiles.Zone.Shape Shape;
            public bool Live, Unseen, Heavy, Cut, Hurts;
            public float Width, Height, ZoneRadius, Length, Angle;
            public int Debuff, Ticks;
            Hostiles.Visual look;

            void Update()
            {
                if (Kind == None || Kind == BodyKind) return;
                float now = Time.time;
                if (Kind == ShotKind)
                {
                    // Between the owner's messages (fifteen a second) it flies on as it was going.
                    float dt = Mathf.Min(now - BaseAt, 0.2f);
                    var pos = Base + Velocity * dt + Vector2.down * (0.5f * Gravity * dt * dt);
                    transform.position = new Vector3(pos.x, pos.y, transform.position.z);
                    if (Style != null) Hostiles.DrawShot(Style, pos, Velocity + Vector2.down * Gravity * dt, Radius, ref look);
                }
                else if (Style != null && !Unseen)
                {
                    Hostiles.DrawZone(Style, Shape, Live, transform.position, Width, Height, ZoneRadius, Length, Angle, now, ref look);
                }
                if (Cut && Hurts && Col != null && Col.enabled && Hostiles.CrossesRope(Col)) EnemyAi.CutHooks(transform.position);
            }

            /// <summary>It just hurt this game's player (EnemyAi's TakeDamage postfix): its debuff, and off the mount if heavy.</summary>
            public void HurtPlayer()
            {
                if (Debuff > 0) PlayerDebuffs.Apply(Debuff, Ticks);
                if (Heavy && Player.Mounts.Riding) Player.Mounts.Dismount();
            }
        }
    }
}
