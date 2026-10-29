using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.World
{
    internal sealed partial class Chests
    {
        internal static void RestorationProtection(List<Rect> boxes)
        {
            foreach (var c in chests)
            {
                if (c.Region == null) c.Region = RegionNamed(c.Scene);
                if (c.Region != null) boxes.Add(RestorationGun.ContainerProtection(c.Box));
            }
        }
    }

    internal sealed partial class Stations
    {
        static bool RestorationContainer(Station s) => s.Kind == StationKind.PiggyBank || s.Kind == StationKind.Safe
            || s.Kind == StationKind.DefendersForge || s.Kind == StationKind.VoidVault;

        internal static void RestorationProtection(List<Rect> boxes)
        {
            foreach (var s in stations)
            {
                if (s.Region == null) s.Region = RegionNamed(s.Scene);
                if (s.Region != null && RestorationContainer(s)) boxes.Add(RestorationGun.ContainerProtection(s.Box));
            }
            // A bank on a workbench must not lose that workbench or its supporting blocks.
            var supported = new HashSet<Station>();
            bool added;
            do
            {
                added = false;
                foreach (var s in stations)
                    if (s.Region != null && !supported.Contains(s) && RestorationGun.Protected(s.Box, boxes))
                    {
                        supported.Add(s);
                        boxes.Add(RestorationGun.ContainerProtection(s.Box));
                        added = true;
                    }
            } while (added);
        }

        internal static void RestorationRemove(Vector2 at, List<Rect> protection, List<(ItemStack Stack, Vector2 At)> drops)
        {
            for (int i = stations.Count - 1; i >= 0; i--)
            {
                var s = stations[i];
                if (s.Region == null) s.Region = RegionNamed(s.Scene);
                if (s.Region == null || RestorationContainer(s) || !RestorationGun.Touches(s.Box, at)
                    || RestorationGun.Protected(s.Box, protection)) continue;
                if (s.Spec.Item != ItemType.None) drops.Add((ItemStack.Of(s.Spec.Item), s.Box.center));
                if (s.Fixed && IsAltar(s.Kind)) FixedStations.MarkTaken(s.Kind);
                if (RegionalFurniture.BaseKind(s.Kind) == StationKind.Bed) Beds.Gone(s.Scene, s.Local);
                Remove(i);
                RestorationGun.DevFurniture++;
            }
        }
    }

    internal sealed partial class LifeCrystals
    {
        internal static void RestorationRemove(Vector2 at, List<Rect> protection)
        {
            foreach (var c in crystals)
            {
                if (c.Taken || !c.Local.HasValue) continue;
                var r = TerrainSystem.Find(c.Scene);
                if (r == null) continue;
                var box = Box(c, r);
                if (RestorationGun.Touches(box, at) && !RestorationGun.Protected(box, protection)) Break(c, box);
            }
        }
    }

    internal static partial class Pylons
    {
        internal static void RestorationRemove(Vector2 at, List<Rect> protection)
        {
            foreach (var o in new List<PylonObject>(spawned))
            {
                if (o == null || !o.isActiveAndEnabled || !RestorationGun.Touches(o.Bounds, at)
                    || RestorationGun.Protected(o.Bounds, protection)) continue;
                Dormant.Remove(o.Pylon);
                KnockOut(o.Pylon, o);
            }
        }
    }
}
