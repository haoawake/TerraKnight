using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "p4_v5F_dirt_zh"): recorder F's Dirtmouth takes. A new save's dormant half-clear pylon by Dirtmouth's
    /// bench lit by the first rest there; the Target Dummy struck by the Terra Blade, the Zenith and a Stardust Dragon,
    /// its DPS line over it; both altars by the bench knocked off as items by the Pwnhammer; the lit pylon knocked out by
    /// a pickaxe and set down further along the street.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator P4_v5F_dirt(HeroController hero)
        {
            F5Safe();
            StoryBeats.SetOn(false);
            yield return F5PylonWake();
            yield return P4Kit();
            F5Safe();
            mod.TrySwitchLoadout(0);
            yield return new WaitForSeconds(4f);
            F5ClearChat();
            yield return F5Altars();
            yield return F5PylonMove();
            yield return F5Dummy();
            F5CamOff();
        }

        float f5Bottom;

        /// <summary>The hero standing on the street at x (Dirtmouth's street height), facing a way.</summary>
        void F5Street(HeroController hero, float x, int face)
        {
            var st = TownNpcs.DevStreet;
            var col = hero.GetComponent<BoxCollider2D>();
            f5Bottom = col.bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(x, st.y - f5Bottom + 0.15f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            C4Face(hero, face);
        }

        /// <summary>A held use of the selected item at a moving aim for some seconds (eased onto it).</summary>
        IEnumerator F5HoldUse(float seconds, System.Func<Vector2> aim, float rate = 10f)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                var a = aim();
                SmoothAim(a, rate);
                mod.DevHold = true;
                if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aimNow);
                yield return null;
            }
            mod.DevHold = null;
        }

        /// <summary>The selected item used once at a point, the aim eased there first and the use waited for.</summary>
        IEnumerator F5UseAt(Vector2 at, float settle = 0.2f, float rate = 12f, bool target = true)
        {
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                SmoothAim(at, rate);
                if (t >= settle && Vector2.Distance(aimNow, at) < 0.12f) break;
                yield return null;
            }
            while (!mod.DevReady || mod.IsUsingItem) { SmoothAim(at, rate); yield return null; }
            mod.DevUseSelected(at, false, target);
            yield return null;
        }

        IEnumerator F5PylonWake()
        {
            yield return EnterRoom("Town");
            float entered = Time.time;
            var hero = HeroController.instance;
            F5Safe();
            var st = TownNpcs.DevStreet;
            var p = Pylons.FindDormant("Town", "RestBench");
            if (p == null) { F5Log("晶塔：德特茅斯没有未激活的晶塔"); yield break; }
            var region = TerrainSystem.Find("Town");
            var corner = (Vector2)region.CellOffset * CellGrid.CellSize + p.Local;
            var box = new Rect(corner, new Vector2(Pylons.WidthCells, Pylons.HeightCells) * CellGrid.CellSize);
            F5Street(hero, st.x - 8f, 1);
            var col = hero.GetComponent<BoxCollider2D>();
            F5CamOn(new Vector2(1.5f, 3.8f));
            while (Time.time - entered < 9.5f) yield return null;
            F5Shoo(st.x - 13f, st.x + 10f, 0, 9f);
            F5ClearChat();
            yield return new WaitForSeconds(0.3f);
            F5CamSnap();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(2.5f, 0.8f), 99f);
            int lit0 = Pylons.DevDormantLit;
            StartRec("v5F_pylon_wake");
            yield return new WaitForSeconds(0.4f);
            yield return F5WalkTo(hero, st.x, 2.5f, () => box.center + new Vector2(-1.5f, 0.6f));
            yield return new WaitForSeconds(0.25f);
            yield return Tap(Key.W, 0.3f);
            for (float t = 0f; t < 3f && Pylons.DevDormantLit == lit0; t += Time.deltaTime) { SmoothAim(box.center + new Vector2(0f, 0.3f), 3f); yield return null; }
            bool sat = PlayerData.instance.atBench;
            for (float t = 0f; t < 2.6f; t += Time.deltaTime) { SmoothAim(box.center + new Vector2(0f, 0.5f), 3f); yield return null; }
            yield return Tap(Key.Space, 0.2f);
            yield return new WaitForSeconds(1.0f);
            StopRec();
            F5Log($"晶塔点亮：坐下 {sat}，点亮 {Pylons.DevDormantLit - lit0}，现在 {(Pylons.Find("Town", "RestBench") != null ? "已解锁" : "没有")}，坐着 {PlayerData.instance.atBench}");
        }

        IEnumerator F5Dummy()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            mod.TrySwitchLoadout(0);
            yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { F5Log("假人：没有"); yield break; }
            var saved = (ItemStack[])inv.Slots.Clone();
            inv.Slots[2] = ItemStack.Of(ItemType.TerraBlade);
            inv.Slots[3] = ItemStack.Of(ItemType.Meowmere);
            F5Street(hero, at.Value.x - 7f, 1);
            var col = hero.GetComponent<BoxCollider2D>();
            F5CamOn(new Vector2(2.0f, 3.8f));
            F5Shoo(at.Value.x - 12f, at.Value.x + 9f, 0, 10f);
            yield return new WaitForSeconds(1.2f);
            F5ClearChat();
            F5CamSnap();
            Vector2 body = at.Value + new Vector2(0f, 0.9f);
            aimInit = false;
            SmoothAim(body + new Vector2(-2f, 0.5f), 99f);
            int hits0 = TargetDummy.DevHits;
            inv.Selected = 2;
            StartRec("v5F_dummy");
            yield return new WaitForSeconds(0.3f);
            yield return F5WalkTo(hero, at.Value.x - 4.6f, 1.5f, () => body);
            C4Face(hero, 1);
            yield return F5HoldUse(4.0f, () => body);
            // The Meowmere, then the Zenith: the line over it climbs.
            inv.Selected = 3;
            yield return new WaitForSeconds(0.15f);
            yield return F5HoldUse(3.4f, () => body);
            inv.Selected = 0;
            yield return new WaitForSeconds(0.15f);
            yield return F5HoldUse(4.6f, () => body);
            yield return new WaitForSeconds(1.2f);
            StopRec();
            F5Log($"假人：打了 {TargetDummy.DevHits - hits0} 下，伤害 {TargetDummy.DevDamage}");
            for (int i = 0; i < saved.Length; i++) inv.Slots[i] = saved[i];
            mod.DevAim = null;
        }

        IEnumerator F5Altars()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var demon = Stations.DevAltarBox(StationKind.DemonAltar, true);
            var crimson = Stations.DevAltarBox(StationKind.CrimsonAltar, true);
            if (demon == null || crimson == null) { F5Log($"祭坛：恶魔 {demon} 猩红 {crimson}"); yield break; }
            var saved = (ItemStack[])inv.Slots.Clone();
            inv.Slots[2] = ItemStack.Of(ItemType.Pwnhammer);
            inv.Selected = 2;
            F5Street(hero, demon.Value.xMin - 5f, 1);
            var col = hero.GetComponent<BoxCollider2D>();
            F5CamOn(new Vector2(2.0f, 3.8f));
            F5Shoo(demon.Value.xMin - 9f, crimson.Value.xMax + 7f, 1, 10f);
            yield return new WaitForSeconds(1.2f);
            F5ClearChat();
            F5CamSnap();
            aimInit = false;
            SmoothAim(demon.Value.center + new Vector2(-1.5f, 0.8f), 99f);
            int smashed0 = Stations.DevSmashed;
            StartRec("v5F_altar");
            yield return new WaitForSeconds(0.3f);
            foreach (var box in new[] { demon.Value, crimson.Value })
            {
                yield return F5WalkTo(hero, box.xMin - 1.1f, 3f, () => box.center + new Vector2(0f, 0.4f));
                for (float t0 = 0f; t0 < 0.35f; t0 += Time.deltaTime) { SmoothAim(box.center, 10f); yield return null; }
                C4Face(hero, 1);
                for (int k = 0; k < 2; k++)
                {
                    int smashedBefore = Stations.DevSmashed;
                    yield return F5UseAt(box.center, 0.15f, 12f, false);
                    // The swing's blow didn't land on it (logged): the blow given in the same moment.
                    if (k == 1 && Stations.DevSmashed == smashedBefore && StationAltarAt(box))
                    {
                        bool direct = Stations.TrySmashAltar(ItemDb.Get(ItemType.Pwnhammer), box.center, hero);
                        if (Stations.DevSmashed == smashedBefore) direct &= Stations.TrySmashAltar(ItemDb.Get(ItemType.Pwnhammer), box.center, hero);
                        F5Log($"祭坛：挥锤没敲到，直接敲 {direct}，敲下 {Stations.DevSmashed - smashedBefore}，DevAim {mod.DevAim}");
                    }
                    yield return new WaitForSeconds(0.25f);
                    F5Log($"祭坛 一下：主角 {hero.GetComponent<BoxCollider2D>().bounds.center.x:F2}，祭坛 {box.center.x:F2}，够得着 {Stations.DevReach(hero).Overlaps(box)}，敲下 {Stations.DevSmashed - smashed0}，手里 {inv.Current.Def?.Name}");
                }
                yield return new WaitForSeconds(0.6f);
            }
            yield return new WaitForSeconds(1.0f);
            StopRec();
            int items = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && (s.Type == ItemType.DemonAltar || s.Type == ItemType.CrimsonAltar)) items += s.Count;
            F5Log($"祭坛：敲下 {Stations.DevSmashed - smashed0}，包里祭坛 {items}，地上 {WorldItems.DevCount(ItemType.DemonAltar) + WorldItems.DevCount(ItemType.CrimsonAltar)}");
            for (int i = 0; i < saved.Length; i++) inv.Slots[i] = saved[i];
            mod.DevAim = null;
        }

        /// <summary>An altar still stands in this box.</summary>
        static bool StationAltarAt(Rect box)
        {
            foreach (var k in new[] { StationKind.DemonAltar, StationKind.CrimsonAltar })
            {
                var b = Stations.DevAltarBox(k, true);
                if (b.HasValue && b.Value.Overlaps(box)) return true;
            }
            return false;
        }

        IEnumerator F5PylonMove()
        {
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var p = Pylons.Find("Town", "RestBench");
            if (p == null) { F5Log("挪晶塔：德特茅斯没有点亮的晶塔"); yield break; }
            var region = TerrainSystem.Find("Town");
            var corner = (Vector2)region.CellOffset * CellGrid.CellSize + p.Local;
            var box = new Rect(corner, new Vector2(Pylons.WidthCells, Pylons.HeightCells) * CellGrid.CellSize);
            var saved = (ItemStack[])inv.Slots.Clone();
            for (int i = 0; i < inv.Slots.Length; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Def.IsPylon) inv.Slots[i] = default;
            inv.Slots[2] = ItemStack.Of(ItemType.GoldPickaxe);
            inv.Slots[3] = default;
            inv.Selected = 2;
            F5Street(hero, box.xMin - 4.5f, 1);
            var col = hero.GetComponent<BoxCollider2D>();
            F5CamOn(new Vector2(2.0f, 3.8f));
            F5Shoo(box.xMin - 7f, box.xMax + 26f, -1, 9f);
            yield return new WaitForSeconds(1.2f);
            F5ClearChat();
            F5CamSnap();
            aimInit = false;
            SmoothAim(box.center + new Vector2(-1.5f, 0.5f), 99f);
            int mined0 = Pylons.DevMined, placed0 = Pylons.DevPlaced;
            StartRec("v5F_pylon_move");
            yield return new WaitForSeconds(0.3f);
            yield return F5WalkTo(hero, box.xMin - 1.0f, 2f, () => box.center);
            C4Face(hero, 1);
            for (int k = 0; k < 4 && Pylons.DevMined == mined0; k++)
            {
                yield return F5UseAt(box.center, 0.12f, 12f);
                yield return new WaitForSeconds(0.15f);
            }
            // The item flies to the hero; on along the street to a free spot, then set down there.
            yield return new WaitForSeconds(0.6f);
            int slot = -1;
            for (int i = 0; i < Inventory.Size && slot < 0; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Def.IsPylon) slot = i;
            if (slot >= 0 && slot != 3) { inv.Slots[3] = inv.Slots[slot]; inv.Slots[slot] = default; }
            float goal = box.center.x + 11f;
            yield return F5WalkTo(hero, goal, 3f, () => (Vector2)col.bounds.center + new Vector2(2.5f, 0.3f));
            C4Face(hero, 1);
            inv.Selected = 3;
            yield return new WaitForSeconds(0.25f);
            Vector2 spot = new Vector2(col.bounds.center.x + 2.2f, col.bounds.min.y + 0.3f);
            yield return F5UseAt(spot, 0.3f, 9f);
            for (float t = 0f; t < 2.4f; t += Time.deltaTime) { SmoothAim(spot + new Vector2(0f, 1.4f), 3f); yield return null; }
            StopRec();
            F5Log($"挪晶塔：敲下 {Pylons.DevMined - mined0}，放下 {Pylons.DevPlaced - placed0}，物品在 {slot}");
            for (int i = 0; i < saved.Length; i++) inv.Slots[i] = saved[i];
            mod.DevAim = null;
        }
    }
}
