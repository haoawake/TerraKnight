using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// The story beats of everything Hollownest gives (the user, 2026-10-20: "骨钉技艺和这四种法术都应该有个泰君自己的小剧情",
    /// then "拿到每个护符的时候的小剧情，总之就是原版全都要小剧情"): the abilities and the Nailmasters' arts, the spells, the Dream
    /// Nail's three, the nail's honings, the key items and relics, the vessels and notches, the map, lifeblood, grubs, dream
    /// essence, the Hunter's, the Godseeker's and the Collector's things, each charm, the Kingsoul's three states, Carefree
    /// Melody and the three made unbreakable. Each is watched (<see cref="Watch"/>): said when its record rises in play, never
    /// for what the save had already. A charm's waits for the charm page, the 护符背包 / Charm Bag (Charms.Enabled); the first charm got opens the
    /// inventory on that page (the core "charm" beat). The tips say where the mod put each thing and how it is used here.
    /// </summary>
    internal static partial class StoryBeats
    {
        /// <summary>A record looked at each frame: a rise above what it was (from the save's own when play began) lets its beat be said.</summary>
        sealed class Watch
        {
            public string Id;
            public System.Func<PlayerData, int> Value;
            public int Last;
        }

        static readonly List<Watch> watches = new List<Watch>();
        static Beat[] all;

        static Beat[] Beats
        {
            get
            {
                if (all != null) return all;
                var list = new List<Beat>(coreBeats);
                list.AddRange(Hollownest());
                all = list.ToArray();
                return all;
            }
        }

        static void PrimeWatches(PlayerData pd)
        {
            var _ = Beats;
            foreach (var w in watches) w.Last = Value(w, pd);
        }

        static void DetectWatches(PlayerData pd)
        {
            for (int i = 0; i < watches.Count; i++)
            {
                var w = watches[i];
                int v = Value(w, pd);
                if (v > w.Last) rose.Add(w.Id);
                w.Last = v;
            }
        }

        static int Value(Watch w, PlayerData pd)
        {
            try { return w.Value(pd); }
            catch (System.Exception) { return w.Last; }
        }

        static void Watched(string id, System.Func<PlayerData, int> value) => watches.Add(new Watch { Id = id, Value = value });

        /// <summary>Dev: the ids of Hollownest's beats (this file's), in order.</summary>
        public static List<string> DevHollownestIds()
        {
            var ids = new List<string>();
            var b = Beats;
            for (int i = coreBeats.Length; i < b.Length; i++) ids.Add(b[i].Id);
            return ids;
        }

        /// <summary>Dev: a beat's lines and tip as they would be said now, in Chinese and in English.</summary>
        public static string DevText(string id)
        {
            var pd = PlayerData.instance;
            foreach (var b in Beats)
            {
                if (b.Id != id) continue;
                string tipZh = pd != null ? b.TipZh?.Invoke(pd) : null, tipEn = pd != null ? b.TipEn?.Invoke(pd) : null;
                return string.Join(" / ", b.Zh) + (tipZh != null ? " ｜提示：" + tipZh : "") + " ‖ " + string.Join(" / ", b.En) + (tipEn != null ? " | Tip: " + tipEn : "");
            }
            return null;
        }

        /// <summary>Dev: a beat's record taken as risen (its watch skipped), and the line being said dropped.</summary>
        public static void DevRise(string id) { rose.Clear(); rose.Add(id); }
        public static void DevHush() => talk = null;

        /// <summary>A beat for something got: said once its record holds after not holding (and also, if given, holds).</summary>
        static Beat Got(string id, System.Func<PlayerData, bool> has, string[] zh, string[] en,
            System.Func<PlayerData, string> tipZh = null, System.Func<PlayerData, string> tipEn = null, System.Func<bool> also = null)
        {
            Watched(id, pd => has(pd) ? 1 : 0);
            return new Beat { Id = id, When = (mod, pd) => rose.Contains(id) && (also == null || also()), Zh = zh, En = en, TipZh = tipZh, TipEn = tipEn };
        }

        static string[] L(params string[] lines) => lines;

        static string Key(Player.TerraKeys.Action a) => Player.TerraKeys.KeyName(Player.TerraKeys.Get(a));
        static bool Bound(Player.TerraKeys.Action a) => Player.TerraKeys.Get(a) != UnityEngine.InputSystem.Key.None;

        /// <summary>The use key's name, a mouse button's without its "click" ("按住左键蓄力", not "按住左键点击蓄力").</summary>
        static string UseKey
        {
            get
            {
                string k = Key(Player.TerraKeys.Action.UseItem);
                return k.EndsWith("点击") ? k.Substring(0, k.Length - 2) : k.EndsWith(" Click") ? k.Substring(0, k.Length - 6) : k;
            }
        }

        /// <summary>A spell's tip: its four class weapons given (where the ones that didn't fit are), and the Arms Dealer selling them.</summary>
        static string WeaponsTipZh(string spell)
        {
            string where = SpellRewards.LastDropped > 0 ? "已经交给你，背包放不下的掉在了脚边" : "已经放进背包";
            string shop = Town.TownNpcs.Lives("ArmsDealer") ? "德特茅斯的军火商那里也能再买到" : "等军火商住进德特茅斯，他那里也能再买到";
            return $"{spell}的力量凝成了四件职业武器（战士、射手、法师、召唤各一件），{where}。弄丢了也不要紧，{shop}。";
        }

        static string WeaponsTipEn(string spell)
        {
            string where = SpellRewards.LastDropped > 0 ? "are yours now; the ones your bag couldn't hold lie at your feet" : "are in your bag";
            string shop = Town.TownNpcs.Lives("ArmsDealer") ? "the Arms Dealer in Dirtmouth sells them too" : "once the Arms Dealer moves into Dirtmouth, his shop will have them too";
            return $"{spell}'s power has taken shape as four class weapons (one each for melee, ranged, magic and summoning), which {where}. Lose one and it's no matter: {shop}.";
        }

        /// <summary>An ability's accessory in the Hallownest Bag: worn, it works.</summary>
        static string WearZh(string was, string item, string how) => $"{was}变成了“{item}”，放进了圣巢背包。把它拖进饰品栏装上，{how}";
        static string WearEn(string was, string item, string how) => $"{was} has become the {item}, in your Hallownest Bag. Drag it into an accessory slot, and {how}";

        /// <summary>A fragile charm broken: still in the bag, no effect; the Leg Eater mends it (the game's price, times the shops'), Divine makes it whole for good.</summary>
        static string BrokenTipZh(int id, int vanillaRepair) =>
            $"{Charms.Name(id)}碎了：易碎护符戴着倒下就会碎，碎了还留在护符背包里，但没有效果。去真菌荒地找食腿者修好它（{vanillaRepair * HkShopPrices.Factor} 吉欧）；或者交给德特茅斯格林剧团帐篷里的迪瓦恩，换成坚固的，以后就不会再碎了。";
        static string BrokenTipEn(int id, int vanillaRepair) =>
            $"{Charms.Name(id)} has broken: a fragile charm worn when you fall breaks, and stays in the Charm Bag with no effect. The Leg Eater in the Fungal Wastes mends it ({vanillaRepair * HkShopPrices.Factor} Geo); or give it to Divine in the Grimm Troupe's tent in Dirtmouth for an unbreakable one that never breaks again.";

        static string CharmTipZh(int id) => $"{Charms.Name(id)}：占 {Charms.Cost(id)} 个凹槽，效果写在护符背包里。坐在长椅上时，打开护符背包点它就能戴上。";
        static string CharmTipEn(int id) => $"{Charms.Name(id)}: {Charms.Cost(id)} notch{(Charms.Cost(id) == 1 ? "" : "es")}, its effect described in the Charm Bag. Rest on a bench, then click it in the Charm Bag to wear it.";

        /// <summary>A charm's beat: its lines, the page's tip (and more, for the Sprintmaster's boots); only with the charm page.</summary>
        static Beat Charm(int id, string[] zh, string[] en, string moreZh = null, string moreEn = null) =>
            Got("charm" + id, pd => Charms.Got(id), zh, en, pd => CharmTipZh(id) + (moreZh ?? ""), pd => CharmTipEn(id) + (moreEn ?? ""), () => Charms.Enabled);

        static IEnumerable<Beat> Hollownest()
        {
            // The first charm (the core "charm" beat's watch).
            Watched("charm", pd => Charms.Owned > 0 ? 1 : 0);

            // ---------------------------------------------------------------- the Nailmasters' arts (hasDashSlash is the Great Slash, hasUpwardSlash the Dash Slash)
            yield return Got("cyclone", pd => pd.hasCyclone,
                L("那个老骨钉大师逼着我原地转了不知多少圈，才肯哼一声“还行”。", "……头还晕着。不过下回被一群虫子围住，我知道该怎么做了。"),
                L("The old Nailmaster made me spin on the spot more times than I could count before he'd grunt \"Not bad.\"", "...My head's still spinning. But the next time a swarm of bugs closes in, I'll know what to do."),
                pd => $"拿着骨钉按住{UseKey}蓄力，骨钉闪光后按住上或下再松开，就是旋风斩：转着圈连续砍中身边的敌人。",
                pd => $"With the Nail in hand, hold {UseKey} to charge; when it flashes, let go while holding up or down for the Cyclone Slash, spinning through the enemies around you.");
            yield return Got("dashslash", pd => pd.hasUpwardSlash,
                L("八百吉欧，只换来他冷冷的一句“看好了”。", "可那一刀快得我连影子都没看清……行吧，这钱花得不冤。"),
                L("Eight hundred Geo, and all I got was a cold \"Watch closely.\"", "But that cut was so fast I didn't even catch its shadow... All right. Money well spent."),
                pd => $"拿着骨钉按住{UseKey}蓄力，骨钉闪光后在冲刺中松开，就是冲刺斩，伤害是普通一刀的 2.5 倍。",
                pd => $"With the Nail in hand, hold {UseKey} to charge; when it flashes, let go mid-dash for the Dash Slash, 2.5 times a slash's damage.");
            yield return Got("greatslash", pd => pd.hasDashSlash,
                L("一位握着画笔的老战士，教给我的却是最沉的一刀。", "他说，蓄力和作画是一回事：别急，等那一笔自己落下来。"),
                L("An old warrior holding a paintbrush, and what he taught me was the heaviest cut of all.", "He said charging a blow is just like painting: don't rush it; wait for the stroke to fall on its own."),
                pd => $"拿着骨钉按住{UseKey}蓄力，骨钉闪光后不按上下直接松开，就是强力斩，伤害是普通一刀的 2.5 倍。",
                pd => $"With the Nail in hand, hold {UseKey} to charge; when it flashes, let go without up or down for the Great Slash, 2.5 times a slash's damage.");

            // ---------------------------------------------------------------- the spells (fireballLevel 1 / 2, screamLevel 1 / 2, quakeLevel 1 / 2)
            yield return Got("vengeful", pd => pd.fireballLevel >= 1,
                L("那位老萨满把一团冰凉的东西按进我手心，它顺着胳膊，一路钻进了胸口。", "我一抬手，它就尖叫着扑了出去……原来这片土地上的魂，是能拿来用的。"),
                L("The old shaman pressed something ice-cold into my palm, and it crept up my arm and into my chest.", "I raised my hand and it shrieked out of me... So the souls of this land can be put to use."),
                pd => WeaponsTipZh("复仇之魂"), pd => WeaponsTipEn("The Vengeful Spirit"));
            yield return Got("wraiths", pd => pd.screamLevel >= 1,
                L("一阵尖啸从地底冲上来，没有散去，全灌进了我的喉咙。", "现在只要我张开嘴，头顶就会炸开一群看不见的东西……耳朵到现在还在嗡嗡作响。"),
                L("A shriek rushed up from beneath the ground, and instead of fading it poured down my throat.", "Now I only have to open my mouth and something unseen bursts out above my head... My ears are still ringing."),
                pd => WeaponsTipZh("嚎叫幽灵"), pd => WeaponsTipEn("The Howling Wraiths"));
            yield return Got("shadesoul", pd => pd.fireballLevel >= 2,
                L("那团白色的魂……被染成了和我的暗影一样的黑。", "它更沉，也更饿。放出去的那一下，差点连我的胳膊一起拽走。"),
                L("That white soul... has been stained as black as my own shade.", "It's heavier now, and hungrier. When I let it go, it nearly took my arm with it."),
                pd => WeaponsTipZh("暗影之魂"), pd => WeaponsTipEn("The Shade Soul"));
            yield return Got("shriek", pd => pd.screamLevel >= 2,
                L("深渊底下，无数个和我一模一样的小小身影，在黑暗里一齐张开了嘴。", "那声音如今也住进了我身体里……我分不清，是我在用它，还是它在借我的喉咙。"),
                L("Down in the Abyss, countless little figures exactly like me opened their mouths together in the dark.", "That voice lives in me now too... I can't tell whether I'm using it, or it's borrowing my throat."),
                pd => WeaponsTipZh("深渊尖啸"), pd => WeaponsTipEn("The Abyss Shriek"));
            yield return Got("dive", pd => pd.quakeLevel >= 1,
                L("灵魂大师留下的那团魂，重得像一块铁。", "从高处往下一砸，连地板都裂开了……这下面，原来还有路。"),
                L("The soul the Soul Master left behind is as heavy as iron.", "Slamming down from above, I cracked the floor itself... and there's a way through underneath."),
                pd => WearZh("荒芜俯冲", "重锤符文", "在空中按住下再按跳跃，就会猛地砸向地面，还能砸开脆弱的地板。"),
                pd => WearEn("Desolate Dive", "Ram Rune", "in mid-air hold Down and press Jump to slam into the ground, breaking fragile floors."));
            yield return Got("descdark", pd => pd.quakeLevel >= 2,
                L("水晶山峰深处的那团黑暗，比灵魂大师的魂还要沉。", "往下砸的时候，四周好像都跟着暗了一下。"),
                L("The darkness deep in the Crystal Peak is heavier still than the Soul Master's soul.", "As I slam down, everything around me seems to dim for a moment."),
                pd => "“暗渊符文”放进了圣巢背包。把它拖进饰品栏装上，下砸的伤害是普通的两倍，落地时短暂无敌。",
                pd => "The Descending Dark rune is in your Hallownest Bag. Drag it into an accessory slot, and your slam deals twice the damage, with a moment's invincibility as you land.");

            // ---------------------------------------------------------------- the abilities (each an accessory in the Hallownest Bag)
            yield return Got("dash", pd => pd.hasDash,
                L("那个红披风的身影走了，留下一件轻得像蛾翼的披风。", "披上它，身子一下子轻了……往前一窜，就是好几步远。"),
                L("The figure in the red cloak is gone, and left behind a cloak as light as a moth's wing.", "With it on, my body suddenly feels light... One lunge and I'm several steps ahead."),
                pd => WearZh("蛾翼披风", "克苏鲁护盾", "按冲刺键就能冲刺。"),
                pd => WearEn("The Mothwing Cloak", "Shield of Cthulhu", "the Dash key dashes."));
            yield return Got("walljump", pd => pd.hasWalljump,
                L("螳螂部族的爪子，扣在手上冰凉而锋利。", "现在再陡的墙，我也能贴上去、蹬着往上爬了。"),
                L("The Mantis tribe's claw, cold and sharp on my hand.", "Now even the sheerest wall is something I can cling to and kick my way up."),
                pd => WearZh("螳螂爪", "攀爬爪", "贴着墙就能抓住，再按跳跃蹬墙跳。"),
                pd => WearEn("The Mantis Claw", "Climbing Claws", "you cling to walls; press Jump to kick off."));
            yield return Got("superdash", pd => pd.hasSuperDash,
                L("矿工们留下的机器核心里，嵌着一颗还在发光的水晶之心。", "攥紧它，能感到一股劲在胸口越积越满——松手的那一刻，大概就会被它射出去吧。"),
                L("In the core of a machine the miners left behind sits a Crystal Heart, still glowing.", "Gripping it, I feel power gathering in my chest. The moment I let go, it'll probably fling me forward."),
                pd => "水晶之心放进了圣巢背包。把它拖进饰品栏装上，长按冲刺键蓄力、松开就是超级冲刺，空中也能用；轻点冲刺键仍是普通冲刺。",
                pd => "The Crystal Heart is in your Hallownest Bag. Drag it into an accessory slot, then hold Dash to charge and let go for a Super Dash, even in the air; a tap of Dash is still an ordinary dash.");
            yield return Got("doublejump", pd => pd.hasDoubleJump,
                L("那个破碎的容器倒下了，留下一对淡金色的翅膀。", "跳到最高处时再一振翅——原来天空没有那么远。"),
                L("The Broken Vessel has fallen, leaving a pair of pale golden wings.", "At the top of a jump, one more beat of the wings... The sky isn't so far away after all."),
                pd => WearZh("帝王之翼", "云朵瓶", "在空中再按一次跳跃就能二段跳。"),
                pd => WearEn("The Monarch Wings", "Cloud in a Bottle", "press Jump again in mid-air to jump twice."));
            yield return Got("acid", pd => pd.hasAcidArmour,
                L("那滴眼泪裹在身上，像一层看不见的薄膜。", "那些会咬人的酸水，现在碰到我只会乖乖滑开。"),
                L("The tear wraps around me like an invisible film.", "That biting acid now just slides off me."),
                pd => "现在能在酸水里游泳，不会受伤。它还变成了一副“脚蹼”，放进了圣巢背包，装进饰品栏后在水里也能游泳。",
                pd => "You can now swim in acid unharmed. It has also become a pair of Flippers, in your Hallownest Bag: wear them to swim in water as well.");
            yield return Got("shadowdash", pd => pd.hasShadowDash,
                L("深渊最深处的那件披风，黑得能吞掉光。", "披上它冲出去的一瞬间，我好像变成了一团影子——敌人的爪子从我身上穿了过去。"),
                L("The cloak from the deepest part of the Abyss, black enough to swallow light.", "The instant I dash in it, I seem to turn into a shadow, and the enemy's claws pass right through me."),
                pd => "暗影披风放进了圣巢背包。把它拖进饰品栏装上，" + (Bound(Player.TerraKeys.Action.ShadowDash) ? $"按 {Key(Player.TerraKeys.Action.ShadowDash)}（或双击方向键）" : "双击方向键")
                    + "就能暗影冲刺：穿过敌人和它们的攻击，还能穿过暗影之门。",
                pd => "The Shade Cloak is in your Hallownest Bag. Drag it into an accessory slot, then " + (Bound(Player.TerraKeys.Action.ShadowDash) ? $"press {Key(Player.TerraKeys.Action.ShadowDash)} (or double-tap a direction)" : "double-tap a direction")
                    + " to shadow dash through enemies, their attacks and shadow gates.");

            // ---------------------------------------------------------------- the Dream Nail's three
            yield return Got("dreamnail", pd => pd.hasDreamNail,
                L("先知把一把看不见刃的钉子交到我手里。", "她说，用它刺进去，就能听见生物心里的话……甚至走进它们的梦。"),
                L("The Seer placed a nail with an invisible blade in my hand.", "She says that piercing with it lets me hear what creatures are thinking... even walk into their dreams."),
                pd => "梦之钉放进了背包。拿在手上，按住左键或右键蓄力、松开挥出：能读出生物的想法，进入梦境。",
                pd => "The Dream Nail is in your bag. With it in hand, hold the left or right button to charge and let go to swing: it reads creatures' thoughts and opens dreams.");
            yield return Got("dreamgate", pd => pd.hasDreamGate,
                L("先知教我在现实里钉下一扇梦之门。", "不管走多远，只要还记得门在哪，就能一步回去。"),
                L("The Seer taught me to pin a Dreamgate into the waking world.", "However far I go, as long as I remember where it stands, one step takes me back."),
                pd => "拿着梦之钉，按住下键挥出就放下梦之门；按住上键挥出，就传送回梦之门。",
                pd => "With the Dream Nail in hand, hold Down as you swing to set a Dreamgate; hold Up as you swing to warp back to it.");
            yield return Got("awoken", pd => pd.dreamNailUpgraded,
                L("先知说，梦之钉醒了。", "握着它，能听见更远处、更深处的低语。"),
                L("The Seer says the Dream Nail has awoken.", "Holding it, I can hear whispers from farther off, and deeper down."),
                pd => "梦之钉的伤害提高了 50%。", pd => "The Dream Nail deals 50% more damage.");

            // ---------------------------------------------------------------- the Nailsmith's honings
            yield return Got("nail1", pd => pd.nailSmithUpgrades >= 1,
                L("骨钉匠把我的骨钉打磨了一遍，刃口亮得能照出人影。"),
                L("The Nailsmith honed my nail; the edge is bright enough to show my reflection."),
                pd => "骨钉的伤害提高了（每磨砺一次 +25%）。再往后，还要带上苍白矿石。",
                pd => "Your Nail deals more damage (25% more for each honing). The next ones will need Pale Ore as well.");
            yield return Got("nail2", pd => pd.nailSmithUpgrades >= 2,
                L("第二次锻打。骨钉里像多了一条看不见的纹路，挥起来更顺手了。"),
                L("A second forging. It's as if an unseen grain runs through the nail now; it swings more true."),
                pd => "骨钉的伤害又提高了。", pd => "Your Nail deals more damage again.");
            yield return Got("nail3", pd => pd.nailSmithUpgrades >= 3,
                L("骨钉匠说这是他这辈子打过最好的一把。", "……他每次都这么说吗？"),
                L("The Nailsmith says it's the best nail he's ever made.", "...Does he say that every time?"),
                pd => "骨钉的伤害又提高了。", pd => "Your Nail deals more damage again.");
            yield return Got("nail4", pd => pd.nailSmithUpgrades >= 4,
                L("纯粹之钉。", "骨钉匠盯着它看了很久，久到我以为他舍不得还给我了。"),
                L("The Pure Nail.", "The Nailsmith stared at it so long I thought he wouldn't give it back."),
                pd => "骨钉磨砺到了尽头，伤害是最初的两倍。", pd => "Your Nail is honed as far as it goes: twice the damage it began with.");

            // ---------------------------------------------------------------- key items (in the Hallownest Bag: carried, they count)
            yield return Got("lantern", pd => pd.hasLantern,
                L("一盏装着光蝇的小灯笼。光不算亮，但足够让我看清脚下。"),
                L("A little lantern with a lumafly inside. Its light isn't strong, but it's enough to see where I'm stepping."),
                pd => "光蝇灯笼放进了圣巢背包，带在身上就能照亮漆黑的房间；放进照明宠物栏，它还会跟着你发光。",
                pd => "The Lumafly Lantern is in your Hallownest Bag: carry it and pitch-dark rooms light up. Put it in the light pet slot and it follows you, glowing.");
            yield return Got("trampass", pd => pd.hasTramPass,
                L("一张电车通行证。圣巢里还在运转的东西不多了，这算一个。"),
                L("A tram pass. Not much in Hallownest still runs; this is one of them."),
                pd => "电车通行证放进了圣巢背包，带在身上就能乘坐电车。", pd => "The Tram Pass is in your Hallownest Bag; carry it to ride the trams.");
            yield return Got("citycrest", pd => pd.hasCityKey,
                L("从那具空壳里掉出一枚纹章，上面刻着泪水之城的标记。"),
                L("A crest fell out of that empty shell, carved with the mark of the City of Tears."),
                pd => "城市纹章放进了圣巢背包。带在身上，泪水之城的大门就会为你打开。",
                pd => "The City Crest is in your Hallownest Bag. Carry it and the gate to the City of Tears will open for you.");
            yield return Got("simplekey", pd => pd.simpleKeys > 0,
                L("一把再普通不过的钥匙。圣巢某处，总有一扇门在等它。"),
                L("The plainest of keys. Somewhere in Hallownest, there's always a door waiting for one."),
                pd => "简单钥匙放进了圣巢背包，带在身上就能打开一扇普通的锁门，用一次就没了。",
                pd => "The Simple Key is in your Hallownest Bag; carry it to open a plain locked door. It's used up once used.");
            yield return Got("slykey", pd => pd.hasSlykey,
                L("一把刻着斯莱小店标记的钥匙……那个小商人丢东西倒是挺随意。"),
                L("A key marked with Sly's shop sign... That little merchant is careless with belongings."),
                pd => "把店主的钥匙带回德特茅斯交给斯莱，店里会多出几样东西。",
                pd => "Bring the Shopkeeper's Key back to Sly in Dirtmouth, and the shop will have a few more things for sale.");
            yield return Got("whitekey", pd => pd.hasWhiteKey,
                L("一把精致得过分的钥匙。斯莱说，它能打开城里一扇很讲究的门。"),
                L("An overly elegant key. Sly says it opens a very particular door in the city."),
                pd => "典雅的钥匙放进了圣巢背包。带在身上，能打开灵魂圣所里那扇雕花的门。",
                pd => "The Elegant Key is in your Hallownest Bag. Carry it to open the ornate door in the Soul Sanctum.");
            yield return Got("lovekey", pd => pd.hasLoveKey,
                L("一把心形的钥匙。拿着它的时候，莫名有点不自在。"),
                L("A heart-shaped key. Holding it makes me oddly self-conscious."),
                pd => "爱之钥放进了圣巢背包。带在身上，能打开泪水之城的爱之塔。",
                pd => "The Love Key is in your Hallownest Bag. Carry it to open the Tower of Love in the City of Tears.");
            yield return Got("kingsbrand", pd => pd.hasKingsBrand,
                L("巨大的骸骨里留着一道烙印。它烫在我身上，却一点也不疼。", "这是……王的印记。"),
                L("Inside the huge bones, a brand was left behind. It burned into me, yet didn't hurt at all.", "This is... the mark of the King."),
                pd => "王之印记放进了圣巢背包。带在身上，深渊那扇刻着王之印记的大门就会为你打开。",
                pd => "The King's Brand is in your Hallownest Bag. Carry it and the Abyss's door bearing the brand will open for you.");
            yield return Got("flower", pd => pd.hasXunFlower,
                L("那位悲伤的老人托我送一朵花。花瓣薄得一碰就要碎。"),
                L("The grieving old mourner asked me to deliver a flower. Its petals are so thin a touch might break them."),
                pd => "带着娇嫩的花去王后花园深处那座孤零零的墓前。路上只要挨一下打，花就碎了。",
                pd => "Carry the Delicate Flower to the lonely grave deep in the Queen's Gardens. A single hit on the way and it breaks.");
            yield return Got("rancidegg", pd => pd.rancidEggs > 0,
                L("一颗腐臭蛋……味道冲得我眼泪都要出来了。真不知道谁会想要这种东西。"),
                L("A rancid egg... The smell nearly brings tears to my eyes. Who would ever want this?"),
                pd => "腐臭蛋放进了圣巢背包。带一颗去德特茅斯找杰吉，就能把你的暗影召回来。",
                pd => "The Rancid Egg is in your Hallownest Bag. Bring one to Jiji in Dirtmouth to call your shade back.");

            // ---------------------------------------------------------------- relics (Lemm buys them)
            System.Func<PlayerData, string> relicZh = pd => "遗物放进了圣巢背包。泪水之城的莱姆会高价收购，带在身上去他的店里卖。";
            System.Func<PlayerData, string> relicEn = pd => "Relics go into your Hallownest Bag. Lemm in the City of Tears pays well for them: carry them to his shop.";
            yield return Got("journal", pd => pd.trinket1 > 0, L("一本被翻烂的日记，写满了一个旅人在圣巢的见闻。"),
                L("A well-thumbed journal, full of a traveller's notes on Hallownest."), relicZh, relicEn);
            yield return Got("seal", pd => pd.trinket2 > 0, L("一枚沉甸甸的印章，刻着圣巢的徽记。"),
                L("A heavy seal stamped with the crest of Hallownest."), relicZh, relicEn);
            yield return Got("idol", pd => pd.trinket3 > 0, L("一尊小小的国王神像，不知被谁擦得锃亮。"),
                L("A small idol of the King, polished bright by someone."), relicZh, relicEn);
            yield return Got("arcaneegg", pd => pd.trinket4 > 0, L("一枚神秘的蛋，壳上的花纹像是在慢慢流动。"),
                L("An arcane egg. The patterns on its shell seem to drift slowly."), relicZh, relicEn);

            // ---------------------------------------------------------------- vessels, notches, the map, lifeblood, grubs, essence
            yield return Got("vesselshard", pd => pd.vesselFragments > 0 || pd.MPReserveMax > 0,
                L("一块灵魂容器的碎片，摸上去空落落的，像在等着被填满。"),
                L("A fragment of a soul vessel. It feels hollow, as if waiting to be filled."),
                pd => "每集齐三块灵魂容器碎片，就能拼成一个完整的容器，魔力上限提高 20 点。",
                pd => "Every three Vessel Fragments make a whole vessel, raising your maximum mana by 20.");
            yield return Got("vessel", pd => pd.MPReserveMax > 0,
                L("碎片拼在一起了。容器里慢慢涨起一股暖流。"),
                L("The fragments came together. A warm tide slowly rises in the vessel."),
                pd => "魔力上限提高了 20 点。", pd => "Your maximum mana is 20 higher.");
            Watched("notch", pd => pd.charmSlots);
            yield return new Beat
            {
                Id = "notch", When = (mod, pd) => Charms.Enabled && rose.Contains("notch"),
                Zh = L("又多了一个护符凹槽。身上能容下的东西，好像又多了一点。"),
                En = L("Another charm notch. It feels like I can carry a little more with me."),
                TipZh = pd => $"护符凹槽多了一个，现在一共 {pd.charmSlots} 个。",
                TipEn = pd => $"One more charm notch: {pd.charmSlots} in all.",
            };
            yield return Got("map", pd => pd.hasMap,
                L("一张地图！虽然画得有点潦草，总比在黑暗里乱撞强。"),
                L("A map! A bit scrawled, but better than stumbling about in the dark."),
                pd => $"按 {Key(Player.TerraKeys.Action.FullMap)} 打开世界地图。买下的地图会画在上面，没去过的地方颜色淡一些。",
                pd => $"Press {Key(Player.TerraKeys.Action.FullMap)} to open the world map. The maps you buy are drawn on it, the places you haven't been fainter.");
            yield return Got("lifeblood", pd => pd.healthBlue > 0,
                L("一团蓝色的血涌进身体，胸口多了一层冰凉的护甲。"),
                L("A rush of blue blood floods into me, and a cool armour forms over my chest."),
                pd => "生命血给你一层 40 点的银色护盾和 40 点临时魔力，挨打时先扣护盾。",
                pd => "Lifeblood gives you a silver shield of 40 and 40 temporary mana; hits take the shield first.");
            yield return Got("grub", pd => pd.grubsCollected > 0,
                L("一只小幼虫从罐子里蹦出来，蹭了蹭我，然后欢快地钻进了地里。"),
                L("A little grub hopped out of its jar, nuzzled me, and burrowed happily into the ground."),
                pd => "被困的幼虫会标在世界地图上。救出的幼虫越多，遗忘十字路的幼虫之父给的谢礼越多。",
                pd => "Trapped grubs are marked on the world map. The more you free, the more the Grubfather in the Forgotten Crossroads gives you.");
            yield return Got("essence", pd => pd.dreamOrbs > 0,
                L("从梦里带出来一点发光的东西，捏在手里，像一小撮凉凉的星光。"),
                L("I brought something glowing out of the dream. In my hand it's like a pinch of cool starlight."),
                pd => "梦境精华放在钱币栏里。带去休憩之地的先知那里，能换来她的谢礼。",
                pd => "Dream Essence sits in your coin slots. Take it to the Seer in the Resting Grounds for her rewards.");

            // ---------------------------------------------------------------- the Hunter's, the Godseeker's, the Collector's, World Sense, Salubra's blessing
            yield return Got("huntersjournal", pd => pd.hasJournal,
                L("那个猎人丢给我一本日志：“把你杀过的东西都记下来。”"),
                L("The Hunter tossed me a journal: \"Record everything you kill.\""),
                pd => "打败过的敌人都记在背包里的“怪物图鉴”里，掉落和数据都查得到。",
                pd => "The enemies you defeat are recorded in the Bestiary in your inventory, with their drops and stats.");
            yield return Got("huntersmark", pd => pd.hasHuntersMark,
                L("猎人把他的印记给了我。", "……被一个只认猎物的家伙认可，感觉有点奇怪。"),
                L("The Hunter gave me his mark.", "...Being acknowledged by someone who only respects prey feels strange."));
            yield return Got("godtuner", pd => pd.hasGodfinder,
                L("神之调谐器在手心里嗡嗡震动，像在回应某处的祈祷。"),
                L("The Godtuner hums in my palm, as if answering a prayer from somewhere."));
            yield return Got("collectormap", pd => pd.hasPinGrub,
                L("收藏家的地图，上面密密麻麻全是标记……它可真舍得下功夫。"),
                L("The Collector's map, crammed with markings... It certainly put in the effort."));
            yield return Got("worldsense", pd => pd.unlockedCompletionRate,
                L("黑卵神殿里，那个空洞的影子让我看清了自己走了多远。"),
                L("In the Black Egg Temple, the hollow figure showed me how far I've come."));
            yield return Got("salubra", pd => pd.salubraBlessing,
                L("那位护符商人的祝福……她亲了我一下。", "脸上到现在还有点烫。"),
                L("The charm seller's blessing... She kissed me.", "My face still feels warm."));

            // ---------------------------------------------------------------- the Kingsoul's three states (royalCharmState 1-2 a fragment, 3 the Kingsoul, 4 the Void Heart)
            yield return Got("whitefragment", pd => pd.royalCharmState >= 1,
                L("一片洁白的碎片，冰凉而轻盈，像从某个更大的东西上掰下来的。"),
                L("A pure white fragment, cool and light, as if broken off something much greater."),
                pd => Charms.Enabled ? "白色碎片在护符背包里，还戴不上：它缺了另一半。" : null,
                pd => Charms.Enabled ? "The White Fragment is in the Charm Bag but can't be worn yet: it's missing its other half." : null);
            yield return Got("kingsoul", pd => pd.royalCharmState >= 3,
                L("两片碎片合在了一起，成了一枚完整的护符。", "它温柔地发着光，像是在呼唤我。"),
                L("The two fragments joined into one whole charm.", "It glows gently, as if calling to me."),
                pd => Charms.Enabled ? $"国王之魂在护符背包里，戴上要 {Charms.Cost(Charms.Royal)} 个凹槽。" : "国王之魂放进了圣巢背包，带在身上就算戴着。",
                pd => Charms.Enabled ? $"The Kingsoul is in the Charm Bag; wearing it takes {Charms.Cost(Charms.Royal)} notches." : "The Kingsoul is in your Hallownest Bag; carrying it counts as wearing it.");
            yield return Got("voidheart", pd => pd.royalCharmState >= 4,
                L("深渊深处，我听见了它们所有的声音。", "那些和我一样的影子不再四处游荡了……它们都回到了这里，回到了我心里。"),
                L("Deep in the Abyss, I heard all of their voices.", "The shadows like me no longer wander... They've all come back here, into my heart."),
                pd => Charms.Enabled ? "国王之魂变成了虚空之心：它自己戴上了，不占凹槽，以后也取不下来。" : "国王之魂变成了虚空之心，在圣巢背包里，带在身上就算戴着。",
                pd => Charms.Enabled ? "The Kingsoul has become the Void Heart: it put itself on, takes no notches, and never comes off." : "The Kingsoul has become the Void Heart, in your Hallownest Bag; carrying it counts as wearing it.");
            yield return Got("melody", pd => pd.grimmChildLevel >= 5,
                L("剧团的火熄灭了，帐篷不见了。", "留在手心里的，只有一段无忧无虑的旋律。"),
                L("The troupe's fire has gone out, and the tent is gone.", "All that's left in my hand is a carefree melody."),
                pd => Charms.Enabled ? "格林之子变成了无忧旋律，在护符背包里。" : null,
                pd => Charms.Enabled ? "The Grimmchild has become Carefree Melody, in the Charm Bag." : null);
            yield return Got("heartunbreakable", pd => pd.fragileHealth_unbreakable,
                L("剧团里那位胃口惊人的家伙把易碎心脏吞下去，又吐了回来。", "……它现在坚固得再也不会碎了。"),
                L("That troupe member with the astonishing appetite swallowed my Fragile Heart and spat it back out.", "...Now it will never break."),
                pd => Charms.Enabled ? $"{Charms.Name(23)}不会再碎了：倒下也不用找食腿者修。" : null,
                pd => Charms.Enabled ? $"{Charms.Name(23)} won't break any more: no need for the Leg Eater after a fall." : null);
            yield return Got("greedunbreakable", pd => pd.fragileGreed_unbreakable,
                L("又是一大笔吉欧，又是一次吞下吐出。", "这枚贪婪，现在怎么摔也摔不碎了。"),
                L("Another pile of Geo, another swallow and spit.", "This Greed won't shatter now, however I fall."),
                pd => Charms.Enabled ? $"{Charms.Name(24)}不会再碎了：倒下也不用找食腿者修。" : null,
                pd => Charms.Enabled ? $"{Charms.Name(24)} won't break any more: no need for the Leg Eater after a fall." : null);
            yield return Got("heartbroken", pd => pd.brokenCharm_23,
                L("倒下的时候，胸口有什么东西碎了。", "是那枚易碎心脏……碎片还在，得找人把它补好。"),
                L("As I fell, something at my chest shattered.", "That Fragile Heart... the pieces are still here. Someone will have to mend it."),
                pd => Charms.Enabled ? BrokenTipZh(23, 200) : null, pd => Charms.Enabled ? BrokenTipEn(23, 200) : null, () => Charms.Enabled);
            yield return Got("greedbroken", pd => pd.brokenCharm_24,
                L("贪婪的护符碎成了几片。", "倒下的时候，连捡钱的运气也一起摔没了。"),
                L("The greedy charm has broken into pieces.", "When I fell, my luck with Geo went down with me."),
                pd => Charms.Enabled ? BrokenTipZh(24, 150) : null, pd => Charms.Enabled ? BrokenTipEn(24, 150) : null, () => Charms.Enabled);
            yield return Got("strengthbroken", pd => pd.brokenCharm_25,
                L("握着骨钉的手忽然轻了。", "易碎力量碎了……那股力气也跟着散了。"),
                L("The hand holding my nail suddenly feels lighter.", "Fragile Strength has broken... and its strength has scattered with it."),
                pd => Charms.Enabled ? BrokenTipZh(25, 350) : null, pd => Charms.Enabled ? BrokenTipEn(25, 350) : null, () => Charms.Enabled);
            yield return Got("strengthunbreakable", pd => pd.fragileStrength_unbreakable,
                L("最后一枚也坚固了。", "说真的，我不太想知道它在那个肚子里经历了什么。"),
                L("The last one is unbreakable too.", "Honestly, I'd rather not know what it went through in that stomach."),
                pd => Charms.Enabled ? $"{Charms.Name(25)}不会再碎了：倒下也不用找食腿者修。" : null,
                pd => Charms.Enabled ? $"{Charms.Name(25)} won't break any more: no need for the Leg Eater after a fall." : null);

            // ---------------------------------------------------------------- the charms (36 and 40 above and in the core beats)
            yield return Charm(1, L("一枚嗡嗡作响的护符。戴着它，掉在地上的东西好像都会自己找过来。"),
                L("A charm that hums faintly. Wear it, and whatever falls to the ground seems to come looking for me."));
            yield return Charm(2, L("一枚指针乱转的指南针……说是能帮迷路的人认清方向。", "希望它比看起来靠谱。"),
                L("A compass whose needle won't sit still... Supposedly it helps the lost find their way.", "I hope it's more reliable than it looks."));
            yield return Charm(3, L("幼虫之父塞给我一枚护符，里面隐约传出一群小家伙细细的歌声。"),
                L("The Grubfather pressed a charm on me. Faintly, from inside, comes the thin singing of a crowd of little ones."));
            yield return Charm(4, L("一枚沉甸甸的护符，摸上去像一块磨得发亮的甲壳。"),
                L("A heavy charm. It feels like a piece of shell worn smooth and bright."));
            yield return Charm(5, L("从那只缩成一团的巴德尔身上掉下来的壳……", "原来它们是靠这个挡住一切的。"),
                L("The shell that fell from that curled-up Baldur...", "So this is how they shrug everything off."));
            yield return Charm(6, L("一枚透着红光的护符。握紧时，像有一股不服输的怒气顺着指尖烧上来。"),
                L("A charm with a red glow. When I grip it, a stubborn anger seems to burn up through my fingers."));
            yield return Charm(7, L("那位护符商人卖的护符，亮晶晶的，带着一股说不清的甜香。"),
                L("One of the charm seller's charms, glittering, with a sweet scent I can't quite place."));
            yield return Charm(8, L("一颗泛着蓝光的小小心脏。贴在胸口时，能感到它和我的心跳叠在了一起。"),
                L("A small heart glowing blue. Held to my chest, I can feel it beating along with mine."));
            yield return Charm(9, L("深渊里，一扇只为满身生命血的人打开的门后，藏着这颗还在搏动的核心。"),
                L("Deep in the Abyss, behind a door that opens only to someone brimming with lifeblood, this core was still beating."));
            yield return Charm(10, L("粪堆守卫留下的纹章……还带着一股挥之不去的味道。", "不管他守着的是什么，他是真心的。"),
                L("The crest the Dung Defender left behind... It still carries a smell that won't go away.", "Whatever he was guarding, he meant it."));
            yield return Charm(11, L("吸虫之母留下的巢，里面有什么东西还在蠕动。", "我尽量不去想。"),
                L("The nest the Flukemarm left behind. Something inside is still squirming.", "I try not to think about it."));
            yield return Charm(12, L("在那片荆棘里摸爬了半天，换来一圈带刺的藤蔓。", "它好像比我还记仇。"),
                L("Crawling through those thorns for ages got me a ring of spiked vines.", "It seems to hold a grudge even better than I do."));
            yield return Charm(13, L("螳螂领主们把这枚印记交给了我。", "能被她们认可，比打赢那一仗还难。"),
                L("The Mantis Lords handed me this mark.", "Earning their respect was harder than winning the fight."));
            yield return Charm(14, L("据说戴上它，挨打的时候脚下就像生了根。"),
                L("They say that wearing it, my feet take root when I'm struck."));
            yield return Charm(15, L("一枚压手的护符。据说挥出去的每一下，都能把对手推得更远。"),
                L("A charm that weighs down my hand. They say every blow it lands drives the enemy farther back."));
            yield return Charm(16, L("一片薄得几乎看不见的黑影，边缘却锋利得能割开空气。"),
                L("A sliver of shadow so thin it's barely there, yet its edge could cut the air."));
            yield return Charm(17, L("一朵小蘑菇，一碰就喷出呛人的孢子。", "……阿嚏。"),
                L("A little mushroom that puffs out choking spores at the slightest touch.", "...Achoo."));
            yield return Charm(18, L("据说戴上它，骨钉能够得更远一点。", "一点点也好。"),
                L("They say wearing it lets the nail reach a little farther.", "Even a little helps."));
            yield return Charm(19, L("一块刻着古老符号的石头，摸上去暖暖的，像还记得那些萨满的咒语。"),
                L("A stone carved with old symbols. It's warm to the touch, as if it still remembers the shamans' chants."));
            yield return Charm(20, L("萨满的祖坟里留下的护符。据说能从敌人身上多榨出一点魂来。"),
                L("A charm left in the shamans' ancestral mound. They say it wrings a little more soul out of enemies."));
            yield return Charm(21, L("一枚饿得发慌的护符……它想吞下的魂，比我愿意给的还多。"),
                L("A ravenous charm... It wants to swallow more soul than I'd care to give."));
            yield return Charm(22, L("一团发着光的软囊，里面有小小的东西在等着孵出来。"),
                L("A soft, glowing sac. Little things inside are waiting to hatch."));
            yield return Charm(23, L("食腿者卖的护符，漂亮是漂亮，就是脆得让人不敢倒下。"),
                L("A charm from the Leg Eater. Pretty enough, but so fragile I wouldn't dare fall while wearing it."));
            yield return Charm(24, L("据说戴上它，敌人掉的吉欧会多一些……前提是我别倒下，不然它就碎了。"),
                L("They say enemies drop more Geo while I wear it... as long as I don't fall, or it shatters."));
            yield return Charm(25, L("握着它，连骨钉都重了几分。", "可惜它和另外两枚一样，经不起一次倒下。"),
                L("Holding it, even the nail feels heavier.", "Shame it's like the other two: one fall and it's gone."));
            yield return Charm(26, L("斯莱说，这是给学会全部骨钉技艺的人的。", "那个小个子商人……原来也曾是一位骨钉大师。"),
                L("Sly says it's for those who've mastered every nail art.", "That little shopkeeper... was a Nailmaster once, too."));
            yield return Charm(27, L("乔尼长眠的地方留下了这枚护符。", "听说她被逐出了家乡，却仍然祝福着每一个路过的人。"),
                L("This charm was left where Joni rests.", "They say she was cast out of her home, yet still blessed everyone who passed."));
            yield return Charm(28, L("湖底的乌恩把她的样子分了一点给我。", "我现在大概有点像一只蛞蝓。"),
                L("Unn, at the bottom of the lake, shared a little of her shape with me.", "I suppose I look a bit like a slug now."));
            yield return Charm(29, L("蜂巢深处的一滴金色蜜血。温热，黏稠，带着蜂群守护彼此的那股固执。"),
                L("A drop of golden hive blood from deep in the Hive. Warm, thick, and as stubborn as bees guarding each other."));
            yield return Charm(30, L("先知说，这是给真正懂得做梦的人的。", "梦之钉握在手里，似乎轻了一些。"),
                L("The Seer says it's for those who truly understand dreams.", "The Dream Nail feels a little lighter in my hand."));
            yield return Charm(31, L("一座冲刺者雕像旁留下的护符。", "看来这里的虫子，都把跑得快当成一种信仰。"),
                L("A charm left by the statue of a dashing bug.", "The bugs around here seem to treat speed like a religion."));
            yield return Charm(32, L("一枚轻得出奇的护符。握着它，连呼吸都跟着快了半拍。"),
                L("A surprisingly light charm. Holding it, even my breathing quickens half a beat."));
            yield return Charm(33, L("灵魂圣所的学者们留下的东西，上面爬满了我看不懂的公式。"),
                L("Something the Soul Sanctum's scholars left behind, crawling with formulas I can't read."));
            yield return Charm(34, L("水晶山峰深处，一块晶簇里裹着这枚护符。", "它沉静得让人想闭上眼睛。"),
                L("Deep in the Crystal Peak, this charm was wrapped in a cluster of crystal.", "It's so still it makes me want to close my eyes."));
            yield return Charm(35, L("幼虫之父最后的谢礼。", "那些小家伙长大以后……原来这么美。"),
                L("The Grubfather's last gift.", "So this is what the little ones become when they grow up... They're beautiful."));
            yield return Charm(37, L("斯莱卖给我的护符，轻快得像要从掌心溜走。"),
                L("A charm Sly sold me, so nimble it seems ready to slip out of my palm."),
                "斯莱还附送了一双赫尔墨斯靴，放在圣巢背包里，装进饰品栏就能疾跑。",
                " Sly threw in a pair of Hermes Boots too, in your Hallownest Bag: wear them to sprint.");
            yield return Charm(38, L("一枚在梦与醒之间浮动的护符。", "它的光，像是从别人的梦里借来的。"),
                L("A charm that drifts between dreaming and waking.", "Its light seems borrowed from someone else's dream."));
            yield return Charm(39, L("织者们离开后的巢里，还回荡着她们的歌。", "几只小小的丝虫跟着我爬了出来。"),
                L("In the den the weavers left, their song still echoes.", "A few tiny silk-spinners crawled out after me."));
        }
    }
}
