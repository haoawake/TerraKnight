using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "spellshop"): in Dirtmouth, the story beats of the three Nailmasters' arts and the four spells, each said
    /// as its flag or level rises (the beat's flag set, its lines and tip logged; the talk cleared between them); then the
    /// Arms Dealer's shop: with no spell, the sixteen shown locked with the spell each waits for and not bought; with the
    /// Vengeful Spirit, its four bought and the rest locked; with all four, all on sale and the shop in four rows. The
    /// save's levels, flags, bag and purse put back.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SpellShopTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var pd = PlayerData.instance;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 法术商店 {what} {(pass ? "通过" : "不通过")}");
            }
            var saved = (ItemStack[])inv.Slots.Clone();
            int fire0 = pd.fireballLevel, scream0 = pd.screamLevel;
            bool cyc0 = pd.hasCyclone, great0 = pd.hasDashSlash, dash0 = pd.hasUpwardSlash, art0 = pd.hasNailArt;
            bool storyOn0 = StoryBeats.On;
            var granted0 = mod.GrantedAbilities.ToList();
            long purse0 = Shop.Purse;
            bool infinite0 = inv.Infinite, free0 = inv.FreeMaking;
            try
            {
                // The beats: each flag or level put below, a frame for the beats to see it, then raised.
                StoryBeats.SetOn(true);
                var rises = new (string Id, System.Action Lower, System.Action Raise)[]
                {
                    ("cyclone", () => pd.hasCyclone = false, () => { pd.hasCyclone = true; pd.hasNailArt = true; }),
                    ("dashslash", () => pd.hasUpwardSlash = false, () => { pd.hasUpwardSlash = true; pd.hasNailArt = true; }),
                    ("greatslash", () => pd.hasDashSlash = false, () => { pd.hasDashSlash = true; pd.hasNailArt = true; }),
                    ("vengeful", () => pd.fireballLevel = 0, () => pd.fireballLevel = 1),
                    ("wraiths", () => pd.screamLevel = 0, () => pd.screamLevel = 1),
                    ("shadesoul", () => pd.fireballLevel = 1, () => pd.fireballLevel = 2),
                    ("shriek", () => pd.screamLevel = 1, () => pd.screamLevel = 2),
                };
                foreach (var (id, lower, raise) in rises)
                {
                    mod.GrantedAbilities.Remove("story.beat.v1." + id);
                    lower();
                    yield return null;
                    yield return null;
                    int said0 = StoryBeats.DevSaid;
                    raise();
                    for (float t = Time.realtimeSinceStartup + 15f; StoryBeats.DevSaid == said0 && Time.realtimeSinceStartup < t;) yield return null;
                    bool flagged = mod.GrantedAbilities.Contains("story.beat.v1." + id);
                    string first = StoryBeats.DevLast;
                    // The rest of its lines, to read in the log.
                    var lines = new System.Collections.Generic.List<string> { first };
                    for (float t = Time.realtimeSinceStartup + 40f; StoryBeats.Talking && Time.realtimeSinceStartup < t;)
                    {
                        if (StoryBeats.DevLast != lines[lines.Count - 1]) lines.Add(StoryBeats.DevLast);
                        yield return null;
                    }
                    Check(flagged && StoryBeats.DevSaid > said0 && lines.Count >= 3, $"小剧情 {id}：{lines.Count} 句 —— " + string.Join(" / ", lines));
                }

                // The shop: no spell (the infinite bag off: with it everything is on sale, free).
                inv.Infinite = false;
                pd.fireballLevel = 0;
                pd.screamLevel = 0;
                var pos = TownNpcs.DevPos("ArmsDealer");
                Check(pos.HasValue, $"军火商在德特茅斯 {pos.HasValue}");
                if (!pos.HasValue) yield break;
                float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
                hero.transform.position = Clear(hero, new Vector2(pos.Value.x - 1.5f, pos.Value.y - 20f / 32f - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.6f);
                Shop.Earn(5000000);
                IEnumerator Open()
                {
                    TownNpcs.Close(false);
                    yield return null;
                    TownNpcs.TryRightClick(TownNpcs.DevPos("ArmsDealer").Value);
                    yield return new WaitForSeconds(0.2f);
                    TownNpcs.OpenShop();
                    yield return new WaitForSeconds(0.3f);
                }
                int SlotOf(ItemType t) => System.Array.FindIndex(TownNpcs.ShopItems, s => !s.IsEmpty && s.Type == t);
                yield return Open();
                var weapons = SpellRewards.All.ToArray();
                int shown = weapons.Count(w => SlotOf(w) >= 0);
                int lockedRight = weapons.Count(w => { int i = SlotOf(w); return i >= 0 && TownNpcs.Locked(i) && TownNpcs.ShopSpell[i] == SpellRewards.SpellOf(w); });
                int stock = TownNpcs.ShopItems.Count(s => !s.IsEmpty);
                Check(shown == 16 && lockedRight == 16, $"没有法术：16 把都摆着 {shown}，锁着且标明法术 {lockedRight}；商店共 {stock} 格");
                for (int s = 1; s <= 4; s++) Plugin.Log.LogInfo($"[AutoTest] 法术商店 锁定说明 {s}：{SpellRewards.LockText(s)}（游戏自己的名字 {SpellRewards.Name(s)}）");
                int ai = SlotOf(ItemType.RevenantEdge);
                bool boughtLocked = ai >= 0 && TownNpcs.Buy(ai, 1, inv);
                inv.Mouse = default;
                Check(!boughtLocked, $"没吸收复仇之魂时买不了怨魂之锋（买到 {boughtLocked}）");

                // The Vengeful Spirit absorbed.
                pd.fireballLevel = 1;
                yield return Open();
                int onSale = SoulWave.Weapons.Count(w => { int i = SlotOf(w); return i >= 0 && !TownNpcs.Locked(i); });
                int stillLocked = weapons.Where(w => SpellRewards.SpellOf(w) > 1).Count(w => { int i = SlotOf(w); return i >= 0 && TownNpcs.Locked(i); });
                ai = SlotOf(ItemType.RevenantEdge);
                long before = Shop.Purse;
                bool bought = ai >= 0 && TownNpcs.Buy(ai, 1, inv);
                long paid = before - Shop.Purse;
                bool inHand = !inv.Mouse.IsEmpty && inv.Mouse.Type == ItemType.RevenantEdge;
                Check(onSale == 4 && stillLocked == 12 && bought && inHand && paid > 0,
                    $"吸收复仇之魂后：它的四把在卖 {onSale}，其余锁着 {stillLocked}，买下怨魂之锋 {bought}（拿在光标上 {inHand}），花了 {Shop.Money(paid)}");
                inv.Mouse = default;
                foreach (var w in weapons) { int i = SlotOf(w); if (i >= 0) Plugin.Log.LogInfo($"[AutoTest] 法术商店 {ItemDb.Get(w).Name} 第 {i} 格 {(TownNpcs.Locked(i) ? "锁着" : "在卖 " + Shop.Money(TownNpcs.BuyPrice(i)))}"); }

                // All four.
                pd.fireballLevel = 2;
                pd.screamLevel = 2;
                yield return Open();
                int allOnSale = weapons.Count(w => { int i = SlotOf(w); return i >= 0 && !TownNpcs.Locked(i); });
                Check(allOnSale == 16 && TownNpcs.ShopRowsShown == 4, $"四个法术都吸收后：16 把都在卖 {allOnSale}，商店 {TownNpcs.ShopRowsShown} 行");
                Plugin.Log.LogInfo($"[AutoTest] 法术商店 军火商：{TownNpcs.DevStock()}");
                TownNpcs.Close(false);
            }
            finally
            {
                TownNpcs.Close(false);
                InventoryGui.Open = false;
                inv.Mouse = default;
                for (int i = 0; i < saved.Length && i < inv.Slots.Length; i++) inv.Slots[i] = saved[i];
                inv.Infinite = infinite0;
                inv.FreeMaking = free0;
                pd.fireballLevel = fire0;
                pd.screamLevel = scream0;
                pd.hasCyclone = cyc0;
                pd.hasDashSlash = great0;
                pd.hasUpwardSlash = dash0;
                pd.hasNailArt = art0;
                mod.GrantedAbilities.Clear();
                foreach (var g in granted0) mod.GrantedAbilities.Add(g);
                StoryBeats.SetOn(storyOn0);
                long diff = Shop.Purse - purse0;
                if (diff > 0) Shop.Spend(diff);
                Plugin.Log.LogInfo($"[AutoTest] 法术商店 结果 {ok}/{all}");
            }
        }
    }
}
