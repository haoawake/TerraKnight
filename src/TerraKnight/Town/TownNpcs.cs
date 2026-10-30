using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Town
{
    /// <summary>One line of a shop (Chest.SetupShop): the item, the stage it's sold from, and the moon phases it needs (null = any).</summary>
    internal sealed class Stock
    {
        public readonly ItemType Type;
        public readonly int Stage;
        public readonly int[] Phases;

        public Stock(ItemType type, int stage, int[] phases)
        {
            Type = type;
            Stage = stage;
            Phases = phases;
        }
    }

    /// <summary>
    /// When a townsperson is in Dirtmouth: from its stage on (a resident, a town pet or slime), woken at the Blue Lake (the
    /// Angler), in December (Santa Claus), every other townsperson home (the Princess), some days from dawn to dusk (the
    /// Traveling Merchant).
    /// </summary>
    internal enum NpcKind : byte { Resident, Angler, Santa, Princess, Visitor, Pet }

    /// <summary>A Terraria town NPC: its id, internal and Chinese names, its profile texture, the stage it moves in at, and its shop.</summary>
    internal sealed class NpcDef
    {
        public readonly int Id;
        public readonly string Internal, Zh, Profile;
        public readonly int Stage;
        /// <summary>What it sells (the Traveling Merchant's changes every visit).</summary>
        public Stock[] Items;
        /// <summary>The generated townsfolk's (TownNpcsMore.cs): the texture (under Images), Main.npcFrameCount and
        /// NPCID.Sets.ExtraFramesCount, the given names, the dialogue's text keys, and when it is in town.</summary>
        public string Texture;
        public int Frames, Extra;
        public string[] Names, Chat;
        public NpcKind Kind;

        public NpcDef(int id, string internalName, string zh, string profile, int stage, Stock[] items)
        {
            Id = id;
            Internal = internalName;
            Zh = zh;
            Profile = profile;
            Stage = TownShopExtra.Arrival(internalName, stage);
            Items = TownShopExtra.With(internalName, items);
        }
    }

    /// <summary>
    /// Terraria's townsfolk, all living in Dirtmouth (spec-townnpc): each moves in once the
    /// player has reached its stage, wanders about its spot on the main street (AI_007: stand a
    /// while, walk a while at 1 px a tick, turn at walls and drops), and is talked to with a right
    /// click within reach: a chat line, "商店" and "关闭". The shop is Terraria's: its stock rebuilt
    /// each time it opens (by stage and, for the Skeleton Merchant, the moon phase), bought for
    /// geo, and what's sold to it waiting to be bought back until it closes.
    /// </summary>
    internal static partial class TownNpcs
    {
        const float Px = TerraParticles.Px;
        const float L = 0.874f;

        sealed class Npc
        {
            public NpcDef Def;
            public string Given;
            /// <summary>Feet (world units); speed in px per tick.</summary>
            public float X, FeetY, Vx, HomeX;
            /// <summary>How far either side of home it may wander (Safe): the bench's own default spot
            /// is the town's shared one, so those roam the street; one bound to a chair stays near it.</summary>
            public float Range = RoamRange;
            public int Dir = 1, Ai0, Frame;
            public float Ai1, FrameCounter, LocalAi3, StuckTicks, LastX;
            /// <summary>NPC.closeDoor, doorX/doorY: it opened the door whose doorway is there and shuts it once past.</summary>
            public bool ShutDoor;
            public Vector2 DoorAt;
            /// <summary>Main.HoverOverNPCs' GivenOrTypeName: the type (Terraria's own name for it in the language now) and the given name (Game.NPCTitle's order).</summary>
            public string Name
            {
                get
                {
                    // Made once for its given name in the language (asked every frame by the map and the hover).
                    if (name != null && ReferenceEquals(nameGiven, Given) && nameCulture == Lang.Culture && nameSource == Lang.IsSource) return name;
                    nameGiven = Given;
                    nameCulture = Lang.Culture;
                    nameSource = Lang.IsSource;
                    if (Lang.IsSource) return name = Def.Zh + Given;
                    string type = Terraria.TerrariaText.NpcName(Lang.Culture, Def.Id) ?? Def.Zh;
                    string title = Terraria.TerrariaText.Get(Lang.Culture, "Game.NPCTitle") ?? "{0} the {1}";
                    try { return name = string.Format(title, Given, type); }
                    catch (System.FormatException) { return name = Given + " " + type; }
                }
            }
            string name, nameGiven, nameCulture;
            bool nameSource;
        }

        static readonly List<Npc> npcs = new List<Npc>();

        static NpcDef[] all;
        /// <summary>Every townsperson: the first eleven (TownShopData.cs) and the rest (TownNpcsMore.cs).</summary>
        static NpcDef[] All
        {
            get
            {
                if (all != null) return all;
                AddBatch1017Stock();
                var list = new List<NpcDef>(roster);
                list.AddRange(more);
                return all = list.ToArray();
            }
        }

        static readonly Dictionary<NpcDef, string> defaultTextures = new Dictionary<NpcDef, string>();

        /// <summary>The body sheet's name (made once: asked every frame).</summary>
        static string TextureOf(NpcDef d)
        {
            if (d.Texture != null) return d.Texture;
            if (!defaultTextures.TryGetValue(d, out var t)) defaultTextures[d] = t = "TownNPCs/" + d.Profile + "_Default";
            return t;
        }
        static int FramesOf(NpcDef d) => d.Frames > 0 ? d.Frames : FrameCount(d.Id);
        // Town residents have no HealthManager. Flymeal gives them a flee reaction without touching quest FSMs.
        public static int FlymealStrikes { get; private set; }
        internal static void FlymealStrike(Rect box, HashSet<string> hit)
        {
            foreach (var n in npcs)
            {
                if (!box.Overlaps(new Rect(n.X - .35f, n.FeetY, .7f, 1.4f)) || !hit.Add(n.Def.Internal)) continue;
                n.Dir = n.X < box.center.x ? -1 : 1;
                n.Vx = n.Dir * 3f; n.Ai0 = 1; n.Ai1 = 180;
                FlymealStrikes++;
            }
        }
        static TerrainRegion placedFor;
        static float streetMin, streetMax, benchX, benchY;
        /// <summary>Dirtmouth's own bench was found and the town read from it: a click has somewhere to put a townsperson.</summary>
        static bool townFound;
        /// <summary>
        /// How far a townsperson walks either side of its home. Everyone Dirtmouth's own bench is home to
        /// shares that one spot, so they need room to spread over; one the player bound to a chair keeps
        /// close to it (ChairRange, set where it is put down).
        /// </summary>
        public const float RoamRange = 16f, ChairRange = 4f;
        static float tickAccumulator;
        static Npc talk;
        static bool shop;
        /// <summary>The Guide's crafting menu is open (Main.InGuideCraftMenu).</summary>
        static bool guideCraft;

        /// <summary>
        /// Main.guideItem: what the Guide is shown. The crafting column lists every recipe it goes
        /// into (Recipe.CollectGuideRecipes) whatever the stations and the bag, to look at only; it
        /// goes back into the bag when the talk ends (Player.dropItemCheck).
        /// </summary>
        public static ItemStack GuideItem;
        static int lastStage = -1;

        /// <summary>Given names chosen so far (saved), and who has been announced as moved in.</summary>
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>();
        public static readonly HashSet<string> Arrived = new HashSet<string>();

        /// <summary>
        /// The townsfolk the player has sent away with a right click in the panel (TownNpcsManage.Remove):
        /// they stay away — even the ones who would otherwise move in on their own, which is how a
        /// right-clicked shopkeeper used to come straight back — until the player sets one down again
        /// (PlaceClick takes it out of here). Kept in the save with the town (ModSave.NpcDismissed).
        /// </summary>
        public static readonly HashSet<string> Dismissed = new HashSet<string>();

        /// <summary>The chat box's text (empty while the shop is open, as Main.npcChatText).</summary>
        public static string ChatText { get; private set; } = "";
        public static string TalkingName => talk?.Name;
        public static bool ChatOpen => talk != null && !shop && !guideCraft && !reforge;
        /// <summary>Talking to the Guide, whose chat has the crafting button.</summary>
        public static bool TalkingIsGuide => talk != null && talk.Def.Internal == "Guide";
        /// <summary>
        /// The Guide's crafting menu: talking to him with the inventory open after his crafting button, or his advice carried
        /// with the column switched to it (<see cref="GuideAdvice"/>).
        /// </summary>
        public static bool GuideCraftOpen => InventoryGui.Open && (talk != null && guideCraft || GuideAdvice.On);
        /// <summary>A shop is open: talking with the inventory open (Main.npcShop).</summary>
        public static bool ShopOpen => talk != null && shop && InventoryGui.Open;
        public static bool TalkingHasShop => talk != null && talk.Def.Items != null && talk.Def.Items.Length > 0;
        /// <summary>Talking to the Nurse (her healing) or the Tax Collector (his taxes).</summary>
        public static bool TalkingIsNurse => talk != null && talk.Def.Id == 18;
        public static bool TalkingIsTaxCollector => talk != null && talk.Def.Id == 441;
        /// <summary>Talking to the Angler, whose chat has the "Quest" button.</summary>
        public static bool TalkingIsAngler => talk != null && talk.Def.Internal == "Angler";
        /// <summary>Talking to the Stylist, whose chat has the "Hair Style" button (UI.AppearanceWindow).</summary>
        public static bool TalkingIsStylist => talk != null && talk.Def.Id == 353;

        /// <summary>The Angler's "Quest" button (Lang.inter[64]): today's fish, or handing it in.</summary>
        public static void AnglerQuest()
        {
            if (!TalkingIsAngler || TerraMod.Instance == null) return;
            ChatText = Angler.QuestButton(TerraMod.Instance.Inventory, talk.Name);
            TerraMod.Instance.PlaySound("Menu_Tick", 0.7f);
        }

        /// <summary>
        /// The open shop's slots, and which hold bought-back items (buyOnce): Terraria's 40 (four rows of ten), and a fifth
        /// row for a stock past them (the Merchant's at the last stage, with every healing and mana potion), shown only then.
        /// </summary>
        public const int ShopColumns = 10, ShopBaseSlots = 40;
        public static readonly ItemStack[] ShopItems = new ItemStack[50];
        public static readonly bool[] BuyBack = new bool[50];
        /// <summary>A buy-back slot's price apiece, in copper: what each fetched when sold.</summary>
        public static readonly long[] BuyBackPrice = new long[50];

        /// <summary>
        /// Main.shopSellbackHelper (ItemShopSellbackHelper): what was bought at the townsfolk's since the save was entered,
        /// by item, prefix and forging, with what each cost; sold back, as many as were bought fetch that price.
        /// </summary>
        static readonly List<(ItemType Type, int Prefix, int Forge, int Count, long Price)> bought = new List<(ItemType, int, int, int, long)>();

        /// <summary>A new save or the title screen: the purchases forgotten (Main.shopSellbackHelper.Clear).</summary>
        public static void ClearBought() => bought.Clear();
        /// <summary>A slot showing what the seller brings at a later stage (that stage; 0: on sale now): to look at, not to buy.</summary>
        public static readonly int[] ShopLock = new int[50];
        /// <summary>A slot showing a spell's weapon before that spell is absorbed (the spell, 1-4: SpellRewards; 0: none): to look at, not to buy.</summary>
        public static readonly int[] ShopSpell = new int[50];

        /// <summary>The rows the shop shows: four, five once something lies past the fortieth slot.</summary>
        public static int ShopRowsShown
        {
            get
            {
                for (int i = ShopBaseSlots; i < ShopItems.Length; i++) if (!ShopItems[i].IsEmpty) return ShopItems.Length / ShopColumns;
                return ShopBaseSlots / ShopColumns;
            }
        }

        /// <summary>Main.moonPhase: a new phase every Terraria day (24 minutes of play), eight in all; in a LAN game the host's.</summary>
        public static int MoonPhase => PlayerData.instance == null ? 0 : ((World.TerraTime.Day % 8) + 8) % 8;

        public static int Count => npcs.Count;
        public static string DevDescribe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var n in npcs) sb.Append($"{n.Name}@{n.X:F1} ");
            return sb.Length == 0 ? "无" : sb.ToString().Trim();
        }

        /// <summary>Dev: where a townsperson stands (the middle of its sprite), if it lives here now.</summary>
        public static Vector2? DevPos(string internalName)
        {
            foreach (var n in npcs) if (n.Def.Internal == internalName) return new Vector2(n.X, n.FeetY + 20f * Px);
            return null;
        }

        /// <summary>Dev: the open shop's stock, briefly.</summary>
        public static string DevStock()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < ShopItems.Length; i++)
                if (!ShopItems[i].IsEmpty) sb.Append($"{ShopItems[i].Def.Name}{(BuyBack[i] ? "(回购)" : "")}={Shop.Money(BuyPrice(i))} ");
            return sb.Length == 0 ? "空" : sb.ToString().Trim();
        }

        /// <summary>Dev: how many items a townsperson sells at a stage (every moon phase's), and whether one of them is this.</summary>
        public static int DevStockAt(string internalName, int stage, ItemType type, out bool has)
        {
            has = false;
            foreach (var def in All)
            {
                if (def.Internal != internalName) continue;
                if(def.Stage>stage)return 0;
                int n = 0;
                foreach (var s in ProgressionBalance.ShopStock(def.Internal,def.Items))
                {
                    if (s.Stage > stage) continue;
                    if (ItemDb.Get(s.Type).Stage > stage) continue;
                    n++;
                    if (s.Type == type) has = true;
                }
                return n;
            }
            return 0;
        }

        // All possible stock, independent of today's moon, stage, visitor roll or spell flags.
        internal static IEnumerable<ItemType> ShopCatalogue()
        {
            foreach(var stock in travelPool)yield return stock.Type;
            foreach(var npc in All)
                foreach(var stock in ProgressionBalance.ShopStock(npc.Internal,npc.Items))yield return stock.Type;
        }

        internal static IEnumerable<(string Npc, ItemType Type)> DevSourcesAt(int stage)
        {
            if(stage>=1)
            foreach (var stock in travelPool)
                if (stock.Stage <= stage && ItemDb.Get(stock.Type).Stage <= stage)
                    yield return ("TravelingMerchant", stock.Type);
            foreach (var npc in All)
                if (npc.Stage <= stage)
                    foreach (var stock in ProgressionBalance.ShopStock(npc.Internal, npc.Items))
                        if (stock.Stage <= stage && ItemDb.Get(stock.Type).Stage <= stage)
                            yield return (npc.Internal, stock.Type);
        }

        public static void Clear()
        {
            npcs.Clear();
            placedFor = null;
            // Another save is coming in: what the Guide held is in the last save's bag already
            // (ModSave.Write put it back), so it goes, rather than into the next save's bag.
            GuideItem = default;
            ReforgeItem = default;
            talk = null;
            shop = false;
            guideCraft = false;
            reforge = false;
            Names.Clear();
            Arrived.Clear();
            Dismissed.Clear();
            Taxes = 0;
            TaxClock = 0;
            ClearHouses();
            lastStage = -1;
            travelerCame = -1;
            travelerWasHere = null;
        }

        // ------------------------------------------------------------------ the town

        /// <summary>Every frame in play: keep the townsfolk in Dirtmouth (when it's loaded), move them, take right clicks.</summary>
        public static void Tick(HeroController hero)
        {
            var region = TerrainSystem.Find("Town");
            if (region == null || hero == null)
            {
                // Dirtmouth isn't loaded: only those with a house in a scene that is stay, in their rooms (TownNpcsHousing).
                if (npcs.Count > 0) npcs.RemoveAll(n => hero == null || !Houses.ContainsKey(n.Def.Internal));
                placedFor = null;
                if (talk != null && !npcs.Contains(talk)) Close(false);
                if (hero == null) return;
            }
            else
            {
                int stage = Shop.UnlockedStage();
                if (region != placedFor || stage != lastStage || Time.unscaledTime >= nextRecheck)
                {
                    nextRecheck = Time.unscaledTime + 3f;
                    Place(region, stage, hero);
                }
            }
            WatchTraveler();
            HousingTick(hero);
            CollectTaxTime();
            tickAccumulator += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            while (tickAccumulator >= 1f)
            {
                tickAccumulator -= 1f;
                foreach (var n in npcs) Step(n, hero);
            }
            // Main.HoverOverNPCs's reach: leaving it closes the chat (and the shop).
            if (talk != null && !InReach(talk, hero)) Close(true);
            if ((shop || guideCraft || reforge) && talk != null && !InventoryGui.Open) Close(true);
        }

        /// <summary>
        /// Main.UpdateTime_SpawnTownNPCs read in Hallownest: functional residents obey their stage even after a save reload; the Angler once woken; Santa in December (Main.xMas: the 15th on); the Princess with every
        /// other resident home; the Traveling Merchant on his days, by day.
        /// </summary>
        static bool InTown(NpcDef def, int stage)
        {
            // Sent away with a right click in the panel: it stays away, whatever it would do on its own,
            // until the player puts it down again.
            if (Dismissed.Contains(def.Internal)) return false;
            if (AutoArrives(def) && stage < def.Stage) return false;
            switch (def.Kind)
            {
                case NpcKind.Angler: return Angler.Saved;
                case NpcKind.Santa:
                    if (!AutoArrives(def) && !Arrived.Contains(def.Internal)) return false;   // no shop of his own: the townsfolk panel's
                    return stage >= def.Stage && System.DateTime.Now.Month == 12 && System.DateTime.Now.Day >= 15;
                case NpcKind.Visitor: return stage >= def.Stage && TravelerVisiting();
                case NpcKind.Princess:
                    if (Arrived.Contains(def.Internal)) return true;
                    if (!AutoArrives(def)) return false;   // the decorative ones wait for the townsfolk panel
                    if (stage < def.Stage) return false;
                    foreach (var o in All)
                        if (o != def && o.Kind == NpcKind.Resident && o.Stage <= stage && !npcs.Exists(n => n.Def == o)) return false;
                    return true;
                // Terraria's own townsfolk have moved in by their stage ever since; in Hallownest the
                // shops and the few with something to do still do, and everyone else waits in the
                // townsfolk panel (AutoArrives): the street was filling up with townsfolk nobody asked for.
                default: return (def.Stage <= stage && AutoArrives(def)) || Arrived.Contains(def.Internal);
            }
        }

        /// <summary>
        /// Main.UpdateTime's Traveling Merchant: through the morning (the day's first 27000 ticks, 450 s here) each tick a
        /// one in 108000 chance (27000 / dayRate × 4) that he comes, when two or more townsfolk are in town (not the Old
        /// Man or the Skeleton Merchant), so on about one day in four and a half (1 − e^−¼) at some time of the morning.
        /// Rolled here once for the day, by its number (the same in a LAN game): whether he comes and when. He goes after
        /// the day's 48600th tick (810 s) or at nightfall, once no player is by him (UnspawnTravelNPC: off screen). His
        /// stock (Chest.SetupTravelShop) is chosen for the day from what he may bring: four to six things.
        /// </summary>
        static bool TravelerVisiting()
        {
            if (PlayerData.instance == null) return false;
            int day = World.TerraTime.Day;
            float now = World.TerraTime.Now;
            bool came = travelerCame == day;
            if (!World.TerraTime.IsDay || now > TravelerLeaves)
            {
                if (came && TravelerByPlayer()) return true;
                return false;
            }
            if (came) return true;
            var rng = new System.Random(day * 7919 + 17);
            if (rng.NextDouble() >= TravelerOdds) return false;
            float at = (float)rng.NextDouble() * TravelerMorning;
            if (now < at || TownsfolkHere() < 2) return false;
            travelerCame = day;
            return true;
        }

        const float TravelerMorning = 450f, TravelerLeaves = 810f;
        /// <summary>1 − (1 − 1/108000)^27000.</summary>
        const double TravelerOdds = 0.2212;
        /// <summary>The day he came on (−1: not come).</summary>
        static int travelerCame = -1;
        /// <summary>Whether he was in town at the last look (null: no look yet since loading, nothing to tell).</summary>
        static bool? travelerWasHere;
        static float nextTravelerLook;

        /// <summary>The townsfolk in town now, the Traveling Merchant aside.</summary>
        static int TownsfolkHere()
        {
            int stage = Shop.UnlockedStage(), n = 0;
            foreach (var d in All)
                if (d.Kind != NpcKind.Visitor && InTown(d, stage)) n++;
            return n;
        }

        /// <summary>A player beside him on the street (about a screen round him): he waits to go till they're away.</summary>
        static bool TravelerByPlayer()
        {
            var hero = HeroController.SilentInstance;
            var him = npcs.Find(n => n.Def.Kind == NpcKind.Visitor);
            if (hero == null || him == null) return false;
            if (talk == him) return true;
            var at = hero.transform.position;
            return Mathf.Abs(at.x - him.X) < 20f && Mathf.Abs(at.y - him.FeetY) < 12f;
        }

        /// <summary>
        /// His coming and going told wherever the player is (Main.NewText, Announcement.HasArrived; LegacyMisc.35), not only
        /// in Dirtmouth; each visit has a name of its own. A look a second; nothing told at the first look after loading.
        /// </summary>
        static void WatchTraveler()
        {
            if (Time.unscaledTime < nextTravelerLook) return;
            nextTravelerLook = Time.unscaledTime + 1f;
            var def = System.Array.Find(All, d => d.Kind == NpcKind.Visitor);
            if (def == null) return;
            bool here = InTown(def, Shop.UnlockedStage());
            if (travelerWasHere == here) return;
            bool tell = travelerWasHere != null;
            travelerWasHere = here;
            if (here && !Names.ContainsKey(def.Internal))
            {
                var pool = GivenNames(def.Internal);
                Names[def.Internal] = pool[Random.Range(0, pool.Length)];
            }
            string name = new Npc { Def = def, Given = Names.TryGetValue(def.Internal, out var given) ? given : "" }.Name;
            if (!here) Names.Remove(def.Internal);
            if (!tell) return;
            var color = new Color(50 / 255f, 125 / 255f, 255 / 255f);
            string line = Terraria.TerrariaText.Get(Lang.Culture, here ? "Announcement.HasArrived" : "LegacyMisc.35");
            Net.NetChat.Add(line != null ? line.Replace("{0}", name) : Lang.F(here ? "{0}已到达！" : "{0}已离去！", name), color);
            Plugin.Log.LogInfo($"[城镇] {name} {(here ? "已到达" : "已离去")}");
        }

        static int travelDay = -1,travelStage=-1;

        static void StockTraveler(NpcDef def, int stage)
        {
            int day = PlayerData.instance == null ? 0 : World.TerraTime.Day;
            if (day == travelDay && stage==travelStage && def.Items != null && def.Items.Length > 0) return;
            travelDay = day;travelStage=stage;
            var rng = new System.Random(day * 104729 + 3);
            var pool = new List<(ItemType Type, int Stage)>();
            foreach (var t in travelPool) if (t.Stage <= stage && ItemDb.Get(t.Type).Stage<=stage) pool.Add(t);
            var pick = new List<Stock>();
            int n = 4 + rng.Next(3);
            int best=-1;
            foreach(var item in pool)if(ProgressionLoot.CombatWeapon(ItemDb.Get(item.Type)))best=System.Math.Max(best,ItemDb.Get(item.Type).Stage);
            var weapons=pool.FindAll(item=>ProgressionLoot.CombatWeapon(ItemDb.Get(item.Type)) && ItemDb.Get(item.Type).Stage==best);
            if(weapons.Count>0)
            {
                var weapon=weapons[rng.Next(weapons.Count)];
                pick.Add(new Stock(weapon.Type,weapon.Stage,null));pool.Remove(weapon);
            }
            while (pick.Count < n && pool.Count > 0)
            {
                int k = rng.Next(pool.Count);
                pick.Add(new Stock(pool[k].Type, 0, null));
                pool.RemoveAt(k);
            }
            def.Items = pick.ToArray();
        }

        static float nextRecheck;

        /// <summary>Puts every townsperson who has moved in on the street round Dirtmouth's bench.</summary>
        static void Place(TerrainRegion region, int stage, HeroController hero)
        {
            bool fresh = region != placedFor;
            placedFor = region;
            lastStage = stage;
            var scene = region.Root.gameObject.scene;
            Transform bench = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                    if (fsm.FsmName == "Bench Control")
                    {
                        bench = fsm.transform;
                        break;
                    }
                if (bench != null) break;
            }
            if (bench == null) return;
            townFound = true;
            if (fresh)
            {
                npcs.Clear();
                benchX = bench.position.x;
                benchY = bench.position.y;
                Survey();
                HideStreetForeground(scene, Ground(benchX) ?? benchY);
            }
            // Who has left (the Traveling Merchant at dusk, Santa after December): "{0} has departed!" (LegacyMisc.35).
            for (int k = npcs.Count - 1; k >= 0; k--)
            {
                var gone = npcs[k];
                if (InTown(gone.Def, stage)) continue;
                if (talk == gone) Close(false);
                npcs.RemoveAt(k);
                Arrived.Remove(gone.Def.Internal);
                if (gone.Def.Kind == NpcKind.Visitor) continue;
                string left = Terraria.TerrariaText.Get(Lang.Culture, "LegacyMisc.35");
                Net.NetChat.Add(left != null ? left.Replace("{0}", gone.Name) : Lang.F("{0}已离去！", gone.Name), new Color(50 / 255f, 125 / 255f, 255 / 255f));
            }
            var homes = StreetHomes();
            int i = 0;
            foreach (var def in All)
            {
                // Once moved in, a townsperson stays (as in Terraria), even if the progress reads lower than when it came.
                if (!InTown(def, stage)) continue;
                // One with a house lives there, wherever it is (TownNpcsHousing), not on the street.
                if (Houses.ContainsKey(def.Internal)) continue;
                if (npcs.Exists(n => n.Def == def))
                {
                    // Already standing here: the ground under it may have changed since (dug out by the
                    // player, or a position saved before the panel learned to stand them on the floor).
                    var have = npcs.Find(n => n.Def == def);
                    var under = StandingFloor(have.X, have.FeetY, have);
                    if (under.HasValue) have.FeetY = under.Value;
                    i++;
                    continue;
                }
                if (homes.Count == 0) break;
                if (def.Kind == NpcKind.Visitor) StockTraveler(def, stage);
                if (!Names.TryGetValue(def.Internal, out var given))
                {
                    var pool = GivenNames(def.Internal);
                    given = pool[Random.Range(0, pool.Length)];
                    Names[def.Internal] = given;
                }
                // More townsfolk than homes along the street: they share, a little apart.
                // One the player put down stays where they put it (Homes, saved with the town); a spot
                // that has no ground any more (or was saved by a build that let them float) is given up.
                float home;
                float put, putY = 0f;
                bool hasHome = Homes.TryGetValue(def.Internal, out put) && HomeY.TryGetValue(def.Internal, out putY);
                // A chair home is the chair's seat: the chair itself is furniture, not a cell in the
                // grid, and the floor under it may be the street while the seat is an upper storey.
                float seatY = putY;
                bool onChair = hasHome && World.Stations.ChairNear(new Vector2(put, putY), out _, out seatY);
                bool placed = hasHome && (onChair || GridGround(put, putY + 0.5f, out _));
                if (placed) home = put;
                else
                {
                    if (Homes.ContainsKey(def.Internal)) Homes.Remove(def.Internal);
                    if (HomeY.ContainsKey(def.Internal)) HomeY.Remove(def.Internal);
                    home = homes[i % homes.Count] + 1.2f * (i / homes.Count) * ((i / homes.Count) % 2 == 0 ? 1f : -1f);
                }
                var n = new Npc { Def = def, Given = given, HomeX = home, X = home, Dir = Random.Range(0, 2) == 0 ? -1 : 1, Ai1 = Random.Range(60, 300) };
                n.FeetY = Ground(n.X) ?? benchY;
                if (placed)
                {
                    if (onChair)
                    {
                        n.FeetY = seatY;
                        HomeY[def.Internal] = seatY;   // an older save may hold the chair's base
                    }
                    else if (GridGround(n.X, putY + 0.5f, out float putGround)) n.FeetY = putGround;
                }
                n.LastX = n.X;
                // Where it lives says how far it may wander: a chair the player set it on keeps it close,
                // everything else (the town's own bench) is one spot for everybody and roams the street.
                n.Range = World.Stations.ChairNear(new Vector2(n.HomeX, n.FeetY + 0.3f), out _, out _) ? ChairRange : RoamRange;
                npcs.Add(n);
                i++;
                if (Arrived.Add(def.Internal) && !fresh && def.Kind != NpcKind.Visitor)
                {
                    // Main.NewText(Language.GetTextValue("Announcement.HasArrived", FullName), 50, 125, 255): in the chat at the bottom left.
                    string line = Terraria.TerrariaText.Get(Lang.Culture, "Announcement.HasArrived");
                    Net.NetChat.Add(line != null ? line.Replace("{0}", n.Name) : Lang.F("{0}已到达！", n.Name), new Color(50 / 255f, 125 / 255f, 255 / 255f));
                    Plugin.Log.LogInfo($"[城镇] {n.Name} 已到达");
                }
            }
            if (fresh) foreach (var n in npcs) Arrived.Add(n.Def.Internal);
        }

        /// <summary>
        /// Homes alternate left and right of the bench, past the pylon beside it: room for all forty.
        /// </summary>
        static List<float> StreetHomes()
        {
            var homes = new List<float>();
            for (int k = 0; homes.Count < 40 && k < 120; k++)
            {
                float off = 5f + 3f * (k / 2);
                float x = benchX + (k % 2 == 0 ? -off : off + 2f);
                if (x > streetMin + 1f && x < streetMax - 1f) homes.Add(x);
            }
            // No run read round the bench at all (the player has built the street over, or the survey was
            // beaten by something of theirs): still give them their spots either side of it, a little
            // closer in, so the townsfolk who move in by themselves have somewhere to arrive.
            if (homes.Count == 0)
                for (int k = 0; homes.Count < 40 && k < 120; k++)
                {
                    float off = 2.5f + 2f * (k / 2);
                    homes.Add(benchX + (k % 2 == 0 ? -off : off + 2f));
                }
            return homes;
        }

        /// <summary>
        /// The near-foreground silhouettes in front of the street (rocks and grass, well in front of
        /// the gameplay plane) were laid out for Hollow Knight's camera, held high over Dirtmouth,
        /// where they make the bottom of its view. This camera keeps the player in the middle,
        /// which lifts them over the street, and they would black out the townsfolk: those
        /// standing up to a few tiles above the street go while the town is loaded.
        /// </summary>
        static void HideStreetForeground(UnityEngine.SceneManagement.Scene scene, float streetY)
        {
            int hidden = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(false))
                {
                    if (!sr.enabled) continue;
                    var b = sr.bounds;
                    if (b.center.z > -3f || b.max.y <= streetY - 0.5f || b.min.y >= streetY + 4f) continue;
                    if (b.max.x < streetMin - 8f || b.min.x > streetMax + 8f) continue;
                    sr.enabled = false;
                    hidden++;
                }
            Plugin.Log.LogInfo($"[城镇] 街道前方的近景剪影藏起 {hidden} 个（街道 {streetMin:F1}..{streetMax:F1}，地面 y={streetY:F1}）");
        }

        /// <summary>How far the street runs either side of the bench without a drop or a wall.</summary>
        static void Survey()
        {
            float y0 = Ground(benchX) ?? benchY;
            streetMin = streetMax = benchX;
            foreach (int dir in new[] { -1, 1 })
            {
                float last = y0, x = benchX;
                for (int k = 1; k <= 120; k++)
                {
                    float nx = benchX + dir * k * 0.5f;
                    var g = Ground(nx);
                    if (g == null || Mathf.Abs(g.Value - last) > 0.7f) break;
                    last = g.Value;
                    x = nx;
                }
                if (dir < 0) streetMin = x;
                else streetMax = x;
            }
        }

        static readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        static readonly ContactFilter2D terrain = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << ChunkColliders.TerrainLayer };

        /// <summary>The ground's height under a point of the street (null where there's none near).</summary>
        static float? Ground(float x)
        {
            // The cell grid first: it holds the terrain the player walks on — the street as it ships, and
            // the player's own blocks the moment they are laid — and nothing that merely stands on the
            // street. A ray from above hits the first of those props to come along: at the very bench the
            // town is laid out from it finds the bench's own collider (0.7 tiles above the street, which
            // is why the street used to read as 0 tiles long and every townsperson stood still), and a
            // floor built over the street would hide the street under it.
            var cell = CellGrid.WorldToCell(new Vector2(x, benchY));
            for (int i = 0; i < 80; i++)
            {
                int cy = cell.y - i;
                if (TerraWorld.RegionAt(cell.x, cy) == null || (!TerraWorld.IsSolid(cell.x, cy) && !TerraWorld.IsPlatform(cell.x, cy))) continue;
                return CellGrid.CellCenter(cell.x, cy).y + CellGrid.CellSize / 2f;
            }
            // Then the scene's own floor where the grid holds none (kept as it is), the exact contact the
            // ray gives — from above the bench down past it, ignoring what stands higher than the street.
            int n = Physics2D.Raycast(new Vector2(x, benchY + 3f), Vector2.down, terrain, hits, 10f);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider.usedByEffector) continue;
                if (hits[i].point.y > benchY + 0.4f) continue;
                return hits[i].point.y;
            }
            return null;
        }

        // ------------------------------------------------------------------ AI_007 (simplified)

        static void Step(Npc n, HeroController hero)
        {
            int talkDir = 0;
            if (n == talk)
            {
                // Talked to: stand, face the player, and don't wander off for a while after.
                n.Ai0 = 0;
                n.Ai1 = 300;
                n.LocalAi3 = 100;
                talkDir = hero.transform.position.x >= n.X ? 1 : -1;
            }
            if (n.Ai0 == 0)
            {
                if (n.Vx > 0.1f) n.Vx -= 0.1f;
                else if (n.Vx < -0.1f) n.Vx += 0.1f;
                else n.Vx = 0f;
                if (talkDir != 0) n.Dir = talkDir;
                if (n.Ai1 > 0f) n.Ai1--;
                if (n.LocalAi3 > 0f) n.LocalAi3--;
                if (n.Ai1 <= 0f && talkDir == 0)
                {
                    if (Safe(n, n.Dir))
                    {
                        n.Ai0 = 1;
                        n.Ai1 = 200 + Random.Range(0, 300);
                        n.StuckTicks = 0;
                    }
                    else
                    {
                        n.Dir = -n.Dir;
                        n.Ai1 = 60 + Random.Range(0, 120);
                    }
                }
                if (talkDir == 0)
                {
                    // Its own range, not a fixed six: everyone Dirtmouth's bench is home to shares that
                    // one spot, so they have to be free to walk the street apart (Safe's own limit).
                    if (Mathf.Abs(n.X - n.HomeX) > n.Range) n.Dir = n.X < n.HomeX ? 1 : -1;
                    else if (n.LocalAi3 <= 0f && Random.Range(0, 80) == 0)
                    {
                        n.Dir = -n.Dir;
                        n.LocalAi3 = 200;
                    }
                }
            }
            else
            {
                if (Mathf.Abs(n.Vx) > 1f) n.Vx *= 0.8f;
                else n.Vx = Mathf.Clamp(n.Vx + 0.07f * n.Dir, -1f, 1f);
                n.Ai1--;
                if (Mathf.Abs(n.X - n.HomeX) > n.Range && (n.X - n.HomeX) * n.Dir > 0f) n.Ai1 -= 5f;
                if (n.Ai1 <= 0f)
                {
                    n.Ai0 = 0;
                    n.Ai1 = 300 + Random.Range(0, 300) + Random.Range(0, 900);
                    n.LocalAi3 = 60;
                }
                else if (!Safe(n, n.Dir))
                {
                    // A drop or a wall ahead: stop and stand a moment.
                    n.Ai0 = 0;
                    n.Ai1 = 50 + Random.Range(0, 50);
                    n.Vx = 0f;
                }
            }
            n.X += n.Vx * Px;
            // NPC.AI_007's closeDoor: two tiles past the door it opened, it shuts it (given up four tiles on).
            if (n.ShutDoor)
            {
                float away = Mathf.Abs(n.X - n.DoorAt.x);
                if (away > 2f * CellGrid.CellSize && (World.Stations.NpcShutDoor(n.DoorAt) || away > 4f * CellGrid.CellSize)) n.ShutDoor = false;
            }
            var g = StandingFloor(n.X, n.FeetY, n);
            if (g.HasValue) n.FeetY = Mathf.MoveTowards(n.FeetY, g.Value, 0.25f);
            // Stuck for half a second while walking: turn round.
            if (n.Ai0 == 1 && Mathf.Abs(n.X - n.LastX) < 0.001f && ++n.StuckTicks >= 30)
            {
                n.Dir = -n.Dir;
                n.StuckTicks = 0;
            }
            n.LastX = n.X;
            // NPC.FindFrame for town NPCs on the ground: 0 standing, 2.. walking.
            int frames = FramesOf(n.Def), extra = n.Def.Extra > 0 ? n.Def.Extra : ExtraFrames(n.Def.Id);
            if (n.Vx == 0f)
            {
                n.Frame = 0;
                n.FrameCounter = 0f;
            }
            else
            {
                n.FrameCounter += Mathf.Abs(n.Vx) * 2f + 1f;
                if (n.Frame < 2) n.Frame = 2;
                if (n.FrameCounter > 6f)
                {
                    n.FrameCounter = 0f;
                    n.Frame++;
                }
                if (n.Frame >= frames - extra) n.Frame = 2;
            }
        }

        /// <summary>The next step along stays on the street, near home, clear of walls.</summary>
        static bool Safe(Npc n, int dir)
        {
            float nx = n.X + dir * 0.6f;
            // The street's own run; when the survey read none (the player has built the street over), the
            // range round home instead of a run of zero length, which would pin everyone in place.
            float lo = streetMin + 0.5f, hi = streetMax - 0.5f;
            // One with a house walks its room's floor (TownNpcsHousing).
            if (HouseSpan(n, out float roomLo, out float roomHi)) { lo = roomLo; hi = roomHi; }
            else if (hi - lo < 4f) { lo = n.HomeX - n.Range; hi = n.HomeX + n.Range; }
            if (nx < lo || nx > hi || Mathf.Abs(nx - n.HomeX) > n.Range) return false;
            var g = StandingFloor(nx, n.FeetY, n);
            if (g == null || Mathf.Abs(g.Value - n.FeetY) > 0.7f) return false;
            int c = Physics2D.Raycast(new Vector2(n.X, n.FeetY + 0.6f), new Vector2(dir, 0f), terrain, hits, 0.7f);
            for (int i = 0; i < c; i++)
            {
                if (hits[i].collider.usedByEffector) continue;
                // NPC.AI_007: a shut door at head height ahead is opened the way it walks (else the other way) and walked through.
                if (World.Stations.NpcOpenDoor(hits[i].collider, dir, out var doorway))
                {
                    n.ShutDoor = true;
                    n.DoorAt = doorway;
                    n.Ai1 += 80f;
                    continue;
                }
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>Each townsperson: its profile texture's frame, bottom 4 px under the feet, flipped to face right (the art faces left), lit.</summary>
        public static void Draw()
        {
            var fx = FxBatch.Instance;
            // The housing menu's banners over each home (Main.DrawNPCHousesInWorld, TownNpcsHousing).
            DrawHouseBanners(fx);
            if (fx == null || npcs.Count == 0) return;
            Vector2? mouse = TerraMod.MouseWorld(out var m) ? m : (Vector2?)null;
            foreach (var n in npcs)
            {
                string tex = TextureOf(n.Def);
                var t = Terraria.TerrariaAssets.Texture(tex);
                int frames = FramesOf(n.Def), fh = t.height / frames;
                var feet = TerraParticles.ToPx(new Vector2(n.X, n.FeetY)) + new Vector2(0f, AddHeight(n.Def.Id));
                fx.Draw(FxBatch.Frame(tex, 0, fh * n.Frame, t.width, fh, t.width / 2f, fh - 4f), feet, new Color(L, L, L, 1f), 0f, 1f, flipX: Facing(n) == 1, town: true);
                // The chat bubble over a townsperson the cursor is on and the player can reach.
                var hero = HeroController.SilentInstance;
                // Or the one the smart cursor picked (SmartInteract), as Main.HoverOverNPCs takes it.
                if (mouse.HasValue && hero != null && n != talk && (FrameRect(n).Contains(mouse.Value) || SmartPicked(n)) && InReach(n, hero))
                {
                    var head = TerraParticles.ToPx(new Vector2(n.X, n.FeetY)) + new Vector2((n.Dir == 1 ? 1f : -1f) * 17f, -40f - 13f);
                    fx.Draw(FxBatch.Whole("Chat"), head, Color.white, 0f, 1f, flipX: n.Dir != 1, overPlayer: true);
                    hovered = n;
                }
            }
        }

        static Npc hovered;

        /// <summary>The name under the cursor (Main.HoverOverNPCs shows GivenOrTypeName).</summary>
        public static string HoverName()
        {
            var h = hovered;
            hovered = null;
            // A house banner under the cursor says whose it is (Lang.GetNPCHouseBannerText).
            string banner = BannerHoverText();
            return h?.Name ?? banner;
        }

        /// <summary>
        /// The way a townsperson is drawn facing: where it walks or looks, and on its chair's seat at rest the way the chair
        /// faces (PlayerSittingHelper's direction for an NPC sat on tile 15: the chair's frameX).
        /// </summary>
        static int Facing(Npc n)
        {
            if (n != talk && n.Ai0 == 0 && World.Stations.ChairNear(new Vector2(n.X, n.FeetY), out float chairX, out float seat, out int chairFace)
                && Mathf.Abs(seat - n.FeetY) < 0.05f && Mathf.Abs(chairX - n.X) < 0.3f) return chairFace;
            return n.Dir;
        }

        /// <summary>Dev: the way a townsperson is drawn facing now (0: not there).</summary>
        public static int DevFacing(string internalName)
        {
            var n = npcs.Find(v => v.Def.Internal == internalName);
            return n == null ? 0 : Facing(n);
        }

        /// <summary>A townsperson's body (Terraria's 18x40 town NPC box at its feet) is in a box: Collision.EmptyTile's look at the NPCs (a door doesn't shut on one).</summary>
        public static bool AnyIn(Rect box)
        {
            foreach (var n in npcs)
                if (new Rect(n.X - 9f * Px, n.FeetY, 18f * Px, 40f * Px).Overlaps(box)) return true;
            return false;
        }

        /// <summary>Dev: the doorway of the door a townsperson means to shut (none: null).</summary>
        public static Vector2? DevDoorOf(string internalName)
        {
            var n = npcs.Find(v => v.Def.Internal == internalName);
            return n != null && n.ShutDoor ? n.DoorAt : (Vector2?)null;
        }

        /// <summary>Dev: a townsperson on the street set walking from x that way (dir) for ten seconds, or (dir 0) standing there as long; false when it isn't there.</summary>
        public static bool DevSend(string internalName, float x, int dir)
        {
            var n = npcs.Find(v => v.Def.Internal == internalName);
            if (n == null) return false;
            n.X = n.LastX = x;
            var g = StandingFloor(x, n.FeetY + 0.3f, n);
            if (g.HasValue) n.FeetY = g.Value;
            if (dir != 0) n.Dir = dir;
            n.Ai0 = dir != 0 ? 1 : 0;
            n.Ai1 = 600f;
            n.LocalAi3 = 600f;
            n.Vx = 0f;
            n.StuckTicks = 0f;
            return true;
        }

        /// <summary>Dev: Dirtmouth's bench (x, y) the street is read from, and the street's run either side.</summary>
        public static Vector4 DevStreet => new Vector4(benchX, benchY, streetMin, streetMax);

        static Rect FrameRect(Npc n)
        {
            var t = Terraria.TerrariaAssets.Texture(TextureOf(n.Def));
            float w = t.width * Px, h = t.height / (float)FramesOf(n.Def) * Px;
            return new Rect(n.X - w / 2f, n.FeetY - 4f * Px, w, h);
        }

        /// <summary>TileReachCheckSettings.Simple round the player: 5 tiles beyond him sideways, 3 up and down.</summary>
        static bool InReach(Npc n, HeroController hero)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            float dx = Mathf.Max(0f, Mathf.Abs(n.X - b.center.x) - b.extents.x - 9f * Px);
            float dy = Mathf.Max(0f, Mathf.Abs(n.FeetY + 20f * Px - b.center.y) - b.extents.y - 20f * Px);
            return dx <= 5f * 16f * Px && dy <= 3f * 16f * Px;
        }

        // ------------------------------------------------------------------ talking

        /// <summary>A right click in the world: on a townsperson in reach, the chat opens (sound Chat). True when taken.</summary>
        public static bool TryRightClick(Vector2 mouseWorld)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            // A right click on a house banner with the housing menu up turns that townsperson out (WorldGen.kickOut).
            if (BannerRightClick(mouseWorld)) return true;
            // Several under the cursor (a crowded street): the one nearest it.
            Npc pick = null;
            foreach (var c in npcs)
                if (FrameRect(c).Contains(mouseWorld) && InReach(c, hero) && (pick == null || Mathf.Abs(c.X - mouseWorld.x) < Mathf.Abs(pick.X - mouseWorld.x))) pick = c;
            if (pick == null) return false;
            Talk(pick);
            return true;
        }

        /// <summary>The talk with a townsperson opens (sound Chat); already talking to it, nothing more.</summary>
        static void Talk(Npc n)
        {
            if (talk == n) return;
            ReturnGuideItem();
            ReturnReforgeItem();
            talk = n;
            shop = false;
            guideCraft = false;
            reforge = false;
            forge = false;
            ChatText = Chat(n);
            TerraMod.Instance?.PlaySound("Chat", 0.8f);
        }

        /// <summary>Main.OpenShop: the inventory opens, the chat text goes, the stock is set up anew.</summary>
        public static void OpenShop()
        {
            if (talk == null || talk.Def.Items.Length == 0) return;
            shop = true;
            RecoveryShop = false;
            ChatText = "";
            InventoryGui.Open = true;
            SetupShop(talk.Def);
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
        }

        /// <summary>Main.NPCChatText_GuideReverseCrafting: the inventory opens, the chat text goes, the Guide's slot shows.</summary>
        public static void OpenGuideCraft()
        {
            if (!TalkingIsGuide) return;
            guideCraft = true;
            ChatText = "";
            InventoryGui.Open = true;
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
        }

        /// <summary>The Goblin Tinkerer's reforge slot (Main.reforgeItem) and whether his reforging is open (Main.InReforgeMenu).</summary>
        public static ItemStack ReforgeItem;
        static bool reforge, forge, refit;
        public static bool RefitMode => ReforgeOpen && refit;
        public static bool ReforgeOpen => talk != null && reforge && InventoryGui.Open;
        /// <summary>His slot open for forging (Items.Forging) rather than reforging.</summary>
        public static bool ForgeMode => ReforgeOpen && forge;
        public static bool TalkingIsGoblin => talk != null && talk.Def.Id == 107;

        /// <summary>His "Reforge" button: the inventory opens, the chat text goes, the reforge slot shows.</summary>
        public static void OpenReforge()
        {
            if (!TalkingIsGoblin) return;
            reforge = true;
            forge = false;
            refit = false;
            ChatText = "";
            InventoryGui.Open = true;
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
        }

        /// <summary>His "Forge" button: the same slot, for a weapon to forge (Items.Forging).</summary>
        public static void OpenForge()
        {
            if (!TalkingIsGoblin) return;
            reforge = true;
            forge = true;
            refit = false;
            ChatText = "";
            InventoryGui.Open = true;
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
        }

        /// <summary>Player.dropItemCheck for the reforge slot: back into the bag whole (its prefix kept), else at the player's feet. Also before every save.</summary>
        public static void OpenRefit()
        {
            if (!TalkingIsGoblin) return;
            ReturnReforgeItem();
            reforge = refit = true; forge = false; ChatText = "";
            InventoryGui.Open = true;
            TerraMod.Instance?.PlaySound("Menu_Tick", .7f);
        }
        internal static void ReturnReforgeItem()
        {
            if (ReforgeItem.IsEmpty) return;
            var stack = ReforgeItem;
            ReforgeItem = default;
            var inv = TerraMod.Instance?.Inventory;
            int left = inv != null ? inv.Pickup(stack, stack.Favorite) : stack.Count;
            if (left <= 0) return;
            stack.Count = left;
            var hero = HeroController.SilentInstance;
            if (hero != null) WorldItems.Drop(stack, hero.GetComponent<BoxCollider2D>().bounds.center);
        }

        /// <summary>
        /// Player.dropItemCheck for the Guide's slot: back into the bag, or dropped at the player's
        /// feet if it won't fit. Also before every save (ModSave.Write), so a save never misses it.
        /// </summary>
        internal static void ReturnGuideItem()
        {
            if (GuideItem.IsEmpty) return;
            var stack = GuideItem;
            GuideItem = default;
            var inv = TerraMod.Instance?.Inventory;
            // A reforged or forged one goes back whole, its prefix and level kept.
            bool whole = stack.Prefix != 0 || stack.Forge != 0;
            int left = inv == null ? stack.Count : whole ? inv.Pickup(stack) : inv.AddSome(stack.Type, stack.Count);
            if (left <= 0) return;
            var hero = HeroController.SilentInstance;
            var rest = stack;
            rest.Count = left;
            if (hero != null) WorldItems.Drop(rest, hero.GetComponent<BoxCollider2D>().bounds.center);
        }

        /// <summary>Main.CloseNPCChatOrSign (sound Menu_Close).</summary>
        public static void Close(bool sound)
        {
            if (talk == null) return;
            ReturnGuideItem();
            ReturnReforgeItem();
            talk = null;
            shop = false;
            guideCraft = false;
            reforge = false;
            forge = false;
            ChatText = "";
            for (int i = 0; i < ShopItems.Length; i++)
            {
                ShopItems[i] = default;
                BuyBack[i] = false;
                BuyBackPrice[i] = 0;
                ShopLock[i] = 0;
                ShopSpell[i] = 0;
            }
            if (sound) TerraMod.Instance?.PlaySound("Menu_Close", 0.7f);
        }

        /// <summary>
        /// Chest.SetupShop: the lines whose stage and moon phase hold, in order; then, in the slots left, what the seller
        /// brings at the later stages, from the nearest stage on, locked (<see cref="ShopLock"/>): every stock to look at
        /// from the start, with the stage it opens at.
        /// </summary>
        public static bool RecoveryShop { get; private set; }
        public static void OpenRecoveryShop()
        {
            if (!TalkingIsGoblin) return;
            shop = true; RecoveryShop = true; ChatText = "";
            InventoryGui.Open = true;
            SetupShop(talk.Def);
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
        }

        static void SetupShop(NpcDef def)
        {
            for (int i = 0; i < ShopItems.Length; i++)
            {
                ShopItems[i] = default;
                BuyBack[i] = false;
                BuyBackPrice[i] = 0;
                ShopLock[i] = 0;
                ShopSpell[i] = 0;
            }
            int stage = Shop.UnlockedStage(), phase = MoonPhase, k = 0;
            if (RecoveryShop && def.Id == 107)
            {
                foreach (var type in HallownestRecovery.Stock(TerraMod.Instance?.Inventory))
                {
                    if (k >= ShopItems.Length) throw new System.InvalidOperationException("Recovery shop capacity exceeded");
                    ShopItems[k++] = ItemStack.Of(type);
                }
                return;
            }
            var later = new List<(ItemType Type, int Stage)>();
            var spells = new List<(ItemType Type, int Spell)>();
            var shown = new HashSet<ItemType>();
            foreach (var s in ProgressionBalance.ShopStock(def.Internal,def.Items))
            {
                if (s.Phases != null && System.Array.IndexOf(s.Phases, phase) < 0) continue;
                // A spell's weapon: by its spell, not by stage.
                int spell = SpellRewards.SpellOf(s.Type);
                if (spell > 0 && !SpellRewards.OnSale(s.Type))
                {
                    spells.Add((s.Type, spell));
                    continue;
                }
                if (spell == 0 && (s.Stage > stage || !ProgressionBalance.Unlocked(s.Type)))
                {
                    int at = Mathf.Max(s.Stage, ItemDb.Get(s.Type).Stage, stage + 1);
                    if (at <= World.Areas.MaxLevel) later.Add((s.Type, at));
                    continue;
                }
                if (k >= ShopItems.Length) break;
                ShopItems[k++] = ItemStack.Of(s.Type);
                shown.Add(s.Type);
            }
            // The later stages' looks only in the four rows, or in the fifth when the stock on sale already reaches it.
            int lookLimit = k > ShopBaseSlots ? ShopItems.Length : ShopBaseSlots;
            // The spells' weapons not yet absorbed first, by the spells' order, beside those on sale.
            foreach (var l in spells)
            {
                if (k >= lookLimit) break;
                if (!shown.Add(l.Type)) continue;
                ShopSpell[k] = l.Spell;
                ShopItems[k++] = ItemStack.Of(l.Type);
            }
            // Stable by stage: the next stage's first, each stage's in the stock's order.
            var order = new Dictionary<ItemType, int>();
            for (int i = 0; i < later.Count; i++) if (!order.ContainsKey(later[i].Type)) order[later[i].Type] = i;
            later.Sort((a, b) => a.Stage != b.Stage ? a.Stage.CompareTo(b.Stage) : order[a.Type].CompareTo(order[b.Type]));
            foreach (var l in later)
            {
                if (k >= lookLimit) break;
                if (!shown.Add(l.Type)) continue;
                ShopLock[k] = l.Stage;
                ShopItems[k++] = ItemStack.Of(l.Type);
            }
        }

        public static bool Locked(int slot) => slot >= 0 && slot < ShopLock.Length && (ShopLock[slot] > 0 || ShopSpell[slot] > 0) && !ShopItems[slot].IsEmpty;

        /// <summary>Buys from a shop slot onto the cursor (as many as count allows); false when nothing was bought.</summary>
        public static bool Buy(int slot, int count, Inventory inv)
        {
            if (!ShopOpen || slot < 0 || slot >= ShopItems.Length || ShopItems[slot].IsEmpty) return false;
            if (Locked(slot)) return false;
            if(!BuyBack[slot]&&ShopItems[slot].Type==ItemType.RandomPrefabHouse)return PrefabHouses.Buy(inv,count);
            if (RecoveryShop && !BuyBack[slot])
            {
                if (!HallownestRecovery.CanBuy(inv, ShopItems[slot].Type)) return false;
            }
            else if (!BuyBack[slot] && !SpellRewards.OnSale(ShopItems[slot].Type)) return false;
            int bought = 0;
            for (int k = 0; k < count; k++)
            {
                var item = ShopItems[slot];
                if (item.IsEmpty) break;
                if (!(inv.Mouse.IsEmpty || (inv.Mouse.Type == item.Type && inv.Mouse.Count < item.Def.MaxStack))) break;
                long price = BuyPrice(slot);
                // Can't afford it: nothing happens at all (Player.BuyItem).
                if (!Shop.Spend(price)) break;
                if (inv.Mouse.IsEmpty) inv.Mouse = new ItemStack { Type = item.Type, Prefix = item.Prefix, Forge = item.Forge, WeaponVersion = item.WeaponVersion };
                inv.Mouse.Count++;
                HkItems.Trade(inv, item.Type, 1);
                HallownestRecovery.Record(inv, item.Type);
                bought++;
                Achievements.AchievementTracker.Bought(price);
                if (BuyBack[slot])
                {
                    ShopItems[slot].Count--;
                    if (ShopItems[slot].Count <= 0)
                    {
                        ShopItems[slot] = default;
                        BuyBack[slot] = false;
                        BuyBackPrice[slot] = 0;
                    }
                }
                else Bought(item, price);
            }
            if (bought > 0) TerraMod.Instance?.PlaySound(ItemDb.Get(inv.Mouse.Type).Value > 0 ? "Coins" : "Grab", 0.7f);
            return bought > 0;
        }

        /// <summary>What a shop slot costs apiece, in copper: the value, or a fifth of it to buy back.</summary>
        public static long BuyPrice(int slot)
        {
            var item = ShopItems[slot];
            if (item.IsEmpty) return 0;
            if(!BuyBack[slot]&&item.Type==ItemType.CasinoKit)return Casino.HousePrice;
            if(!BuyBack[slot]&&item.Type==ItemType.RandomPrefabHouse)return PrefabHouses.Price;
            if(!BuyBack[slot]&&item.Type==ItemType.SimplePrefabHouse)return PrefabHouses.SimplePrice;
            // The Discount Card (GetItemExpectedPrice, Pl:35648-35657): four fifths, as worn when the shop opened.
            if (BuyBack[slot]) return BuyBackPrice[slot];
            long price = RecoveryShop ? HallownestRecovery.Price(item.Def) : Shop.PriceOf(item.Def);
            return DiscountLatched ? (long)(price * 0.8f) : price;
        }

        /// <summary>One bought from the stock, at a price (ItemShopSellbackHelper.Add).</summary>
        static void Bought(ItemStack item, long price)
        {
            for (int i = 0; i < bought.Count; i++)
            {
                var b = bought[i];
                if (b.Type != item.Type || b.Prefix != item.Prefix || b.Forge != item.Forge || b.Price != price) continue;
                bought[i] = (b.Type, b.Prefix, b.Forge, b.Count + 1, b.Price);
                return;
            }
            bought.Add((item.Type, item.Prefix, item.Forge, 1, price));
        }

        /// <summary>
        /// Of a stack being sold, how many were bought this session and what they cost together (taken off the record):
        /// they fetch their price again (Player.SellItem with the helper's amount), the dearest first.
        /// </summary>
        static (int count, long paid) Refund(ItemStack stack)
        {
            int left = stack.Count, count = 0;
            long paid = 0;
            while (left > 0)
            {
                int best = -1;
                for (int i = 0; i < bought.Count; i++)
                {
                    var b = bought[i];
                    if (b.Type == stack.Type && b.Prefix == stack.Prefix && b.Forge == stack.Forge && (best < 0 || b.Price > bought[best].Price)) best = i;
                }
                if (best < 0) break;
                var m = bought[best];
                int n = System.Math.Min(left, m.Count);
                count += n;
                paid += m.Price * n;
                left -= n;
                if (m.Count - n <= 0) bought.RemoveAt(best);
                else bought[best] = (m.Type, m.Prefix, m.Forge, m.Count - n, m.Price);
            }
            return (count, paid);
        }

        /// <summary>Dev: every shop's every item (any stage, any moon) whose icon doesn't load or is blank.</summary>
        public static string DevBadIcons(out int checkedCount)
        {
            checkedCount = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var def in All)
                foreach (var st in def.Items)
                {
                    checkedCount++;
                    var d = ItemDb.Get(st.Type);
                    string why = null;
                    if (d == null || string.IsNullOrEmpty(d.Texture)) why = "没有贴图名";
                    else
                    {
                        try
                        {
                            var tex = Terraria.TerrariaAssets.Texture(d.Texture);
                            if (tex == null || tex.width <= 1 || tex.height <= 1) why = "贴图是空的";
                        }
                        catch (System.Exception e) { why = "读不出 " + d.Texture + "：" + e.GetType().Name; }
                    }
                    if (why != null) sb.Append($" {def.Zh}:{st.Type}（{why}）");
                }
            return sb.ToString();
        }

        /// <summary>discountAvailable: whether the Discount Card was worn as the shop opened.</summary>
        public static bool DiscountLatched;

        /// <summary>Player.SellItem: the stack for a fifth of its value apiece, into the shop's buy-back slots (Chest.AddItemToShop).</summary>
        public static void Sell(ref ItemStack stack)
        {
            // Money isn't sold.
            if (!ShopOpen || stack.IsEmpty || stack.Favorite || stack.Def.CannotSell || Currencies.Is(stack.Type)) return;
            HallownestRecovery.Record(TerraMod.Instance?.Inventory, stack.Type);
            HkItems.Trade(TerraMod.Instance?.Inventory, stack.Type, -stack.Count);
            long money = Shop.SellValue(stack);
            long each = stack.Count > 0 ? money / stack.Count : 0;
            // Bought here this session: those fetch what they cost, and go back to the stock rather than a buy-back slot
            // (Chest.AddItemToShop takes them off the helper first).
            var (refunded, paid) = Refund(stack);
            if (refunded > 0) money += paid - each * refunded;
            Shop.Earn(money);
            int keep = stack.Count - refunded;
            if (keep <= 0)
            {
                stack = default;
                TerraMod.Instance?.PlaySound(money > 0 ? "Coins" : "Grab", 0.7f);
                return;
            }
            int at = -1;
            int last = ShopItems.Length - 1;
            for (int i = 0; i < last; i++)
                if (BuyBack[i] && ShopItems[i].Type == stack.Type && ShopItems[i].Prefix == stack.Prefix && ShopItems[i].Forge == stack.Forge
                    && BuyBackPrice[i] == each) { at = i; break; }
            if (at < 0)
                for (int i = 0; i < last; i++)
                    if (ShopItems[i].IsEmpty) { at = i; break; }
            // A full shop: the last of the later stages' looks gives up its slot.
            if (at < 0)
                for (int i = last - 1; i >= 0; i--)
                    if (Locked(i)) { ShopItems[i] = default; ShopLock[i] = 0; ShopSpell[i] = 0; at = i; break; }
            if (at >= 0)
            {
                if (ShopItems[at].IsEmpty) ShopItems[at] = new ItemStack { Type = stack.Type, Prefix = stack.Prefix, Forge = stack.Forge, WeaponVersion = stack.WeaponVersion };
                ShopItems[at].Count += keep;
                BuyBack[at] = true;
                // Bought back for exactly what each fetched (Terraria's buyOnce price: the sale's fifth).
                BuyBackPrice[at] = each;
            }
            stack = default;
            TerraMod.Instance?.PlaySound(money > 0 ? "Coins" : "Grab", 0.7f);
        }

        // ------------------------------------------------------------------ data

        static string Chat(Npc n)
        {
            if (n.Def.Internal == "Angler") return Angler.Chat(n.Name);
            if (n.Def.Chat != null && n.Def.Chat.Length > 0) return ChatOf(n);
            var lines = Dialog(n.Def.Internal);
            string line = Lang.T(lines[Random.Range(0, lines.Length)]).Replace("{PlayerName}", Lang.T("泰拉人"));
            // Any new movement ability opens the next stage, regardless of acquisition order.
            int next = Shop.UnlockedStage() + 1;
            if (n.Def.Internal == "Guide" && next <= World.Areas.MaxLevel)
                line += "\n" + Lang.F("再取得任意一种尚未获得的移动能力，就能进入阶段 {0}，让更多人搬来、商店上新货、工作站开放更多配方。", next)
                    + "\n" + Lang.T("计入阶段的能力：蛾翼披风、螳螂爪、水晶之心、帝王之翼、暗影披风。每种只计算一次，顺序不限。");
            return line;
        }

        static string[] Dialog(string npc)
        {
            switch (npc)
            {
                case "Guide": return new[] { "嗨，{PlayerName}。有什么可以帮你？", "我的工作是为你接下来的任务提供建议。建议你遇到任何困难时都来和我谈谈。", "他们说，有个人会告诉你如何在这地方上生存……哦等下。那个人就是我。" };
                case "Merchant": return new[] { "看看我的土块；它们特别土。", "太阳很高，但我的价格可不高。", "这里的最后一个人给我留了些垃圾……呃，我是说……宝藏！", "不知道月亮是不是奶酪做的……什么？对了，买点东西！", "你是说黄金？交给我吧。" };
                case "ArmsDealer": return new[] { "我看你在盯着迷你鲨……你绝对想不到它是怎么做成的。", "嘿，兄弟，这可不是演电影。弹药得另算。", "哥们，把手从我的枪上拿开！" };
                case "Demolitionist": return new[] { "炸药如今十分火爆。马上买一些！", "今天是个找死的好日子！", "雷管，这是我特别为你准备的灵丹妙药，包治百病。", "看看我的商品；都是惊爆价！", "想玩爆炸试胆吗？", "此处禁止吸烟！！" };
                case "GoblinTinkerer": return new[] { "老实说，大部分哥布林都不是真正的火箭科学家。好吧，有一些是。", "哥布林盗贼不太擅长偷东西。没上锁的箱子都不会偷！", "唷，我听说你喜欢火箭和跑鞋，所以我在你的跑鞋上加了一些火箭。", "沉默是金。导管胶布是银。" };
                case "WitchDoctor": return new[] { "我是啥医？我是巫医。", "魔法的心脏是本质。心脏的本质是魔法。", "认真选，{PlayerName}，我的商品不稳定，我的黑魔法很神秘。" };
                case "SkeletonMerchant": return new[] { "还有多余的骨头卖吗？我的髋骨断了，我想再……换一次。", "你还真有骨气啊，大老远跑到这来，来都来了，要不买点什么？", "你肯定想不到大家扔了些什么给我……想买点？", "这个世界上永恒不变的就只有死亡和税收，我两样都有！", "鸡骨头？干嘛问这个，你需要吗？" };
                case "BestiaryGirl": return new[] { "我可能学识浅薄……但有关自然、小动物、动物和野生动物之类的话题，我可以给你说上三天三夜……", "噢，这两只大耳朵吗？哈哈，是用来听你的，宝贝！", "我不知道我是怎么来到这里的，但这感觉太棒了。" };
                case "PartyGirl": return new[] { "我们得谈谈。关于……聚会的事。", "我不知道我是更喜欢派对还是余兴派对。", "装个迪斯科球，我会让你知道怎么开派对。" };
                case "Steampunker": return new[] { "欧嘿，可有宝物否？", "不管怎样，喷气背包和你很搭！", "来点齿轮让我瞧瞧！", "我喜欢你的...装备。有黄铜做的吗？" };
                case "Cyborg": return new[] { "射弹击中我的运动执行机构会严重降低我的探险效率。", "这句话是假的，是不是？", "有时候我有一点……懂了吗？有点？" };
                default: return new[] { "……" };
            }
        }

        /// <summary>The given-name pools (NPC.getNewNPCNameInner; zh keeps the Latin names).</summary>
        static string[] GivenNames(string npc)
        {
            foreach (var d in more)
                if (d.Internal == npc) return d.Names != null && d.Names.Length > 0 ? d.Names : new[] { "" };
            switch (npc)
            {
                case "Guide": return new[] { "Joe", "Connor", "Tanner", "Wyatt", "Cody", "Levi", "Luke", "Jack", "Scott", "Logan", "Cole", "Asher", "Bradley", "Jacob", "Garrett", "Dylan", "Maxwell", "Steve", "Brett", "Andrew" };
                case "Merchant": return new[] { "Alfred", "Barney", "Calvin", "Edmund", "Edwin", "Eugene", "Frank", "Frederick", "Gilbert", "Gus", "Wilbur", "Seymour", "Louis", "Humphrey", "Harold", "Milton", "Mortimer", "Howard", "Walter", "Isaac", "Joseph", "Kristian", "Ralph" };
                case "ArmsDealer": return new[] { "DeShawn", "DeAndre", "Marquis", "Darnell", "Terrell", "Malik", "Trevon", "Tyrone", "Willie", "Dominique", "Demetrius", "Reginald", "Jamal", "Maurice", "Jalen", "Darius", "Xavier", "Terrance", "Andre", "Dante", "Brimst", "Bronson", "Tony", "Darryl" };
                case "Demolitionist": return new[] { "Dolbere", "Bazdin", "Durim", "Tordak", "Garval", "Morthal", "Oten", "Dolgen", "Gimli", "Gimut", "Duerthen", "Beldin", "Jarut", "Ovbere", "Norkas", "Dolgrim", "Boften", "Norsun", "Dias", "Fikod", "Urist", "Darur" };
                case "GoblinTinkerer": return new[] { "Grodax", "Sarx", "Xon", "Mrunok", "Nuxatk", "Tgerd", "Darz", "Smador", "Stazen", "Mobart", "Knogs", "Tkanus", "Negurk", "Nort", "Durnok", "Trogem", "Stezom", "Gnudar", "Ragz", "Fahd", "Xanos", "Arback", "Fjell", "Dalek", "Knub" };
                case "WitchDoctor": return new[] { "Abibe", "Jamundi", "U'wa", "Tairona", "Xirigua", "Zop'a", "Opuni", "Kogi-ghi", "Konah", "Gboto" };
                case "SkeletonMerchant": return new[] { "Skellington", "Bones McGee", "Gloomy Mays", "Jack Sellington", "Billy Marrows", "Tom", "Rattles Magoo", "Mandible Calavera", "Mika", "No-Eyed Wiley", "Victor" };
                case "BestiaryGirl": return new[] { "Maria", "MacKenzie", "Azaria", "Juanita", "Astra", "Hayley", "Becca", "Veronica", "Tia", "Robyn", "Arien", "Jane", "Bindi", "Dian", "Lori", "Mardy", "Mollie", "Lizzy", "Daphne", "Ellen", "Lilith" };
                case "PartyGirl": return new[] { "Candy", "Isis", "Trixy", "Destiny", "Lexus", "Bambi", "Bailey", "Glitter", "Sparkle", "Paris", "Dazzle", "Fantasy", "Bunny", "Sugar", "Fantasia", "Star", "Cherry", "Amanda" };
                case "Steampunker": return new[] { "Whitney", "Verity", "Ada", "Cornelia", "Lydia", "Leila", "Minerva", "Emeline", "Cynthia", "Fidelia", "Lilly", "Phoebe", "Zylphia", "Zelda", "Selina", "Hope", "Isabella", "Judith", "Savannah", "Vivian" };
                case "Angler": return new[] { "Bobby", "Sammy", "Jimmy", "Danny", "Timmy", "Simon", "Johnny", "Billy", "Matty", "Bart", "Phillip", "Spencer", "Nathan", "Grayson", "Miles", "Charles", "Adam", "Tyler", "Jey", "Ivan", "Izzy" };
                case "Cyborg": return new[] { "Alpha", "Beta", "Delta", "Omega", "Gamma", "Theta", "Kappa", "Omicron", "Sigma", "Upsilon", "Phi", "Zeta", "Lambda", "Nu", "Ci", "Rho", "Fender", "T-3E0", "Niner-7", "A.N.D.Y.", "Syn-X", "Mu", "Raynbro" };
                default: return new[] { "" };
            }
        }

        /// <summary>Main.npcFrameCount (spec-townnpc 3.1).</summary>
        static int FrameCount(int id) => id switch { 369 => 23, 22 => 26, 17 => 25, 19 => 25, 38 => 25, 107 => 25, 228 => 26, 633 => 23, 453 => 26, 208 => 23, 178 => 23, 209 => 26, _ => 25 };

        /// <summary>NPCID.Sets.ExtraFramesCount: the sitting, talking and attack frames after the walk.</summary>
        static int ExtraFrames(int id) => id switch { 22 => 10, 228 => 10, 209 => 10, _ => 9 };

        /// <summary>NPCAddHeight and num46: the Steampunker's sheet sits 2 px lower.</summary>
        static float AddHeight(int id) => id == 178 ? 2f : 0f;
    }
}
