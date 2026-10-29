using System.Collections.Generic;
using System.IO;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>How the local player was drawn this frame (TerraMod.PosePlayer), to be drawn the same way in the others' games.</summary>
    internal struct PoseRecord
    {
        public bool Shown;
        /// <summary>The drawn feet (world), the velocity it animates by (world units a second, y up).</summary>
        public Vector2 Feet, Velocity;
        public bool Grounded, Upside, Seated, WingAir, Glow;
        public float Facing, Swing, ItemScale, Opacity;
        public ItemType Item;
        public int BodyOverride, LegOverride, Shadows, Wing, WingFrame, Mount;
        public TerraPlayerSprite.Equips Equips;
        public TerraPlayerSprite.Hold Hold;
        /// <summary>The hero's box (world).</summary>
        public Bounds Box;
    }

    /// <summary>
    /// Terraria's PlayerControls (13) and the parts of SyncPlayer and SyncEquipment it needs, in
    /// one: where a player is (their scene and a point in its own frame), how they move and face,
    /// what they hold and how (the swing, the held-out angle, a hold style), what they wear, their
    /// wings' frame, their life; and the blade's box while a swing can hurt (for PvP).
    /// </summary>
    internal struct PlayerState
    {
        public string Scene;
        public Vector2 Feet, Vel, BoxCenter, BoxSize;
        public bool Shown, Grounded, FacingRight, Upside, Seated, Dead, WingAir, Glow;
        public float Swing, ItemScale, Opacity;
        public ItemType Item;
        public int Body, Leg, Shadows, Wing, WingFrame, Mount;
        public byte HoldFlags;
        public float HoldAngle, IdleRot;
        public Vector2 HoldOffset, IdleLoc;
        public int IdleBody;
        public ItemType HoldItem;
        public TerraPlayerSprite.Equips Eq;
        public int Life, LifeMax;
        /// <summary>Player.aggro (EnemyTargets.LocalAggro): how much nearer than they are the enemies take them for.</summary>
        public int Aggro;
        public bool Melee;
        public Rect MeleeBox;
        public int MeleeSerial, MeleeDamage, MeleeCrit;
        public float MeleeKb;
        public ItemType MeleeItem;

        const byte HoldOut = 1, HoldStaff = 2, HoldThrust = 4, HoldIdle = 8, HoldShield = 16;

        public static PlayerState From(PoseRecord p, string scene, TerraMod mod)
        {
            var s = new PlayerState
            {
                Scene = scene ?? "",
                Mount = p.Mount,
                Shown = p.Shown,
                Feet = NetScenes.ToLocal(scene, p.Feet),
                Vel = p.Velocity,
                BoxCenter = NetScenes.ToLocal(scene, p.Box.center),
                BoxSize = p.Box.size,
                Grounded = p.Grounded,
                FacingRight = p.Facing >= 0f,
                Upside = p.Upside,
                Seated = p.Seated,
                WingAir = p.WingAir,
                Glow = p.Glow,
                Swing = p.Swing,
                ItemScale = p.ItemScale,
                Opacity = p.Opacity,
                Item = p.Item,
                Body = p.BodyOverride,
                Leg = p.LegOverride,
                Shadows = p.Shadows,
                Wing = p.Wing,
                WingFrame = p.WingFrame,
                Eq = p.Equips,
                HoldAngle = p.Hold.Angle,
                HoldOffset = p.Hold.Offset,
                IdleLoc = p.Hold.IdleLoc,
                IdleRot = p.Hold.IdleRot,
                IdleBody = p.Hold.IdleBody,
                HoldItem = p.Hold.Item != null ? p.Hold.Item.Type : ItemType.None,
                Life = mod.Stats.Life,
                LifeMax = mod.Stats.LifeMax,
                Aggro = EnemyTargets.LocalAggro(),
            };
            var hero = HeroController.SilentInstance;
            s.Dead = hero != null && hero.cState.dead;
            if (p.Hold.Out) s.HoldFlags |= HoldOut;
            if (p.Hold.Staff) s.HoldFlags |= HoldStaff;
            if (p.Hold.Thrust) s.HoldFlags |= HoldThrust;
            if (p.Hold.Idle) s.HoldFlags |= HoldIdle;
            if (p.Hold.ShieldUp) s.HoldFlags |= HoldShield;
            // A swing that can hurt: its blade's box this frame (MeleeHits.SwingBox), for PvP.
            var item = mod.SwingItem;
            var box = mod.Melee != null ? mod.Melee.SwingBox : default;
            if (item != null && item.IsWeapon && !item.NoMelee && box.width > 0f)
            {
                s.Melee = true;
                s.MeleeBox = new Rect(NetScenes.ToLocal(scene, box.position), box.size);
                s.MeleeSerial = mod.UseSerial;
                s.MeleeDamage = Equipment.Damage(item);
                s.MeleeCrit = Mathf.RoundToInt(Equipment.Crit(item));
                s.MeleeKb = Equipment.Knockback(item);
                s.MeleeItem = item.Type;
            }
            return s;
        }

        public TerraPlayerSprite.Hold Hold()
        {
            var h = new TerraPlayerSprite.Hold
            {
                Out = (HoldFlags & HoldOut) != 0,
                Staff = (HoldFlags & HoldStaff) != 0,
                Thrust = (HoldFlags & HoldThrust) != 0,
                Idle = (HoldFlags & HoldIdle) != 0,
                ShieldUp = (HoldFlags & HoldShield) != 0,
                Angle = HoldAngle,
                Offset = HoldOffset,
                IdleLoc = IdleLoc,
                IdleRot = IdleRot,
                IdleBody = IdleBody,
            };
            if (ItemDb.Has(HoldItem)) h.Item = ItemDb.Get(HoldItem);
            return h;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(Scene);
            W(w, Feet);
            W(w, Vel);
            W(w, BoxCenter);
            W(w, BoxSize);
            int f = (Shown ? 1 : 0) | (Grounded ? 2 : 0) | (FacingRight ? 4 : 0) | (Upside ? 8 : 0) | (Seated ? 16 : 0) | (Dead ? 32 : 0) | (WingAir ? 64 : 0) | (Glow ? 128 : 0);
            w.Write((byte)f);
            w.Write(Swing);
            w.Write(ItemScale);
            w.Write((byte)Mathf.Clamp(Mathf.RoundToInt(Opacity * 255f), 0, 255));
            w.Write((ushort)Item);
            w.Write((sbyte)Body);
            w.Write((sbyte)Leg);
            w.Write((byte)Shadows);
            w.Write((short)Wing);
            w.Write((byte)WingFrame);
            w.Write(HoldFlags);
            w.Write(HoldAngle);
            W(w, HoldOffset);
            W(w, IdleLoc);
            w.Write(IdleRot);
            w.Write((sbyte)IdleBody);
            w.Write((ushort)HoldItem);
            var e = Eq;
            foreach (int v in new[] { e.Shoe, e.Waist, e.Balloon, e.Shield, e.Neck, e.Back, e.HandOn, e.HandOff, e.Head, e.Body, e.Legs, e.Face, e.Front, e.BodyGlow, e.BrainCounter })
                w.Write((short)v);
            int ef = (e.HairUnderHead ? 1 : 0) | (e.HideCapes ? 2 : 0) | (e.IceBarrier ? 4 : 0) | (e.Holy ? 8 : 0) | (e.SocialGhost ? 16 : 0) | (e.BodyGlowArms ? 32 : 0);
            w.Write((byte)ef);
            w.Write((byte)e.Fx);
            w.Write(e.Stealth);
            w.Write(e.StealthKind);
            w.Write(e.HeadGlow ?? "");
            w.Write(e.LegsGlow ?? "");
            // The vanity look (VanityLooks.Dress): its other layers, its flags, its glows' colours.
            foreach (int v in new[] { e.Backpack, e.Tail, e.FaceHead, e.FaceFlower, e.FaceMask, e.Beard, e.BackHead, e.BodyExt, e.Coat, e.CoatExt, e.CoatBack, e.BalloonFront })
                w.Write((short)v);
            int vf = (e.Robe ? 1 : 0) | (e.FaceMaskUnder ? 2 : 0) | (e.HelmetHidden ? 4 : 0) | (e.BeardHair ? 8 : 0) | (e.HeadSkin ? 16 : 0)
                | (e.HideHeadSkin ? 32 : 0) | (e.HideTopSkin ? 64 : 0) | (e.HideBottomSkin ? 128 : 0) | (e.HideShoulders ? 256 : 0) | (e.Halo ? 512 : 0)
                | (e.Horn ? 1024 : 0) | (e.Scowl ? 2048 : 0);
            w.Write((ushort)vf);
            w.Write(e.HeadRegion);
            w.Write(e.BodyRegion);
            w.Write(e.LegsRegion);
            foreach (var c in new[] { e.HeadGlowColor, e.BodyGlowColor, e.ArmGlowColor, e.LegsGlowColor, e.UnderShirt })
            {
                w.Write(c.r);
                w.Write(c.g);
                w.Write(c.b);
                w.Write(c.a);
            }
            w.Write((short)Life);
            w.Write((short)LifeMax);
            w.Write((short)Mathf.Clamp(Aggro, short.MinValue, short.MaxValue));
            w.Write((sbyte)Mount);
            w.Write(Melee);
            if (!Melee) return;
            W(w, MeleeBox.position);
            W(w, MeleeBox.size);
            w.Write(MeleeSerial);
            w.Write(MeleeDamage);
            w.Write((byte)Mathf.Clamp(MeleeCrit, 0, 255));
            w.Write(MeleeKb);
            w.Write((ushort)MeleeItem);
        }

        public static PlayerState Read(BinaryReader r)
        {
            var s = new PlayerState { Scene = r.ReadString(), Feet = V(r), Vel = V(r), BoxCenter = V(r), BoxSize = V(r) };
            int f = r.ReadByte();
            s.Shown = (f & 1) != 0;
            s.Grounded = (f & 2) != 0;
            s.FacingRight = (f & 4) != 0;
            s.Upside = (f & 8) != 0;
            s.Seated = (f & 16) != 0;
            s.Dead = (f & 32) != 0;
            s.WingAir = (f & 64) != 0;
            s.Glow = (f & 128) != 0;
            s.Swing = r.ReadSingle();
            s.ItemScale = r.ReadSingle();
            s.Opacity = r.ReadByte() / 255f;
            s.Item = (ItemType)r.ReadUInt16();
            s.Body = r.ReadSByte();
            s.Leg = r.ReadSByte();
            s.Shadows = r.ReadByte();
            s.Wing = r.ReadInt16();
            s.WingFrame = r.ReadByte();
            s.HoldFlags = r.ReadByte();
            s.HoldAngle = r.ReadSingle();
            s.HoldOffset = V(r);
            s.IdleLoc = V(r);
            s.IdleRot = r.ReadSingle();
            s.IdleBody = r.ReadSByte();
            s.HoldItem = (ItemType)r.ReadUInt16();
            var e = new TerraPlayerSprite.Equips
            {
                Shoe = r.ReadInt16(), Waist = r.ReadInt16(), Balloon = r.ReadInt16(), Shield = r.ReadInt16(), Neck = r.ReadInt16(), Back = r.ReadInt16(),
                HandOn = r.ReadInt16(), HandOff = r.ReadInt16(), Head = r.ReadInt16(), Body = r.ReadInt16(), Legs = r.ReadInt16(), Face = r.ReadInt16(),
                Front = r.ReadInt16(), BodyGlow = r.ReadInt16(), BrainCounter = r.ReadInt16(),
            };
            int ef = r.ReadByte();
            e.HairUnderHead = (ef & 1) != 0;
            e.HideCapes = (ef & 2) != 0;
            e.IceBarrier = (ef & 4) != 0;
            e.Holy = (ef & 8) != 0;
            e.SocialGhost = (ef & 16) != 0;
            e.BodyGlowArms = (ef & 32) != 0;
            e.Fx = (TerraPlayerSprite.ArmorFx)r.ReadByte();
            e.Stealth = r.ReadSingle();
            e.StealthKind = r.ReadByte();
            string hg = r.ReadString(), lg = r.ReadString();
            e.HeadGlow = hg.Length > 0 ? hg : null;
            e.LegsGlow = lg.Length > 0 ? lg : null;
            e.Backpack = r.ReadInt16(); e.Tail = r.ReadInt16(); e.FaceHead = r.ReadInt16(); e.FaceFlower = r.ReadInt16(); e.FaceMask = r.ReadInt16();
            e.Beard = r.ReadInt16(); e.BackHead = r.ReadInt16(); e.BodyExt = r.ReadInt16(); e.Coat = r.ReadInt16(); e.CoatExt = r.ReadInt16();
            e.CoatBack = r.ReadInt16(); e.BalloonFront = r.ReadInt16();
            int vf = r.ReadUInt16();
            e.Robe = (vf & 1) != 0; e.FaceMaskUnder = (vf & 2) != 0; e.HelmetHidden = (vf & 4) != 0; e.BeardHair = (vf & 8) != 0;
            e.HeadSkin = (vf & 16) != 0; e.HideHeadSkin = (vf & 32) != 0; e.HideTopSkin = (vf & 64) != 0; e.HideBottomSkin = (vf & 128) != 0;
            e.HideShoulders = (vf & 256) != 0; e.Halo = (vf & 512) != 0; e.Horn = (vf & 1024) != 0; e.Scowl = (vf & 2048) != 0;
            e.HeadRegion = r.ReadByte(); e.BodyRegion = r.ReadByte(); e.LegsRegion = r.ReadByte();
            Color32 C32() => new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
            e.HeadGlowColor = C32(); e.BodyGlowColor = C32(); e.ArmGlowColor = C32(); e.LegsGlowColor = C32(); e.UnderShirt = C32();
            s.Eq = e;
            s.Life = r.ReadInt16();
            s.LifeMax = r.ReadInt16();
            s.Aggro = r.ReadInt16();
            s.Mount = r.ReadSByte();
            s.Melee = r.ReadBoolean();
            if (s.Melee)
            {
                var pos = V(r);
                s.MeleeBox = new Rect(pos, V(r));
                s.MeleeSerial = r.ReadInt32();
                s.MeleeDamage = r.ReadInt32();
                s.MeleeCrit = r.ReadByte();
                s.MeleeKb = r.ReadSingle();
                s.MeleeItem = (ItemType)r.ReadUInt16();
            }
            return s;
        }

        static void W(BinaryWriter w, Vector2 v)
        {
            w.Write(v.x);
            w.Write(v.y);
        }

        static Vector2 V(BinaryReader r) => new Vector2(r.ReadSingle(), r.ReadSingle());
    }

    /// <summary>
    /// Another player of the game: who they are (name, look, PvP, team), where they are, and their
    /// Terraria character drawn there (a <see cref="TerraPlayerSprite"/> of its own, posed from their
    /// state, the movement smoothed out over a tenth of a second), with their wings, their name over
    /// them and their health bar under them once hurt (DrawInterface_14_EntityHealthBars).
    /// </summary>
    internal sealed class RemotePlayer
    {
        public int Slot;
        public string Name = "?";
        public int Look;
        /// <summary>Their character's look as their game sent it (null from a game that sends none: <see cref="Look"/>'s colouring then).</summary>
        public Player.Appearance Appearance;
        public bool Pvp;
        public byte Team;
        public readonly List<string> In = new List<string>(), Loaded = new List<string>();
        public PlayerState Latest;
        public bool HasState;
        public float LastStateAt;
        /// <summary>Their chat line over their head (Player.chatOverhead) and until when.</summary>
        public string Overhead;
        public float OverheadUntil;
        /// <summary>Where they are drawn now (world) and whether they are in this world at all.</summary>
        public Vector2 Feet, Center;
        public bool Visible;
        /// <summary>The swing (its serial) that last hurt the local player: a swing hits once.</summary>
        public int MeleeHitSerial = -1;

        GameObject root;
        TerraPlayerSprite sprite;
        int spriteLook = -1;
        Player.Appearance spriteAppearance;
        bool wasDead;
        int coverFrames;

        struct Sample
        {
            public float Time;
            public string Scene;
            public Vector2 Feet, Vel;
        }

        readonly List<Sample> samples = new List<Sample>();
        const float Delay = 0.1f;

        public void Receive(PlayerState s)
        {
            Latest = s;
            HasState = true;
            LastStateAt = UnityEngine.Time.unscaledTime;
            if (samples.Count > 0 && samples[samples.Count - 1].Scene != s.Scene) samples.Clear();
            samples.Add(new Sample { Time = UnityEngine.Time.unscaledTime, Scene = s.Scene, Feet = s.Feet, Vel = s.Vel });
            while (samples.Count > 20) samples.RemoveAt(0);
        }

        /// <summary>The smoothed point of their own scene's frame for now (a tenth of a second behind, extrapolated a little past the last).</summary>
        Vector2 SmoothedFeet()
        {
            float t = UnityEngine.Time.unscaledTime - Delay;
            for (int i = samples.Count - 1; i > 0; i--)
            {
                var a = samples[i - 1];
                var b = samples[i];
                if (t < a.Time) continue;
                if (t > b.Time) break;
                float k = b.Time - a.Time > 1e-4f ? (t - a.Time) / (b.Time - a.Time) : 1f;
                return Vector2.Lerp(a.Feet, b.Feet, k);
            }
            var last = samples[samples.Count - 1];
            if (t <= samples[0].Time) return samples[0].Feet;
            // Past the newest: on along its velocity a little, then hold.
            return last.Feet + last.Vel * Mathf.Min(t - last.Time, 0.15f);
        }

        /// <summary>Each frame: posed where they are in this world, or hidden.</summary>
        public void Pose()
        {
            Visible = false;
            var s = Latest;
            bool show = HasState && s.Shown && !s.Dead && samples.Count > 0 && NetScenes.ToWorld(s.Scene, SmoothedFeet(), out Feet);
            if (show) Center = Feet + (s.BoxCenter - s.Feet);
            if (HasState && s.Dead && !wasDead && sprite != null && sprite.gameObject.activeSelf)
            {
                // Player.KillMe in their game: the pieces fly here too.
                sprite.FallApart(s.Vel.x < 0f ? -1 : 1);
                if (Vector2.Distance(Center, HeroPos()) < 30f) TerraMod.Instance?.PlaySound("Player_Killed", 0.6f);
            }
            wasDead = HasState && s.Dead;
            if (!show)
            {
                if (sprite != null && sprite.gameObject.activeSelf) sprite.gameObject.SetActive(false);
                return;
            }
            if (sprite == null)
            {
                root = new GameObject("Terra Net Player " + Slot);
                root.transform.SetParent(TerraMod.Instance.transform, false);
                sprite = TerraPlayerSprite.Create(root.transform);
            }
            var mount=Mounts.Get(s.Mount);
            bool showRider=mount==null || !mount.HideRider;
            if (sprite.gameObject.activeSelf != showRider) sprite.gameObject.SetActive(showRider);
            if (Appearance != null)
            {
                if (spriteAppearance != Appearance)
                {
                    spriteAppearance = Appearance;
                    sprite.SetAppearance(Appearance);
                }
            }
            else if (spriteLook != Look)
            {
                spriteLook = Look;
                sprite.SetPalette(TerraPlayerSprite.Looks[Mathf.Clamp(Look, 0, TerraPlayerSprite.Looks.Length - 1)]);
            }
            Visible = true;
            sprite.Upside = s.Upside;
            Sprite item = ItemDb.Has(s.Item) ? TerraMod.ItemSprite(s.Item) : null;
            sprite.HeldGlow = s.Glow && ItemDb.Has(s.Item) && ItemDb.Get(s.Item).Glow != null ? TerraMod.GlowSprite(ItemDb.Get(s.Item)) : null;
            if (--coverFrames <= 0)
            {
                coverFrames = 4;
                var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
                sprite.OutlineStrength = cam != null && World.TerraWorld.CoveredByScenery(Center, cam.transform.position) ? 1f : 0f;
            }
            float facing = s.FacingRight ? 1f : -1f;
            if (showRider)
            {
                var mountEquips=s.Eq;
                if(mount?.Skates==true)mountEquips.Shoe=mount.Native-30;
                sprite.Pose(Feet, s.Vel, s.Grounded, facing, s.Swing, item, s.ItemScale, s.Opacity, s.Body, mountEquips, s.Shadows, s.Hold(), s.Leg, s.Seated);
                if (!sprite.SpecialPoseActive) sprite.MountAppearance(mount, facing, s.Vel, s.Grounded, s.Eq);
            }
            if(mount!=null)Mounts.Draw(s.Mount,Feet-Vector2.up*Mounts.RiderRise(mount,s.Vel,s.Grounded),s.Vel,s.Grounded,facing,s.Opacity);
            // DrawPlayer_09_Wings, on their wings' frame.
            var wing = TerraWings.Get(s.Wing);
            if (!sprite.SpecialPoseActive && (mount==null || mount.Skates) && wing != null && Effects.FxBatch.Instance != null)
            {
                int keep = TerraWings.Frame;
                TerraWings.Frame = s.WingFrame;
                var centrePx = Effects.TerraParticles.ToPx(sprite.transform.position);
                if (s.Upside) Effects.FxBatch.Instance.MirrorAboutY = centrePx.y;
                TerraWings.Draw(Effects.FxBatch.Instance, wing, centrePx, s.FacingRight ? 1 : -1, s.Opacity, s.WingAir);
                Effects.FxBatch.Instance.MirrorAboutY = null;
                TerraWings.Frame = keep;
            }
        }

        static Vector2 HeroPos()
        {
            var hero = HeroController.SilentInstance;
            return hero != null ? (Vector2)hero.transform.position : Vector2.zero;
        }

        public Color TeamColor => Server.TeamColors[Mathf.Clamp(Team, 0, 5)];

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
            root = null;
            sprite = null;
        }

        static GUIStyle nameStyle, chatStyle;

        /// <summary>Their name over them, a chat line over that, their health bar under them while hurt.</summary>
        public void DrawGui(Camera cam, float ui)
        {
            if (!Visible || cam == null) return;
            var s = Latest;
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontStyle = FontStyle.Bold };
                chatStyle = new GUIStyle(nameStyle) { wordWrap = true };
            }
            nameStyle.fontSize = Mathf.RoundToInt(17 * ui);
            chatStyle.fontSize = Mathf.RoundToInt(16 * ui);
            var top = cam.WorldToScreenPoint(new Vector3(Center.x, Center.y + s.BoxSize.y * 0.5f + 0.25f, 0f));
            if (top.z < 0f) return;
            var at = new Vector2(top.x, Screen.height - top.y);
            var rect = new Rect(at.x - 150f * ui, at.y - 26f * ui, 300f * ui, 24f * ui);
            Outlined(rect, Name, nameStyle, Team == 0 ? Color.white : (Color)TeamColor);
            if (Overhead != null && Time.unscaledTime < OverheadUntil)
                Outlined(new Rect(at.x - 180f * ui, at.y - 92f * ui, 360f * ui, 64f * ui), Overhead, chatStyle, Color.white);
            if (s.Life < s.LifeMax && s.Life > 0)
            {
                var under = new Vector3(Center.x, Center.y - s.BoxSize.y * 0.5f - 10f * Effects.TerraParticles.Px, 0f);
                UI.EnemyHealthBars.SmallBar(cam, under, s.Life, s.LifeMax, 1f);
            }
        }

        internal static void Outlined(Rect r, string text, GUIStyle st, Color c)
        {
            UI.BorderText.Draw(r, text, st, new Color(0f, 0f, 0f, 0.85f * c.a), c, 1.5f * UI.GameView.Scale);
        }
    }
}
