using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.4, recorder E, suite "p4_v5E_ach_zh"): the achievements, on an ordinary save that follows the guidance.
    /// Dirtmouth's Target Dummy struck, first with the Terra Blade, then the Zenith: its DPS over its head climbs, and the
    /// popups "木桩测试" and "输出机器" rise at the bottom middle. The inventory opened, its bottom-right buttons hovered
    /// (the Wiki's tooltip), 成就 clicked: the achievements window, its kinds' tabs and the filter clicked, the list
    /// scrolled. Then the Gruz Mother fought with the new AI and brought down: "母爱如山" pops up, her loot and her Master
    /// relic fall, and the Voice Beyond the Door judges the win and points to the next fight. Her relic set down in her
    /// room, and a row of relics on Dirtmouth's street.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>The guidance followed, its opening already heard (a save past its start).</summary>
        void E5GuideOn()
        {
            mod.GrantedAbilities.Add("boss.guide.asked.v1");
            mod.GrantedAbilities.Add("boss.guide.on.v1");
            mod.GrantedAbilities.Add("boss.guide.opening.v1");
        }

        static readonly string[] e5GotAchievements =
        {
            "J01", "J02", "J03", "J07", "J10", "J12", "J14", "J17", "J26", "B01", "B03", "B04", "B05", "B06", "B07", "B08", "B09", "B12", "B20",
            "C01", "C02", "C13", "I01", "I02", "I08", "I12", "I14", "E06", "E07", "E08", "E10", "E15", "G01", "G02", "G05", "G08", "G10", "G13",
            "F01", "F07", "F08", "H01", "H03", "H07", "H08", "W01", "W07", "W11", "W12", "D02", "D03", "D04", "D11", "D17", "D22", "X02", "X10",
        };

        /// <summary>The weapon in hand used at a point as a held button would (its swing's repeat).</summary>
        void E5Use(ItemType weapon, Vector2 at)
        {
            int s = SlotOf(weapon);
            if (s >= 0) mod.Inventory.Selected = s;
            mod.DevHold = true;
            if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow);
        }

        bool e5DummyOnly;

        IEnumerator P4_v5E_achdummy(HeroController hero)
        {
            e5DummyOnly = true;
            yield return P4_v5E_ach(hero);
        }

        IEnumerator P4_v5E_ach(HeroController hero)
        {
            yield return E5OrdinaryKit();
            E5GuideOn();
            E5PlayerName("泰拉旅人");
            Achievements.Achievements.DevSetAll(false);
            foreach (var id in e5GotAchievements) Achievements.Achievements.DevSet(id, true);
            EnemyAiMode.Current = EnemyAiKind.New;
            yield return E5ToTown(2f);
            hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var inv = mod.Inventory;
            float zoom0 = Player.TerraCamera.Zoom;

            // ---- The Target Dummy.
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var dummy = TargetDummy.DevAt;
            E5Log($"假人在 {dummy}，成就计数 {Achievements.Achievements.Counting}，已得 {Achievements.Achievements.GotCount}");
            if (dummy != null)
            {
                float bottom = col.bounds.min.y - hero.transform.position.y;
                hero.transform.position = Clear(hero, new Vector2(dummy.Value.x - 7.5f, dummy.Value.y - bottom + 0.3f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                Player.TerraCamera.Zoom = 2f;
                inv.Selected = Mathf.Max(0, SlotOf(ItemType.TerraBlade));
                inv.Slots[9] = ItemStack.Of(ItemType.Terraprisma);
                // The fight's potions and a feast in (Wrath, Rage, Tipsy, Exquisitely Stuffed): the dummy's DPS to five figures.
                foreach (int b in new[] { 117, 115, Player.PotionBuffs.Tipsy, Player.PotionBuffs.ExquisitelyStuffed }) Player.PlayerBuffs.Add(b, 60 * 600);
                yield return new WaitForSeconds(1.5f);
                Vector2 Body()
                {
                    var hm = TargetDummy.DevHm;
                    var c = hm != null ? hm.GetComponent<Collider2D>() : null;
                    return c != null ? (Vector2)c.bounds.center : dummy.Value + new Vector2(0f, 1f);
                }
                aimInit = false;
                SmoothAim(Body() + new Vector2(-3f, 0.5f), 99f);
                E5ClearChat();
                float uiWas = UI.GameView.WantedScale;
                UI.GameView.SetUiScale(1.4f);
                yield return E5Rec("ach_dummy");
                // Up to it, then the Terra Blade until the first popup, then the Zenith.
                Player.TerraMovement.DevMove = 1;
                while (col.bounds.center.x < dummy.Value.x - 3.2f)
                {
                    SmoothAim(Body(), 6f);
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                int shown0 = Achievements.AchievementPopup.DevShown;
                float t0 = Time.time, switched = -1f, prism = -1f, second = -1f;
                while (Time.time - t0 < 22f)
                {
                    PlayerData.instance.isInvincible = true;
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    SmoothAim(Body() + new Vector2(0f, Mathf.Sin((Time.time - t0) * 2.1f) * 0.4f), 8f);
                    bool zen = switched >= 0f;
                    E5Use(prism >= 0f ? ItemType.LastPrism : zen ? ItemType.Zenith : ItemType.TerraBlade, aimNow);
                    int popped = Achievements.AchievementPopup.DevShown - shown0;
                    if (popped >= 2 && second < 0f) { second = Time.time; E5Log($"假人：第二个成就 {Achievements.AchievementPopup.DevLast}"); }
                    if (!zen && (popped >= 1 && Time.time - t0 > 3.2f || Time.time - t0 > 7f))
                    {
                        // A beat to read it, then the Terraprisma called beside the hero and the strongest sword.
                        mod.DevHold = null;
                        yield return new WaitForSeconds(0.4f);
                        inv.Selected = 9;
                        for (float w = 0f; w < 1f && (!mod.DevReady || mod.IsUsingItem); w += Time.deltaTime) yield return null;
                        mod.DevUseSelected((Vector2)col.bounds.center + new Vector2(0.8f, 1.6f));
                        yield return new WaitForSeconds(0.45f);
                        switched = Time.time;
                        E5Log($"假人：换天顶剑，已弹出 {popped}（{Achievements.AchievementPopup.DevLast}）");
                    }
                    if (second >= 0f && Time.time - second > 3.5f) break;
                    if (zen && second < 0f && Time.time - switched > 15f) break;
                    yield return null;
                }
                mod.DevHold = null;
                yield return new WaitForSeconds(1.6f);
                StopRec();
                UI.GameView.SetUiScale(uiWas);
                E5Log($"假人：弹出 {Achievements.AchievementPopup.DevShown - shown0} 个，最后 {Achievements.AchievementPopup.DevLast}，假人伤害 {TargetDummy.DevDamage}");
            }
            Player.TerraCamera.Zoom = zoom0;
            if (e5DummyOnly) { E5PlayerName(null); yield break; }

            // ---- The achievements window, from the inventory.
            yield return new WaitForSeconds(4f);
            yield return E5AchievementPanel(hero);

            // ---- A boss's first win: the popup, its relic, the Voice.
            yield return E5BossWin();

            // ---- Relics on Dirtmouth's street.
            yield return E5RelicRow();
            E5PlayerName(null);
        }

        /// <summary>The achievements window's layout (AchievementPanel.Draw), for the pointer: its panel, a tab's rect, a filter's.</summary>
        static Rect E5AchPanel()
        {
            var view = UI.GameView.Rect;
            float ui = UI.GameView.Scale;
            float w = Mathf.Min(1320f * ui, view.width - 40f * ui);
            float top = view.y + Mathf.Max(70f * ui, view.height * 0.1f);
            float bottom = view.yMax - 80f * ui;
            return new Rect(view.center.x - w / 2f, top, w, bottom - top);
        }

        /// <summary>Tab i (0 all, then the twelve kinds).</summary>
        static Rect E5AchTab(int i)
        {
            var p = E5AchPanel();
            float ui = UI.GameView.Scale, pad = 10f * ui, tabW = (p.width - 2f * pad) / 7f, tabH = 44f * ui;
            int row = i / 7, col = i % 7;
            return new Rect(p.x + pad + col * tabW, p.y + pad + row * (tabH + 6f * ui), tabW - 6f * ui, tabH);
        }

        static Rect E5AchFilter(int i)
        {
            var p = E5AchPanel();
            float ui = UI.GameView.Scale, pad = 10f * ui, tabH = 44f * ui, fw = 150f * ui;
            float y2 = p.y + pad + 2f * tabH + 6f * ui + 10f * ui;
            return new Rect(p.xMax - pad - (3 - i) * (fw + 6f * ui), y2, fw, 40f * ui);
        }

        static readonly System.Reflection.FieldInfo e5AchScroll = AccessTools.Field(typeof(Achievements.AchievementPanel), "scroll");

        /// <summary>The list scrolled smoothly by so many pixels (as the wheel would), over so many seconds.</summary>
        static IEnumerator E5AchScrollBy(float by, float seconds)
        {
            float from = (float)e5AchScroll.GetValue(null);
            int n = E5Frames(seconds);
            for (int i = 1; i <= n; i++)
            {
                e5AchScroll.SetValue(null, Mathf.Max(0f, from + by * Mathf.SmoothStep(0f, 1f, i / (float)n)));
                yield return null;
            }
        }

        IEnumerator E5AchievementPanel(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            float ui0 = UI.GameView.WantedScale;
            E5PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(2.5f, 2f)));
            yield return E5Rec("ach_panel");
            yield return E5Wait(0.4f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.7f);
            yield return E5Wait(0.6f);
            // The bottom-right buttons, top to bottom: 成就, Wiki (its tooltip), 泰拉瑞亚设置, 设置; then 成就.
            for (int k = 3; k >= 1; k--)
            {
                var r = InventoryGui.DevHitRect("Settings", k);
                if (r == null) continue;
                yield return E5Glide(r.Value.center, k == 3 ? 0.8f : 0.35f);
                yield return E5Wait(k == 2 ? 1.3f : 0.5f);
            }
            yield return E5Hit("Settings", 3, 0.45f, 0.25f);
            yield return E5Wait(0.9f);
            E5Log($"成就面板开 {Achievements.AchievementPanel.Open}");
            if (!Achievements.AchievementPanel.Open) { StopRec(); E5PointerOff(); yield break; }
            // Over two of the list, then the kinds and the filter.
            var p = E5AchPanel();
            yield return E5Glide(new Vector2(p.x + p.width * 0.28f, p.y + p.height * 0.42f), 0.6f);
            yield return E5Wait(0.6f);
            yield return E5Glide(new Vector2(p.x + p.width * 0.72f, p.y + p.height * 0.52f), 0.5f);
            yield return E5Wait(0.5f);
            yield return E5GlidePress(E5AchTab(2).center, 0.6f, 0.2f);   // 首领
            yield return E5Wait(1.2f);
            yield return E5AchScrollBy(260f, 1.2f);
            yield return E5Wait(0.6f);
            yield return E5GlidePress(E5AchFilter(1).center, 0.7f, 0.2f);   // 已获得
            yield return E5Wait(1.2f);
            yield return E5GlidePress(E5AchFilter(2).center, 0.4f, 0.2f);   // 未获得
            yield return E5Wait(1.2f);
            yield return E5GlidePress(E5AchFilter(0).center, 0.4f, 0.2f);   // 默认
            yield return E5Wait(0.5f);
            yield return E5GlidePress(E5AchTab(6).center, 0.7f, 0.2f);   // 赌场
            yield return E5Wait(1.2f);
            yield return E5GlidePress(E5AchTab(10).center, 0.6f, 0.2f);   // 花式去世
            yield return E5Wait(1.0f);
            yield return E5Glide(new Vector2(p.x + p.width * 0.5f, p.y + p.height * 0.6f), 0.5f);
            yield return E5AchScrollBy(420f, 2.0f);
            yield return E5Wait(1.0f);
            yield return E5GlidePress(E5AchTab(0).center, 0.7f, 0.2f);   // 全部
            yield return E5Wait(0.6f);
            yield return E5AchScrollBy(1400f, 2.4f);
            yield return E5Wait(1.0f);
            StopRec();
            Achievements.AchievementPanel.Hide();
            yield return null;
            InventoryGui.Open = false;
            E5PointerOff();
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// A boss's first win as a player has it: the sleeping Gruz Mother come up to and struck (the lunar gear brings her
        /// down in a moment), "母爱如山" and "闪电战" popping up, her young cleared, her loot and relic gathered, the Voice's
        /// words on the win and the way on. Then her relic set down in her room.
        /// </summary>
        IEnumerator E5BossWin()
        {
            EnemyAiMode.Current = EnemyAiKind.New;
            GameDifficulty.Current = DifficultyLevel.Master;
            yield return EnterRoom("Crossroads_04");
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            D4NoAreaTitle();
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var inv = mod.Inventory;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var all = BossesOf("BigFly", true);
            E5Log($"格鲁兹之母：{all.Count} 个，在 {(all.Count > 0 ? all[0].transform.position.ToString("F1") : "无")}");
            if (all.Count == 0) yield break;
            Vector2 bp = all[0].transform.position;
            var down = Physics2D.Raycast(bp + new Vector2(-9f, 2f), Vector2.down, 30f, terrain);
            if (down.collider != null) hero.transform.position = new Vector3(bp.x - 9f, down.point.y + 0.8f, hero.transform.position.z);
            rb.linearVelocity = Vector2.zero;
            Player.TerraCamera.Zoom = 1.6f;
            float ui0 = UI.GameView.WantedScale;
            UI.GameView.SetUiScale(1.4f);
            E5ClearChat();
            yield return new WaitForSeconds(1.2f);
            HealthManager Nearest()
            {
                HealthManager best = null;
                float bd = float.MaxValue;
                var list = BossesOf("BigFly", false);
                foreach (var hm in EnemiesOnScreen()) if (!list.Contains(hm)) list.Add(hm);
                foreach (var hm in list)
                {
                    if (hm.isDead || hm.hp <= 0) continue;
                    float d = Vector2.Distance(hm.transform.position, col.bounds.center);
                    if (d < bd) { bd = d; best = hm; }
                }
                return best;
            }
            Vector2 Centre(HealthManager hm)
            {
                var c = hm.GetComponent<Collider2D>();
                return c != null && c.enabled ? (Vector2)c.bounds.center : (Vector2)hm.transform.position;
            }
            int said0 = BossGuide.DevSaid, shown0 = Achievements.AchievementPopup.DevShown;
            aimInit = false;
            SmoothAim(bp + new Vector2(-2f, 0.5f), 99f);
            yield return E5Rec("ach_boss");
            // Up to her, weapon out.
            Player.TerraMovement.DevMove = 1;
            for (float t = 0f; t < 1.2f && col.bounds.center.x < bp.x - 5.5f; t += Time.deltaTime)
            {
                SmoothAim(bp, 5f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            float t0 = Time.time, dead = -1f, voiceDone = -1f, clearSince = -1f, young = -1f;
            Vector2 lastBoss = bp;
            int side = -1, move = 0;
            var weapon = ItemType.StarWrath;
            while (Time.time - t0 < 60f)
            {
                PlayerData.instance.isInvincible = true;
                mod.Stats.FullHeal();
                var boss = BossesOf("BigFly", false).Find(h => !h.isDead && h.hp > 0);
                if (boss != null) lastBoss = Centre(boss);
                if (boss == null && dead < 0f) { dead = Time.time; E5Log("首领：倒下"); }
                var target = Nearest();
                if (dead >= 0f && target != null && young < 0f) { young = Time.time; E5Log("首领：幼虫出来了"); }
                if (dead >= 0f && voiceDone < 0f && BossGuide.DevSaid - said0 >= 2 && !BossGuide.DevTalking) { voiceDone = Time.time; E5Log("首领：门外之声说完"); }
                if (dead >= 0f && target == null)
                {
                    if (clearSince < 0f) clearSince = Time.time;
                }
                else clearSince = -1f;
                // Done: the Voice heard out, the young gone a while (or long past).
                if (voiceDone >= 0f && young >= 0f && clearSince >= 0f && Time.time - clearSince > 1.2f) break;
                if (dead >= 0f && (Time.time - dead > 45f || voiceDone >= 0f && young < 0f && Time.time - dead > 26f)) break;
                if (target != null)
                {
                    Vector2 at = Centre(target);
                    SmoothAim(at, 9f);
                    E5Use(weapon, aimNow);
                    Vector2 c = col.bounds.center;
                    if (Physics2D.Raycast(c, Vector2.right * side, 1.6f, terrain).collider != null) side = -side;
                    float wantX = at.x + side * 5.5f;
                    var dn = Physics2D.Raycast(c, Vector2.down, 40f, terrain);
                    float ground = dn.collider != null ? dn.point.y : c.y - 6f;
                    float wantY = Mathf.Clamp(at.y + 1f, ground + 1.2f, ground + 6f);
                    float dx = wantX - c.x;
                    if (Mathf.Abs(dx) > 1.3f) move = dx > 0 ? 1 : -1;
                    else if (Mathf.Abs(dx) < 0.35f) move = 0;
                    Player.TerraMovement.DevMove = move;
                    float dy = wantY - c.y, vy = rb.linearVelocity.y;
                    bool rise = dy > 0.5f || (dy > -0.6f && vy < -1.2f);
                    if (rise && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
                    Player.TerraMovement.DevJump = rise;
                }
                else
                {
                    mod.DevHold = null;
                    Player.TerraMovement.DevJump = false;
                    // The loot walked over, nearest first (her relic among it); the Voice listened to.
                    Vector2? loot = null;
                    float bd = 9f;
                    foreach (var it in WorldItems.DevItems)
                    {
                        float d = Mathf.Abs(it.At.x - col.bounds.center.x);
                        if (d < bd && Mathf.Abs(it.At.y - col.bounds.center.y) < 6f) { bd = d; loot = it.At; }
                    }
                    if (loot != null && bd > 0.4f)
                    {
                        Player.TerraMovement.DevMove = loot.Value.x > col.bounds.center.x ? 1 : -1;
                        SmoothAim(loot.Value + new Vector2(0f, 1.2f), 4f);
                    }
                    else
                    {
                        Player.TerraMovement.DevMove = 0;
                        SmoothAim((Vector2)col.bounds.center + new Vector2(hero.cState.facingRight ? 3f : -3f, 1.5f), 3f);
                    }
                }
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            mod.DevHold = null;
            yield return new WaitForSeconds(1.0f);
            StopRec();
            UI.GameView.SetUiScale(ui0);
            E5Log($"首领：倒下后 {(dead >= 0f ? Time.time - dead : -1f):F1} 秒；弹出 {Achievements.AchievementPopup.DevShown - shown0} 个成就（{Achievements.AchievementPopup.DevLast}），门外之声 {BossGuide.DevSaid - said0} 句：{BossGuide.DevLast}；圣物 {Relics.DevDropped}，背包里 {SlotOf(ItemType.RelicBigFly)}");

            // Off the record: her body left to burst and its young struck down, so the room is quiet for her relic.
            float quiet = -1f;
            for (float until = Time.time + 30f; Time.time < until;)
            {
                PlayerData.instance.isInvincible = true;
                var y = Nearest();
                if (y != null)
                {
                    quiet = -1f;
                    SmoothAim(Centre(y), 12f);
                    E5Use(weapon, aimNow);
                }
                else
                {
                    mod.DevHold = null;
                    if (quiet < 0f) quiet = Time.time;
                    if (dead >= 0f && Time.time - dead > 24f && Time.time - quiet > 2f) break;
                }
                yield return null;
            }
            mod.DevHold = null;
            yield return new WaitForSeconds(0.8f);

            // ---- Her relic set down in her room, away from her body.
            int relic = SlotOf(ItemType.RelicBigFly);
            if (relic < 0)
            {
                relic = 9;
                inv.Slots[relic] = ItemStack.Of(ItemType.RelicBigFly);
                E5Log("圣物：没捡到，补一个");
            }
            yield return E5PlaceRelic(hero, relic, "relic_place", lastBoss);
            Player.TerraCamera.Zoom = 2f;
        }

        /// <summary>A free spot for a relic near a point, searched outward (null: none within the span).</summary>
        static Vector2? E5RelicSpot(StationKind kind, Vector2 around, float span, int dir)
        {
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            for (float d = 0f; d <= span; d += 0.5f)
                foreach (int s in new[] { dir, -dir })
                {
                    var p = around + new Vector2(s * d, 0f);
                    var g = Physics2D.Raycast(p + new Vector2(0f, 1.5f), Vector2.down, 6f, terrain);
                    if (g.collider == null) continue;
                    var spot = g.point + new Vector2(0f, 0.3f);
                    if (Stations.FindSpot(kind, null, spot, out _)) return spot;
                    if (d == 0f) break;
                }
            return null;
        }

        /// <summary>A relic from the bag put in the hotbar and set down a few steps away from a point, recorded: walked to, placed, its glow.</summary>
        IEnumerator E5PlaceRelic(HeroController hero, int slot, string clip, Vector2 avoid)
        {
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            if (slot >= Inventory.HotbarSize)
            {
                var tmp = inv.Slots[9];
                inv.Slots[9] = inv.Slots[slot];
                inv.Slots[slot] = tmp;
                slot = 9;
            }
            var kind = inv.Slots[slot].Def != null ? inv.Slots[slot].Def.PlacesStation : StationKind.None;
            var c = (Vector2)col.bounds.center;
            int dir = c.x >= avoid.x ? 1 : -1;
            var spot = E5RelicSpot(kind, new Vector2(c.x + dir * 3f, c.y), 6f, dir);
            E5Log($"圣物：{kind}，主角 {c:F1}，避开 {avoid:F1}，选 {spot}");
            if (spot == null) yield break;
            aimInit = false;
            SmoothAim(c + new Vector2(dir * 2f, 1.5f), 99f);
            yield return E5Rec(clip);
            yield return new WaitForSeconds(0.4f);
            inv.Selected = slot;
            // Beside it, facing it.
            float standX = spot.Value.x - dir * 1.9f;
            for (float t = 0f; t < 2.5f && Mathf.Abs(col.bounds.center.x - standX) > 0.2f; t += Time.deltaTime)
            {
                Player.TerraMovement.DevMove = standX > col.bounds.center.x ? 1 : -1;
                SmoothAim(spot.Value + new Vector2(0f, 0.6f), 6f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.35f);
            int placed0 = Stations.DevPlaced;
            yield return C4PlaceAt(spot.Value);
            yield return new WaitForSeconds(0.2f);
            E5Log($"圣物：放下 {Stations.DevPlaced > placed0}（{Stations.DevWhy}）");
            // Its glow a while, the pointer drifting off it.
            for (float t = 0f; t < 3.4f; t += Time.deltaTime)
            {
                SmoothAim(spot.Value + new Vector2(dir * 1.6f, 3.2f), 1.5f);
                yield return null;
            }
            StopRec();
        }

        /// <summary>Four relics set down along Dirtmouth's street where there is room, the hero walking on between them.</summary>
        IEnumerator E5RelicRow()
        {
            yield return E5ToTown(0f);
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var kinds = new[] { ItemType.RelicFalseKnight, ItemType.RelicHornet, ItemType.RelicMantisLord, ItemType.RelicHollowKnight };
            for (int i = 0; i < kinds.Length; i++) inv.Slots[5 + i] = ItemStack.Of(kinds[i]);
            var kind0 = ItemDb.Get(kinds[0]).PlacesStation;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            // Four free spots 2.4 apart along the street, the first run of them from the left.
            var street = Town.TownNpcs.DevStreet;
            float y0 = street.y;
            List<Vector2> spots = null;
            var starts = new List<float>();
            for (float x = street.x + 4f; x < street.w - 12f; x += 0.5f) starts.Add(x);
            for (float x = street.x - 14f; x > street.z + 3f; x -= 0.5f) starts.Add(x);
            foreach (float x in starts)
            {
                if (spots != null) break;
                var run = new List<Vector2>();
                for (int k = 0; k < 4; k++)
                {
                    var g = Physics2D.Raycast(new Vector2(x + k * 2.4f, y0 + 2f), Vector2.down, 6f, terrain);
                    if (g.collider == null) break;
                    var p = g.point + new Vector2(0f, 0.3f);
                    if (!Stations.FindSpot(kind0, null, p, out _)) break;
                    run.Add(p);
                }
                if (run.Count == 4) spots = run;
            }
            E5Log($"圣物一排：街道 {street}，位置 {(spots != null ? string.Join(" ", spots) : "没有")}");
            if (spots == null) yield break;
            float bottom = col.bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(spots[0].x - 4f, spots[0].y - 0.3f - bottom + 0.3f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            Player.TerraCamera.Zoom = 2f;
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(2.5f, 1f), 99f);
            yield return new WaitForSeconds(1.5f);
            yield return E5Rec("relic_row");
            yield return new WaitForSeconds(0.4f);
            int placed = 0;
            for (int i = 0; i < kinds.Length; i++)
            {
                inv.Selected = 5 + i;
                float standX = spots[i].x - 1.9f;
                for (float t = 0f; t < 2.5f && Mathf.Abs(col.bounds.center.x - standX) > 0.2f; t += Time.deltaTime)
                {
                    Player.TerraMovement.DevMove = standX > col.bounds.center.x ? 1 : -1;
                    SmoothAim(spots[i] + new Vector2(0f, 0.6f), 6f);
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                yield return new WaitForSeconds(0.25f);
                int p0 = Stations.DevPlaced;
                yield return C4PlaceAt(spots[i]);
                yield return new WaitForSeconds(0.35f);
                if (Stations.DevPlaced > p0) placed++;
                else E5Log($"圣物一排：{kinds[i]} 没放下（{Stations.DevWhy}）");
            }
            // A step past the last, then the row looked back on.
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                Player.TerraMovement.DevMove = 1;
                SmoothAim((Vector2)col.bounds.center + new Vector2(3f, 1f), 4f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            hero.FaceLeft();
            for (float t = 0f; t < 2.8f; t += Time.deltaTime)
            {
                SmoothAim(spots[1] + new Vector2(1.2f, 2.5f), 2f);
                yield return null;
            }
            StopRec();
            E5Log($"圣物一排：放下 {placed}/{kinds.Length}");
            Player.TerraCamera.Zoom = 2f;
        }
    }
}
