using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "chargeglow", SwordFxCharge, the user's 10-27 "所有武器蓄力结束后应该焕发些许光辉…只需要在最外层加粒子效果"):
    /// in Dirtmouth by its Target Dummy, every weapon whose right-click art is charged (melee, spears, thrown, yoyos, flails,
    /// close weapons, the Zenith and the monk staffs, the ranged weapons), its art held: no outline motes while it charges,
    /// motes once it is full, its picture's outline found; then the Flint held charged. Shots Town_glow_&lt;weapon&gt; for one of
    /// each kind. Lines "[AutoTest] 蓄力光辉 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ChargeGlowTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 蓄力光辉 {what} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            BladeArts.Set(true);
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt ?? (Vector2)hero.transform.position + new Vector2(5f, 0f);
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var aim = at + new Vector2(0f, 0.8f);
            var inv = mod.Inventory;
            var saved = inv.Slots.ToArray();
            int savedSel = inv.Selected;
            bool savedInfinite = inv.Infinite;
            inv.Infinite = true;
            var tp = TerraProjectiles.Instance;
            var weapons = ItemDb.All.Where(d => d != null && ItemDb.Get(d.Type) == d && d.IsWeapon && BladeSkills.ChargedArt(d)).ToList();
            Plugin.Log.LogInfo($"[AutoTest] 蓄力光辉 蓄力右键技 {weapons.Count} 件");
            // One of each kind pictured.
            var shown = new HashSet<ItemType>
            {
                ItemType.Katana, ItemType.BreakerBlade, ItemType.TerraBlade, ItemType.Starfury, ItemType.ThunderSpear, ItemType.Valor, ItemType.Mace,
                ItemType.WoodenBoomerang, ItemType.Zenith, ItemType.WoodenBow, ItemType.Handgun, ItemType.Shotgun, ItemType.Javelin,
            };
            foreach (var def in weapons)
            {
                for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                inv.Slots[0] = ItemStack.Of(def.Type, def.MaxStack > 1 ? def.MaxStack : 1);
                if (def.UseAmmo != AmmoKind.None)
                {
                    var ammo = ItemDb.All.Where(a => a != null && ItemDb.Get(a.Type) == a && a.Ammo == def.UseAmmo).OrderBy(a => a.Damage).FirstOrDefault();
                    if (ammo != null) inv.Slots[1] = ItemStack.Of(ammo.Type, ammo.MaxStack);
                }
                inv.Selected = 0;
                hero.transform.position = Clear(hero, new Vector2(at.x - 4f, at.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
                mod.DevAim = aim;
                yield return new WaitForSeconds(0.6f);
                int motes0 = SwordFx.DevChargeMotes;
                BladeSkills.DevHeld = true;
                BladeSkills.DevPress = true;
                int early = 0;
                for (float end = Time.time + 3f; Time.time < end && !(BladeSkills.Charging && BladeSkills.Charge >= 1f);)
                {
                    early = SwordFx.DevChargeMotes - motes0;
                    yield return null;
                }
                bool charging = BladeSkills.Charging && BladeSkills.Charge >= 1f;
                int full0 = SwordFx.DevChargeMotes;
                yield return new WaitForSeconds(0.6f);
                int full = SwordFx.DevChargeMotes - full0;
                if (shown.Contains(def.Type)) Shot("Town", $"glow_{def.Type}");
                BladeSkills.DevHeld = false;
                yield return new WaitForSeconds(0.2f);
                BladeSkills.DevHeld = null;
                mod.DevAim = null;
                Check(charging && early == 0 && full >= 6, $"{def.Name}（{BladeSkills.KindArtOf(def)?.Name ?? BladeSkills.Of(def).ToString()}）：蓄满 {(charging ? "是" : "否")}，蓄力中光点 {early}，蓄满后 0.6 秒光点 {full}");
                yield return new WaitForSeconds(1.2f);
                tp.Clear();
            }

            // The Flint, held charged over the head.
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            inv.Slots[0] = ItemStack.Of(ItemType.DeadCellsFlint);
            inv.Selected = 0;
            mod.DevAim = aim;
            mod.DevHold = true;
            yield return new WaitForSeconds(0.3f);
            mod.DevUseSelected(aim);
            int flint0 = SwordFx.DevChargeMotes;
            yield return new WaitForSeconds(0.35f);
            int flintEarly = SwordFx.DevChargeMotes - flint0;
            yield return new WaitForSeconds(0.6f);
            int flintFull = SwordFx.DevChargeMotes - flint0 - flintEarly;
            Shot("Town", "glow_DeadCellsFlint");
            mod.DevHold = null;
            mod.DevAim = null;
            Check(flintEarly == 0 && flintFull >= 6, $"燧石：蓄力中光点 {flintEarly}，蓄满后光点 {flintFull}");
            yield return new WaitForSeconds(1f);

            tp.Clear();
            for (int i = 0; i < inv.Slots.Length && i < saved.Length; i++) inv.Slots[i] = saved[i];
            inv.Selected = savedSel;
            inv.Infinite = savedInfinite;
            Plugin.Log.LogInfo($"[AutoTest] 蓄力光辉 合计 {ok}/{all}");
        }
    }
}
