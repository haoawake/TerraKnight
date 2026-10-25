using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Deep Focus (charm 34) carries the focus mode, switched by its skill (no cooldown): on, it stays on (the save keeps it)
    /// until the skill again switches it off. The whole scene goes grey, whatever of the enemies can hurt the player (their
    /// bodies, attacks and shots: the game's DamageHero of an enemy's hit) is drawn red over the grey, and the player blue,
    /// with everything of his that strikes (the nail's and the spells' slashes and blasts, the Terraria weapons' shots,
    /// swings, whips and minions). The grey comes in over half a second and goes over 0.7 s, as the colours do; a death or
    /// the menu ends it at once, and back in play it comes on again while switched on. The hazards (spikes, thorns, acid,
    /// lava, the traps: the game's DamageHero of hazard types 2-4) are red too: their own picture where they have one, a red
    /// box over where they hurt where they don't.
    ///   The grey is the game's own colour correction (the main camera's ColorCorrectionCurves) taken down to no saturation,
    /// set just before each render (after the game's own writers) and given back as it was. The colours are copies of those
    /// renderers (half-clear silhouettes, Unity's built-in GUI/Text shader, so the grey picture shows through them) on a layer
    /// no camera of the game draws, drawn by a camera of the mod's own right after the main one's picture is made grey. A
    /// hurting thing with nothing to copy (a shot of particles) gets a red glow there instead; the Terraria weapons' effects
    /// (FxBatch's weapon draws) get their blue copies from FxBatch itself.
    /// </summary>
    internal static partial class CharmEffects
    {
        const float FocusIn = 0.5f, FocusOut = 0.7f, FocusReach = 40f;
        /// <summary>The overlay's layer: no camera of the game draws it.</summary>
        public const int FocusLayer = 29;
        const string FocusKey = "charm.focus.on.v1";
        static readonly Color FocusEnemy = new Color(1f, 0.2f, 0.16f, 0.62f);
        /// <summary>The player's blue (FxBatch's copies of the weapon effects use it too).</summary>
        internal static readonly Color FocusSelf = new Color(0.32f, 0.62f, 1f, 0.5f);
        static float focusStart = -1f, focusUntil = -1f, focusK, focusScanAt;
        static Camera focusCam;
        static float baseSaturation = 1f, setSaturation = float.NaN;
        static bool focusHooked, focusShown;
        static Material spriteSilhouette;
        static readonly Dictionary<Texture, Material> meshSilhouettes = new Dictionary<Texture, Material>(), selfSilhouettes = new Dictionary<Texture, Material>();

        sealed class Copy
        {
            public Renderer Src, Dst;
            public SpriteRenderer SrcSprite, DstSprite;
            /// <summary>A mesh copy's meshes and the texture its silhouette is of (an animation may move to another atlas page).</summary>
            public MeshFilter SrcFilter, DstFilter;
            public Texture Tex;
            public bool Self;
        }
        static readonly List<Copy> copies = new List<Copy>();
        static readonly HashSet<Renderer> copied = new HashSet<Renderer>();
        /// <summary>Hurting things near with nothing of theirs to copy: a red glow drawn on each.</summary>
        static readonly List<Collider2D> focusMarks = new List<Collider2D>();
        /// <summary>The hazards near (spikes, thorns, acid, lava, traps): a red box over each.</summary>
        static readonly List<Collider2D> focusHazards = new List<Collider2D>();
        public static int DevFocusHazards => focusHazards.Count;

        public static bool Focusing => focusUntil > 0f;
        public static float DevFocusK => focusK;
        public static int DevFocusCopies => copies.Count;
        public static int DevFocusEnemyCopies { get { int n = 0; foreach (var c in copies) if (!c.Self) n++; return n; } }
        public static int DevFocusMarks => focusMarks.Count;
        public static float DevSaturation => GameCameras.instance != null && GameCameras.instance.colorCorrectionCurves != null ? GameCameras.instance.colorCorrectionCurves.saturation : -1f;
        /// <summary>Dev: whether this renderer has a copy now (and whether it is the player's blue one).</summary>
        public static bool DevCopied(Renderer r, out bool self)
        {
            foreach (var c in copies) if (c.Src == r) { self = c.Self; return true; }
            self = false;
            return false;
        }

        /// <summary>Switched on (kept in the save): the mode runs while the charm is worn and the player is in play.</summary>
        static bool FocusOn
        {
            get => Mod != null && Mod.GrantedAbilities.Contains(FocusKey);
            set
            {
                if (Mod == null) return;
                if (value) Mod.GrantedAbilities.Add(FocusKey);
                else Mod.GrantedAbilities.Remove(FocusKey);
            }
        }

        public static bool DevFocusOn => FocusOn;

        /// <summary>The skill: on, or off again.</summary>
        static void ToggleFocus(HeroController hero)
        {
            FocusOn = !FocusOn;
            Mod.PlaySound(FocusOn ? "MaxMana" : "Menu_Tick", 0.5f);
            Plugin.Log.LogInfo($"[护符] 深度聚集：专心模式{(FocusOn ? "开启" : "关闭")}");
        }

        /// <summary>The overlay's silhouette for sprites (FxBatch's blue copies share it).</summary>
        internal static Material FocusSpriteMaterial => spriteSilhouette != null ? spriteSilhouette : spriteSilhouette = NewSilhouette(null);

        static void StartFocus(HeroController hero)
        {
            if (!Hook()) return;
            focusStart = Time.time;
            focusUntil = float.PositiveInfinity;
            focusScanAt = 0f;
        }

        /// <summary>The two cameras' hooks and the overlay camera, once.</summary>
        static bool Hook()
        {
            var main = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (main == null) return false;
            if (!focusHooked)
            {
                Camera.onPreCull += BeforeCull;
                focusHooked = true;
            }
            if (focusCam == null)
            {
                var go = new GameObject("TerraKnight Focus Camera");
                Object.DontDestroyOnLoad(go);
                focusCam = go.AddComponent<Camera>();
                focusCam.enabled = false;
            }
            focusCam.CopyFrom(main);
            focusCam.clearFlags = CameraClearFlags.Depth;
            focusCam.cullingMask = 1 << FocusLayer;
            focusCam.depth = main.depth + 0.01f;
            focusCam.useOcclusionCulling = false;
            focusCam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            // Nothing of ours drawn twice (grey) by the game's cameras.
            foreach (var c in Camera.allCameras)
                if (c != focusCam && (c.cullingMask & (1 << FocusLayer)) != 0) c.cullingMask &= ~(1 << FocusLayer);
            focusCam.enabled = true;
            return true;
        }

        static void TickFocus(HeroController hero)
        {
            // Switched on, its state in the buff bar without a time (made again only as it changes or goes missing).
            bool shown = FocusOn && On(DeepFocus);
            if (shown && (!focusShown || !Player.PlayerBuffs.Has(CharmSkills.ActiveBase + DeepFocus)))
            {
                focusShown = true;
                CharmSkills.Hold(DeepFocus, Lang.T("专心"), Lang.T("看清危险：再按一次护符技关闭"));
            }
            else if (!shown && focusShown)
            {
                focusShown = false;
                CharmSkills.EndActive(DeepFocus);
            }
            var gm = GameManager.UnsafeInstance;
            // Off at once with the charm off, a death, the menu: nothing left grey.
            if (!On(DeepFocus) || hero == null || hero.cState.dead || !GameHooks.IsGameplay(gm)) { if (Focusing) StopFocus(); return; }
            // Switched on: on again in play (a death, the menu, the charm put back on).
            if (FocusOn && !Focusing) StartFocus(hero);
            if (!Focusing) return;
            float t = Time.time;
            // Switched off: fading out.
            if (!FocusOn && focusUntil > t + FocusOut) focusUntil = t + FocusOut;
            if (t >= focusUntil) { StopFocus(); return; }
            focusK = Mathf.Min(Smooth(Mathf.Clamp01((t - focusStart) / FocusIn)), Smooth(Mathf.Clamp01((focusUntil - t) / FocusOut)));
            FxBatch.FocusTint = focusK;
            if (t >= focusScanAt)
            {
                focusScanAt = t + 0.2f;
                Scan(hero);
            }
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        static void StopFocus()
        {
            focusUntil = -1f;
            focusK = 0f;
            FxBatch.FocusTint = 0f;
            var cc = GameCameras.instance != null ? GameCameras.instance.colorCorrectionCurves : null;
            // The saturation given back, unless the game has set its own meanwhile.
            if (cc != null && !float.IsNaN(setSaturation) && Mathf.Approximately(cc.saturation, setSaturation)) cc.saturation = baseSaturation;
            setSaturation = float.NaN;
            foreach (var c in copies) if (c.Dst != null) Object.Destroy(c.Dst.gameObject);
            copies.Clear();
            copied.Clear();
            focusMarks.Clear();
            focusHazards.Clear();
            if (focusCam != null) focusCam.enabled = false;
        }

        /// <summary>Just before a camera culls: the main one's grey set, the overlay's view matched and the copies brought up to date.</summary>
        static void BeforeCull(Camera cam)
        {
            if (!Focusing) return;
            var gc = GameCameras.instance;
            var main = gc != null ? gc.mainCamera : null;
            if (main == null) return;
            // Out of play (the menu, a quit): the frame's clock no longer runs to end it, so it ends here, nothing left grey.
            if (cam == main && (HeroController.SilentInstance == null || GameManager.UnsafeInstance == null || GameManager.UnsafeInstance.gameState == GlobalEnums.GameState.MAIN_MENU
                || !GameHooks.GameplayScene(GameManager.UnsafeInstance)))
            {
                StopFocus();
                return;
            }
            if (cam == main)
            {
                var cc = gc.colorCorrectionCurves;
                if (cc == null) return;
                // The game's own value (anything other than what was set here last) is the one to come back to.
                if (float.IsNaN(setSaturation) || !Mathf.Approximately(cc.saturation, setSaturation)) baseSaturation = cc.saturation;
                cc.saturation = Mathf.Lerp(baseSaturation, 0f, focusK);
                setSaturation = cc.saturation;
                return;
            }
            if (cam != focusCam) return;
            cam.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            cam.fieldOfView = main.fieldOfView;
            cam.projectionMatrix = main.projectionMatrix;
            cam.targetTexture = main.targetTexture;
            Sync();
        }

        static readonly List<Renderer> selfRenderers = new List<Renderer>(), heroRenderers = new List<Renderer>(), childRenderers = new List<Renderer>(), found = new List<Renderer>();
        static readonly List<Collider2D> near = new List<Collider2D>();
        static readonly ContactFilter2D nearFilter = new ContactFilter2D { useTriggers = true };

        /// <summary>
        /// What to colour now, within reach of the player: every collider that hurts him (an enemy's DamageHero: its own
        /// picture, else its shot's or its enemy's), and his own: the Terraria body, the Knight's slashes, and what of his
        /// strikes enemies (the game's DamageEnemies: the spells' blasts).
        /// </summary>
        static void Scan(HeroController hero)
        {
            var at = (Vector2)hero.transform.position;
            focusMarks.Clear();
            focusHazards.Clear();
            Physics2D.OverlapCircle(at, FocusReach, nearFilter, near);
            foreach (var col in near)
            {
                if (col == null || !col.enabled) continue;
                var dh = col.GetComponent<DamageHero>();
                if (dh != null)
                {
                    if (dh.damageDealt <= 0 || !dh.isActiveAndEnabled) continue;
                    if (dh.hazardType == 1)
                    {
                        if (Drawables(col.gameObject)) foreach (var r in found) Highlight(r, false);
                        else focusMarks.Add(col);
                    }
                    else if (dh.hazardType >= 2 && dh.hazardType <= 4)
                    {
                        // A hazard: its own picture red (a saw), and a red box over where it hurts (the spikes' and the acid's
                        // pictures are the scenery's, apart from them).
                        found.Clear();
                        if (AddDrawable(col.gameObject)) foreach (var r in found) Highlight(r, false);
                        focusHazards.Add(col);
                    }
                    continue;
                }
                var de = col.GetComponent<DamageEnemies>();
                if (de == null || !de.isActiveAndEnabled || col.GetComponentInParent<HealthManager>() != null) continue;
                if (Drawables(col.gameObject)) foreach (var r in found) Highlight(r, true);
            }
            var look = Mod != null ? Mod.PlayerLook : null;
            if (look != null)
            {
                look.GetComponentsInChildren(true, selfRenderers);
                foreach (var r in selfRenderers) if (r is SpriteRenderer) Highlight(r, true);
            }
            // The Knight's slashes and arts (the pictures NailArts puts in front), the Dream Nail's.
            hero.GetComponentsInChildren(true, heroRenderers);
            foreach (var r in heroRenderers) if (NailArts.IsAttackPicture(r, hero.transform)) Highlight(r, true);
        }

        /// <summary>What shows a hurting thing (into <see cref="found"/>): its own picture, else its children's, else its holder's.</summary>
        static bool Drawables(GameObject go)
        {
            found.Clear();
            if (AddDrawable(go)) return true;
            go.GetComponentsInChildren(false, childRenderers);
            foreach (var r in childRenderers)
                if (r.gameObject != go && Copyable(r) && found.Count < 6) found.Add(r);
            if (found.Count > 0) return true;
            // A hitbox of its own under the thing it belongs to: the enemy's body, the shot's picture.
            var t = go.transform.parent;
            for (int k = 0; k < 3 && t != null; k++, t = t.parent)
            {
                if (AddDrawable(t.gameObject)) return true;
                if (t.GetComponent<HealthManager>() != null) break;
            }
            var hm = go.GetComponentInParent<HealthManager>();
            return hm != null && AddDrawable(hm.gameObject);
        }

        static bool AddDrawable(GameObject go)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null && Copyable(sr)) { found.Add(sr); return true; }
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && Copyable(mr)) { found.Add(mr); return true; }
            return false;
        }

        static bool Copyable(Renderer r) =>
            r != null && r.gameObject.layer != FocusLayer && (r is SpriteRenderer || r is MeshRenderer && r.GetComponent<MeshFilter>() != null && r.sharedMaterial != null && r.sharedMaterial.mainTexture != null);

        static void Highlight(Renderer src, bool self)
        {
            // Only what can be copied (a sprite, a mesh with its texture): the Knight's particle effects are left as they are.
            if (src == null || !Copyable(src) || !copied.Add(src)) return;
            var go = new GameObject("Focus copy") { layer = FocusLayer };
            go.transform.SetParent(src.transform, false);
            var copy = new Copy { Src = src, Self = self };
            if (src is SpriteRenderer ssr)
            {
                var dsr = go.AddComponent<SpriteRenderer>();
                dsr.sharedMaterial = FocusSpriteMaterial;
                copy.SrcSprite = ssr;
                copy.DstSprite = dsr;
                copy.Dst = dsr;
            }
            else
            {
                var tex = src.sharedMaterial != null ? src.sharedMaterial.mainTexture : null;
                if (tex == null) { Object.Destroy(go); return; }
                copy.SrcFilter = src.GetComponent<MeshFilter>();
                copy.DstFilter = go.AddComponent<MeshFilter>();
                copy.DstFilter.sharedMesh = copy.SrcFilter.sharedMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Silhouette(tex, self);
                copy.Tex = tex;
                copy.Dst = mr;
            }
            copy.Dst.sortingLayerID = src.sortingLayerID;
            copy.Dst.sortingOrder = src.sortingOrder;
            copies.Add(copy);
        }

        /// <summary>The half-clear silhouette of a texture, red or blue (one material each, coloured in Sync).</summary>
        static Material Silhouette(Texture tex, bool self)
        {
            var mats = self ? selfSilhouettes : meshSilhouettes;
            if (!mats.TryGetValue(tex, out var mat) || mat == null) mats[tex] = mat = NewSilhouette(tex);
            return mat;
        }

        static Material NewSilhouette(Texture tex)
        {
            var shader = Shader.Find("GUI/Text Shader") ?? Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = "TerraKnight focus silhouette" };
            if (tex != null) m.mainTexture = tex;
            return m;
        }

        /// <summary>Each copy as its renderer is now: shown or not, the sprite and its turn, the colour at the fade's strength.</summary>
        static void Sync()
        {
            var enemy = new Color(FocusEnemy.r, FocusEnemy.g, FocusEnemy.b, FocusEnemy.a * focusK);
            var self = new Color(FocusSelf.r, FocusSelf.g, FocusSelf.b, FocusSelf.a * focusK);
            foreach (var m in meshSilhouettes.Values) if (m != null) m.color = enemy;
            foreach (var m in selfSilhouettes.Values) if (m != null) m.color = self;
            for (int i = copies.Count - 1; i >= 0; i--)
            {
                var c = copies[i];
                if (c.Src == null || c.Dst == null)
                {
                    if (c.Dst != null) Object.Destroy(c.Dst.gameObject);
                    copies.RemoveAt(i);
                    continue;
                }
                bool shown = c.Src.enabled && c.Src.gameObject.activeInHierarchy && !c.Src.forceRenderingOff;
                if (c.Dst.enabled != shown) c.Dst.enabled = shown;
                if (!shown) continue;
                if (c.DstSprite != null)
                {
                    c.DstSprite.sprite = c.SrcSprite.sprite;
                    c.DstSprite.flipX = c.SrcSprite.flipX;
                    c.DstSprite.flipY = c.SrcSprite.flipY;
                    var tint = c.Self ? FocusSelf : FocusEnemy;
                    c.DstSprite.color = new Color(tint.r, tint.g, tint.b, tint.a * focusK * c.SrcSprite.color.a);
                }
                // A mesh's copy: its mesh and its atlas page as they are now (an attack's frames from another page than the
                // walk's: the Husk Miner's swing drew the walk's picture with the swing's corners).
                if (c.DstFilter != null && c.SrcFilter != null)
                {
                    if (c.DstFilter.sharedMesh != c.SrcFilter.sharedMesh) c.DstFilter.sharedMesh = c.SrcFilter.sharedMesh;
                    var tex = c.Src.sharedMaterial != null ? c.Src.sharedMaterial.mainTexture : null;
                    if (tex != null && tex != c.Tex)
                    {
                        c.Tex = tex;
                        c.Dst.sharedMaterial = Silhouette(tex, c.Self);
                        DevFocusRetextured++;
                    }
                }
                if (c.Dst.sortingOrder != c.Src.sortingOrder) c.Dst.sortingOrder = c.Src.sortingOrder;
            }
        }

        public static int DevFocusRetextured;

        /// <summary>In the world: a red glow on each hurting thing that has no picture to copy (on the overlay, so not grey).</summary>
        static Sprite square;
        /// <summary>A plain white square, 4 px across (the hazards' boxes).</summary>
        static Sprite Square => square != null ? square : square = Make(4, (x, y) => 1f);

        static void DrawFocusWorld(FxBatch fx)
        {
            if (!Focusing || focusMarks.Count == 0 && focusHazards.Count == 0) return;
            bool top = fx.OnTop, focus = fx.FocusDraw;
            fx.OnTop = true;
            fx.FocusDraw = true;
            try
            {
                // The hazards: a half-clear red box over where each hurts, a brighter line along its top.
                for (int i = focusHazards.Count - 1; i >= 0; i--)
                {
                    var col = focusHazards[i];
                    if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) { focusHazards.RemoveAt(i); continue; }
                    var b = col.bounds;
                    float w = Mathf.Max(6f, b.size.x / TerraParticles.Px), h = Mathf.Max(6f, b.size.y / TerraParticles.Px);
                    var px = TerraParticles.ToPx(b.center);
                    float a = 0.34f * focusK;
                    fx.Draw(Square, px, new Color(FocusEnemy.r * a, FocusEnemy.g * a, FocusEnemy.b * a, a), 0f, new Vector2(w / 4f, h / 4f));
                    fx.Draw(Square, px + new Vector2(0f, -h / 2f), Glow(FocusEnemy, 0.5f * focusK), 0f, new Vector2(w / 4f, 0.5f));
                }
                for (int i = focusMarks.Count - 1; i >= 0; i--)
                {
                    var col = focusMarks[i];
                    if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) { focusMarks.RemoveAt(i); continue; }
                    var b = col.bounds;
                    float size = Mathf.Clamp(Mathf.Max(b.size.x, b.size.y) / TerraParticles.Px, 12f, 160f);
                    var px = TerraParticles.ToPx(b.center);
                    fx.Draw(Dot, px, Glow(FocusEnemy, 0.75f * focusK), 0f, size * 1.4f / 32f);
                    fx.Draw(Dot, px, Glow(FocusEnemy, 0.9f * focusK), 0f, size * 0.7f / 32f);
                }
            }
            finally { fx.OnTop = top; fx.FocusDraw = focus; }
        }
    }
}
