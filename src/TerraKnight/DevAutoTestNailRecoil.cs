using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "nailrecoil"): the player standing still beside a common enemy swings the Nail once (the blow killing it,
    /// then one that doesn't): how far the player moves and how fast, which should be nothing, the nail never throwing him
    /// off an enemy (NailEnemyRecoil).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NailRecoilTest()
        {
            if (!DevCopy.Verified) yield break;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            int pass = 0, all = 0;
            foreach (var scene in new[] { "Crossroads_01", "Crossroads_07", "Crossroads_15", "Tutorial_01" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(2f);
                var hero = HeroController.instance;
                var body = hero.GetComponent<Rigidbody2D>();
                var targets = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .Where(h => !h.isDead && h.gameObject.activeInHierarchy && EnemyStats.Of(h) is var i && i != null && !i.Boss && !i.Elite && h.GetComponent<Collider2D>() != null)
                    .Take(6).ToList();
                int n = 0;
                foreach (var hm in targets)
                {
                    if (hm == null || hm.isDead) continue;
                    bool kill = n++ % 2 == 0;
                    hm.hp = kill ? 1 : 99999;
                    var col = hm.GetComponent<Collider2D>();
                    float side = hero.transform.position.x < hm.transform.position.x ? -1f : 1f;
                    hero.transform.position = Clear(hero, new Vector2(hm.transform.position.x + side * 1.1f, col.bounds.min.y + 0.2f));
                    body.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(1f);
                    if (hm == null || hm.isDead) continue;
                    InHand(ItemType.KnightNail);
                    pd.hasNailArt = false;
                    int hits0 = NailItem.DevHits;
                    NailEnemyRecoil.DevLetThrough = null;
                    NailEnemyRecoil.DevLastCaller = null;
                    NailEnemyRecoil.DevTrace = true;
                    float hurt0 = Player.TerraHurt.LastHurtAt;
                    string name = Bestiary.NameOf(EnemyStats.Of(hm)?.Record ?? "?");
                    float x0 = hero.transform.position.x, moved = 0f, fastest = 0f;
                    for (float t = 0f; t < 1.5f && NailItem.DevHits == hits0 && hm != null && !hm.isDead; t += Time.deltaTime)
                    {
                        if (hm.transform.position.x > hero.transform.position.x) hero.FaceRight(); else hero.FaceLeft();
                        if (col == null) break;
                        NailAim.DevAim = col.bounds.center;
                        NailItem.DevHold = true;
                        yield return null;
                        moved = Mathf.Max(moved, Mathf.Abs(hero.transform.position.x - x0));
                        fastest = Mathf.Max(fastest, Mathf.Abs(body.linearVelocity.x));
                    }
                    NailItem.DevHold = false;
                    NailAim.DevAim = null;
                    bool landed = NailItem.DevHits != hits0;
                    for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                    {
                        moved = Mathf.Max(moved, Mathf.Abs(hero.transform.position.x - x0));
                        fastest = Mathf.Max(fastest, Mathf.Abs(body.linearVelocity.x));
                        yield return null;
                    }
                    all++;
                    bool ok = !landed || moved < 0.6f || Player.TerraHurt.LastHurtAt != hurt0;
                    if (ok) pass++;
                    Plugin.Log.LogInfo($"[AutoTest] 骨钉后坐 {scene} {name}（{(kill ? "打死" : "不打死")}）{(ok ? "通过" : "不通过")} 砍中 {landed}，人物被推 {moved:F2}，最快 {fastest:F1}，放过的 {NailEnemyRecoil.DevLetThrough ?? "无"}，后坐来自 {NailEnemyRecoil.DevLastCaller ?? "无"}，期间受伤 {Player.TerraHurt.LastHurtAt != hurt0}");
                }
            }
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = false;
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 骨钉后坐 合计 {pass}/{all} {(pass == all ? "通过" : "不通过")}");
        }
    }
}
