using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossvis_Key"): is the boss to be seen all through its fight (the user's: the False Knight in the air
    /// showed only his health bar)? Fought by its seal; for 40 seconds where it is, the light the light map lays there,
    /// whether that is in its room's grid and in the camera's view, and its renderer; shots at the highest it went.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossVisTest(string key)
        {
            var inv = mod.Inventory;
            var seal = System.Array.Find(BossSummons.All, s => s.Key == key);
            if (seal == null) { Plugin.Log.LogInfo($"[AutoTest] 首领可见 找不到 {key} 的召唤物 不通过"); yield break; }
            Bestiary.Kills[key] = Mathf.Max(1, Bestiary.KillsOf(key));
            inv.Selected = 0;
            inv.Slots[0] = ItemStack.Of(seal.Type, 3);
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1f);
            mod.DevUseSelected(HeroController.instance.transform.position);
            float until = Time.time + 30f;
            while (Time.time < until && !BossArena.Arrived(key)) yield return null;
            yield return new WaitForSeconds(2f);
            string record = key.Split('@')[0];
            float top = float.MinValue;
            int dark = 0, outside = 0, samples = 0, hidden = 0;
            string worst = "";
            var cam = GameCameras.instance.mainCamera;
            for (float t = 0f; t < 40f; t += 0.25f)
            {
                PlayerData.instance.isInvincible = true;
                mod.Stats.FullHeal();
                HealthManager boss = null;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (hm != null && !hm.isDead && hm.hp > 0 && hm.gameObject.activeInHierarchy && UI.EnemyHover.RecordOf(hm) == record && hm.name != "Head") { boss = hm; break; }
                if (boss != null)
                {
                    var r = boss.GetComponent<Renderer>();
                    Vector2 at = r != null ? (Vector2)r.bounds.center : (Vector2)boss.transform.position;
                    float light = LightMap.DevLightAt(at);
                    var cell = CellGrid.WorldToCell(at);
                    bool inGrid = TerraWorld.RegionAt(cell.x, cell.y) != null;
                    var vp = cam.WorldToViewportPoint(at);
                    bool inView = vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
                    bool shown = r != null && r.enabled && r.isVisible;
                    samples++;
                    if (inView && light >= 0f && light < 0.3f) dark++;
                    if (inView && !inGrid) outside++;
                    if (inView && !shown) hidden++;
                    if (at.y > top)
                    {
                        top = at.y;
                        worst = $"{(Vector2)boss.transform.position:F1} 光 {light:F2} 网格内 {inGrid} 视野内 {inView}（{vp.x:F2},{vp.y:F2}） 显示 {shown}";
                        if (inView) Shot(GameManager.instance.sceneName, "bossvis_top");
                    }
                }
                yield return new WaitForSeconds(0.25f);
            }
            var room = TerrainSystem.Current?.Region;
            string grid = room != null ? $"{room.CellRect}" : "-";
            bool pass = samples > 0 && dark == 0 && hidden == 0;
            Plugin.Log.LogInfo($"[AutoTest] 首领可见 {key}：采样 {samples}，视野内太暗 {dark} 次、在网格外 {outside} 次、没画出来 {hidden} 次；最高处 {worst}；房间网格 {grid} {(pass ? "通过" : "不通过")}");
        }
    }
}
