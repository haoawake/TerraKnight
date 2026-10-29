using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bladeskills", BladeSkills): in Dirtmouth by its Target Dummy, (1) the Katana's right button held a second
    /// and let go: the player flashes through past the dummy and the cut strikes it; let go at once (under a quarter
    /// second): nothing; (2) the Blue Phaseblade thrown at the dummy: it hangs there, having cut it on the way; the left
    /// button then cuts with it there (the dummy struck while the player stands well off); called back, it comes home; (3)
    /// the Iron Broadsword has neither. Lines "[AutoTest] 右键技 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BladeSkillsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 右键技 {what} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { Check(false, "德特茅斯的训练假人"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var inv = mod.Inventory;
            IEnumerator Stand(float dx)
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x + dx, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
                yield return new WaitForSeconds(0.6f);
            }
            var aim = at.Value + new Vector2(0f, 0.8f);
            mod.DevAim = aim;

            // (1) The katana's draw.
            inv.Slots[0] = ItemStack.Of(ItemType.Katana);
            inv.Selected = 0;
            yield return Stand(-3f);
            int draws0 = BladeSkills.DevDraws, hits0 = TargetDummy.DevHits;
            float x0 = hero.transform.position.x;
            BladeSkills.DevHeld = true;
            BladeSkills.DevPress = true;
            // A step while charging: the dragged tip scores the ground.
            yield return new WaitForSeconds(0.1f);
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(0.35f);
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.15f);
            Shot("Town", "bladeskills_iai_charge");
            yield return new WaitForSeconds(0.55f);
            BladeSkills.DevHeld = false;
            yield return new WaitForSeconds(0.12f);
            Shot("Town", "bladeskills_iai_cut");
            yield return new WaitForSeconds(0.5f);
            BladeSkills.DevHeld = null;
            float went = hero.transform.position.x - x0;
            Check(BladeSkills.DevDraws == draws0 + 1 && went > 3f && TargetDummy.DevHits > hits0, $"武士刀蓄力一秒拔刀：冲过 {went:F2}，假人挨了 {TargetDummy.DevHits - hits0} 下");
            yield return Stand(-3f);
            draws0 = BladeSkills.DevDraws;
            BladeSkills.DevHeld = true;
            BladeSkills.DevPress = true;
            yield return new WaitForSeconds(0.1f);
            BladeSkills.DevHeld = false;
            yield return new WaitForSeconds(0.4f);
            BladeSkills.DevHeld = null;
            Check(BladeSkills.DevDraws == draws0, $"按一下就松开不拔刀（{BladeSkills.DevDraws - draws0}）");
            // The right button first: pressed in a swing of the left's, the swing gives way to the charge; let go with the
            // left still held, the draw goes.
            yield return Stand(-3f);
            draws0 = BladeSkills.DevDraws;
            mod.DevHold = true;
            mod.DevUse(ItemType.Katana, aim);
            yield return new WaitForSeconds(0.05f);
            bool swinging = mod.IsUsingItem;
            BladeSkills.DevHeld = true;
            BladeSkills.DevPress = true;
            yield return new WaitForSeconds(0.1f);
            bool gaveWay = !mod.IsUsingItem && BladeSkills.Charging;
            yield return new WaitForSeconds(0.6f);
            BladeSkills.DevHeld = false;
            yield return new WaitForSeconds(0.2f);
            Shot("Town", "bladeskills_iai_fade");
            yield return new WaitForSeconds(0.2f);
            BladeSkills.DevHeld = null;
            mod.DevHold = null;
            Check(swinging && gaveWay && BladeSkills.DevDraws == draws0 + 1, $"左键挥砍中按右键：挥砍让给蓄力（{swinging}/{gaveWay}），左键按着松开右键就拔刀（{BladeSkills.DevDraws - draws0}）");

            // (2) The phase blade thrown.
            inv.Slots[0] = ItemStack.Of(ItemType.BluePhaseblade);
            yield return Stand(-5f);
            int throws0 = BladeSkills.DevThrows, saberHits0 = BladeSkills.DevSaberHits;
            BladeSkills.DevPress = true;
            yield return new WaitForSeconds(0.9f);
            var pivot = BladeSkills.RemotePivot(ItemDb.Get(ItemType.BluePhaseblade));
            Shot("Town", "bladeskills_saber_hang");
            Check(BladeSkills.DevThrows == throws0 + 1 && pivot.HasValue && Vector2.Distance(pivot.Value, aim) < 1.5f,
                $"光剑扔到假人那里悬着（{(pivot.HasValue ? Vector2.Distance(pivot.Value, aim).ToString("F2") : "没悬着")}，路上和悬着打中 {BladeSkills.DevSaberHits - saberHits0} 下）");
            int cuts0 = SwordFx.DevArtCuts, h1 = TargetDummy.DevHits;
            var def = ItemDb.Get(ItemType.BluePhaseblade);
            for (int k = 0; k < 3; k++)
            {
                while (mod.IsUsingItem || !mod.DevReady) yield return null;
                mod.RemoteUse(def);
                yield return new WaitForSeconds(0.05f);
                if (k == 1) Shot("Town", "bladeskills_saber_cut");
            }
            while (mod.IsUsingItem) yield return null;
            float off = Mathf.Abs(hero.transform.position.x - at.Value.x);
            Check(SwordFx.DevArtCuts >= cuts0 + 3 && TargetDummy.DevHits > h1 && off > 3f, $"左键在光剑那里挥砍：{SwordFx.DevArtCuts - cuts0} 刀，假人挨了 {TargetDummy.DevHits - h1} 下，人离假人 {off:F1}");
            int recalls0 = BladeSkills.DevRecalls;
            BladeSkills.DevPress = true;
            for (float t = Time.time + 3f; Time.time < t && BladeSkills.thrown != null;) yield return null;
            Check(BladeSkills.thrown == null && BladeSkills.DevRecalls == recalls0 + 1, "再按右键收回光剑");

            // (3) A plain sword: no art.
            inv.Slots[0] = ItemStack.Of(ItemType.IronBroadsword);
            yield return Stand(-3f);
            draws0 = BladeSkills.DevDraws;
            throws0 = BladeSkills.DevThrows;
            BladeSkills.DevHeld = true;
            BladeSkills.DevPress = true;
            yield return new WaitForSeconds(0.8f);
            BladeSkills.DevHeld = false;
            yield return new WaitForSeconds(0.3f);
            BladeSkills.DevHeld = null;
            Check(BladeSkills.DevDraws == draws0 && BladeSkills.DevThrows == throws0 && BladeSkills.thrown == null, "铁阔剑没有右键技");
            // Left swings, for the look of their trails.
            foreach (var t in new[] { ItemType.BreakerBlade, ItemType.NightsEdge, ItemType.Muramasa })
            {
                inv.Slots[0] = ItemStack.Of(t);
                yield return Stand(-3f);
                while (mod.IsUsingItem) yield return null;
                mod.DevUse(t, aim);
                yield return new WaitForSeconds(0.13f);
                Shot("Town", "bladeskills_swing_" + t);
                while (mod.IsUsingItem) yield return null;
            }

            // (4) Every other weapon's art by the dummy: used, the dummy struck (by the art itself, its shots or its ground),
            // and what each does besides.
            var arts = new (ItemType Type, char How, float Dx, float Wait)[]
            {
                (ItemType.LucyTheAxe, 'p', -3f, 1.6f), (ItemType.Sickle, 'p', -3f, 1.6f), (ItemType.IceSickle, 'p', -3f, 1.6f), (ItemType.DeathSickle, 'p', -3f, 1.6f),
                (ItemType.BreakerBlade, 'c', -2.5f, 1.2f), (ItemType.FieryGreatsword, 'c', -2.5f, 1.4f), (ItemType.ChlorophyteClaymore, 'c', -2.5f, 2f),
                (ItemType.DD2SquireBetsySword, 'c', -3f, 1.4f), (ItemType.Keybrand, 'c', -3f, 1f), (ItemType.BatBat, 'c', -2f, 1f), (ItemType.SlapHand, 'c', -2f, 1f),
                (ItemType.WaffleIron, 'p', -2f, 0.8f), (ItemType.FalconBlade, 'p', -3.5f, 0.8f), (ItemType.InfluxWaver, 'p', -6f, 0.9f), (ItemType.PsychoKnife, 'p', -5f, 0.8f),
                (ItemType.LightsBane, 'p', -2.5f, 0.8f), (ItemType.TerraBlade, 'c', -3f, 1.5f), (ItemType.TentacleSpike, 'p', -5f, 1.2f), (ItemType.CandyCaneSword, 'p', -5f, 1f),
                (ItemType.Starfury, 'c', -4f, 2.5f), (ItemType.StarWrath, 'c', -4f, 2.5f), (ItemType.RevenantEdge, 'c', -5f, 1.2f), (ItemType.BeeKeeper, 'p', -3f, 3f), (ItemType.TheHorsemansBlade, 'p', -4f, 2.5f),
                (ItemType.Flymeal, 'p', -4f, 2f), (ItemType.Bladetongue, 'p', -3f, 1.5f), (ItemType.IceBlade, 'p', -3f, 1.2f), (ItemType.Frostbrand, 'p', -3f, 1.2f),
                (ItemType.BladeofGrass, 'p', -3f, 1.2f), (ItemType.HamBat, 'e', -2f, 0.5f), (ItemType.TaxCollectorsStickOfDoom, 'p', -1.5f, 0.6f),
                (ItemType.BloodButcherer, 'b', -2f, 0.5f), (ItemType.BladedGlove, 'h', -1.5f, 0.8f), (ItemType.FetidBaghnakhs, 'h', -1.5f, 0.8f),
                (ItemType.Cutlass, 'g', -2f, 0.2f), (ItemType.Excalibur, 'g', -2f, 0.2f),
            };
            foreach (var a in arts)
            {
                var d = ItemDb.Get(a.Type);
                inv.Slots[0] = ItemStack.Of(a.Type);
                inv.Selected = 0;
                yield return Stand(a.Dx);
                mod.DevAim = aim;
                while (mod.IsUsingItem) yield return null;
                int uses0 = BladeSkills.DevUses, hitsBefore = TargetDummy.DevHits, life0 = mod.Stats.Life, taxes0 = BladeSkills.DevTaxes, rush0 = BladeSkills.DevRushCuts;
                float xBefore = hero.transform.position.x;
                if (a.How == 'e') { mod.Stats.Life = Mathf.Max(1, mod.Stats.LifeMax / 2); life0 = mod.Stats.Life; }
                if (a.How == 'c' || a.How == 'h')
                {
                    BladeSkills.DevHeld = true;
                    BladeSkills.DevPress = true;
                    yield return new WaitForSeconds(a.How == 'c' ? 1.3f : a.Wait);
                    BladeSkills.DevHeld = false;
                }
                else BladeSkills.DevPress = true;
                yield return new WaitForSeconds(0.15f);
                bool shoot = a.Type == ItemType.FieryGreatsword || a.Type == ItemType.InfluxWaver || a.Type == ItemType.TentacleSpike || a.Type == ItemType.Starfury
                    || a.Type == ItemType.TerraBlade || a.Type == ItemType.BreakerBlade || a.Type == ItemType.DD2SquireBetsySword || a.Type == ItemType.FalconBlade || a.Type == ItemType.PsychoKnife || a.Type == ItemType.RevenantEdge;
                if (shoot) Shot("Town", "bladeskills_" + a.Type);
                if (a.Type == ItemType.Frostbrand || a.Type == ItemType.BladeofGrass || a.Type == ItemType.IceBlade)
                {
                    yield return new WaitForSeconds(0.2f);
                    Shot("Town", "bladeskills_" + a.Type);
                }
                yield return new WaitForSeconds(a.How == 'c' ? a.Wait : Mathf.Max(0.1f, a.Wait - 0.15f));
                BladeSkills.DevHeld = null;
                int struck = TargetDummy.DevHits - hitsBefore;
                float moved = hero.transform.position.x - xBefore;
                bool used = BladeSkills.DevUses > uses0 || BladeSkills.DevRushCuts > rush0;
                bool pass;
                string more;
                switch (a.How)
                {
                    case 'e': pass = used && mod.Stats.Life > life0; more = $"生命 {life0} → {mod.Stats.Life}"; break;
                    case 'b': pass = used && mod.Stats.Life < life0 && BladeSkills.BloodRiteOn; more = $"生命 {life0} → {mod.Stats.Life}，血祭 {BladeSkills.BloodRiteOn}"; break;
                    case 'h': pass = BladeSkills.DevRushCuts - rush0 >= 4 && struck > 0; more = $"乱舞 {BladeSkills.DevRushCuts - rush0} 下"; break;
                    case 'g': pass = used; more = $"举刀格挡（{BladeSkills.DevLast}）"; break;
                    default: pass = used && struck > 0; more = ""; break;
                }
                if (a.Type == ItemType.TaxCollectorsStickOfDoom) { pass &= BladeSkills.DevTaxes > taxes0; more = $"征税 {BladeSkills.DevTaxes - taxes0} 次"; }
                if (a.Type == ItemType.InfluxWaver || a.Type == ItemType.PsychoKnife || a.Type == ItemType.FalconBlade || a.Type == ItemType.TentacleSpike) { pass &= moved > 1.5f; more += $"，移动 {moved:F1}"; }
                Check(pass, $"{d.Name}（{BladeSkills.Of(d)}）：假人挨了 {struck} 下{(more.Length > 0 ? "，" + more : "")}");
                for (float wait = Time.time + 2f; Time.time < wait && BladeSkills.thrown != null;) yield return null;
                mod.Stats.Life = mod.Stats.LifeMax;
                yield return new WaitForSeconds(0.3f);
            }
            // The charges' sounds (Terraria.SoundMix): made, not clipping, about as loud as Terraria's own item sounds.
            foreach (var name in new[] { "tk_iai_charge", "tk_heavy_charge", "tk_blade_charge", "tk_star_charge", "tk_iai_scrape" })
            {
                var clip = Terraria.SoundMix.Build(name);
                var data = new float[clip.samples];
                clip.GetData(data, 0);
                float peak = 0f; double sum = 0;
                foreach (var v in data) { peak = Mathf.Max(peak, Mathf.Abs(v)); sum += v * v; }
                float rms = (float)System.Math.Sqrt(sum / Mathf.Max(1, data.Length));
                Check(peak < 0.95f && rms > 0.01f && rms < 0.12f, $"蓄力音效 {name}：{clip.length:F2} 秒，峰值 {peak:F2}，响度 {rms:F3}");
            }
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 右键技 合计 {ok}/{all}");
        }
    }
}
