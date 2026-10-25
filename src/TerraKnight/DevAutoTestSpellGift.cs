using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "spellgift"): the spells' weapons given with the bag full (Items/SpellRewards.cs): two slots left, the
    /// Abyss Shriek's two in the bag and two at the feet; none left, the Vengeful Spirit's four at the feet; the player's
    /// own drops, the notice's count, still lying after the lying items' cap clears 400 others and after a while, and in
    /// the save. Each grant in one frame with the spell's level set and put back, so the per-second check grants nothing else.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SpellGiftTest(string scene, HeroController hero)
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 法术奖励 {what} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            var pd = PlayerData.instance;
            var saved = (ItemStack[])inv.Slots.Clone();
            int fire0 = pd.fireballLevel, scream0 = pd.screamLevel;
            string[] keys = { "fireballLevel.soulwave.v1", "screamLevel.howl.v1", "fireballLevel2.shade.v1", "screamLevel2.abyss.v1" };
            bool[] had = keys.Select(k => mod.GrantedAbilities.Contains(k)).ToArray();
            WorldItems.DevNoGrab = true;
            try
            {
                WorldItems.DevClear();
                Vector2 at = hero.GetComponent<BoxCollider2D>().bounds.center;
                int Lying(ItemType[] ws) => ws.Sum(w => WorldItems.DevCount(w));
                int InBag(ItemType[] ws) => ws.Count(w => inv.Slots.Any(s => !s.IsEmpty && s.Type == w));
                int Spells() => WorldItems.DevItems.Count(x => SpellRewards.Is(x.Stack.Type));

                // Two slots left.
                for (int i = 0; i < inv.Slots.Length; i++) if (!inv.Slots[i].IsEmpty && SpellRewards.Is(inv.Slots[i].Type)) inv.Slots[i] = default;
                for (int i = 0; i < Inventory.Size; i++) if (inv.Slots[i].IsEmpty) inv.Slots[i] = ItemStack.Of(ItemType.Wood, 1);
                inv.Slots[0] = default;
                inv.Slots[1] = default;
                int dropped0 = SpellRewards.DevDropped;
                mod.GrantedAbilities.Remove(keys[3]);
                pd.screamLevel = 2;
                Abyss.GrantReward(mod, pd);
                pd.screamLevel = scream0;
                Check(InBag(Abyss.Weapons) == 2 && Lying(Abyss.Weapons) == 2 && SpellRewards.DevDropped - dropped0 == 2 && mod.GrantedAbilities.Contains(keys[3]),
                    $"背包剩两格吸收深渊尖啸：放进背包 {InBag(Abyss.Weapons)}，掉在脚下 {Lying(Abyss.Weapons)}，提示里 {SpellRewards.DevDropped - dropped0} 把，记为已发 {mod.GrantedAbilities.Contains(keys[3])}");

                // None left.
                dropped0 = SpellRewards.DevDropped;
                mod.GrantedAbilities.Remove(keys[0]);
                pd.fireballLevel = 1;
                SoulWave.GrantReward(mod, pd);
                pd.fireballLevel = fire0;
                Check(InBag(SoulWave.Weapons) == 0 && Lying(SoulWave.Weapons) == 4 && SpellRewards.DevDropped - dropped0 == 4 && mod.GrantedAbilities.Contains(keys[0]),
                    $"背包全满吸收复仇之魂：放进背包 {InBag(SoulWave.Weapons)}，掉在脚下 {Lying(SoulWave.Weapons)}");
                var mine = WorldItems.DevItems.Where(x => SpellRewards.Is(x.Stack.Type)).ToList();
                float far = mine.Count > 0 ? mine.Max(x => Vector2.Distance(x.At, at)) : 99f;
                Check(mine.Count == 6 && mine.All(x => x.NetId == null) && far < 1f, $"六把都是自己的掉落，离身体中心最远 {far:F2}");

                // The cap: 410 others dropped in the same frame, the oldest cleared, the weapons kept.
                for (int k = 0; k < 410; k++) WorldItems.Drop(ItemStack.Of(ItemType.Wood, 1), at + new Vector2((k % 20 - 10) * 0.4f, 0f));
                int wood = WorldItems.DevCount(ItemType.Wood);
                Check(wood < 410 && Spells() == 6, $"地上满 400 件时清掉最早的：木材剩 {wood}/410，法术武器还在 {Spells()}/6");

                // The save keeps them.
                var list = WorldItems.Save();
                int savedSpells = list.Count(s => SpellRewards.Is(s.Type));
                WorldItems.Load(list);
                Check(savedSpells == 6 && Spells() == 6, $"存档里有 {savedSpells}/6，读回 {Spells()}/6");

                // A while later, still lying (landed, not fallen out of the scene).
                for (float t = Time.realtimeSinceStartup + 2.5f; Time.realtimeSinceStartup < t;) yield return null;
                mine = WorldItems.DevItems.Where(x => SpellRewards.Is(x.Stack.Type)).ToList();
                Check(mine.Count == 6 && mine.All(x => x.Drawn), $"2.5 秒后还躺在地上 {mine.Count}/6，都画出来了 {mine.All(x => x.Drawn)}");
            }
            finally
            {
                WorldItems.DevClear();
                WorldItems.DevNoGrab = false;
                for (int i = 0; i < saved.Length && i < inv.Slots.Length; i++) inv.Slots[i] = saved[i];
                pd.fireballLevel = fire0;
                pd.screamLevel = scream0;
                for (int i = 0; i < keys.Length; i++)
                    if (had[i]) mod.GrantedAbilities.Add(keys[i]);
                    else mod.GrantedAbilities.Remove(keys[i]);
                Plugin.Log.LogInfo($"[AutoTest] 法术奖励 结果 {ok}/{all}");
            }
        }
    }
}
