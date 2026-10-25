using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "p4_v5F_*_zh"): recorder F's clips for the 4.4 trailer: home building and the world (prefab houses, area
    /// furniture, torches and chests, animated stations, block swap and the smart cursor, the shimmer, liquids and fishing,
    /// the Target Dummy, pylons, spikes and the altars). Helpers here; the clips in DevAutoTestPromo4F*.cs.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>A JPG of the screen into dev/&lt;copy&gt;/f5/ (scouting pictures).</summary>
        IEnumerator F5Snap(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            string dir = Path.Combine(outDir, "f5");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".jpg"), tex.EncodeToJPG(80));
            Object.Destroy(tex);
        }

        static void F5Log(string s) => Plugin.Log.LogInfo("[AutoTest] 录制F " + s);

        /// <summary>The hero kept from harm for a take (no Terraria fall damage, no death).</summary>
        void F5Safe()
        {
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            if (PlayerData.instance != null) PlayerData.instance.isInvincible = true;
        }

        // ------------------------------------------------------------------ the camera, the chat, the townsfolk

        bool f5Cam, f5CamInit, f5CamWas;
        Vector2 f5CamOffset, f5CamAt;
        /// <summary>A fixed point the camera eases to instead of the hero (null: the hero plus the offset).</summary>
        Vector2? f5CamFix;
        float f5CamRate = 7f;

        /// <summary>
        /// The centred (Terraria) camera on, eased each frame toward the hero's middle plus an offset (or a fixed point), so
        /// the street sits low in the frame and nothing jumps.
        /// </summary>
        void F5CamOn(Vector2 offset, float rate = 7f)
        {
            f5CamOffset = offset;
            f5CamRate = rate;
            f5CamFix = null;
            if (f5Cam) return;
            f5CamWas = Player.TerraCamera.Enabled;
            Player.TerraCamera.Enabled = true;
            f5Cam = true;
            f5CamInit = false;
            mod.StartCoroutine(F5CamLoop());
        }

        void F5CamOff()
        {
            if (!f5Cam) return;
            f5Cam = false;
            Player.TerraCamera.DevLook = null;
            Player.TerraCamera.Enabled = f5CamWas;
        }

        IEnumerator F5CamLoop()
        {
            while (f5Cam)
            {
                var hero = HeroController.SilentInstance;
                if (hero != null)
                {
                    var col = hero.GetComponent<BoxCollider2D>();
                    Vector2 c = col != null ? (Vector2)hero.transform.TransformPoint(col.offset) : (Vector2)hero.transform.position;
                    Vector2 target = f5CamFix ?? c + f5CamOffset;
                    f5CamAt = f5CamInit ? Vector2.Lerp(f5CamAt, target, 1f - Mathf.Exp(-f5CamRate * Time.deltaTime)) : target;
                    f5CamInit = true;
                    Player.TerraCamera.DevLook = f5CamAt;
                }
                yield return null;
            }
            Player.TerraCamera.DevLook = null;
        }

        /// <summary>The camera put on its target at once (after a teleport, before a take).</summary>
        void F5CamSnap() => f5CamInit = false;

        static System.Collections.IList f5ChatLines;

        /// <summary>The chat's lines gone (a tip shown at the kit's start would sit over the first take).</summary>
        static void F5ClearChat()
        {
            if (f5ChatLines == null)
                f5ChatLines = typeof(Net.NetChat).GetField("lines", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null) as System.Collections.IList;
            f5ChatLines?.Clear();
        }

        /// <summary>
        /// The townsfolk standing between x0 and x1 sent out of it, for a clear view: to the given side (-1 left, 1 right, 0
        /// each its nearer one), `far` units and more past the edge, standing there a while (they roam back after ten seconds).
        /// </summary>
        static int F5Shoo(float x0, float x1, int side = 0, float far = 0.5f)
        {
            int n = 0;
            float mid = (x0 + x1) / 2f;
            foreach (var d in TownNpcs.AllDefs)
            {
                var p = TownNpcs.DevPos(d.Internal);
                if (!p.HasValue || p.Value.x < x0 || p.Value.x > x1) continue;
                bool left = side < 0 || (side == 0 && p.Value.x < mid);
                float x = left ? x0 - far - Random.Range(0f, 6f) : x1 + far + Random.Range(0f, 6f);
                if (TownNpcs.DevSend(d.Internal, x, 0)) n++;
            }
            return n;
        }

        /// <summary>
        /// A walk to x, let go early enough that the run's slide ends there (the boots' speed carries a body on a little:
        /// about an eighth of a second of its speed), the aim eased meanwhile.
        /// </summary>
        IEnumerator F5WalkTo(HeroController hero, float x, float maxTime, System.Func<Vector2> aim = null)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            for (float t = 0f; t < maxTime; t += Time.deltaTime)
            {
                float dx = x - col.bounds.center.x;
                float slide = 0.08f + Mathf.Abs(rb.linearVelocity.x) * 0.12f;
                if (Mathf.Abs(dx) < slide) break;
                Player.TerraMovement.DevMove = dx > 0 ? 1 : -1;
                if (aim != null) SmoothAim(aim(), 8f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            for (float t = 0f; t < 0.35f && Mathf.Abs(rb.linearVelocity.x) > 0.3f; t += Time.deltaTime) { if (aim != null) SmoothAim(aim(), 8f); yield return null; }
        }

        /// <summary>The running boots off for a take (a walk instead of a sprint); the accessories as they were come back from the returned copy.</summary>
        ItemStack[] F5NoBoots()
        {
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Accessories.Clone();
            for (int i = 0; i < inv.Accessories.Length; i++)
                if (!inv.Accessories[i].IsEmpty && (inv.Accessories[i].Type == ItemType.TerrasparkBoots || inv.Accessories[i].Type == ItemType.HermesBoots
                    || inv.Accessories[i].Type == ItemType.LightningBoots || inv.Accessories[i].Type == ItemType.FrostsparkBoots)) inv.Accessories[i] = default;
            inv.RefreshEffective();
            Items.Equipment.Update(inv);
            return saved;
        }

        void F5Accessories(ItemStack[] saved)
        {
            var inv = mod.Inventory;
            for (int i = 0; i < saved.Length && i < inv.Accessories.Length; i++) inv.Accessories[i] = saved[i];
            inv.RefreshEffective();
            Items.Equipment.Update(inv);
        }

        /// <summary>Dirtmouth entered and the hero put by its bench a while (the town's titles shown and gone before any take).</summary>
        IEnumerator F5TownReady()
        {
            yield return EnterRoom("Town");
            float entered = Time.time;
            var hero = HeroController.instance;
            F5Safe();
            F5Street(hero, TownNpcs.DevStreet.x - 3f, 1);
            while (Time.time - entered < 11f) yield return null;
            F5ClearChat();
        }

        /// <summary>The top solid row of the ground under a point (scanning down from it).</summary>
        static int F5GroundRow(float x, float fromY)
        {
            var c = CellGrid.WorldToCell(new Vector2(x, fromY));
            for (int y = c.y; y > c.y - 40; y--) if (TerraWorld.IsSolid(c.x, y)) return y;
            return c.y - 1;
        }

        /// <summary>Scouting: what this save's Dirtmouth, hot spring and chests look like, for planning the takes.</summary>
        IEnumerator P4_v5F_probe(HeroController hero)
        {
            F5Safe();
            yield return EnterRoom("Town");
            hero = HeroController.instance;
            var st = TownNpcs.DevStreet;
            F5Log($"街道 长椅 ({st.x:F1},{st.y:F1}) 街 {st.z:F1}..{st.w:F1}；假人 {TargetDummy.DevAt}；恶魔祭坛 {Stations.DevAltarBox(StationKind.DemonAltar, true)}；猩红祭坛 {Stations.DevAltarBox(StationKind.CrimsonAltar, true)}");
            F5Log($"NPC {TownNpcs.DevDescribe()}；渔夫 {TownNpcs.DevPos("Angler")}；向导 {TownNpcs.DevPos("Guide")}");
            F5Log($"未激活晶塔 {string.Join(" ", Pylons.Dormant.Select(p => p.Scene + "/" + p.Bench + "@" + p.Local))}；已解锁 {Pylons.Unlocked.Count}");
            F5Log($"地区 {string.Join(" ", AreaBlocks.All.Select((a, i) => i + ":" + a.Key + "/" + a.Name))}");
            F5Log("储物箱计划 " + RegionalChests.DevDescribe());
            yield return F5Snap("town_start");
            // Where along the street a house of each width goes.
            int gy = CellGrid.WorldToCell(new Vector2(st.x, st.y + 0.2f)).y;
            for (int dy = -3; dy <= 3; dy++)
            {
                var ok = new List<int>();
                int lo = CellGrid.WorldToCell(new Vector2(st.z - 40f, st.y)).x, hi = CellGrid.WorldToCell(new Vector2(st.w + 40f, st.y)).x;
                for (int x = lo; x <= hi; x++)
                    if (PrefabHouses.CanBuild(4, new Vector2Int(x, gy + dy), hero, out _)) ok.Add(x);
                var runs = new List<string>();
                for (int i = 0; i < ok.Count;)
                {
                    int j = i;
                    while (j + 1 < ok.Count && ok[j + 1] == ok[j] + 1) j++;
                    runs.Add($"{ok[i]}..{ok[j]}(x {CellGrid.CellCenter(ok[i], 0).x:F1}..{CellGrid.CellCenter(ok[j], 0).x:F1})");
                    i = j + 1;
                }
                bool solidBelow = TerraWorld.IsSolid(CellGrid.WorldToCell(new Vector2(st.x, st.y)).x, gy + dy - 1);
                F5Log($"预制房 行 {gy + dy}（下面实心 {solidBelow}）：{string.Join(" ", runs)}");
            }
            // Snapshots along the street.
            var col = hero.GetComponent<BoxCollider2D>();
            float bottom = col.bounds.min.y - hero.transform.position.y;
            for (float x = st.z - 30f; x <= st.w + 30f; x += 14f)
            {
                hero.transform.position = Clear(hero, new Vector2(x, st.y - bottom + 0.3f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1.2f);
                F5Log($"街上 x {x:F1}：主角 {(Vector2)hero.transform.position:F1} 地面 {hero.cState.onGround}");
                yield return F5Snap($"town_x{Mathf.RoundToInt(x)}");
            }

            // The hot spring.
            yield return EnterRoom(Shimmer.Scene);
            hero = HeroController.instance;
            F5Log($"温泉 {GameManager.instance.sceneName} 池 {Shimmer.Pool}；主角 {(Vector2)hero.transform.position:F1}；未激活晶塔 {string.Join(" ", Pylons.Dormant.Where(p => p.Scene == Shimmer.Scene).Select(p => p.Bench + "@" + p.Local))}");
            foreach (var go in GameObject.FindGameObjectsWithTag("RespawnPoint")) F5Log($"长椅 {go.name} {(Vector2)go.transform.position:F1} 场景 {go.scene.name}");
            yield return new WaitForSeconds(2f);
            yield return F5Snap("spring_enter");
            var pool = Shimmer.Pool;
            if (pool.HasValue)
            {
                hero.transform.position = Clear(hero, new Vector2(pool.Value.xMin - 2f, pool.Value.yMax + 0.5f));
                yield return new WaitForSeconds(2f);
                yield return F5Snap("spring_pool");
            }
            yield return P4Kit();
            F5Log($"创造后 已解锁晶塔 {Pylons.Unlocked.Count}，未激活 {Pylons.Dormant.Count}；阶段 {Shop.UnlockedStage()}");
        }
    }
}
