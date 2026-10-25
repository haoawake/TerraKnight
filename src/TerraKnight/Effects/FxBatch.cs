using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Effects
{
    /// <summary>
    /// Terraria-style drawing for effects that are redrawn from scratch every frame (sword slashes,
    /// sparkles, hit flashes): call <see cref="Draw"/> as Terraria calls SpriteBatch.Draw, in
    /// Terraria's space (pixels, y down, clockwise radians), and pooled sprite renderers show it
    /// this frame. Terraria blends with premultiplied alpha, so a draw colour with A = 0 adds
    /// light, one with A = 255 covers, and one in between does a bit of both; that is split into
    /// a covering and an adding renderer (Sprites/Default and Particles/Additive) so it looks the same.
    /// Draws stack in call order, each a hair nearer the camera than the last.
    /// </summary>
    // Last of all each frame, so what the player's pose draws in LateUpdate (the wings) shows the same frame.
    [DefaultExecutionOrder(10000)]
    internal sealed class FxBatch : MonoBehaviour
    {
        const float Px = TerraParticles.Px;
        /// <summary>Behind the player (projectiles), and over him (hit flashes, as ParticleSystem_World_OverPlayers).</summary>
        public const float BehindZ = 0.0047f, OverZ = 0.0025f;
        /// <summary>
        /// Townsfolk: nearer the camera than the furniture they stand beside (World.Stations' 0.0045, the
        /// chests' 0.0040 — later layers sit nearer the camera, so those are reached by going lower) and
        /// short of the player's own 0.0039, so the Knight still passes in front of them.
        /// </summary>
        public const float TownZ = 0.00395f;
        const float Step = 0.0000005f;
        /// <summary>
        /// How much nearer a draw's added part sits than its covering part. The camera is a
        /// perspective one about 38 units away, where Step alone is below what the renderers'
        /// distance sort can tell apart, and the two materials then sorted the black cover over
        /// the light (a near-white, mostly solid colour came out dark grey).
        /// </summary>
        const float AddNearer = 0.0002f;

        public static FxBatch Instance { get; private set; }

        /// <summary>
        /// The sorting order the weapons' effects draw at: over all of the game's scenery (its leaves,
        /// silhouettes and other near things sort by distance at order 0 and would cover them), as
        /// Terraria draws its shots, swings and dust over its tiles and NPCs. The over-player draws go
        /// two above. Below the player's see-through outline (TerraPlayerSprite, 32000).
        /// </summary>
        public const int OnTopOrder = 31000;

        /// <summary>While set, draws are the weapons' effects (<see cref="OnTopOrder"/>); the player's own layers, the townsfolk and the minions stay among the scenery.</summary>
        public bool OnTop;

        /// <summary>
        /// Deep Focus's mode (Items.CharmEffectsFocus), 0..1: each weapon draw (<see cref="WeaponEffect"/>) also gets a blue
        /// half-clear copy on the mode's overlay, which isn't made grey.
        /// </summary>
        public static float FocusTint;

        /// <summary>
        /// While not 1, every draw this much larger round where it is drawn (Shaman Stone on a magic shot drawn its own way,
        /// TerraProjectiles.DrawOne); with <see cref="GrowThick"/> a drawn-out picture (a scale per axis) only thicker, its
        /// length kept (a beam's), a round one (one scale) larger all the same.
        /// </summary>
        public float Grow = 1f;
        public bool GrowThick;
        bool uniformScale;
        public int DevGrowDraws;
        /// <summary>While set, draws go on Deep Focus's overlay as they are (its red marks).</summary>
        public bool FocusDraw;
        readonly List<SpriteRenderer> focus = new List<SpriteRenderer>();
        int focusUsed, focusLast;
        public int DevFocusDraws;
        public bool WeaponEffect;
        internal Color DevLastAlpha, DevLastAdditive;
        internal int DevDrawCount;

        /// <summary>Dev: while set, who drew what wasn't on top (the method that called Draw), counted.</summary>
        public static bool DevTraceLow;
        public static readonly Dictionary<string, int> DevLowCallers = new Dictionary<string, int>();

        static void TraceLow(string kind)
        {
            var frames = new System.Diagnostics.StackTrace(1, false).GetFrames();
            string who = "?";
            if (frames != null)
                foreach (var f in frames)
                {
                    var m = f.GetMethod();
                    if (m == null || m.DeclaringType == typeof(FxBatch)) continue;
                    who = m.DeclaringType?.Name + "." + m.Name;
                    break;
                }
            who = kind + who;
            DevLowCallers[who] = DevLowCallers.TryGetValue(who, out int n) ? n + 1 : 1;
        }

        readonly List<SpriteRenderer> alpha = new List<SpriteRenderer>(), additive = new List<SpriteRenderer>(), glows = new List<SpriteRenderer>();
        int alphaUsed, additiveUsed, glowUsed;
        int alphaLast, additiveLast, glowLast, alphaPeak, additivePeak, glowPeak;
        float nextTrim;
        internal int DevPoolCount => alpha.Count+additive.Count+glows.Count;
        int viewFrame=-1;
        Rect effectView;
        bool hasEffectView;
        bool OutsideView(Sprite sprite,Vector2 world,Vector2 scale)
        {
            if(viewFrame!=Time.frameCount) {
                viewFrame=Time.frameCount;
                var camera=GameCameras.instance!=null?GameCameras.instance.mainCamera:null;
                hasEffectView=camera!=null&&Mathf.Abs(camera.transform.forward.z)>.999f;
                if(hasEffectView) {
                    float depth=Mathf.Abs(camera.transform.position.z);
                    Vector2 a=camera.ViewportToWorldPoint(new Vector3(0,0,depth)),b=camera.ViewportToWorldPoint(new Vector3(1,1,depth));
                    effectView=Rect.MinMaxRect(Mathf.Min(a.x,b.x)-4,Mathf.Min(a.y,b.y)-4,Mathf.Max(a.x,b.x)+4,Mathf.Max(a.y,b.y)+4);
                }
            }
            if(!hasEffectView)return false;
            var bounds=sprite.bounds;
            float r=(bounds.extents.magnitude+bounds.center.magnitude)*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y));
            return world.x+r<effectView.xMin||world.x-r>effectView.xMax||world.y+r<effectView.yMin||world.y-r>effectView.yMax;
        }
        float behind = BehindZ, over = OverZ, townZ = TownZ;
        Material additiveMaterial;

        public static FxBatch Create(Transform parent)
        {
            var go = new GameObject("Terra Fx");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<FxBatch>();
            return Instance;
        }

        void Awake() => additiveMaterial = TerraParticles.AdditiveMaterial();

        /// <summary>
        /// One SpriteBatch.Draw. pos in Terraria pixels (y down); rotation in Terraria radians
        /// (clockwise); scale per axis; colour as Terraria's bytes / 255 (premultiplied semantics);
        /// the sprite's pivot is the draw origin.
        /// </summary>
        /// <summary>While set, every draw is mirrored top to bottom about this Terraria y (the player turned over by the Gravity Globe).</summary>
        public float? MirrorAboutY;

        public void Draw(Sprite sprite, Vector2 pos, Color c, float rotation, Vector2 scale, bool flipY = false, bool flipX = false, bool overPlayer = false, bool town = false)
        {
            bool uniform = uniformScale;
            uniformScale = false;
            if (Grow != 1f && !town)
            {
                scale = GrowThick && !uniform ? new Vector2(scale.x, scale.y * Grow) : scale * Grow;
                DevGrowDraws++;
            }
            if (WeaponEffect && !town) c = WeaponEffects.Premultiplied(c);
            if (sprite == null || (c.a <= 0.001f && c.r <= 0.001f && c.g <= 0.001f && c.b <= 0.001f)) return;
            if (MirrorAboutY.HasValue)
            {
                pos.y = 2f * MirrorAboutY.Value - pos.y;
                rotation = -rotation;
                flipY = !flipY;
            }
            if (scale.x == 0f || scale.y == 0f) return;
            var world = TerraParticles.ToWorld(pos);
            // Rendering only: offscreen shots still move, collide and deal damage.
            if(OutsideView(sprite,world,scale))return;
            float deg = -rotation * Mathf.Rad2Deg;
            float a = c.a;
            // Deep Focus: the player's weapon effects also blue on its overlay (their shape, half clear, over the grey).
            if (FocusTint > 0.01f && WeaponEffect && !town && !FocusDraw)
            {
                var blue = Items.CharmEffects.FocusSelf;
                float strength = a > 0.001f ? a : Mathf.Clamp01(Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
                var sr = Next(focus, ref focusUsed, Items.CharmEffects.FocusSpriteMaterial);
                if (sr.gameObject.layer != Items.CharmEffects.FocusLayer) sr.gameObject.layer = Items.CharmEffects.FocusLayer;
                Put(sr, sprite, world, deg, scale, new Color(blue.r, blue.g, blue.b, Mathf.Clamp01(blue.a * 1.3f * strength) * FocusTint), flipX, flipY, overPlayer, 0f, town);
                DevFocusDraws++;
            }
            bool needAdd = c.r > a + 0.002f || c.g > a + 0.002f || c.b > a + 0.002f;
            if (a > 0.001f)
            {
                // The covering part: straight colour rgb/a at opacity a (black when the rest is added below).
                var col = needAdd ? new Color(0f, 0f, 0f, a) : new Color(Mathf.Clamp01(c.r / a), Mathf.Clamp01(c.g / a), Mathf.Clamp01(c.b / a), a);
            Put(Next(alpha, ref alphaUsed, null), sprite, world, deg, scale, col, flipX, flipY, overPlayer, 0f, town);
            }
            if (needAdd || a <= 0.001f)
            {
                if (additiveMaterial == null)
                {
                    // No additive shader: a translucent stand-in.
                    Put(Next(alpha, ref alphaUsed, null), sprite, world, deg, scale, new Color(c.r, c.g, c.b, Mathf.Clamp01(Mathf.Max(c.r, Mathf.Max(c.g, c.b)))), flipX, flipY, overPlayer, 0f, town);
                    return;
                }
                Put(Next(additive, ref additiveUsed, additiveMaterial), sprite, world, deg, scale, new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f), flipX, flipY, overPlayer,
                    a > 0.001f ? AddNearer : 0f, town);
            }
        }

        public void Draw(Sprite sprite, Vector2 pos, Color c, float rotation, float scale, bool flipY = false, bool flipX = false, bool overPlayer = false, bool town = false)
        {
            uniformScale = true;
            Draw(sprite, pos, c, rotation, new Vector2(scale, scale), flipY, flipX, overPlayer, town);
        }

        /// <summary>Lighting.AddLight at a Terraria position: a soft glow of that colour (radius in tiles).</summary>
        public void Light(Vector2 pos, Color rgb, float radiusTiles = 4f, bool weaponEffect = false)
        {
            if (WeaponEffect || weaponEffect) rgb *= WeaponEffects.Opacity;
            if (rgb.r + rgb.g + rgb.b < 0.01f) return;
            // Terraria's lighting on: the light goes into the map, which spreads it tile by tile.
            if (World.LightMap.Active)
            {
                World.LightMap.Add(TerraParticles.ToWorld(pos), rgb);
                return;
            }
            SpriteRenderer sr;
            if (glowUsed < glows.Count) sr = glows[glowUsed];
            else
            {
                sr = World.Glow.Create(transform, Color.white, 1f);
                glows.Add(sr);
            }
            glowUsed++;
            if (!sr.enabled) sr.enabled = true;
            // A weapon's shot's own light goes over the scenery with it, just under the effects (FxBatch.OnTop).
            int glowOrder = OnTop ? OnTopOrder - 1 : 0;
            if (DevTraceLow && !OnTop) TraceLow("光 ");
            if (sr.sortingOrder != glowOrder) sr.sortingOrder = glowOrder;
            var world = TerraParticles.ToWorld(pos);
            sr.transform.position = new Vector3(world.x, world.y, World.Glow.Z);
            float k = Mathf.Clamp01(Mathf.Max(rgb.r, Mathf.Max(rgb.g, rgb.b)));
            sr.color = World.Glow.Additive ? new Color(Mathf.Clamp01(rgb.r) * 0.5f, Mathf.Clamp01(rgb.g) * 0.5f, Mathf.Clamp01(rgb.b) * 0.5f, 1f) : new Color(rgb.r, rgb.g, rgb.b, 0.25f * k);
            float diameter = radiusTiles * 2f * 16f * Px;
            float spriteSize = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
            sr.transform.localScale = Vector3.one * (diameter / spriteSize);
        }

        SpriteRenderer Next(List<SpriteRenderer> pool, ref int used, Material material)
        {
            if (used < pool.Count) return pool[used++];
            var go = new GameObject("Fx");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (material != null) sr.sharedMaterial = material;
            pool.Add(sr);
            used++;
            return sr;
        }

        void Put(SpriteRenderer sr, Sprite sprite, Vector2 world, float deg, Vector2 scale, Color color, bool flipX, bool flipY, bool overPlayer, float nearer = 0f, bool town = false)
        {
            DevDrawCount++;
            if(additiveMaterial!=null && sr.sharedMaterial==additiveMaterial) DevLastAdditive=color;
            else DevLastAlpha=color;
            if (!sr.enabled) sr.enabled = true;
            sr.sprite = sprite;
            sr.color = color;
            int order = OnTop ? (overPlayer ? OnTopOrder + 2 : OnTopOrder) : 0;
            if (DevTraceLow && !OnTop) TraceLow("");
            if (false)
            {
                var frames = new System.Diagnostics.StackTrace(2, false).GetFrames();
                string who = "?";
                if (frames != null)
                    foreach (var f in frames)
                    {
                        var m = f.GetMethod();
                        if (m == null || m.DeclaringType == typeof(FxBatch)) continue;
                        who = m.DeclaringType?.Name + "." + m.Name;
                        break;
                    }
                DevLowCallers[who] = DevLowCallers.TryGetValue(who, out int n) ? n + 1 : 1;
            }
            if (sr.sortingOrder != order) sr.sortingOrder = order;
            // Deep Focus's marks on its overlay; every other draw on the game's (a pooled one may have been either).
            int layer = FocusDraw ? Items.CharmEffects.FocusLayer : sr.sharedMaterial == Items.CharmEffects.FocusSpriteMaterial ? Items.CharmEffects.FocusLayer : 0;
            if (sr.gameObject.layer != layer) sr.gameObject.layer = layer;
            sr.flipX = flipX;
            sr.flipY = flipY;
            float z;
            if (town) { z = townZ; townZ -= Step; }
            else if (overPlayer) { z = over; over -= Step; }
            else { z = behind; behind -= Step; }
            var t = sr.transform;
            t.position = new Vector3(world.x, world.y, z - nearer);
            t.rotation = Quaternion.Euler(0f, 0f, deg);
            t.localScale = new Vector3(scale.x, scale.y, 1f);
        }

        /// <summary>After everything has drawn this frame: what wasn't used goes dark, and the next frame starts over.</summary>
        void LateUpdate()
        {
            // Only last frame's draws may need hiding; parked pool entries are
            // already disabled. Do not pay the historical burst's cost forever.
            for (int i = alphaUsed; i < alphaLast; i++) alpha[i].enabled = false;
            for (int i = additiveUsed; i < additiveLast; i++) additive[i].enabled = false;
            for (int i = glowUsed; i < glowLast; i++) glows[i].enabled = false;
            for (int i = focusUsed; i < focusLast; i++) focus[i].enabled = false;
            focusLast = focusUsed;
            focusUsed = 0;
            alphaLast=alphaUsed;additiveLast=additiveUsed;glowLast=glowUsed;
            alphaPeak=Mathf.Max(alphaPeak,alphaUsed);additivePeak=Mathf.Max(additivePeak,additiveUsed);glowPeak=Mathf.Max(glowPeak,glowUsed);
            if(Time.unscaledTime>=nextTrim) {
                Trim(alpha,Mathf.Max(32,alphaPeak));Trim(additive,Mathf.Max(32,additivePeak));Trim(glows,Mathf.Max(8,glowPeak));
                alphaPeak=alphaUsed;additivePeak=additiveUsed;glowPeak=glowUsed;nextTrim=Time.unscaledTime+2f;
            }
            alphaUsed = additiveUsed = glowUsed = 0;
            behind = BehindZ;
            over = OverZ;
            townZ = TownZ;
        }
        static void Trim(List<SpriteRenderer> pool,int keep)
        {
            int count=Mathf.Min(64,Mathf.Max(0,pool.Count-keep));
            while(count-->0){int i=pool.Count-1;var sr=pool[i];pool.RemoveAt(i);if(sr!=null)Destroy(sr.gameObject);}
        }

        static readonly Dictionary<(string, int, int, int, int, float, float), Sprite> sprites = new Dictionary<(string, int, int, int, int, float, float), Sprite>();

        /// <summary>A rectangle of a Terraria texture (top-left origin, y down) as a sprite pivoting at (ox, oy) pixels from its top-left.</summary>
        public static Sprite Frame(string texture, int x, int y, int w, int h, float ox, float oy)
        {
            var key = (texture, x, y, w, h, ox, oy);
            if (sprites.TryGetValue(key, out var s)) return s;
            var tex = Terraria.TerrariaAssets.Texture(texture);
            s = Sprite.Create(tex, new Rect(x, tex.height - y - h, w, h), new Vector2(ox / w, 1f - oy / h), 1f / Px, 0, SpriteMeshType.FullRect);
            sprites[key] = s;
            return s;
        }

        /// <summary>A whole texture pivoting on its centre.</summary>
        public static Sprite Whole(string texture)
        {
            var tex = Terraria.TerrariaAssets.Texture(texture);
            return Frame(texture, 0, 0, tex.width, tex.height, tex.width / 2f, tex.height / 2f);
        }

        /// <summary>
        /// Main.DrawPrettyStarSparkle on Extra_98: two crossed streaks of the shine colour and two
        /// thinner white ones, all added, flaring up and down with flare between the four times.
        /// </summary>
        public void Sparkle(float opacity, Vector2 pos, Color drawColor, Color shine, float flare, float fadeInStart, float fadeInEnd, float fadeOutStart, float fadeOutEnd,
            float rotation, Vector2 scale, Vector2 fatness, bool overPlayer = false)
        {
            var tex = Whole("Extra_98");
            var c1 = shine * (opacity * 0.5f);
            c1.a = 0f;
            var c2 = drawColor * 0.5f;
            float k = Mathf.Clamp01(Mathf.InverseLerp(fadeInStart, fadeInEnd, flare)) * Mathf.Clamp01(Mathf.InverseLerp(fadeOutEnd, fadeOutStart, flare));
            var v1 = new Vector2(fatness.x * 0.5f, scale.x) * k;
            var v2 = new Vector2(fatness.y * 0.5f, scale.y) * k;
            Draw(tex, pos, c1, rotation + Mathf.PI / 2f, v1, overPlayer: overPlayer);
            Draw(tex, pos, c1, rotation, v2, overPlayer: overPlayer);
            Draw(tex, pos, c2, rotation + Mathf.PI / 2f, v1 * 0.6f, overPlayer: overPlayer);
            Draw(tex, pos, c2, rotation, v2 * 0.6f, overPlayer: overPlayer);
        }

        /// <summary>Terraria's Color from bytes.</summary>
        public static Color C(int r, int g, int b, int a = 255) => new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}
