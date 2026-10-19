using System.Collections.Generic;
using TerraKnight.Items;

namespace TerraKnight.Player
{
    /// <summary>The mount items' tooltips in English (the names are Terraria's own, by item id): what the mount does, how it is ridden, where it comes from.</summary>
    internal static partial class Mounts
    {
        static readonly Dictionary<string, string> englishTips = new Dictionary<string, string>
        {
            ["高速奔跑、短时飞行"] = "Runs fast, flies for a while",
            ["陆行、高跳"] = "Runs, jumps high",
            ["陆行、短时飞行"] = "Runs, flies for a while",
            ["高跳；落下踩踏敌人并反弹"] = "Jumps high; stomps the enemies it lands on and bounces off",
            ["陆地缓慢；入水后高速游泳"] = "Slow on land; swims fast in liquid",
            ["有限悬停飞行"] = "Hovers for a while",
            ["无限悬停飞行"] = "Hovers without end",
            ["高跳；自动瞄准射击可见敌人"] = "Jumps high; shoots enemies in sight on its own",
            ["高速冲撞、额外跳跃"] = "Charges at speed, jumps again in the air",
            ["无限飞行；浸水或低生命时加速"] = "Flies without end; faster in liquid or at low life",
            ["高速陆行"] = "Drives fast",
            ["陆行；入水后游泳"] = "Runs; swims in liquid",
            ["长距离奔跑、冲撞"] = "Runs far, charges",
            ["弹簧高跳"] = "Bounces high",
            ["无限飞行；自动炮击可见敌人"] = "Flies without end; fires its cannon at enemies in sight",
            ["高跳；自动机枪射击可见敌人"] = "Jumps high; machine-guns enemies in sight on its own",
            ["液体中高速游泳；免疫熔岩与酸液"] = "Swims fast in liquid; immune to lava and acid",
            ["短时飞行；落下踩踏敌人并反弹"] = "Flies for a while; stomps the enemies it lands on and bounces off",
            ["高速狼形奔跑与高跳"] = "Runs fast and jumps high as a wolf",
            ["迅猛龙疾跑、高跳"] = "Sprints and jumps high as a raptor",
            ["老鼠疾跑、高跳"] = "Sprints and jumps high as a rat",
            ["无限悬停飞行；受伤或使用物品时解除变身"] = "Hovers without end; the form breaks when hurt or using an item",
            ["滑行：移速与跳跃小幅叠加，保留翅膀与额外跳跃"] = "Skates: a little faster and higher, wings and extra jumps kept",
            ["无限悬停飞行；小体型；使用武器时解除变身"] = "Hovers without end; small; the form breaks when using a weapon",
            ["疾跑、高跳"] = "Sprints, jumps high",
            ["强化疾跑、高跳；近身寒冰冲撞"] = "Sprints harder, jumps high; a frost charge up close",
            ["强化疾跑、高跳；近身火焰冲撞"] = "Sprints harder, jumps high; a fire charge up close",
        };

        /// <summary>A mount item's tooltip in English, its lines as the Chinese one's.</summary>
        internal static string EnglishTooltip(ItemDef d)
        {
            var m = d != null ? Of(d.Type) : null;
            if (m == null) return d?.ZhTooltip;
            string what = englishTips.TryGetValue(m.Tip ?? "", out var en) ? en : m.Tip;
            return what + "\nRight-click to put it in the mount slot; press R (by default) to get on or off, or use it in hand"
                + "\nStage " + m.Stage + "; each source drops it at " + (100f / m.Den).ToString("0.##") + "%";
        }
    }
}
