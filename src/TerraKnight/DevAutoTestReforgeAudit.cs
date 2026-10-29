using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "reforgeaudit", the user's 10-27 "黑波装备无法重铸，是否其他都这样？"): every weapon (damage, one to a stack, not
    /// spent, a class) and accessory the item table has that can't take a prefix (Prefixes.CanHave false): its name, picture
    /// and class; then each weapon and accessory rolled three times (Prefixes.Roll) and checked valid. Writes
    /// reforge-audit.txt in the save folder. Lines "[AutoTest] 重铸 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ReforgeAuditTest()
        {
            yield return new WaitForSeconds(1f);
            var sb = new System.Text.StringBuilder();
            int weapons = 0, accs = 0, noW = 0, noA = 0, badRoll = 0;
            foreach (var d in ItemDb.All)
            {
                if (d == null || ItemDb.Get(d.Type) != d) continue;
                bool weapon = d.Damage > 0 && d.MaxStack <= 1 && !d.Consumable && d.Class != DamageClass.None && !d.HkItem && !d.Permanent;
                bool acc = d.IsAccessory && d.MaxStack <= 1 && !d.HkItem;
                if (!weapon && !acc) continue;
                if (weapon) weapons++; else accs++;
                bool can = Prefixes.CanHave(d);
                if (!can)
                {
                    // Terraria's own that Terraria gives no prefix (its ammo pouches, the Flare Gun): as there.
                    if (Prefixes.TerrariaId(d) > 0) { sb.AppendLine($"原版不给词缀	{d.Type}	{d.Name}"); continue; }
                    if (weapon) noW++; else noA++;
                    sb.AppendLine($"{(weapon ? "武器" : "饰品")}\t{d.Type}\t{d.Name}\t{d.Texture}\t{d.Class}\t泰拉编号 {Prefixes.TerrariaId(d)}");
                    continue;
                }
                for (int k = 0; k < 3; k++)
                {
                    int p = Prefixes.Roll(d, out _);
                    if (p <= 0 || !Prefixes.Valid(d, p)) { badRoll++; sb.AppendLine($"抽不出\t{d.Type}\t{d.Name}\t{p}"); break; }
                }
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(TerraMod.SaveDir, "reforge-audit.txt"), sb.ToString(), new System.Text.UTF8Encoding(false));
            foreach (var t in new[] { "ShadeSoul", "Shade", "SoulWave", "Howl", "Abyss" })
                Plugin.Log.LogInfo($"[AutoTest] 重铸 名字含 {t} 的武器：{string.Join("、", ItemDb.All.Where(d => d != null && d.Damage > 0 && d.Type.ToString().Contains(t)).Select(d => $"{d.Name}({(Prefixes.CanHave(d) ? "能" : "不能")})"))}");
            var own = ItemDb.All.Where(d => d != null && ItemDb.Get(d.Type) == d && d.Damage > 0 && Prefixes.TerrariaId(d) == 0 && d.Class != DamageClass.None && !d.Consumable && d.MaxStack <= 1).ToList();
            Plugin.Log.LogInfo($"[AutoTest] 重铸 模组自制武器 {own.Count} 件：" + string.Join("，", own.Select(d =>
            {
                int p = Prefixes.Roll(d, out _);
                return $"{d.Name}({d.Class}) → {(p > 0 ? Prefixes.Name(p) : "无")}";
            })));
            bool pass = noW == 0 && noA == 0 && badRoll == 0;
            Plugin.Log.LogInfo($"[AutoTest] 重铸 武器 {weapons} 件（不能重铸 {noW}），饰品 {accs} 件（不能重铸 {noA}），抽不出词缀 {badRoll} {(pass ? "通过" : "不通过")}");
        }
    }
}
