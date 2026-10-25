using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Quick Slash and Grubberfly's Elegy on the nail arts (the user's, 2026-10-21): each art cuts again and throws light as
    /// the plain slashes do, from the moment its slash shows (past the Great Slash's wind-up). Quick Slash: a silver echo of
    /// the art's own slash a moment after it, crossed over it (the Great
    /// and Dash Slashes turned over top to bottom, each sweep of the Cyclone Slash the other way round); its hits are the 30%
    /// crits every nail hit is followed by (AfterNailHit). Grubberfly's Elegy: the Great and Dash Slashes throw one big beam
    /// their way (half the art's blow, 1.6 times the size), the Cyclone Slash a beam at each sweep, right and left in turn
    /// (half a sweep's blow).
    /// </summary>
    internal static partial class CharmEffects
    {
        const float EchoDelay = 0.05f, EchoLife = 0.24f, SpinEchoLife = 0.16f, ArtBeamScale = 1.6f;
        static readonly Color EchoColor = new Color(0.8f, 0.84f, 0.93f);
        public static int DevArtBeams, DevEchoes, DevEchoParts, DevEchoDrawn;
        public static string DevLastEcho = "";

        /// <summary>An art's moment: which art, its picture, its way (world degrees), when it came, what is done of it.</summary>
        sealed class ArtCue
        {
            public string Art, Scene;
            public Transform Picture;
            public float Angle, At, Seen = -1f;
            public bool Spin, Beamed;
        }
        static readonly List<ArtCue> artCues = new List<ArtCue>();
        static bool spinLeft;

        /// <summary>The Great or Dash Slash shown (NailAim.TickArts), turned its way (world degrees).</summary>
        public static void OnArtShown(HeroController hero, Transform art, float angle)
        {
            if (!Charms.Enabled || hero == null || art == null || !(On(QuickSlash) || On(GrubberflysElegy))) return;
            artCues.Add(new ArtCue { Art = art.name, Picture = art, Angle = angle, At = Time.time, Scene = SceneNow });
        }

        /// <summary>A sweep of the Cyclone Slash (NailArts.Tick, every 0.2 s it spins).</summary>
        public static void OnCycloneSweep(HeroController hero, Transform cyclone)
        {
            if (!Charms.Enabled || hero == null || cyclone == null || !(On(QuickSlash) || On(GrubberflysElegy))) return;
            spinLeft = !spinLeft;
            artCues.Add(new ArtCue { Art = "Cyclone Slash", Picture = cyclone, Angle = spinLeft ? 180f : 0f, At = Time.time, Spin = true, Scene = SceneNow });
        }

        /// <summary>TickNail, after the art's Heavy Blow is known: the beam at once, the echo when it is due.</summary>
        static void TickArtCues(HeroController hero)
        {
            string scene = SceneNow;
            for (int i = artCues.Count - 1; i >= 0; i--)
            {
                var cue = artCues[i];
                if (cue.Scene != scene || hero == null || cue.Picture == null || !cue.Picture.gameObject.activeInHierarchy || Time.time - cue.At > 1f) { artCues.RemoveAt(i); continue; }
                // The Great Slash's object wakes with its wind-up, its picture empty for a moment: from when the slash shows.
                if (cue.Seen < 0f)
                {
                    if (!Showing(cue.Picture)) continue;
                    cue.Seen = Time.time;
                }
                if (!cue.Beamed)
                {
                    cue.Beamed = true;
                    if (On(GrubberflysElegy)) ArtBeam(hero, cue);
                }
                if (!On(QuickSlash)) { artCues.RemoveAt(i); continue; }
                if (Time.time - cue.Seen < (cue.Spin ? 0f : EchoDelay)) continue;
                artCues.RemoveAt(i);
                EchoOf(cue.Picture, cue.Spin);
            }
        }

        /// <summary>Some of the art's picture drawn now (a renderer on with a real size).</summary>
        static bool Showing(Transform picture)
        {
            picture.GetComponentsInChildren(false, echoFound);
            foreach (var r in echoFound) if (r != null && r.enabled && r.bounds.size.sqrMagnitude > 0.25f) return true;
            return false;
        }

        static void ArtBeam(HeroController hero, ArtCue cue)
        {
            // The nail in hand, as its hits are (NailItem.NailHit): a reforged one with its prefix.
            ItemDb.Get(ItemType.KnightNail).Damage = NailItem.DamageNow();
            var def = Mod?.Inventory.Current.Def;
            if (def == null || def.Type != ItemType.KnightNail) def = ItemDb.Get(ItemType.KnightNail);
            float blow = Equipment.Damage(def) * NailArts.Multiplier(cue.Art) * ArtDamage(cue.Art);
            float a = cue.Angle * Mathf.Deg2Rad;
            Beam(hero, def, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), Mathf.Max(1, Mathf.RoundToInt(blow * BeamShare)), cue.Spin ? 1f : ArtBeamScale);
            DevArtBeams++;
        }

        // ------------------------------------------------------------------ Quick Slash's echo

        /// <summary>An echo: its copy of the art's picture, the copy's mesh and colours, when, how long.</summary>
        sealed class Echo
        {
            public GameObject Go;
            public Renderer R;
            public Mesh Mesh;
            public SpriteRenderer Sprite;
            public Color32[] Colors;
            public Vector3 Scale;
            public float Born, Life;
            public string Scene;
        }
        static readonly List<Echo> echoes = new List<Echo>();
        static readonly List<Renderer> echoFound = new List<Renderer>();

        /// <summary>A silver copy of the art's picture as it is now, turned over, fading out over a quarter second.</summary>
        static void EchoOf(Transform picture, bool spin)
        {
            picture.GetComponentsInChildren(false, echoFound);
            int made = 0;
            foreach (var r in echoFound)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.bounds.size.sqrMagnitude <= 0.25f) continue;
                var t = r.transform;
                var go = new GameObject("TerraKnight/Quick Slash echo");
                go.transform.SetPositionAndRotation(t.position, t.rotation);
                var s = t.lossyScale;
                var scale = spin ? new Vector3(-s.x, s.y, s.z) : new Vector3(s.x, -s.y, s.z);
                go.transform.localScale = scale;
                var e = new Echo { Go = go, Scale = scale, Born = Time.time, Life = spin ? SpinEchoLife : EchoLife, Scene = SceneNow };
                if (r is MeshRenderer mr)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) { Object.Destroy(go); continue; }
                    e.Mesh = Object.Instantiate(mf.sharedMesh);
                    e.Mesh.name = "TerraKnight/Quick Slash echo";
                    go.AddComponent<MeshFilter>().sharedMesh = e.Mesh;
                    var copy = go.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = mr.sharedMaterials;
                    copy.sortingLayerID = mr.sortingLayerID;
                    copy.sortingOrder = mr.sortingOrder + 1;
                    e.Colors = new Color32[e.Mesh.vertexCount];
                    e.R = copy;
                }
                else if (r is SpriteRenderer sr && sr.sprite != null)
                {
                    var copy = go.AddComponent<SpriteRenderer>();
                    copy.sprite = sr.sprite;
                    copy.sharedMaterial = sr.sharedMaterial;
                    copy.flipX = sr.flipX;
                    copy.flipY = sr.flipY;
                    copy.sortingLayerID = sr.sortingLayerID;
                    copy.sortingOrder = sr.sortingOrder + 1;
                    e.Sprite = copy;
                    e.R = copy;
                }
                else { Object.Destroy(go); continue; }
                echoes.Add(e);
                made++;
                DevLastEcho = r.name;
            }
            if (made == 0) return;
            DevEchoes++;
            DevEchoParts += made;
            if (!spin) Mod.PlaySound("Item_1", 0.3f, 1.35f);
            FadeEchoes();
        }

        /// <summary>Each frame: the echoes fading (silver, a little larger as they go), gone with their time or the room.</summary>
        static void FadeEchoes()
        {
            string scene = SceneNow;
            for (int i = echoes.Count - 1; i >= 0; i--)
            {
                var e = echoes[i];
                float k = (Time.time - e.Born) / e.Life;
                if (k >= 1f || e.Scene != scene || e.Go == null)
                {
                    if (e.Go != null) Object.Destroy(e.Go);
                    if (e.Mesh != null) Object.Destroy(e.Mesh);
                    echoes.RemoveAt(i);
                    continue;
                }
                float a = (1f - k) * (1f - k) * 0.9f;
                var c = new Color(EchoColor.r, EchoColor.g, EchoColor.b, a);
                if (e.Mesh != null)
                {
                    Color32 c32 = c;
                    for (int j = 0; j < e.Colors.Length; j++) e.Colors[j] = c32;
                    e.Mesh.colors32 = e.Colors;
                }
                if (e.Sprite != null) e.Sprite.color = c;
                e.Go.transform.localScale = e.Scale * (1f + 0.08f * k);
                if (e.R != null && e.R.isVisible) DevEchoDrawn++;
            }
        }

        /// <summary>Dev: echoes showing now.</summary>
        public static int DevEchoesShown => echoes.Count;
    }
}
