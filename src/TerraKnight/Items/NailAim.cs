using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The nail and the Dream Nail swung at the mouse, any way round (the Terraria settings' switch, off by default). The
    /// Knight faces the cursor's side and the game's own attack is the one for where the cursor is: up within 45° of
    /// straight up, down within 45° of straight down while in the air, level otherwise (HeroController.DoAttack's vertical
    /// input); so the recoil off what is struck, the pogo and the wall's thunk stay the game's four ways, read from the
    /// slash FSM's direction (NailSlash.OnTriggerEnter2D: 0, 90, 180, 270). The slash itself, its picture and its hitbox,
    /// is turned about the Knight from that way to the cursor's exact angle; the Dream Nail's slash alike (it has no recoil),
    /// its picture and its hitbox both; and the nail arts' Great Slash and Dash Slash as they come out (the Cyclone Slash
    /// goes all round already).
    /// </summary>
    [HarmonyPatch]
    internal static class NailAim
    {
        public static ConfigEntry<bool> Setting;
        public static bool On => Setting != null && Setting.Value;

        public static void Bind(ConfigFile cfg) =>
            Setting = cfg.Bind("Gameplay", "NailAimAtMouse", false,
                "骨钉和梦之钉朝鼠标方向劈砍（360 度）；打中东西时自己的后坐力、下劈弹跳仍按原版的上下左右。\nThe nail and the Dream Nail swing toward the mouse, any angle; the recoil off what they strike and the pogo stay the game's four ways.");

        public static void Set(bool on)
        {
            if (Setting != null) Setting.Value = on;
        }

        static readonly AccessTools.FieldRef<HeroController, float> verticalInput = AccessTools.FieldRefAccess<HeroController, float>("vertical_input");
        static readonly AccessTools.FieldRef<NailSlash, float> slashAngle = AccessTools.FieldRefAccess<NailSlash, float>("slashAngle");

        /// <summary>The angle (degrees: 0 right, 90 up) the slash starting now is turned to; NaN for none.</summary>
        static float pending = float.NaN;

        /// <summary>Each turned slash's own place under the Knight, put back before it is turned again.</summary>
        static readonly Dictionary<Transform, (Vector3 Pos, Quaternion Rot)> home = new Dictionary<Transform, (Vector3, Quaternion)>();

        public static float DevLastAngle = float.NaN, DevLastTurn;
        public static string DevLastKind = "";
        /// <summary>Dev: the cursor's world point for a test (null: the real mouse).</summary>
        public static Vector2? DevAim;

        static bool Aim(HeroController hero, out float angle)
        {
            angle = 0f;
            Vector2 at;
            if (DevAim.HasValue) at = DevAim.Value;
            else if (!TerraMod.MouseWorld(out at)) return false;
            Vector2 from = hero.GetComponent<BoxCollider2D>().bounds.center;
            var d = at - from;
            if (d.sqrMagnitude < 0.0001f) return false;
            angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            return true;
        }

        static void Face(HeroController hero, float angle)
        {
            bool right = Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) <= 90f;
            if (right && !hero.cState.facingRight) hero.FaceRight();
            else if (!right && hero.cState.facingRight) hero.FaceLeft();
        }

        /// <summary>
        /// Just before the nail item calls DoAttack: facing the cursor, the vertical input set for the game's way nearest it
        /// (down only in the air, as DoAttack has it), and the cursor's angle kept for the slash about to start.
        /// </summary>
        public static void BeforeAttack(HeroController hero)
        {
            pending = float.NaN;
            if (!On || hero == null || hero.cState.wallSliding || !Aim(hero, out float angle)) return;
            Face(hero, angle);
            float fromUp = Mathf.Abs(Mathf.DeltaAngle(angle, 90f)), fromDown = Mathf.Abs(Mathf.DeltaAngle(angle, -90f));
            verticalInput(hero) = fromUp <= 45f ? 1f : fromDown <= 45f && !hero.cState.onGround ? -1f : 0f;
            pending = angle;
            DevLastAngle = angle;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(NailSlash), nameof(NailSlash.StartSlash))]
        static void Turned(NailSlash __instance)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || !__instance.transform.IsChildOf(hero.transform)) return;
            var t = __instance.transform;
            Restore(t);
            if (float.IsNaN(pending)) return;
            float baseAngle = slashAngle(__instance);
            DevLastKind = baseAngle == 90f ? "上劈" : baseAngle == 270f ? "下劈" : "横劈";
            Turn(t, hero, Mathf.DeltaAngle(baseAngle, pending));
            pending = float.NaN;
        }

        static void Restore(Transform t)
        {
            if (home.TryGetValue(t, out var h))
            {
                t.localPosition = h.Pos;
                t.localRotation = h.Rot;
            }
            else home[t] = (t.localPosition, t.localRotation);
        }

        /// <summary>The slash turned about the Knight's middle by so many degrees as seen on screen (the Knight is mirrored facing one way).</summary>
        static void Turn(Transform t, HeroController hero, float degrees)
        {
            var parent = t.parent;
            if (parent == null) return;
            float mirror = Mathf.Sign(parent.lossyScale.x) * Mathf.Sign(parent.lossyScale.y);
            var spin = Quaternion.Euler(0f, 0f, degrees * mirror);
            Vector3 pivot = parent.InverseTransformPoint(hero.GetComponent<BoxCollider2D>().bounds.center);
            pivot.z = t.localPosition.z;
            t.localPosition = pivot + spin * (t.localPosition - pivot);
            t.localRotation = spin * t.localRotation;
            DevLastTurn = degrees;
        }

        // ------------------------------------------------------------------ the Dream Nail

        static Transform dreamSlash;
        static HeroController dreamOf;
        static string lastDreamState;
        static float dreamAngle = float.NaN;

        /// <summary>The Knight's Dream Nail slash: his child whose FSM sends the dream impact (SendDreamImpact).</summary>
        static Transform DreamSlash(HeroController hero)
        {
            if (dreamOf == hero && dreamSlash != null) return dreamSlash;
            dreamOf = hero;
            dreamSlash = null;
            foreach (var f in hero.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (f.gameObject == hero.gameObject) continue;
                foreach (var s in f.FsmStates)
                {
                    foreach (var a in s.Actions)
                        if (a is SendDreamImpact) { dreamSlash = f.transform; break; }
                    if (dreamSlash != null) break;
                }
                if (dreamSlash != null) break;
            }
            if (dreamSlash != null) Plugin.Log.LogInfo($"[骨钉朝向] 梦之钉的挥砍：{dreamSlash.name}");
            return dreamSlash;
        }

        public static string DevDreamSlash => dreamSlash != null ? dreamSlash.name : "";

        static Transform dreamPicture;

        /// <summary>The Dream Nail's slash as drawn: the machine's "Slash" (Dream Effects/Slash), not always the one that reads.</summary>
        static Transform DreamPicture(HeroController hero, PlayMakerFSM dream)
        {
            if (dreamPicture != null && dreamPicture.IsChildOf(hero.transform)) return dreamPicture;
            var v = dream.FsmVariables.FindFsmGameObject("Slash");
            dreamPicture = v != null && v.Value != null ? v.Value.transform : null;
            if (dreamPicture != null) Plugin.Log.LogInfo($"[骨钉朝向] 梦之钉挥砍的贴图：{dreamPicture.name}（读取的是 {(dreamSlash != null ? dreamSlash.name : "?")}）");
            return dreamPicture;
        }

        /// <summary>Dev: the Dream Nail's drawn slash, for a test.</summary>
        public static Transform DevDreamPicture => dreamPicture;

        // ------------------------------------------------------------------ the nail arts

        static readonly List<Transform> arts = new List<Transform>();
        static readonly HashSet<Transform> artShown = new HashSet<Transform>();
        static HeroController artsOf;
        /// <summary>The Knight's effects drawn with an art (its white flash, SD Sharp Flash), turned with it while it lasts.</summary>
        static readonly List<Transform> artFlashes = new List<Transform>();
        static readonly HashSet<Transform> flashTurned = new HashSet<Transform>();
        static float artTurnNow = float.NaN;

        /// <summary>Dev: the last nail art turned, and by how much.</summary>
        public static string DevLastArt = "";
        public static float DevLastArtTurn;

        /// <summary>
        /// Each frame with the nail: the Great Slash and the Dash Slash turned to the cursor as they come out (facing its
        /// side first), put back once they're done. Not while the setting is off.
        /// </summary>
        public static void TickArts(HeroController hero)
        {
            if (hero == null) return;
            if (artsOf != hero)
            {
                artsOf = hero;
                arts.Clear();
                artShown.Clear();
                artFlashes.Clear();
                flashTurned.Clear();
                foreach (var t in hero.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Great Slash" || t.name == "Dash Slash") arts.Add(t);
                    if (t.name == "SD Sharp Flash") artFlashes.Add(t);
                }
            }
            foreach (var t in arts)
            {
                if (t == null) continue;
                bool shown = t.gameObject.activeInHierarchy;
                if (shown && artShown.Add(t))
                {
                    Restore(t);
                    float way = hero.cState.facingRight ? 0f : 180f;
                    if (On && Aim(hero, out float angle))
                    {
                        Face(hero, angle);
                        float turn = Mathf.DeltaAngle(hero.cState.facingRight ? 0f : 180f, angle);
                        Turn(t, hero, turn);
                        artTurnNow = turn;
                        DevLastArt = t.name;
                        DevLastArtTurn = turn;
                        DevLastAngle = angle;
                        way = (hero.cState.facingRight ? 0f : 180f) + turn;
                    }
                    // Quick Slash's echo and Grubberfly's beam for the art, its way.
                    CharmEffects.OnArtShown(hero, t, way);
                }
                else if (!shown && artShown.Remove(t)) Restore(t);
            }
            // The art's white flash across the Knight goes its way too, while the art lasts.
            if (artShown.Count > 0 && !float.IsNaN(artTurnNow))
            {
                foreach (var f in artFlashes)
                    if (f != null && f.gameObject.activeInHierarchy && flashTurned.Add(f))
                    {
                        Restore(f);
                        Turn(f, hero, artTurnNow);
                    }
            }
            else
            {
                foreach (var f in flashTurned) if (f != null) Restore(f);
                flashTurned.Clear();
                artTurnNow = float.NaN;
            }
        }

        /// <summary>
        /// Each frame with the Dream Nail in hand: facing the cursor while it charges, and its slash turned to the cursor's
        /// angle as it comes (the Dream Nail FSM's "Slash"), put back once it is over.
        /// </summary>
        public static void TickDream(HeroController hero, PlayMakerFSM dream)
        {
            if (hero == null || dream == null) return;
            string state = dream.ActiveStateName;
            if (state == lastDreamState) return;
            lastDreamState = state;
            var slash = DreamSlash(hero);
            var picture = DreamPicture(hero, dream);
            if (slash == null && picture == null) return;
            if (state == "Slash Antic" || state == "Slash")
            {
                if (!On || !Aim(hero, out float angle)) return;
                if (state == "Slash Antic" || float.IsNaN(dreamAngle))
                {
                    Face(hero, angle);
                    dreamAngle = angle;
                }
                // Its picture turned from the antic on (it shows the swing coming), the hitbox as it strikes.
                float turn = Mathf.DeltaAngle(hero.cState.facingRight ? 0f : 180f, dreamAngle);
                if (picture != null && picture != slash && (slash == null || !picture.IsChildOf(slash)))
                {
                    Restore(picture);
                    Turn(picture, hero, turn);
                }
                if (state == "Slash" && slash != null && (picture == null || !slash.IsChildOf(picture)))
                {
                    Restore(slash);
                    Turn(slash, hero, turn);
                }
                if (state == "Slash")
                {
                    DevLastAngle = dreamAngle;
                    DevLastKind = "梦之钉";
                    dreamAngle = float.NaN;
                }
            }
            else
            {
                if (slash != null) Restore(slash);
                if (picture != null) Restore(picture);
                dreamAngle = float.NaN;
            }
        }
    }
}
