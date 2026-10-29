using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "yoyoarts", Items.TerraProjectilesYoyoArts, the user's 10-26 "把悠悠球的升级加进去"): in Dirtmouth by its Target
    /// Dummy, each yoyo held out at it 4.5 s with the remade swings: it goes through its tricks (two or more), striking the
    /// dummy; held out far, its string reaching at least 20% further than the game's (the option off); its damage a second on
    /// the dummy against the game's, within 0.85 to 1.4; then its right-click art with the yoyo out (pressed, or held its full
    /// charge and let go): used, by its name, doing its thing (a ring, a slam, copies, a ricochet, a whirl, a burst...) and
    /// the dummy struck. TK_YOYO_ONLY narrows it (item names). Shots Town_yoyoarts_&lt;yoyo&gt;, _art_&lt;yoyo&gt;. Lines
    /// "[AutoTest] 悠悠球 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator YoyoArtsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 悠悠球 {what} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            BladeArts.Set(true);
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { Check(false, "德特茅斯的训练假人"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var aim = at.Value + new Vector2(0f, 0.8f);
            var inv = mod.Inventory;
            var saved = inv.Slots[0];
            int savedSel = inv.Selected;
            var tp = TerraProjectiles.Instance;
            var only = (System.Environment.GetEnvironmentVariable("TK_YOYO_ONLY") ?? "").Split(',').Where(s => s.Length > 0).ToList();

            IEnumerator Gone(ItemDef def)
            {
                mod.DevHold = null;
                mod.DevAim = null;
                for (float t = Time.time + 4f; Time.time < t && tp.DevFarthest(def.Shoot, hero.transform.position) >= 0f;) yield return null;
                yield return new WaitForSeconds(0.3f);
            }

            IEnumerator Hold(ItemDef def, Vector2 to, float seconds, string shot, System.Action each = null)
            {
                inv.Slots[0] = ItemStack.Of(def.Type);
                inv.Selected = 0;
                TerraProjectiles.DevShowStart = Time.time;
                mod.DevAim = to;
                mod.DevHold = true;
                mod.DevUse(def.Type, to);
                float start = Time.time;
                bool shotDone = false;
                while (Time.time - start < seconds)
                {
                    each?.Invoke();
                    if (!shotDone && shot != null && Time.time - start > seconds * 0.55f) { Shot("Town", shot); shotDone = true; }
                    yield return null;
                }
            }

            void Stand(float dx)
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - dx, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
            }

            int ArtCount() => TerraProjectiles.DevYoyoPulses + TerraProjectiles.DevYoyoSlams + TerraProjectiles.DevYoyoOrbiters + TerraProjectiles.DevYoyoRicochets
                              + TerraProjectiles.DevYoyoBursts + TerraProjectiles.DevYoyoLoose + TerraProjectiles.DevYoyoWhirls + TerraProjectiles.DevYoyoFocus;

            var types = new[]
            {
                ItemType.WoodYoyo, ItemType.Rally, ItemType.CorruptYoyo, ItemType.CrimsonYoyo, ItemType.JungleYoyo, ItemType.Code1, ItemType.HiveFive,
                ItemType.Valor, ItemType.Cascade, ItemType.Chik, ItemType.Amarok, ItemType.HelFire, ItemType.Gradient, ItemType.FormatC, ItemType.Code2,
                ItemType.Yelets, ItemType.RedsYoyo, ItemType.ValkyrieYoyo, ItemType.TheEyeOfCthulhu, ItemType.Kraken, ItemType.Terrarian,
            };
            foreach (var type in types)
            {
                var def = ItemDb.Get(type);
                if (only.Count > 0 && !only.Contains(type.ToString())) continue;
                // Its tricks at the dummy.
                BladeArts.Set(true);
                Stand(2.4f);
                yield return new WaitForSeconds(0.6f);
                TerraProjectiles.DevYoyoTricks.Clear();
                int hits0 = TargetDummy.DevHits;
                yield return Hold(def, aim, 4.5f, $"yoyoarts_{type}");
                var tricks = TerraProjectiles.DevYoyoTricks.ToList();
                int hits = TargetDummy.DevHits - hits0;
                yield return Gone(def);
                Check(tricks.Count >= 2 && hits >= 4, $"{def.Name}：花式 {string.Join(" → ", tricks)}，打中假人 {hits} 下");

                // Its reach: held out far, the remade against the game's.
                float[] far = new float[2];
                for (int mode = 0; mode < 2; mode++)
                {
                    BladeArts.Set(mode == 1);
                    Stand(2.4f);
                    yield return new WaitForSeconds(0.4f);
                    var faraway = (Vector2)hero.transform.position + new Vector2(-30f, 3f);
                    float best = 0f;
                    yield return Hold(def, faraway, 2.2f, null, () => best = Mathf.Max(best, tp.DevFarthest(def.Shoot, hero.GetComponent<BoxCollider2D>().bounds.center)));
                    far[mode] = best;
                    yield return Gone(def);
                }
                Check(far[0] > 0f && far[1] >= far[0] * 1.2f, $"{def.Name} 射程：原版 {far[0]:F0} px，新版 {far[1]:F0} px（×{(far[0] > 0f ? far[1] / far[0] : 0f):F2}）");

                // Its damage a second on the dummy: three holds of 4 s in each mode, taking turns (the procs' and crits' luck evened out).
                float[] dps = new float[2], dealt = new float[2], spent = new float[2];
                TerraProjectiles.DevGaps.Remove(def.Shoot);
                for (int rep = 0; rep < 3; rep++)
                    for (int mode = 0; mode < 2; mode++)
                    {
                        BladeArts.Set(mode == 1);
                        Stand(2.4f);
                        yield return new WaitForSeconds(0.6f);
                        int dmg0 = TargetDummy.DevDamage;
                        float start = Time.time;
                        yield return Hold(def, aim, 4f, null);
                        dealt[mode] += TargetDummy.DevDamage - dmg0;
                        spent[mode] += Time.time - start;
                        yield return Gone(def);
                    }
                for (int mode = 0; mode < 2; mode++) dps[mode] = spent[mode] > 0f ? dealt[mode] / spent[mode] : 0f;
                BladeArts.Set(true);
                float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                Check(ratio >= 0.85f && ratio <= 1.4f, $"{def.Name} 每秒伤害：原版 {dps[0]:F0}，新版 {dps[1]:F0}（×{ratio:F2}），原版两击间隔 {TerraProjectiles.DevMeanGap(def.Shoot):F1} 帧");

                // Its right-click art, with the yoyo out at the dummy.
                var art = BladeSkills.KindArtOf(def);
                if (art == null) { Check(false, $"右键 {def.Name}：没有右键技"); continue; }
                Stand(2.4f);
                yield return new WaitForSeconds(0.6f);
                int uses0 = BladeSkills.DevUses, arts0 = ArtCount(), dmgA0 = 0;
                BladeSkills.DevLastArt = "";
                bool started = false;
                float holdStart = Time.time;
                yield return Hold(def, aim, 1.0f, null);
                dmgA0 = TargetDummy.DevDamage;
                if (art.Charged)
                {
                    BladeSkills.DevHeld = true;
                    BladeSkills.DevPress = true;
                    yield return new WaitForSeconds(art.Full + 0.15f);
                    BladeSkills.DevHeld = false;
                }
                else BladeSkills.DevPress = true;
                started = true;
                yield return new WaitForSeconds(0.35f);
                Shot("Town", $"yoyoarts_art_{type}");
                yield return new WaitForSeconds(1.6f);
                BladeSkills.DevHeld = null;
                int artDmg = TargetDummy.DevDamage - dmgA0, did = ArtCount() - arts0;
                bool went = BladeSkills.DevUses > uses0 && BladeSkills.DevLastArt == art.Name;
                yield return Gone(def);
                Check(started && went && did > 0 && artDmg > 0, $"右键 {def.Name}：{art.Name}（{BladeSkills.DevLastArt}），效果 {did}，假人受到 {artDmg}");
                yield return new WaitForSeconds(1f);
            }
            // A filmstrip of tricks in the open (no enemy at the point, the tricks at their full size): Town_yoyoarts_show_<yoyo>_<k>.
            foreach (var type in new[] { ItemType.Gradient, ItemType.JungleYoyo, ItemType.Terrarian })
            {
                if (only.Count > 0 && !only.Contains(type.ToString())) continue;
                var def = ItemDb.Get(type);
                BladeArts.Set(true);
                Stand(9f);
                yield return new WaitForSeconds(0.5f);
                var open = (Vector2)hero.transform.position + new Vector2(4f, 1.6f);
                int k = 0;
                float next = 0.6f;
                yield return Hold(def, open, 4.2f, null, () =>
                {
                    if (Time.time - TerraProjectiles.DevShowStart < next) return;
                    next += 0.3f;
                    Shot("Town", $"yoyoarts_show_{type}_{k++:00}");
                });
                yield return Gone(def);
            }
            inv.Slots[0] = saved;
            inv.Selected = savedSel;
            Plugin.Log.LogInfo($"[AutoTest] 悠悠球 合计 {ok}/{all}");
        }
    }
}
