using System.Collections;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "grimmsky"): the user's "神居王格林房顶有bug，可以无限上天" (10-29). The Godhome halls of the Nightmare King
    /// and of the Troupe Master, entered from the Hall of Gods as a statue does, and the Nightmare King's dream: the scenes'
    /// grids listed, then the player carried up at a wing's pace for 4 s, at once and again with the fight under way; how high
    /// he got against the scene's top and the arena's ceiling (World.ArenaCeiling), what stopped him, a picture at the top.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GrimmSkyTest()
        {
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            foreach (string hall in new[] { "GG_Grimm_Nightmare", "GG_Grimm", "Grimm_Nightmare" })
            {
                if (hall == "Grimm_Nightmare")
                {
                    Items.BossArena.Fight("NightmareGrimm", true);
                    float until = Time.time + 40f;
                    while ((GameManager.instance.sceneName != hall || !GameHooks.PlayerHasControl()) && Time.time < until) yield return null;
                    yield return new WaitForSeconds(2f);
                    Plugin.Log.LogInfo($"[AutoTest] 到了 {GameManager.instance.sceneName}（梦境），可操作={GameHooks.PlayerHasControl()}");
                }
                else
                {
                    yield return GoTo("GG_Workshop", EntryGateOf("GG_Workshop"));
                    yield return GoTo(hall, "door_dreamEnter");
                }
                yield return new WaitForSeconds(1.5f);
                foreach (var t in TerrainSystem.All)
                {
                    if (t.Region == null) continue;
                    var s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(t.Region.SceneName);
                    var r = t.Region.CellRect;
                    Plugin.Log.LogInfo($"[AutoTest] 格林天顶 {hall} 格子 {t.Region.SceneName}{(t.Region == TerraWorld.Live ? "（当前）" : "")}：格 x {r.xMin}~{r.xMax} y {r.yMin}~{r.yMax}"
                        + $"（{r.xMin * CellGrid.CellSize:F1}~{r.xMax * CellGrid.CellSize:F1} × {r.yMin * CellGrid.CellSize:F1}~{r.yMax * CellGrid.CellSize:F1}），场景已载入 {s.IsValid() && s.isLoaded}");
                }
                for (int round = 0; round < 2; round++)
                {
                    if (round == 1) yield return new WaitForSeconds(6f);
                    yield return GrimmSkyFly(hall, round == 0 ? "进门就飞" : "开打 6 秒后飞", round == 1);
                }
            }
            Plugin.Log.LogInfo("[AutoTest] 格林天顶 完成");
        }

        IEnumerator GrimmSkyFly(string hall, string when, bool shot)
        {
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            var live = TerraWorld.Live;
            float top = live != null ? live.CellRect.yMax * CellGrid.CellSize : float.NaN;
            int stops0 = WorldBorder.DevStops, ceiling0 = ArenaCeiling.DevStops;
            float highest = float.MinValue;
            var trail = new System.Collections.Generic.List<string>();
            float next = 0f;
            bool shotTaken = false;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                if (GameHooks.PlayerHasControl()) rb.linearVelocity = new Vector2(0f, 20f);
                highest = Mathf.Max(highest, col.bounds.max.y);
                if (t >= next)
                {
                    next += 0.5f;
                    var c = CellGrid.WorldToCell(col.bounds.center);
                    trail.Add($"{col.bounds.max.y:F1}{(TerraWorld.RegionAt(c.x, c.y) == null ? "（场景外）" : "")}{(GameHooks.PlayerHasControl() ? "" : "（无操作）")}");
                }
                if (shot && !shotTaken && t > 3.2f)
                {
                    shotTaken = true;
                    Shot(hall, "grimmsky_top_" + hall);
                }
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 格林天顶 {hall} {when}：场景顶 {top:F1}，场地顶 {(float.IsNaN(ArenaCeiling.DevTop) ? "无" : ArenaCeiling.DevTop.ToString("F1"))}，"
                + $"头顶飞到最高 {highest:F1}，场地顶挡下 {ArenaCeiling.DevStops - ceiling0} 次，世界边缘挡下 {WorldBorder.DevStops - stops0} 次；每 0.5 秒头顶高度 {string.Join(" ", trail)}");
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(2f);
        }
    }
}
