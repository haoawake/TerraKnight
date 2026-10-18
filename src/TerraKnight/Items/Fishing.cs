using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using TerraKnight.UI;
using TerraKnight.World;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's fishing (1.4.5) in Hallownest's waters. The pole casts a bobber (shootSpeed toward the cursor, gravity 0.2,
    /// falling at most 15.9 px a tick); in water it floats on the surface, and every tick
    /// (Projectile.AI_061_FishingBobber) the bite timer goes on by the fishing power until 660, when a fishing check is
    /// made (Projectile.FishingCheck): the pond's size (fewer than 75 tiles: none; fewer than it needs: less power, and
    /// junk), the luck, the rarity rolls (common 1/max(2,150/FL) .. legendary 1/max(6,4500/FL)), the crate roll (10%, 25%
    /// with the Crate potion), the gate (Next(100) &lt;= (FL+75)/2), and Terraria's catch rules in their order (FishingData.cs)
    /// under the area's conditions: the zones it stands for, its height level, hardmode from the Monarch Wings on, today's
    /// quest fish. A catch bobs the bobber for (90..240 + FL) / 2.5 ticks; using the pole then reels it in (the line
    /// breaking 1 time in 7 without the High Test Fishing Line, the bait used up 1 time in 1 + bait/6, one more with the
    /// Tackle Box). An item above the stage reached isn't caught: the next rules decide.
    ///
    /// Hallownest's acid is Terraria's lava: fished only with the acid hook, the Acidline or acid bait; there it is the
    /// area's water rules with Terraria's lava catches first (the acid crate, the Obsidifish and the Flarefin Koi at half
    /// their sure chance, the Obsidian Swordfish).
    /// </summary>
    internal static partial class Fishing
    {
        // The rules' condition bits (gen_fishing.py CONDS).
        const ulong HardMode = 1UL << 0, EarlyMode = 1UL << 1, InLava = 1UL << 2, InHoney = 1UL << 3, Junk = 1UL << 4, CrateRoll = 1UL << 5,
            CanFishInLava = 1UL << 7, Dungeon = 1UL << 8, Beach = 1UL << 9, Hallow = 1UL << 10, Glowshroom = 1UL << 11, TrueDesert = 1UL << 12, TrueSnow = 1UL << 13,
            Height0 = 1UL << 15, Height1 = 1UL << 16, Height1And2 = 1UL << 17, HeightAbove1 = 1UL << 18, HeightAboveAnd1 = 1UL << 19, HeightUnder2 = 1UL << 20,
            HeightAbove2 = 1UL << 21, Height3 = 1UL << 22, Corruption = 1UL << 23, Crimson = 1UL << 24, Jungle = 1UL << 25, Snow = 1UL << 26, Desert = 1UL << 27,
            RolledHallowDesert = 1UL << 28, Ocean = 1UL << 29, DidNotUseCombatBook = 1UL << 32, Water1000 = 1UL << 33;

        const float Px = TerraParticles.Px;

        /// <summary>A body of water or acid: its surface, how far it spreads, and how many tiles it holds.</summary>
        sealed class Pond
        {
            public Bounds Area;
            /// <summary>The swim region's object: acid while the Acid Armour Check stands on it (asked each time).</summary>
            public GameObject Region;
            public bool SpaAcid;
            public int Tiles;
            public string Scene;
            public bool Acid => Region != null ? HasFsm(Region, "Acid Armour Check") : SpaAcid;
        }

        public static bool Active => bobber != null;
        /// <summary>A fish is on: the bobber bobs, and using the pole reels it in.</summary>
        public static bool Biting { get; private set; }
        public static int Catches { get; private set; }
        /// <summary>The fishing power of the last cast (as Terraria's GetFishingConditions).</summary>
        public static int Power { get; private set; }
        public static string DevLast { get; private set; } = "";
        /// <summary>Dev: a pond's size for every check (a test's water fixture), 0 for the real one.</summary>
        public static int DevPondTiles;

        static GameObject bobber;
        static LineRenderer line;
        static SpriteRenderer sprite;
        static ItemType pole;
        static Vector2 pos, vel;
        static Pond pond;
        static bool floating;
        static float tickAcc, timer;
        static int hookTicks;
        static ItemType catchType;
        static int catchStack;

        // ------------------------------------------------------------------ power

        /// <summary>
        /// Player.GetFishingConditions: (bait + pole + fishing skill) × the weather, time and moon (×1.2 rain, ×1.1 clouds,
        /// ×1.3 at dawn and dusk, ×0.8 at midday and midnight, the moon ×1.1 full to ×0.9 new). Fishing skill: the gear's
        /// (the earring, the tackle bags), a bobber's 10, the Fishing potion's 15, 5 a piece of the Angler set, the Gummy Worm's 3.
        /// </summary>
        public static int FishingLevel(ItemDef rod, Inventory inv)
        {
            int bait = FindBait(inv);
            if (rod == null || rod.FishingPower <= 0 || bait < 0) return 0;
            int skill = Equipment.Current.FishingSkill + (Equipment.Current.BobberKind > 0 ? 10 : 0) + (PlayerBuffs.Has(PotionBuffs.Fishing) ? 15 : 0);
            foreach (var armor in inv.EffectiveArmor)
                if (!armor.IsEmpty && armor.Def.Set == ArmorSet.Angler) skill += 5;
            if (TerraMod.Instance != null && TerraMod.Instance.Stats.Boosts.Contains(PermanentBoost.GummyWorm)) skill += 3;
            float level = rod.FishingPower + inv.Slots[bait].Def.BaitPower + skill;
            var area = AreaHere();
            if ((area.Zones & Zone.Rain) != 0) level *= 1.2f * 1.1f;
            float now = TerraTime.Now;
            bool day = TerraTime.IsDay;
            float t = day ? now * 60f : (now - TerraTime.DaySeconds) * 60f;
            if (day && (t < 5400f || t > 48600f)) level *= 1.3f;
            else if (day && t > 16200f && t < 37800f) level *= 0.8f;
            else if (!day && t > 6480f && t < 25920f) level *= 0.8f;
            switch (Town.TownNpcs.MoonPhase)
            {
                case 0: level *= 1.1f; break;
                case 1: case 7: level *= 1.05f; break;
                case 3: case 5: level *= 0.95f; break;
                case 4: level *= 0.9f; break;
            }
            return (int)level;
        }

        /// <summary>The first bait: the ammo slots first, then the bag.</summary>
        static int FindBait(Inventory inv)
        {
            for (int i = Inventory.Size; i < Inventory.AmmoEnd; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Def.BaitPower > 0) return i;
            for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Def.BaitPower > 0) return i;
            return -1;
        }

        static bool CanFishInAcid(ItemDef rod, Inventory inv)
        {
            int bait = FindBait(inv);
            return Equipment.Current.AcidFishing || (rod != null && rod.AcidPole) || (bait >= 0 && inv.Slots[bait].Def.AcidBait);
        }

        /// <summary>The area the player fishes in, by the scene they stand in.</summary>
        static (int Stage, int Height, Zone Zones) AreaHere()
        {
            string scene = TerraWorld.Live?.SceneName ?? GameManager.UnsafeInstance?.sceneName;
            return areas.TryGetValue(Areas.AreaName(scene ?? ""), out var a) ? a : (0, 1, Zone.None);
        }

        /// <summary>The Fisherman's Pocket Guide's line (Player.displayedFishingInfo): the pole in hand's power, or the pond's lack.</summary>
        public static string InfoLine()
        {
            var mod = TerraMod.Instance;
            if (mod == null) return null;
            var held = mod.Inventory.Current;
            if (held.IsEmpty || held.Def.FishingPower <= 0) return null;
            int fl = FishingLevel(held.Def, mod.Inventory);
            if (Active && floating && pond != null)
            {
                int tiles = DevPondTiles > 0 ? DevPondTiles : pond.Tiles;
                int need = Needed();
                if (tiles < need) return Lang.F("{0}（{1}%）渔力", (int)(fl * (float)tiles / need), Mathf.RoundToInt(100f * tiles / need));
            }
            return Lang.F("{0} 渔力", fl);
        }

        // ------------------------------------------------------------------ the pole

        /// <summary>Using a pole: cast a bobber toward the cursor, or with one out, reel it in.</summary>
        public static bool Use(HeroController hero, Vector2 aim, ItemDef rod)
        {
            if (hero == null || rod == null || TerraMod.Instance == null) return false;
            var inv = TerraMod.Instance.Inventory;
            if (Active) return Reel(hero, inv);
            if (FindBait(inv) < 0)
            {
                CombatText.Show(hero.transform.position + Vector3.up, Lang.T("没有鱼饵"), new Color(1f, 0.6f, 0.4f));
                return false;
            }
            // Cast at acid without the acid gear: it can't be fished (the bobber would only melt).
            var aimed = PondAt(aim, 2.5f);
            if (aimed != null && aimed.Acid && !CanFishInAcid(rod, inv))
            {
                CombatText.Show(hero.transform.position + Vector3.up, Lang.T("需要酸水钩才能在酸水中钓鱼"), new Color(0.6f, 1f, 0.4f));
                return false;
            }
            pole = rod.Type;
            Power = FishingLevel(rod, inv);
            Vector2 from = hero.GetComponent<BoxCollider2D>().bounds.center;
            Vector2 dir = aim - from;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.right * (hero.cState.facingRight ? 1f : -1f);
            pos = from;
            vel = dir.normalized * rod.ShootSpeed;   // px a tick
            // A cast right onto the water (a test's, or the cursor on it within reach) lands there.
            if (aimed != null && Vector2.Distance(from, aim) < 22f) { pos = new Vector2(aim.x, aimed.Area.max.y); vel = Vector2.zero; Settle(aimed); }
            else { floating = false; pond = null; }
            Biting = false;
            timer = 0f;
            tickAcc = 0f;
            MakeBobber(rod);
            DevLast = $"抛竿 渔力 {Power}";
            return true;
        }

        static bool Reel(HeroController hero, Inventory inv)
        {
            if (Biting && catchType != ItemType.None)
            {
                int baitSlot = FindBait(inv);
                if (baitSlot >= 0)
                {
                    var bait = inv.Slots[baitSlot].Def;
                    var caught = ItemDb.Get(catchType);
                    // Player.ItemCheck_CheckFishingBobber_ConsumeBait: 1 in 1 + bait/6 (one more with the Tackle Box); never
                    // for junk or a quest fish (rare below 0); the Gold Worm 1 in 20.
                    if (!inv.Infinite && caught.Rare >= 0)
                    {
                        float d = Mathf.Max(1f, 1f + bait.BaitPower / 6f) + (Equipment.Current.TackleBox ? 1f : 0f);
                        bool use = bait.Type == ItemType.GoldWorm ? Random.Range(0, 20) == 0 : Random.value * d < 1f;
                        if (use) inv.ConsumeAt(baitSlot);
                    }
                    // Player.PullBobber: the line breaks 1 in 7 without the High Test Fishing Line.
                    if (!Equipment.Current.FishingLine && Random.Range(0, 7) == 0)
                    {
                        CombatText.Show(hero.transform.position + Vector3.up, Lang.T("鱼线断了！"), new Color(1f, 0.4f, 0.4f));
                        DevLast = "断线";
                    }
                    else Give(hero, inv, catchType, catchStack);
                }
            }
            Cancel();
            TerraMod.Instance?.PlaySound("Item_1");
            return true;
        }

        static void Give(HeroController hero, Inventory inv, ItemType type, int count)
        {
            int left = inv.Pickup(type, count);
            if (left < count) PopupText.Show(ItemDb.Get(type), count - left, hero.transform.position);
            if (left > 0) WorldItems.Drop(ItemStack.Of(type, left), hero.transform.position);
            Catches++;
            TerraMod.Instance?.PlaySound("Grab");
            DevLast = $"钓到 {ItemDb.Get(type).Name}×{count}";
            Plugin.Log.LogInfo($"[钓鱼] 钓到 {ItemDb.Get(type).Name}×{count}（渔力 {Power}，{(pond != null ? pond.Scene + (pond.Acid ? " 酸水" : " 水") : "?")}）");
        }

        static void MakeBobber(ItemDef rod)
        {
            Cancel();
            bobber = new GameObject("Terra Fishing Bobber");
            Object.DontDestroyOnLoad(bobber);
            sprite = bobber.AddComponent<SpriteRenderer>();
            int kind = Equipment.Current.BobberKind;
            // Terraria's bobbers (360-366, 381, 382, 760, 775); the Fishing Bobber accessories' own (986-993).
            string tex = "Projectile_" + (kind > 0 ? 985 + kind : rod.Bobber > 0 ? rod.Bobber : 360);
            sprite.sprite = Terraria.TerrariaAssets.Sprite(tex, 1f / Px);
            sprite.sortingOrder = FxBatch.OnTopOrder;
            line = bobber.AddComponent<LineRenderer>();
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            line.positionCount = 2;
            line.startWidth = line.endWidth = 2f * Px;
            line.startColor = line.endColor = new Color(200 / 255f, 200 / 255f, 200 / 255f, 0.8f);
            line.sortingOrder = FxBatch.OnTopOrder - 1;
            bobber.transform.position = new Vector3(pos.x, pos.y, -0.02f);
        }

        static void Settle(Pond p)
        {
            pond = p;
            floating = true;
            vel = Vector2.zero;
            pos.y = p.Area.max.y;
        }

        // ------------------------------------------------------------------ every frame

        public static void Tick(HeroController hero)
        {
            if (!Active) return;
            var mod = TerraMod.Instance;
            // AI_061: gone when the pole leaves the hand, the player dies, or the bobber is far off (900 px).
            if (hero == null || mod == null || mod.Inventory.Current.Type != pole || mod.Stats.Life <= 0
                || Vector2.Distance(hero.transform.position, pos) > 900f * Px * 1.6f) { Cancel(); return; }
            tickAcc += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            while (tickAcc >= 1f && Active)
            {
                tickAcc -= 1f;
                Step(hero, mod.Inventory);
            }
            if (!Active) return;
            float bob = Biting ? Mathf.Sin(Time.time * 30f) * 4f * Px : floating ? Mathf.Sin(Time.time * 3f) * Px : 0f;
            bobber.transform.position = new Vector3(pos.x, pos.y + bob, -0.02f);
            // Main.DrawProj_FishingLine: from the rod's tip, ahead of the player and up.
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            float dirX = hero.cState.facingRight ? 1f : -1f;
            Vector2 tip = new Vector2(b.center.x + dirX * 43f * Px, b.center.y + 30f * Px);
            line.SetPosition(0, tip);
            line.SetPosition(1, (Vector2)bobber.transform.position);
            int kind = Equipment.Current.BobberKind;
            if (kind > 1)
            {
                Color glow = kind == 8 ? Color.HSVToRGB(Time.time % 1f, .6f, 1f) : kind == 3 ? new Color(0.6f, 1f, 0.3f) : kind == 4 ? Color.green
                    : kind == 5 ? Color.cyan : kind == 6 ? Color.magenta : kind == 7 ? new Color(.6f, .2f, 1f) : Color.white;
                FxBatch.Instance?.Light(TerraParticles.ToPx(pos), glow * .65f);
            }
        }

        static void Step(HeroController hero, Inventory inv)
        {
            if (!floating)
            {
                // In the air: gravity 0.2, at most 15.9 a tick; into a pond it floats; onto the ground it lies there.
                vel.y = Mathf.Max(vel.y - 0.2f, -15.9f);
                Vector2 next = pos + vel * Px;
                var hit = Physics2D.Linecast(pos, next, 1 << ChunkColliders.TerrainLayer | 1 << 8);
                var p = PondAt(next, 0f);
                if (p != null && next.y <= p.Area.max.y)
                {
                    if (p.Acid && !CanFishInAcid(ItemDb.Get(pole), inv))
                    {
                        // The bobber melts in acid without the gear.
                        CombatText.Show(new Vector3(next.x, p.Area.max.y + 0.5f), Lang.T("需要酸水钩才能在酸水中钓鱼"), new Color(0.6f, 1f, 0.4f));
                        TerraMod.Instance?.PlaySound("Item_1");
                        Cancel();
                        return;
                    }
                    pos = new Vector2(next.x, p.Area.max.y);
                    Settle(p);
                    TerraMod.Instance?.PlaySound("Splash_0", 0.5f);
                    return;
                }
                if (hit.collider != null)
                {
                    pos = hit.point + hit.normal * 4f * Px;
                    vel = Vector2.zero;
                    return;
                }
                pos = next;
                return;
            }
            if (Biting)
            {
                // The fish stays on (90..240 + power) / 2.5 ticks, then gets away; the timer starts over.
                if ((hookTicks += Random.Range(1, 5)) >= 0)
                {
                    Biting = false;
                    catchType = ItemType.None;
                    timer = 0f;
                }
                return;
            }
            int fl = FishingLevel(ItemDb.Get(pole), inv);
            if (fl <= 0) return;
            // AI_061_FishingBobber's bite timer.
            if (Random.Range(0, 300) < fl) timer += Random.Range(1, 3);
            timer += fl / 30;
            timer += Random.Range(1, 3);
            if (Random.Range(0, 60) == 0) timer += 60;
            if (timer <= 660f) return;
            timer = 0f;
            Check(hero, inv, fl);
        }

        // ------------------------------------------------------------------ the fishing check

        static void Check(HeroController hero, Inventory inv, int fl)
        {
            var rod = ItemDb.Get(pole);
            int tiles = DevPondTiles > 0 ? DevPondTiles : pond.Tiles;
            var area = areas.TryGetValue(Areas.AreaName(pond.Scene), out var a) ? a : (Stage: 0, Height: 1, Zones: Zone.None);
            int need = Needed(area.Height);
            if (tiles < need) fl = (int)(fl * (float)tiles / need);
            // Luck (Player.luck): the fishing power a little up or down, as often as the luck is strong.
            float luck = Mathf.Clamp(Equipment.Current.Luck, -0.7f, 1f);
            if (luck < 0f && Random.value < -luck) fl = (int)(fl * (0.9f - 0.3f * Random.value));
            else if (luck > 0f && Random.value < luck) fl = (int)(fl * (1.1f + 0.3f * Random.value));
            fl = Mathf.Max(1, fl);
            bool junk = Random.Range(0, 50) > fl && Random.Range(0, 50) > fl && tiles < need;
            // FishingCheck_RollDropLevels.
            bool common = Random.Range(0, Mathf.Max(2, 150 / fl)) == 0;
            bool uncommon = Random.Range(0, Mathf.Max(3, 300 / fl)) == 0;
            bool rare = Random.Range(0, Mathf.Max(4, 1050 / fl)) == 0;
            bool veryRare = Random.Range(0, Mathf.Max(5, 2250 / fl)) == 0;
            bool legendary = Random.Range(0, Mathf.Max(6, 4500 / fl)) == 0;
            bool crate = Random.Range(0, 100) < (PlayerBuffs.Has(PotionBuffs.Crate) ? 25 : 10);
            // The gate: Next(100) <= (FL + 75) / 2.
            if (Random.Range(0, 100) > (fl + 75) / 2) return;
            ulong flags = Flags(area, tiles, rod, inv, junk, crate);
            ItemType quest = Town.Angler.QuestFishToCatch(inv);
            var got = Roll(flags, quest, common, uncommon, rare, veryRare, legendary, Areas.AreaName(pond.Scene));
            if (got == ItemType.None) return;
            // Now and then the Fishing Chest on the line instead (never in place of the Angler's fish).
            if (got != quest && FishingChest.Caught(PlayerBuffs.Has(PotionBuffs.Crate))) got = FishingChest.Type;
            catchType = got;
            catchStack = Stack(got, FishingLevel(rod, inv));
            Biting = true;
            hookTicks = -Random.Range(90, 240) - fl;
            TerraMod.Instance?.PlaySound("Item_2", 0.6f);
            // The Sonar potion: what is on the line, over the bobber.
            if (PlayerBuffs.Has(PotionBuffs.Sonar))
                CombatText.Show(new Vector3(pos.x, pos.y + 0.8f), ItemDb.Get(got).Name, ItemDb.RarityColor(ItemDb.Get(got).Rare));
            DevLast = $"咬钩 {ItemDb.Get(got).Name}（渔力 {fl}，{tiles} 格{(pond.Acid ? " 酸水" : "")}）";
        }

        /// <summary>
        /// The water a pond needs not to weaken the line (Terraria's waterNeededToFish, 300): any here (the user's, 2026-10-16:
        /// every water fishes whatever its size; Hallownest's pools are small next to Terraria's lakes).
        /// </summary>
        static int Needed(int height = -1) => 1;

        static ulong Flags((int Stage, int Height, Zone Zones) area, int tiles, ItemDef rod, Inventory inv, bool junk, bool crate)
        {
            var z = area.Zones;
            ulong f = DidNotUseCombatBook;
            f |= Shop.UnlockedStage() >= 4 ? HardMode : EarlyMode;
            if (pond.Acid) f |= InLava;
            if (CanFishInAcid(rod, inv)) f |= CanFishInLava;
            if (junk) f |= Junk;
            if (crate) f |= CrateRoll;
            bool hallow = (z & Zone.Hallow) != 0 || ((z & Zone.HalfHallow) != 0 && Random.Range(0, 2) == 0);
            bool jungle = (z & Zone.Jungle) != 0, snow = (z & Zone.Snow) != 0, dungeon = (z & Zone.Dungeon) != 0;
            // FishingCheck_ProbeForQuestFish's biome rolls: snow and jungle at once keep the jungle half the time.
            if (snow && jungle && Random.Range(0, 2) == 0) snow = false;
            bool desert = (z & Zone.Desert) != 0 && !dungeon;
            if (dungeon) f |= Dungeon;
            if ((z & Zone.Beach) != 0) f |= Beach;
            if (hallow) f |= Hallow;
            if ((z & Zone.Glowshroom) != 0) f |= Glowshroom;
            if ((z & Zone.Desert) != 0) f |= TrueDesert;
            if ((z & Zone.Snow) != 0) f |= TrueSnow;
            if ((z & Zone.Corrupt) != 0) f |= Corruption;
            if ((z & Zone.Crimson) != 0) f |= Crimson;
            if (jungle) f |= Jungle;
            if (snow) f |= Snow;
            if (desert) f |= Desert;
            if (desert && hallow && Random.Range(0, 2) == 0) f |= RolledHallowDesert;
            int h = area.Height;
            f |= h == 0 ? Height0 : h == 1 ? Height1 : h == 3 ? Height3 : 0;
            if (h == 1 || h == 2) f |= Height1And2;
            if (h > 1) f |= HeightAbove1;
            if (h >= 1) f |= HeightAboveAnd1;
            if (h < 2) f |= HeightUnder2;
            if (h > 2) f |= HeightAbove2;
            if ((z & Zone.Ocean) != 0 && h <= 1 && tiles > 1000) f |= Ocean;
            if (tiles > 1000) f |= Water1000;
            return f;
        }

        /// <summary>
        /// The rules in order (FishDropRuleList.TryGetItemDropType): conditions, the rule's own chance, the rarity roll. Where
        /// Terraria's biome crates stand, the area's own crate (its coffer in hardmode): the crate roll on a rare catch.
        /// </summary>
        static ItemType Roll(ulong flags, ItemType quest, bool common, bool uncommon, bool rare, bool veryRare, bool legendary, string area)
        {
            bool acid = (flags & InLava) != 0 && (flags & CanFishInLava) != 0;
            int stage = Shop.UnlockedStage();
            bool areaCrateAsked = false;
            foreach (var r in rules)
            {
                ulong f = flags;
                // Acid with the gear: Terraria's lava catches first (the sure ones at half), then the area's water.
                if (acid)
                {
                    if (r.Lava && r.Stopper) continue;
                    if (!r.Lava) f &= ~InLava;
                }
                if (r.Items.Length == 1 && biomeCrates.Contains(r.Items[0]))
                {
                    if (areaCrateAsked) continue;
                    areaCrateAsked = true;
                    if ((f & CrateRoll) == 0 || (f & InLava) != 0 || !rare) continue;
                    var own = AreaCrateFor(area, (f & HardMode) != 0);
                    if (own != ItemType.None) return own;
                    continue;
                }
                if ((r.Mask & ~f) != 0) continue;
                if (r.Quest != ItemType.None && r.Quest != quest) continue;
                int num = r.Num, den = r.Den;
                if (acid && r.Lava && num >= den) { num = 1; den = 2; }
                if (num < den && Random.Range(0, den) >= num) continue;
                bool hit = r.Rarity switch
                {
                    0 => true, 1 => common, 2 => uncommon, 3 => rare, 4 => veryRare, 5 => legendary,
                    6 => uncommon && !veryRare && !legendary,
                    _ => uncommon || common,
                };
                if (!hit) continue;
                if (r.Stopper) return ItemType.None;
                // What the stage reached allows (a quest fish and junk always).
                var pick = new List<ItemType>();
                foreach (var t in r.Items)
                {
                    var d = ItemDb.Get(t);
                    if (d.QuestFish || d.Rare < 0 || d.Stage <= stage) pick.Add(t);
                }
                if (pick.Count == 0) continue;
                return pick[Random.Range(0, pick.Count)];
            }
            return ItemType.None;
        }

        /// <summary>The Bomb Fish's and the Frost Daggerfish's stacks (AI_061_FishingBobber_GiveItemToPlayer).</summary>
        static int Stack(ItemType t, int fl)
        {
            if (t == ItemType.BombFish)
            {
                int min = (fl / 20 + 3) / 2, max = (fl / 10 + 6) / 2;
                foreach (int n in new[] { 50, 100, 150, 200 }) if (Random.Range(0, n) < fl) max++;
                return Random.Range(min, max + 1);
            }
            if (t == ItemType.FrostDaggerfish)
            {
                int min = (fl / 4 + 15) / 2, max = (fl / 2 + 40) / 2;
                foreach (int n in new[] { 50, 100, 150, 200 }) if (Random.Range(0, n) < fl) max += 6;
                return Random.Range(min, max + 1);
            }
            return 1;
        }

        // ------------------------------------------------------------------ ponds

        static readonly List<Pond> ponds = new List<Pond>();
        static int pondSignature;

        /// <summary>The pond whose surface is at a point (within a little above it), of the rooms loaded.</summary>
        static Pond PondAt(Vector2 p, float above)
        {
            Scan();
            Pond best = null;
            float bestD = float.MaxValue;
            foreach (var q in ponds)
            {
                var b = q.Area;
                if (p.x < b.min.x || p.x > b.max.x || p.y < b.min.y || p.y > b.max.y + above) continue;
                float d = Mathf.Abs(p.y - b.max.y);
                if (d < bestD) { bestD = d; best = q; }
            }
            return best;
        }

        /// <summary>
        /// The rooms' water: each Surface Water Region (acid where the Acid Armour Check stands on it, its Acid Box beneath
        /// counted in), the hot springs' Spa Region; how many tiles each holds (FishingCheck's pond: its width, and down to
        /// the floor under it at each column).
        /// </summary>
        internal static bool NearCraftingLiquid(bool acid)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            Scan();
            var position = (Vector2)hero.transform.position;
            foreach (var p in ponds)
            {
                if (p.Acid != acid || (p.Region != null && !p.Region.activeInHierarchy)) continue;
                var bounds = p.Area;
                bounds.Expand(new Vector3(10 * CellGrid.CellSize, 8 * CellGrid.CellSize, 0));
                if (position.x >= bounds.min.x && position.x <= bounds.max.x
                    && position.y >= bounds.min.y && position.y <= bounds.max.y) return true;
            }
            return false;
        }

        static void Scan()
        {
            int sig = 17;
            for (int i = 0; i < USM.sceneCount; i++) sig = sig * 31 + USM.GetSceneAt(i).handle;
            if (sig == pondSignature) return;
            pondSignature = sig;
            ponds.Clear();
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fsm == null || fsm.FsmName != "Surface Water Region" || !fsm.gameObject.scene.isLoaded) continue;
                var box = fsm.GetComponent<BoxCollider2D>();
                if (box == null) continue;
                bool acid = HasFsm(fsm.gameObject, "Acid Armour Check");
                var b = BoxBounds(box);
                if (acid)
                {
                    // The Acid Box under the swim region: the acid's depth.
                    var parent = fsm.transform.parent;
                    if (parent != null)
                        foreach (var dh in parent.GetComponentsInChildren<DamageHero>(true))
                        {
                            var c = dh.GetComponent<BoxCollider2D>();
                            if (c != null && HasFsm(dh.gameObject, "Acid Armour Check")) b.Encapsulate(BoxBounds(c));
                        }
                }
                ponds.Add(new Pond { Area = b, Region = fsm.gameObject, Tiles = Count(b, acid), Scene = fsm.gameObject.scene.name });
            }
            // Acid that is only a hazard (no swim region over it): acid water too.
            foreach (var dh in Object.FindObjectsByType<DamageHero>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (dh == null || dh.hazardType != 3 || !dh.gameObject.scene.isLoaded) continue;
                var c = dh.GetComponent<BoxCollider2D>();
                if (c == null || !c.enabled) continue;
                var b = BoxBounds(c);
                bool covered = false;
                foreach (var q in ponds) if (q.Area.Intersects(b)) { covered = true; break; }
                if (covered || b.size.x < CellGrid.CellSize) continue;
                ponds.Add(new Pond { Area = b, Tiles = Count(b, true), Scene = dh.gameObject.scene.name, SpaAcid = true });
            }
            foreach (var col in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (col == null || col.gameObject.name != "Spa Region") continue;
                var b = col is BoxCollider2D bc ? BoxBounds(bc) : col.bounds;
                ponds.Add(new Pond { Area = b, Tiles = Count(b, false), Scene = col.gameObject.scene.name });
            }
        }

        /// <summary>A pond's tiles: its width in cells, each column down from the surface to the floor (at most 40), or its box.</summary>
        static int Count(Bounds b, bool acid)
        {
            const float cell = CellGrid.CellSize;
            int cols = Mathf.Max(1, Mathf.RoundToInt(b.size.x / cell));
            int boxRows = Mathf.Max(1, Mathf.RoundToInt(b.size.y / cell));
            if (acid) return cols * boxRows;
            int total = 0;
            for (int i = 0; i < cols; i++)
            {
                float x = b.min.x + (i + 0.5f) * cell;
                var hit = Physics2D.Raycast(new Vector2(x, b.max.y - 0.05f), Vector2.down, 40f * cell, 1 << 8 | 1 << ChunkColliders.TerrainLayer);
                total += hit.collider != null ? Mathf.Max(boxRows, Mathf.RoundToInt((b.max.y - hit.point.y) / cell)) : boxRows;
            }
            return total;
        }

        /// <summary>An FSM of that name on the object, switched on or not (PlayMakerFSM.FindFsmOnGameObject sees only the enabled).</summary>
        static bool HasFsm(GameObject go, string name)
        {
            foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f.FsmName == name) return true;
            return false;
        }

        static Bounds BoxBounds(BoxCollider2D box)
        {
            var t = box.transform;
            Vector2 c = t.TransformPoint(box.offset), s = Vector2.Scale(box.size, t.lossyScale);
            return new Bounds(c, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 0f));
        }

        /// <summary>The water surface at a point (a test's way to aim): where, and whether it is acid.</summary>
        internal static bool TrySurface(Vector2 aim, out Vector2 at, out bool acidic)
        {
            var p = PondAt(aim, 2.5f);
            at = p != null ? new Vector2(aim.x, p.Area.max.y) : default;
            acidic = p != null && p.Acid;
            return p != null;
        }

        public static void Cancel()
        {
            if (line != null && line.sharedMaterial != null) Object.Destroy(line.sharedMaterial);
            if (bobber != null) Object.Destroy(bobber);
            bobber = null; sprite = null; line = null; Biting = false; floating = false; pond = null; catchType = ItemType.None;
        }

        /// <summary>Dev: a catch on the line now.</summary>
        internal static void DevBite(ItemType item)
        {
            catchType = item;
            catchStack = 1;
            Biting = true;
            hookTicks = -600;
        }

        /// <summary>Dev: rolls a fishing check's catch under an area's conditions many times (no bobber): what came up and how often.</summary>
        internal static Dictionary<ItemType, int> DevSimulate(string area, bool acid, int fl, int tiles, int times, bool crateBuff = false)
        {
            var counts = new Dictionary<ItemType, int>();
            var inv = TerraMod.Instance.Inventory;
            (int Stage, int Height, Zone Zones) a = areas.TryGetValue(area, out var v) ? v : (0, 1, Zone.None);
            var saved = pond;
            pond = new Pond { SpaAcid = acid, Tiles = tiles, Scene = area };
            for (int i = 0; i < times; i++)
            {
                int need = Needed(a.Height);
                int f = tiles < need ? (int)(fl * (float)tiles / need) : fl;
                f = Mathf.Max(1, f);
                bool junk = Random.Range(0, 50) > f && Random.Range(0, 50) > f && tiles < need;
                bool common = Random.Range(0, Mathf.Max(2, 150 / f)) == 0, uncommon = Random.Range(0, Mathf.Max(3, 300 / f)) == 0;
                bool rare = Random.Range(0, Mathf.Max(4, 1050 / f)) == 0, veryRare = Random.Range(0, Mathf.Max(5, 2250 / f)) == 0;
                bool legendary = Random.Range(0, Mathf.Max(6, 4500 / f)) == 0;
                bool crate = Random.Range(0, 100) < (crateBuff ? 25 : 10);
                if (Random.Range(0, 100) > (f + 75) / 2) { Add(ItemType.None); continue; }
                ulong flags = Flags(a, tiles, ItemDb.Get(ItemType.GoldenFishingRod), inv, junk, crate);
                if (acid) flags |= InLava | CanFishInLava;
                Add(Roll(flags, Town.Angler.QuestFishToCatch(inv), common, uncommon, rare, veryRare, legendary, area));
            }
            pond = saved;
            return counts;
            void Add(ItemType t) { counts.TryGetValue(t, out int n); counts[t] = n + 1; }
        }
    }
}
