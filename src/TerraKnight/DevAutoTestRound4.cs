using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "shamanspells", "steadykeep", "pylondormant"): the user's 2026-10-21 fourth list. Shaman Stone's 30% seen
    /// on the four spell weapons' magic ones (白波 / 黑波 / 白吼 / 黑吼); Steady Body keeping its speed half a second after a
    /// let-go, and on a mount; a bench's pylon standing half clear, its place taken, before the bench is rested on.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Round4(string suite)
        {
            charmPass = charmAll = 0;
            charmTag = suite;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            yield return CharmPlace();
            switch (suite)
            {
                case "shamanspells": yield return ShamanSpellsTest(); break;
                case "steadykeep": yield return SteadyKeepTest(); break;
                case "pylondormant": yield return PylonDormantTest(); break;
            }
            EndNailTest();
            WearOnly();
            Player.TerraStats.DevImmortal = false;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }

        // ------------------------------------------------------------------ Shaman Stone on the spell weapons

        IEnumerator ShamanSpellsTest()
        {
            var st = mod.Stats;
            var tp = TerraProjectiles.Instance;
            var fx = Effects.FxBatch.Instance;
            int crystals = st.ManaCrystals;
            st.ManaCrystals = 9;
            var weapons = new (ItemType Item, ProjKind Kind, float Wait, string Name)[]
            {
                (ItemType.VengefulSpiritFocus, ProjKind.SpiritWave, 0.08f, "白波法术"),
                (ItemType.ShadeSoulFocus, ProjKind.ShadeGhost, 0.45f, "黑波法术"),
                (ItemType.HowlingWraithsFocus, ProjKind.WraithTriad, 0.5f, "白吼法术"),
                (ItemType.AbyssStaff, ProjKind.AbyssTriad, 0.55f, "黑吼法杖"),
            };
            foreach (var w in weapons)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    bool shaman = pass == 1;
                    if (shaman) WearOnly(CharmEffects.ShamanStone); else WearOnly();
                    yield return ToDummy(w.Item == ItemType.VengefulSpiritFocus ? 14f : 5f);
                    InHand(w.Item);
                    st.Mana = st.ManaMax;
                    mod.DevAim = DummyCentre();
                    // The shade ghost goes out while the button is held.
                    mod.DevHold = w.Item == ItemType.ShadeSoulFocus;
                    int grow0 = fx.DevGrowDraws;
                    for (float until = Time.time + 2f; !mod.DevReady && Time.time < until;) yield return null;
                    mod.DevUseSelected(DummyCentre());
                    for (float t = Time.time + w.Wait; Time.time < t;) { st.Mana = st.ManaMax; yield return null; }
                    var (all, grown) = tp.DevGrown(w.Kind);
                    CharmShot(w.Kind + (shaman ? "_shaman" : "_plain"));
                    int grew = fx.DevGrowDraws - grow0;
                    if (shaman) CharmCheck($"{w.Name}：戴萨满之石，弹幕放大画出", all > 0 && grown == all && grew > 0, $"{grown}/{all} 个，放大画了 {grew} 次");
                    else CharmCheck($"{w.Name}：不戴时原样", all > 0 && grown == 0 && grew == 0, $"{grown}/{all} 个，放大画了 {grew} 次");
                    mod.DevHold = false;
                    mod.DevAim = null;
                    yield return new WaitForSeconds(1.2f);
                }
            }
            // The spell's split (right click before it bursts): its little wraiths as big; its blast wider.
            WearOnly(CharmEffects.ShamanStone);
            yield return ToDummy(14f);
            InHand(ItemType.VengefulSpiritFocus);
            st.Mana = st.ManaMax;
            yield return UseAt(DummyCentre(), 0f);
            // The right click once the wave is out (the split needs one flying).
            for (float w = Time.time + 0.6f; tp.DevCount(ProjKind.SpiritWave) == 0 && Time.time < w;) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 分裂前：波 {tp.DevCount(ProjKind.SpiritWave)}，背包开着 {InventoryGui.Open}，魔力 {st.Mana}/{st.ManaMax}");
            InventoryGui.Open = false;
            mod.DevAltPress = true;
            yield return null;
            yield return null;
            var (wraiths, grownWraiths) = tp.DevGrown(ProjKind.SoulWraith);
            CharmShot("split_shaman");
            CharmCheck("白波法术右键分裂的小幽魂也放大", wraiths > 0 && grownWraiths == wraiths, $"{grownWraiths}/{wraiths}");
            yield return new WaitForSeconds(1.5f);
            yield return ToDummy(3f);
            st.Mana = st.ManaMax;
            TerraProjectiles.DevLastBlastPx = 0f;
            yield return UseAt(DummyCentre(), 0.8f);
            CharmCheck("白波法术的爆炸范围也大 30%", Mathf.Abs(TerraProjectiles.DevLastBlastPx - SoulWave.SpellBlastPx * 1.3f) < 0.5f, $"{TerraProjectiles.DevLastBlastPx:F1}（原本 {SoulWave.SpellBlastPx}）");
            st.ManaCrystals = crystals;
            mod.Inventory.Slots[0] = default;
        }

        // ------------------------------------------------------------------ Steady Body: the speed kept, on a mount too

        IEnumerator SteadyKeepTest()
        {
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            WearOnly(CharmEffects.SteadyBody);
            yield return ToDummy(14f);
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.left * 6f);
            yield return new WaitForSeconds(0.4f);
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(0.3f);
            float run = rb.linearVelocity.x;
            rb.linearVelocity = new Vector2(run * 1.6f, rb.linearVelocity.y);
            yield return new WaitForFixedUpdate();
            float fast = rb.linearVelocity.x;
            // Let go a quarter second, then the other way: the speed it had.
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.25f);
            float still = rb.linearVelocity.x;
            Player.TerraMovement.DevMove = -1;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float back = rb.linearVelocity.x;
            CharmCheck("松开时立刻停下", Mathf.Abs(still) < 0.01f, $"{still:F3}");
            CharmCheck("松开 0.25 秒内再按，保持原来的速度", back < -fast * 0.85f, $"跑速 {run:F2}，松开前 {fast:F2}，再按后 {back:F2}");
            // Let go longer than half a second: a start of its own, not the kept speed.
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.7f);
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            float later = rb.linearVelocity.x;
            Player.TerraMovement.DevMove = 0;
            CharmCheck("超过 0.5 秒再按就照常起步，不带之前的速度", later >= 0f && later < fast * 0.5f, $"{later:F2}（跑速 {run:F2}，之前 {fast:F2}）");

            // On a mount: speeding up as it would, turned at once at the speed it had.
            var def = Player.Mounts.All.FirstOrDefault(m => m.Flight == 0 && !m.Hover && !m.Skates && !m.Swim && ProgressionBalance.Unlocked(m.Item));
            if (def == null) { CharmCheck("找到能骑的地面坐骑", false, ""); yield break; }
            yield return new WaitForSeconds(0.5f);
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0) WearOnly(); else WearOnly(CharmEffects.SteadyBody);
                yield return new WaitForSeconds(0.3f);
                hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.left * 4f);
                yield return new WaitForSeconds(0.3f);
                bool on = Player.Mounts.TryMount(def, hero);
                yield return new WaitForSeconds(0.5f);
                if (!on || !Player.Mounts.Riding) { CharmCheck("骑上 " + def.Name, false, ""); yield break; }
                float speed = Mathf.Max(Player.Mounts.Current.Run, Player.Mounts.Current.Dash) * Player.TerraMovement.PxT;
                Player.TerraMovement.DevMove = 1;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                float start = rb.linearVelocity.x;
                yield return new WaitForSeconds(0.3f);
                float pre = rb.linearVelocity.x;
                Player.TerraMovement.DevMove = -1;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                float turned = rb.linearVelocity.x;
                Player.TerraMovement.DevMove = 0;
                if (pass == 0) CharmCheck($"不戴时坐骑（{def.Name}）慢慢加速", start < speed * 0.6f, $"两帧 {start:F2}，满速 {speed:F2}");
                else
                {
                    CharmCheck($"戴着时坐骑（{def.Name}）照常加速", start < speed * 0.6f, $"两帧 {start:F2}，满速 {speed:F2}");
                    CharmCheck("坐骑上也能立刻转向，速度是转向前的", pre > 0.5f && turned < -pre * 0.9f, $"转向前 {pre:F2}，两帧后 {turned:F2}");
                    CharmShot("mount");
                }
                yield return new WaitForSeconds(0.3f);
                Player.Mounts.Dismount();
                yield return new WaitForSeconds(0.4f);
            }
        }

        // ------------------------------------------------------------------ a bench's pylon before its first rest

        IEnumerator PylonDormantTest()
        {
            var pd = PlayerData.instance;
            var hero = HeroController.instance;
            // Dirtmouth's: its altars come after its pylon's place was chosen; a dormant pylon on one moves off it.
            yield return new WaitForSeconds(2.5f);
            var town = World.Pylons.FindDormant("Town", "RestBench");
            if (town != null)
            {
                var o = (Vector2)World.TerraWorld.Live.CellOffset * World.CellGrid.CellSize;
                var tb = new Rect(o + town.Local, new Vector2(World.Pylons.WidthCells, World.Pylons.HeightCells) * World.CellGrid.CellSize);
                CharmCheck("德特茅斯未激活的晶塔不压在祭坛上", !World.Stations.Overlaps(tb), $"{town.Local}，挪过 {World.Pylons.DevDormantMoved} 次");
                CharmShot("town");
            }
            string scene = null;
            foreach (var s in new[] { "Crossroads_47", "Crossroads_04", "Crossroads_30", "Fungus1_01b", "Fungus1_16_alt" })
                if (World.Pylons.Find(s, "RestBench") == null) { scene = s; break; }
            if (scene == null) { CharmCheck("找到还没坐过的长椅", false, ""); yield break; }
            const string bench = "RestBench";
            yield return GoTo(scene, EntryGateOf(scene));
            for (float w = Time.time + 6f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1f);
            var p = World.Pylons.FindDormant(scene, bench);
            CharmCheck($"{scene} 的长椅旁一开始就立着晶塔（未点亮）", p != null && p.Dormant, "");
            if (p == null) yield break;
            var live = World.TerraWorld.Live;
            var corner = (Vector2)live.CellOffset * World.CellGrid.CellSize + p.Local;
            var box = new Rect(corner, new Vector2(World.Pylons.WidthCells, World.Pylons.HeightCells) * World.CellGrid.CellSize);
            CharmCheck("它占着自己的位置", World.Pylons.Occupies(box), $"{box}");
            var foot = World.CellGrid.WorldToCell(new Vector2(box.center.x, box.y + 0.1f));
            CharmCheck("那里放不了方块", !TerraMod.CanPlace(foot), $"{foot}");
            hero.transform.position = Clear(hero, new Vector2(box.center.x - 2.5f, box.y + 0.8f));
            yield return new WaitForSeconds(0.5f);
            int chests = World.Chests.DevPlaced;
            bool chest = World.Chests.TryPlace(hero, new Vector2(box.center.x, box.y + 0.25f));
            CharmCheck("那里放不了箱子", !chest && World.Chests.DevPlaced == chests, "");
            bool bench2 = World.Stations.TryPlace(World.StationKind.WorkBench, hero, new Vector2(box.center.x, box.y + 0.25f));
            CharmCheck("那里放不了家具", !bench2, World.Stations.DevWhy);
            var origin = World.CellGrid.WorldToCell(box.center) - new Vector2Int(World.PrefabHouses.Width(0) / 2, 2);
            bool house = World.PrefabHouses.CanBuild(0, origin, null, out var why);
            CharmCheck("晶塔旁边放不了预制房", !house && why.Contains("晶塔"), why);
            CharmShot("dormant");
            var local = p.Local;
            // Kept in the save: out to the menu and back.
            yield return QuitTest(1);
            yield return new WaitForSeconds(1f);
            var again = World.Pylons.FindDormant(scene, bench);
            CharmCheck("存档记住它的位置", again != null && (again.Local - local).sqrMagnitude < 0.0001f, again != null ? $"{again.Local} / {local}" : "没了");
            // The first rest: lit where it stands.
            yield return GoTo(scene, EntryGateOf(scene));
            for (float w = Time.time + 6f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1f);
            hero = HeroController.instance;
            pd = PlayerData.instance;
            Transform benchAt = null;
            foreach (var go in GameObject.FindGameObjectsWithTag("RespawnPoint")) if (go.name == bench && go.scene.name == scene) benchAt = go.transform;
            if (benchAt != null) hero.transform.position = Clear(hero, (Vector2)benchAt.position);
            yield return new WaitForSeconds(0.4f);
            string scene0 = pd.respawnScene, marker0 = pd.respawnMarkerName;
            pd.respawnScene = scene;
            pd.respawnMarkerName = bench;
            pd.atBench = true;
            int lit0 = World.Pylons.DevDormantLit;
            for (float w = Time.time + 3f; World.Pylons.DevDormantLit == lit0 && Time.time < w;) yield return null;
            pd.atBench = false;
            yield return new WaitForSeconds(0.8f);
            var litP = World.Pylons.Find(scene, bench);
            CharmCheck("坐一下长椅，晶塔在原地点亮", litP != null && World.Pylons.FindDormant(scene, bench) == null && (litP.Local - local).sqrMagnitude < 0.0001f && !litP.Dormant,
                litP != null ? $"{litP.Local} / {local}" : "没点亮");
            CharmShot("lit");
            pd.respawnScene = scene0;
            pd.respawnMarkerName = marker0;
            yield return CharmPlace();
        }
    }
}
