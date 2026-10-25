namespace TerraKnight.Items
{
    /// <summary>
    /// The Voice Beyond the Door's words (docs/神的指引-对话审阅稿.md): for each of the 48 fights, in the order a journey
    /// meets them, stage by stage (the bestiary's stages; the four fights the book shares, by their own), what it says
    /// when that fight is first won, what it says when the fight is won only after a later stage's (the player came
    /// back for it), and how it points the player to that fight. Stage 5's fights have no late line: nothing comes
    /// after them.
    /// </summary>
    internal static partial class BossGuide
    {
        sealed class Entry
        {
            public ItemType Relic;
            public int Stage;
            public string WinZh, WinEn, LateZh, LateEn, HintZh, HintEn;
        }

        static Entry E(ItemType relic, int stage, string winZh, string winEn, string lateZh, string lateEn, string hintZh, string hintEn) =>
            new Entry { Relic = relic, Stage = stage, WinZh = winZh, WinEn = winEn, LateZh = lateZh, LateEn = lateEn, HintZh = hintZh, HintEn = hintEn };

        /// <summary>
        /// A fight's way in as this save's story stands. The Nightmare King's back room (Grimm_Main_Tent's "Backroom Mask", a
        /// wall whose Remove FSM takes itself away on killedGrimm with flamesCollected at flamesRequired) opens only once the
        /// Troupe Master is beaten in his tent and the Grimmchild has gathered the flames after it; a win in a rematch (the
        /// book's challenge, a seal) leaves the save's story where it was (user 10-25: told to go behind the curtain, walled in).
        /// </summary>
        static string HintZhOf(Entry e) => HintOf(e).Zh;
        static string HintEnOf(Entry e) => HintOf(e).En;
        static Page Hint(Entry e)
        {
            var h = HintOf(e);
            return Voice(h.Zh, h.En);
        }

        static (string Zh, string En) HintOf(Entry e)
        {
            var pd = PlayerData.instance;
            if (pd != null && e.Relic == ItemType.RelicNightmareGrimm)
            {
                if (!pd.GetBool("killedGrimm"))
                    return ("帐篷里的团长还在等一场真正的演出：在猩红帐幕里赢下他的舞，再让格林之子收齐新的火焰，帐幕后的里间才会打开；在沉睡的团长身上敲响梦之钉。",
                        "The master in the tent still waits for a real performance: win his dance in the scarlet tent, then let the Grimmchild gather new flames, and the room behind the curtain will open; strike the sleeping master with the dream nail.");
                if (pd.GetInt("flamesCollected") < pd.GetInt("flamesRequired"))
                    return ("让格林之子再收齐三团猩红火焰，帐幕后的里间才会打开；在沉睡的团长身上敲响梦之钉，真正支撑梦境的心跳就在那场梦里。",
                        "Let the Grimmchild gather three more scarlet flames and the room behind the curtain will open; strike the sleeping master with the dream nail, and the heartbeat that holds the dream up is in there.");
            }
            return (e.HintZh, e.HintEn);
        }

        /// <summary>The fights in the journey's order: by stage, and within a stage the way the roads lead.</summary>
        static readonly Entry[] Route =
        {
            // Stage 0: the Forgotten Crossroads.
            E(ItemType.RelicFalseKnight, 0,
                "甲壳碎了，里面的恐惧却比你想的更小。", "The shell is broken, and the fear inside was smaller than you thought.",
                "从更深处回来的人才看得清：那副重甲从来只是空心的虚张声势。", "Back from the depths, you see it plainly: that armour was only ever hollow bluster.",
                "十字交错的旧路中央，一副不合身的重甲正替它的主人守门。", "Where the old roads cross, an ill-fitting suit of armour guards the door for its master."),
            E(ItemType.RelicBigFly, 0,
                "母巢安静了。她腹中的孩子，再也不必飞出来送死。", "The nest is quiet. The young in her belly need never fly out to die.",
                "你本可以早些来。她睡了这么久，醒来面对的却是比她可怕得多的东西。", "You might have come sooner. She slept so long, and woke to something far worse than herself.",
                "旧路东侧有一间安静的巢室，一位母亲在沉睡里护着满腹的孩子。", "East along the old roads lies a silent chamber where a mother sleeps over a belly full of young."),
            E(ItemType.RelicMawlek, 0,
                "独处太久，连呼唤也会长出利齿。", "Alone too long, even a call for company grows teeth.",
                "它守了一辈子的孤独，等来的却是一个早已走远、又折回来的人。", "It guarded its solitude all its life, and the one who came had already gone far ahead and turned back.",
                "旧路尽头一处封死的洞穴里，有只独居太久的野兽，连同伴的呼唤都忘了。", "In a sealed cave at the end of the old roads lives a beast alone so long it has forgotten its kin's call."),

            // Stage 1: Greenpath and the cliffs.
            E(ItemType.RelicHornet, 1,
                "她认出了你身上不属于此地的气息，也认下了你手里的武器。", "She knew the scent of somewhere else on you, and she has acknowledged the weapon in your hand.",
                "她拿你当新来的客人试探，却不知道你早已走过比她守的路更深的地方。", "She tested you as a newcomer, not knowing you had already gone deeper than the road she guards.",
                "往旧路西边的绿意里去；有人用一根针，替整座废墟试探来客。", "Go west from the old roads into the green; someone with a needle tests every visitor on the ruins' behalf."),
            E(ItemType.RelicMegaMossCharger, 1,
                "叶片遮不住惊慌。藏得再深，它的冲撞也只是在原地打转。", "Leaves can't hide panic. However deep it hid, its charge only ran in circles.",
                "一团苔藓也敢挡路；你走过的地方，比它见过的整片林子都深。", "A heap of moss dared bar the way; you have walked deeper than any forest it has known.",
                "绿径的湖边，有一大团苔藓装作泥土；靠近时，它会整片冲撞过来。", "By the lake in the green path, a great mound of moss pretends to be earth; come close and it charges."),
            E(ItemType.RelicBigBuzzer, 1,
                "它占据高处，只因为下面还有更值得畏惧的东西。", "It held the heights only because something below was more worth fearing.",
                "它的嗡鸣曾经能吓退旅人；对一个从深处回来的人，那只是噪音。", "Its buzzing once turned travellers back; to one returned from the depths, it is only noise.",
                "绿径的高处，一只自封为王的飞虫正召集它的群落，封住去路。", "High in the green path, a self-crowned fly gathers its swarm to seal the way."),
            E(ItemType.RelicGhostNoEyes, 1,
                "歌停了，黑暗仍记得它的孩子。", "The song has stopped, yet the dark still remembers its children.",
                "她的哭声在黑暗里等了很久，等来的却是一个早已不怕黑的人。", "Her weeping waited long in the dark, and what came was someone long past fearing it.",
                "绿径深处有一座无灯的石之庇护所，黑暗里，一位母亲还在唱给她的孩子听。", "Deep in the green lies a lightless sanctuary of stone, where a mother still sings to her children in the dark."),
            E(ItemType.RelicGhostAladar, 1,
                "升得再高，也未必离真相更近。", "Rising higher does not always bring you nearer the truth.",
                "他一生都在向上苦修；你却是从更低、更深的地方回来，替他收了尾。", "He spent his life climbing upward; you came back from lower, deeper places to end it for him.",
                "小镇西边呼啸的悬崖上有一座孤坟，坟主至今仍以为自己正在升天。", "West of the town, on the howling cliffs, lies a lonely grave whose keeper still believes he is ascending."),

            // Stage 2: the fungal wastes, the city of tears, the peak, the waterways, the canyon and the gardens.
            E(ItemType.RelicMantisLord, 2,
                "她们放你通行，因为你用战斗说清了自己的名字。", "They let you pass because you spoke your name in battle.",
                "她们仍按旧礼向来客拔刀，却不知你早已穿过她们守着的那片深处。", "They still draw their blades at guests by the old rite, not knowing you have long since passed the depths they guard.",
                "孢子之下的菌林深处，三张王座仍未蒙尘；先让持刃者承认你能站着离开。", "Deep in the fungal woods beneath the spores stand three thrones untouched by dust; make the blade-bearers admit you can leave standing."),
            E(ItemType.RelicGhostHu, 2,
                "他把每个陌生人都看成了灾祸，如今终于肯闭眼。", "He took every stranger for a calamity; now at last he closes his eyes.",
                "他到最后仍把你当作灾祸；而你确实是从他想不到的深处回来的。", "To the end he took you for a calamity, and you truly have come back from depths he never imagined.",
                "真菌荒地的边缘，一位长者的魂魄悬在半空，把每个陌生人都当作灾祸。", "At the edge of the fungal wastes an elder's spirit hangs in the air, taking every stranger for a calamity."),
            E(ItemType.RelicMageKnight, 2,
                "守门的刃已经折了，圣所的门后还亮着灯。", "The blade at the gate is broken, and lamps still burn behind the sanctum door.",
                "这位守门人一直在等闯入者，等来的却是一个从更高处折返的人。", "This gatekeeper waited for intruders, and got one already coming back down from higher up.",
                "雨城的灵魂圣所里，有个披着长袍的战士，正替他的主人看守学堂的大门。", "In the rain city's soul sanctum, a robed warrior keeps the school's gate for his master."),
            E(ItemType.RelicMageLord, 2,
                "他想夺来的永生，终究没有替他多留一口气。", "The eternity he tried to seize did not buy him one more breath.",
                "他还在高塔里钻研不死，并不知道来客早已走过他想象不到的路。", "He was still studying deathlessness in his tower, never knowing his guest had walked roads beyond his imagining.",
                "东边那座永远下雨的城里，有人把灵魂装进高塔，妄想填平死亡。", "In the city east where the rain never stops, someone fills a tower with souls, hoping to fill in death itself."),
            E(ItemType.RelicMegaBeamMiner, 2,
                "晶光熄了一盏，整座山却还在低声歌唱。", "One crystal light has gone out, and the whole mountain still hums.",
                "他守着长椅睡了太久；叫醒他的挑战者，早已不是新手。", "He slept too long by his bench; the challenger who woke him is no novice any more.",
                "城北那片发亮的山腹里，有个守着长椅的矿工，已经忘了休息是什么。", "In the glowing heart of the mountain north of the city, a miner guarding a bench has forgotten what rest is."),
            E(ItemType.RelicDungDefender, 2,
                "有些誓言，即使埋在污泥里也不肯腐烂。", "Some oaths refuse to rot even when buried in the muck.",
                "他仍在污泥里守着旧日的荣耀，而你带来的，是他再也追不上的远方。", "He still guards old glory in the muck, and you bring a distance he can never catch up with.",
                "雨城下方，沿污水而行；昔日的荣耀在那里滚成了泥土，还笑着迎接来客。", "Beneath the rain city, follow the sewage; old glory has rolled itself into dung there, and still laughs to greet its guests."),
            E(ItemType.RelicFlukeMother, 2,
                "水道里少了一张永不满足的嘴。", "The waterways have one less mouth that is never full.",
                "她只知道吞咽与繁殖，从不在意来的是新手，还是从深处归来的人。", "She knows only swallowing and breeding, and never cares whether her visitor is new or back from the depths.",
                "水道最暗的角落里，有一张永远吃不饱的嘴，正把孩子一个个吐向来客。", "In the darkest corner of the waterways, a mouth that is never full spits its young at visitors one by one."),
            E(ItemType.RelicMegaJellyfish, 2,
                "雷光散了，沉睡者的门露出一角。", "The lightning has scattered, and the sleeper's door shows a corner.",
                "它在书页之间守了很久的雷，最后被一个早已越过它的人回头熄灭。", "It guarded the thunder between the pages for long, only to be put out by one who had already gone past it.",
                "盛雾的峡谷里有一座藏书的地方，一团借雷声守门的透明生命，正浮在酸水之上。", "In the foggy canyon stands a house of books, where a clear creature that borrows the thunder floats above the acid."),
            E(ItemType.RelicGhostMarmu, 2,
                "她一直等人教她飞，如今不必再等。", "She waited for someone to teach her to fly; now she need not wait.",
                "她在花园里等了太久，等到的人早已走过比花园更远的路。", "She waited too long in the garden, and the one who came had already travelled far beyond it.",
                "王后花园的长廊里，一位守园人的魂魄还在滚来滚去，等着有人陪她玩最后一次。", "In the queen's garden halls a keeper's spirit still rolls about, waiting for someone to play with her one last time."),
            E(ItemType.RelicTraitorLord, 2,
                "他用亲族换来的力量，没有替他挡住最后一击。", "The strength he bought with his kin did not stop the final blow.",
                "他以为腐败让他更强；可你来时，早已见过比他更深的黑暗。", "He thought corruption made him stronger; you had already seen darker things than him.",
                "王后荒废的花园尽头，一位背弃族群的持刃者，正把腐败误认作更强的力量。", "At the end of the queen's abandoned garden, a blade-bearer who betrayed his kind mistakes rot for greater strength."),
            E(ItemType.RelicFalseKnightAt5, 2,
                "梦里那副重甲比现实更沉，因为它还装着不肯承认的失败。", "The armour in the dream weighed more than the real one, for it still held a failure it would not admit.",
                "在梦里重披旧甲的冠军，迎来的对手早已不需要向任何荣耀证明什么。", "The champion donned the old armour again in a dream, and met an opponent with no glory left to prove.",
                "回到旧路中央那副空甲旁，敲响梦之钉；它的梦里，还有一位不肯认输的冠军。", "Return to the empty armour where the old roads cross and strike it with the dream nail; in its dream a champion still refuses defeat."),

            // Stage 3: the watchers' tower, the collector, the deep nest, the hive, the resting grounds, the dreams of the city.
            E(ItemType.RelicBlackKnight, 3,
                "他们守住的不是主人，是一个从未撤回的命令。", "What they guarded was not their master, but an order never withdrawn.",
                "他们还在执行那道旧命令，并不知道门外的人早已走到命令够不着的地方。", "They still carry out the old order, not knowing the one at the door has gone where no order can reach.",
                "雨城东侧的高塔还在守望；数清那些滚动的甲壳，别被第一声倒地骗住。", "The tower east of the rain city still keeps watch; count the rolling shells, and don't be fooled by the first to fall."),
            E(ItemType.RelicJarCollector, 3,
                "把活物装进玻璃，并不能替它们留下世界。", "Putting the living in glass will not keep the world for them.",
                "它收集了一塔的活物，却没料到最后上门的，是它装不下的东西。", "It collected a tower full of the living, never expecting the last caller to be something it could not hold.",
                "雨城东北的高塔顶上，有个不懂爱的收藏者，正把活物一只只装进玻璃罐。", "Atop the tower northeast of the rain city, a collector who knows no love seals the living in glass jars."),
            E(ItemType.RelicMageLordAt5, 3,
                "梦替他续了命，却没能替他学会停手。", "The dream lengthened his life but never taught him to stop.",
                "他在梦里也不肯停手；可惜醒着的你，早已走得比他的野心更远。", "Even in his dream he will not stop; awake, you have long since gone farther than his ambition ever reached.",
                "回到雨城的圣所，在那位法师留下的躯壳上敲响梦之钉；他的梦仍自称主人。", "Return to the rain city's sanctum and strike what the mage left behind with the dream nail; his dream still calls itself master."),
            E(ItemType.RelicEnragedGuardian, 3,
                "同一束晶光，因愤怒而烧得更亮，也灭得更快。", "The same crystal light burns brighter in anger, and dies faster.",
                "他把怒火烧到最旺，还是没赶上你如今的脚步。", "He burned his fury at its hottest and still could not keep up with your stride.",
                "再回发亮的山腹一次；那个守长椅的矿工醒着，晶光里只剩愤怒。", "Go back once more to the glowing heart of the mountain; the miner by the bench is awake, and only anger is left in his light."),
            E(ItemType.RelicMimicSpider, 3,
                "熟悉的脸，原来也能是一张诱人的网。", "A familiar face can be a tempting web.",
                "它想借你的模样骗你，可它借到的，只是你早已走过的那段路。", "It tried to fool you with your own likeness, but all it borrowed was a road you had already left behind.",
                "深巢的蛛网深处，有个东西会长出你最熟悉的脸，等你走近。", "Deep in the webs of the nest, something wears the face you know best and waits for you to come near."),
            E(ItemType.RelicGhostGalien, 3,
                "镰刀终于停下了，他却到最后也没问自己为何还在战斗。", "The scythe has finally stopped, yet he never asked himself why he still fought.",
                "他在深巢里挥镰守望，直到一个早已越过此处的人回来，替他停下。", "He swung his scythe in the nest until one who had long since passed came back to stop him.",
                "深巢的一处断崖下，有位扛镰的守卫魂魄，仍在为一个早已失效的使命挥刃。", "Below a cliff in the deep nest, the spirit of a scythe-bearing guard still swings for a mission long void."),
            E(ItemType.RelicHiveKnight, 3,
                "王后不在了，最后一针仍替她守着家。", "The queen is gone, and the last stinger still guards her home.",
                "他仍替不在的王后守着巢，却不知道外面的世界早已被你翻过一遍。", "He still guards the hive for an absent queen, not knowing you have already turned the world outside over.",
                "王国边缘的蜂巢深处，最后一名骑士仍替不在的王后守着家门。", "Deep in the hive at the kingdom's edge, the last knight still guards the door for a queen who is gone."),
            E(ItemType.RelicGhostXero, 3,
                "他的双刃终于不再追逐王的影子。", "His twin blades no longer chase the king's shadow.",
                "他的刃追着王的影子追了太久；而你，早已走到那道影子之外。", "His blades chased the king's shadow for too long; you have already walked out beyond that shadow.",
                "安息之地的高处，一棵枯树下埋着一位被逐的骑士，他的双刃仍在追逐王的影子。", "High in the resting grounds, beneath a dead tree, lies an exiled knight whose twin blades still chase the king's shadow."),

            // Stage 4: the basin, the kingdom's edge, the colosseum and the junk pit.
            E(ItemType.RelicInfectedKnight, 4,
                "借来的呼吸散了，空壳终于得以安静。", "The borrowed breath has gone; the empty shell can rest at last.",
                "它在盆地底下替别的东西呼吸了太久，而你走过的路，早已比它的牢笼更深。", "It breathed for something else beneath the basin for so long, and your road already runs deeper than its cage.",
                "往王国最深的盆地去；那具破损的身体里，有什么东西仍在替它呼吸。", "Go to the deepest basin of the kingdom; inside a broken body, something is still breathing for it."),
            E(ItemType.RelicHornetAt4, 4,
                "她终于肯让你走进更深的黑暗。", "At last she lets you walk into the deeper dark.",
                "她在边缘守着最后的试炼，却发现你早已从那片黑暗里走了出来。", "She guards the final test at the edge, only to find you have already come back out of that dark.",
                "去王国边缘的灰烬里追那抹红色；这一次，她守着的东西会认出你是否有资格继续向下。", "Chase the red into the ashes at the kingdom's edge; this time what she guards will judge whether you may go further down."),
            E(ItemType.RelicGhostMarkoth, 4,
                "盾能挡住刀锋，却挡不住一个人把自己困住。", "A shield can stop a blade, but not a man who traps himself.",
                "他在边缘把自己困了很久，而你早已走得比他的冥想更远。", "He trapped himself at the edge for long; you have already gone farther than his meditation ever took him.",
                "王国边缘最高的灰烬尖上，有位冥想者的魂魄，绕着盾与刃把自己困在原地。", "On the highest spire of ash at the kingdom's edge, a meditating spirit circles shield and blade, trapped in place."),
            E(ItemType.RelicInfectedKnightAt5, 4,
                "它伸出的手，并不是还想夺走什么。", "The hand it reached out was not trying to take anything any more.",
                "梦里的近亲还在等一位来客，等来的却是一个早已不会迷路的人。", "The kin in the dream still waits for a visitor, and gets one who no longer loses the way.",
                "回到盆地深处那具空壳旁，敲响梦之钉；一道没有散尽的思念，还想再见一次来客。", "Return to the empty shell deep in the basin and strike it with the dream nail; a longing not yet faded wants to meet a visitor once more."),
            E(ItemType.RelicZote, 4,
                "他还会把这次倒下编成胜利的故事。你不必替他纠正。", "He will turn this fall into a tale of victory too. You need not correct him.",
                "他会说自己输给了一个传说；这一次，他竟没有说错。", "He will say he lost to a legend; for once, he will not be wrong.",
                "斗兽场的第一场试炼里，那位满口箴言的小骑士正等着证明自己。", "In the colosseum's first trial, the little knight full of precepts waits to prove himself."),
            E(ItemType.RelicOblobble, 4,
                "喧闹中倒下的，总不止眼前的对手。", "In the din, more than the foe before you always falls.",
                "它们在看台的喝彩里膨胀，却没认出你早已不是来博喝彩的新手。", "They swell in the crowd's cheers, never seeing you are no novice come for applause.",
                "斗兽场的第二场试炼里，一对胖鼓鼓的飞虫正等着在喝彩中炸开。", "In the colosseum's second trial, a pair of bloated fliers wait to burst amid the cheering."),
            E(ItemType.RelicLobsterLancer, 4,
                "鞭子倒下，兽仍会咬；兽倒下，鞭子才肯沉默。", "When the whip falls, the beast still bites; when the beast falls, the whip falls silent.",
                "看客们等着一场好戏，等来的却是一个早已不需要喝彩的人。", "The crowd hoped for a good show, and got someone who no longer needs applause.",
                "去愚人斗兽场走完最后的试炼；那里有一对互相驱使的驯者与野兽。", "Finish the last trial of the fools' colosseum; a tamer and a beast who drive each other wait there."),
            E(ItemType.RelicPaleLurker, 4,
                "她握着钥匙，却早已忘了要打开哪扇门。", "She holds a key, but long ago forgot which door it opens.",
                "她在废料堆里藏了太久，以为没人找得到；你却是走遍深处之后才回来的。", "She hid in the junk so long she thought no one would find her; you came only after walking all the depths.",
                "水道尽头的废料堆里，有个苍白的潜伏者攥着一把钥匙，躲着所有来客。", "In the scrap heap at the waterways' end, a pale lurker clutches a key and hides from every visitor."),

            // Stage 5: the troupe, the dreams' last guardians, the black egg, the end, and the gods' home.
            E(ItemType.RelicGrimm, 5,
                "你跟上了舞步，却还没见过奏乐的心脏。", "You kept up with the dance, but you have yet to see the heart that plays the music.",
                null, null,
                "点亮悬崖上的梦魇灯笼，小镇里会支起猩红的帐幕；团长在台上等一位能跟上舞步的舞伴。", "Light the nightmare lantern on the cliffs and a scarlet tent will rise in the town; its master waits on stage for a partner who can keep up."),
            E(ItemType.RelicNightmareGrimm, 5,
                "心脏仍会在别处跳动，你却已经不必照着它的拍子呼吸。", "The heart will still beat somewhere else, but you need no longer breathe to its rhythm.",
                null, null,
                "去猩红帐幕后，在沉睡的团长身上敲响梦之钉；真正支撑梦境的心跳，就在那场梦里。", "Behind the scarlet curtain, strike the sleeping master with the dream nail; the heartbeat that holds the dream up is in there."),
            E(ItemType.RelicWhiteDefender, 5,
                "梦里的忠诚终于听见了回答。", "Loyalty in the dream has finally heard its answer.",
                null, null,
                "水道深处那位污泥骑士的坟前，敲响梦之钉；梦里有一身不曾染尘的白，仍在等王归来。", "At the dung knight's grave deep in the waterways, strike with the dream nail; in the dream a spotless white still waits for the king's return."),
            E(ItemType.RelicGreyPrince, 5,
                "被仰望的影子，往往比它的主人更难打倒。", "The shadow that is looked up to is often harder to beat than its owner.",
                null, null,
                "先救下左特与布蕾塔，并在勇士的试炼中击败左特；再进入布蕾塔家的地下室，用梦之钉攻击左特雕像。", "Rescue Zote and Bretta, defeat Zote in the Trial of the Warrior, then enter Bretta's basement and strike Zote's statue with the Dream Nail."),
            E(ItemType.RelicHollowKnight, 5,
                "你斩开了容器，却未必斩到了里面的光。", "You cut the vessel open, but not necessarily the light inside it.",
                null, null,
                "旧路尽头的黑卵还在等待；去看看，一个王国把希望锁成了什么。", "The black egg at the end of the old roads still waits; go and see what a kingdom locked its hope into."),
            E(ItemType.RelicFinalBoss, 5,
                "这一次，光也会闭上眼睛。你已经证明，门外之人能够改写门内的结局。", "This time even the light closes its eyes. You have shown that one from beyond the door can rewrite the ending within.",
                null, null,
                "若还想找到回去的门，就别把借来的锁链当作终点；循梦而入，直视那轮不肯落下的太阳。", "If you still want the door home, don't take the borrowed chains for the end; go into the dream and look straight at the sun that will not set."),
            E(ItemType.RelicNailBros, 5,
                "同出一门的刀锋，终于肯为彼此停下。", "Blades from the same school finally stop for each other.",
                null, null,
                "去众神沉睡的地方，先向持双钉的兄弟问路。", "Go where the gods sleep, and first ask the way of the brothers with two nails."),
            E(ItemType.RelicPaintmaster, 5,
                "颜色落定，画里只剩一条能走出的路。", "The colours have settled, and only one way out remains in the painting.",
                null, null,
                "神居的回声里，有一位师者用颜料记下他不愿说出口的战意。", "In the echoes of the gods' home, a master records in paint the fighting spirit he will not speak of."),
            E(ItemType.RelicNailsage, 5,
                "他把一生藏进了短短的锋刃，你已看见。", "He hid his whole life in a short blade, and you have seen it.",
                null, null,
                "去找那位身形最小的师父；别因他曾经卖过灯火，就忘了他怎样握剑。", "Find the smallest master of all; don't forget how he holds a sword just because he once sold lanterns."),
            E(ItemType.RelicHollowKnightPrime, 5,
                "完美也倒下了。门从来不只朝一个方向开。", "Even perfection has fallen. The door never opened in only one direction.",
                null, null,
                "神居更高处还留着一具未曾破碎的容器；去问问，没有裂缝的东西，是否真的装得下命运。", "Higher in the gods' home a vessel still stands unbroken; go and ask whether something without a crack can really hold a fate."),
            E(ItemType.RelicSistersOfBattle, 5,
                "三道刀锋不再轮流试探，她们把最后的敬意一并交给了你。", "Three blades no longer take turns testing you; they gave you their last respect together.",
                null, null,
                "神居里，那三位持刃者不再轮流出手；去接下她们一起拔出的刀。", "In the gods' home, the three blade-bearers no longer strike in turn; go and meet the blades they draw together."),
            E(ItemType.RelicWingedNosk, 5,
                "这次，它连她的影子也想偷走。不要追那张脸。", "This time it tried to steal even her shadow. Don't chase that face.",
                null, null,
                "神居的回声里，那个会偷脸的东西长出了翅膀，学着另一个人的模样。", "In the echoes of the gods' home, the face-stealer has grown wings and copies someone else."),
            E(ItemType.RelicAbsoluteRadiance, 5,
                "你已走过连神也不愿回望的高度。", "You have walked heights even the gods will not look back on.",
                null, null,
                "若最高处仍有光在叫你的名字，就带着自己的意志去；这一次，不必相信我。", "If light still calls your name from the very top, go with your own will; this time, you need not trust me."),
        };

        // Between the win and the way on: the fight's stage finished, or nothing left ahead but some behind.
        const string OnwardZh = "这一程的对手，都已倒在你身后。", OnwardEn = "Everyone on this stretch of the road lies behind you now.";
        const string BehindZh = "再往前已无人拦路，你身后却还留着没算完的账。", BehindEn = "No one bars the road ahead any more, but behind you some accounts are still open.";

        /// <summary>The first words, once the portal has closed behind the player; the voice's last line ends with the way to the first fight.</summary>
        static readonly (bool Voice, string Zh, string En)[] Opening =
        {
            (false, "门后应该是军团的营地……这是什么地方？", "Past the portal should have been the army's camp... Where is this?"),
            (false, "没有守卫，没有水晶。连天空都像被石头封住了。", "No guards, no crystal. Even the sky looks sealed with stone."),
            (true, "你的世界仍在门的另一边。只是，来路已经不肯认你。", "Your world is still on the other side of the door. Only, the road back no longer knows you."),
            (false, "是你把我带来的？", "Did you bring me here?"),
            (true, "先活下去。你会在这里找到炉火、矿石，还有足够多不愿让路的东西。", "Survive first. You will find forges here, and ore, and plenty of things that will not step aside."),
            (false, "我问的是，怎么回去。", "I asked how to get back."),
        };
        const string OpeningLastZh = "让它倒下，我便能把你听得更清楚一点。", OpeningLastEn = "Bring it down, and I will hear you a little more clearly.";

        /// <summary>The last words, after the last of the 48 fights is won.</summary>
        static readonly (bool Voice, string Zh, string En)[] Closing =
        {
            (false, "你说，打倒它们就能找到回去的路。", "You said beating them would show me the way back."),
            (true, "我说过，你会找到门。我从未替你选过门的另一边。", "I said you would find a door. I never chose for you what lies on its other side."),
            (false, "你一直在借我的手，打开这里。", "You've been using my hands to open this place."),
            (true, "也许。也许一扇关闭太久的门，只能等一个不属于这里的人来推。", "Perhaps. Perhaps a door shut for too long can only be pushed by someone who doesn't belong here."),
            (false, "那就由我决定，什么能跟着我回去。", "Then I'll decide what comes back with me."),
        };
    }
}
