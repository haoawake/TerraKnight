using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "charm11"…"charm40"): the second batch of the charm bag's charms (CharmEffects*.cs), each tried as it
    /// should work and then where it could go wrong, by the Target Dummy in Dirtmouth.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CharmsTest2(int charm)
        {
            switch (charm)
            {
                case CharmEffects.MarkOfPride: yield return PrideTest(); break;
                case CharmEffects.Longnail: yield return LongnailTest(); break;
                case CharmEffects.HeavyBlow: yield return HeavyBlowTest(); break;
                case CharmEffects.UnbreakableStrength: yield return StrengthTest(); break;
                case CharmEffects.NailmastersGlory: yield return GloryTest(); break;
                case CharmEffects.QuickSlash: yield return QuickSlashTest(); break;
                case CharmEffects.GrubberflysElegy: yield return ElegyTest(); break;
                case CharmEffects.SharpShadow: yield return SharpShadowTest(); break;
                case CharmEffects.ShamanStone: yield return ShamanTest(); break;
                case CharmEffects.SoulCatcher: yield return CatcherTest(); break;
                case CharmEffects.SoulEater: yield return EaterTest(); break;
                case CharmEffects.FragileHeart: yield return HeartTest(); break;
                case CharmEffects.JonisBlessing: yield return JoniTest(); break;
                case CharmEffects.SpellTwister: yield return TwisterTest(); break;
                case CharmEffects.Kingsoul: yield return KingsoulTest(); break;
                case CharmEffects.Sprintmaster: yield return SprintTest(); break;
                case CharmEffects.Flukenest: yield return FlukenestTest(); break;
                case CharmEffects.Thorns: yield return ThornsTest(); break;
                case CharmEffects.SporeShroom: yield return SporeTest(); break;
                case CharmEffects.GlowingWomb: yield return WombTest(); break;
                case CharmEffects.ShapeOfUnn: yield return UnnTest(); break;
                case CharmEffects.Hiveblood: yield return HivebloodTest(); break;
                case CharmEffects.Dashmaster: yield return DashmasterTest(); break;
                case CharmEffects.Dreamshield: yield return DreamshieldTest(); break;
                case CharmEffects.Weaversong: yield return WeaversongTest(); break;
                case CharmEffects.Grimmchild: yield return GrimmchildTest(); break;
                case CharmEffects.FragileGreed: yield return GreedTest(); break;
                case CharmEffects.DreamWielder: yield return DreamWielderTest(); break;
                default: CharmCheck("没有这个护符的测试", false); break;
            }
        }

        // ------------------------------------------------------------------ shared

        /// <summary>Stands dx units left of the Target Dummy (right of it when negative), landed, facing it.</summary>
        IEnumerator ToDummy(float dx)
        {
            var hero = HeroController.instance;
            float wait = Time.time + 6f;
            while (World.TargetDummy.DevAt == null && Time.time < wait) yield return null;
            var at = World.TargetDummy.DevAt;
            if (!at.HasValue) { CharmCheck("找到木偶", false); yield break; }
            hero.transform.position = new Vector3(at.Value.x - dx, at.Value.y + 1.2f, hero.transform.position.z);
            if (dx > 0f) hero.FaceRight(); else hero.FaceLeft();
            yield return new WaitForSeconds(0.9f);
        }

        Vector2 DummyCentre()
        {
            var hm = World.TargetDummy.DevHm;
            var col = hm != null ? hm.GetComponent<Collider2D>() : null;
            return col != null ? (Vector2)col.bounds.center : hm != null ? (Vector2)hm.transform.position : Vector2.zero;
        }

        void InHand(ItemType type, int count = 1)
        {
            var inv = mod.Inventory;
            inv.Slots[0] = ItemStack.Of(type, count);
            inv.Selected = 0;
            inv.Override = -1;
        }

        /// <summary>The nail held swinging (no nail art: a press held swings on) at the dummy for some seconds; the dummy's hits and damage.</summary>
        IEnumerator SwingNail(float seconds, System.Action<int, int> done)
        {
            var hero = HeroController.instance;
            InHand(ItemType.KnightNail);
            PlayerData.instance.hasNailArt = false;
            NailAim.DevAim = DummyCentre();
            int h0 = World.TargetDummy.DevHits, d0 = World.TargetDummy.DevDamage, s0 = NailItem.DevSwings;
            NailItem.DevHold = true;
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
            NailItem.DevHold = false;
            yield return new WaitForSeconds(0.35f);
            NailAim.DevAim = null;
            done(World.TargetDummy.DevHits - h0, World.TargetDummy.DevDamage - d0);
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 挥骨钉 {seconds:F1} 秒：{NailItem.DevSwings - s0} 刀，木偶 {World.TargetDummy.DevHits - h0} 下 {World.TargetDummy.DevDamage - d0} 伤害");
        }

        /// <summary>A Terraria weapon used once at a point, then waited out.</summary>
        IEnumerator UseAt(Vector2 aim, float wait = 0.6f)
        {
            float until = Time.time + 2f;
            while (!mod.DevReady && Time.time < until) yield return null;
            mod.DevUseSelected(aim);
            yield return new WaitForSeconds(wait);
        }

        void EndNailTest()
        {
            NailItem.DevHold = false;
            NailAim.DevAim = null;
            KeysDown();
        }

        // ------------------------------------------------------------------ Mark of Pride, Longnail

        IEnumerator PrideTest()
        {
            var sword = ItemDb.Get(ItemType.CopperBroadsword);
            WearOnly();
            float s0 = Equipment.ItemScale(sword);
            yield return ToDummy(2.2f);
            float plain = 1f;
            yield return SwingNail(0.6f, (h, d) => { });
            plain = NailItem.DevSlashLength;
            CharmCheck("不戴时骨钉是原本的长度（原版的 1.25 倍不在）", Mathf.Abs(plain - 1f) < 0.02f, $"{plain:F2}");
            WearOnly(CharmEffects.MarkOfPride);
            yield return null;
            CharmCheck("近战武器 1.5 倍", Mathf.Abs(Equipment.ItemScale(sword) / s0 - 1.5f) < 0.01f, $"{s0:F2} → {Equipment.ItemScale(sword):F2}");
            int hits = 0;
            NailItem.DevHold = true;
            NailAim.DevAim = DummyCentre();
            InHand(ItemType.KnightNail);
            PlayerData.instance.hasNailArt = false;
            yield return new WaitForSeconds(0.12f);
            CharmShot("nail");
            yield return SwingNail(0.5f, (h, d) => hits = h);
            CharmCheck("骨钉 1.5 倍", Mathf.Abs(NailItem.DevSlashLength - 1.5f) < 0.02f, $"{NailItem.DevSlashLength:F2}");
            // How far each reaches: the farthest the dummy is still hit from.
            float[] reach = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) WearOnly(); else WearOnly(CharmEffects.MarkOfPride);
                for (float dx = 2.6f; dx <= 5.2f; dx += 0.4f)
                {
                    int hit = 0;
                    yield return ToDummy(dx);
                    yield return SwingNail(0.5f, (h, d) => hit = h);
                    if (hit > 0) reach[pass] = dx; else break;
                }
            }
            CharmCheck("骨钉打得更远", reach[1] >= reach[0] + 0.7f, $"不戴最远 {reach[0]:F1}，戴着最远 {reach[1]:F1}");
            // A sword swung: its crescent drawn.
            InHand(ItemType.CopperBroadsword);
            yield return UseAt(DummyCentre(), 0.05f);
            CharmShot("sword");
            yield return new WaitForSeconds(0.5f);
            WearOnly(CharmEffects.MarkOfPride, CharmEffects.Longnail);
            yield return null;
            CharmCheck("和修长之钉同戴 1.725 倍", Mathf.Abs(Equipment.ItemScale(sword) / s0 - 1.725f) < 0.01f, $"{Equipment.ItemScale(sword) / s0:F3}");
            WearOnly();
            yield return null;
            CharmCheck("摘下后回到原本", Mathf.Abs(Equipment.ItemScale(sword) - s0) < 0.001f, $"{Equipment.ItemScale(sword):F2}");
            EndNailTest();
        }

        IEnumerator LongnailTest()
        {
            var hero = HeroController.instance;
            yield return ToDummy(2.2f);
            // A stone wall between the player and the dummy.
            var dc = DummyCentre();
            var cell = World.CellGrid.WorldToCell(new Vector2(dc.x - 0.8f, dc.y));
            var placed = new List<Vector2Int>();
            for (int y = cell.y - 3; y < cell.y + 6; y++)
                if (World.TerraWorld.Get(cell.x, y) == World.CellKind.Empty) { World.TerraWorld.Set(cell.x, y, World.CellKind.Stone); placed.Add(new Vector2Int(cell.x, y)); }
            yield return new WaitForSeconds(0.3f);
            yield return ToDummy(1.5f);
            int stops0 = CharmEffects.DevWallStops, through = 0, blocked = 0, swordBlocked = 0;
            WearOnly();
            yield return SwingNail(1.2f, (h, d) => blocked = h);
            CharmCheck("不戴时隔墙打不到（骨钉）", blocked == 0 && CharmEffects.DevWallStops > stops0, $"{blocked} 下，挡下 {CharmEffects.DevWallStops - stops0} 次");
            // The nail's slashes on the wall pushed the player back (the game's recoil off terrain): stood there again.
            yield return ToDummy(1.5f);
            InHand(ItemType.CopperBroadsword);
            int h0 = World.TargetDummy.DevHits;
            for (int i = 0; i < 3; i++) yield return UseAt(DummyCentre(), 0.5f);
            swordBlocked = World.TargetDummy.DevHits - h0;
            CharmCheck("不戴时隔墙打不到（剑）", swordBlocked == 0, $"{swordBlocked} 下");
            WearOnly(CharmEffects.Longnail);
            int p0 = CharmEffects.DevPierces;
            NailItem.DevHold = true;
            NailAim.DevAim = DummyCentre();
            InHand(ItemType.KnightNail);
            yield return new WaitForSeconds(0.2f);
            CharmShot("through");
            yield return SwingNail(1.2f, (h, d) => through = h);
            CharmCheck("戴着隔墙也打得到（骨钉）", through > 0 && CharmEffects.DevPierces > p0, $"{through} 下，穿墙 {CharmEffects.DevPierces - p0} 次");
            yield return ToDummy(1.5f);
            InHand(ItemType.CopperBroadsword);
            h0 = World.TargetDummy.DevHits;
            var covered = new List<string>();
            MeleeHits.DebugLog = line => { if (covered.Count < 12) covered.Add(line); };
            for (int i = 0; i < 3; i++) yield return UseAt(DummyCentre(), 0.5f);
            MeleeHits.DebugLog = null;
            CharmCheck("戴着隔墙也打得到（剑）", World.TargetDummy.DevHits - h0 > 0, $"{World.TargetDummy.DevHits - h0} 下；挥砍碰到：{string.Join(" | ", covered)}");
            foreach (var c in placed) World.TerraWorld.Set(c.x, c.y, World.CellKind.Empty);
            yield return new WaitForSeconds(0.3f);
            WearOnly();
            int open = 0;
            yield return ToDummy(1.5f);
            yield return SwingNail(1f, (h, d) => open = h);
            CharmCheck("没墙时不戴也照常打到", open > 0, $"{open} 下");
            InHand(ItemType.CopperBroadsword);
            h0 = World.TargetDummy.DevHits;
            for (int i = 0; i < 3; i++) yield return UseAt(DummyCentre(), 0.5f);
            CharmCheck("没墙时剑也打得到（距离对照）", World.TargetDummy.DevHits - h0 > 0, $"{World.TargetDummy.DevHits - h0} 下");
            EndNailTest();
        }

        // ------------------------------------------------------------------ Heavy Blow

        IEnumerator HeavyBlowTest()
        {
            var pd = PlayerData.instance;
            var hero = HeroController.instance;
            foreach (var f in new[] { "hasNailArt", "hasCyclone", "hasDashSlash", "hasUpwardSlash", "hasAllNailArts" }) pd.SetBool(f, true);
            Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem, Key.K);
            TerraMod.DevNoRealMouse = false;
            InHand(ItemType.KnightNail);
            var artScales = new Dictionary<Transform, Vector3>();
            foreach (var t in hero.GetComponentsInChildren<Transform>(true))
                if (t.name == "Great Slash" || t.name == "Cyclone Slash" || t.name == "Dash Slash") artScales[t] = t.localScale;
            int[] dealt = new int[2];
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) WearOnly(); else WearOnly(CharmEffects.HeavyBlow);
                yield return ToDummy(1.3f);
                NailAim.DevAim = DummyCentre();
                int d0 = World.TargetDummy.DevDamage;
                float until = Time.time + 2.7f;
                bool shot = false;
                while (Time.time < until)
                {
                    KeysDown(Key.K);
                    hero.FaceRight();
                    if (pass == 1 && !shot && Time.time > until - 0.4f) { CharmShot("overcharged"); shot = true; }
                    yield return null;
                }
                KeysDown();
                yield return new WaitForSeconds(0.08f);
                if (pass == 1) CharmShot("release");
                yield return new WaitForSeconds(1.4f);
                dealt[pass] = World.TargetDummy.DevDamage - d0;
                CharmCheck(pass == 0 ? "平时技艺伤害倍数 1" : "蓄满的技艺伤害倍数 1.6", Mathf.Abs(CharmEffects.DevLastArtFactor - (pass == 0 ? 1f : 1.6f)) < 0.03f, $"{CharmEffects.DevLastArtFactor:F2}");
                CharmEffects.DevLastArtFactor = 1f;
                if (pass == 1)
                {
                    CharmCheck("多蓄 1.5 秒蓄满三段", CharmEffects.DevHeavyArt > 0.95f, $"{CharmEffects.DevHeavyArt:F2}");
                    CharmCheck("技艺变大 35%", Mathf.Abs(CharmEffects.DevLastArtScale - 1.35f) < 0.03f, $"{CharmEffects.DevLastArtScale:F2}");
                }
                yield return new WaitForSeconds(0.5f);
            }
            float ratio = dealt[1] / Mathf.Max(1f, dealt[0]);
            CharmCheck("强力斩打出来的伤害更高（一下有 ±15% 浮动）", ratio > 1.15f, $"平时 {dealt[0]}，蓄满 {dealt[1]}（{ratio:F2} 倍）");
            // A short charge (just ready): nothing more.
            yield return ToDummy(1.3f);
            float t1 = Time.time + 1.05f;
            while (Time.time < t1) { KeysDown(Key.K); hero.FaceRight(); yield return null; }
            KeysDown();
            yield return new WaitForSeconds(1.5f);
            CharmCheck("刚蓄满就松开没有加成", CharmEffects.DevHeavyArt < 0.1f, $"{CharmEffects.DevHeavyArt:F2}");
            // Taken off while charging: the art plain, its pieces their own size after.
            float t2 = Time.time + 2.2f;
            while (Time.time < t2) { KeysDown(Key.K); hero.FaceRight(); if (Time.time > t2 - 0.3f && Charms.Worn(CharmEffects.HeavyBlow)) WearOnly(); yield return null; }
            KeysDown();
            yield return new WaitForSeconds(1.5f);
            bool sizes = artScales.Count > 0;
            foreach (var kv in artScales) if (kv.Key != null && (kv.Key.localScale - kv.Value).sqrMagnitude > 1e-4f) sizes = false;
            CharmCheck("技艺的大小放回原样", sizes, $"{artScales.Count} 个");
            foreach (var f in new[] { "hasNailArt", "hasCyclone", "hasDashSlash", "hasUpwardSlash", "hasAllNailArts" }) pd.SetBool(f, false);
            EndNailTest();
        }

        // ------------------------------------------------------------------ Unbreakable Strength, Glory, Quick Slash, Elegy, Sharp Shadow

        IEnumerator StrengthTest()
        {
            var pd = PlayerData.instance;
            var nail = ItemDb.Get(ItemType.KnightNail);
            nail.Damage = NailItem.DamageNow();
            var sword = ItemDb.Get(ItemType.CopperBroadsword);
            WearOnly();
            yield return null;
            int n0 = Equipment.Damage(nail), s0 = Equipment.Damage(sword);
            float m0 = Equipment.Current.MeleeDamage;
            WearOnly(CharmEffects.UnbreakableStrength);
            yield return null;
            yield return null;
            int n1 = Equipment.Damage(nail), s1 = Equipment.Damage(sword);
            CharmCheck("近战 +10%", Mathf.Abs(Equipment.Current.MeleeDamage - m0 - 0.1f) < 0.001f, $"{m0:F2} → {Equipment.Current.MeleeDamage:F2}");
            CharmCheck("骨钉 +30%（加法）", Mathf.Abs(n1 - n0 * (1f + m0 + 0.3f) / (1f + m0)) <= 1.01f, $"{n0} → {n1}");
            CharmCheck("别的近战武器 +10%", Mathf.Abs(s1 - s0 * (1f + m0 + 0.1f) / (1f + m0)) <= 1.01f, $"{s0} → {s1}");
            yield return ToDummy(1.4f);
            yield return SwingNail(0.4f, (h, d) => { });
            CharmShot("embers");
            bool broken = pd.brokenCharm_25;
            pd.brokenCharm_25 = true;
            yield return null;
            yield return null;
            CharmCheck("碎了没有效果", Equipment.Damage(nail) == n0 && Mathf.Abs(Equipment.Current.MeleeDamage - m0) < 0.001f, $"{Equipment.Damage(nail)}");
            pd.brokenCharm_25 = broken;
            EndNailTest();
        }

        IEnumerator GloryTest()
        {
            var hero = HeroController.instance;
            var need = HarmonyLib.AccessTools.FieldRefAccess<HeroController, float>("nailChargeTime");
            WearOnly();
            yield return null;
            float c0 = need(hero);
            WearOnly(CharmEffects.NailmastersGlory);
            yield return null;
            yield return null;
            CharmCheck("蓄力时间快 30%", Mathf.Abs(need(hero) - NailArts.ChargeTime / 1.3f) < 0.01f, $"{c0:F2} → {need(hero):F2}");
            yield return ToDummy(1.4f);
            int a = 0, b = 0, s0 = NailItem.DevSwings;
            WearOnly();
            yield return SwingNail(3f, (h, d) => { });
            a = NailItem.DevSwings - s0;
            s0 = NailItem.DevSwings;
            WearOnly(CharmEffects.NailmastersGlory);
            yield return SwingNail(3f, (h, d) => { });
            b = NailItem.DevSwings - s0;
            float r = b / Mathf.Max(1f, a);
            CharmCheck("挥砍间隔短 30%（刀数多约 1.2~1.45 倍）", r > 1.15f && r < 1.5f, $"不戴 {a} 刀，戴着 {b} 刀（{r:F2} 倍）");
            // With Quick Slash too: the game's quicker cooldown left out (only the bag's).
            s0 = NailItem.DevSwings;
            WearOnly(CharmEffects.NailmastersGlory, CharmEffects.QuickSlash);
            yield return SwingNail(3f, (h, d) => { });
            int c = NailItem.DevSwings - s0;
            CharmCheck("原版快速劈砍的冷却不叠上来", Mathf.Abs(c - b) <= 2, $"{c} 刀（只有荣耀 {b} 刀）");
            // The charge's look.
            var pd = PlayerData.instance;
            pd.SetBool("hasNailArt", true);
            pd.SetBool("hasDashSlash", true);
            Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem, Key.K);
            InHand(ItemType.KnightNail);
            float t = Time.time + 0.5f;
            while (Time.time < t) { KeysDown(Key.K); yield return null; }
            CharmShot("charging");
            KeysDown();
            pd.SetBool("hasNailArt", false);
            pd.SetBool("hasDashSlash", false);
            yield return new WaitForSeconds(0.8f);
            WearOnly();
            yield return null;
            yield return null;
            CharmCheck("摘下后蓄力时间回到原本", Mathf.Abs(need(hero) - NailArts.ChargeTime) < 0.01f, $"{need(hero):F2}");
            EndNailTest();
        }

        IEnumerator QuickSlashTest()
        {
            yield return ToDummy(1.4f);
            int h0 = 0, d0 = 0, h1 = 0, d1 = 0;
            WearOnly();
            yield return SwingNail(2.5f, (h, d) => { h0 = h; d0 = d; });
            int q0 = CharmEffects.DevQuickHits;
            CharmEffects.DevQuickDealt = CharmEffects.DevQuickBase = 0;
            WearOnly(CharmEffects.QuickSlash);
            yield return SwingNail(0.25f, (h, d) => { });
            CharmShot("cross");
            yield return SwingNail(2.5f, (h, d) => { h1 = h; d1 = d; });
            int extra = CharmEffects.DevQuickHits - q0;
            CharmCheck("每次命中再劈一刀", extra > 0 && Mathf.Abs(h1 - 2 * (h1 - extra)) <= 2, $"追加 {extra} 刀，木偶 {h1} 下");
            float ratio = CharmEffects.DevQuickDealt / Mathf.Max(1f, CharmEffects.DevQuickBase);
            CharmCheck("追加一刀是 30%，必定暴击（两倍）", ratio > 1.6f && ratio < 2.4f, $"追加的基础 {CharmEffects.DevQuickBase}，实际 {CharmEffects.DevQuickDealt}（{ratio:F2} 倍）");
            EndNailTest();
        }

        IEnumerator ElegyTest()
        {
            var hero = HeroController.instance;
            WearOnly(CharmEffects.GrubberflysElegy);
            yield return ToDummy(7f);
            int b0 = CharmEffects.DevBeams, bh0 = CharmEffects.DevBeamHits, hits = 0;
            NailItem.DevHold = true;
            NailAim.DevAim = DummyCentre();
            InHand(ItemType.KnightNail);
            yield return new WaitForSeconds(0.16f);
            CharmShot("beam");
            yield return SwingNail(1.5f, (h, d) => hits = h);
            CharmCheck("每次挥砍射出剑气", CharmEffects.DevBeams - b0 >= 3, $"{CharmEffects.DevBeams - b0} 道");
            CharmCheck("剑气打到远处的木偶", CharmEffects.DevBeamHits > bh0 && hits > 0, $"{CharmEffects.DevBeamHits - bh0} 下");
            int vanilla = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Grubberfly Beam") && t.gameObject.activeInHierarchy) vanilla++;
            CharmCheck("没有原版的光束", vanilla == 0, $"{vanilla} 个");
            // Damage: half the nail's.
            yield return ToDummy(7f);
            int bd0 = World.TargetDummy.DevDamage, bhits0 = CharmEffects.DevBeamHits;
            yield return SwingNail(1.2f, (h, d) => { });
            float per = (World.TargetDummy.DevDamage - bd0) / Mathf.Max(1f, CharmEffects.DevBeamHits - bhits0);
            var nail = ItemDb.Get(ItemType.KnightNail);
            nail.Damage = NailItem.DamageNow();
            CharmCheck("剑气约一半伤害", per > Equipment.Damage(nail) * 0.3f && per < Equipment.Damage(nail) * 0.8f, $"每下 {per:F1}，骨钉 {Equipment.Damage(nail)}");
            // A wall stops it.
            var dc = DummyCentre();
            var cell = World.CellGrid.WorldToCell(new Vector2(dc.x - 3f, dc.y));
            var placed = new List<Vector2Int>();
            for (int y = cell.y - 3; y < cell.y + 6; y++)
                if (World.TerraWorld.Get(cell.x, y) == World.CellKind.Empty) { World.TerraWorld.Set(cell.x, y, World.CellKind.Stone); placed.Add(new Vector2Int(cell.x, y)); }
            yield return new WaitForSeconds(0.3f);
            int wh = CharmEffects.DevBeamHits;
            yield return SwingNail(1.2f, (h, d) => { });
            CharmCheck("剑气被墙挡住", CharmEffects.DevBeamHits == wh, $"{CharmEffects.DevBeamHits - wh} 下");
            foreach (var c in placed) World.TerraWorld.Set(c.x, c.y, World.CellKind.Empty);
            WearOnly();
            int nb = CharmEffects.DevBeams;
            yield return SwingNail(0.6f, (h, d) => { });
            CharmCheck("摘下后不再射", CharmEffects.DevBeams == nb, "");
            EndNailTest();
        }

        IEnumerator SharpShadowTest()
        {
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            bool had = pd.hasShadowDash;
            pd.hasShadowDash = true;
            pd.hasDash = true;
            Wear(ItemType.ShadeCloak);
            yield return new WaitForSeconds(0.5f);
            int[] dealt = new int[2];
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) WearOnly(); else WearOnly(CharmEffects.SharpShadow);
                yield return ToDummy(2.5f);
                int d0 = World.TargetDummy.DevDamage, s0 = CharmEffects.DevSharpHits;
                AbilityAccessories.DevReset();
                hero.FaceRight();
                AbilityAccessories.DevShadowKey = true;
                yield return new WaitForSeconds(0.15f);
                if (pass == 1) CharmShot("cut");
                yield return new WaitForSeconds(0.6f);
                dealt[pass] = World.TargetDummy.DevDamage - d0;
                Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 黑冲 第 {pass} 次：木偶 {dealt[pass]}，速度 {Player.TerraMovement.DevHkDashSpeed:F1}");
                if (pass == 1) CharmCheck("穿过的敌人留下暗影斩", CharmEffects.DevSharpHits > s0, "");
                yield return new WaitForSeconds(1.5f);
            }
            CharmCheck("黑冲伤害比平时高", dealt[1] > dealt[0] * 1.5f, $"平时 {dealt[0]}，戴着 {dealt[1]}");
            CharmCheck("冲得更快（原版锋利之影的速度）", Mathf.Abs(Player.TerraMovement.DevHkDashSpeed - hero.DASH_SPEED_SHARP) < 0.1f, $"{Player.TerraMovement.DevHkDashSpeed:F1}（原版 {hero.DASH_SPEED_SHARP:F1}）");
            pd.hasShadowDash = had;
        }

        // ------------------------------------------------------------------ the class charms

        IEnumerator ShamanTest()
        {
            WearOnly();
            yield return null;
            float m0 = Equipment.Current.MagicDamage;
            WearOnly(CharmEffects.ShamanStone);
            yield return null;
            yield return null;
            CharmCheck("魔法伤害 +15%", Mathf.Abs(Equipment.Current.MagicDamage - m0 - 0.15f) < 0.001f, $"{m0:F2} → {Equipment.Current.MagicDamage:F2}");
            yield return ToDummy(5f);
            mod.Stats.Mana = mod.Stats.ManaMax;
            InHand(ItemType.AmethystStaff);
            yield return UseAt(DummyCentre(), 0.08f);
            CharmShot("bolt");
            CharmCheck("魔法弹幕变大 30%", Mathf.Abs(TerraProjectiles.DevLastGrow - 1.3f) < 0.01f && TerraProjectiles.DevLastClass == DamageClass.Magic, $"{TerraProjectiles.DevLastGrow:F2}");
            yield return new WaitForSeconds(0.6f);
            InHand(ItemType.WoodenBow);
            mod.Inventory.Slots[1] = ItemStack.Of(ItemType.WoodenArrow, 50);
            yield return UseAt(DummyCentre());
            CharmCheck("远程弹幕不变", Mathf.Abs(TerraProjectiles.DevLastGrow - 1f) < 0.01f, $"{TerraProjectiles.DevLastGrow:F2}");
            mod.Inventory.Slots[1] = default;
        }

        IEnumerator CatcherTest()
        {
            var st = mod.Stats;
            Player.TerraStats.DevImmortal = true;
            WearOnly(CharmEffects.SoulCatcher);
            yield return ToDummy(1.5f);
            CharmCheck("吉欧 +20%", CharmEffects.ScaleGeo(100) == 120, $"{CharmEffects.ScaleGeo(100)}");
            st.Life = st.LifeMax / 2;
            int stolen0 = CharmEffects.DevStolen;
            var col = World.TargetDummy.DevHm.GetComponent<Collider2D>();
            // 10 melee hits of 200: 3% is 60, at most 4 a second.
            for (int i = 0; i < 10; i++)
            {
                ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Melee, TrueMelee = true };
                mod.Melee.HitWith(col, 200, 0f, 0f, 1f, (Vector2)col.bounds.center + Vector2.left);
            }
            // Observe actual payments each frame. A WaitForSeconds(2) can cross the third
            // payment at t=2, so its inclusive total is not a rolling one-second limit.
            var payments = new List<(float At, int Amount)>();
            float observeAt = Time.time;
            int observed = CharmEffects.DevStolen, peak = 0, firstBurst = -1;
            while (Time.time - observeAt < 2.1f)
            {
                yield return null;
                int paid = CharmEffects.DevStolen - observed;
                observed = CharmEffects.DevStolen;
                if (paid > 0) payments.Add((Time.time, paid));
                payments.RemoveAll(p => Time.time - p.At >= 0.999f);
                int rolling = 0; foreach (var p in payments) rolling += p.Amount;
                peak = Mathf.Max(peak, rolling);
                if (firstBurst < 0 && Time.time - observeAt >= 0.3f) firstBurst = observed - stolen0;
            }
            CharmShot("wisp");
            CharmCheck("首次爆发吸血也不超过每秒4点", firstBurst >= 0 && firstBurst <= 4, $"0.3 秒吸到 {firstBurst}");
            int got = CharmEffects.DevStolen - stolen0;
            CharmCheck("近战命中吸血，每秒最多 4 点", got >= 4 && peak <= 4, $"滚动一秒峰值 {peak}，2.1 秒总共 {got}");
            // A minion's hits steal nothing; magic neither.
            WearOnly(); yield return null;
            WearOnly(CharmEffects.SoulCatcher); yield return null;
            stolen0 = CharmEffects.DevStolen;
            for (int i = 0; i < 10; i++) mod.Melee.HitWith(col, 200, 0f, 0f, 1f, (Vector2)col.bounds.center + Vector2.left, autonomous: true);
            for (int i = 0; i < 10; i++) { ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Magic }; mod.Melee.HitWith(col, 200, 0f, 0f, 1f, (Vector2)col.bounds.center + Vector2.left); }
            yield return new WaitForSeconds(2f);
            CharmCheck("召唤物和魔法命中不吸血", CharmEffects.DevStolen == stolen0, $"{CharmEffects.DevStolen - stolen0}");
            // Full life: nothing banked for later.
            st.Life = st.LifeMax;
            for (int i = 0; i < 10; i++) { ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Ranged }; mod.Melee.HitWith(col, 400, 0f, 0f, 1f, (Vector2)col.bounds.center + Vector2.left); }
            yield return new WaitForSeconds(0.3f);
            st.Life = st.LifeMax - 50;
            stolen0 = CharmEffects.DevStolen;
            yield return new WaitForSeconds(1f);
            CharmCheck("满血时不攒着", CharmEffects.DevStolen == stolen0, $"{CharmEffects.DevStolen - stolen0}");
            WearOnly(CharmEffects.SoulCatcher, CharmEffects.SpellTwister);
            st.Life = st.LifeMax / 2;
            int orb0 = CharmEffects.DevOrbs, orbHit0 = CharmEffects.DevOrbHits;
            stolen0 = CharmEffects.DevStolen;
            for (int i = 0; i < 10; i++)
            {
                ArmorEffects.Context = new ArmorEffects.Hit {Class = DamageClass.Ranged};
                mod.Melee.HitWith(col, 200, 0f, 0f, 1f, (Vector2)col.bounds.center + Vector2.left);
            }
            yield return new WaitForSeconds(1.2f);
            CharmCheck("捕手加扭曲者：法球命中且不递归生成", CharmEffects.DevOrbs == orb0 + 1 && CharmEffects.DevOrbHits > orbHit0, $"orbs={CharmEffects.DevOrbs - orb0} hits={CharmEffects.DevOrbHits - orbHit0}");
            CharmCheck("捕手加扭曲者：吸血仍受每秒上限限制", CharmEffects.DevStolen > stolen0 && CharmEffects.DevStolen - stolen0 <= 8, "heal=" + (CharmEffects.DevStolen - stolen0));
            Player.TerraStats.DevImmortal = false;
        }

        IEnumerator EaterTest()
        {
            WearOnly(CharmEffects.SoulEater);
            yield return ToDummy(3f);
            CharmEffects.DevEaterMode = 1;
            var whip = ItemDb.Get(ItemType.LeatherWhip);
            int w1 = Equipment.Damage(whip);
            CharmCheck("吉欧 +30%", CharmEffects.ScaleGeo(100) == 130, $"{CharmEffects.ScaleGeo(100)}");
            CharmCheck("鞭子模式：召唤物 ×0.6", Mathf.Abs(CharmEffects.MinionScale - 0.6f) < 0.001f, $"{CharmEffects.MinionScale}");
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.SoulEater);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmShot("minionmode");
            int w2 = Equipment.Damage(whip);
            CharmCheck("切到召唤物模式：召唤物 ×1.4", CharmEffects.DevEaterMode == 2 && Mathf.Abs(CharmEffects.MinionScale - 1.4f) < 0.001f, $"模式 {CharmEffects.DevEaterMode}");
            CharmCheck("鞭子模式的鞭子是两倍", w1 == w2 * 2 || Mathf.Abs(w1 - w2 * 2) <= 1, $"鞭子模式 {w1}，召唤物模式 {w2}");
            CharmCheck("模式显示在增益栏", Player.PlayerBuffs.Has(CharmSkills.ActiveBase + CharmEffects.SoulEater), "");
            // No weapon in the minion mode; a summoning staff and a pickaxe still go.
            InHand(ItemType.CopperBroadsword);
            int locked0 = CharmEffects.DevLocked, h0 = World.TargetDummy.DevHits;
            yield return UseAt(DummyCentre());
            CharmCheck("召唤物模式下剑挥不出来", CharmEffects.DevLocked > locked0 && World.TargetDummy.DevHits == h0, $"拦下 {CharmEffects.DevLocked - locked0}");
            int sw0 = NailItem.DevSwings;
            yield return SwingNail(0.6f, (h, d) => { });
            CharmCheck("召唤物模式下骨钉也挥不出来", NailItem.DevSwings == sw0, $"{NailItem.DevSwings - sw0} 刀");
            CharmCheck("召唤杖不算武器", !CharmEffects.WeaponLocked(ItemDb.Get(ItemType.SlimeStaff)), "");
            // In its cooldown the key does nothing; past it, back.
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("冷却中切不回去", CharmEffects.DevEaterMode == 2, "");
            yield return new WaitForSeconds(3.1f);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("3 秒后能切回鞭子模式", CharmEffects.DevEaterMode == 1, "");
            WearOnly();
            yield return null;
            yield return null;
            CharmCheck("摘下后召唤物伤害不变、增益消失", Mathf.Abs(CharmEffects.MinionScale - 1f) < 0.001f && !Player.PlayerBuffs.Has(CharmSkills.ActiveBase + CharmEffects.SoulEater), "");
        }

        IEnumerator HeartTest()
        {
            var pd = PlayerData.instance;
            var st = mod.Stats;
            int keep = pd.maxHealthBase;
            pd.maxHealthBase = 9;
            WearOnly();
            yield return null;
            yield return null;
            int mask0 = st.MaskLife, max0 = st.LifeMax, hk0 = pd.maxHealth;
            WearOnly(CharmEffects.FragileHeart);
            yield return null;
            yield return null;
            CharmCheck("青色心变为 3 倍", st.MaskLife == mask0 * 3 && st.LifeMax == max0 + mask0 * 2, $"面具生命 {mask0} → {st.MaskLife}，上限 {max0} → {st.LifeMax}");
            CharmCheck("原版 +2 面具不在", pd.maxHealth == pd.maxHealthBase, $"面具 {pd.maxHealth}/{pd.maxHealthBase}");
            HudShot("hud");
            pd.brokenCharm_23 = true;
            yield return null;
            yield return null;
            CharmCheck("碎了没有效果", st.MaskLife == mask0, $"{st.MaskLife}");
            pd.brokenCharm_23 = false;
            WearOnly();
            pd.maxHealthBase = keep;
            yield return null;
        }

        IEnumerator JoniTest()
        {
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            float[] speed = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) WearOnly(); else WearOnly(CharmEffects.JonisBlessing);
                yield return ToDummy(-1f);
                hero.transform.position += Vector3.left * 1f;
                Player.TerraMovement.DevMove = -1;
                float top = 0f, until = Time.time + 1.2f;
                while (Time.time < until) { top = Mathf.Max(top, Mathf.Abs(rb.linearVelocity.x)); yield return null; }
                Player.TerraMovement.DevMove = 0;
                speed[pass] = top;
                if (pass == 1) CharmShot("glow");
                yield return new WaitForSeconds(0.4f);
            }
            CharmCheck("移动速度减半", Mathf.Abs(speed[1] / Mathf.Max(0.01f, speed[0]) - 0.5f) < 0.06f, $"{speed[0]:F2} → {speed[1]:F2}");
            WearOnly();
            yield return null;
            var b0 = Equipment.Current;
            WearOnly(CharmEffects.JonisBlessing);
            yield return null;
            yield return null;
            var b1 = Equipment.Current;
            CharmCheck("四职业 +35%", Mathf.Abs(b1.MeleeDamage - b0.MeleeDamage - 0.35f) < 0.001f && Mathf.Abs(b1.RangedDamage - b0.RangedDamage - 0.35f) < 0.001f
                && Mathf.Abs(b1.MagicDamage - b0.MagicDamage - 0.35f) < 0.001f && Mathf.Abs(b1.MinionDamage - b0.MinionDamage - 0.35f) < 0.001f, "");
            var pd = PlayerData.instance;
            CharmCheck("原版乔尼的单面具不在", pd.maxHealth == pd.maxHealthBase && pd.joniHealthBlue == 0, $"面具 {pd.maxHealth}，蓝血 {pd.joniHealthBlue}");
            WearOnly();
        }

        IEnumerator TwisterTest()
        {
            var staff = ItemDb.Get(ItemType.AmethystStaff);
            WearOnly();
            yield return null;
            int mana0 = Equipment.ManaCost(staff);
            float use0 = Equipment.UseScale(staff);
            WearOnly(CharmEffects.SpellTwister);
            yield return null;
            yield return null;
            CharmCheck("魔力消耗 −10%", Equipment.ManaCost(staff) == Mathf.Max(1, Mathf.FloorToInt(staff.Mana * (1f - Mathf.Clamp01(Equipment.Current.ManaCost)) * 0.9f)), $"{mana0} → {Equipment.ManaCost(staff)}");
            CharmCheck("施法快 30%", Mathf.Abs(Equipment.UseScale(staff) - use0 / 1.3f) < 0.001f, $"{use0:F2} → {Equipment.UseScale(staff):F2}");
            CharmCheck("弹药 33% 不消耗", Equipment.Current.AmmoSave >= 0.33f - 0.001f, $"{Equipment.Current.AmmoSave:F2}");
            yield return ToDummy(3f);
            var col = World.TargetDummy.DevHm.GetComponent<Collider2D>();
            int o0 = CharmEffects.DevOrbs, oh0 = CharmEffects.DevOrbHits;
            for (int i = 0; i < 6; i++)
            {
                ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Ranged, FromProjectile = true };
                mod.Melee.HitWith(col, 40, 0f, 0f, 1f, (Vector2)col.bounds.center + Vector2.left);
                yield return new WaitForSeconds(0.1f);
            }
            CharmShot("orbs");
            yield return new WaitForSeconds(1.8f);
            CharmCheck("远程命中飞出法球，每秒最多 4 颗", CharmEffects.DevOrbs - o0 >= 2 && CharmEffects.DevOrbs - o0 <= 4, $"{CharmEffects.DevOrbs - o0} 颗");
            CharmCheck("法球打中敌人", CharmEffects.DevOrbHits > oh0, $"{CharmEffects.DevOrbHits - oh0} 下");
            // The orbs are magic: they make no orbs of their own.
            int o1 = CharmEffects.DevOrbs;
            yield return new WaitForSeconds(1f);
            CharmCheck("法球不会再生法球", CharmEffects.DevOrbs == o1, "");
        }

        IEnumerator KingsoulTest()
        {
            var staff = ItemDb.Get(ItemType.AmethystStaff);
            var slime = ItemDb.Get(ItemType.SlimeStaff);
            WearOnly();
            yield return null;
            int m0 = Equipment.ManaCost(staff), s0 = Equipment.ManaCost(slime);
            var pd = PlayerData.instance;
            int royal = pd.royalCharmState;
            pd.royalCharmState = 3;
            WearOnly(CharmEffects.Kingsoul);
            yield return null;
            int m1 = Equipment.ManaCost(staff), s1 = Equipment.ManaCost(slime);
            CharmCheck("魔法武器魔力减半", Mathf.Abs(m1 - m0 / 2f) <= 1f, $"{m0} → {m1}");
            CharmCheck("召唤杖的魔力不变", s1 == s0, $"{s0} → {s1}");
            yield return ToDummy(3f);
            mod.Stats.Mana = mod.Stats.ManaMax;
            InHand(ItemType.AmethystStaff);
            yield return UseAt(DummyCentre(), 0.1f);
            CharmShot("motes");
            pd.royalCharmState = 4;
            yield return null;
            CharmCheck("虚空之心一样减半", Equipment.ManaCost(staff) == m1, $"{Equipment.ManaCost(staff)}");
            pd.royalCharmState = royal;
            WearOnly();
        }

        IEnumerator SprintTest()
        {
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var inv = mod.Inventory;
            var keep = inv.Accessories[4];
            float Reach(float[] trace, float v) { for (int i = 0; i < trace.Length; i++) if (trace[i] >= v) return i / 60f; return 9f; }
            float[] at = new float[2];
            float top = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                inv.Accessories[4] = ItemStack.Of(ItemType.HermesBoots);
                if (pass == 0) WearOnly(); else WearOnly(CharmEffects.Sprintmaster);
                yield return ToDummy(-1f);
                hero.transform.position += Vector3.left * 1f;
                yield return new WaitForSeconds(0.3f);
                top = Player.TerraMovement.AccRunSpeed * Player.TerraMovement.PxT;
                Player.TerraMovement.DevMove = -1;
                float t0 = Time.time, reach = 9f;
                bool shot = false;
                while (Time.time - t0 < 3.5f)
                {
                    if (Mathf.Abs(rb.linearVelocity.x) >= top * 0.95f && reach > 8f) reach = Time.time - t0;
                    if (pass == 1 && !shot && Time.time - t0 > 0.5f) { CharmShot("streaks"); shot = true; }
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                at[pass] = reach;
                yield return new WaitForSeconds(0.4f);
            }
            CharmCheck("穿跑鞋直接最高速", at[1] < 0.3f && at[0] > 1f, $"不戴 {at[0]:F2} 秒到最高速，戴着 {at[1]:F2} 秒（最高 {top:F1}）");
            inv.Accessories[4] = default;
            yield return null;
            yield return ToDummy(-1f);
            hero.transform.position += Vector3.left * 1f;
            Player.TerraMovement.DevMove = -1;
            float best = 0f, until = Time.time + 1.5f;
            while (Time.time < until) { best = Mathf.Max(best, Mathf.Abs(rb.linearVelocity.x)); yield return null; }
            Player.TerraMovement.DevMove = 0;
            CharmCheck("没穿跑鞋没有效果", best < 3.2f * Player.TerraMovement.PxT, $"最高 {best:F1}");
            inv.Accessories[4] = keep;
            WearOnly();
        }

        // ------------------------------------------------------------------ the creatures

        IEnumerator FlukenestTest()
        {
            var st = mod.Stats;
            Player.TerraStats.DevImmortal = true;
            WearOnly(CharmEffects.Flukenest);
            yield return ToDummy(4f);
            st.Life = st.LifeMax / 2;
            int f0 = CharmEffects.DevFlukes, h0 = CharmEffects.DevFlukeHits, heal0 = CharmEffects.DevFlukeHealed;
            yield return new WaitForSeconds(0.35f);
            CharmShot("flukes");
            yield return new WaitForSeconds(0.4f);
            CharmShot("heal");
            yield return new WaitForSeconds(3.6f);
            int flukes = CharmEffects.DevFlukes - f0;
            CharmCheck("范围里有敌人时每 2 秒 3 只", flukes >= 6 && flukes <= 9, $"4.4 秒放出 {flukes} 只");
            CharmCheck("吸虫打中敌人", CharmEffects.DevFlukeHits > h0, $"{CharmEffects.DevFlukeHits - h0} 下");
            int healed = CharmEffects.DevFlukeHealed - heal0;
            CharmCheck("每只命中回 1% 生命", healed > 0 && healed <= (CharmEffects.DevFlukeHits - h0) * Mathf.Max(1, Mathf.RoundToInt(st.LifeMax * 0.01f)), $"回了 {healed}");
            // Out of reach: none come.
            yield return ToDummy(9f);
            yield return new WaitForSeconds(0.5f);
            f0 = CharmEffects.DevFlukes;
            yield return new WaitForSeconds(2.5f);
            CharmShot("range");
            CharmCheck("范围外没有敌人不放", CharmEffects.DevFlukes == f0, $"{CharmEffects.DevFlukes - f0}");
            Player.TerraStats.DevImmortal = false;
        }

        IEnumerator ThornsTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = true;
            WearOnly(CharmEffects.Thorns);
            yield return ToDummy(1.6f);
            int b0 = CharmEffects.DevThornBursts, h0 = CharmEffects.DevThornHits, d0 = World.TargetDummy.DevDamage;
            HitFromRight(10);
            yield return new WaitForSeconds(0.08f);
            CharmShot("burst");
            yield return new WaitForSeconds(0.5f);
            CharmCheck("被打时荆棘爆开", CharmEffects.DevThornBursts == b0 + 1, "");
            CharmCheck("打到身边的敌人", CharmEffects.DevThornHits > h0 && World.TargetDummy.DevDamage > d0, $"{World.TargetDummy.DevDamage - d0}");
            bool control = GameHooks.PlayerHasControl();
            CharmCheck("不像原版那样定住主角", control, "");
            // Far off: no hit; a spike's harm: no thorns.
            yield return new WaitForSeconds(2f);
            yield return ToDummy(6f);
            h0 = CharmEffects.DevThornHits;
            HitFromRight(10);
            yield return new WaitForSeconds(0.5f);
            CharmCheck("远处的敌人打不到", CharmEffects.DevThornHits == h0, "");
            int vanilla = 0;
            var fx = HeroController.instance.transform.Find("Charm Effects/Thorn Hit");
            if (fx != null && fx.gameObject.activeSelf) vanilla++;
            CharmCheck("原版荆棘不出来", vanilla == 0, "");
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = false;
        }

        IEnumerator SporeTest()
        {
            var pd = PlayerData.instance;
            var st = mod.Stats;
            pd.isInvincible = false;
            WearOnly(CharmEffects.SporeShroom);
            CharmCheck("护符技已收藏", CharmSkills.Chosen == CharmEffects.SporeShroom, "");
            yield return ToDummy(1.2f);
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.SporeShroom);
            int hits0 = CharmEffects.DevSporeHits;
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("遁入孢子：看不见", CharmEffects.HidePlayer && CharmEffects.Untouchable, "");
            yield return new WaitForSeconds(0.3f);
            CharmShot("cloud");
            int life = st.Life;
            HitFromRight(30);
            yield return new WaitForSeconds(0.3f);
            CharmCheck("免疫所有伤害", st.Life == life, $"{life} → {st.Life}");
            InHand(ItemType.CopperBroadsword);
            int locked0 = CharmEffects.DevLocked;
            yield return UseAt(DummyCentre(), 0.2f);
            CharmCheck("雾里不能攻击", CharmEffects.DevLocked > locked0, "");
            yield return new WaitForSeconds(1.3f);
            CharmCheck("雾里的敌人受到毒伤", CharmEffects.DevSporeHits - hits0 >= 4, $"{CharmEffects.DevSporeHits - hits0} 下");
            yield return new WaitForSeconds(0.4f);
            CharmCheck("2 秒后出来", !CharmEffects.HidePlayer && !CharmEffects.Untouchable, "");
            CharmShot("after");
            yield return new WaitForSeconds(1.5f);
            life = st.Life;
            HitFromRight(30);
            yield return new WaitForSeconds(0.3f);
            CharmCheck("出来后照常受伤", st.Life < life, $"{life} → {st.Life}");
            CharmCheck("冷却 30 秒", CharmSkills.Cooling(CharmEffects.SporeShroom), "");
            // Taken off in the middle: seen again at once.
            yield return new WaitForSeconds(1.5f);
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.SporeShroom);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            WearOnly();
            yield return null;
            yield return null;
            CharmCheck("中途摘下立刻现身", !CharmEffects.HidePlayer, "");
            pd.isInvincible = true;
        }

        IEnumerator WombTest()
        {
            var st = mod.Stats;
            WearOnly(CharmEffects.GlowingWomb);
            yield return ToDummy(5f);
            st.Mana = st.ManaMax;
            int h0 = CharmEffects.DevHatched, b0 = CharmEffects.DevHatchBursts, d0 = World.TargetDummy.DevDamage;
            int mana0 = st.Mana;
            yield return new WaitForSeconds(0.6f);
            CharmShot("hatch");
            yield return new WaitForSeconds(0.5f);
            CharmShot("dart");
            yield return new WaitForSeconds(7.5f);
            CharmCheck("战斗中每 4 秒孵一只", CharmEffects.DevHatched - h0 >= 2 && CharmEffects.DevHatched - h0 <= 3, $"{CharmEffects.DevHatched - h0} 只");
            CharmCheck("幼崽扑向敌人自爆", CharmEffects.DevHatchBursts > b0 && World.TargetDummy.DevDamage > d0, $"炸了 {CharmEffects.DevHatchBursts - b0} 次，{World.TargetDummy.DevDamage - d0} 伤害");
            int vanilla = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Knight Hatchling") && t.gameObject.activeInHierarchy) vanilla++;
            CharmCheck("没有原版的幼崽", vanilla == 0, $"{vanilla}");
            // No mana: none.
            h0 = CharmEffects.DevHatched;
            for (float until = Time.time + 4.5f; Time.time < until;) { st.Mana = 5; yield return null; }
            CharmCheck("魔力不够不孵", CharmEffects.DevHatched == h0, "");
            // No enemy near: none.
            st.Mana = st.ManaMax;
            yield return ToDummy(30f);
            h0 = CharmEffects.DevHatched;
            yield return new WaitForSeconds(4.5f);
            CharmCheck("附近没有敌人不孵", CharmEffects.DevHatched == h0, "");
        }

        IEnumerator UnnTest()
        {
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var pd = PlayerData.instance;
            var st = mod.Stats;
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = true;
            WearOnly(CharmEffects.ShapeOfUnn);
            yield return ToDummy(4f);
            float g0 = rb.gravityScale, y0 = hero.transform.position.y;
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.ShapeOfUnn);
            CharmSkills.DevPress = true;
            yield return null;
            yield return null;
            CharmCheck("变成乌恩的光芒", CharmEffects.HidePlayer, "");
            CharmEffects.DevUnnKeys = new Vector2(0f, 1f);
            yield return new WaitForSeconds(1f);
            CharmShot("orb");
            float rose = hero.transform.position.y - y0;
            CharmEffects.DevUnnKeys = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            float y1 = hero.transform.position.y;
            yield return new WaitForSeconds(0.5f);
            CharmCheck("按 W 往上飞", rose > 4f, $"{rose:F2}");
            CharmCheck("松开就停住，不往下掉", Mathf.Abs(hero.transform.position.y - y1) < 0.2f, $"{hero.transform.position.y - y1:F2}");
            int life = st.Life;
            HitFromRight(20);
            yield return new WaitForSeconds(0.3f);
            CharmCheck("照常受伤", st.Life < life, $"{life} → {st.Life}");
            CharmEffects.DevUnnKeys = new Vector2(1f, 0f);
            yield return new WaitForSeconds(0.6f);
            CharmEffects.DevUnnKeys = null;
            yield return new WaitForSeconds(2.6f);
            CharmCheck("5 秒后结束，重力回来", !CharmEffects.HidePlayer && Mathf.Abs(rb.gravityScale - g0) < 0.001f, $"重力 {g0} → {rb.gravityScale}");
            yield return new WaitForSeconds(1f);
            CharmCheck("结束后落回地面", hero.cState.onGround, "");
            // Calm on a mount: a never-struck dummy's touch doesn't hurt; struck, it does.
            var dummy = World.TargetDummy.DevHm;
            bool calmRiding;
            CharmEffects.DevRiding = true;
            calmRiding = CharmEffects.CalmHit(dummy.gameObject);
            CharmEffects.DevRiding = false;
            CharmCheck("骑着坐骑时，没被打过的敌人碰到不伤人", calmRiding || EnemyStats.Of(dummy).LastHit > -50f, $"木偶上次被打 {EnemyStats.Of(dummy).LastHit:F1}");
            CharmCheck("不骑坐骑没有这个效果", !CharmEffects.CalmHit(dummy.gameObject), "");
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = false;
        }

        IEnumerator HivebloodTest()
        {
            var pd = PlayerData.instance;
            var st = mod.Stats;
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = true;
            WearOnly(CharmEffects.Hiveblood);
            yield return ToDummy(5f);
            st.Life = st.LifeMax;
            Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + CharmEffects.Hiveblood);
            st.PotionSickness = 0f;
            HitFromRight(30);
            yield return new WaitForSeconds(0.4f);
            CharmCheck("受伤后下一瓶翻倍待命", CharmEffects.DevHiveArmed, "");
            CharmShot("armed");
            st.Life = Mathf.Max(1, st.LifeMax - 200);
            int before = st.Life;
            InHand(ItemType.LesserHealingPotion, 3);
            yield return UseAt(DummyCentre(), 0.3f);
            CharmCheck("小治疗药水回 100（翻倍）", st.Life - before == Mathf.Min(100, st.LifeMax - before), $"{before} → {st.Life}");
            CharmCheck("之后冷却 60 秒", CharmSkills.Cooling(CharmEffects.Hiveblood) && !CharmEffects.DevHiveArmed, "");
            st.PotionSickness = 0f;
            HitFromRight(30);
            yield return new WaitForSeconds(0.4f);
            CharmCheck("冷却中受伤不翻倍", !CharmEffects.DevHiveArmed, "");
            // Three seconds unhurt: the honey.
            yield return new WaitForSeconds(3.3f);
            CharmCheck("3 秒没受伤获得蜂蜜", CharmEffects.HiveHoney && Player.PlayerBuffs.Has(CharmSkills.ActiveBase + CharmEffects.Hiveblood), "");
            CharmShot("honey");
            HitFromRight(30);
            yield return new WaitForSeconds(0.3f);
            CharmCheck("受伤后蜂蜜没了", !CharmEffects.HiveHoney && !Player.PlayerBuffs.Has(CharmSkills.ActiveBase + CharmEffects.Hiveblood), "");
            mod.Inventory.Slots[0] = default;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = false;
        }
    
        // ------------------------------------------------------------------ Dashmaster

        IEnumerator DashKeyOnce()
        {
            AbilityAccessories.DevDashKey = false;
            yield return null;
            AbilityAccessories.DevDashKey = true;
            yield return null;
            yield return null;
            AbilityAccessories.DevDashKey = false;
            yield return null;
            AbilityAccessories.DevDashKey = null;
        }

        IEnumerator DashmasterTest()
        {
            var hero = HeroController.instance;
            Wear(ItemType.ShieldOfCthulhu);
            yield return new WaitForSeconds(0.4f);
            CharmCheck("克苏鲁之盾", Player.TerraMovement.DashKind == 2, $"{Player.TerraMovement.DashKind}");
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) WearOnly(); else WearOnly(CharmEffects.Dashmaster);
                yield return ToDummy(-6f);
                yield return new WaitForSeconds(0.6f);
                float y0 = hero.transform.position.y;
                CharmEffects.DevDashAim = (Vector2)hero.transform.position + new Vector2(0.3f, 6f);
                int a0 = CharmEffects.DevAimedDashes;
                yield return DashKeyOnce();
                yield return new WaitForSeconds(0.12f);
                if (pass == 1) CharmShot("up");
                yield return new WaitForSeconds(0.15f);
                float rose = hero.transform.position.y - y0;
                if (pass == 0) CharmCheck("不戴时冲刺只往水平方向", rose < 0.6f, $"升高 {rose:F2}");
                else CharmCheck("戴着时冲刺键朝鼠标（上）冲", rose > 2.5f && CharmEffects.DevAimedDashes > a0, $"升高 {rose:F2}");
                yield return new WaitForSeconds(1.2f);
            }
            // Down and to the right from the air: down and right both.
            hero.transform.position += Vector3.up * 5f;
            yield return new WaitForSeconds(0.05f);
            var start = (Vector2)hero.transform.position;
            CharmEffects.DevDashAim = start + new Vector2(4f, -4f);
            yield return DashKeyOnce();
            yield return new WaitForSeconds(0.25f);
            var moved = (Vector2)hero.transform.position - start;
            CharmCheck("斜下方也行", moved.x > 1.5f && moved.y < -1.5f, $"移动 {moved:F2}");
            yield return new WaitForSeconds(1.5f);
            // Level: the Shield's own dash, as ever.
            yield return ToDummy(-6f);
            CharmEffects.DevDashAim = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + new Vector2(-5f, 0.1f);
            int a1 = CharmEffects.DevAimedDashes;
            float xl = hero.transform.position.x;
            yield return DashKeyOnce();
            yield return new WaitForSeconds(0.3f);
            CharmCheck("鼠标在水平方向时照常是克盾的冲刺", CharmEffects.DevAimedDashes == a1 && hero.transform.position.x < xl - 1.5f, $"移动 {hero.transform.position.x - xl:F2}");
            // The shadow dash toward the cursor (up), its burst turned.
            var pd = PlayerData.instance;
            pd.hasShadowDash = true;
            Wear(ItemType.ShieldOfCthulhu, ItemType.ShadeCloak);
            yield return new WaitForSeconds(1.5f);
            AbilityAccessories.DevReset();
            float ys = hero.transform.position.y;
            CharmEffects.DevDashAim = (Vector2)hero.transform.position + new Vector2(0f, 6f);
            AbilityAccessories.DevShadowKey = true;
            yield return new WaitForSeconds(0.1f);
            CharmShot("shadowup");
            yield return new WaitForSeconds(0.15f);
            CharmCheck("黑冲也朝鼠标（上）冲", hero.transform.position.y - ys > 2f, $"升高 {hero.transform.position.y - ys:F2}");
            yield return new WaitForSeconds(1.2f);
            var rb = hero.GetComponent<Rigidbody2D>();
            CharmCheck("冲完重力回来", rb.gravityScale > 0.1f, $"{rb.gravityScale:F2}");
            CharmEffects.DevDashAim = null;
            Wear();
            WearOnly();
        }

        // ------------------------------------------------------------------ the game's companions

        IEnumerator DreamshieldTest()
        {
            WearOnly(CharmEffects.Dreamshield);
            // The shields go round about 2.8 units off: the dummy stood on their way.
            yield return ToDummy(2.7f);
            yield return new WaitForSeconds(2f);
            GameObject orbit = null;
            try { orbit = GameObject.FindGameObjectWithTag("Orbit Shield"); } catch (UnityException) { }
            var a = orbit != null ? orbit.transform.Find("Shield") : null;
            var b = orbit != null ? orbit.transform.Find("Shield 2") : null;
            CharmCheck("梦之盾出来了", orbit != null && a != null, "");
            CharmCheck("两面盾", b != null && b.gameObject.activeInHierarchy, "");
            if (a != null && b != null)
            {
                var c = (Vector2)orbit.transform.position;
                var da = (Vector2)a.position - c;
                var db = (Vector2)b.position - c;
                CharmCheck("两面盾在两侧（相对）", Vector2.Dot(da.normalized, db.normalized) < -0.9f, $"{da:F2} / {db:F2}");
            }
            CharmShot("two");
            int h0 = CharmEffects.DevShieldHits, d0 = World.TargetDummy.DevDamage;
            yield return new WaitForSeconds(5f);
            CharmShot("hit");
            var nail = ItemDb.Get(ItemType.KnightNail);
            nail.Damage = NailItem.DamageNow();
            CharmCheck("盾撞到敌人造成泰拉伤害", CharmEffects.DevShieldHits > h0 && World.TargetDummy.DevDamage > d0, $"{CharmEffects.DevShieldHits - h0} 下，{World.TargetDummy.DevDamage - d0} 伤害");
            CharmCheck("一下约骨钉的 80%", Mathf.Abs(CharmEffects.DevLastCompanionDamage - Equipment.Damage(nail) * 0.8f) <= 1.5f, $"{CharmEffects.DevLastCompanionDamage}，骨钉 {Equipment.Damage(nail)}");
            WearOnly();
            yield return new WaitForSeconds(2f);
            bool gone = true;
            try { var o = GameObject.FindGameObjectWithTag("Orbit Shield"); gone = o == null || !o.activeInHierarchy; } catch (UnityException) { }
            CharmCheck("摘下后盾消失", gone, "");
        }

        IEnumerator WeaversongTest()
        {
            WearOnly(CharmEffects.Weaversong);
            yield return ToDummy(2f);
            yield return new WaitForSeconds(1.5f);
            int h0 = CharmEffects.DevWeaverHits, d0 = World.TargetDummy.DevDamage;
            yield return new WaitForSeconds(6f);
            CharmShot("weavers");
            CharmCheck("编织者咬到敌人造成泰拉伤害", CharmEffects.DevWeaverHits > h0 && World.TargetDummy.DevDamage > d0, $"{CharmEffects.DevWeaverHits - h0} 下");
            int plain = CharmEffects.DevLastCompanionDamage;
            // A whip's tag on the dummy: the bites take it.
            SummonTags.Apply(WhipKind.Leather, World.TargetDummy.DevHm);
            int h1 = CharmEffects.DevWeaverHits;
            float until = Time.time + 6f;
            while (CharmEffects.DevWeaverHits == h1 && Time.time < until) yield return null;
            CharmCheck("吃鞭子的标记", CharmEffects.DevWeaverHits > h1 && CharmEffects.DevLastCompanionDamage > plain, $"{plain} → {CharmEffects.DevLastCompanionDamage}");
        }

        IEnumerator GrimmchildTest()
        {
            var pd = PlayerData.instance;
            int level = pd.grimmChildLevel;
            pd.grimmChildLevel = 3;
            pd.SetBool("gotCharm_40", true);
            WearOnly(CharmEffects.Grimmchild);
            yield return ToDummy(4f);
            yield return new WaitForSeconds(3f);
            GameObject child = null;
            try { child = GameObject.FindGameObjectWithTag("Grimmchild"); } catch (UnityException) { }
            CharmCheck("格林之子出来了", child != null, "");
            yield return new WaitForSeconds(1f);
            CharmCheck("调快调准", CharmEffects.DevGrimmTuned > 0, "");
            int h0 = CharmEffects.DevGrimmHits;
            yield return new WaitForSeconds(8f);
            CharmShot("fire");
            int hits = CharmEffects.DevGrimmHits - h0;
            CharmCheck("8 秒里打中好几次（原版约 1.5 秒一发）", hits >= 5, $"{hits} 下，追踪修正 {CharmEffects.DevGrimmSteered}");
            CharmCheck("一发约 3 级的 1.1 倍骨钉", CharmEffects.DevLastCompanionDamage >= Mathf.RoundToInt(NailItem.DamageNow() * 1.1f * 0.85f), $"{CharmEffects.DevLastCompanionDamage}");
            pd.grimmChildLevel = level;
            WearOnly();
        }

        IEnumerator GreedTest()
        {
            var pd = PlayerData.instance;
            WearOnly(CharmEffects.FragileGreed);
            CharmCheck("自己的吉欧倍数不重复算", CharmEffects.ScaleGeo(100) == 100, $"{CharmEffects.ScaleGeo(100)}");
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            Plugin.Log.LogInfo($"[AutoTest] 护符24 存档里易碎贪婪 碎了 {pd.brokenCharm_24}，坚固 {pd.fragileGreed_unbreakable}");
            bool broken = pd.brokenCharm_24;
            pd.brokenCharm_24 = false;
            // Two of a kind killed, the first without the charm, the second with it: the flung pieces counted.
            var prey = new List<HealthManager>();
            string kind = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || EnemyStats.Of(hm).Boss) continue;
                string k = hm.name.Split(' ', '(')[0];
                if (k != "Crawler" && k != "Climber" && k != "Zombie" && k != "Fly") continue;
                if (kind == null) kind = k;
                if (k == kind) prey.Add(hm);
            }
            CharmCheck("找到两只同样的小怪", prey.Count >= 2, $"{kind} {prey.Count} 只");
            if (prey.Count < 2) { pd.brokenCharm_24 = broken; yield break; }
            int[] pieces = new int[2];
            int[] geo = new int[2];
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) WearOnly(); else WearOnly(CharmEffects.FragileGreed);
                yield return null;
                var hm = prey[pass];
                var before = new HashSet<int>();
                foreach (var g in Object.FindObjectsByType<GeoControl>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (g.gameObject.activeInHierarchy) before.Add(g.GetInstanceID());
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                var at = (Vector2)hm.transform.position;
                for (int i = 0; i < 30 && !hm.isDead; i++) mod.Melee.HitWith(col, 99999, 0f, 0f, 1f, at + Vector2.left);
                yield return new WaitForSeconds(0.15f);
                foreach (var g in Object.FindObjectsByType<GeoControl>(FindObjectsSortMode.None))
                    if (g.gameObject.activeInHierarchy && !before.Contains(g.GetInstanceID())) pieces[pass]++;
                geo[pass] = EnemyDrops.DevGeo;
                if (pass == 1) CharmShot("geo");
            }
            int g1 = geo[1], small = g1 % 5, medium = g1 % 25 / 5, large = g1 / 25;
            int expect = small + Mathf.CeilToInt(small * 0.2f) + medium + Mathf.CeilToInt(medium * 0.2f) + large + Mathf.CeilToInt(large * 0.2f);
            CharmCheck("戴着时多掉两成（原版的规则，每种向上取整）", pieces[1] >= expect - 1 && pieces[1] > pieces[0], $"不戴 {geo[0]} 吉欧飞出 {pieces[0]} 枚，戴着 {geo[1]} 吉欧飞出 {pieces[1]} 枚（应约 {expect} 枚）");
            pd.brokenCharm_24 = broken;
        }

        IEnumerator DreamWielderTest()
        {
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            pd.hasDreamNail = true;
            WearOnly(CharmEffects.DreamWielder);
            InHand(ItemType.CopperBroadsword);
            yield return null;
            CharmCheck("没拿梦钉时不能绕过物品限制", !hero.CanDreamNail());
            InHand(ItemType.DreamNail);
            var attackTimer = typeof(HeroController).GetField("attack_time", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            float keepTimer = (float)attackTimer.GetValue(hero);
            bool keepAttack = hero.cState.attacking, keepGround = hero.cState.onGround;
            hero.cState.attacking = true; hero.cState.onGround = false; attackTimer.SetValue(hero, 0f);
            CharmCheck("空中梦钉不绕过攻击恢复限制", !hero.CanDreamNail());
            hero.cState.attacking = keepAttack; hero.cState.onGround = keepGround; attackTimer.SetValue(hero, keepTimer);
            PlayMakerFSM dn = DreamNailItem.DevFsm(hero);
            float[] walked = new float[2];
            int hits0 = DreamNailItem.DevHits;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) WearOnly(); else WearOnly(CharmEffects.DreamWielder);
                yield return ToDummy(4f);
                InHand(ItemType.DreamNail);
                yield return new WaitForSeconds(0.3f);
                if (pass == 1) { hero.transform.position += Vector3.up * 6f; yield return new WaitForSeconds(0.2f); }
                bool airborne = !hero.cState.onGround;
                float x0 = hero.transform.position.x;
                int air0 = CharmEffects.DevAirDreams;
                DreamNailItem.DevHold = true;
                Player.TerraMovement.DevMove = 1;
                bool shot = false, started = false, slashed = false;
                for (float t = 0f; t < 1.3f; t += Time.deltaTime)
                {
                    if (dn != null && dn.ActiveStateName != "Inactive") started = true;
                    if (dn != null && dn.ActiveStateName == "Slash") slashed = true;
                    if (pass == 1 && !shot && t > 0.15f) { CharmShot("charge"); shot = true; }
                    if (t > 0.9f) DreamNailItem.DevHold = false;
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                DreamNailItem.DevHold = false;
                walked[pass] = hero.transform.position.x - x0;
                if (pass == 1)
                {
                    CharmCheck("空中也能蓄力", airborne && started && CharmEffects.DevAirDreams > air0, $"按下时在空中 {airborne}，空中开始 {CharmEffects.DevAirDreams - air0} 次");
                    CharmCheck("边移动边挥出", slashed, "");
                    CharmCheck("蓄力时没交出控制", CharmEffects.DevDreamKeptControl > 0 && hero.acceptingInput, "");
                }
                yield return new WaitForSeconds(1.2f);
            }
            CharmCheck("不戴时蓄力定住不动", Mathf.Abs(walked[0]) < 0.5f, $"走了 {walked[0]:F2}");
            CharmCheck("戴着时蓄力和挥出都能移动", walked[1] > 1.5f, $"走了 {walked[1]:F2}");
            mod.Inventory.Slots[0] = default;
            WearOnly();
        }

    }
}
