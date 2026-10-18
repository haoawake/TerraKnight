using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "currencies"): the coin column's four slots and Hallownest's currencies in them. Essence the game gives
    /// (a Whispering Root's IncrementPlayerDataInt, an FSM's PlayerDataIntAdd) arrives as the Dream Essence in a coin
    /// slot; any currency fits any coin slot and nothing else does; a Dream Gate warp's essence leaves the bag, then the
    /// bank; the game's count is the essence held and banked as it goes into a chest, the Piggy Bank and back; Deposit
    /// All, Quick Stack, Shift+click and Quick Stack to Nearby put it onto the stacks already there; a pickup with the coin
    /// slots full goes into the bag and stacks there; a save and its reload keep it; a save with one coin slot and no
    /// essence items loads with its Geo in place and its essence turned into the item; the Seer's own reading of the count
    /// sees the essence held and banked. The coin column is photographed with the essence hovered.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CurrenciesTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 货币 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            var gm = GameManager.instance;
            Player.TerraStats.DevImmortal = true;
            pd.isInvincible = true;
            float settle = Time.time + 3f;
            while (!hero.cState.onGround && Time.time < settle) yield return null;
            var inv = mod.Inventory;
            bool wasInfinite = inv.Infinite;
            mod.SetInfiniteItems(false);
            yield return null;
            var savedBag = (ItemStack[])inv.Slots.Clone();
            var savedBank = (ItemStack[])inv.Bank.Clone();
            var savedMouse = inv.Mouse;
            inv.Mouse = default;
            const ItemType E = ItemType.DreamEssence;
            int c = Inventory.CoinSlot;

            // What the game calls the essence (its own text), and where the picture came from.
            {
                var found = new System.Text.StringBuilder();
                try
                {
                    foreach (var sheet in TeamCherry.Localization.Language.GetSheets())
                        foreach (var key in TeamCherry.Localization.Language.GetKeys(sheet))
                        {
                            string v = TeamCherry.Localization.Language.Get(key, sheet);
                            if (v == null || v.Length > 24) continue;
                            if ((v.Contains("精华") || v.Contains("精華") || v.Contains("Essence") || v.Contains("essence")) && found.Length < 2000)
                                found.Append($"\n    {sheet}/{key} = {v.Replace("\n", " ")}");
                        }
                }
                catch (System.Exception ex) { found.Append(" 出错 " + ex.Message); }
                var d = ItemDb.Get(E);
                var tex = Terraria.TerrariaAssets.Texture(d.Texture);
                Plugin.Log.LogInfo($"[AutoTest] 货币 游戏语言 {TeamCherry.Localization.Language.CurrentLanguage()}，梦境精华叫「{d.Name}」，最多一堆 {d.MaxStack}，图 {d.Texture} 来自 {EssenceWallet.DevIconFrom}（{(tex != null ? tex.width + "x" + tex.height : "无")}）；游戏文本里短的带「精华」的：{found}");
                Check("梦境精华的图用原版的", EssenceWallet.DevIconFrom.Length > 0 && tex != null && tex.width > 4, EssenceWallet.DevIconFrom);
                try
                {
                    if (tex != null && tex.isReadable) System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, "currencies_icon.png"), tex.EncodeToPNG());
                    var hud = GameCameras.instance != null ? GameCameras.instance.hudCanvas : null;
                    var at = hud != null ? hud.transform.Find("Extras/Dream Nail/Sprite") : null;
                    var tk = at != null ? at.GetComponent<tk2dBaseSprite>() : null;
                    var frames = new System.Text.StringBuilder();
                    if (tk != null && tk.Collection != null)
                        foreach (var def in tk.Collection.spriteDefinitions)
                        {
                            if (def == null || def.name == null || !def.name.StartsWith("HUD_dream")) continue;
                            var ft = GeoItem.CaptureTk2d(def, 56, def.name);
                            frames.Append(' ').Append(def.name).Append(ft != null ? $"({ft.width}x{ft.height}{(def.flipped != tk2dSpriteDefinition.FlipMode.None ? " 转" : "")})" : "(无)");
                            if (ft != null) System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, $"currencies_frame_{def.name}.png"), ft.EncodeToPNG());
                        }
                    var anim = at != null ? at.GetComponent<tk2dSpriteAnimator>() : null;
                    Plugin.Log.LogInfo($"[AutoTest] 货币 HUD 精华的帧：{frames}；动画 {anim?.CurrentClip?.name} 默认 {anim?.DefaultClip?.name}");
                }
                catch (System.Exception ex) { Plugin.Log.LogInfo($"[AutoTest] 货币 存图出错 {ex.Message}"); }
            }

            int CoinOf(ItemType t) => Enumerable.Range(c, Inventory.CoinSlots).Where(i => !inv.Slots[i].IsEmpty && inv.Slots[i].Type == t).Sum(i => inv.Slots[i].Count);
            int BagOf(ItemType t) => Enumerable.Range(0, Inventory.Size).Where(i => !inv.Slots[i].IsEmpty && inv.Slots[i].Type == t).Sum(i => inv.Slots[i].Count);
            int BankOf(ItemType t) => inv.Bank.Where(s => !s.IsEmpty && s.Type == t).Sum(s => s.Count);
            int BankStacks(ItemType t) => inv.Bank.Count(s => !s.IsEmpty && s.Type == t);
            long Owned() => EssenceWallet.Held(inv) + EssenceWallet.Banked(inv);
            string Column() => string.Join(" | ", Enumerable.Range(c, Inventory.CoinSlots).Select(i => inv.Slots[i].IsEmpty ? "空" : $"{(inv.Slots[i].Type == E ? "精华" : inv.Slots[i].Type == ItemType.Geo ? "吉欧" : inv.Slots[i].Type.ToString())}×{inv.Slots[i].Count}"));
            string Game() => $"钱币栏 [{Column()}]，游戏精华 {pd.dreamOrbs}，带着+存着 {Owned()}";
            void ClearEssence()
            {
                for (int i = 0; i < Inventory.Total; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == E) inv.Slots[i] = default;
                for (int k = 0; k < 4; k++) { var b = inv.BankOf(k); for (int i = 0; i < b.Length; i++) if (!b[i].IsEmpty && b[i].Type == E) b[i] = default; }
            }

            // 1. The game gives essence: into a coin slot as the item, beside the Geo.
            for (int i = Inventory.HotbarSize; i < Inventory.Total; i++) inv.Slots[i] = default;
            ClearEssence();
            yield return new WaitForSeconds(0.3f);
            GeoWallet.Give(mod, 500);
            yield return new WaitForSeconds(0.3f);
            int before = pd.dreamOrbs;
            for (int k = 0; k < 3; k++) gm.IncrementPlayerDataInt("dreamOrbs");
            gm.IntAdd("dreamOrbs", 20);
            yield return new WaitForSeconds(0.3f);
            Check("游戏给的精华进钱币槽", inv.Slots[c].Type == ItemType.Geo && inv.Slots[c].Count == 500 && inv.Slots[c + 1].Type == E && inv.Slots[c + 1].Count == 23
                && pd.dreamOrbs == 23 && Owned() == 23, $"之前 {before}，{Game()}");

            // 2. The coin slots take any currency, nothing else.
            Check("钱币槽只收货币", Inventory.FitsCoinSlot(ItemDb.Get(E)) && Inventory.FitsCoinSlot(ItemDb.Get(ItemType.Geo)) && !Inventory.FitsCoinSlot(ItemDb.Get(ItemType.IronBar))
                && Inventory.IsCoinSlot(c + 3) && !Inventory.IsCoinSlot(Inventory.Total) && !Inventory.IsAmmoSlot(c + 3) && ItemDb.Get(E).MaxStack == int.MaxValue);
            InventoryGui.Open = true;
            yield return null;
            InventoryGui.DevLeftClick(c + 1);
            bool onMouse = inv.Mouse.Type == E && inv.Mouse.Count == 23 && pd.dreamOrbs == 23;
            yield return null;
            int onMouseGame = pd.dreamOrbs;
            InventoryGui.DevLeftClick(c + 3);
            yield return null;
            bool moved = inv.Slots[c + 3].Type == E && inv.Slots[c + 3].Count == 23 && inv.Slots[c + 1].IsEmpty && inv.Mouse.IsEmpty;
            inv.Mouse = ItemStack.Of(ItemType.IronBar, 3);
            InventoryGui.DevLeftClick(c + 1);
            bool refused = inv.Slots[c + 1].IsEmpty && inv.Mouse.Type == ItemType.IronBar;
            inv.Mouse = default;
            yield return null;
            Check("鼠标把精华放到任意钱币槽，别的东西放不进", onMouse && onMouseGame == 23 && moved && refused && pd.dreamOrbs == 23,
                $"拿在鼠标上 {onMouse}（游戏精华 {onMouseGame}），放到第 4 格 {moved}，铁锭被拒 {refused}，{Game()}");
            inv.Slots[c + 1] = inv.Slots[c + 3];
            inv.Slots[c + 3] = default;
            InventoryGui.Open = false;
            yield return new WaitForSeconds(0.2f);

            // 3. A Dream Gate warp's essence (Dream Return's "DG Warp In": PlayerDataIntAdd -1) leaves the bag.
            gm.IntAdd("dreamOrbs", -1);
            yield return new WaitForSeconds(0.3f);
            Check("梦之门传送花掉的精华从钱币槽扣", CoinOf(E) == 22 && pd.dreamOrbs == 22 && Owned() == 22, Game());

            // 4. A chest: Deposit All onto its essence, the count without it, Loot All back, Quick Stack, Shift+click.
            var feet = hero.GetComponent<BoxCollider2D>().bounds;
            bool chestPlaced = false;
            foreach (float dx in new[] { -1.4f, -2.2f, -3f, -3.8f, 1.4f, 2.2f })
                if (Chests.TryPlace(hero, new Vector2(feet.center.x + dx, feet.min.y + 0.3f))) { chestPlaced = true; break; }
            Chests.DevOpenNear(feet.center);
            var items = Chests.OpenItems;
            Check("放下箱子并打开", chestPlaced && items != null && !Chests.BankOpen, $"放下 {chestPlaced}，打开 {items != null}");
            if (items != null)
            {
                int ChestOf(ItemType t) => items.Where(s => !s.IsEmpty && s.Type == t).Sum(s => s.Count);
                int ChestStacks(ItemType t) => items.Count(s => !s.IsEmpty && s.Type == t);
                IEnumerator ResetChest()
                {
                    for (int i = 0; i < items.Length; i++) items[i] = default;
                    items[7] = ItemStack.Of(E, 5);
                    items[2] = ItemStack.Of(ItemType.Geo, 10);
                    for (int i = Inventory.HotbarSize; i < Inventory.Total; i++) inv.Slots[i] = default;
                    inv.Slots[c] = ItemStack.Of(ItemType.Geo, 500);
                    inv.Slots[c + 1] = ItemStack.Of(E, 22);
                    yield return new WaitForSeconds(0.3f);
                }
                string ChestState() => $"箱子精华 {ChestOf(E)}（{ChestStacks(E)} 堆，第 8 格 {(items[7].IsEmpty ? 0 : items[7].Count)}），箱子吉欧 {ChestOf(ItemType.Geo)}（{ChestStacks(ItemType.Geo)} 堆），{Game()}";

                yield return ResetChest();
                Chests.DepositAll(inv);
                yield return new WaitForSeconds(0.3f);
                Check("全部存入：精华并进箱子原有的那堆", items[7].Type == E && items[7].Count == 27 && ChestStacks(E) == 1 && ChestOf(ItemType.Geo) == 510 && ChestStacks(ItemType.Geo) == 1
                    && CoinOf(E) == 0 && CoinOf(ItemType.Geo) == 0, ChestState());
                Check("精华在箱子里时游戏的数是带着+存着的", pd.dreamOrbs == Owned() && pd.dreamOrbs == 0 && pd.geo == GeoWallet.Held(inv) + GeoWallet.Banked(inv), ChestState());
                Chests.LootAll(inv);
                yield return new WaitForSeconds(0.3f);
                Check("全部拿取：精华回钱币槽，游戏的数回来", ChestOf(E) == 0 && CoinOf(E) == 27 && pd.dreamOrbs == 27 && Owned() == 27 && CoinOf(ItemType.Geo) == 510, ChestState());

                yield return ResetChest();
                Chests.QuickStack(inv);
                yield return new WaitForSeconds(0.3f);
                Check("箱子面板的快速堆叠：精华并进原有的那堆，箱子里有吉欧所以吉欧也进", items[7].Count == 27 && ChestStacks(E) == 1 && CoinOf(E) == 0 && ChestOf(ItemType.Geo) == 510 && pd.dreamOrbs == 0, ChestState());

                yield return ResetChest();
                items[2] = default;
                Chests.QuickStack(inv);
                yield return new WaitForSeconds(0.3f);
                Check("箱子里没有吉欧：快速堆叠只放精华，吉欧留在钱币槽", items[7].Count == 27 && CoinOf(ItemType.Geo) == 500 && ChestOf(ItemType.Geo) == 0, ChestState());

                yield return ResetChest();
                InventoryGui.Open = true;
                yield return null;
                InventoryGui.DevLeftClick(c + 1, shift: true);
                yield return new WaitForSeconds(0.3f);
                Check("Shift 点精华的钱币槽：并进箱子那堆", items[7].Count == 27 && ChestStacks(E) == 1 && CoinOf(E) == 0 && pd.dreamOrbs == 0, ChestState());
                // Some left in the chest for the Seer's count (not counted while there).
                Chests.LootAll(inv);
                items[7] = ItemStack.Of(E, 7);
                Chests.Close(false);
                InventoryGui.Open = false;
                yield return new WaitForSeconds(0.3f);
            }

            // 5. The Piggy Bank: counted; Quick Stack to Nearby onto its essence; spent from it; its panel's Deposit All and Loot All.
            for (int i = Inventory.HotbarSize; i < Inventory.Total; i++) inv.Slots[i] = default;
            for (int i = 0; i < inv.Bank.Length; i++) inv.Bank[i] = default;
            inv.Slots[c] = ItemStack.Of(ItemType.Geo, 500);
            inv.Slots[c + 1] = ItemStack.Of(E, 22);
            inv.Bank[3] = ItemStack.Of(E, 10);
            yield return new WaitForSeconds(0.3f);
            Check("存钱罐里的精华算数", pd.dreamOrbs == 32 && Owned() == 32, Game());
            bool piggy = Stations.PiggyBankWithin(feet.center, 600f * Effects.TerraParticles.Px);
            if (!piggy)
                foreach (float dx in new[] { 1.5f, 2.5f, 3.5f, -4.6f, 4.5f })
                    if (Stations.TryPlace(StationKind.PiggyBank, hero, new Vector2(feet.center.x + dx, feet.min.y + 0.2f))) { piggy = true; break; }
            int movedStacks = Chests.QuickStackNearby(inv, hero.GetComponent<BoxCollider2D>().bounds.center);
            yield return new WaitForSeconds(0.3f);
            Check("快速堆叠到附近：精华并进存钱罐原有的那堆", piggy && BankOf(E) == 32 && BankStacks(E) == 1 && inv.Bank[3].Count == 32 && CoinOf(E) == 0 && pd.dreamOrbs == 32,
                $"有存钱罐 {piggy}，移动 {movedStacks} 组，存钱罐精华 {BankOf(E)}（{BankStacks(E)} 堆），{Game()}");
            gm.IntAdd("dreamOrbs", -1);
            yield return new WaitForSeconds(0.3f);
            Check("背包里没有精华：梦之门花的从存钱罐扣", BankOf(E) == 31 && pd.dreamOrbs == 31, $"存钱罐精华 {BankOf(E)}，{Game()}");
            inv.Slots[c + 2] = ItemStack.Of(E, 4);
            yield return new WaitForSeconds(0.3f);
            InventoryGui.Open = true;
            bool bankOpened = Stations.DevToggleBank(feet.center);
            yield return new WaitForSeconds(0.3f);
            if (Chests.BankOpen)
            {
                Chests.DepositAll(inv);
                yield return new WaitForSeconds(0.3f);
                Check("存钱罐面板的全部存入：精华并进那堆", BankOf(E) == 35 && BankStacks(E) == 1 && CoinOf(E) == 0 && pd.dreamOrbs == 35, $"存钱罐精华 {BankOf(E)}（{BankStacks(E)} 堆），{Game()}");
                Chests.LootAll(inv);
                yield return new WaitForSeconds(0.3f);
                Check("存钱罐面板的全部拿取：回钱币槽", BankOf(E) == 0 && CoinOf(E) == 35 && pd.dreamOrbs == 35, Game());
                Chests.Close(false);
            }
            else Check("打开存钱罐", false, $"打开 {bankOpened}");
            InventoryGui.Open = false;
            yield return new WaitForSeconds(0.3f);

            // 6. The coin slots full: a pickup into the bag, the next onto that stack.
            for (int i = Inventory.HotbarSize; i < Inventory.Total; i++) inv.Slots[i] = default;
            ClearEssence();
            for (int k = 0; k < Inventory.CoinSlots; k++) inv.Slots[c + k] = ItemStack.Of(ItemType.Geo, 100 + k);
            yield return new WaitForSeconds(0.3f);
            gm.IntAdd("dreamOrbs", 7);
            yield return new WaitForSeconds(0.3f);
            int bagSlot = System.Array.FindIndex(inv.Slots, s => !s.IsEmpty && s.Type == E);
            Check("钱币槽满了：精华进背包", bagSlot >= 0 && bagSlot < Inventory.Size && BagOf(E) == 7 && CoinOf(E) == 0 && pd.dreamOrbs == 7, $"在第 {bagSlot} 格，{Game()}");
            gm.IntAdd("dreamOrbs", 3);
            yield return new WaitForSeconds(0.3f);
            Check("再捡到的叠到背包那堆", bagSlot >= 0 && inv.Slots[bagSlot].Count == 10 && inv.Slots.Count(s => !s.IsEmpty && s.Type == E) == 1 && pd.dreamOrbs == 10, Game());

            // 7. The coin column pictured: the Geo, the essence, the essence hovered.
            for (int i = Inventory.HotbarSize; i < Inventory.Total; i++) inv.Slots[i] = default;
            ClearEssence();
            inv.Slots[c] = ItemStack.Of(ItemType.Geo, 500);
            inv.Slots[c + 1] = ItemStack.Of(E, 40);
            inv.Bank[0] = ItemStack.Of(E, 10);
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.4f);
            var hit = InventoryGui.DevHitRect("Slot", c + 1);
            var rects = Enumerable.Range(c, Inventory.CoinSlots).Select(i => InventoryGui.DevHitRect("Slot", i)).ToList();
            bool column = rects.All(r => r != null) && Enumerable.Range(1, rects.Count - 1).All(k => rects[k].Value.y > rects[k - 1].Value.y + 1f && Mathf.Abs(rects[k].Value.x - rects[0].Value.x) < 0.5f);
            Check("钱币栏四格从上到下画出来", column, string.Join(" ", rects.Select(r => r == null ? "无" : $"({r.Value.x:F0},{r.Value.y:F0})")));
            Shot("Town", "currencies_coins");
            yield return null;
            yield return null;
            if (hit != null)
            {
                UI.TerraCursor.DevDrives = true;
                UI.TerraCursor.DevAt = new Vector2(hit.Value.xMax - 2f, hit.Value.yMax - 2f);
            }
            yield return new WaitForSeconds(0.5f);
            Shot("Town", "currencies_hover");
            yield return null;
            yield return null;
            UI.TerraCursor.DevDrives = false;
            UI.TerraCursor.DevAt = null;
            InventoryGui.Open = false;
            yield return new WaitForSeconds(0.3f);

            // 8. A save and its reload; a save with one coin slot and no essence items.
            int slot = gm.profileID;
            Save.ModSave.Write(slot, mod);
            for (int i = Inventory.HotbarSize; i < Inventory.Total; i++) inv.Slots[i] = default;
            ClearEssence();
            Save.ModSave.Read(slot, mod);
            yield return new WaitForSeconds(0.6f);
            Check("存档读回：钱币栏和存钱罐的精华都在", inv.Slots[c].Type == ItemType.Geo && inv.Slots[c].Count == 500 && inv.Slots[c + 1].Type == E && inv.Slots[c + 1].Count == 40
                && BankOf(E) == 10 && pd.dreamOrbs == 50, Game());
            string path = System.IO.Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json");
            int savedSlots = -1;
            bool rewritten = false;
            try
            {
                var jo = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path));
                var list = (Newtonsoft.Json.Linq.JArray)jo["Inventory"];
                savedSlots = list.Count;
                // As a save of one coin slot was: 55 entries (the bag, the ammo, one coin slot), no essence items, no flag.
                while (list.Count > Inventory.AmmoEnd + 1) list.RemoveAt(list.Count - 1);
                jo.Remove("EssenceItems");
                foreach (var name in new[] { "Bank", "Safe", "Forge", "Void" })
                    if (jo[name] is Newtonsoft.Json.Linq.JArray bank)
                        foreach (var e in bank)
                            if ((string)e["Type"] == nameof(ItemType.DreamEssence)) { e["Type"] = "None"; e["Count"] = 0; }
                System.IO.File.WriteAllText(path, jo.ToString(Newtonsoft.Json.Formatting.None));
                rewritten = true;
            }
            catch (System.Exception ex) { Plugin.Log.LogInfo($"[AutoTest] 货币 改旧存档出错 {ex.Message}"); }
            Save.ModSave.Read(slot, mod);
            bool slotsAfterRead = inv.Slots[c].Type == ItemType.Geo && inv.Slots[c + 1].IsEmpty && BankOf(E) == 0;
            yield return new WaitForSeconds(0.6f);
            Check("一格钱币槽的旧存档：吉欧在原处，游戏的精华换成物品", rewritten && savedSlots == Inventory.Total && slotsAfterRead && inv.Slots[c].Count == 500
                && CoinOf(E) == 50 && inv.Slots[c + 1].Type == E && pd.dreamOrbs == 50 && EssenceWallet.Migrated,
                $"存档原有 {savedSlots} 格，读入后精华还没换 {slotsAfterRead}，{Game()}");

            // 9. The Seer: her own machine's reading of the count is the essence held and banked (not the chest's).
            inv.Slots[c + 1].Count = 40;
            inv.Bank[0] = ItemStack.Of(E, 10);
            yield return new WaitForSeconds(0.3f);
            yield return GoTo("RestingGrounds_07", EntryGateOf("RestingGrounds_07"));
            yield return new WaitForSeconds(2f);
            pd.isInvincible = true;
            int seerRead = -1, reads = 0;
            var lines = new System.Text.StringBuilder();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.gameObject.name != "Dream Moth" && !(f.gameObject.name == "Knight" && f.FsmName == "Dream Nail")) continue;
                foreach (var st in f.FsmStates)
                    foreach (var a in st.Actions)
                    {
                        if (!(a is HutongGames.PlayMaker.Actions.GetPlayerDataInt g) || g.intName == null || g.intName.Value != "dreamOrbs") continue;
                        // Her own action run where it stands (not in the state she is in now); the game's call if it can't be.
                        int got = -1;
                        string how = "她自己的动作";
                        try
                        {
                            if (st.Name == f.ActiveStateName) throw new System.InvalidOperationException("正在这个状态");
                            g.OnEnter();
                            got = g.storeValue != null ? g.storeValue.Value : -1;
                        }
                        catch (System.Exception ex)
                        {
                            got = gm.GetPlayerDataInt(g.intName.Value);
                            how = $"GameManager.GetPlayerDataInt（{ex.Message}）";
                        }
                        lines.Append($"\n    {f.gameObject.name} '{f.FsmName}' 状态 {st.Name}：读到 {got}（{how}，存进 {g.storeValue?.Name}）");
                        if (f.gameObject.name == "Dream Moth") { seerRead = got; reads++; }
                    }
            }
            Check("先知读到的精华数是带着+存着的（箱子里的 7 个不算）", reads > 0 && seerRead == 50 && pd.dreamOrbs == 50 && Owned() == 50, $"先知那里 {reads} 处读数{lines}，{Game()}");

            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            for (int i = 0; i < Inventory.Total; i++) inv.Slots[i] = savedBag[i];
            for (int i = 0; i < inv.Bank.Length; i++) inv.Bank[i] = savedBank[i];
            inv.Mouse = savedMouse;
            mod.SetInfiniteItems(wasInfinite);
            yield return new WaitForSeconds(0.3f);
            Player.TerraStats.DevImmortal = false;
            pd.isInvincible = false;
            Plugin.Log.LogInfo($"[AutoTest] 货币 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
