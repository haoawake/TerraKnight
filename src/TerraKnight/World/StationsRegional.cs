using UnityEngine;
using TerraKnight.Terraria;

namespace TerraKnight.World
{
    internal sealed partial class Stations
    {
        static void AddRegionalSpecs()
        {
            for (int a = 0; a < AreaBlocks.All.Length; a++)
            {
                for (int i = 0; i < RegionalFurniture.Pieces.Length; i++)
                {
                    if (i == RegionalFurniture.PlatformIndex) continue;
                    var p = RegionalFurniture.Pieces[i];
                    specs[RegionalFurniture.Kind(a, i)] = new Spec {
                        Native = RegionalFurniture.Texture(RegionalFurniture.Kind(a, i)),
                        W = p.W, H = p.Heights.Length, Heights = p.Heights, Item = RegionalFurniture.Item(a, i),
                        // TileObjectData.DrawYOffset: candle -4, hanging lights -2, sofa/clock/candelabra/lamp +2.
                        DrawY = p.Tile == 33 ? -4 : p.Tile == 34 || p.Tile == 42 ? -2 : p.Tile == 89 || p.Tile == 104 || p.Tile == 100 || p.Tile == 93 ? 2 : 0,
                        Counts = p.Base == StationKind.WorkBench ? new[] { "工作台" } : p.Base == StationKind.Bookcase ? new[] { "书架" } : p.Base == StationKind.Sink ? new[] { "水槽" } : new string[0],
                        SolidTop = p.Base == StationKind.Table || p.Base == StationKind.WorkBench || p.Base == StationKind.Dresser || p.Base == StationKind.Bookcase || p.Base == StationKind.Piano,
                        Faces = p.Base == StationKind.Chair || p.Base == StationKind.Bed || p.Base == StationKind.Sofa || p.Base == StationKind.Toilet,
                        RightX = p.W * 18,
                        Ceiling = p.Base == StationKind.Chandelier || p.Base == StationKind.Lantern,
                    };
                }
                for (int pose = 19; pose <= 20; pose++)
                    specs[RegionalFurniture.Kind(a, pose)] = new Spec { Native = RegionalFurniture.Texture(RegionalFurniture.Kind(a, pose)), W = 2, H = 3, StyleX = pose == 20 ? 36 : 0, Heights = new[] { 16,16,16 }, Item = RegionalFurniture.Item(a,3), Counts = new string[0] };
            }
        }

        // A chandelier hangs by its middle top tile (TileObjectData.AnchorTop).
        static bool CeilingSpot(float x, float aimY, Spec spec, out float bottom)
        {
            float top = (Mathf.Floor(aimY / CellGrid.CellSize) + spec.H) * CellGrid.CellSize;
            foreach (int d in new[] { 0, 1, -1, 2, -2, 3, -3 })
            {
                float y = top + d * CellGrid.CellSize;
                if (!SolidTileAt(x, y, true)) continue;
                bottom = y - spec.H * CellGrid.CellSize;
                return true;
            }
            bottom = 0; return false;
        }

        static void UpdateRegionalLight(Station s)
        {
            if (!RegionalFurniture.IsRegional(s.Kind) || RegionalFurniture.Index(s.Kind) >= 19) return;
            var p = RegionalFurniture.Pieces[RegionalFurniture.Index(s.Kind)];
            if (p.Torches <= 0 || s.Renderer.GetComponent<RegionalFurnitureLight>() != null) return;
            s.Renderer.gameObject.AddComponent<RegionalFurnitureLight>().Init(s.Kind);
        }
    }

    /// <summary>Native coloured flame pixels, flickering in seven jittered copies as the existing torches do.</summary>
    internal sealed class RegionalFurnitureLight : MonoBehaviour
    {
        public RegionalTorchFlames.Flame Flame { get; private set; }
        public Color Rgb { get; private set; }
        public void Init(StationKind kind)
        {
            Rgb = RegionalFurniture.LightColor(RegionalFurniture.Area(kind));
            var tex = TerrariaAssets.Texture(RegionalFurniture.Texture(kind) + "_Flame");
            if (tex == null) return;
            var sprite = Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(.5f,0), 1f / Effects.TerraParticles.Px, 0, SpriteMeshType.FullRect);
            var fire = new GameObject("Terra Regional Furniture Flame");
            fire.transform.SetParent(transform, false);
            fire.transform.localPosition = new Vector3(0,0,-.00015f);
            Flame = fire.AddComponent<RegionalTorchFlames.Flame>();
            Flame.Build();
            Flame.Set(Mathf.RoundToInt(transform.position.x * 32),Mathf.RoundToInt(transform.position.y * 32),sprite);
            var light = Glow.CreateLight(transform,new Color(Rgb.r,Rgb.g,Rgb.b,.2f),Rgb);
            light.transform.localPosition = new Vector3(0,tex.height * Effects.TerraParticles.Px * .65f, .00005f);
            light.gameObject.AddComponent<Glow.Flicker>();
        }
    }
}
