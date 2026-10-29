using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Every pylon can be moved. A pickaxe knocks one out (pick power until 100, as a Life Crystal) and it drops as its item,
    /// Terraria's pylon of its style; a bench's pylon knocked out doesn't come back by resting on that bench again. The item
    /// set down where it has room (three cells wide and four tall over solid floor, within reach) is a pylon there, on the map
    /// and travelled to like the others; in dreams, Godhome and the White Palace too, so the pylons by their benches knocked
    /// out can go back (the user's 10-27 "晶塔可以在神居和梦境被放置，不然挖掉了放不回去"); not in a boss's fight (the cells
    /// BossRooms keeps), the final fight's room or a cinematic. A moved pylon is reached the way into its scene that is there:
    /// its nearest bench (made the respawn point, as resting on it does), else one of its gates; the player steps out of the
    /// portal beside the pylon.
    /// </summary>
    internal static partial class Pylons
    {
        /// <summary>The pylon items, by style (Terraria's forest ... universal pylon).</summary>
        static readonly ItemType[] styleItemTypes =
        {
            ItemType.PylonForest, ItemType.PylonJungle, ItemType.PylonHallow, ItemType.PylonCavern, ItemType.PylonOcean,
            ItemType.PylonDesert, ItemType.PylonSnow, ItemType.PylonMushroom, ItemType.PylonUniversal,
        };

        /// <summary>A bench whose pylon was knocked out (per save, in the granted records): resting there again gives none.</summary>
        const string TakenPrefix = "pylon.taken.v1.";

        public static int DevMined, DevPlaced;
        internal static bool DevLegacyWatcherEntry;

        internal static void AddItems(List<ItemDef> list)
        {
            for (int i = 0; i < styleItemTypes.Length; i++)
                list.Add(new ItemDef
                {
                    Type = styleItemTypes[i], Name = StyleNames[i], Texture = "Item_" + StyleItems[i], PylonStyle = i, MaxStack = 99,
                    Source = ItemSource.Drop, Stage = 0, Rare = 1, Value = 100000, AutoReuse = false, UseAnimation = .25f, UseTime = .25f, NoMelee = true,
                    Tooltip = "放在有空地的地面上（宽 3 格、高 4 格），之后和其他晶塔一样可以传送过来\n用镐敲下来可以再搬到别处",
                });
        }

        static bool Taken(string scene, string bench) => TerraMod.Instance != null && TerraMod.Instance.GrantedAbilities.Contains(TakenPrefix + scene + "|" + bench);

        static Rect ReachRect(HeroController hero)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            float c = CellGrid.CellSize;
            return Rect.MinMaxRect(b.min.x - 5f * c, b.min.y - 4f * c, b.max.x + 5f * c, b.max.y + 4f * c);
        }

        static void Refuse(Vector2 at, string zh)
        {
            UI.CombatText.Show(new Vector3(at.x, at.y + 0.4f, 0f), Lang.T(zh), UI.CombatText.Notice);
        }

        static readonly Dictionary<Pylon, int> hits = new Dictionary<Pylon, int>();

        /// <summary>A pickaxe swung at a pylon in reach: pick power until 100 knocks it out, as its item. True when the swing was at one.</summary>
        /// <summary>The smart cursor's Step_ForceCursorToAnyMinableThing: a lit pylon at this point in reach.</summary>
        public static bool MinableAt(Vector2 p, HeroController hero)
        {
            if (hero == null) return false;
            var reach = ReachRect(hero);
            foreach (var o in spawned)
                if (o != null && o.isActiveAndEnabled && o.Bounds.Contains(p) && reach.Overlaps(o.Bounds)) return !o.Pylon.Dormant;
            return false;
        }

        public static bool TryMine(ItemDef pick, Vector2 mouse, HeroController hero)
        {
            if (pick == null || pick.PickPower <= 0 || hero == null) return false;
            var reach = ReachRect(hero);
            foreach (var o in spawned)
            {
                if (o == null || !o.isActiveAndEnabled || !o.Bounds.Contains(mouse) || !reach.Overlaps(o.Bounds)) continue;
                var p = o.Pylon;
                // Not lit yet: it stays (lit by resting on its bench).
                if (p.Dormant)
                {
                    Refuse(mouse, "这座晶塔还没点亮，在旁边的长椅上坐一下");
                    return true;
                }
                hits.TryGetValue(p, out int done);
                done += pick.PickPower;
                TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
                if (done < 100)
                {
                    hits[p] = done;
                    PylonObject.Burst(o.Bounds.center, 4, p.Style);
                    return true;
                }
                hits.Remove(p);
                KnockOut(p, o);
                return true;
            }
            return false;
        }

        static void KnockOut(Pylon p, PylonObject o)
        {
            var at = o.Bounds.center;
            PylonObject.Burst(at, 30, p.Style);
            Unlocked.Remove(p);
            spawned.Remove(o);
            Object.Destroy(o.gameObject);
            if (!p.Placed && !string.IsNullOrEmpty(p.Bench)) TerraMod.Instance?.GrantedAbilities.Add(TakenPrefix + p.Scene + "|" + p.Bench);
            WorldItems.Drop(ItemStack.Of(styleItemTypes[Mathf.Clamp(p.Style, 0, styleItemTypes.Length - 1)]), at);
            UI.WorldMap.Refresh(p.Scene);
            DevMined++;
            Plugin.Log.LogInfo($"[晶塔] 敲下了 {p.Scene} 的{p.Name}（{(p.Placed ? "自己放的" : "长椅 " + p.Bench + " 旁的")}）");
        }

        /// <summary>A pylon item used at the cursor: the pylon set down there if it fits. True when it was (the item is spent).</summary>
        public static bool TryPlace(ItemDef item, HeroController hero, Vector2 mouse)
        {
            var gm = GameManager.UnsafeInstance;
            if (item == null || !item.IsPylon || hero == null || gm == null) return false;
            string scene = gm.sceneName ?? "";
            if (scene.StartsWith("Room_Final_Boss") || scene.StartsWith("Cinematic"))
            {
                Refuse(mouse, "这里放不下晶塔");
                return false;
            }
            var cell = CellGrid.WorldToCell(mouse);
            var region = TerraWorld.RegionAt(cell.x, cell.y);
            if (region == null) return false;
            int x0 = cell.x - WidthCells / 2;
            // The cursor on the pylon's foot or a little above it: the floor under it, three cells down at most.
            int y = int.MinValue;
            for (int dy = 0; dy <= 3 && y == int.MinValue; dy++)
                if (Fits(region, x0, cell.y - dy, false, HeightCells)) y = cell.y - dy;
            var size = new Vector2(WidthCells, HeightCells) * CellGrid.CellSize;
            if (y == int.MinValue)
            {
                Refuse(mouse, "这里放不下晶塔");
                return false;
            }
            var corner = CellGrid.CellCenter(x0, y) - Vector2.one * (CellGrid.CellSize / 2f);
            var foot = new Rect(corner, size);
            if (!ReachRect(hero).Overlaps(foot)) return false;
            foreach (var o in spawned)
                if (o != null && o.Bounds.Overlaps(foot))
                {
                    Refuse(mouse, "这里已经有晶塔了");
                    return false;
                }
            var origin = (Vector2)region.CellOffset * CellGrid.CellSize;
            var p = new Pylon
            {
                Scene = region.SceneName,
                Bench = "",
                Placed = true,
                Local = corner - origin,
                SceneSize = new Vector2(region.Grid.Width, region.Grid.Height) * CellGrid.CellSize,
                Style = item.PylonStyle,
                ArriveBench = NearestBench(region.SceneName, foot.center),
                Entry = GateOf(region.SceneName),
            };
            if (string.IsNullOrEmpty(p.ArriveBench) && string.IsNullOrEmpty(p.Entry))
            {
                Refuse(mouse, "这个房间没有能传送进来的入口，不能放晶塔");
                return false;
            }
            Unlocked.Add(p);
            var obj = Spawn(p, region);
            if (obj != null) obj.Appear();
            UI.WorldMap.Refresh(p.Scene);
            TerraMod.Instance?.PlaySound("Item_4", 0.8f);
            DevPlaced++;
            Plugin.Log.LogInfo($"[晶塔] 在 {p.Scene} 放下了{p.Name}，位置（场景坐标）{p.Local}，传送{(string.IsNullOrEmpty(p.ArriveBench) ? "从入口 " + p.Entry : "经长椅 " + p.ArriveBench)}进来");
            return true;
        }

        /// <summary>The bench nearest a point in a loaded scene (its respawn marker's name), or "" when it has none.</summary>
        static string NearestBench(string scene, Vector2 near)
        {
            string best = "";
            float bestD = float.MaxValue;
            foreach (var go in GameObject.FindGameObjectsWithTag("RespawnPoint"))
            {
                if (go.scene.name != scene || !BenchUsable(go.name)) continue;
                float d = Vector2.Distance(go.transform.position, near);
                if (d < bestD) { bestD = d; best = go.name; }
            }
            return best;
        }

        /// <summary>A gate of a loaded scene to come in by (a side or top gate before a door), or "" when it has none.</summary>
        static string GateOf(string scene)
        {
            string door = "";
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
            {
                if (tp == null || tp.gameObject.scene.name != scene || string.IsNullOrEmpty(tp.name)) continue;
                if (tp.isADoor) { if (door == "") door = tp.name; continue; }
                return tp.name;
            }
            return door;
        }
    }
}
