using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ProgressionBalance
    {
        // Apply after model, boost, calibration and recipe passes: none may promote
        // cheap construction wood or flatten lunar/final weapons back into ordinary drops.
        internal static readonly Dictionary<ItemType,int> DamageBeforeProgression = new Dictionary<ItemType,int>();
        internal static bool TimberWeapon(ItemType t)
        {
            string name=t.ToString();
            return (name.EndsWith("Sword",StringComparison.Ordinal)||name.EndsWith("Bow",StringComparison.Ordinal))
                && (name.IndexOf("wood",StringComparison.OrdinalIgnoreCase)>=0||name.StartsWith("RichMahogany",StringComparison.Ordinal)||t==ItemType.CactusSword);
        }
        internal static void ApplyDamageProgression(ItemDef[] items)
        {
            foreach(var d in items)
            {
                if(!BalancedWeapon(d))continue;
                DamageBeforeProgression[d.Type]=d.Damage;
                if(!OriginalWeapon(d,out int native,out int rarity)||native<=0)continue;
                // At most twice their native hit: a wooden bow is an improvised
                // beginning, not a metal/boss weapon bought with a few pieces of wood.
                if(TimberWeapon(d.Type))d.Damage=Mathf.Min(d.Damage,native*2);
                // These late weapons retain their native hit alongside their cadence,
                // piercing and multi-hit behavior; the DPS model is not a damage ceiling.
                else if(d.Stage>=4&&rarity>=10)d.Damage=Mathf.Max(d.Damage,native);
                if(d.Type==ItemType.TerraBlade)
                {
                    // A close target can take only one hit per swing, not both slash and wave.
                    // Its long crafting chain must beat the life-stealing knife volley even there.
                    int defense=World.Areas.Defense(d.Stage);
                    float target=WeaponDps(items[(int)ItemType.VampireKnives],defense)*1.35f;
                    float rate=OutputOf(d).Rate,crit=1+Mathf.Clamp01((d.Crit+4)/100f);
                    int floor=Mathf.CeilToInt(target/(crit*rate)+Mathf.Max(0,defense-WeaponArmorPen(d))*.5f);
                    d.Damage=Mathf.Max(d.Damage,floor);
                }
            }
        }
    }
}
