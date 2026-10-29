using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "weaponmode", the user's 10-27 "把新的武器跟旧的武器分为两个模式，新建世界的时候可以选择用新版武器还是旧版武器，不要
    /// 把新武器搞到旧存档里"): on the title, the world creation screen (its weapons row; shots title_weaponmode_new/_vanilla),
    /// Terraria's weapons picked and the world created; in it: Terraria's weapons (no remade swing, no right-click art, the
    /// Zenith's own loop); saved, the file and the start menu's header say so; the file's field set to the remade weapons and
    /// read again: they are on; the field taken out (a save from before the choice) and read again: Terraria's. The suite's
    /// world plays as made (Run leaves its weapons be). Lines "[AutoTest] 武器模式 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>On the title: the world creation screen shown, Terraria's weapons picked, created.</summary>
        IEnumerator WeaponModeBoot()
        {
            yield return new WaitForSecondsRealtime(2f);
            // 开始游戏's new game: the character creation, then on to the world's.
            UIManager.instance.StartNewGame(false, false);
            for (float until = Time.realtimeSinceStartup + 10f; Time.realtimeSinceStartup < until && UI.CreativeMenu.DevStep != "Character";) yield return null;
            UI.TerraMenu.DevToWorld();
            foreach (float wait in new[] { 0.5f, 1.5f, 2f })
            {
                yield return new WaitForSecondsRealtime(wait);
                Plugin.Log.LogInfo($"[AutoTest] 武器模式 建世界界面 {UI.CreativeMenu.DevStep}，主菜单 {UI.CreativeMenu.InMainMenu()}，屏幕 {Screen.width}x{Screen.height}");
                Shot("title", $"weaponmode_new_{wait:F1}");
                // The shot is taken as the frame ends: nothing changed before it is.
                yield return null;
                yield return null;
            }
            UI.TerraMenu.DevWeapons(WeaponKind.Vanilla);
            yield return new WaitForSecondsRealtime(0.6f);
            Shot("title", "weaponmode_vanilla");
            yield return null;
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 武器模式 建世界界面：{UI.CreativeMenu.DevStep}，选原版武器，创建");
            UI.TerraMenu.DevCreate();
        }

        IEnumerator WeaponModeTest()
        {
            int ok = 0, all = 0;
            void Check(string name, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 武器模式 {name}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return new WaitForSeconds(1f);
            var katana = ItemDb.Get(ItemType.Katana);
            var yoyo = ItemDb.Get(ItemType.Rally);
            string Now() => $"本局 {WeaponMode.NameOf(WeaponMode.Current)}，武士刀新挥砍 {BladeArts.Active(katana)}，右键技艺 {BladeSkills.Of(katana)}，悠悠球技艺 {(BladeSkills.KindArtOf(yoyo) != null)}";
            bool Remade() => BladeArts.Enabled && BladeArts.Active(katana) && BladeSkills.Of(katana) != BladeSkills.Skill.None && BladeSkills.KindArtOf(yoyo) != null;
            bool Vanilla() => !BladeArts.Enabled && !BladeArts.Active(katana) && BladeSkills.Of(katana) == BladeSkills.Skill.None && BladeSkills.KindArtOf(yoyo) == null;
            Check("建世界选原版武器：开局就是原版", WeaponMode.Current == WeaponKind.Vanilla && Vanilla(), Now());
            // A swing of each kind, shot.
            var inv = mod.Inventory;
            inv.Slots[0] = ItemStack.Of(ItemType.Katana);
            inv.Selected = 0;
            var hero = HeroController.instance;
            var aim = (Vector2)hero.transform.position + new Vector2(3f, 0.5f);
            mod.DevAim = aim;
            mod.DevUse(ItemType.Katana, aim);
            yield return new WaitForSeconds(0.12f);
            Shot(GameManager.instance.sceneName, "weaponmode_swing_vanilla");
            yield return new WaitForSeconds(0.6f);

            int slot = GameManager.instance.profileID;
            Save.ModSave.Write(slot, mod);
            string path = Save.ModSave.DevPathFor(slot);
            var json = JObject.Parse(File.ReadAllText(path));
            var head = Save.ModSave.HeaderOf(slot);
            Check("存档写下原版武器，开始菜单读到的也是", (int?)json["Weapons"] == 0 && head != null && head.Weapons == WeaponKind.Vanilla, $"文件 Weapons={json["Weapons"]}，列表 {head?.Weapons}");

            // The field set to the remade weapons: read again, they are on.
            json["Weapons"] = 1;
            File.WriteAllText(path, json.ToString());
            Save.ModSave.DevIdentity(slot);
            Check("存档字段改成新版：读档后是新版武器", WeaponMode.Current == WeaponKind.New && Remade() && Save.ModSave.HeaderOf(slot)?.Weapons == WeaponKind.New, Now());
            mod.DevUse(ItemType.Katana, aim);
            yield return new WaitForSeconds(0.12f);
            Shot(GameManager.instance.sceneName, "weaponmode_swing_new");
            yield return new WaitForSeconds(0.6f);

            // The field taken out: a save from before the choice.
            json.Remove("Weapons");
            File.WriteAllText(path, json.ToString());
            Save.ModSave.DevIdentity(slot);
            Check("旧存档（没有这个字段）：读档后是原版武器", WeaponMode.Current == WeaponKind.Vanilla && Vanilla() && Save.ModSave.HeaderOf(slot)?.Weapons == WeaponKind.Vanilla, Now());
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 武器模式 合计 {ok}/{all}");
        }
    }
}
