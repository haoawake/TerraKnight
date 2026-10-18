using System.Collections.Generic;
using System.Text.RegularExpressions;
using TerraKnight.Effects;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Town
{
    /// <summary>
    /// Terraria's Angler. He sleeps by the Blue Lake (Crossroads_50; Terraria's Sleeping Angler on the beach): talked to,
    /// he wakes (NPC.savedAngler) and moves into Dirtmouth. Each day (at dawn, Main.AnglerQuestSwap) he wants one fish, of
    /// those catchable in Hallownest by the stage reached (the hardmode ones from the Monarch Wings on); it can be caught
    /// only while it is the quest, not yet handed in today, and not already carried. His "Quest" button shows it (his
    /// words, and where it is caught here), or takes it and gives Terraria's reward (Player.GetAnglerReward): the 5th..30th
    /// quests' set rewards, rarer things the more quests done, a fishing accessory not owned yet, potions, and always
    /// money and often bait.
    /// </summary>
    internal static class Angler
    {
        /// <summary>Woken at the lake: he lives in Dirtmouth.</summary>
        public static bool Saved;
        /// <summary>Today's fish, the day it was chosen (TerraTime days), handed in today; quests done in all.</summary>
        public static ItemType Quest = ItemType.None;
        public static int QuestDay = -1;
        public static bool QuestDone;
        public static int QuestsDone;
        public static int DevRewards;

        public static void Clear()
        {
            Saved = false;
            Quest = ItemType.None;
            QuestDay = -1;
            QuestDone = false;
            QuestsDone = 0;
            HideSleeper();
        }

        static int Today => PlayerData.instance == null ? 0 : World.TerraTime.Day;

        /// <summary>Main.AnglerQuestSwap at dawn: a new fish, today's quest open again.</summary>
        static void Refresh()
        {
            if (!Saved) return;
            int day = Today;
            if (day == QuestDay && Quest != ItemType.None) return;
            QuestDay = day;
            QuestDone = false;
            var pick = new List<ItemType>();
            foreach (var q in Fishing.QuestList(Shop.UnlockedStage())) pick.Add(q);
            Quest = pick.Count > 0 ? pick[Random.Range(0, pick.Count)] : ItemType.None;
            Plugin.Log.LogInfo($"[渔夫] 第 {day} 天的任务：{(Quest == ItemType.None ? "无" : ItemDb.Get(Quest).Name)}");
        }

        /// <summary>Projectile.FishingCheck_ProbeForQuestFish: the fish that can be caught now (none if handed in, or carried).</summary>
        public static ItemType QuestFishToCatch(Inventory inv)
        {
            Refresh();
            if (!Saved || QuestDone || Quest == ItemType.None || inv == null) return ItemType.None;
            return Carries(inv, Quest) ? ItemType.None : Quest;
        }

        static bool Carries(Inventory inv, ItemType t)
        {
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == t) return true;
            return !inv.Mouse.IsEmpty && inv.Mouse.Type == t;
        }

        // ------------------------------------------------------------------ talking

        static string T(string key) => TerrariaText.Get(Lang.IsSource ? Lang.Source : Lang.Culture, key) ?? TerrariaText.Get("en-US", key);

        static string Fill(string s, string name)
        {
            if (s == null) return "";
            return s.Replace("{Angler}", name ?? Lang.T("渔夫")).Replace("{WorldName}", Lang.T("圣巢")).Replace("{AnglerCompletedQuestsCount}", QuestsDone.ToString())
                .Replace("{PlayerName}", Lang.T("小骑士")).Replace("{Bartender}", TerrariaText.NpcName(Lang.IsSource ? Lang.Source : Lang.Culture, 550) ?? "Bartender")
                .Replace("{Dryad}", TerrariaText.NpcName(Lang.IsSource ? Lang.Source : Lang.Culture, 20) ?? "Dryad");
        }

        /// <summary>NPC.GetChat for the Angler: his quest chatter now and then, a nudge while the quest is open, else his chatter.</summary>
        public static string Chat(string name)
        {
            Refresh();
            if (QuestsDone > 1 && Random.Range(0, 5) == 0) return Fill(T("AnglerQuestChatter.Chatter_" + Random.Range(1, 3)), name);
            if (!QuestDone && Quest != ItemType.None) return Fill(T("LegacyDialog." + Random.Range(334, 338)), name);
            return Fill(T("AnglerChatter.Chatter_" + Random.Range(1, 11)), name);
        }

        /// <summary>The "Quest" button (Main.NPCChatText_DoAnglerQuest): today's fish, handed in for the reward, or nothing more today.</summary>
        public static string QuestButton(Inventory inv, string name)
        {
            Refresh();
            if (QuestDone || Quest == ItemType.None) return Fill(T("AnglerQuestText.NoQuest_" + Random.Range(1, 6)), name);
            int slot = -1;
            for (int i = 0; i < inv.Slots.Length && slot < 0; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == Quest) slot = i;
            if (slot >= 0)
            {
                inv.ConsumeAt(slot);
                QuestsDone++;
                QuestDone = true;
                Reward(inv);
                TerraMod.Instance?.PlaySound("Item_1");
                return Fill(T("AnglerQuestText.TurnIn_" + Random.Range(1, 6)), name);
            }
            return QuestText(Quest, name);
        }

        /// <summary>The quest's words, Terraria's, its "(Caught in ...)" told by Hallownest's areas.</summary>
        static string QuestText(ItemType fish, string name)
        {
            string text = Fill(T("AnglerQuestText.Quest_" + fish), name);
            text = Regex.Replace(text, @"\s*[\(（][^\(（]*[\)）]\s*$", "");
            return text + "\n\n" + Lang.F("（抓捕位置：{0}）", Fishing.WhereText(fish));
        }

        // ------------------------------------------------------------------ the reward

        /// <summary>Player.GetAnglerReward (Pl:56967-57357), with the items Hallownest has.</summary>
        static readonly ItemType[] rewardAccessories = { ItemType.HighTestFishingLine, ItemType.AnglerEarring, ItemType.TackleBox,
            ItemType.FishermansGuide, ItemType.WeatherRadio, ItemType.Sextant, ItemType.FishingBobber };

        internal static IEnumerable<ItemType> DevRewardSources()
        {
            foreach (var t in rewardAccessories) if (Has(t)) yield return t;
            foreach (var t in new[] { ItemType.AnglerHat, ItemType.AnglerVest, ItemType.AnglerPants })
                if (Has(t)) yield return t;
        }

        static void Reward(Inventory inv)
        {
            int q = QuestsDone;
            float r0 = q <= 50 ? 1f - 0.01f * q : q <= 100 ? 0.5f - 0.005f * (q - 50) : q > 150 ? 0.15f : 0.25f - 0.002f * (q - 100);
            float r = r0 * 0.9f;
            bool hard = Shop.UnlockedStage() >= 4;
            int N(float n) => Mathf.Max(0, (int)(n * r));
            bool Roll(float n) => Random.Range(0, Mathf.Max(1, N(n))) == 0;
            var got = new List<(ItemType, int)>();
            void Give(ItemType t, int n = 1) { if (Has(t)) got.Add((t, n)); }
            bool main = false;
            bool Try(bool cond, ItemType t)
            {
                if (main || !cond || !Has(t)) return false;
                Give(t);
                main = true;
                return true;
            }
            Try(q == 10, ItemType.AnglerHat);
            Try(q == 15, ItemType.AnglerVest);
            Try(q == 20, ItemType.AnglerPants);
            Try(q == 30, ItemType.GoldenFishingRod);
            Try(q > 75 && Roll(250), ItemType.GoldenFishingRod);
            Try(hard && q > 25 && Roll(100), ItemType.HotlineFishingHook);
            Try(hard && q > 10 && Roll(70), ItemType.FinWings);
            Try(Roll(60), ItemType.FishHook);
            if (!main)
            {
                // The fishing accessories not owned yet (the tackle bags count for their parts, a glowing bobber for the plain one).
                var pool = new List<ItemType>(rewardAccessories);
                var owned = Owned(inv);
                if (owned.Contains(ItemType.AnglerTackleBag) || owned.Contains(ItemType.LavaproofTackleBag)) { owned.Add(ItemType.HighTestFishingLine); owned.Add(ItemType.TackleBox); owned.Add(ItemType.AnglerEarring); }
                if (owned.Contains(ItemType.FishFinder) || owned.Contains(ItemType.PDA) || owned.Contains(ItemType.CellPhone)) { owned.Add(ItemType.FishermansGuide); owned.Add(ItemType.WeatherRadio); owned.Add(ItemType.Sextant); }
                for (var t = ItemType.FishingBobberGlowingStar; t <= ItemType.FishingBobberGlowingRainbow; t++) if (owned.Contains(t)) owned.Add(ItemType.FishingBobber);
                var missing = pool.FindAll(t => !owned.Contains(t) && Has(t));
                float a = 1f / Mathf.Max(1, N(40)), b = 1f / Mathf.Max(1, N(30)), c = 1f / Mathf.Max(1, N(25));
                float p = 0.8f * (1f - Mathf.Pow(1f - a, 3) * Mathf.Pow(1f - b, 3) * (1f - c));
                var from = missing.Count > 0 ? missing : pool.FindAll(Has);
                if (from.Count > 0 && Random.value < p) { Give(from[Random.Range(0, from.Count)]); main = true; }
                else
                {
                    var potions = new[] { ItemType.FishingPotion, ItemType.SonarPotion, ItemType.CratePotion };
                    Give(potions[Random.Range(0, potions.Length)], Random.Range(2, 6));
                }
            }
            // Money, always (n silver, or gold over 100): a geo is a silver.
            int m = (q + 50) / 2;
            m = (int)(m * Random.Range(50, 201) * 0.015f);
            m = (int)(m * 1.5f);
            if (hard) m *= 2;
            if (World.GameDifficulty.Current >= World.DifficultyLevel.Expert) m *= 2;
            long copper = m > 100 ? Mathf.Clamp(m / 100, 1, 10) * 10000L : Mathf.Clamp(m, 1, 99) * 100L;
            Shop.Earn(copper);
            // Bait.
            if (Random.Range(0, Mathf.Max(1, N(100))) <= 50)
            {
                var bait = Random.Range(0, Mathf.Max(1, N(15))) == 0 ? ItemType.MasterBait : Random.Range(0, Mathf.Max(1, N(5))) == 0 ? ItemType.JourneymanBait : ItemType.ApprenticeBait;
                int n = 1;
                foreach (int k in new[] { 25, 50, 100, 150, 200, 250 }) if (Random.Range(0, k) <= q) n++;
                Give(bait, n);
            }
            var hero = HeroController.SilentInstance;
            Vector2 at = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
            foreach (var (t, n) in got)
            {
                int left = inv.Pickup(t, n);
                if (left < n) UI.PopupText.Show(ItemDb.Get(t), n - left, at);
                if (left > 0) WorldItems.Drop(ItemStack.Of(t, left), at);
            }
            DevRewards++;
            Plugin.Log.LogInfo($"[渔夫] 第 {q} 个任务的奖励：{string.Join("，", got.ConvertAll(g => ItemDb.Get(g.Item1).Name + "×" + g.Item2))}，钱 {copper / 100} 吉欧");
        }

        static bool Has(ItemType t) => ItemDb.Get(t) != null && ItemDb.Get(t).Name != t.ToString();

        static HashSet<ItemType> Owned(Inventory inv)
        {
            var set = new HashSet<ItemType>();
            void Add(ItemStack[] xs) { foreach (var s in xs) if (!s.IsEmpty) set.Add(s.Type); }
            Add(inv.Slots); Add(inv.Accessories); Add(inv.Armor); Add(inv.Bank);
            return set;
        }

        // ------------------------------------------------------------------ the sleeper by the Blue Lake

        const string LakeScene = "Crossroads_50";
        static float nextLook;
        static SpriteRenderer sleeper;
        static Rect sleeperBox;
        public static bool DevSleeperShown => sleeper != null && sleeper.enabled;
        public static Vector2 DevSleeperAt => sleeperBox.center;

        static void HideSleeper()
        {
            if (sleeper != null) Object.Destroy(sleeper.gameObject);
            sleeper = null;
        }

        /// <summary>Every frame in play: the Sleeping Angler on the Blue Lake's shore while he hasn't been woken.</summary>
        public static void Tick()
        {
            if (Saved) { if (sleeper != null) HideSleeper(); return; }
            // The lake looked for only while its scene is in, and once a second at most: searching every state machine of
            // every scene each frame cost 2 ms a frame everywhere (more where many enemies are, the Royal Waterways).
            if (sleeper != null && UnityEngine.SceneManagement.SceneManager.GetSceneByName(LakeScene).isLoaded) return;
            if (Time.unscaledTime < nextLook) return;
            nextLook = Time.unscaledTime + 1f;
            if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName(LakeScene).isLoaded) { if (sleeper != null) HideSleeper(); return; }
            var lake = FindLake();
            if (lake == null) { if (sleeper != null) HideSleeper(); return; }
            if (sleeper != null) return;
            var b = lake.Value;
            // On the shore at the lake's left end, standing on the ground there.
            float x = b.min.x - 1.2f;
            var hit = Physics2D.Raycast(new Vector2(x, b.max.y + 3f), Vector2.down, 10f, 1 << 8 | 1 << World.ChunkColliders.TerrainLayer);
            float feet = hit.collider != null ? hit.point.y : b.max.y;
            var go = new GameObject("Terra Sleeping Angler");
            sleeper = go.AddComponent<SpriteRenderer>();
            // NPC_376's first frame (the sheet's frames stacked: 1 frame here).
            var tex = TerrariaAssets.Texture("NPC_376");
            int frames = Mathf.Max(1, tex.height / 32);
            int fh = tex.height / frames;
            sleeper.sprite = Sprite.Create(tex, new Rect(0, tex.height - fh, tex.width, fh), new Vector2(0.5f, 0f), 1f / TerraParticles.Px);
            // Drawn as the Knight is (his sorting layer), just behind him.
            var heroSr = HeroController.SilentInstance != null ? HeroController.SilentInstance.GetComponent<MeshRenderer>() : null;
            if (heroSr != null) { sleeper.sortingLayerID = heroSr.sortingLayerID; sleeper.sortingOrder = heroSr.sortingOrder - 1; }
            else sleeper.sortingOrder = 5;
            go.transform.position = new Vector3(x, feet, 0.01f);
            float w = tex.width * TerraParticles.Px, h = fh * TerraParticles.Px;
            sleeperBox = new Rect(x - w / 2f, feet, w, h);
            Plugin.Log.LogInfo($"[渔夫] 沉睡的渔夫在蓝湖岸边 ({x:F1}, {feet:F1})");
        }

        static Bounds? FindLake()
        {
            var gm = GameManager.UnsafeInstance;
            if (gm == null) return null;
            Bounds? best = null;
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (fsm.FsmName != "Surface Water Region" || fsm.gameObject.scene.name != LakeScene) continue;
                var box = fsm.GetComponent<BoxCollider2D>();
                if (box == null) continue;
                var t = box.transform;
                Vector2 c = t.TransformPoint(box.offset), s = Vector2.Scale(box.size, t.lossyScale);
                var b = new Bounds(c, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 0f));
                if (best == null || b.size.x > best.Value.size.x) best = b;
            }
            return best;
        }

        /// <summary>A right click on the sleeper within reach: he wakes and moves into Dirtmouth.</summary>
        public static bool TryRightClick(Vector2 mouse)
        {
            var hero = HeroController.SilentInstance;
            if (Saved || sleeper == null || hero == null || !sleeperBox.Contains(mouse)) return false;
            if (Vector2.Distance(hero.transform.position, sleeperBox.center) > 7f) return false;
            Wake();
            return true;
        }

        public static void Wake()
        {
            Saved = true;
            HideSleeper();
            string line = TerrariaText.Get(Lang.IsSource ? Lang.Source : Lang.Culture, "LegacyDialog." + Random.Range(353, 356)) ?? "";
            var hero = HeroController.SilentInstance;
            if (hero != null) UI.CombatText.Show(hero.transform.position + Vector3.up * 1.5f, Fill(line, null), Color.white);
            Net.NetChat.Add(Lang.T("渔夫醒了，他会搬进德特茅斯。"), new Color(50 / 255f, 125 / 255f, 255 / 255f));
            TerraMod.Instance?.PlaySound("Chat", 0.8f);
            Plugin.Log.LogInfo("[渔夫] 在蓝湖叫醒了渔夫");
        }
    }
}
