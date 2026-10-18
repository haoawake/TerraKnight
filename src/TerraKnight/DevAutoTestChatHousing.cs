using System.Collections;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17chathousing"): Terraria's chat alone (Enter, pressed on the keyboard, opens the line; the Knight
    /// doesn't move while it is typed into; a line, /roll, /me, /help and /death answered here; Escape's way lets it go)
    /// and the equipment's page buttons (the housing button left of the misc one opens the townspeople's list in the
    /// equipment column's place, the misc button and the housing button again bring the equipment back).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ChatHousing17()
        {
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            for (float t = Time.realtimeSinceStartup + 6f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;

            // Enter on the keyboard, the way a player opens it.
            yield return Tap(Key.Enter);
            yield return new WaitForSeconds(0.3f);
            FxCheck("单人按回车打开聊天框", Net.NetChat.Typing, $"联机 {Net.NetSession.Active}");
            var jump = InControl.InputManager.ActiveDevice != null ? InputHandler.Instance.inputActions.jump : null;
            FxCheck("打字时小骑士不听按键", jump != null && !InputHandler.Instance.inputActions.Enabled, $"操作 {InputHandler.Instance.inputActions.Enabled}");
            Shot("Town", "chat_open");
            Net.NetChat.Say("你好，圣巢");
            bool said = Net.NetChat.DevHas("你好，圣巢") && Net.NetChat.DevLastLine.StartsWith("<");
            Net.NetChat.Say("/roll");
            string roll = Net.NetChat.DevLastLine;
            Net.NetChat.Say("/me 在德特茅斯休息");
            string me = Net.NetChat.DevLastLine;
            int before = Net.NetChat.DevLines;
            Net.NetChat.Say("/help");
            int help = Net.NetChat.DevLines - before;
            Net.NetChat.Say("/death");
            string death = Net.NetChat.DevLastLine;
            FxCheck("单人发言显示在聊天框", said, Net.NetChat.DevLastLine);
            FxCheck("单人 /roll /me /help /death", roll.StartsWith("*") && me.StartsWith("*") && me.Contains("在德特茅斯休息") && help == 9 && death.Length > 0,
                $"roll「{roll}」me「{me}」help {help} 行 death「{death}」");
            yield return new WaitForSeconds(0.3f);
            Shot("Town", "chat_lines");
            Net.NetChat.DevClose();
            yield return new WaitForSeconds(0.3f);
            FxCheck("关闭后小骑士又能操作", !Net.NetChat.Typing && InputHandler.Instance.inputActions.Enabled, "");

            // The page buttons.
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.5f);
            var housing = InventoryGui.DevPageIconRect(1);
            var misc = InventoryGui.DevPageIconRect(0);
            FxCheck("装备区有房屋和其他装备两个页签", housing != null && misc != null && housing.Value.x < misc.Value.x,
                $"房屋 {housing}，其他装备 {misc}");
            if (housing != null && misc != null)
            {
                UI.TerraCursor.DevDrives = true;
                UI.TerraCursor.DevAt = housing.Value.center;
                yield return null;
                UI.TerraCursor.DevClick();
                yield return new WaitForSeconds(0.4f);
                bool opened = Town.TownNpcs.PanelOpen;
                Shot("Town", "housing_tab");
                UI.TerraCursor.DevAt = InventoryGui.DevPageIconRect(0)?.center ?? misc.Value.center;
                yield return null;
                UI.TerraCursor.DevClick();
                yield return new WaitForSeconds(0.4f);
                bool toMisc = !Town.TownNpcs.PanelOpen;
                Shot("Town", "misc_tab");
                UI.TerraCursor.DevAt = InventoryGui.DevPageIconRect(1)?.center ?? housing.Value.center;
                yield return null;
                UI.TerraCursor.DevClick();
                yield return new WaitForSeconds(0.4f);
                UI.TerraCursor.DevAt = InventoryGui.DevPageIconRect(1)?.center ?? housing.Value.center;
                yield return null;
                UI.TerraCursor.DevClick();
                yield return new WaitForSeconds(0.4f);
                bool back = !Town.TownNpcs.PanelOpen;
                UI.TerraCursor.DevDrives = false;
                UI.TerraCursor.DevAt = null;
                FxCheck("点房屋页签打开居民列表，点其他装备换过去，再点房屋两下回到装备", opened && toMisc && back, $"打开 {opened}，换到其他装备 {toMisc}，回到装备 {back}");
            }
            InventoryGui.Open = false;
        }
    }
}
