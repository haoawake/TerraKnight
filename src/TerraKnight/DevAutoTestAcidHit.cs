using System.Collections;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "acidhit"): a player's report — standing on the acid in Uumuu's room, struck by its blast, the Knight is
    /// stuck at the bottom of the pool. In Fungus3_archive_02, floating on the acid with Isma's Tear and standing on it
    /// with the Water Walking potion, the Knight is struck (HeroController.TakeDamage, from above, the side and below, as a
    /// blast's DamageHero does); where his feet are against the surface, the hero's Surface Water FSM state, his ground
    /// and control logged every quarter second for three seconds, then a jump to get out.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static string SurfaceFsmState(HeroController hero) =>
            PlayMakerFSM.FindFsmOnGameObject(hero.gameObject, "Surface Water")?.ActiveStateName ?? "无";

        IEnumerator AcidHitTest()
        {
            mod.DevNoFallDamage = true;
            const string room = "Fungus3_archive_02";
            yield return GoTo(room, EntryGateOf(room));
            yield return new WaitForSeconds(2f);
            var pd = PlayerData.instance;
            var hero = HeroController.instance;
            var dry = hero.transform.position;
            Plugin.Log.LogInfo($"[AutoTest] 酸里挨打 进房：{AcidLiquid.DevReport()}");
            bool tear = pd.hasAcidArmour;
            // A blast near Uumuu hits as Uumuu does (PlayerHooks.SourceFactor): life enough to take it.
            mod.Stats.LifeCrystals = Player.TerraStats.MaxLifeCrystals;
            mod.Stats.LifeFruits = Player.TerraStats.MaxLifeFruits;
            var blast = new GameObject("Dev Uumuu Blast");
            // First, unhurt: floating, then a jump (held a while, as a player does) out of the acid.
            pd.hasAcidArmour = true;
            pd.isInvincible = true;
            if (AcidLiquid.DevSurface(out var still))
            {
                hero.transform.position = new Vector3(still.x, still.y + 1.5f, hero.transform.position.z);
                yield return new WaitForSeconds(2f);
                var c0 = hero.GetComponent<BoxCollider2D>();
                float before = c0.bounds.min.y - still.y;
                bool floated = AcidLiquid.Swimming(hero);
                yield return Tap(UnityEngine.InputSystem.Key.Space, 0.35f);
                float top = float.MinValue;
                for (float t = 0f; t < 0.8f; t += Time.deltaTime)
                {
                    top = Mathf.Max(top, c0.bounds.min.y - still.y);
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 酸里挨打 没挨打时漂着跳：漂着 {floated}，脚 {before:F2} → 最高 {top:F2}（相对酸面） {(floated && top > 0.5f ? "通过" : "不通过")}");
            }
            foreach (var (gear, label) in new[] { ("tear", "伊思玛的眼泪漂在酸上"), ("walk", "水上漂站在酸面上") })
                foreach (var side in new[] { GlobalEnums.CollisionSide.top, GlobalEnums.CollisionSide.left, GlobalEnums.CollisionSide.bottom })
                {
                    pd.hasAcidArmour = gear == "tear";
                    PlayerBuffs.Remove(15);
                    if (gear == "walk") PotionBuffs.Give(15, 3600);
                    pd.isInvincible = true;
                    hero.transform.position = dry;
                    yield return new WaitForSeconds(1f);
                    if (!AcidLiquid.DevSurface(out var surface)) { Plugin.Log.LogInfo("[AutoTest] 酸里挨打 没找到酸面 不通过"); yield break; }
                    hero.transform.position = new Vector3(surface.x, surface.y + 1.5f, hero.transform.position.z);
                    yield return new WaitForSeconds(2f);
                    var col = hero.GetComponent<BoxCollider2D>();
                    string State() => $"脚 {col.bounds.min.y - surface.y:+0.00;-0.00}（相对酸面），游泳 {AcidLiquid.Swimming(hero)}，着地 {hero.cState.onGround}，能操作 {GameHooks.PlayerHasControl()}，生命 {mod.Stats.Life}/{mod.Stats.LifeMax}，状态机 {SurfaceFsmState(hero)}";
                    Plugin.Log.LogInfo($"[AutoTest] 酸里挨打 {label} 从{side}挨打前：{State()}");
                    mod.Stats.Life = mod.Stats.LifeMax;
                    pd.isInvincible = false;
                    var h = (Vector2)hero.transform.position;
                    blast.transform.position = side == GlobalEnums.CollisionSide.top ? h + Vector2.up * 2f
                        : side == GlobalEnums.CollisionSide.bottom ? h + Vector2.down * 2f : h + Vector2.left * 2f;
                    hero.TakeDamage(blast, side, 1, 1);
                    var trace = new System.Text.StringBuilder();
                    for (int i = 0; i < 12; i++)
                    {
                        yield return new WaitForSeconds(0.25f);
                        pd.isInvincible = true;
                        trace.Append($"\n    {0.25f * (i + 1):F2}s {State()}");
                    }
                    yield return Tap(UnityEngine.InputSystem.Key.Space, 0.35f);
                    float below = float.MinValue;
                    for (float t = 0f; t < 0.8f; t += Time.deltaTime)
                    {
                        below = Mathf.Max(below, col.bounds.min.y - surface.y);
                        yield return null;
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 酸里挨打 重新浮起 {AcidLiquid.DevRefloats} 次");
                    Plugin.Log.LogInfo($"[AutoTest] 酸里挨打 {label} 从{side}挨打后：{trace}\n    跳一下：脚最高 {below:F2}，{State()} {(below > 0.3f ? "通过" : "不通过")}");
                    Shot(room, $"acidhit_{gear}_{side}");
                }
            Object.Destroy(blast);
            PlayerBuffs.Remove(15);
            pd.hasAcidArmour = tear;
        }
    }
}
