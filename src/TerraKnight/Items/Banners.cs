using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's kill banners (BannerSystem.AddNPCKillBy) for Hallownest's journal records: every 20th kill of a record
    /// (ItemID.Sets.KillsToBanner is 50 by default, DefaultKillsForBannerNeeded; here 20) drops that record's banner
    /// where it fell, with Terraria's line in the chat ("Game.EnemiesDefeatedAnnouncement", (250,250,0)). The kills are
    /// the monster book's own count (Bestiary.Kills, by the book's key: a stage's own record, ZombieMiner@0, has its own
    /// banner). A banner works in the inventory or the Piggy Bank, or in the Banner Case (Containers) carried or lying in
    /// the Piggy Bank, instead of hung up nearby (SceneMetrics.NPCBannerBuff): Terraria's BannerEffect (ItemID.Sets.BannerStrength, the
    /// default BannerEffect(1f)) against its record, damage dealt ×DamageDealt (Player.ApplyDamageToNPC Pl:20597-20600,
    /// ItemCheck_MeleeHitNPCs Pl:44656-44659, Projectile.Damage P:13388-13391: before the variation) and damage taken
    /// ×DamageReceived (the touch Pl:31624-31627, a hostile shot P:14906-14909: before the defense), both sampled at the
    /// save's difficulty (Main.Difficulty). The Monster Banner buff (147) shows while any is carried (Pl:25747-25750),
    /// its tooltip listing the monsters (Main.cs:21613-21616). A shot an enemy's FSM spawns carries its banner
    /// (Projectile.bannerIdToRespondTo): the pool's spawns and created objects are marked with the enemy that made them.
    /// </summary>
    [HarmonyPatch]
    internal static partial class Banners
    {
        /// <summary>Kills of a record for each of its banners.</summary>
        public const int KillsPerBanner = 20;
        /// <summary>BuffID.MonsterBanner.</summary>
        public const int MonsterBannerBuff = 147;

        static Dictionary<string, int> byRecord;
        static Dictionary<ItemType, int> byType;

        static void Build()
        {
            if (byType != null) return;
            byRecord = new Dictionary<string, int>();
            byType = new Dictionary<ItemType, int>();
            for (int i = 0; i < all.Length; i++)
            {
                byRecord[all[i].Record] = i;
                byType[all[i].Type] = i;
            }
        }

        public static int Count => all.Length;
        public static bool Is(ItemType t) { Build(); return byType.ContainsKey(t); }

        public static IEnumerable<ItemType> Types()
        {
            foreach (var e in all) yield return e.Type;
        }

        /// <summary>The banner of a book key (its own, else its record's), or None.</summary>
        public static ItemType Of(string key)
        {
            if (string.IsNullOrEmpty(key)) return ItemType.None;
            Build();
            if (byRecord.TryGetValue(key, out int i)) return all[i].Type;
            int at = key.IndexOf('@');
            return at > 0 && byRecord.TryGetValue(key.Substring(0, at), out i) ? all[i].Type : ItemType.None;
        }

        /// <summary>The book key a banner is for, or null.</summary>
        public static string RecordOf(ItemType t)
        {
            Build();
            return byType.TryGetValue(t, out int i) ? all[i].Record : null;
        }

        /// <summary>The monster's name in the language now: the journal's, else the art pack's Chinese or English.</summary>
        public static string MonsterName(ItemType t)
        {
            Build();
            if (!byType.TryGetValue(t, out int i)) return "";
            var e = all[i];
            if (Lang.IsSource) return e.Zh;
            string live = JournalReady ? UI.EnemyHover.NameOfRecord(e.Record) : null;
            if (!string.IsNullOrEmpty(live)) return live;
            return Lang.Culture.StartsWith("zh") ? e.Zh : e.En;
        }

        static int readyFrame = -1;
        static bool ready;

        /// <summary>The Hunter's Journal is loaded (it names the records in the game's language), looked at once a frame.</summary>
        static bool JournalReady
        {
            get
            {
                if (readyFrame == Time.frameCount) return ready;
                readyFrame = Time.frameCount;
                ready = UI.EnemyHover.PortraitOf("Crawler") != null && UI.EnemyHover.NameOfRecord("Crawler") != null;
                return ready;
            }
        }

        // ------------------------------------------------------------------ items

        /// <summary>The banner line of a tooltip (CommonItemTooltip.BannerBonus + the monster), Terraria's text in the language.</summary>
        static string BonusLine(ItemType t) =>
            (TerrariaText.Get(Lang.Culture, "CommonItemTooltip.BannerBonus") ?? "附近的玩家获得针对以下怪的加成：") + MonsterName(t);

        const string CarriedLine = "放在背包、猪猪存钱罐或旗帜盒里就生效，无需挂出";

        /// <summary>
        /// Each banner (the monster banners Item 1615-1701: rare 1, value 1000, Item.cs:19137-19237; stacking to 9999):
        /// named as Terraria's ("僵尸旗"), dropped by its record (ItemSource.Drop, the record's stage).
        /// </summary>
        public static void AddItems(List<ItemDef> list)
        {
            foreach (var e in all)
            {
                string name = e.Zh + "旗";
                list.Add(new ItemDef
                {
                    Type = e.Type, Name = name, ZhName = name, Texture = Relics.TexturePrefix + e.Texture, MaxStack = 9999, Rare = 1,
                    UseSound = null, Value = 1000, Price = 10, Stage = e.Stage, Source = ItemSource.Drop,
                    Tooltip = "附近的玩家获得针对以下怪的加成：" + e.Zh + "\n" + CarriedLine,
                });
            }
        }

        /// <summary>A banner's name and tooltip in the language now ("{0}旗" in each language's own way, Terraria's banner line).</summary>
        public static void Localize(ItemDef d)
        {
            string monster = MonsterName(d.Type);
            d.Name = Lang.IsSource ? d.ZhName : Lang.F("{0}旗", monster);
            d.Tooltip = BonusLine(d.Type) + "\n" + Lang.T(CarriedLine);
            localizedIn = Lang.Culture;
            localizedLive = Lang.IsSource || JournalReady;
        }

        static string localizedIn;
        static bool localizedLive;

        // ------------------------------------------------------------------ the effect

        /// <summary>GameDifficultyData.LinearCurve: the output between the keys either side of the input, the end keys' beyond.</summary>
        static float Sample((float In, float Out)[] keys, float value)
        {
            var key = keys[0];
            var key2 = key;
            for (int i = 0; i < keys.Length; i++)
            {
                key2 = keys[i];
                if (value <= key2.In) break;
                key = key2;
            }
            float num = key2.In - key.In;
            float num2 = key2.Out - key.Out;
            if (num == 0f) return key.Out;
            return (value - key.In) * num2 / num + key.Out;
        }

        /// <summary>
        /// ItemID.BannerEffect(strength 1f), every monster banner's (ItemID.Sets.BannerStrength's default): dealt 1 + s/2 on
        /// Classic, 1 + s on Expert; taken (1 + 1/(s+1))/2 on Classic, 1/(s+1) on Expert (GameDifficultyLevel Classic 1, Expert 2).
        /// </summary>
        const float Strength = 1f;
        static readonly (float, float)[] dealtCurve = { (1f, 1f + Strength * 0.5f), (2f, 1f + Strength) };
        static readonly (float, float)[] takenCurve = { (1f, (1f + 1f / (Strength + 1f)) / 2f), (2f, 1f / (Strength + 1f)) };

        /// <summary>Main.Difficulty for the save: Normal is Classic (1), Expert 2, Master 3; Hard between Classic and Expert (1.5).</summary>
        public static float DifficultyLevel(World.DifficultyLevel d) => d switch
        {
            World.DifficultyLevel.Normal => 1f,
            World.DifficultyLevel.Hard => 1.5f,
            World.DifficultyLevel.Expert => 2f,
            _ => 3f,
        };

        public static float DealtFactor(World.DifficultyLevel d) => Sample(dealtCurve, DifficultyLevel(d));
        public static float TakenFactor(World.DifficultyLevel d) => Sample(takenCurve, DifficultyLevel(d));

        static readonly HashSet<ItemType> active = new HashSet<ItemType>();
        static readonly List<ItemType> activeOrder = new List<ItemType>();
        static int activeFrame = -1;

        /// <summary>
        /// The banners in effect now (the inventory's and the Piggy Bank's; the Banner Case's when the case is carried or in
        /// the Piggy Bank), worked out once a frame.
        /// </summary>
        public static void Refresh(bool force = false)
        {
            if (!force && activeFrame == Time.frameCount) return;
            activeFrame = Time.frameCount;
            active.Clear();
            activeOrder.Clear();
            var inv = TerraMod.Instance?.Inventory;
            if (inv == null) return;
            foreach (var s in inv.Slots)
                if (!s.IsEmpty && Is(s.Type) && active.Add(s.Type)) activeOrder.Add(s.Type);
            foreach (var s in inv.Bank)
                if (!s.IsEmpty && Is(s.Type) && active.Add(s.Type)) activeOrder.Add(s.Type);
            if (!Containers.InEffect(inv, ItemType.BannerCase)) return;
            foreach (var page in inv.BannerCase)
                foreach (var s in page)
                    if (!s.IsEmpty && Is(s.Type) && active.Add(s.Type)) activeOrder.Add(s.Type);
        }

        /// <summary>Player.HasNPCBannerBuff: this banner is carried.</summary>
        public static bool Active(ItemType banner)
        {
            Refresh();
            return banner != ItemType.None && active.Contains(banner);
        }

        public static int ActiveCount { get { Refresh(); return active.Count; } }

        static readonly Dictionary<int, ItemType> ofEnemy = new Dictionary<int, ItemType>();

        /// <summary>A live enemy's banner (its book key's), kept by the enemy.</summary>
        public static ItemType BannerOf(HealthManager hm)
        {
            if (hm == null) return ItemType.None;
            int id = hm.GetInstanceID();
            if (ofEnemy.TryGetValue(id, out var t)) return t;
            if (ofEnemy.Count > 4096) ofEnemy.Clear();
            t = Of(Bestiary.KeyOf(hm));
            ofEnemy[id] = t;
            return t;
        }

        /// <summary>Dev: the last damage a banner raised (before and after), and how many it has raised.</summary>
        public static int DevDealtIn, DevDealtOut, DevDealtCount, DevTakenIn, DevTakenOut, DevTakenCount;

        /// <summary>A hit on an enemy, before its variation: ×DamageDealt with its banner carried (Pl:20597-20600, 44656-44659).</summary>
        public static int Dealt(HealthManager hm, int damage)
        {
            if (hm == null || damage <= 0) return damage;
            var banner = BannerOf(hm);
            if (!Active(banner)) return damage;
            int raised = (int)(damage * DealtFactor(World.GameDifficulty.Current));
            DevDealtIn = damage;
            DevDealtOut = raised;
            DevDealtCount++;
            return raised;
        }

        /// <summary>
        /// A hit on the player from an enemy (its body, an attack of it, a shot it spawned), before the defense: ×DamageReceived
        /// with its banner carried (Pl:31624-31627, P:14906-14909).
        /// </summary>
        public static int Taken(GameObject source, int damage)
        {
            if (source == null || damage <= 0) return damage;
            var banner = SourceBanner(source);
            if (!Active(banner)) return damage;
            int lowered = (int)(damage * TakenFactor(World.GameDifficulty.Current));
            DevTakenIn = damage;
            DevTakenOut = lowered;
            DevTakenCount++;
            return lowered;
        }

        /// <summary>The banner of whatever dealt a hit: the enemy it belongs to, else the enemy that spawned it.</summary>
        public static ItemType SourceBanner(GameObject source)
        {
            if (source == null) return ItemType.None;
            var hm = source.GetComponentInParent<HealthManager>();
            if (hm != null) return BannerOf(hm);
            for (var t = source.transform; t != null; t = t.parent)
                if (shotOf.TryGetValue(t.gameObject.GetInstanceID(), out var b)) return b;
            return ItemType.None;
        }

        // ------------------------------------------------------------------ shots

        static readonly Dictionary<int, ItemType> shotOf = new Dictionary<int, ItemType>();
        /// <summary>The FSM spawning from the pool now (its owner).</summary>
        static GameObject spawner;
        public static int DevShotsMarked;

        /// <summary>A spawned or created object marked with the banner of the enemy whose FSM made it (none for anything else).</summary>
        public static void Mark(GameObject made, GameObject owner)
        {
            if (made == null) return;
            var hm = owner != null ? owner.GetComponentInParent<HealthManager>() : null;
            int id = made.GetInstanceID();
            if (hm == null)
            {
                shotOf.Remove(id);
                return;
            }
            if (shotOf.Count > 4096) shotOf.Clear();
            var banner = BannerOf(hm);
            shotOf[id] = banner;
            if (banner != ItemType.None) DevShotsMarked++;
        }

        [HarmonyPatch]
        static class PoolSpawns
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (var (type, name) in new (System.Type, string)[]
                {
                    (typeof(SpawnObjectFromGlobalPool), "OnEnter"), (typeof(FlingObjectsFromGlobalPool), "OnEnter"),
                    (typeof(FlingObjectsFromGlobalPoolVel), "OnEnter"), (typeof(FlingObjectsFromGlobalPoolTime), "OnUpdate"),
                    (typeof(SpawnObjectFromGlobalPoolOverTime), "OnUpdate"), (typeof(SpawnObjectFromGlobalPoolOverTimeV2), "OnUpdate"),
                })
                {
                    var m = AccessTools.DeclaredMethod(type, name);
                    if (m != null) yield return m;
                }
            }

            static void Prefix(object __instance) => spawner = (__instance as FsmStateAction)?.Owner;

            static void Finalizer() => spawner = null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectPool), nameof(ObjectPool.Spawn), typeof(GameObject), typeof(Transform), typeof(Vector3), typeof(Quaternion))]
        static void PoolSpawned(GameObject __result)
        {
            if (spawner != null && __result != null) Mark(__result, spawner);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CreateObject), nameof(CreateObject.OnEnter))]
        static void Created(CreateObject __instance)
        {
            var made = __instance.storeObject != null ? __instance.storeObject.Value : null;
            if (made != null) Mark(made, __instance.Owner);
        }

        // ------------------------------------------------------------------ kills

        public static int DevDropped;

        /// <summary>
        /// A kill counted by the book (after its loot): the record's 20th, 40th... drops its banner where it fell
        /// (BannerSystem.AddNPCKillBy: killCount % KillsToBanner == 0), straight into the Banner Case or the bag where
        /// the fight leaves no time to pick it up, and Terraria's line in the chat.
        /// </summary>
        public static void OnKill(string key, HealthManager hm, Vector2? at)
        {
            var banner = Of(key);
            if (banner == ItemType.None || hm == null) return;
            int kills = Bestiary.KillsOf(key);
            if (kills <= 0 || kills % KillsPerBanner != 0) return;
            var col = hm.GetComponent<Collider2D>();
            Vector2 where = at ?? (col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position);
            bool direct = EnemyDrops.LeavesOnItsOwn(hm.gameObject.scene.name);
            var stack = ItemStack.Of(banner);
            DevDropped++;
            var inv = TerraMod.Instance?.Inventory;
            if (direct && inv != null && Containers.Store(inv, stack, 1) == 0)
            {
                var hero = HeroController.SilentInstance;
                if (hero != null) UI.PopupText.Show(stack.Def, 1, hero.GetComponent<BoxCollider2D>().bounds.center);
            }
            else EnemyDrops.Spill(stack, where, direct);
            string name = MonsterName(banner);
            string line = TerrariaText.Get(Lang.Culture, "Game.EnemiesDefeatedAnnouncement");
            Net.NetChat.Add(line != null ? string.Format(line, kills, name) : Lang.F("已打败第{0}个{1}！", kills, name), new Color(250 / 255f, 250 / 255f, 0f));
            Plugin.Log.LogInfo($"[旗帜] {key} 第 {kills} 次击杀，掉落 {stack.Def.Name}");
        }

        // ------------------------------------------------------------------ the buff

        static readonly bool defined = Define();

        static bool Define()
        {
            // BuffID 147, Lang's zh-Hans (buffNoTimeDisplay: SceneMetrics' banner buff).
            Player.PlayerBuffs.Define(MonsterBannerBuff, "旗帜", "提高对以下怪的伤害和防御力：", noTime: true);
            return true;
        }

        /// <summary>
        /// Each frame: the Monster Banner buff while any banner is carried (AddBuff(147, 2) each update as SceneMetrics.hasBanner
        /// does); the banners' names again once the journal can name them in another language.
        /// </summary>
        public static void Tick(TerraMod mod)
        {
            if (!defined || mod == null) return;
            if (ActiveCount > 0) Player.PlayerBuffs.Add(MonsterBannerBuff, 10);
            if (!Lang.IsSource && localizedIn == Lang.Culture && !localizedLive && Time.frameCount % 120 == 0 && JournalReady)
                foreach (var e in all) Localize(ItemDb.Get(e.Type));
        }

        /// <summary>The banner buff's tooltip: the monsters it is for under it (Main.cs:21613-21616, bannerMouseOver).</summary>
        public static void BuffLines(int type, List<string> lines)
        {
            if (type != MonsterBannerBuff) return;
            Refresh();
            const int Shown = 20;
            for (int i = 0; i < activeOrder.Count && i < Shown; i++) lines.Add(MonsterName(activeOrder[i]));
            if (activeOrder.Count > Shown) lines.Add(TerrariaText.Get(Lang.Culture, "UI.Ellipsis") ?? "...");
        }

        public static void Clear()
        {
            ofEnemy.Clear();
            shotOf.Clear();
            activeFrame = -1;
        }
    }
}
