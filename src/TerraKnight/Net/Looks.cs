using System.IO;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// How one object looks, as the game running it has it, written to the others and put on their copy: switched on or
    /// off, drawn or not, its place, turn and size, its tk2d sprite and the clip it plays (the copy plays the same clip
    /// itself from that frame), its colour (fades, flashes), its particles running or not, its collider on or off.
    /// </summary>
    internal static class Looks
    {
        const byte FOn = 1, FDrawn = 2, FHasRend = 4, FEmit = 8, FHasPs = 16, FCol = 32, FHasCol = 64, FClip = 128;

        public struct State
        {
            public byte Flags;
            public Vector3 Pos;
            public float Rot;
            public Vector2 Scale;
            public int Sprite, Clip, Frame;
            public Color32 Color;
            public bool On => (Flags & FOn) != 0;
        }

        /// <summary>A stable short hash of a name (the same in every game): the copy's object is checked against it.</summary>
        public static ushort Hash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return (ushort)(h ^ (h >> 16));
        }

        public static State Of(Transform tr, Vector3 pos, float rot)
        {
            var s = new State { Pos = pos, Rot = rot, Scale = tr.localScale, Sprite = -1, Clip = -1, Color = new Color32(255, 255, 255, 255) };
            if (tr.gameObject.activeSelf) s.Flags |= FOn;
            var rend = tr.GetComponent<Renderer>();
            if (rend != null) { s.Flags |= FHasRend; if (rend.enabled) s.Flags |= FDrawn; }
            var ps = tr.GetComponent<ParticleSystem>();
            if (ps != null) { s.Flags |= FHasPs; if (ps.isEmitting) s.Flags |= FEmit; }
            var col = tr.GetComponent<Collider2D>();
            if (col != null) { s.Flags |= FHasCol; if (col.enabled) s.Flags |= FCol; }
            var sprite = tr.GetComponent<tk2dBaseSprite>();
            if (sprite != null)
            {
                s.Sprite = sprite.spriteId;
                s.Color = sprite.color;
            }
            else if (rend is SpriteRenderer sr) s.Color = sr.color;
            var anim = tr.GetComponent<tk2dSpriteAnimator>();
            if (anim != null && anim.Playing && anim.CurrentClip != null)
            {
                s.Flags |= FClip;
                s.Clip = anim.GetClipIdByName(anim.CurrentClip.name);
                s.Frame = anim.CurrentFrame;
            }
            return s;
        }

        /// <summary>Whether two looks differ enough to send again (the frame of a playing clip aside: the copy plays it on).</summary>
        public static bool Differs(State a, State b, float move = 0.02f) =>
            a.Flags != b.Flags || a.Sprite != b.Sprite && (a.Flags & FClip) == 0 || a.Clip != b.Clip || !a.Color.Equals(b.Color)
            || (a.Pos - b.Pos).sqrMagnitude > move * move || Mathf.Abs(Mathf.DeltaAngle(a.Rot, b.Rot)) > 0.5f || (a.Scale - b.Scale).sqrMagnitude > 1e-4f;

        public static void Write(BinaryWriter w, State s)
        {
            w.Write(s.Flags);
            w.Write(s.Pos.x); w.Write(s.Pos.y); w.Write(s.Pos.z);
            w.Write(s.Rot);
            w.Write(s.Scale.x); w.Write(s.Scale.y);
            w.Write(s.Sprite);
            if ((s.Flags & FClip) != 0) { w.Write((short)s.Clip); w.Write((byte)Mathf.Clamp(s.Frame, 0, 255)); }
            w.Write(s.Color.r); w.Write(s.Color.g); w.Write(s.Color.b); w.Write(s.Color.a);
        }

        public static State Read(BinaryReader r)
        {
            var s = new State { Flags = r.ReadByte() };
            s.Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            s.Rot = r.ReadSingle();
            s.Scale = new Vector2(r.ReadSingle(), r.ReadSingle());
            s.Sprite = r.ReadInt32();
            s.Clip = -1;
            if ((s.Flags & FClip) != 0) { s.Clip = r.ReadInt16(); s.Frame = r.ReadByte(); }
            s.Color = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
            return s;
        }

        /// <summary>
        /// The look put on this game's object. Its place only when <paramref name="place"/> (the caller moves the object
        /// itself otherwise): local to its parent when <paramref name="local"/>, else in this world. Its collider only
        /// when <paramref name="collider"/>.
        /// </summary>
        public static void Apply(Transform tr, State s, bool place, bool local, bool collider)
        {
            var go = tr.gameObject;
            if (go.activeSelf != s.On) go.SetActive(s.On);
            if (!s.On) return;
            if (place)
            {
                if (local)
                {
                    tr.localPosition = s.Pos;
                    tr.localEulerAngles = new Vector3(0f, 0f, s.Rot);
                }
                else
                {
                    tr.position = s.Pos;
                    var e = tr.eulerAngles;
                    tr.eulerAngles = new Vector3(e.x, e.y, s.Rot);
                }
            }
            tr.localScale = new Vector3(s.Scale.x, s.Scale.y, tr.localScale.z);
            var rend = tr.GetComponent<Renderer>();
            if (rend != null && (s.Flags & FHasRend) != 0) rend.enabled = (s.Flags & FDrawn) != 0;
            var sprite = tr.GetComponent<tk2dBaseSprite>();
            var anim = tr.GetComponent<tk2dSpriteAnimator>();
            if ((s.Flags & FClip) != 0 && anim != null && go.activeInHierarchy)
            {
                var clip = anim.GetClipById(s.Clip);
                if (clip != null)
                {
                    int frame = Mathf.Clamp(s.Frame, 0, Mathf.Max(0, clip.frames.Length - 1));
                    if (anim.CurrentClip != clip || !anim.Playing) anim.PlayFromFrame(clip, frame);
                    else if (Mathf.Abs(anim.CurrentFrame - frame) > 1) anim.SetFrame(frame);
                }
            }
            else
            {
                if (anim != null && anim.Playing) anim.Stop();
                if (sprite != null && s.Sprite >= 0 && sprite.spriteId != s.Sprite && sprite.Collection != null && s.Sprite < sprite.Collection.spriteDefinitions.Length)
                    sprite.SetSprite(s.Sprite);
            }
            if (sprite != null) { if (!((Color32)sprite.color).Equals(s.Color)) sprite.color = s.Color; }
            else if (rend is SpriteRenderer sr && !((Color32)sr.color).Equals(s.Color)) sr.color = s.Color;
            var ps = tr.GetComponent<ParticleSystem>();
            if (ps != null && (s.Flags & FHasPs) != 0)
            {
                bool emit = (s.Flags & FEmit) != 0;
                if (emit && !ps.isEmitting) ps.Play(false);
                else if (!emit && ps.isEmitting) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            if (collider && (s.Flags & FHasCol) != 0)
            {
                var col = tr.GetComponent<Collider2D>();
                if (col != null) col.enabled = (s.Flags & FCol) != 0;
            }
        }
    }
}
