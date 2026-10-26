using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fix501"): the user's 5.0.0 reports. Geo comes straight in from the grab range (the game's own pieces and
    /// the Terraria piles, the base range, the Treasure Magnet and the Gold Ring); bars made back from their fences keep
    /// their price; a Moss Charger knocked about out of its charge comes down; a minion's blow on a Moss Knight's shield
    /// isn't answered; a minion's blow wakes the hanging Vengefly King.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly AccessTools.FieldRef<HealthManager, GameObject> fixSmallGeo = AccessTools.FieldRefAccess<HealthManager, GameObject>("smallGeoPrefab");

        IEnumerator Fix501Test()
        {
            if (!DevCopy.Verified) yield break;
            int pass = 0, all = 0;
            void Check(string name, bool ok, string detail = "")
            {
                all++; if (ok) pass++;
                Plugin.Log.LogInfo($"[AutoTest] 修复501 {name} {(ok ? "通过" : "不通过")} {detail}");
            }
            var only = (System.Environment.GetEnvironmentVariable("TK_FIX501") ?? "").Split(',').Where(k => k.Length > 0).ToArray();
            bool Want(string k) => only.Length == 0 || only.Contains(k);
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;

            // 1. Prices: a bar keeps its value although its fence is made back into it.
            if (Want("price"))
            {
            foreach (var t in new[] { ItemType.IronBar, ItemType.LeadBar, ItemType.CopperBar, ItemType.Cog })
                Check("卖价 " + ItemDb.Get(t).Name, Shop.SellValue(ItemStack.Of(t)) > 0, $"卖 {Shop.SellValue(ItemStack.Of(t))} 铜，价值 {ItemDb.Get(t).Value}");
            Check("木头照旧不值钱的东西做不出钱", Shop.SellValue(ItemStack.Of(ItemType.WoodenFence)) == 0);
            var oldFree = new HashSet<ItemType>();
            foreach (var d in ItemDb.All) if (d != null && d.Type != ItemType.None && d.Value <= 0) oldFree.Add(d.Type);
            for (bool more = true; more;)
            {
                more = false;
                foreach (var r in Recipes.All)
                {
                    if (r.IsBought || oldFree.Contains(r.Result)) continue;
                    if (r.Ingredients.All(i => oldFree.Contains(i.Item1))) { oldFree.Add(r.Result); more = true; }
                }
            }
            var priced = ItemDb.All.Where(d => d != null && d.Value > 0 && oldFree.Contains(d.Type) && !Recipes.Worthless(d.Type)).Select(d => d.Name).ToList();
            Plugin.Log.LogInfo($"[AutoTest] 修复501 重新有卖价的 {priced.Count} 件：{string.Join("、", priced.Take(60))}");

            }
            // 2. Geo.
            if (Want("geo"))
            {
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            GameObject prefab = null;
            foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if ((prefab = fixSmallGeo(h)) != null) break;
            Check("找到吉欧预制体", prefab != null);
            var body = hero.GetComponent<Rigidbody2D>();
            var spot = Clear(hero, (Vector2)hero.transform.position + Vector2.right * 3f);
            void Wear(ItemType t)
            {
                System.Array.Clear(inv.Accessories, 0, inv.Accessories.Length);
                foreach (var l in inv.Loadouts) System.Array.Clear(l.Accessories, 0, l.Accessories.Length);
                if (t != ItemType.None) inv.Accessories[0] = ItemStack.Of(t);
                Equipment.Update(inv);
            }
            long Money() => Currencies.Held(inv, ItemType.Geo) + Currencies.Banked(inv, ItemType.Geo);
            var cases = new (string name, ItemType acc, float dx, bool comes)[]
            {
                ("不戴饰品 2 格内", ItemType.None, 2f, true),
                ("不戴饰品 6 格外", ItemType.None, 6f, false),
                ("宝藏磁石 6.5 格", ItemType.TreasureMagnet, 6.5f, true),
                ("金戒指 10 格", ItemType.GoldRing, 10f, true),
            };
            if (prefab != null)
                foreach (var c in cases)
                {
                    Wear(c.acc);
                    hero.transform.position = spot;
                    body.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.5f);
                    // The game's own piece.
                    int touched0 = AccessoryEffects.DevGeoTouched;
                    long before = Money();
                    var centre = hero.GetComponent<BoxCollider2D>().bounds.center;
                    var g = Object.Instantiate(prefab, new Vector3(centre.x + c.dx, centre.y + 0.3f, 0.0015f), Quaternion.identity);
                    g.SetActive(true);
                    float t0 = Time.time, got = -1f, nearest = 99f, onHim = 0f;
                    while (Time.time - t0 < 3f)
                    {
                        if (g == null || !g.activeInHierarchy) { got = Time.time - t0; break; }
                        var hb = hero.GetComponent<BoxCollider2D>().bounds;
                        float d = Vector2.Distance(g.transform.position, hb.center);
                        nearest = Mathf.Min(nearest, d);
                        if (d < 0.8f) onHim += Time.deltaTime;
                        yield return null;
                    }
                    bool taken = got >= 0f || Money() > before;
                    Check("原版吉欧 " + c.name + (c.comes ? " 吸进来收下" : " 不吸"), taken == c.comes,
                        $"用时 {got:F2}s，最近 {nearest:F2}，贴身 {onHim:F2}s，接触收下 {AccessoryEffects.DevGeoTouched - touched0}，吉欧 {before}->{Money()}");
                    if (g != null) Object.Destroy(g);
                    // A Terraria pile of geo.
                    before = Money();
                    centre = hero.GetComponent<BoxCollider2D>().bounds.center;
                    WorldItems.Drop(ItemStack.Of(ItemType.Geo, 7), new Vector2(centre.x + c.dx, centre.y + 0.3f));
                    t0 = Time.time; got = -1f;
                    while (Time.time - t0 < 3f)
                    {
                        if (Money() >= before + 7) { got = Time.time - t0; break; }
                        yield return null;
                    }
                    Check("掉落的吉欧 " + c.name + (c.comes ? " 吸进来收下" : " 不吸"), (got >= 0f) == c.comes, $"用时 {got:F2}s，吉欧 {before}->{Money()}");
                    if (!c.comes) WorldItems.SweepPickup((Vector2)centre + Vector2.right * c.dx, 2f, GameManager.instance.sceneName);
                }
            Wear(ItemType.None);

            }
            // 3. A Moss Charger knocked about out of its charge comes down and goes on.
            if (Want("charger"))
            {
            yield return GoTo("Fungus1_10", EntryGateOf("Fungus1_10"));
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            var chargers = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .Where(h => !h.isDead && EnemyStats.Of(h)?.Record == "MossCharger").OrderBy(h => h.name.Contains("(TK)") ? 1 : 0).ToList();
            Check("找到苔藓冲锋者", chargers.Count > 0, string.Join("、", chargers.Select(h => h.name)));
            foreach (var charger in chargers.Take(4))
            {
                var fsm = charger.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Mossy Control");
                var rb = charger.GetComponent<Rigidbody2D>();
                var anim = charger.GetComponent<tk2dSpriteAnimator>();
                string Anim() => anim == null ? "无动画" : $"片段 {anim.CurrentClip?.name} 播放 {anim.Playing} 暂停 {anim.Paused} 帧 {anim.CurrentFrame}/{anim.CurrentClip?.frames?.Length} fps {anim.ClipFps:F1} 循环 {anim.CurrentClip?.wrapMode}";
                for (int round = 0; round < 2 && fsm != null; round++)
                {
                    charger.hp = 99999;
                    hero.transform.position = Clear(hero, (Vector2)charger.transform.position + Vector2.left * 3f);
                    float until = Time.time + 12f;
                    while (fsm.ActiveStateName != "Charge" && Time.time < until) yield return null;
                    if (fsm.ActiveStateName != "Charge") { Check($"{charger.name} 第{round + 1}轮 等到冲锋", false, fsm.ActiveStateName + "，" + Anim()); continue; }
                    int kept0 = KnockFlight.DevGravityKept;
                    var visited = new List<string>();
                    var trace = new List<string>();
                    float y0 = charger.transform.position.y, t0 = Time.time, nextTrace = 0f;
                    var body = charger.GetComponent<Collider2D>();
                    void Trace()
                    {
                        if (charger == null || Time.time < nextTrace) return;
                        nextTrace = Time.time + 0.05f;
                        var fl = charger.GetComponent<KnockFlight>();
                        trace.Add($"{Time.time - t0:F2} {fsm.ActiveStateName} y{charger.transform.position.y - y0:+0.00;-0.00} v{rb.linearVelocity.y:F1} g{rb.gravityScale:F1} 碰{(body != null && body.enabled ? 1 : 0)}{(rb.bodyType == RigidbodyType2D.Kinematic ? "运" : "")} 退{(fl != null && fl.Active ? (fl.Airborne ? "空" : "地") : "-")}");
                    }
                    float hitEnd = Time.time + 1.2f, nextHit = 0f;
                    while (Time.time < hitEnd && charger != null && !charger.isDead)
                    {
                        if (Time.time >= nextHit)
                        {
                            nextHit = Time.time + 0.12f;
                            var col = charger.GetComponent<Collider2D>();
                            if (col == null || !col.enabled) col = charger.GetComponentsInChildren<Collider2D>().FirstOrDefault(x => x.enabled && x.GetComponentInParent<HealthManager>() == charger);
                            if (col != null)
                            {
                                float facing = Mathf.Sign(charger.transform.position.x - hero.transform.position.x);
                                mod.Melee.HitWith(col, 1, 8f, 0f, facing, hero.GetComponent<BoxCollider2D>().bounds.center);
                            }
                        }
                        if (visited.Count == 0 || visited[visited.Count - 1] != fsm.ActiveStateName) visited.Add(fsm.ActiveStateName);
                        Trace();
                        yield return null;
                    }
                    float watch = Time.time + 8f, floated = 0f, gettingUp = 0f, lowest = 0f;
                    string getUpAnim = null;
                    while (Time.time < watch && charger != null && !charger.isDead)
                    {
                        string s = fsm.ActiveStateName;
                        if (visited[visited.Count - 1] != s) visited.Add(s);
                        if (s == "In Air" && rb.gravityScale < 0.01f && Mathf.Abs(rb.linearVelocity.y) < 0.05f) floated += Time.deltaTime;
                        if (s == "Get Up") { gettingUp += Time.deltaTime; if (gettingUp > 2f && getUpAnim == null) getUpAnim = Anim(); }
                        lowest = Mathf.Min(lowest, charger.transform.position.y - y0);
                        if (Time.time - t0 < 4f) Trace();
                        yield return null;
                    }
                    bool sank = charger == null || charger.isDead || lowest < -3f;
                    Check($"第{round + 1}轮 没有掉出地面", !sank, $"最低 {lowest:F1}，死了 {charger == null || charger.isDead}");
                    if (sank || charger == null) Plugin.Log.LogInfo("[AutoTest] 修复501 掉落轨迹：" + string.Join(" | ", trace));
                    if (charger == null || charger.isDead) break;
                    string end = fsm.ActiveStateName;
                    bool down = end != "In Air" && !end.StartsWith("Fly") && end != "Burst" && floated < 0.5f;
                    Check($"{charger.name} 第{round + 1}轮 连续击退后落地照常行动", down,
                        $"最后 {end}，重力 {rb.gravityScale}，浮空 {floated:F2}s，记下原版重力 {KnockFlight.DevGravityKept - kept0} 次，经过 {string.Join(">", visited.Take(14))}");
                    Check($"{charger.name} 第{round + 1}轮 起身不卡住", gettingUp < 3f, $"起身用了 {gettingUp:F1}s {getUpAnim}，击退中 {charger.GetComponent<KnockFlight>()?.Active}");
                }
            }

            }
            // 4. A minion's blow on a Moss Knight's raised shield isn't answered.
            if (Want("knight"))
            {
            yield return GoTo("Fungus1_21", EntryGateOf("Fungus1_21"));
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            HealthManager knight = null;
            foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (!h.isDead && EnemyStats.Of(h)?.Record == "MossKnight") { knight = h; break; }
            Check("找到苔藓骑士", knight != null);
            if (knight != null)
            {
                var fsm = knight.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Moss Knight Control");
                knight.hp = 99999;
                hero.transform.position = Clear(hero, (Vector2)knight.transform.position + Vector2.left * 4f);
                float until = Time.time + 12f;
                // This room's knight sleeps until its battle starts.
                if (fsm != null && fsm.ActiveStateName == "Sleep") fsm.SendEvent("WAKE");
                while (fsm != null && !fsm.ActiveStateName.StartsWith("Shield ") && Time.time < until) yield return null;
                string shield = fsm?.ActiveStateName;
                Check("苔藓骑士举盾", shield != null && shield.StartsWith("Shield "), shield);
                if (shield != null && shield.StartsWith("Shield "))
                {
                    var col = knight.GetComponent<Collider2D>();
                    int dropped0 = MinionQuiet.DevDropped;
                    var seen = new List<string>();
                    for (int i = 0; i < 6; i++)
                    {
                        float facing = Mathf.Sign(knight.transform.position.x - hero.transform.position.x);
                        mod.Melee.HitWith(col, 5, 0f, 0f, facing, hero.GetComponent<BoxCollider2D>().bounds.center, autonomous: true);
                        for (float t = 0f; t < 0.1f; t += Time.deltaTime)
                        {
                            if (seen.Count == 0 || seen[seen.Count - 1] != fsm.ActiveStateName) seen.Add(fsm.ActiveStateName);
                            yield return null;
                        }
                    }
                    bool answered = seen.Any(s => s.StartsWith("Block"));
                    Check("召唤物打盾 苔藓骑士不格挡反击", !answered, $"压下事件 {MinionQuiet.DevDropped - dropped0} 个，经过 {string.Join(">", seen)}");
                    // The player's own blow on the shield is answered as ever.
                    while (!fsm.ActiveStateName.StartsWith("Shield ") && Time.time < until + 8f) yield return null;
                    float f2 = Mathf.Sign(knight.transform.position.x - hero.transform.position.x);
                    mod.Melee.HitWith(col, 5, 0f, 0f, f2, hero.GetComponent<BoxCollider2D>().bounds.center);
                    bool blocked = false;
                    for (float t = 0f; t < 0.5f && !blocked; t += Time.deltaTime)
                    {
                        blocked = fsm.ActiveStateName.StartsWith("Block") || fsm.ActiveStateName.StartsWith("Attack");
                        yield return null;
                    }
                    Check("玩家自己打盾 苔藓骑士照常格挡", blocked, fsm.ActiveStateName);
                }
            }

            }
            // 5. The Elder Baldur under a stream of shots still shoots.
            if (Want("elder"))
            {
            yield return GoTo("Crossroads_11_alt", EntryGateOf("Crossroads_11_alt"));
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            HealthManager elder = null;
            foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (!h.isDead && EnemyStats.Of(h)?.Record == "Blocker") { elder = h; break; }
            Check("找到巴德尔长者", elder != null);
            if (elder != null)
            {
                var fsm = elder.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Blocker Control");
                elder.hp = 99999;
                // In its alert range, out of the close range that shuts it.
                float side = 1f;
                Vector3 stand = Vector3.zero;
                foreach (float dx in new[] { 9f, 8f, 10f, 7f, 11f })
                {
                    hero.transform.position = Clear(hero, (Vector2)elder.transform.position + Vector2.right * dx * side);
                    yield return new WaitForSeconds(2.5f);
                    if (fsm.ActiveStateName == "Idle" || fsm.ActiveStateName.StartsWith("Shot") || fsm.ActiveStateName == "Fire") { stand = hero.transform.position; break; }
                    side = -side;
                }
                Check("巴德尔长者张开", stand != Vector3.zero, fsm.ActiveStateName);
                if (stand != Vector3.zero)
                {
                    // Without the gap (as 5.0.0) and with it, eight seconds of a hit every 0.15 s each.
                    var col = elder.GetComponent<Collider2D>();
                    var shots = new int[2];
                    for (int phase = 0; phase < 2; phase++)
                    {
                        FlinchGap.DevOff = phase == 0;
                        int held0 = FlinchGap.DevHeld, hp0 = elder.hp, flinched = 0, landed = 0;
                        string lastState = fsm.ActiveStateName;
                        float end = Time.time + 8f, nextHit = 0f;
                        while (Time.time < end)
                        {
                            if (Time.time >= nextHit)
                            {
                                nextHit = Time.time + 0.15f;
                                if (mod.Melee.HitWith(col, 40, 0f, 0f, Mathf.Sign(elder.transform.position.x - hero.transform.position.x), hero.GetComponent<BoxCollider2D>().bounds.center)) landed++;
                            }
                            string s = fsm.ActiveStateName;
                            if (s != lastState && s == "Shot Antic") shots[phase]++;
                            if (s != lastState && s == "Hit Pause") flinched++;
                            lastState = s;
                            yield return null;
                        }
                        Plugin.Log.LogInfo($"[AutoTest] 修复501 巴德尔长者 {(phase == 0 ? "不限受击（5.0.0）" : "受击间隔 3 秒")}：出招 {shots[phase]} 次，受击硬直 {flinched} 次，命中 {landed} 次，血 {hp0}->{elder.hp}，压下受击 {FlinchGap.DevHeld - held0} 次");
                    }
                    FlinchGap.DevOff = false;
                    Check("连续攻击下巴德尔长者照样出招", shots[1] >= 2 && shots[1] > shots[0], $"不限 {shots[0]} 次，限 {shots[1]} 次");
                }
            }

            }
            // 6. A minion's blow wakes the Vengefly King hanging from the roof.
            if (Want("king"))
            {
            pd.SetBool("zoteRescuedBuzzer", false);
            pd.SetBool("zoteDead", false);
            yield return GoTo("Fungus1_20_v02", EntryGateOf("Fungus1_20_v02"));
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            HealthManager king = null;
            foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (!h.isDead && h.name.StartsWith("Giant Buzzer")) { king = h; break; }
            Check("找到反击蝇之王", king != null);
            if (king != null)
            {
                var fsm = king.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Big Buzzer");
                hero.transform.position = Clear(hero, (Vector2)king.transform.position + Vector2.down * 5f);
                yield return new WaitForSeconds(1f);
                string from = fsm?.ActiveStateName;
                Check("反击蝇之王挂着算睡着", from == "Hanging" && BossWake.Asleep(king), $"{from} 睡着 {BossWake.Asleep(king)}");
                int hp0 = king.hp;
                var col = king.GetComponent<Collider2D>();
                bool hit = col != null && mod.Melee.HitWith(col, 40, 0f, 0f, 1f, hero.GetComponent<BoxCollider2D>().bounds.center, autonomous: true);
                yield return new WaitForSeconds(0.6f);
                string now = fsm?.ActiveStateName;
                Check("召唤物一击叫醒反击蝇之王", from == "Hanging" && now != "Hanging", $"命中 {hit}，{from} -> {now}，血 {hp0}->{king.hp}");
                Check("叫醒那一击只扣一点", hp0 - king.hp <= 1, $"血 {hp0}->{king.hp}");
            }

            }
            pd.isInvincible = false;
            Player.TerraStats.DevImmortal = false;
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 修复501 合计 {pass}/{all} {(pass == all ? "通过" : "不通过")}");
        }
    }
}
