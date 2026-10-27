using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "featherdown", the user's 10-26 "羽落药剂按下键的时候居然会摔死"): Dirtmouth, the player let go far above
    /// the ground (TK_FD_TILES, 80) three ways: Featherfall with down held, Featherfall alone, down held without it; the life
    /// lost on landing, the fall's own line and the fastest fall. Lines "[AutoTest] 羽落 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FeatherDownTest()
        {
            mod.DevNoFallDamage = false;
            Player.TerraStats.DevImmortal = false;
            int tiles = int.TryParse(System.Environment.GetEnvironmentVariable("TK_FD_TILES"), out var t) ? t : 80;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            Vector2 ground = hero.transform.position + Vector3.right * 20f;
            int ok = 0, all = 0;
            foreach (var (feather, down, name) in new[] { (true, true, "羽落+按住下键"), (true, false, "只有羽落"), (false, true, "没羽落+按住下键") })
            {
                mod.Stats.Life = mod.Stats.LifeMax;
                Player.PlayerBuffs.Clear();
                if (feather) Player.PlayerBuffs.Add(Player.PotionBuffs.Featherfall, 36000);
                yield return new WaitForSeconds(0.3f);
                Player.TerraMovement.DevDown = down;
                rb.linearVelocity = Vector2.zero;
                hero.transform.position = new Vector3(ground.x, ground.y + tiles * World.CellGrid.CellSize, hero.transform.position.z);
                mod.ResetFall();
                TerraMod.DevLastFall = null;
                int life0 = mod.Stats.Life;
                float fastest = 0f, began = Time.time;
                yield return new WaitForSeconds(0.3f);
                while (!hero.cState.onGround && Time.time - began < 40f)
                {
                    fastest = Mathf.Max(fastest, -rb.linearVelocity.y);
                    yield return null;
                }
                yield return new WaitForSeconds(0.5f);
                Player.TerraMovement.DevDown = false;
                int lost = life0 - mod.Stats.Life;
                bool expect = !feather;
                all++;
                if ((lost > 0) == expect) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 羽落 {name}：掉血 {lost}（{life0} → {mod.Stats.Life}），用时 {Time.time - began:F1}s，最快下落 {fastest:F1}，摔落记录 {TerraMod.DevLastFall ?? "无"}，" +
                    $"缓降 {Items.Equipment.Current.SlowFall} {((lost > 0) == expect ? "通过" : "不通过")}");
                if (mod.Stats.IsDead) yield return new WaitForSeconds(8f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 羽落 合计 {ok}/{all}");
        }
    }
}
