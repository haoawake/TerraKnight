using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Small moments of story that point the way, apart from the Voice's guidance (<see cref="BossGuide"/>): the first time
    /// something happens in a save (the first fall and the shade it leaves, the first bench, a charm, a pitch-dark room,
    /// life running low, a mask shard, Pale Ore, each Nailmaster's art learned, each of the four spells with class weapons
    /// absorbed, the Crossroads infected, the Grimm Troupe in town and its Grimmchild, the shimmer, the three Dreamers
    /// fallen) the player says a line or two of their own, and a tip says what to do about it. Each is said
    /// once a save (its flag kept in the save), in the Voice's subtitle place and in the chat, never over the Voice or the
    /// game's own talk, and held while the inventory is open. The Terraria settings' guidance tab turns them off for the
    /// save; a Creative save starts with them off.
    /// </summary>
    internal static partial class StoryBeats
    {
        const string Prefix = "story.beat.v1.", OffFlag = "story.beats.off.v1", OnFlag = "story.beats.on.v1";
        const float QuietBefore = 2f, QuietAgain = 1f;
        /// <summary>A line held this long in all (interrupted again and again) is let go; one shown this long is not started over.</summary>
        const float MaxHeld = 45f, ReadEnough = 1.5f;
        static readonly Color TipName = new Color(0.55f, 0.85f, 1f), TipText = new Color(0.82f, 0.92f, 1f),
            PlayerName = new Color(1f, 0.86f, 0.5f);

        sealed class Beat
        {
            public string Id;
            public System.Func<TerraMod, PlayerData, bool> When;
            /// <summary>Said as soon as it holds (life low, a dark room), not after a quiet while.</summary>
            public bool Now;
            /// <summary>Said while resting on a bench (the game holds the Knight there).</summary>
            public bool Seated;
            public string[] Zh, En;
            public System.Func<PlayerData, string> TipZh, TipEn;
            /// <summary>Done as it starts, before its lines (the first charm's: the inventory opened on the charm page).</summary>
            public System.Action Act;
        }

        struct Line
        {
            public bool Tip;
            public string Text;
        }

        static bool Chinese => Lang.IsSource || Lang.Culture.StartsWith("zh");
        static string Pick(string zh, string en) => Chinese || string.IsNullOrEmpty(en) ? zh : en;

        // What was had a moment ago: a beat for something got is said when it rises (kept until said), not for what an
        // older save had already.
        static int lastShards = -1, lastOre = -1;
        static bool lastGrimm, lastDreamers, died;
        static readonly HashSet<string> rose = new HashSet<string>();

        static int Shards(PlayerData pd) => (pd.maxHealthBase - 5) * 4 + pd.heartPieces;
        static bool Dreamers(PlayerData pd) => pd.lurienDefeated && pd.monomonDefeated && pd.hegemolDefeated;
        static bool ShadeLeft(PlayerData pd) => !string.IsNullOrEmpty(pd.shadeScene) && pd.shadeScene != "None";
        static string Scene => GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName ?? "" : "";

        static bool Dark(TerraMod mod)
        {
            var gm = GameManager.UnsafeInstance;
            if (gm == null || gm.sm == null || gm.sm.darknessLevel != 2) return false;
            var eq = Equipment.Current;
            return !HkItems.LanternLit(mod.Inventory) && !eq.Light && !eq.NightVision;
        }

        static readonly Beat[] coreBeats =
        {
            new Beat
            {
                Id = "lowlife", Now = true,
                When = (mod, pd) => mod.Stats.Life > 0 && mod.Stats.Life <= mod.Stats.LifeMax * 0.3f,
                Zh = new[] { "……再挨一下，就真的回不去了。" },
                En = new[] { "...One more hit and I really won't make it back." },
                TipZh = pd => "按快速治疗键（默认 H）会喝下背包里最合适的治疗药水，喝完要等“耐药性”过去才能再喝。身上没有药的话，先退回长椅吧。",
                TipEn = pd => "Quick Heal (H by default) drinks the best healing potion you carry; after one, Potion Sickness must wear off before the next. No potions on you? Fall back to a bench.",
            },
            new Beat
            {
                Id = "dark", Now = true,
                When = (mod, pd) => Dark(mod),
                Zh = new[] { "黑得连自己的手都看不见……再往前摸，迟早一脚踩空。" },
                En = new[] { "So dark I can't see my own hands... Keep feeling my way forward and sooner or later I'll step off an edge." },
                TipZh = pd => "带上光蝇灯笼，或者戴上会发光的装备（比如挖矿头盔）、喝一瓶夜猫子药水，都能照亮这种漆黑的地方。",
                TipEn = pd => "Carry the Lumafly Lantern, wear something that glows (a Mining Helmet, say) or drink a Night Owl Potion to light up places this dark.",
            },
            new Beat
            {
                Id = "death",
                When = (mod, pd) => died && ShadeLeft(pd),
                Zh = new[] { "……又睁开眼了。只是腰间的钱袋，轻得不对劲。", "我倒下的地方，还留着一团和我一模一样的黑影——它攥着我的吉欧，不肯撒手。" },
                En = new[] { "...Awake again. But my purse is far lighter than it should be.", "Where I fell, a shadow in my own shape stayed behind, and it's clutching my Geo." },
                TipZh = pd => "回到倒下的地方，打倒自己的暗影，吉欧就会回来（地图上标着它的位置）。拿回来之前再倒下一次，这些吉欧就找不回来了；存钱罐里的钱不受影响。"
                              + (World.GameDifficulty.MasterRules ? "大师难度下，暗影只替你留着一半。" : ""),
                TipEn = pd => "Go back to where you fell and beat your shade to get the Geo back (the map marks it). Fall again before then and that Geo is gone for good; money in the Piggy Bank is safe."
                              + (World.GameDifficulty.MasterRules ? " On Master, the shade keeps only half of it." : ""),
            },
            new Beat
            {
                Id = "bench", Seated = true,
                When = (mod, pd) => pd.atBench,
                Zh = new[] { "一张长椅。坐下来，伤口不疼了，连脑子都清醒了几分。", "……要是哪天倒在外面，大概会在这儿醒过来吧。" },
                En = new[] { "A bench. Sitting down, the wounds stop aching and my head clears a little.", "...If I ever fall out there, I suppose this is where I'll wake." },
                TipZh = pd => "坐在长椅上会回满生命，这里也会成为你的复活点" + (Charms.Enabled ? "；护符只能坐在长椅上更换" : "") + "。坐过的长椅旁会立起一座晶塔，打开地图就能传送回来。",
                TipEn = pd => "Resting on a bench fills your life and makes it your respawn point" + (Charms.Enabled ? ", and charms can only be changed while resting on one" : "") + ". A pylon stands beside every bench you've rested at: open the map to teleport back.",
            },
            new Beat
            {
                // The first charm got: the inventory opened on the charm page, its button pointed out (InventoryGui.ShowCharmIntro);
                // the charm's own beat follows once the inventory shuts.
                Id = "charm",
                When = (mod, pd) => Charms.Enabled && rose.Contains("charm"),
                Act = () => InventoryGui.ShowCharmIntro(),
                Zh = new string[0],
                En = new string[0],
            },
            new Beat
            {
                Id = "mask",
                When = (mod, pd) => rose.Contains("mask"),
                Zh = new[] { "一片面具的碎片，边缘还泛着微光。捏在手里，心跳都稳了些。" },
                En = new[] { "A shard of a mask, its edge still faintly glowing. Just holding it steadies my heartbeat." },
                TipZh = pd => "每片面具碎片都会提高生命上限（青色的心），集齐四片就能拼成一张完整的面具。",
                TipEn = pd => "Every mask shard raises your maximum life (the cyan hearts); four of them make a whole mask.",
            },
            new Beat
            {
                Id = "ore",
                When = (mod, pd) => rose.Contains("ore"),
                Zh = new[] { "一块冷得出奇的矿石，泛着苍白的光。随便哪个铁匠见了，眼睛都得发直。" },
                En = new[] { "A strangely cold ore with a pale sheen. Any smith would stare at the sight of it." },
                TipZh = pd => "把苍白矿石带给泪水之城的骨钉匠，他能把骨钉打磨得更锋利（每次升级还要付吉欧）。",
                TipEn = pd => "Take Pale Ore to the Nailsmith in the City of Tears to sharpen your nail (each upgrade costs Geo as well).",
            },
            new Beat
            {
                Id = "infected",
                When = (mod, pd) => pd.crossroadsInfected && Scene.StartsWith("Crossroads_"),
                Zh = new[] { "空气里多了股甜腻的腐味。墙缝里渗出橘色的脓泡，一鼓一瘪，像在呼吸。", "连这些虫子的眼睛都染成了同一种颜色……我不在的这些日子，十字路出了什么事？" },
                En = new[] { "The air has turned sweet and rotten. Orange blisters seep from the cracks, swelling and sinking as if they breathe.", "Even the bugs' eyes have taken on that colour... What happened to the Crossroads while I was gone?" },
                TipZh = pd => "遗忘十字路被感染了，这里的敌人比从前更加狂暴，别掉以轻心。",
                TipEn = pd => "The Forgotten Crossroads is infected: its enemies are more violent than before. Don't take them lightly.",
            },
            new Beat
            {
                Id = "troupe",
                When = (mod, pd) => pd.troupeInTown && Scene == "Town" && !Charms.Got(Charms.Grimmchild),
                Zh = new[] { "镇子边上多了一顶猩红的帐篷。里面的鼓点一下一下，像直接敲在心口上。" },
                En = new[] { "A scarlet tent has gone up at the edge of town. The drumbeat inside lands right on my chest, one beat at a time." },
                TipZh = pd => "格林剧团来到了德特茅斯，去帐篷里见见剧团的团长。",
                TipEn = pd => "The Grimm Troupe has come to Dirtmouth: go into the tent and meet its master.",
            },
            new Beat
            {
                Id = "grimmchild",
                When = (mod, pd) => rose.Contains("grimmchild") && pd.grimmChildLevel < 4,
                Zh = new[] { "团长塞给我一枚护符，里面像封着一团还在跳动的火。", "“去收集火焰”……他说得倒是轻巧。" },
                En = new[] { "The master pressed a charm into my hand. Something like a living flame is sealed inside.", "\"Gather the flames\"... He makes it sound easy." },
                TipZh = pd => Charms.Enabled ? "在护符背包里装上格林之子（2 个凹槽），它就会跟着你；戴着它时，地图上会标出要收集的梦魇之焰。"
                    : "把圣巢背包里的格林之子放进宠物栏，它就会跟着你；它跟着你时，地图上会标出要收集的梦魇之焰。",
                TipEn = pd => Charms.Enabled ? "Put the Grimmchild on in the Charm Bag (2 notches) and it follows you; while you wear it, the map marks the Nightmare flames to gather."
                    : "Put the Grimmchild from the Hallownest Bag in the pet slot and it follows you; while it does, the map marks the Nightmare flames to gather.",
            },
            new Beat
            {
                Id = "shimmer", Now = true,
                // The room the Knight is in (the seamless world walks into it without the game's own scene changing).
                When = (mod, pd) => World.TerraWorld.Live?.SceneName == Shimmer.Scene,
                Zh = new[] { "这口温泉……不对。水面泛着淡紫的光，四周的岩壁不见了，只剩一片望不到底的星空。", "我把手探进去，指尖像被什么轻轻拆开，又一点一点拼了回来。" },
                En = new[] { "This hot spring... isn't right. The water glows a soft lavender, and the rock around it is gone, leaving only stars with no bottom to them.", "I dip a hand in. My fingertips feel gently taken apart, then pieced back together bit by bit." },
                TipZh = pd => "这是微光。把物品丢进水里泡一会儿，它会变成另一样东西，或者拆回做它的材料；人泡进去不会有事，温泉照样回血。",
                TipEn = pd => "This is the Shimmer. Leave an item in the water for a moment and it turns into something else, or comes apart into what it was made of. Bathing in it is safe, and it still heals you like a hot spring.",
            },
            new Beat
            {
                Id = "dreamers",
                When = (mod, pd) => rose.Contains("dreamers"),
                Zh = new[] { "远处传来一声闷响，像一把锁终于松开了。", "……那座黑色的神殿，该为我开门了。" },
                En = new[] { "A dull boom from far away, like a lock finally giving way.", "...That black temple should open for me now." },
                TipZh = pd => "三位守梦者都已倒下，遗忘十字路的黑卵神殿不再封闭。",
                TipEn = pd => "All three Dreamers have fallen: the Black Egg Temple in the Forgotten Crossroads is sealed no longer.",
            },
        };

        static List<Line> talk;
        static int page;
        static float pageTime, pageLength, quietFor, checkAt, heldFor, shownFor, boxLook;
        static bool held, primed, boxUp;

        public static int DevSaid;
        public static string DevLast = "";

        /// <summary>A line of the player's or a tip is on screen (the Voice waits for it).</summary>
        public static bool Talking => talk != null;

        /// <summary>This save hears them: on unless turned off; a Creative save only once turned on.</summary>
        public static bool On
        {
            get
            {
                var mod = TerraMod.Instance;
                if (mod == null) return false;
                return mod.Creative ? mod.GrantedAbilities.Contains(OnFlag) : !mod.GrantedAbilities.Contains(OffFlag);
            }
        }

        public static void SetOn(bool on)
        {
            var mod = TerraMod.Instance;
            if (mod == null || on == On) return;
            if (on) { mod.GrantedAbilities.Remove(OffFlag); mod.GrantedAbilities.Add(OnFlag); }
            else { mod.GrantedAbilities.Add(OffFlag); mod.GrantedAbilities.Remove(OnFlag); }
            talk = null;
            Plugin.Log.LogInfo($"[小剧情] {(on ? "开启" : "关闭")}");
        }

        public static void Clear()
        {
            talk = null;
            held = false;
            primed = false;
            died = false;
            quietFor = 0f;
            lastShards = lastOre = -1;
            rose.Clear();
        }

        /// <summary>A dialogue box showing words (looked for at most four times a second: the search goes through the scene).</summary>
        static bool BoxUp()
        {
            if (Time.unscaledTime >= boxLook)
            {
                boxLook = Time.unscaledTime + 0.25f;
                boxUp = UI.HudDialogue.DevOpen;
            }
            return boxUp;
        }

        /// <summary>Nothing else going on (BossGuide.Quiet) and the inventory shut; on a bench, the game holding the Knight there counts.</summary>
        static bool Quiet(PlayerData pd, bool seated)
        {
            if (InventoryGui.Open || UI.FullMap.Open || Player.OpeningPortal.Active) return false;
            if (BossGuide.Quiet()) return true;
            if (!seated || pd == null || !pd.atBench) return false;
            var gm = GameManager.UnsafeInstance;
            return gm != null && gm.gameState == GlobalEnums.GameState.PLAYING && !gm.IsInSceneTransition && !UI.DialogueBroadcast.Telling && !BoxUp();
        }

        /// <summary>What holds a line already being said: the inventory or the map open, a room changing, a death, the game's own talk.</summary>
        static bool Holds(GameManager gm, HeroController hero) =>
            InventoryGui.Open || UI.FullMap.Open || gm.gameState != GlobalEnums.GameState.PLAYING || gm.IsInSceneTransition
            || hero.cState.transitioning || hero.cState.dead || UI.DialogueBroadcast.Telling || BoxUp();

        /// <summary>Each frame: what has happened looked at, and a beat's lines one after another.</summary>
        public static void Tick(bool playing)
        {
            var gm = GameManager.UnsafeInstance;
            if (gm == null || gm.gameState == GlobalEnums.GameState.MAIN_MENU)
            {
                Clear();
                return;
            }
            var mod = TerraMod.Instance;
            var pd = PlayerData.instance;
            var hero = HeroController.SilentInstance;
            if (!playing || Time.timeScale == 0f || mod == null || pd == null || hero == null) return;
            if (hero.cState.dead) died = true;
            if (!primed && gm.gameState == GlobalEnums.GameState.PLAYING)
            {
                primed = true;
                lastShards = Shards(pd);
                lastOre = pd.ore;
                lastGrimm = Charms.Got(Charms.Grimmchild);
                lastDreamers = Dreamers(pd);
                PrimeWatches(pd);
            }
            Detect(pd);
            if (!On)
            {
                talk = null;
                rose.Clear();
                return;
            }
            float dt = Time.unscaledDeltaTime;
            if (talk != null)
            {
                if (Holds(gm, hero))
                {
                    held = true;
                    quietFor = 0f;
                    heldFor += dt;
                    if (heldFor > MaxHeld)
                    {
                        Plugin.Log.LogInfo($"[小剧情] 被打断太久，不再说完（第 {page + 1}/{talk.Count} 句）");
                        talk = null;
                    }
                    return;
                }
                quietFor += dt;
                if (held)
                {
                    if (quietFor < QuietAgain) return;
                    held = false;
                    // A line barely begun starts over; one mostly read goes on from where it was.
                    if (pageTime < ReadEnough) pageTime = 0f;
                    shownFor = 0f;
                }
                shownFor += dt;
                pageTime += dt;
                if (pageTime >= pageLength) NextPage();
                return;
            }
            bool calm = Quiet(pd, false);
            quietFor = calm ? quietFor + dt : 0f;
            if (Time.unscaledTime < checkAt) return;
            checkAt = Time.unscaledTime + 0.5f;
            if (BossGuide.DevTalking) return;
            foreach (var b in Beats)
            {
                if (mod.GrantedAbilities.Contains(Prefix + b.Id)) continue;
                bool ready = b.Seated ? Quiet(pd, true) : b.Now ? calm : quietFor >= QuietBefore;
                if (!ready) continue;
                bool holds;
                try { holds = b.When(mod, pd); }
                catch (System.Exception) { holds = false; }
                if (!holds) continue;
                mod.GrantedAbilities.Add(Prefix + b.Id);
                Start(b, pd);
                break;
            }
            // A death without a shade (a dream, Godhome) is let go once back in play.
            if (died && !hero.cState.dead && calm && !ShadeLeft(pd)) died = false;
        }

        /// <summary>Something got since a moment ago (a mask shard, Pale Ore, the Grimmchild, the last Dreamer): kept until its beat is said.</summary>
        static void Detect(PlayerData pd)
        {
            if (!primed) return;
            int shards = Shards(pd);
            if (shards > lastShards) rose.Add("mask");
            lastShards = shards;
            if (pd.ore > lastOre) rose.Add("ore");
            lastOre = pd.ore;
            bool grimm = Charms.Got(Charms.Grimmchild);
            if (grimm && !lastGrimm) rose.Add("grimmchild");
            lastGrimm = grimm;
            bool dreamers = Dreamers(pd);
            if (dreamers && !lastDreamers) rose.Add("dreamers");
            lastDreamers = dreamers;
            DetectWatches(pd);
        }

        static void Start(Beat b, PlayerData pd)
        {
            talk = new List<Line>();
            var words = Chinese ? b.Zh : b.En;
            foreach (var w in words) talk.Add(new Line { Text = w });
            string tip = Chinese ? b.TipZh?.Invoke(pd) : b.TipEn?.Invoke(pd);
            if (!string.IsNullOrEmpty(tip)) talk.Add(new Line { Tip = true, Text = tip });
            if (b.Id == "death") died = false;
            rose.Remove(b.Id);
            b.Act?.Invoke();
            page = -1;
            held = false;
            heldFor = 0f;
            Plugin.Log.LogInfo($"[小剧情] {b.Id}");
            NextPage();
        }

        static string Speaker(Line l) => l.Tip ? Pick("提示", "Tip") : BossGuide.PlayerSpeaker;

        static void NextPage()
        {
            page++;
            if (talk == null || page >= talk.Count)
            {
                talk = null;
                return;
            }
            var l = talk[page];
            pageTime = 0f;
            shownFor = 0f;
            pageLength = Mathf.Clamp(1.6f + l.Text.Length * (Chinese ? 0.12f : 0.05f), 3f, l.Tip ? 11f : 8f);
            bool first = true;
            foreach (var part in BossGuide.ChatParts(l.Text))
            {
                Net.NetChat.Add(first ? (l.Tip ? $"[{Speaker(l)}] {part}" : $"<{Speaker(l)}> {part}") : "    " + part, l.Tip ? TipText : Color.white);
                first = false;
            }
            DevSaid++;
            DevLast = l.Text;
        }

        /// <summary>The line being said, where the Voice's go (BossGuide.DrawSubtitle): the player's name over theirs, "Tip" over a tip.</summary>
        public static void Draw()
        {
            if (talk == null || held || page < 0 || page >= talk.Count || InventoryGui.Open || Event.current.type != EventType.Repaint) return;
            var l = talk[page];
            float a = Mathf.Clamp01(Mathf.Min(pageTime, shownFor) / 0.3f) * Mathf.Clamp01((pageLength - pageTime) / 0.45f);
            if (a <= 0f) return;
            BossGuide.DrawSubtitle(Speaker(l), l.Text, l.Tip ? TipName : PlayerName, l.Tip ? TipText : Color.white, a);
        }

        /// <summary>Dev: a beat said now, whatever has happened (its flag set as when said).</summary>
        public static bool DevSay(string id)
        {
            var mod = TerraMod.Instance;
            var pd = PlayerData.instance;
            foreach (var b in Beats)
                if (b.Id == id && mod != null && pd != null)
                {
                    mod.GrantedAbilities.Add(Prefix + b.Id);
                    Start(b, pd);
                    return true;
                }
            return false;
        }
    }
}
