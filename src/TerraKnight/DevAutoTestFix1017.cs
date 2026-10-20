using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "fx_17&lt;name&gt;", "fx_17all"): the 2026-10-17 batch. "book": the monster book's own drawing time a
    /// frame with every enemy seen and beaten, and the frame time with it open against the inventory alone; "search":
    /// the book's search by an enemy's name and by a drop's, typed letters going to the box and not the Knight;
    /// "buff": Quick Buff on B; "zoom": Terraria's zoom keys held; "lantern": the Lumafly Lantern in the light pet slot
    /// (out, counted as the lantern, its light in a dark room, no copy made of it).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Fix1017(string which)
        {
            bool all = which == "all";
            if (GameManager.instance.sceneName != "Crossroads_01")
            {
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(1.5f);
            }
            PlayerData.instance.isInvincible = true;
            if (all || which == "book") yield return BookFps17();
            if (all || which == "search") yield return BookSearch17();
            if (all || which == "buff") yield return QuickBuff17();
            if (all || which == "zoom") yield return Zoom17();
            if (all || which == "lantern") yield return LanternPet17();
            if (all || which == "sewer") yield return Sewer17();
            if (all || which == "aggro") yield return Aggro17();
            if (all || which == "reforge") yield return Reforge17();
            if (all || which == "map") yield return Map17();
            if (all || which == "bed") yield return Bed17();
            if (all || which == "storage") yield return Storage17();
            if (all || which == "vacancy") yield return Vacancy17();
            if (all || which == "potions") yield return Potions17();
            if (which == "grimmprobe") yield return GrimmProbe17();
            if (all || which == "shadowdash") yield return ShadowDash17();
            if (all || which == "grimm") yield return Grimm17();
            if (all || which == "shops") Shops17();
            if (all || which == "nailaim") yield return NailAim17();
            if (all || which == "buffstations") yield return BuffStations17();
            if (which == "colosseum") yield return Colosseum17();
            if (all || which == "fishchest") yield return FishingChest17();
            if (all || which == "broadcast") yield return Broadcast17();
            if (which == "prview") yield return PrView17();
            if (which.StartsWith("holeview")) yield return HoleView17(which.Length > 9 ? which.Substring(9).Split('.') : null);
            if (which == "dlgprobe") yield return DialogueProbe17();
            if (which == "chathousing") yield return ChatHousing17();
            if (which == "tips") yield return Tips17();
            if (which == "crates") yield return Crates17();
            if (which == "mapoverlap") yield return MapOverlap17();
            if (which == "mapfix") yield return MapFix17();
            if (which == "georockprobe") yield return GeoRockProbe17();
            if (which == "georock") yield return GeoRock17();
            if (which == "bossdrops") yield return BossDrops17();
            if (which == "housequery") yield return HouseQuery17();
            if (which == "worldchests") yield return WorldChests17();
            if (which == "build") yield return Build17();
            if (which == "knockjuggle") yield return KnockJuggle17();
            if (which == "bookleak") yield return BookLeak17();
            if (which.StartsWith("areaart")) yield return AreaArtExport(which.Length > 8 ? which.Substring(8) : null);
            if (which.StartsWith("areablocks")) yield return AreaBlocks17(which.Length > 11 ? which.Substring(11).Split('.') : null);
            if (which == "nkgprobe")
            {
                // The beaten save's flags, then Nightmare Grimm's seal: what the dream's machines read of PlayerData, logged on arrival.
                var pd = PlayerData.instance;
                foreach (var f in typeof(PlayerData).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    string n = f.Name;
                    if (f.FieldType == typeof(bool) && (n.StartsWith("killed") || n.Contains("efeated") || n.EndsWith("Encountered"))) f.SetValue(pd, true);
                    else if (f.FieldType == typeof(int) && n.EndsWith("Defeated")) f.SetValue(pd, 2);
                }
                mod.StartCoroutine(PlayerDataReads("Grimm_Nightmare"));
                mod.StartCoroutine(FsmTrace("Grimm_Nightmare", 20f));
                mod.StartCoroutine(DumpWhenIn("Grimm_Nightmare", "Grimm Control"));
                yield return SealStartTest("NightmareGrimm");
            }
            if (which.StartsWith("sealsbeaten"))
            {
                // A late save: every boss beaten (every PlayerData "killed*"/"*efeated*" bool on, the warriors' dream ints at
                // 2, absorbed), then each seal used: it must still bring its boss and start the fight there.
                var pd = PlayerData.instance;
                int bools = 0, ints = 0;
                foreach (var f in typeof(PlayerData).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    string n = f.Name;
                    if (f.FieldType == typeof(bool) && (n.StartsWith("killed") || n.Contains("efeated") || n.EndsWith("Encountered") || n == "falseKnightDefeated")) { f.SetValue(pd, true); bools++; }
                    else if (f.FieldType == typeof(int) && n.EndsWith("Defeated")) { f.SetValue(pd, 2); ints++; }
                }
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 后期存档：{bools} 个击败记录、{ints} 个梦境战士记录");
                yield return SealStartTest(which.Length > 12 ? which.Substring(12) : null);
            }
            if (which == "guardian")
            {
                // The Crystal Guardian beaten once (gone from Mines_18 to its enraged form in Mines_32): its seal still brings the fight.
                PlayerData.instance.SetBool("killedMegaBeamMiner", true);
                PlayerData.instance.SetBool("defeatedMegaBeamMiner", true);
                yield return SealStartTest("MegaBeamMiner");
            }
        }

        /// <summary>
        /// The Grimmchild got (gotCharm_40, level 2), not worn: refused off a bench; put on from the charm page on one, the game
        /// sees the charm worn and its own Grimmchild flies; taken off, neither. The notches and the bench put back after.
        /// </summary>
        IEnumerator Grimm17()
        {
            var pd = PlayerData.instance;
            QuietAround(HeroController.instance.transform.position);
            pd.SetBool("gotCharm_40", true);
            pd.SetInt("grimmChildLevel", 2);
            bool bench = pd.atBench;
            int slots = pd.charmSlots;
            if (Charms.Worn(Charms.Grimmchild)) { pd.atBench = true; Charms.Click(Charms.Grimmchild); pd.atBench = bench; }
            yield return new WaitForSeconds(1f);
            bool before = pd.GetBool("equippedCharm_40");
            pd.charmSlots = Mathf.Max(slots, pd.charmSlotsFilled + Charms.Cost(Charms.Grimmchild));
            pd.atBench = false;
            var refused = Charms.Click(Charms.Grimmchild);
            pd.atBench = true;
            var answer = Charms.Click(Charms.Grimmchild);
            pd.atBench = bench;
            yield return new WaitForSeconds(2f);
            var child = GameObject.FindWithTag("Grimmchild");
            bool worn = pd.GetBool("equippedCharm_40");
            Shot(GameManager.instance.sceneName, "fx17_grimm");
            yield return null;
            FxCheck("不在长椅上不能换护符", refused == Charms.Answer.NotAtBench, $"{refused}");
            FxCheck("长椅上从护符页装上格林之子：算作佩戴，原版的格林之子出来跟着", !before && worn && answer == Charms.Answer.On && child != null && child.activeInHierarchy,
                $"之前算佩戴 {before}，之后 {worn}，{answer}，格林之子 {(child != null ? child.name + " " + (Vector2)child.transform.position : "没有")}");
            pd.atBench = true;
            Charms.Click(Charms.Grimmchild);
            pd.atBench = bench;
            pd.charmSlots = slots;
            Charms.Apply();
            yield return new WaitForSeconds(2.5f);
            child = GameObject.FindWithTag("Grimmchild");
            FxCheck("卸下：不再算佩戴，格林之子回去", !pd.GetBool("equippedCharm_40") && (child == null || !child.activeInHierarchy),
                $"算佩戴 {pd.GetBool("equippedCharm_40")}，格林之子 {(child != null && child.activeInHierarchy ? "还在" : "走了")}");
        }

        /// <summary>
        /// The Fishing Chest (Items.FishingChest): its item and picture; 200 opened at stage 5 (the Apprentice Bait each time,
        /// the Journeyman's about half, the Master's about a quarter, critter bait, now and then a potion, an accessory, a fish
        /// weapon or tool); one opened from the bag by a right click; about 1 catch in 25 a chest.
        /// </summary>
        IEnumerator FishingChest17()
        {
            var def = ItemDb.Get(FishingChest.Type);
            var tex = Terraria.TerrariaAssets.Texture(def.Texture);
            FxCheck("钓鱼宝箱：物品和贴图", def.Name == "钓鱼宝箱" && def.Opens && tex != null && tex.width > 8, $"{def.Name}，贴图 {(tex != null ? tex.width + "×" + tex.height : "无")}");
            int stageWas = Shop.DevStage;
            Shop.DevStage = 5;
            int apprentice = 0, journeyman = 0, master = 0, critter = 0, potion = 0, acc = 0, gear = 0;
            for (int i = 0; i < 200; i++)
            {
                var got = new List<ItemStack>();
                FishingChest.Contents(got);
                foreach (var s in got)
                {
                    var d = ItemDb.Get(s.Type);
                    if (s.Type == ItemType.ApprenticeBait) apprentice++;
                    else if (s.Type == ItemType.JourneymanBait) journeyman++;
                    else if (s.Type == ItemType.MasterBait) master++;
                    else if (d.BaitPower > 0) critter++;
                    else if (d.IsAccessory) acc++;
                    else if (d.BuffType > 0) potion++;
                    else gear++;
                }
            }
            FxCheck("钓鱼宝箱：200 箱里鱼饵和偶尔的好东西", apprentice == 200 && journeyman > 70 && journeyman < 130 && master > 25 && master < 80 && critter >= 500
                && potion > 35 && potion < 110 && acc > 3 && acc < 40 && gear > 2 && gear < 35,
                $"学徒 {apprentice}，熟手 {journeyman}，大师 {master}，小动物鱼饵 {critter} 组，药水 {potion}，饰品 {acc}，武器工具 {gear}");
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = default;
            inv.Slots[5] = ItemStack.Of(FishingChest.Type, 2);
            int opened0 = FishingChest.DevOpened;
            bool open = FishingChest.TryOpen(inv, 5);
            int bait = 0;
            for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Def.BaitPower > 0) bait += inv.Slots[i].Count;
            FxCheck("钓鱼宝箱：背包里右键打开，消耗一个，鱼饵进背包", open && FishingChest.DevOpened == opened0 + 1 && inv.Slots[5].Type == FishingChest.Type && inv.Slots[5].Count == 1 && bait >= 2,
                $"打开 {open}，剩 {inv.Slots[5].Count}，背包鱼饵 {bait}");
            System.Array.Copy(saved, inv.Slots, saved.Length);
            inv.Infinite = infinite;
            int caught = 0;
            for (int i = 0; i < 5000; i++) if (FishingChest.Caught(false)) caught++;
            FxCheck("钓鱼宝箱：约每 25 次上钩出一个", caught > 130 && caught < 280, $"5000 次 {caught} 个");
            Shop.DevStage = stageWas;
            yield return null;
        }

        /// <summary>
        /// The Colosseum summon (Items.ColosseumTicket): the colosseum's two boss seals gone, an old save's God Tamer whistle
        /// back as it, its recipe; used from Crossroads_01 at stage 4, the player in the colosseum's hall by its door and one
        /// spent; used again there, refused and nothing spent.
        /// </summary>
        IEnumerator Colosseum17()
        {
            FxCheck("竞技场两个首领召唤物已删除", BossSummons.Find(ItemType.BossSigilOblobbles) == null && BossSummons.Find(ItemType.BossSigilGodTamer) == null,
                $"双生酸囊 {BossSummons.Find(ItemType.BossSigilOblobbles) != null}，驯神兽笛 {BossSummons.Find(ItemType.BossSigilGodTamer) != null}");
            var back = Save.ModSave.DevRoundTrip(ItemStack.Of(ItemType.BossSigilGodTamer, 2));
            bool recipe = false;
            foreach (var r in Recipes.All) if (r.Result == ColosseumTicket.Type) recipe = true;
            FxCheck("旧存档的驯神兽笛读回来是竞技场召唤物，能合成", back.Type == ColosseumTicket.Type && back.Count == 2 && recipe && ItemDb.Get(ColosseumTicket.Type).Name == "竞技场召唤物",
                $"读回 {back.Type}×{back.Count}，配方 {recipe}，名字 {ItemDb.Get(ColosseumTicket.Type).Name}");
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            bool infinite = inv.Infinite;
            int stageWas = Shop.DevStage;
            Shop.DevStage = 4;
            inv.Infinite = false;
            inv.Slots[0] = ItemStack.Of(ColosseumTicket.Type, 2);
            inv.Selected = 0;
            inv.Override = -1;
            var gm = GameManager.instance;
            float until = Time.time + 2f;
            while (!mod.DevReady && Time.time < until) yield return null;
            mod.DevUseSelected(HeroController.instance.transform.position);
            until = Time.realtimeSinceStartup + 40f;
            yield return new WaitForSeconds(1f);
            while (Time.realtimeSinceStartup < until && (gm.IsInSceneTransition || gm.sceneName != ColosseumTicket.Scene || !GameHooks.PlayerHasControl())) yield return null;
            yield return new WaitForSeconds(1f);
            int left = inv.Slots[0].IsEmpty ? 0 : inv.Slots[0].Count;
            var hero = HeroController.instance;
            Shot(gm.sceneName, "fx17_colosseum");
            yield return null;
            FxCheck("用了传送到愚人斗兽场门口，消耗一个", gm.sceneName == ColosseumTicket.Scene && left == 1 && GameHooks.PlayerHasControl(),
                $"到了 {gm.sceneName} {(Vector2)hero.transform.position:F1}，剩 {left}，{ColosseumTicket.DevLast}");
            until = Time.time + 2f;
            while (!mod.DevReady && Time.time < until) yield return null;
            mod.DevUseSelected(hero.transform.position);
            yield return new WaitForSeconds(1.5f);
            int left2 = inv.Slots[0].IsEmpty ? 0 : inv.Slots[0].Count;
            FxCheck("在斗兽场里再用：不传送、不消耗", gm.sceneName == ColosseumTicket.Scene && left2 == 1 && ColosseumTicket.DevLast == "已经在愚人斗兽场里",
                $"在 {gm.sceneName}，剩 {left2}，{ColosseumTicket.DevLast}");
            System.Array.Copy(saved, inv.Slots, saved.Length);
            inv.Infinite = infinite;
            Shop.DevStage = stageWas;
        }

        /// <summary>
        /// The touch stations as carried buffs (Items.BuffStationItems): a Crystal Ball in the bag gives Clairvoyance with no
        /// timer, gone once it is taken out; a Sharpening Station in the Piggy Bank gives Sharpened; neither carried, neither.
        /// </summary>
        IEnumerator BuffStations17()
        {
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            var bank = (ItemStack[])inv.BankOf(0).Clone();
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            Player.PlayerBuffs.Clear();
            int free = System.Array.FindIndex(inv.Slots, s => s.IsEmpty);
            inv.Slots[free] = ItemStack.Of(ItemType.CrystalBall);
            yield return new WaitForSeconds(0.3f);
            bool ball = Player.PlayerBuffs.Has(29) && Player.PlayerBuffs.NoTime(29);
            inv.Slots[free] = default;
            yield return new WaitForSeconds(0.4f);
            bool ballGone = !Player.PlayerBuffs.Has(29);
            FxCheck("水晶球放背包：有魔能增益、不计时；拿走后消失", ball && ballGone, $"有 {ball}，拿走后消失 {ballGone}");
            var piggy = inv.BankOf(0);
            int slot = System.Array.FindIndex(piggy, s => s.IsEmpty);
            piggy[slot] = ItemStack.Of(ItemType.SharpeningStation);
            yield return new WaitForSeconds(0.3f);
            bool sharp = Player.PlayerBuffs.Has(159);
            piggy[slot] = default;
            yield return new WaitForSeconds(0.4f);
            bool sharpGone = !Player.PlayerBuffs.Has(159);
            FxCheck("利器站放猪猪存钱罐：有锋利增益，拿出来后消失", sharp && sharpGone, $"有 {sharp}，拿出后消失 {sharpGone}，计数 {BuffStationItems.DevHeld}");
            System.Array.Copy(saved, inv.Slots, saved.Length);
            System.Array.Copy(bank, inv.BankOf(0), bank.Length);
            inv.Infinite = infinite;
        }

        /// <summary>
        /// The nail at the mouse (Items.NailAim) in Crossroads_01: on the ground 30° up-right is a level slash turned up by
        /// 30, its picture toward the cursor; 80° up an up slash; behind and below on the ground a level slash the other
        /// way; in the air straight down a down slash (the pogo's). Off: no turn. The Dream Nail's slash turned alike.
        /// </summary>
        IEnumerator NailAim17()
        {
            if (GameManager.instance.sceneName != "Crossroads_01")
            {
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(1.5f);
            }
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            QuietAround(hero.transform.position);
            var col = hero.GetComponent<BoxCollider2D>();
            bool was = NailAim.On;
            NailAim.Set(true);
            int nail = System.Array.FindIndex(inv.Slots, s => !s.IsEmpty && s.Type == ItemType.KnightNail);
            if (nail < 0) { FxCheck("骨钉朝鼠标：背包里有骨钉", false, ""); yield break; }
            inv.Selected = nail;
            inv.Override = -1;
            yield return new WaitForSeconds(0.3f);
            // The slash this swing uses (HeroController.slashComponent), and where its hitbox lies from the Knight.
            var slashOf = HarmonyLib.AccessTools.FieldRefAccess<HeroController, NailSlash>("slashComponent");
            NailSlash Showing()
            {
                var s = slashOf(hero);
                var r = s != null ? s.GetComponent<MeshRenderer>() : null;
                return r != null && r.enabled ? s : null;
            }
            Vector2 HitboxMiddle(NailSlash s)
            {
                var poly = s.GetComponent<PolygonCollider2D>();
                if (poly == null || poly.points.Length == 0) return s.GetComponent<MeshRenderer>().bounds.center;
                Vector2 sum = Vector2.zero;
                foreach (var pt in poly.points) sum += (Vector2)s.transform.TransformPoint(pt + poly.offset);
                return sum / poly.points.Length;
            }
            IEnumerator Swing(float angle, bool air, System.Action<string, float, float> done)
            {
                // Settled on the ground first; in the air, lifted three tiles and let fall.
                for (float t = Time.time + 2f; !hero.cState.onGround && Time.time < t;) yield return null;
                yield return new WaitForSeconds(0.45f);
                if (air)
                {
                    hero.transform.position = Clear(hero, (Vector2)hero.transform.position + new Vector2(0f, 3f));
                    yield return new WaitForFixedUpdate();
                }
                Vector2 c = col.bounds.center;
                NailAim.DevAim = c + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 4f;
                NailAim.DevLastKind = "";
                NailItem.DevHold = true;
                float shown = float.NaN;
                for (float t = Time.time + 0.4f; Time.time < t && float.IsNaN(shown);)
                {
                    yield return null;
                    var s = Showing();
                    if (s == null) continue;
                    Vector2 d = HitboxMiddle(s) - (Vector2)col.bounds.center;
                    shown = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                }
                // The picture a few frames into the slash, its arc drawn.
                for (int k = 0; k < 3; k++) yield return null;
                Shot(GameManager.instance.sceneName, $"fx17_nailaim_{(NailAim.On ? "on" : "off")}_{Mathf.RoundToInt(angle)}");
                NailItem.DevHold = false;
                done(NailAim.DevLastKind, NailAim.DevLastTurn, shown);
                yield return new WaitForSeconds(0.5f);
            }
            string kind = null; float turn = 0f, pic = 0f;
            void Got(string k, float tn, float p) { kind = k; turn = tn; pic = p; }
            yield return Swing(30f, false, Got);
            FxCheck("骨钉朝鼠标：右上 30° 是横劈，往上转 30°，图朝鼠标", kind == "横劈" && Mathf.Abs(turn - 30f) < 6f && Mathf.Abs(turn - Mathf.DeltaAngle(0f, NailAim.DevLastAngle)) < 0.5f && hero.cState.facingRight && Mathf.Abs(Mathf.DeltaAngle(pic, 30f)) < 30f,
                $"{kind} 转 {turn:F0}°，图在 {pic:F0}°，朝右 {hero.cState.facingRight}");
            yield return Swing(80f, false, Got);
            FxCheck("骨钉朝鼠标：80° 是上劈（后坐力按原版向下）", kind == "上劈" && Mathf.Abs(turn + 10f) < 6f, $"{kind} 转 {turn:F0}°，图在 {pic:F0}°");
            yield return Swing(-150f, false, Got);
            FxCheck("骨钉朝鼠标：在地上朝左下是横劈，人转向左", kind == "横劈" && !hero.cState.facingRight && Mathf.Abs(turn - 30f) < 6f && Mathf.Abs(Mathf.DeltaAngle(pic, -150f)) < 30f,
                $"{kind} 转 {turn:F0}°，图在 {pic:F0}°，朝右 {hero.cState.facingRight}");
            yield return Swing(-80f, true, Got);
            FxCheck("骨钉朝鼠标：空中朝下是下劈（弹跳按原版）", kind == "下劈" && Mathf.Abs(turn - 10f) < 6f, $"{kind} 转 {turn:F0}°，图在 {pic:F0}°");
            NailAim.Set(false);
            yield return Swing(30f, false, Got);
            FxCheck("骨钉朝鼠标关掉：不转，照原版横劈", kind == "" && Mathf.Abs(Mathf.DeltaAngle(pic, hero.cState.facingRight ? 0f : 180f)) < 30f, $"{kind}，图在 {pic:F0}°");
            // The Dream Nail: charged and let go toward 45° up-left.
            NailAim.Set(true);
            PlayerData.instance.hasDreamNail = true;
            int dream = System.Array.FindIndex(inv.Slots, s => !s.IsEmpty && s.Type == ItemType.DreamNail);
            if (dream < 0) { inv.Pickup(ItemType.DreamNail, 1); dream = System.Array.FindIndex(inv.Slots, s => !s.IsEmpty && s.Type == ItemType.DreamNail); }
            if (dream >= 0)
            {
                inv.Selected = dream;
                yield return new WaitForSeconds(0.5f);
                for (float t = Time.time + 2f; !hero.cState.onGround && Time.time < t;) yield return null;
                NailAim.DevAim = (Vector2)col.bounds.center + new Vector2(-3f, 3f);
                NailAim.DevLastKind = "";
                DreamNailItem.DevHold = true;
                float t0 = Time.time;
                // Held to the full charge (released early the Dream Nail cancels), then let go.
                while (Time.time - t0 < 4f && NailAim.DevLastKind != "梦之钉")
                {
                    if (Time.time - t0 > 2.4f) DreamNailItem.DevHold = false;
                    yield return null;
                }
                DreamNailItem.DevHold = null;
                FxCheck("梦之钉朝鼠标：左上 45° 转 45°，人朝左", NailAim.DevLastKind == "梦之钉" && Mathf.Abs(Mathf.Abs(NailAim.DevLastTurn) - 45f) < 2f && !hero.cState.facingRight,
                    $"{NailAim.DevLastKind} 转 {NailAim.DevLastTurn:F0}°，挥砍 {NailAim.DevDreamSlash}，朝右 {hero.cState.facingRight}");
                yield return new WaitForSeconds(1f);
            }
            else FxCheck("梦之钉朝鼠标：背包里有梦之钉", false, "");
            NailAim.DevAim = null;
            NailItem.DevHold = null;
            NailAim.Set(was);
            inv.Selected = nail;
        }

        /// <summary>
        /// Dev: holes dug in a few areas' terrain (an oval pit under the Knight, a stair notch and a pocket in a wall beside
        /// him), pictured in Hollow Knight's style and in Terraria's (World.HoleShapes), for comparing.
        /// </summary>
        IEnumerator HoleView17(string[] scenes)
        {
            bool was = World.HoleShapes.Hollow;
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            foreach (var scene in scenes ?? new[] { "Crossroads_01", "Fungus1_01", "Mines_02", "Ruins1_01" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                PlayerData.instance.isInvincible = true;
                QuietAround(HeroController.instance.transform.position, 60f);
                yield return new WaitForSeconds(1.5f);
                QuietAround(HeroController.instance.transform.position, 60f);
                var hero = HeroController.instance;
                // Away from the entrance (its own floor and art aren't the grid's), into the room, landed.
                hero.transform.position = Clear(hero, (Vector2)hero.transform.position + new Vector2(14f, 1f));
                for (float t = Time.time + 3f; !hero.cState.onGround && Time.time < t;) yield return null;
                yield return new WaitForSeconds(0.5f);
                var feet = World.CellGrid.WorldToCell((Vector2)hero.GetComponent<BoxCollider2D>().bounds.min + new Vector2(hero.GetComponent<BoxCollider2D>().bounds.extents.x, -0.1f));
                int dug = 0;
                void Dig(int gx, int gy)
                {
                    if (!World.TerraWorld.IsSolid(gx, gy)) return;
                    World.TerraWorld.Set(gx, gy, World.CellKind.Empty);
                    dug++;
                }
                // Beside him, his own ground kept: an oval pit to the right (its floor kept), a stair notch beyond, and a pocket
                // inside the ground to the left.
                bool Floor(int gx, int gy) { for (int k = 1; k <= 3; k++) if (!World.TerraWorld.IsSolid(gx, gy - k)) return false; return true; }
                for (int dy = -5; dy <= 0; dy++)
                    for (int dx = 4; dx <= 16; dx++)
                        if ((dx - 10) * (dx - 10) / 36f + (dy + 2.5f) * (dy + 2.5f) / 9f <= 1f && Floor(feet.x + dx, feet.y - 5)) Dig(feet.x + dx, feet.y + dy);
                for (int k = 0; k < 6; k++)
                    for (int j = 0; j <= k; j++) Dig(feet.x + 19 + k, feet.y - j);
                for (int dy = -5; dy <= -2; dy++)
                    for (int dx = -12; dx <= -6; dx++) Dig(feet.x + dx, feet.y + dy);
                Plugin.Log.LogInfo($"[AutoTest] 挖洞外观 {scene}：挖了 {dug} 格，脚下格 {feet}，现在实心 {World.TerraWorld.IsSolid(feet.x, feet.y - 2)}，美术已抠 {World.TerraWorld.ArtCutAt(feet.x, feet.y - 2)}，区域 {World.TerraWorld.RegionAt(feet.x, feet.y)?.SceneName}，主角 {(Vector2)hero.transform.position:F1}，屏幕 {(Vector2)Camera.main.WorldToScreenPoint(hero.transform.position):F0} / {Screen.width}x{Screen.height}");
                for (int k = 0; k < 6; k++)
                {
                    yield return new WaitForSeconds(0.25f);
                    var h = HeroController.instance;
                    Plugin.Log.LogInfo($"[AutoTest] 挖洞外观 {k * 0.25f + 0.25f:F2}s {GameManager.instance.sceneName} 主角 {(Vector2)h.transform.position:F1} 死 {h.cState.dead} 地上 {h.cState.onGround} 速度 {h.GetComponent<Rigidbody2D>().linearVelocity:F1} 生命 {mod.Stats.Life}");
                }
                Plugin.Log.LogInfo($"[AutoTest] 挖洞外观 {scene}：1.5 秒后主角 {(Vector2)HeroController.instance.transform.position:F1}，实心 {World.TerraWorld.IsSolid(feet.x, feet.y - 2)}");
                World.HoleShapes.Set(true);
                yield return new WaitForSeconds(0.6f);
                Shot(scene, "holes_hk");
                yield return new WaitForSeconds(0.4f);
                World.HoleShapes.Set(false);
                yield return new WaitForSeconds(0.6f);
                Shot(scene, "holes_terraria");
                yield return new WaitForSeconds(0.4f);
            }
            World.HoleShapes.Set(was);
            Player.TerraStats.DevImmortal = false;
            FxCheck("挖洞外观截图（看图）", true, $"形状缓存 {World.HoleShapes.DevShapes} 种");
        }

        /// <summary>Dev: pictures of the crafting window (all, then the weapons tab) and the housing panel in Dirtmouth.</summary>
        IEnumerator PrView17()
        {
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var inv = mod.Inventory;
            inv.Pickup(ItemType.Wood, 99);
            inv.Pickup(ItemType.CopperBar, 30);
            InventoryGui.Open = true;
            var grid = HarmonyLib.AccessTools.Field(typeof(InventoryGui), "grid");
            grid.SetValue(null, true);
            yield return new WaitForSeconds(0.5f);
            Shot("Town", "fx17_craft_window");
            yield return new WaitForSeconds(0.3f);
            InventoryGui.DevFilterShot("fx17_craft_window_weapons", 0);
            yield return new WaitForSeconds(0.5f);
            InventoryGui.DevFilterShot("fx17_craft_window_all", -1);
            yield return new WaitForSeconds(0.3f);
            grid.SetValue(null, false);
            Town.TownNpcs.DevPanelShot("fx17_housing_panel");
            yield return new WaitForSeconds(0.5f);
            Town.TownNpcs.ClosePanel();
            InventoryGui.Open = false;
            FxCheck("PR 界面截图（看图）", true, "");
        }

        /// <summary>
        /// Elderbug talked to: his words go into the chat a line a second, the dialogue box is gone at once and the Knight
        /// moves again soon after; the lines' cutting (pages, line breaks, tags).
        /// </summary>
        IEnumerator Broadcast17()
        {
            var cut = UI.DialogueBroadcast.Lines("A<br>B<page>C <b>D</b>");
            FxCheck("台词按页和换行拆成句", cut.Count == 3 && cut[0] == "A" && cut[1] == "B" && cut[2] == "C D", string.Join("|", cut));
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            GameObject elder = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Elderbug") { elder = t.gameObject; break; }
            if (elder == null) { FxCheck("播报 找老虫子", false, ""); yield break; }
            hero.transform.position = Clear(hero, (Vector2)elder.transform.position + new Vector2(-1.5f, 0.5f));
            yield return new WaitForSeconds(1.5f);
            // The plain box's own machine (DialogueManager's "Box Open"): never in its Box Up state through the talk.
            PlayMakerFSM boxFsm = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f != null && f.FsmName == "Box Open") { boxFsm = f; break; }
            bool boxUp = false, watching = true;
            IEnumerator WatchBox()
            {
                while (watching)
                {
                    if (boxFsm != null && boxFsm.ActiveStateName == "Box Up") boxUp = true;
                    yield return null;
                }
            }
            mod.StartCoroutine(WatchBox());
            int kept0 = UI.DialogueBroadcast.DevBoxKept;
            int told0 = UI.DialogueBroadcast.DevTold, chat0 = Net.NetChat.DevLines;
            for (int i = 0; i < 6 && UI.DialogueBroadcast.DevTold == told0; i++)
            {
                yield return Tap(UnityEngine.InputSystem.Key.W, 0.2f);
                yield return new WaitForSeconds(1.5f);
            }
            float t0 = Time.realtimeSinceStartup;
            bool told = UI.DialogueBroadcast.DevTold > told0;
            float back = -1f;
            bool boxSeen = false;
            while (Time.realtimeSinceStartup - t0 < 4f)
            {
                yield return null;
                if (Time.realtimeSinceStartup - t0 > 0.5f && UI.HudDialogue.DevOpen) boxSeen = true;
                if (back < 0f && GameHooks.PlayerHasControl()) back = Time.realtimeSinceStartup - t0;
            }
            int lines = Net.NetChat.DevLines - chat0;
            Shot("Town", "fx17_broadcast");
            yield return null;
            watching = false;
            FxCheck("泰拉瑞亚方式对话：原版对话框不弹起", boxFsm != null && !boxUp && UI.DialogueBroadcast.DevBoxKept > kept0,
                $"Box Open {(boxFsm != null ? boxFsm.ActiveStateName : "没找到")}，弹起过 {boxUp}，拦下 BOX UP {UI.DialogueBroadcast.DevBoxKept - kept0} 次");
            FxCheck("和老虫子说话：台词进聊天框，对话框不留，很快能动", told && !boxSeen && back >= 0f && back < 2.5f && lines >= 2,
                $"播报 {told}（{UI.DialogueBroadcast.DevLastConvo}），框还开着 {boxSeen}，{back:F1} 秒后能动，4 秒内 {lines} 句：{Net.NetChat.DevLastLine}，还排着 {UI.DialogueBroadcast.DevWaiting}");
            // Talked to again: while its lines come, put past Dirtmouth's left edge he is stopped inside; once told, free.
            for (int i = 0; i < 40 && UI.DialogueBroadcast.DevWaiting > 0; i++) yield return new WaitForSeconds(0.25f);
            hero.transform.position = Clear(hero, (Vector2)elder.transform.position + new Vector2(-1.5f, 0.5f));
            yield return new WaitForSeconds(1f);
            int told1 = UI.DialogueBroadcast.DevTold;
            for (int i = 0; i < 6 && UI.DialogueBroadcast.DevTold == told1; i++)
            {
                yield return Tap(UnityEngine.InputSystem.Key.W, 0.2f);
                for (float t = Time.realtimeSinceStartup + 1.5f; UI.DialogueBroadcast.DevTold == told1 && Time.realtimeSinceStartup < t;) yield return null;
            }
            var cellAt = World.CellGrid.WorldToCell(hero.transform.position);
            var townRegion = World.TerraWorld.RegionAt(cellAt.x, cellAt.y);
            bool holding = UI.DialogueBroadcast.Holding;
            int held0 = UI.DialogueBroadcast.DevHeldBack;
            float leftEdge = townRegion != null ? townRegion.CellRect.xMin * World.CellGrid.CellSize : hero.transform.position.x - 50f;
            hero.transform.position = new Vector3(leftEdge - 1f, hero.transform.position.y, hero.transform.position.z);
            yield return null;
            yield return null;
            float x = hero.transform.position.x;
            bool kept = holding && UI.DialogueBroadcast.DevHeldBack > held0 && x > leftEdge;
            for (int i = 0; i < 60 && UI.DialogueBroadcast.Holding; i++) yield return new WaitForSeconds(0.25f);
            yield return null;
            FxCheck("对话没播完不能离开当前房间，播完就不限制", kept && !UI.DialogueBroadcast.Holding,
                $"限制中 {holding}，推到左边界外后 x {x:F1}（边界 {leftEdge:F1}），挡回 {UI.DialogueBroadcast.DevHeldBack - held0} 次，播完后还限制 {UI.DialogueBroadcast.Holding}");
            hero.transform.position = Clear(hero, (Vector2)elder.transform.position + new Vector2(-1.5f, 0.5f));
        }

        /// <summary>
        /// Each seller as Terraria's Chest.SetupShop has it, where that townsperson lives in Dirtmouth: the dyes and the Dye
        /// Vat at the Dye Trader, not the Goblin Tinkerer; the builder accessories the Traveling Merchant's only; the
        /// sentry rods, the Old One's Army armor and the Defender's Forge at the Tavernkeep, not the Witch Doctor or Cyborg.
        /// </summary>
        void Shops17()
        {
            bool Sells(string npc, int stage, ItemType t) { Town.TownNpcs.DevStockAt(npc, stage, t, out bool has); return has; }
            FxCheck("染料、染缸在染料商，不在哥布林", Sells("DyeTrader", 1, ItemType.RedDye) && Sells("DyeTrader", 1, ItemType.DyeVat) && !Sells("GoblinTinkerer", 5, ItemType.RedDye) && !Sells("GoblinTinkerer", 5, ItemType.DyeVat),
                $"染料商 红染料 {Sells("DyeTrader", 1, ItemType.RedDye)} 染缸 {Sells("DyeTrader", 1, ItemType.DyeVat)}；哥布林 红染料 {Sells("GoblinTinkerer", 5, ItemType.RedDye)}");
            FxCheck("砌砖刀等只在旅商的货里", !Sells("GoblinTinkerer", 5, ItemType.BrickLayer) && !Sells("GoblinTinkerer", 5, ItemType.PaintSprayer), $"哥布林 砌砖刀 {Sells("GoblinTinkerer", 5, ItemType.BrickLayer)}");
            FxCheck("哨兵杖、护卫套装、护卫熔炉在酒馆老板", Sells("DD2Bartender", 3, ItemType.FlameburstCane) && Sells("DD2Bartender", 4, ItemType.SquirePlating) && Sells("DD2Bartender", 5, ItemType.MonkAltShirt) && Sells("DD2Bartender", 3, ItemType.DefendersForge)
                && !Sells("WitchDoctor", 5, ItemType.FlameburstCane) && !Sells("Cyborg", 5, ItemType.MonkAltShirt) && !Sells("Merchant", 5, ItemType.DefendersForge),
                $"酒馆 火焰手杖 {Sells("DD2Bartender", 3, ItemType.FlameburstCane)} 套装 {Sells("DD2Bartender", 4, ItemType.SquirePlating)} 熔炉 {Sells("DD2Bartender", 3, ItemType.DefendersForge)}；巫医 {Sells("WitchDoctor", 5, ItemType.FlameburstCane)}");
            int full = Town.TownNpcs.DevStockAt("DD2Bartender", 5, ItemType.Ale, out _);
            FxCheck("酒馆老板最后阶段货品不超过 40 格", full <= 40, $"{full} 件");
        }

        /// <summary>
        /// Dev probe: Elderbug talked to; the dialogue manager's boxes (DialogueBox, their FSMs) and the talk machines
        /// dumped while a page shows, and again once the page has been typed out.
        /// </summary>
        IEnumerator DialogueProbe17()
        {
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            GameObject elder = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Elderbug") { elder = t.gameObject; break; }
            if (elder == null) { FxCheck("对话探测 找老虫子", false, ""); yield break; }
            hero.transform.position = Clear(hero, (Vector2)elder.transform.position + new Vector2(-1.5f, 0.5f));
            yield return new WaitForSeconds(1.5f);
            bool open = false;
            for (int i = 0; i < 6 && !open; i++)
            {
                yield return Tap(UnityEngine.InputSystem.Key.W, 0.2f);
                yield return new WaitForSeconds(1.5f);
                open = UI.HudDialogue.DevOpen;
            }
            Plugin.Log.LogInfo($"[AutoTest] 对话探测 开了 {open}");
            foreach (var box in Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Plugin.Log.LogInfo($"[AutoTest] 对话探测 框 {PathOf(box.transform)} 对话 {box.currentConversation} 页 {box.currentPage} 启用 {box.gameObject.activeInHierarchy}");
                var root = box.transform.parent != null ? box.transform.parent : box.transform;
                DumpFsms(root);
            }
            foreach (var f in elder.GetComponents<PlayMakerFSM>()) DumpFsm(f);
            yield return new WaitForSeconds(3f);
            foreach (var box in Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var f in box.GetComponents<PlayMakerFSM>()) Plugin.Log.LogInfo($"[AutoTest] 对话探测 3 秒后 {PathOf(box.transform)} '{f.FsmName}' 状态 {f.ActiveStateName}");
            FxCheck("对话探测（看日志）", open, "");
        }

        /// <summary>Dev probe: once in the scene, the FSMs on one of its objects dumped (states, actions, transitions).</summary>
        static IEnumerator DumpWhenIn(string scene, string objectName)
        {
            float until = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < until && GameManager.instance.sceneName != scene) yield return null;
            yield return new WaitForSeconds(0.2f);
            foreach (var f in PlayMakerFSM.FsmList)
                if (f != null && f.gameObject.name == objectName && f.gameObject.scene.name == scene) DumpFsm(f);
        }

        /// <summary>
        /// Dev probe: from arriving in the scene, every state change of its FSMs and the Knight's (each frame, with the
        /// time), until the scene is left or the time is up.
        /// </summary>
        static IEnumerator FsmTrace(string scene, float seconds)
        {
            float until = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < until && GameManager.instance.sceneName != scene) yield return null;
            float t0 = Time.realtimeSinceStartup;
            var last = new Dictionary<PlayMakerFSM, string>();
            string body = null;
            while (Time.realtimeSinceStartup - t0 < seconds)
            {
                bool here = GameManager.instance.sceneName == scene;
                var hero = HeroController.SilentInstance;
                var stats = TerraMod.Instance != null ? TerraMod.Instance.Stats : null;
                string key = hero == null ? "-" : $"HK 血 {PlayerData.instance.health} 死 {hero.cState.dead} 危险 {hero.cState.hazardDeath} 泰拉生命 {(stats != null ? stats.Life : -1)} 可操作 {GameHooks.PlayerHasControl()}";
                if (key != body && hero != null) Plugin.Log.LogInfo($"[AutoTest] 状态跟踪 {Time.realtimeSinceStartup - t0:F2}s 主角 {key} 位置 {(Vector2)hero.transform.position:F1}");
                body = key;
                foreach (var f in PlayMakerFSM.FsmList)
                {
                    if (f == null) continue;
                    string sn = f.gameObject.scene.name;
                    if (sn != scene && !(sn == "DontDestroyOnLoad" && f.gameObject.name == "Knight")) continue;
                    string now = f.ActiveStateName;
                    if (last.TryGetValue(f, out var was) && was == now) continue;
                    if (was != null || here) Plugin.Log.LogInfo($"[AutoTest] 状态跟踪 {Time.realtimeSinceStartup - t0:F2}s {PathOf(f.transform)} '{f.FsmName}' {was} → {now}");
                    last[f] = now;
                }
                if (!here) { Plugin.Log.LogInfo($"[AutoTest] 状态跟踪 {Time.realtimeSinceStartup - t0:F2}s 离开 {scene}，到了 {GameManager.instance.sceneName}"); yield break; }
                yield return null;
            }
        }

        /// <summary>
        /// Dev probe: once in the scene, every PlayMaker action of the loaded scenes that names a PlayerData field
        /// (its type's name has "PlayerData" in it, or it reads a bool/int by name), with the field's value now.
        /// </summary>
        static IEnumerator PlayerDataReads(string scene)
        {
            float until = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < until && GameManager.instance.sceneName != scene) yield return null;
            yield return new WaitForSeconds(0.3f);
            var pd = PlayerData.instance;
            var seen = new HashSet<string>();
            foreach (var f in PlayMakerFSM.FsmList)
            {
                if (f == null) continue;
                foreach (var s in f.FsmStates)
                    foreach (var a in s.Actions)
                    {
                        string type = a.GetType().Name;
                        foreach (var field in a.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        {
                            object v = field.GetValue(a);
                            string name = v is HutongGames.PlayMaker.FsmString fs ? fs.Value : v as string;
                            if (string.IsNullOrEmpty(name)) continue;
                            var pf = typeof(PlayerData).GetField(name);
                            if (pf == null || (!type.Contains("PlayerData") && !field.Name.ToLower().Contains("name"))) continue;
                            string line = $"{f.gameObject.scene.name}/{PathOf(f.transform)} '{f.FsmName}' {s.Name}: {type}.{field.Name} = {name} ({pf.GetValue(pd)})";
                            if (seen.Add(line)) Plugin.Log.LogInfo($"[AutoTest] 读档探测 {line}");
                        }
                    }
            }
            Plugin.Log.LogInfo($"[AutoTest] 读档探测 {scene}：{seen.Count} 处");
        }

        /// <summary>
        /// The room's common enemies near the Knight put away for a test of his own moves: the test's Knight takes no HK
        /// damage but a touch still stuns him the Terraria way (no dash, no slash, for a while), at the entrance's crawlers.
        /// </summary>
        static void QuietAround(Vector2 at, float radius = 25f)
        {
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && hm.gameObject.activeInHierarchy && !EnemyStats.Of(hm).Boss && Vector2.Distance(hm.transform.position, at) < radius)
                    hm.gameObject.SetActive(false);
        }

        /// <summary>The shadow dash in mid-air: Hollow Knight's own, level (no fall while it lasts) and its distance (speed × time).</summary>
        IEnumerator ShadowDash17()
        {
            // In "all" the tests before leave the Knight wherever they ended (a narrow spot): the open Crossroads_01.
            if (GameManager.instance.sceneName != "Crossroads_01")
            {
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(1.5f);
                PlayerData.instance.isInvincible = true;
            }
            var hero = HeroController.instance;
            // Control back first (the test before ends on a scene change that may leave the Knight held a while).
            for (float t = Time.realtimeSinceStartup + 6f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
            if (!GameHooks.PlayerHasControl())
            {
                hero.RegainControl();
                hero.StartAnimationControl();
                Plugin.Log.LogInfo("[AutoTest] 修复1016 暗影冲刺 之前主角没有操作权，交还后继续");
            }
            QuietAround(hero.transform.position);
            Wear(ItemType.ShadeCloak);
            yield return new WaitForSeconds(0.5f);
            Items.AbilityAccessories.DevReset();
            var col = hero.GetComponent<BoxCollider2D>();
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + new Vector2(0f, 4f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            // Faced the way with more room (in "all" this runs wherever the tests before it left the Knight).
            float Room(Vector2 dir)
            {
                var hit = Physics2D.BoxCast(col.bounds.center, col.bounds.size * 0.9f, 0f, dir, 12f, LayerMask.GetMask("Terrain"));
                return hit.collider != null ? hit.distance : 12f;
            }
            // Looked at once the collider has followed the move up (the physics step after it).
            yield return new WaitForSeconds(0.15f);
            float roomRight = Room(Vector2.right), roomLeft = Room(Vector2.left);
            if (roomRight >= roomLeft) hero.FaceRight(); else hero.FaceLeft();
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 暗影冲刺 右边空 {roomRight:F1}，左边空 {roomLeft:F1}");
            float x0 = hero.transform.position.x;
            Items.AbilityAccessories.DevShadowKey = true;
            yield return null;
            yield return new WaitForFixedUpdate();
            float y0 = hero.transform.position.y;
            float worst = 0f;
            for (float t = 0f; t < Player.TerraMovement.DevHkDashTime - 0.03f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                worst = Mathf.Max(worst, Mathf.Abs(hero.transform.position.y - y0));
            }
            float went = Mathf.Abs(hero.transform.position.x - x0);
            float want = Player.TerraMovement.DevHkDashSpeed * Player.TerraMovement.DevHkDashTime;
            FxCheck("暗影冲刺是原版黑冲：空中水平直线不下落，距离约 速度×时间", worst < 0.15f && went > want * 0.7f && went < want * 1.3f,
                $"速度 {Player.TerraMovement.DevHkDashSpeed:F1}，时间 {Player.TerraMovement.DevHkDashTime:F2}，冲出 {went:F2}（应约 {want:F2}），高度最多变了 {worst:F3}");
        }

        /// <summary>
        /// Dev probe: the Knight's "Charm Effects" machines dumped; then the Grimmchild got and worn (gotCharm_40,
        /// equippedCharm_40, level 2) and the charm events sent one by one, watching for a Grimmchild to come.
        /// </summary>
        IEnumerator GrimmProbe17()
        {
            var hero = HeroController.instance;
            var fx = hero.transform.Find("Charm Effects");
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 格林 Charm Effects {(fx != null ? fx.name : "没有")}");
            if (fx != null) DumpFsms(fx);
            var pd = PlayerData.instance;
            pd.SetBool("gotCharm_40", true);
            pd.SetBool("equippedCharm_40", true);
            pd.SetInt("grimmChildLevel", 2);
            GameObject Find()
            {
                foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (go.name.StartsWith("Grimmchild")) return go.gameObject;
                return null;
            }
            foreach (var ev in new[] { "CHARM EQUIP CHECK", "CHARM INDICATOR CHECK", "HERO ENTERED", "LEVEL LOADED", "SPAWN" })
            {
                if (fx != null) foreach (var f in fx.GetComponentsInChildren<PlayMakerFSM>(true)) f.SendEvent(ev);
                yield return new WaitForSeconds(1.5f);
                var g = Find();
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 格林 发 {ev} 后：格林之子 {(g != null ? g.name + " " + (Vector2)g.transform.position : "没有")}");
                if (g != null) break;
            }
            Shot(GameManager.instance.sceneName, "fx17_grimm");
            yield return null;
            var go2 = Find();
            if (go2 != null) foreach (var f in go2.GetComponentsInChildren<PlayMakerFSM>(true)) DumpFsm(f);
            FxCheck("格林之子探测（看日志）", true, go2 != null ? "出来了" : "没出来");
        }

        /// <summary>
        /// The Potion of Return drunk in Crossroads_01: home (the world's spawn in King's Pass, no bench rested on), a gate
        /// there; a right click on the gate, back where it was drunk. Sake: Tipsy. Their recipes there.
        /// </summary>
        IEnumerator Potions17()
        {
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            string from = GameManager.instance.sceneName;
            Vector2 at = hero.transform.position;
            inv.Slots[0] = ItemStack.Of(ItemType.PotionOfReturn, 2);
            inv.Selected = 0;
            inv.Override = -1;
            float until = Time.time + 2f;
            while (!mod.DevReady && Time.time < until) yield return null;
            mod.DevUseSelected(at);
            var gm = GameManager.instance;
            until = Time.realtimeSinceStartup + 40f;
            yield return new WaitForSeconds(1f);
            while (Time.realtimeSinceStartup < until && (gm.IsInSceneTransition || gm.sceneName == from || !GameHooks.PlayerHasControl())) yield return null;
            string home = gm.sceneName;
            until = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < until && !World.Wormhole.DevPortal.HasValue) yield return null;
            var gate = World.Wormhole.DevPortal;
            int left = inv.Slots[0].IsEmpty ? 0 : inv.Slots[0].Count;
            Shot(home, "fx17_return_gate");
            yield return null;
            FxCheck("返回药水：传送回家，家里出现传送门，用掉一瓶", home != from && gate.HasValue && left == 1, $"从 {from} 到 {home}，传送门 {gate}，剩 {left}");
            if (gate.HasValue)
            {
                hero = HeroController.instance;
                hero.transform.position = Clear(hero, gate.Value + Vector2.left * 1.5f);
                yield return new WaitForSeconds(0.5f);
                bool clicked = World.Wormhole.TryRightClick(World.Wormhole.DevPortal ?? gate.Value);
                until = Time.realtimeSinceStartup + 40f;
                yield return new WaitForSeconds(1f);
                while (Time.realtimeSinceStartup < until && (gm.IsInSceneTransition || gm.sceneName != from || !GameHooks.PlayerHasControl())) yield return null;
                yield return new WaitForSeconds(0.5f);
                hero = HeroController.instance;
                float off = Vector2.Distance(hero.transform.position, at);
                FxCheck("右键传送门回到喝药的地方，传送门消失", clicked && gm.sceneName == from && off < 3f && !World.Wormhole.DevPortal.HasValue,
                    $"点了 {clicked}，回到 {gm.sceneName} 离原处 {off:F1}，{World.Wormhole.DevLast}");
            }
            // Sake: Tipsy.
            Player.PlayerBuffs.Remove(25);
            inv.Slots[0] = ItemStack.Of(ItemType.Sake, 1);
            inv.Selected = 0;
            until = Time.time + 2f;
            while (!mod.DevReady && Time.time < until) yield return null;
            mod.DevUseSelected(HeroController.instance.transform.position);
            yield return new WaitForSeconds(0.5f);
            FxCheck("清酒：醉醺醺", Player.PlayerBuffs.Has(25), $"增益 {Player.PlayerBuffs.Has(25)}");
            bool wormhole = false, ret = false;
            foreach (var r in Recipes.All) { if (r.Result == ItemType.WormholePotion) wormhole = true; if (r.Result == ItemType.PotionOfReturn) ret = true; }
            FxCheck("虫洞药水和返回药水能合成", wormhole && ret, $"虫洞 {wormhole}，返回 {ret}");
            inv.Slots[0] = default;
            inv.Infinite = infinite;
        }

        /// <summary>
        /// A boss room's battle gates shut (BG CLOSE sent) in the False Knight's room, as a LAN game's rule (DevAlone):
        /// the Knight in the arena, they stay shut; out of it, they open after three seconds.
        /// </summary>
        IEnumerator Vacancy17()
        {
            const string room = "Crossroads_10";
            yield return GoTo(room, EntryGateOf(room));
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            var gates = new List<PlayMakerFSM>();
            foreach (var f in PlayMakerFSM.FsmList) if (f != null && f.FsmName == "BG Control" && f.gameObject.scene.name.StartsWith(room)) gates.Add(f);
            var region = World.TerrainSystem.Find(room);
            var zone = region?.OwnZone;
            if (gates.Count == 0 || !zone.HasValue) { FxCheck("没人的首领房开门", false, $"门 {gates.Count}，场地 {zone.HasValue}"); yield break; }
            bool Shut() { foreach (var g in gates) { var c = g.GetComponent<Collider2D>(); if (c != null && c.enabled) return true; } return false; }
            World.BossRoomVacancy.DevAlone = true;
            var hero = HeroController.instance;
            hero.transform.position = Clear(hero, zone.Value.center);
            yield return new WaitForSeconds(0.5f);
            foreach (var g in gates) g.SendEvent("BG CLOSE");
            yield return new WaitForSeconds(4.5f);
            bool heldShut = Shut();
            hero.transform.position = Clear(hero, new Vector2(zone.Value.xMax + 6f, zone.Value.yMax + 2f));
            if (zone.Value.Contains(hero.transform.position)) hero.transform.position = Clear(hero, new Vector2(zone.Value.xMin - 6f, zone.Value.yMax + 2f));
            int opened0 = World.BossRoomVacancy.DevOpened;
            yield return new WaitForSeconds(5f);
            bool opened = !Shut() && World.BossRoomVacancy.DevOpened > opened0;
            World.BossRoomVacancy.DevAlone = false;
            FxCheck("首领房里有人时门一直关着，房里没人 3 秒后门打开", heldShut && opened,
                $"有人时关着 {heldShut}；人在 {(Vector2)hero.transform.position:F1}（场地 {zone.Value}），之后门开 {opened}，{World.BossRoomVacancy.DevLast}");
        }

        /// <summary>
        /// The player's storages: a Safe set down and right-clicked opens its own slots (not the Piggy Bank's); the Money
        /// Trough opens the Piggy Bank where the player is and it stays open as he walks; the Void Bag carried takes what a
        /// full bag can't; geo in the Safe counts for buying; the Merchant sells the Safe at its stage.
        /// </summary>
        IEnumerator Storage17()
        {
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            var saved = (ItemStack[])inv.Slots.Clone();
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            InventoryGui.Open = true;
            var col = hero.GetComponent<BoxCollider2D>();
            bool placed = false;
            for (float dx = 1.5f; dx <= 4f && !placed; dx += 0.5f)
                foreach (float dir in new[] { 1f, -1f })
                    if (World.Stations.TryPlace(World.StationKind.Safe, hero, new Vector2(col.bounds.center.x + dir * dx, col.bounds.min.y + 0.3f))) { placed = true; break; }
            var box = World.Stations.DevBoxOf(World.StationKind.Safe);
            if (placed && box.HasValue)
            {
                for (int i = 0; i < inv.Safe.Length; i++) inv.Safe[i] = default;
                inv.Safe[0] = ItemStack.Of(ItemType.Geo, 500);
                long banked = GeoWallet.Banked(inv);
                World.Stations.TryRightClick(box.Value.center);
                yield return null;
                FxCheck("保险箱右键打开自己的格子", World.Chests.OpenBankKind == 1 && World.Chests.BankTitle == ItemDb.Get(ItemType.Safe).Name && banked >= 500,
                    $"打开 {World.Chests.OpenBankKind}「{World.Chests.BankTitle}」，存的吉欧算进钱包 {banked >= 500}");
                World.Chests.Close(false);
                inv.Safe[0] = default;
                yield return null;
            }
            else FxCheck("保险箱放得下", false, World.Stations.DevWhy);
            // The Money Trough: the Piggy Bank by the player, staying open as he goes.
            inv.Slots[0] = ItemStack.Of(ItemType.MoneyTrough);
            inv.Selected = 0;
            inv.Override = -1;
            float until = Time.time + 2f;
            while (!mod.DevReady && Time.time < until) yield return null;
            mod.DevUseSelected(hero.transform.position);
            yield return new WaitForSeconds(0.3f);
            bool opened = World.Chests.OpenBankKind == 0;
            string title = World.Chests.BankTitle;
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + new Vector2(8f, 0f));
            yield return new WaitForSeconds(0.5f);
            bool stays = World.Chests.OpenBankKind == 0;
            Shot(GameManager.instance.sceneName, "fx17_moneytrough");
            yield return null;
            FxCheck("钱币槽在身边打开猪猪存钱罐，走开也不关", opened && stays, $"打开 {opened}「{title}」，走开 8 格后还开着 {stays}");
            World.Chests.Close(false);
            // The Void Bag: a full bag, wood dropped at the feet, into the vault.
            for (int i = 0; i < inv.Void.Length; i++) inv.Void[i] = default;
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = ItemStack.Of(ItemType.DirtBlock, ItemDb.Get(ItemType.DirtBlock).MaxStack);
            inv.Slots[Inventory.HotbarSize] = ItemStack.Of(ItemType.VoidBag);
            WorldItems.Drop(ItemStack.Of(ItemType.Wood, 7), hero.transform.position);
            yield return new WaitForSeconds(2.5f);
            int inVoid = 0;
            foreach (var s in inv.Void) if (!s.IsEmpty && s.Type == ItemType.Wood) inVoid += s.Count;
            FxCheck("带着虚空袋时，背包满了捡的东西进虚空保险库", inVoid == 7, $"虚空保险库里木材 {inVoid}");
            for (int i = 0; i < inv.Void.Length; i++) inv.Void[i] = default;
            System.Array.Copy(saved, inv.Slots, saved.Length);
            inv.Infinite = infinite;
            Town.TownNpcs.DevStockAt("Merchant", 2, ItemType.Safe, out bool sells);
            Town.TownNpcs.DevStockAt("Merchant", 1, ItemType.Safe, out bool early);
            FxCheck("商人在阶段 2 起卖保险箱", sells && !early, $"阶段 2 卖 {sells}，阶段 1 卖 {early}");
            InventoryGui.Open = false;
        }

        IEnumerator DieAndComeBack(System.Action<Vector2, string> done)
        {
            var gm = GameManager.instance;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = false;
            mod.DevNoFallDamage = false;
            mod.Stats.TakeDirect(999999);
            hero.TakeDamage(hero.gameObject, GlobalEnums.CollisionSide.bottom, 1, 1);
            float until = Time.time + 10f;
            while (!hero.cState.dead && Time.time < until) yield return null;
            until = Time.time + 30f;
            while ((hero.cState.dead || gm.IsInSceneTransition || !hero.acceptingInput || hero.controlReqlinquished) && Time.time < until)
            {
                yield return null;
                hero = HeroController.instance;
            }
            yield return new WaitForSeconds(1f);
            PlayerData.instance.isInvincible = true;
            done(HeroController.instance.transform.position, gm.sceneName);
        }

        /// <summary>
        /// A bed set down where the player stands (Crossroads_01 alone, Dirtmouth in "all"), right-clicked: the spawn point;
        /// a death there and back beside it; right-clicked again: off; set once more and knocked down: the point before it back.
        /// </summary>
        IEnumerator Bed17()
        {
            var pd = PlayerData.instance;
            var hero = HeroController.instance;
            string here = GameManager.instance.sceneName;
            string scene0 = pd.respawnScene, marker0 = pd.respawnMarkerName;
            int type0 = pd.respawnType;
            var col = hero.GetComponent<BoxCollider2D>();
            Vector2 aim = (Vector2)col.bounds.center + new Vector2(2.5f, 0f);
            bool placed = false;
            for (float dx = 1.5f; dx <= 4f && !placed; dx += 0.5f)
                foreach (float dir in new[] { 1f, -1f })
                {
                    aim = new Vector2(col.bounds.center.x + dir * dx, col.bounds.min.y + 0.3f);
                    if (World.Stations.TryPlace(World.StationKind.Bed, hero, aim)) { placed = true; break; }
                }
            var box = World.Stations.DevBoxOf(World.StationKind.Bed);
            FxCheck("床放得下", placed && box.HasValue, $"{World.Stations.DevWhy}");
            if (!placed || !box.HasValue) yield break;
            yield return new WaitForSeconds(0.3f);
            bool clicked = World.Stations.TryRightClick(box.Value.center);
            bool set = pd.respawnScene == here && pd.respawnMarkerName.StartsWith(World.Beds.Prefix);
            FxCheck("右键床设为重生点", clicked && set && Net.NetChat.DevHas(Terraria.TerrariaText.Get(Lang.Culture, "Game.SpawnPointSet") ?? "已设置重生点"),
                $"复活点 {pd.respawnScene} / {pd.respawnMarkerName}");
            Vector2 back = default;
            string backIn = null;
            yield return DieAndComeBack((p, s) => { back = p; backIn = s; });
            box = World.Stations.DevBoxOf(World.StationKind.Bed);
            bool near = backIn == here && box.HasValue && Mathf.Abs(back.x - box.Value.center.x) < 3f && Mathf.Abs(back.y - box.Value.yMin) < 3f;
            Shot(GameManager.instance.sceneName, "fx17_bed_back");
            yield return null;
            FxCheck("死后在床边复活", near, $"回到 {backIn} {back:F1}，床 {box?.center:F1}，{World.Beds.DevLast}");
            if (!box.HasValue) yield break;
            World.Stations.TryRightClick(box.Value.center);
            FxCheck("再右键同一张床取消重生点", !pd.respawnMarkerName.StartsWith(World.Beds.Prefix), $"复活点 {pd.respawnScene} / {pd.respawnMarkerName}");
            World.Stations.TryRightClick(box.Value.center);
            hero = HeroController.instance;
            hero.transform.position = Clear(hero, new Vector2(box.Value.center.x, box.Value.yMin + 0.8f));
            yield return new WaitForSeconds(0.5f);
            var pick = ItemDb.Get(ItemType.CopperPickaxe);
            for (int k = 0; k < 40 && World.Stations.DevBoxOf(World.StationKind.Bed).HasValue; k++)
            {
                World.Stations.TryMine(pick, box.Value.center, hero);
                yield return null;
            }
            bool gone = !World.Stations.DevBoxOf(World.StationKind.Bed).HasValue;
            if (!gone) Plugin.Log.LogInfo($"[AutoTest] 修复1016 拆床 主角 {(Vector2)hero.transform.position:F1}，床 {box.Value}，触及 {World.Stations.DevReach(hero)}");
            FxCheck("拆掉床后重生点退回原来的", gone && !pd.respawnMarkerName.StartsWith(World.Beds.Prefix), $"床拆了 {gone}，复活点 {pd.respawnScene} / {pd.respawnMarkerName}");
            pd.respawnScene = scene0;
            pd.respawnMarkerName = marker0;
            pd.respawnType = type0;
        }

        /// <summary>The full map with the Forgotten Crossroads' map bought: explored rooms beside charted ones, photographed.</summary>
        IEnumerator Map17()
        {
            var pd = PlayerData.instance;
            bool hadMap = pd.hasMap, hadCross = pd.mapCrossroads;
            pd.hasMap = true;
            pd.mapCrossroads = true;
            yield return new WaitForSeconds(1f);
            if (UI.WorldMap.Open) UI.WorldMap.Toggle();
            UI.WorldMap.Toggle();
            yield return null;
            UI.WorldMap.DevCentre(HeroController.instance.transform.position);
            UI.WorldMap.DevZoom(3f);
            yield return new WaitForSecondsRealtime(0.6f);
            Shot("Crossroads_01", "fx17_map_z3");
            yield return null;
            UI.WorldMap.DevZoom(1.2f);
            yield return new WaitForSecondsRealtime(0.6f);
            Shot("Crossroads_01", "fx17_map_z1");
            yield return null;
            FxCheck("买了遗忘十字路地图后的完整地图（看截图）", UI.WorldMap.DevChartCount("遗忘十字路") > 0, $"标出的场景 {UI.WorldMap.DevChartCount("遗忘十字路")} 个");
            UI.WorldMap.Toggle();
            pd.hasMap = hadMap;
            pd.mapCrossroads = hadCross;
        }

        /// <summary>
        /// The Goblin Tinkerer's hammer: a sword reforged until the best prefix comes up; then the next click straight away
        /// does nothing (it holds a second), one after the hold works; the name popped up over the player. His forging the same.
        /// </summary>
        IEnumerator Reforge17()
        {
            var inv = mod.Inventory;
            bool wasInfinite = inv.Infinite;
            mod.SetInfiniteItems(false);
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            var at = Town.TownNpcs.DevPos("GoblinTinkerer");
            if (!at.HasValue) { FxCheck("重铸", false, "镇上没有哥布林"); mod.SetInfiniteItems(wasInfinite); yield break; }
            hero.transform.position = Clear(hero, at.Value + Vector2.left * 0.8f);
            yield return null;
            Town.TownNpcs.Close(false);
            Town.TownNpcs.TryRightClick(Town.TownNpcs.DevPos("GoblinTinkerer").Value);
            Town.TownNpcs.OpenReforge();
            yield return null;
            inv.Mouse = default;
            InventoryGui.DevPutReforge(ItemStack.Of(ItemType.IronBroadsword));
            Shop.Earn(100000000);
            int tries = 0;
            InventoryGui.DevLastBest = false;
            while (tries < 300 && !InventoryGui.DevLastBest) { InventoryGui.DevReleaseHammer(); InventoryGui.DevReforge(); tries++; }
            string bestName = Town.TownNpcs.ReforgeItem.Def.Name;
            int prefix = Town.TownNpcs.ReforgeItem.Prefix;
            long purse = Shop.Purse;
            bool again = InventoryGui.DevReforge();
            bool keptPrefix = Town.TownNpcs.ReforgeItem.Prefix == prefix && Shop.Purse == purse;
            yield return new WaitForSecondsRealtime(0.2f);
            Shot("Town", "fx17_reforge_best");
            yield return null;
            yield return new WaitForSecondsRealtime(InventoryGui.BestHold);
            bool after = InventoryGui.DevReforge();
            FxCheck("重铸出最好的词条：锤子停一秒，这一秒里再点不扣钱、不变词条，之后又能重铸", tries < 300 && !again && keptPrefix && after,
                $"第 {tries} 次出最好的词条「{bestName}」；马上再点 {again}（词条和钱没变 {keptPrefix}），一秒后再点 {after}");
            // His forging: a forging that takes holds the hammer the same.
            Town.TownNpcs.Close(false);
            Town.TownNpcs.TryRightClick(Town.TownNpcs.DevPos("GoblinTinkerer").Value);
            Town.TownNpcs.OpenForge();
            yield return null;
            var back = Town.TownNpcs.ReforgeItem;
            if (back.IsEmpty) InventoryGui.DevPutReforge(ItemStack.Of(ItemType.IronBroadsword));
            Forging.DevOutcome = true;
            InventoryGui.DevReleaseHammer();
            int lv0 = Town.TownNpcs.ReforgeItem.Forge;
            bool forged = InventoryGui.DevForge();
            bool forgedAgain = InventoryGui.DevForge();
            int lv1 = Town.TownNpcs.ReforgeItem.Forge;
            yield return new WaitForSecondsRealtime(InventoryGui.BestHold + 0.1f);
            bool forgedLater = InventoryGui.DevForge();
            Forging.DevOutcome = null;
            FxCheck("锻造成功后锤子同样停一秒", forged && !forgedAgain && lv1 == lv0 + 1 && forgedLater, $"等级 {lv0} → {lv1} → {Town.TownNpcs.ReforgeItem.Forge}，马上再锻 {forgedAgain}，一秒后 {forgedLater}");
            Town.TownNpcs.Close(false);
            mod.SetInfiniteItems(wasInfinite);
        }

        /// <summary>
        /// An enemy shot from beyond its alert range comes for the player: one with a waking AlertRange found in a room, the
        /// Knight 14 units off at its height, a Flintlock Pistol fired at it until it is hit; then how near it comes in 5 s.
        /// </summary>
        IEnumerator Aggro17()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            inv.Slots[Inventory.Size - 2] = ItemStack.Of(ItemType.MusketBall, 200);
            foreach (var scene in new[] { "Crossroads_01", "Crossroads_07", "Crossroads_04", "Fungus1_01", "Crossroads_05" })
            {
                if (GameManager.instance.sceneName != scene)
                {
                    yield return GoTo(scene, EntryGateOf(scene));
                    yield return new WaitForSeconds(1.5f);
                }
                PlayerData.instance.isInvincible = true;
                HealthManager target = null;
                AlertRange range = null;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                    var info = EnemyStats.Of(hm);
                    if (info == null || info.Boss) continue;
                    foreach (var r in hm.GetComponentsInChildren<AlertRange>())
                    {
                        string n = r.gameObject.name.ToLowerInvariant();
                        if (n.Contains("alert") && !n.Contains("attack")) { target = hm; range = r; break; }
                    }
                    if (target != null) break;
                }
                if (target == null) { Plugin.Log.LogInfo($"[AutoTest] 修复1016 仇恨 {scene} 没有带警戒范围的敌人"); continue; }
                Vector2 at = target.transform.position;
                hero.transform.position = Clear(hero, at + new Vector2(14f, 0.5f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceLeft();
                yield return new WaitForSeconds(1.5f);
                bool sawBefore = range.IsHeroInRange;
                float d0 = Vector2.Distance(target.transform.position, hero.transform.position);
                int hp0 = target.hp, struck0 = World.EnemyAggro.DevStruck;
                // A shot from here (what it is doesn't matter: any of the player's hits counts).
                var tcol = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
                if (tcol != null) mod.Melee.HitWith(tcol, 5, 0f, 0f, -1f, hero.transform.position);
                yield return new WaitForSeconds(0.2f);
                bool hit = World.EnemyAggro.DevStruck > struck0;
                float nearest = d0;
                bool sees = false;
                for (float t = 0f; t < 5f && target != null && !target.isDead; t += Time.deltaTime)
                {
                    nearest = Mathf.Min(nearest, Vector2.Distance(target.transform.position, hero.transform.position));
                    sees |= range != null && range.IsHeroInRange;
                    yield return null;
                }
                Shot(scene, "fx17_aggro");
                yield return null;
                FxCheck("远处被打中的敌人会追过来", hit && sees && nearest < d0 - 2f,
                    $"{scene} {target?.name}：打中前看见 {sawBefore}，打中 {hit}（记仇 {World.EnemyAggro.DevStruck - struck0}），之后看见 {sees}，距离 {d0:F1} → 最近 {nearest:F1}");
                yield break;
            }
            FxCheck("远处被打中的敌人会追过来", false, "这几个房间都没找到带警戒范围的敌人");
        }

        /// <summary>
        /// The City of Tears' way down to the Waterways (Ruins1_05b's manhole, the Waterways Machine beside it) as a player
        /// goes at it: into the room with the Simple Key in the Hallownest bag, then taken into the hotbar and in hand, up
        /// to the machine, W, and the prompt's Yes with Enter one time and Space the other.
        /// </summary>
        IEnumerator Sewer17()
        {
            var pd = PlayerData.instance;
            foreach (var confirm in new[] { Key.Enter, Key.Space })
            {
                pd.SetBool("openedWaterwaysManhole", false);
                pd.simpleKeys = 1;
                HkItems.Tick(mod);
                var inv = mod.Inventory;
                // In the bag, not in hand, as the room comes in.
                for (int i = 0; i < Inventory.HotbarSize; i++)
                    if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == ItemType.HkSimpleKey) { inv.HkBag[HkItems.KeyBagSlots - 1] = inv.Slots[i]; inv.Slots[i] = default; }
                for (int k = 0; k < Inventory.HotbarSize; k++) if (inv.Slots[k].IsEmpty) { inv.Selected = k; break; }
                yield return GoTo("Ruins1_05b", EntryGateOf("Ruins1_05b"));
                pd.isInvincible = true;
                yield return new WaitForSeconds(1f);
                PlayMakerFSM machine = null, manhole = null;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f.gameObject.scene.name != "Ruins1_05b") continue;
                    if (f.gameObject.name == "Waterways Machine" && f.FsmName == "Conversation Control") machine = f;
                    if (f.FsmName == "Manhole Control") manhole = f;
                }
                if (machine == null || manhole == null) { FxCheck("下水道入口", false, $"机器 {machine != null}，井盖 {manhole != null}"); yield break; }
                // How its talk machine and the area title go for a while after the room came in.
                PlayMakerFSM npc0 = null;
                foreach (var f in machine.GetComponents<PlayMakerFSM>()) if (f.FsmName == "npc_control") npc0 = f;
                var timeline = new List<string>();
                string lastNpc = null;
                for (float t = 0f; t < 8f; t += Time.deltaTime)
                {
                    var title = AreaTitle.instance != null ? FSMUtility.GetFSM(AreaTitle.instance.gameObject) : null;
                    string now = $"{npc0?.ActiveStateName}/标题 {(AreaTitle.instance != null && AreaTitle.instance.gameObject.activeInHierarchy ? title?.ActiveStateName : "关")}";
                    if (now != lastNpc) { lastNpc = now; timeline.Add($"{t:F1}s {now}"); }
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 下水道 进房间后 {confirm}：{string.Join(" → ", timeline)}");
                // Taken in hand now.
                HkItemsDev.ToHotbar(ItemType.HkSimpleKey);
                yield return null;
                var hero = HeroController.instance;
                var mb = manhole.GetComponent<Collider2D>().bounds;
                hero.transform.position = Clear(hero, new Vector2(machine.transform.position.x + 1.2f, mb.max.y + 0.8f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1.2f);
                string held = mod.Inventory.Current.Def?.Name ?? "空";
                PlayMakerFSM npc = null;
                foreach (var f in machine.GetComponents<PlayMakerFSM>()) if (f.FsmName == "npc_control") npc = f;
                if (confirm == Key.Enter && npc != null) DumpFsm(npc);
                string before = $"站在 {(Vector2)hero.transform.position:F1} 地上 {hero.cState.onGround} 能查看 {hero.CanInspect()} npc_control {npc?.ActiveStateName}";
                for (int press = 0; press < 2 && machine.ActiveStateName == "Idle"; press++)
                {
                    for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime) { KeysDown(Key.W); yield return null; }
                    KeysDown();
                    yield return new WaitForSeconds(0.6f);
                }
                var seen = new List<string>();
                string last = null;
                float t0 = Time.realtimeSinceStartup, next = t0 + 1.2f;
                while (Time.realtimeSinceStartup - t0 < 9f && !pd.GetBool("openedWaterwaysManhole"))
                {
                    string now = machine.ActiveStateName;
                    if (now != last) { last = now; seen.Add($"{Time.realtimeSinceStartup - t0:F1}s {now}"); }
                    if (Time.realtimeSinceStartup > next) { next = Time.realtimeSinceStartup + 0.8f; mod.StartCoroutine(Tap(confirm)); }
                    yield return null;
                }
                yield return new WaitForSeconds(1.5f);
                bool opened = pd.GetBool("openedWaterwaysManhole");
                Shot("Ruins1_05b", "fx17_sewer_" + confirm);
                yield return null;
                FxCheck($"泪城下水道入口：进房间时钥匙在圣巢背包，拿到手上再去开，用 {confirm} 确认", opened && pd.simpleKeys == 0,
                    $"手里 {held}，按之前 {before}，机器 {string.Join(" → ", seen)}，井盖 {manhole.ActiveStateName}，钥匙剩 {pd.simpleKeys}");
            }
            pd.SetBool("openedWaterwaysManhole", false);
        }

        static List<string> MarkAllBeaten()
        {
            var added = new List<string>();
            foreach (var r in EnemyDrops.DevRecords)
                if (!Bestiary.Kills.ContainsKey(r)) { Bestiary.Kills[r] = 1; added.Add(r); }
            return added;
        }

        static IEnumerator FrameTimes(float seconds, List<float> into)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until; )
            {
                yield return null;
                into.Add(Time.unscaledDeltaTime * 1000f);
            }
        }

        static string Summary(List<float> ms)
        {
            if (ms.Count == 0) return "无";
            var sorted = new List<float>(ms);
            sorted.Sort();
            float sum = 0f;
            foreach (var m in ms) sum += m;
            return $"平均 {sum / ms.Count:F2} ms，95% {sorted[(int)(sorted.Count * 0.95f)]:F2} ms，{ms.Count} 帧";
        }

        static float Mean(List<float> ms)
        {
            float sum = 0f;
            foreach (var m in ms) sum += m;
            return ms.Count > 0 ? sum / ms.Count : 0f;
        }

        IEnumerator BookFps17()
        {
            var added = MarkAllBeaten();
            int vsync = QualitySettings.vSyncCount, fps = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 1000;
            InventoryGui.Open = true;
            InventoryGui.DevBestiary(false);
            yield return new WaitForSeconds(0.5f);
            var shut = new List<float>();
            yield return FrameTimes(2.5f, shut);
            InventoryGui.DevBestiary(true, 40);
            yield return new WaitForSeconds(0.5f);
            for (int i = 0; i < InventoryGui.DevBookTicks.Length; i++) InventoryGui.DevBookTicks[i] = 0;
            InventoryGui.DevBookFrames = 0;
            int rebuilds0 = InventoryGui.DevBookRebuilds, tight0 = UI.BestiaryToast.DevTightSprites;
            var open = new List<float>();
            yield return FrameTimes(2.5f, open);
            int frames = Mathf.Max(1, InventoryGui.DevBookFrames);
            double toMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency / frames;
            string parts = $"列表 {InventoryGui.DevBookTicks[0] * toMs:F2} ms，格子 {InventoryGui.DevBookTicks[1] * toMs:F2} ms，详情 {InventoryGui.DevBookTicks[2] * toMs:F2} ms（每帧，{frames} 帧）";
            double book = (InventoryGui.DevBookTicks[0] + InventoryGui.DevBookTicks[1] + InventoryGui.DevBookTicks[2]) * toMs;
            Shot("Crossroads_01", "fx17_book");
            yield return null;
            FxCheck("怪物图鉴：每帧自己画的时间", book < 3.0,
                $"{parts}；重建列表 {InventoryGui.DevBookRebuilds - rebuilds0} 次，紧密打包的头像 {UI.BestiaryToast.DevTightSprites - tight0}；只开背包 {Summary(shut)}；开着图鉴 {Summary(open)}");
            FxCheck("怪物图鉴：开着图鉴的帧时间接近只开背包", Mean(open) <= Mean(shut) * 1.3f + 2f, $"只开背包 {Summary(shut)}；开着图鉴 {Summary(open)}");
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            QualitySettings.vSyncCount = vsync;
            Application.targetFrameRate = fps;
            foreach (var r in added) Bestiary.Kills.Remove(r);
        }

        IEnumerator BookSearch17()
        {
            var added = MarkAllBeaten();
            InventoryGui.Open = true;
            InventoryGui.DevBestiary(true, 0);
            // An enemy with a drop the book shows, and that drop's name.
            string rec = null, drop = null;
            foreach (var r in EnemyDrops.DevRecords)
            {
                var k = EnemyDrops.KindOf(r);
                if (k == "non_enemy" || k == "player_shade") continue;
                foreach (var dr in EnemyDrops.Of(r))
                    if (!EnemyDrops.Locked(dr) && dr.Type != ItemType.Wood && dr.Type != ItemType.StoneBlock) { rec = r; drop = ItemDb.Get(dr.Type).Name; break; }
                if (rec != null) break;
            }
            InventoryGui.DevBookSearch = drop;
            yield return new WaitForSeconds(0.3f);
            var rows = InventoryGui.DevBookRows();
            Shot("Crossroads_01", "fx17_search_drop");
            yield return null;
            FxCheck("怪物图鉴搜索掉落物", rows.Contains(rec) && rows.Count > 0, $"搜「{drop}」找到 {rows.Count} 种，含 {Bestiary.NameOf(rec)} {rows.Contains(rec)}");
            string name = Bestiary.NameOf(rec);
            InventoryGui.DevBookSearch = name;
            yield return new WaitForSeconds(0.3f);
            rows = InventoryGui.DevBookRows();
            FxCheck("怪物图鉴搜索怪物名", rows.Contains(rec), $"搜「{name}」找到 {rows.Count} 种");
            // A drop of a stage not unlocked yet (the book shows "?"): its name finds nothing.
            int stage0 = Shop.DevStage;
            Shop.DevStage = 0;
            string lockedName = null, lockedRec = null;
            var unlockedNames = new HashSet<string>();
            foreach (var r in EnemyDrops.DevRecords)
                foreach (var dr in EnemyDrops.Of(r))
                    if (!EnemyDrops.Locked(dr)) unlockedNames.Add(ItemDb.Get(dr.Type).Name);
            foreach (var r in EnemyDrops.DevRecords)
            {
                foreach (var dr in EnemyDrops.Of(r))
                {
                    string n = ItemDb.Get(dr.Type).Name;
                    if (EnemyDrops.Locked(dr) && !unlockedNames.Contains(n)) { lockedName = n; lockedRec = r; break; }
                }
                if (lockedName != null) break;
            }
            if (lockedName != null)
            {
                InventoryGui.DevBookSearch = lockedName;
                yield return new WaitForSeconds(0.3f);
                rows = InventoryGui.DevBookRows();
                FxCheck("怪物图鉴搜索不到还没解锁的阶段的掉落物", !rows.Contains(lockedRec), $"阶段 0 搜「{lockedName}」（{Bestiary.NameOf(lockedRec)} 的，阶段锁着）找到 {rows.Count} 种");
            }
            Shop.DevStage = stage0;
            // Typing: letters into the box, not to the Knight.
            InventoryGui.DevBookSearch = "";
            InventoryGui.DevBookType(true);
            var hero = HeroController.instance;
            float x0 = hero.transform.position.x;
            var kb = Keyboard.current;
            foreach (char c in "dddd")
            {
                KeysDown(Key.D);
                InputSystem.QueueTextEvent(kb, c);
                yield return new WaitForSeconds(0.1f);
            }
            KeysDown();
            yield return new WaitForSeconds(0.3f);
            string typed = InventoryGui.DevBookSearch;
            float moved = Mathf.Abs(hero.transform.position.x - x0);
            yield return Tap(Key.Escape);
            yield return new WaitForSeconds(0.3f);
            FxCheck("在搜索框里打字不会让角色走动，按 Esc 只结束输入", typed == "dddd" && moved < 0.2f && InventoryGui.Open && InventoryGui.BestiaryOpen,
                $"输入了「{typed}」，角色挪了 {moved:F2}，背包开着 {InventoryGui.Open}，图鉴开着 {InventoryGui.BestiaryOpen}");
            InventoryGui.DevBookType(false);
            InventoryGui.DevBookSearch = "";
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            foreach (var r in added) Bestiary.Kills.Remove(r);
        }

        IEnumerator QuickBuff17()
        {
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            var kinds = new[] { ItemType.IronskinPotion, ItemType.SwiftnessPotion, ItemType.RegenerationPotion };
            for (int i = 0; i < kinds.Length; i++)
            {
                inv.Slots[30 + i] = ItemStack.Of(kinds[i], 3);
                Player.PlayerBuffs.Remove(ItemDb.Get(kinds[i]).BuffType);
            }
            yield return new WaitForSeconds(0.3f);
            yield return Tap(Key.B);
            yield return new WaitForSeconds(0.3f);
            var sb = new System.Text.StringBuilder();
            bool ok = true;
            for (int i = 0; i < kinds.Length; i++)
            {
                var d = ItemDb.Get(kinds[i]);
                bool on = Player.PlayerBuffs.Has(d.BuffType);
                int left = inv.Slots[30 + i].IsEmpty ? 0 : inv.Slots[30 + i].Count;
                ok &= on && left == 2;
                sb.Append($"{d.Name} 增益 {on} 剩 {left}；");
            }
            FxCheck("B 键快速增益：背包里的增益药水各喝一瓶", ok, sb + TerraMod.DevLastQuickUse);
            for (int i = 0; i < kinds.Length; i++) inv.Slots[30 + i] = default;
            inv.Infinite = infinite;
        }

        IEnumerator Zoom17()
        {
            Player.TerraCamera.Zoom = 2f;
            yield return new WaitForSeconds(0.3f);
            float h0 = Player.TerraCamera.DevViewHeight;
            KeysDown(Key.Minus);
            bool sawKey = false, sawHeld = false;
            for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                sawKey |= Keyboard.current.minusKey.isPressed;
                sawHeld |= Player.TerraKeys.Held(Player.TerraKeys.Action.ViewZoomOut);
            }
            KeysDown();
            yield return new WaitForSeconds(0.3f);
            float z1 = Player.TerraCamera.Zoom, h1 = Player.TerraCamera.DevViewHeight;
            Shot("Crossroads_01", "fx17_zoom_out");
            yield return null;
            FxCheck("按住 - 缩小到原版的最小缩放（看到两倍范围）", Mathf.Abs(z1 - 1f) < 0.01f && Mathf.Abs(h1 / h0 - 2f) < 0.05f,
                $"缩放 {z1:F2}，视野高 {h0:F2} → {h1:F2}，镜头缩放系数 {Player.TerraCamera.DevZoomFactor:F3}；按键 {Player.TerraKeys.Get(Player.TerraKeys.Action.ViewZoomOut)}，看过 {Player.TerraCamera.DevZoomFrames} 帧（自己的镜头 {Player.TerraCamera.DevZoomOursFrames}）、按着 {Player.TerraCamera.DevZoomKeyFrames} 帧，键盘上 - 按下 {sawKey}，按键设置认到 {sawHeld}，打字中 {InventoryGui.TypingName}");
            KeysDown(Key.Equals);
            yield return new WaitForSeconds(0.5f);
            KeysDown();
            yield return new WaitForSeconds(0.3f);
            float z2 = Player.TerraCamera.Zoom, h2 = Player.TerraCamera.DevViewHeight;
            FxCheck("按住 = 放大（每帧 0.02，半秒约 0.6）", z2 > 1.4f && z2 < 1.8f && Mathf.Abs(h2 / h0 - 2f / z2) < 0.05f, $"缩放 {z2:F2}，视野高 {h2:F2}");
            Player.TerraCamera.Zoom = 2f;
            yield return new WaitForSeconds(0.3f);
            FxCheck("缩放回到 2 是空洞骑士自己的视野", Mathf.Abs(Player.TerraCamera.DevViewHeight - h0) < 0.02f, $"视野高 {Player.TerraCamera.DevViewHeight:F2}（原来 {h0:F2}）");
        }

        static int CountOf(Inventory inv, ItemType t)
        {
            int n = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == t) n += s.Count;
            foreach (var s in inv.HkBag) if (!s.IsEmpty && s.Type == t) n += s.Count;
            foreach (var s in inv.Misc) if (!s.IsEmpty && s.Type == t) n += s.Count;
            if (!inv.Mouse.IsEmpty && inv.Mouse.Type == t) n += inv.Mouse.Count;
            return n;
        }

        IEnumerator LanternPet17()
        {
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            bool had = pd.hasLantern;
            pd.hasLantern = true;
            HkItems.Tick(mod);
            // Nothing in hand: an empty hotbar slot.
            for (int k = 0; k < Inventory.HotbarSize; k++) if (inv.Slots[k].IsEmpty) { inv.Selected = k; break; }
            int bag = -1;
            for (int i = 0; i < inv.HkBag.Length; i++) if (!inv.HkBag[i].IsEmpty && inv.HkBag[i].Type == ItemType.HkLumaflyLantern) bag = i;
            for (int i = 0; i < Inventory.HotbarSize && bag < 0; i++)
                if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == ItemType.HkLumaflyLantern) { inv.HkBag[0] = inv.Slots[i]; inv.Slots[i] = default; bag = 0; }
            if (bag < 0) { FxCheck("提灯宠物", false, "背包里没有光蝇灯笼"); pd.hasLantern = had; yield break; }
            bool fits = Inventory.FitsMisc(ItemDb.Get(ItemType.HkLumaflyLantern), Pets.LightSlot);
            var hero = HeroController.instance;
            // The brightest point within 3 units of the Knight (the pet floats by his shoulder).
            float Brightest()
            {
                float best = 0f;
                var c = (Vector2)hero.transform.position;
                for (float dx = -3f; dx <= 3f; dx += 0.5f)
                    for (float dy = -1f; dy <= 3f; dy += 0.5f)
                        best = Mathf.Max(best, World.LightMap.DevLightAt(c + new Vector2(dx, dy)));
                return best;
            }
            int dark0 = World.LightMap.Darkness;
            // Out of the light pet slot first: the darkness, then what is lit.
            World.LightMap.Darkness = 2;
            yield return new WaitForSeconds(0.5f);
            World.LightMap.Darkness = 2;
            bool asOff = pd.GetBool("hasLantern");
            float off = Brightest();
            inv.Misc[Pets.LightSlot] = inv.HkBag[bag];
            inv.HkBag[bag] = default;
            yield return new WaitForSeconds(1f);
            World.LightMap.Darkness = 2;
            yield return new WaitForSeconds(0.2f);
            bool asOn = pd.GetBool("hasLantern");
            float on = Brightest();
            int copies = CountOf(inv, ItemType.HkLumaflyLantern);
            Shot("Crossroads_01", "fx17_lantern");
            yield return null;
            FxCheck("光蝇灯笼能放进照明宠物栏", fits && !inv.Misc[Pets.LightSlot].IsEmpty, $"能放 {fits}，栏里 {inv.Misc[Pets.LightSlot].Def?.Name}");
            FxCheck("照明宠物栏里的灯笼漂浮出来，不拿在手里也算有灯笼", Pets.LanternOut && asOn && !asOff, $"宠物出来 {Pets.LanternOut}，算有灯笼 {asOff} → {asOn}，手里 {inv.Current.Def?.Name ?? "空"}");
            FxCheck("灯笼宠物在黑暗里照亮四周（泰拉最亮的光）", on > 0.95f && on > off + 0.3f, $"身边最亮处 {off:F2} → {on:F2}");
            FxCheck("放进宠物栏不会多出一个灯笼", copies == 1, $"一共 {copies} 个");
            inv.HkBag[bag] = inv.Misc[Pets.LightSlot];
            inv.Misc[Pets.LightSlot] = default;
            yield return new WaitForSeconds(0.5f);
            FxCheck("拿出宠物栏后灯笼宠物收回", !Pets.LanternOut && !pd.GetBool("hasLantern"), $"宠物出来 {Pets.LanternOut}，算有灯笼 {pd.GetBool("hasLantern")}");
            World.LightMap.Darkness = dark0;
            pd.hasLantern = had;
        }
    }
}
