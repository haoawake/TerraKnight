using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "sentryhazard", "rainbowblade", "nailaimarts", "meleesight"): 10-21's fixes, each by the Target Dummy in
    /// Dirtmouth. A sentry stands through a spike's respawn; the rainbow phaseblade and phasesaber show their blades (their
    /// glowmask) in hand, on the ground and in the bag; with the 360° setting the Great Slash and the Dream Nail's slash go
    /// at the cursor; melee and the nail hit nothing behind a wall with the charm bag off too.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Fixes1021(string suite)
        {
            charmPass = charmAll = 0;
            charmTag = suite;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return CharmPlace();
            switch (suite)
            {
                case "sentryhazard": yield return SentryHazardTest(); break;
                case "rainbowblade": yield return RainbowBladeTest(); break;
                case "nailaimarts": yield return NailAimArtsTest(); break;
                case "meleesight": yield return MeleeSightTest(); break;
            }
            Player.TerraStats.DevImmortal = false;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }

        IEnumerator SentryHazardTest()
        {
            var pd = PlayerData.instance;
            var minions = Minions.Instance;
            yield return ToDummy(5f);
            InHand(ItemType.BallistaCane);
            var hero = HeroController.instance;
            yield return UseAt((Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + Vector2.right * 2f, 0.8f);
            int placed = minions.DevSentries();
            CharmCheck("放下哨兵", placed > 0, $"{placed} 个");
            CharmShot("placed");
            // Spikes: the game's hazard respawn (it says it has finished entering the scene again).
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = true;
            int same0 = Minions.DevSameRoomReady;
            yield return Hazard(2, "Charm test spikes");
            CharmCheck("碰尖刺被送回安全点后哨兵还在", minions.DevSentries() == placed && placed > 0, $"{placed} → {minions.DevSentries()}，同房间重新就位 {Minions.DevSameRoomReady - same0} 次");
            yield return Hazard(3, "Charm test acid");
            CharmCheck("碰酸水后哨兵还在", minions.DevSentries() == placed, $"{minions.DevSentries()}");
            CharmShot("after");
            pd.isInvincible = true;
            // A new room: left behind as ever.
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1f);
            CharmCheck("换房间后哨兵留在原地（照旧）", minions.DevSentries() == 0, $"{minions.DevSentries()}");
            PlayerData.instance.isInvincible = true;
        }

        IEnumerator RainbowBladeTest()
        {
            var blade = ItemDb.Get(ItemType.RainbowPhaseblade);
            var saber = ItemDb.Get(ItemType.RainbowPhasesaber);
            CharmCheck("彩虹相位剑有发光贴图（剑身）", blade.Glow == "Glow_372", $"{blade.Glow}");
            CharmCheck("彩虹相位刃有发光贴图（剑身）", saber.Glow == "Glow_373", $"{saber.Glow}");
            var c0 = ItemGlows.Tint(blade);
            yield return new WaitForSeconds(1.2f);
            var c1 = ItemGlows.Tint(blade);
            CharmCheck("剑身颜色是流动的彩虹", (c0 - c1).maxColorComponent > 0.05f || Mathf.Abs(c0.r - c1.r) + Mathf.Abs(c0.g - c1.g) + Mathf.Abs(c0.b - c1.b) > 0.05f, $"{c0} → {c1}");
            yield return ToDummy(2f);
            foreach (var t in new[] { ItemType.RainbowPhaseblade, ItemType.RainbowPhasesaber })
            {
                InHand(t);
                yield return new WaitForSeconds(0.3f);
                bool swinging = false;
                float until = Time.time + 2f;
                while (!mod.DevReady && Time.time < until) yield return null;
                mod.DevUseSelected(DummyCentre());
                for (int k = 0; k < 6; k++) { yield return null; if (mod.DevHeldGlow) swinging = true; }
                CharmShot(t == ItemType.RainbowPhaseblade ? "blade_swing" : "saber_swing");
                CharmCheck(ItemDb.Get(t).Name + "：挥砍时剑身显示", swinging, "");
                yield return new WaitForSeconds(1.2f);
                // Its blade thrown out comes back: called back, waited for.
                for (float w = Time.time + 3f; Time.time < w && TerraProjectiles.Instance.DevState(t == ItemType.RainbowPhaseblade ? ProjKind.PhaseRainbow : ProjKind.SaberRainbow) >= 0f;) yield return null;
            }
            // On the ground.
            var hero = HeroController.instance;
            mod.Inventory.Slots[0] = default;
            // Out of the pickup's reach (the player would take it at once).
            WorldItems.Drop(ItemStack.Of(ItemType.RainbowPhaseblade), (Vector2)hero.transform.position + Vector2.left * 7f + Vector2.up * 0.5f);
            yield return new WaitForSeconds(0.8f);
            bool glowOnGround = false;
            foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if (r.name == "Glow" && r.transform.parent != null && r.transform.parent.name.StartsWith("Terra World Item " + ItemType.RainbowPhaseblade) && r.gameObject.activeInHierarchy) glowOnGround = true;
            CharmCheck("掉在地上剑身也显示", glowOnGround, "");
            CharmShot("ground");
            // In the bag.
            mod.Inventory.Slots[1] = ItemStack.Of(ItemType.RainbowPhasesaber);
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.4f);
            Shot(GameManager.instance.sceneName, $"charm{charmTag}_bag");
            Plugin.Log.LogInfo($"[AutoTest] 护符图 截图 charm{charmTag}_bag 主角屏幕 300,150");
            InventoryGui.Open = false;
            mod.Inventory.Slots[1] = default;
        }

        IEnumerator NailAimArtsTest()
        {
            var pd = PlayerData.instance;
            var hero = HeroController.instance;
            bool was = NailAim.On;
            NailAim.Set(true);
            WearOnly();
            foreach (var f in new[] { "hasNailArt", "hasCyclone", "hasDashSlash", "hasUpwardSlash", "hasAllNailArts" }) pd.SetBool(f, true);
            Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem, Key.K);
            TerraMod.DevNoRealMouse = false;
            yield return ToDummy(3f);
            InHand(ItemType.KnightNail);
            Vector2 Middle() => hero.GetComponent<BoxCollider2D>().bounds.center;
            float AngleOf(Transform t)
            {
                Bounds b = default;
                bool any = false;
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                if (!any) return float.NaN;
                var d = (Vector2)b.center - Middle();
                return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            }
            Transform Great()
            {
                foreach (var t in hero.GetComponentsInChildren<Transform>(true)) if (t.name == "Great Slash") return t;
                return null;
            }
            var great = Great();
            // One Great Slash at a cursor angle (the setting on or off): where its picture shows, seen from the Knight's middle.
            IEnumerator GreatAt(float aim, bool on, System.Action<float> done)
            {
                NailAim.Set(on);
                yield return ToDummy(3f);
                NailAim.DevAim = Middle() + new Vector2(Mathf.Cos(aim * Mathf.Deg2Rad), Mathf.Sin(aim * Mathf.Deg2Rad)) * 4f;
                NailAim.DevLastArt = "";
                float until = Time.time + 1.2f;
                while (Time.time < until) { KeysDown(Key.K); yield return null; }
                KeysDown();
                float seen = float.NaN;
                for (float t = Time.time + 0.6f; Time.time < t;)
                {
                    yield return null;
                    if (great != null && great.gameObject.activeInHierarchy && float.IsNaN(seen))
                    {
                        yield return null;
                        yield return null;
                        seen = AngleOf(great);
                        // The charge's flash (SD Sharp Flash, level across the Knight) gone first.
                        for (int k = 0; k < 6 && great.gameObject.activeInHierarchy; k++) yield return null;
                        CharmShot("great_" + (on ? "on_" : "off_") + Mathf.RoundToInt(aim));
                        // Every big thing drawn now (where the Great Slash's picture really is).
                        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                            if (r.enabled && r.gameObject.activeInHierarchy && r.transform.IsChildOf(hero.transform) && r.bounds.size.x > 2.5f)
                                Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 大贴图 {PathOf(r.transform)} {r.GetType().Name} 中心 {(Vector2)r.bounds.center - Middle():F1} 大小 {(Vector2)r.bounds.size:F1} 旋转 {r.transform.eulerAngles.z:F0}");
                    }
                }
                done(seen);
                yield return new WaitForSeconds(1f);
            }
            // The picture's own lean, unturned (the setting off): what every turned one is measured from.
            float level = float.NaN;
            yield return GreatAt(90f, false, a => level = a);
            CharmCheck("设置关掉时强力斩照常水平（鼠标在上方也不转）", !float.IsNaN(level) && NailAim.DevLastArt == "", $"画面在 {level:F0}°");
            foreach (float aim in new[] { 90f, 45f, -30f })
            {
                float seen = float.NaN;
                yield return GreatAt(aim, true, a => seen = a);
                float turned = Mathf.DeltaAngle(level, seen);
                CharmCheck($"强力斩朝鼠标 {aim}°", NailAim.DevLastArt == "Great Slash" && !float.IsNaN(seen) && Mathf.Abs(Mathf.DeltaAngle(turned, aim)) < 15f,
                    $"转了 {NailAim.DevLastArtTurn:F0}°，画面从 {level:F0}° 转到 {seen:F0}°（{turned:F0}°）");
            }
            // The Dream Nail at the cursor: its drawn slash turned too.
            NailAim.Set(true);
            pd.hasDreamNail = true;
            InHand(ItemType.DreamNail);
            yield return ToDummy(3f);
            NailAim.DevAim = Middle() + Vector2.up * 4f;
            DreamNailItem.DevHold = true;
            float dreamSeen = float.NaN;
            bool dreamShot = false;
            var dn = DreamNailItem.DevFsm(hero);
            for (float t = Time.time + 2.4f; Time.time < t;)
            {
                if (Time.time > t - 1.2f) DreamNailItem.DevHold = false;
                yield return null;
                if (dn != null && dn.ActiveStateName == "Slash" && float.IsNaN(dreamSeen) && NailAim.DevDreamPicture != null)
                {
                    yield return null;
                    dreamSeen = AngleOf(NailAim.DevDreamPicture);
                    if (!dreamShot) { CharmShot("dream_up"); dreamShot = true; }
                }
            }
            DreamNailItem.DevHold = false;
            CharmCheck("梦之钉的挥砍画面也朝鼠标（上）", !float.IsNaN(dreamSeen) && Mathf.Abs(Mathf.DeltaAngle(dreamSeen, 90f)) < 40f,
                $"画面在 {dreamSeen:F0}°（{(NailAim.DevDreamPicture != null ? NailAim.DevDreamPicture.name : "没找到")}，读取的 {NailAim.DevDreamSlash}）");
            NailAim.DevAim = null;
            NailAim.Set(was);
            foreach (var f in new[] { "hasNailArt", "hasCyclone", "hasDashSlash", "hasUpwardSlash", "hasAllNailArts" }) pd.SetBool(f, false);
            mod.Inventory.Slots[0] = default;
        }

        IEnumerator MeleeSightTest()
        {
            // The charm bag off: the rule holds all the same.
            Charms.DevSetPage(false);
            yield return null;
            CharmCheck("护符背包关着", !Charms.Enabled, "");
            yield return ToDummy(1.5f);
            var dc = DummyCentre();
            var cell = World.CellGrid.WorldToCell(new Vector2(dc.x - 0.8f, dc.y));
            var placed = new List<Vector2Int>();
            for (int y = cell.y - 3; y < cell.y + 6; y++)
                if (World.TerraWorld.Get(cell.x, y) == World.CellKind.Empty) { World.TerraWorld.Set(cell.x, y, World.CellKind.Stone); placed.Add(new Vector2Int(cell.x, y)); }
            yield return new WaitForSeconds(0.3f);
            yield return ToDummy(1.5f);
            int nailHits = 0;
            yield return SwingNail(1.2f, (h, d) => nailHits = h);
            CharmCheck("骨钉隔墙打不到", nailHits == 0, $"{nailHits} 下");
            yield return ToDummy(1.5f);
            InHand(ItemType.CopperBroadsword);
            int h0 = World.TargetDummy.DevHits;
            for (int i = 0; i < 3; i++) yield return UseAt(DummyCentre(), 0.5f);
            CharmCheck("剑隔墙打不到", World.TargetDummy.DevHits == h0, $"{World.TargetDummy.DevHits - h0} 下");
            yield return ToDummy(1.5f);
            InHand(ItemType.NightsEdge);
            h0 = World.TargetDummy.DevHits;
            for (int i = 0; i < 2; i++) yield return UseAt(DummyCentre() + Vector2.up * 0.2f, 0.7f);
            CharmCheck("永夜刃的剑气弧隔墙打不到（原版要看得见）", World.TargetDummy.DevHits == h0, $"{World.TargetDummy.DevHits - h0} 下");
            foreach (var c in placed) World.TerraWorld.Set(c.x, c.y, World.CellKind.Empty);
            yield return new WaitForSeconds(0.3f);
            yield return ToDummy(1.5f);
            InHand(ItemType.CopperBroadsword);
            h0 = World.TargetDummy.DevHits;
            for (int i = 0; i < 3; i++) yield return UseAt(DummyCentre(), 0.5f);
            CharmCheck("没墙时剑照常打到", World.TargetDummy.DevHits > h0, $"{World.TargetDummy.DevHits - h0} 下");
            yield return ToDummy(1.5f);
            InHand(ItemType.NightsEdge);
            h0 = World.TargetDummy.DevHits;
            for (int i = 0; i < 2; i++) yield return UseAt(DummyCentre() + Vector2.up * 0.2f, 0.7f);
            CharmCheck("没墙时永夜刃照常打到", World.TargetDummy.DevHits > h0, $"{World.TargetDummy.DevHits - h0} 下");
            mod.Inventory.Slots[0] = default;
            Charms.DevSetPage(true);
        }
    }
}
