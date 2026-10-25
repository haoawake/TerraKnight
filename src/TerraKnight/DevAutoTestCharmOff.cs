using System.Collections;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "charmoff"): the user's "为什么格林之子护符摘不下来 … 我是说护符栏里的格林护符" — sitting on Dirtmouth's
    /// bench with the inventory open on the charm page, the Grimmchild put on, then clicked in the worn row with the dev
    /// pointer (the page's own click path); the same for the Wayward Compass; each watched for three seconds after, its
    /// record, the worn list and the flying Grimmchild logged. Close-ups charmoff_*.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CharmOffTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            var hero = HeroController.instance;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 摘护符 {what} {(pass ? "通过" : "不通过")}");
            }
            bool page0 = Charms.Enabled;
            string State(int id) => $"戴着 {Charms.Worn(id)}，列表 [{string.Join(",", pd.equippedCharms)}]，凹槽 {pd.charmSlotsFilled}/{pd.charmSlots}，坐着 {pd.atBench}，格林之子在飞 {GameObject.FindWithTag("Grimmchild") != null}";
            try
            {
                Charms.DevSetPage(true);
                pd.SetBool("gotCharm_40", true);
                if (pd.grimmChildLevel < 1) pd.grimmChildLevel = 2;
                pd.SetBool("gotCharm_2", true);
                if (pd.charmSlots < 3) pd.charmSlots = 3;
                yield return null;

                // Sit on the bench.
                PlayMakerFSM bench = null;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    if (f.FsmName == "Bench Control" && f.gameObject.scene.name == "Town") { bench = f; break; }
                Check(bench != null, "找到德特茅斯的长椅");
                if (bench == null) yield break;
                var bp = (Vector2)bench.transform.position;
                float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
                hero.transform.position = Clear(hero, new Vector2(bp.x + 0.3f, bp.y - bottom + 0.3f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                for (int k = 0; k < 3 && !pd.atBench; k++)
                {
                    yield return Tap(Key.W, 0.3f);
                    yield return Tap(Key.UpArrow, 0.3f);
                    yield return new WaitForSeconds(2f);
                }
                if (!pd.atBench)
                {
                    // The bench's machine: which event sits the Knight down from where it waits.
                    foreach (var st in bench.FsmStates)
                        if (st.Name == bench.ActiveStateName)
                            foreach (var tr in st.Transitions) Plugin.Log.LogInfo($"[AutoTest] 摘护符 长椅 {st.Name} --{tr.EventName}--> {tr.ToState}");
                    foreach (var st in bench.FsmStates)
                        if (st.Name == bench.ActiveStateName)
                            foreach (var tr in st.Transitions)
                                if (!pd.atBench && tr.EventName != "FINISHED") { bench.SendEvent(tr.EventName); yield return new WaitForSeconds(2.5f); Plugin.Log.LogInfo($"[AutoTest] 摘护符 发 {tr.EventName}：坐着 {pd.atBench}，长椅 {bench.ActiveStateName}"); }
                }
                Plugin.Log.LogInfo($"[AutoTest] 摘护符 坐上长椅 {pd.atBench}（长椅 {bench.ActiveStateName}）；没坐上时用测试开关在原地换");
                if (!pd.atBench) Charms.DevAnywhere = true;

                InventoryGui.Open = true;
                InventoryGui.DevEquipPage = 3;
                yield return new WaitForSeconds(0.5f);
                foreach (int id in new[] { 40, 2 })
                {
                    if (!Charms.Worn(id)) Plugin.Log.LogInfo($"[AutoTest] 摘护符 戴上 {id}：{InventoryGui.DevClickCharm(id)}");
                    yield return new WaitForSeconds(1.5f);
                    Plugin.Log.LogInfo($"[AutoTest] 摘护符 戴着 {id}：{State(id)}");
                    int index = pd.equippedCharms.IndexOf(id);
                    var rect = InventoryGui.DevHitRect("CharmWorn", index);
                    Shot("Town", "charmoff_worn_" + id);
                    bool wornBefore = Charms.Worn(id);
                    if (rect.HasValue)
                    {
                        UI.TerraCursor.DevDrives = true;
                        UI.TerraCursor.DevAt = rect.Value.center;
                        yield return null;
                        UI.TerraCursor.DevClick();
                        yield return new WaitForSeconds(0.6f);
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 摘护符 点了已装备栏第 {index} 格（{(rect.HasValue ? rect.Value.ToString() : "没有这一格")}）：{State(id)}");
                    bool off = !Charms.Worn(id);
                    bool stays = off;
                    for (float t = Time.realtimeSinceStartup + 3f; stays && Time.realtimeSinceStartup < t;)
                    {
                        if (Charms.Worn(id) || pd.equippedCharms.Contains(id)) { stays = false; Plugin.Log.LogInfo($"[AutoTest] 摘护符 {id} 又被戴上了：{State(id)}"); }
                        yield return null;
                    }
                    Shot("Town", "charmoff_after_" + id);
                    UI.TerraCursor.DevDrives = false;
                    Check(wornBefore && off && stays, $"已装备栏里点 {Charms.Name(id)} 能摘下并保持摘下（3 秒后 {State(id)}）");
                }
            }
            finally
            {
                UI.TerraCursor.DevDrives = false;
                Charms.DevAnywhere = false;
                InventoryGui.Open = false;
                Charms.DevSetPage(page0);
                Plugin.Log.LogInfo($"[AutoTest] 摘护符 结果 {ok}/{all}");
            }
        }
    }
}
