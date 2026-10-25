using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;
namespace TerraKnight.World
{
    internal sealed partial class Stations
    {
        internal static void BreakForPrefab(Rect box,List<(ItemStack Stack,Vector2 At)> drops)
        {
            foreach(var s in stations)if(s.Region!=null&&s.Fixed&&s.Box.Overlaps(box))throw new System.InvalidOperationException("固定祭坛不能覆盖");
            for(int i=stations.Count-1;i>=0;i--)
            {
                var s=stations[i];if(s.Region==null||!s.Box.Overlaps(box))continue;
                if(s.Spec.Item!=ItemType.None)drops.Add((ItemStack.Of(s.Spec.Item),s.Box.center));
                if(RegionalFurniture.BaseKind(s.Kind)==StationKind.Bed)Beds.Gone(s.Scene,s.Local);
                Remove(i);
            }
        }
    }
    internal sealed partial class Chests
    {
        internal static bool PrefabBlocked(Rect box)
        {
            foreach(var c in chests)if(c.Region!=null&&!c.Bank&&c.Box.Overlaps(box)&&(c.NetBusy||Net.WorldSync.ChestBusy(c.Scene,c.Local)))return true;
            return false;
        }
        internal static void BreakForPrefab(Rect box,List<(ItemStack Stack,Vector2 At)> drops)
        {
            if(PrefabBlocked(box))throw new System.InvalidOperationException("请先关闭正在使用的箱子");
            if(open!=null&&open.Bank&&open.Box.Overlaps(box))Close(false);
            for(int i=chests.Count-1;i>=0;i--)
            {
                var c=chests[i];if(c.Region==null||c.Bank||!c.Box.Overlaps(box))continue;
                if(open==c)Close(false);
                drops.Add((ItemStack.Of(RegionalChests.ItemOf(c.Style)),c.Box.center));
                foreach(var item in c.Items)if(!item.IsEmpty)drops.Add((item,c.Box.center));
                Remove(i);
            }
        }
    }
}
