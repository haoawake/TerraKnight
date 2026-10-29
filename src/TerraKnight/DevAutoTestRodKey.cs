using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "rodkey", the user's 10-27 "传送杖快捷键没传送cd？"): the Rod of Discord key pressed every other frame for
    /// 1.5 s, the rod in the bag (not in hand), between two spots in Dirtmouth: the teleports it makes, as before (no use
    /// time, TeleportRods.DevNoGate) and now (one per the rod's use time, 20 ticks: about five), with the Rod of Discord
    /// (Chaos State on, a seventh of the most life taken for each one under it) and the Rod of Harmony (no Chaos State).
    /// Lines "[AutoTest] 传送杖 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RodKeyTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var pd = PlayerData.instance;
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            mod.DevNoFallDamage = true;
            int ok = 0, all = 0;
            var from = (Vector2)hero.transform.position;
            var a = from + new Vector2(6f, 1.5f);
            var b = from + new Vector2(12f, 1.5f);
            foreach (var rod in new[] { ItemType.RodofDiscord, ItemType.RodOfHarmony })
                foreach (bool gate in new[] { false, true })
                {
                    for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && ItemDb.IsTeleportRod(inv.Slots[i].Type)) inv.Slots[i] = default;
                    inv.Slots[8] = ItemStack.Of(rod);
                    inv.Selected = 0;
                    PlayerBuffs.Remove(TeleportRods.ChaosState);
                    mod.Stats.FullHeal();
                    TeleportRods.DevNoGate = !gate;
                    hero.transform.position = Clear(hero, from);
                    yield return new WaitForSeconds(1f);
                    int tp0 = TeleportRods.DevTeleports, hurt0 = TeleportRods.DevChaosHurts, life0 = mod.Stats.Life;
                    int presses = 0;
                    pd.isInvincible = true;
                    for (float t = Time.unscaledTime + 1.5f; Time.unscaledTime < t;)
                    {
                        mod.DevAim = presses % 2 == 0 ? a : b;
                        mod.QuickTeleport();
                        presses++;
                        // Ungated, the Rod of Discord's every jump under Chaos State would kill: kept alive to count them.
                        if (!gate) mod.Stats.FullHeal();
                        yield return null;
                        yield return null;
                    }
                    mod.DevAim = null;
                    int tps = TeleportRods.DevTeleports - tp0, hurts = TeleportRods.DevChaosHurts - hurt0;
                    bool chaos = PlayerBuffs.Has(TeleportRods.ChaosState);
                    bool pass = gate ? tps >= 3 && tps <= 6 && (rod == ItemType.RodofDiscord ? chaos && hurts == tps - 1 : !chaos && hurts == 0)
                                     : tps > 10;
                    all++;
                    if (pass) ok++;
                    Plugin.Log.LogInfo($"[AutoTest] 传送杖 {ItemDb.Get(rod).Name} {(gate ? "有使用间隔" : "没间隔（旧）")}：1.5 秒按了 {presses} 次，传送 {tps} 次，"
                        + $"混沌状态 {chaos}，混沌扣血 {hurts} 次（生命 {life0}→{mod.Stats.Life}）{(pass ? "通过" : "不通过")}");
                    pd.isInvincible = false;
                }
            TeleportRods.DevNoGate = false;
            Plugin.Log.LogInfo($"[AutoTest] 传送杖 合计 {ok}/{all}");
        }
    }
}
