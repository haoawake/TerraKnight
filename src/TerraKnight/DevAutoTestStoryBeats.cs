using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "storybeats"): Hollownest's story beats (Items/StoryBeatsHollownest.cs), with the charm page on. Real rises
    /// of a key item, a count and the notches each said; the first charm got (every charm taken away, then one) opening the
    /// inventory on the charm page with its button pointed out (a close-up), and that charm's own beat said once the
    /// inventory shuts; then every one of the beats taken as risen and said, its lines and tip logged to read. The talk is
    /// dropped between them; the save's records, flags, page setting and beats setting put back.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator StoryBeatsTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 小剧情 {what} {(pass ? "通过" : "不通过")}");
            }
            bool page0 = Charms.Enabled, on0 = StoryBeats.On;
            var granted0 = mod.GrantedAbilities.ToList();
            bool lantern0 = pd.hasLantern;
            int keys0 = pd.simpleKeys, slots0 = pd.charmSlots;
            var charms0 = new bool[Charms.Count + 1];
            for (int id = 1; id <= Charms.Count; id++) charms0[id] = pd.GetBool("gotCharm_" + id);
            string Flag(string id) => "story.beat.v1." + id;
            // Waits for that beat to start (another one starting first is dropped); its flag set.
            IEnumerator SaidOf(string id, float within, System.Action<bool> done)
            {
                for (float t = Time.realtimeSinceStartup + within; Time.realtimeSinceStartup < t;)
                {
                    if (mod.GrantedAbilities.Contains(Flag(id))) break;
                    if (StoryBeats.Talking) StoryBeats.DevHush();
                    yield return null;
                }
                // A frame for its first line.
                yield return null;
                done(mod.GrantedAbilities.Contains(Flag(id)));
            }
            try
            {
                Charms.DevSetPage(true);
                StoryBeats.SetOn(true);
                InventoryGui.Open = false;
                yield return null;
                yield return null;

                // Real rises.
                var rises = new (string Id, System.Action Lower, System.Action Raise)[]
                {
                    ("lantern", () => pd.hasLantern = false, () => pd.hasLantern = true),
                    ("simplekey", () => pd.simpleKeys = 0, () => pd.simpleKeys = 1),
                    ("notch", () => { }, () => pd.charmSlots = slots0 + 1),
                };
                foreach (var (id, lower, raise) in rises)
                {
                    mod.GrantedAbilities.Remove(Flag(id));
                    lower();
                    yield return null;
                    yield return null;
                    raise();
                    bool said = false;
                    yield return SaidOf(id, 10f, s => said = s);
                    Check(said, $"真实获得 {id} 时说了：{StoryBeats.DevLast}");
                    StoryBeats.DevHush();
                }

                // The first charm: every charm taken away, then one got.
                for (int id = 1; id <= Charms.Count; id++) pd.SetBool("gotCharm_" + id, false);
                mod.GrantedAbilities.Remove(Flag("charm"));
                mod.GrantedAbilities.Remove(Flag("charm2"));
                yield return null;
                yield return null;
                int intros0 = InventoryGui.DevCharmIntros;
                pd.SetBool("gotCharm_2", true);
                for (float t = Time.realtimeSinceStartup + 10f; InventoryGui.DevCharmIntros == intros0 && Time.realtimeSinceStartup < t;) yield return null;
                yield return new WaitForSeconds(0.6f);
                Check(InventoryGui.DevCharmIntros == intros0 + 1 && InventoryGui.Open && InventoryGui.CharmIntro && InventoryGui.DevEquipPage == 3,
                    $"第一次拿到护符：打开背包 {InventoryGui.Open}，在护符页 {InventoryGui.DevEquipPage == 3}，指出按钮 {InventoryGui.CharmIntro}");
                Shot("Town", "charm_intro");
                yield return new WaitForSeconds(0.3f);
                bool early = mod.GrantedAbilities.Contains(Flag("charm2"));
                InventoryGui.Open = false;
                bool charmSaid = false;
                yield return SaidOf("charm2", 10f, s => charmSaid = s);
                Check(!early && charmSaid && !InventoryGui.CharmIntro, $"关上背包后才说这枚护符的小剧情（背包开着时没说 {!early}）：{StoryBeats.DevLast}");
                StoryBeats.DevHush();

                // Every beat.
                var ids = StoryBeats.DevHollownestIds();
                int saidCount = 0;
                var missing = new System.Collections.Generic.List<string>();
                foreach (var id in ids)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 小剧情 台词 {id}：{StoryBeats.DevText(id)}");
                    mod.GrantedAbilities.Remove(Flag(id));
                    StoryBeats.DevRise(id);
                    bool s = false;
                    yield return SaidOf(id, 6f, v => s = v);
                    if (s) saidCount++;
                    else missing.Add(id);
                    StoryBeats.DevHush();
                }
                Check(saidCount == ids.Count && ids.Count >= 70, $"每一段都说得出来 {saidCount}/{ids.Count}" + (missing.Count > 0 ? "；没说的：" + string.Join("、", missing) : ""));
                Check(ids.Distinct().Count() == ids.Count, $"没有重复的 id（{ids.Count} 段）");
            }
            finally
            {
                InventoryGui.Open = false;
                StoryBeats.DevHush();
                pd.hasLantern = lantern0;
                pd.simpleKeys = keys0;
                pd.charmSlots = slots0;
                for (int id = 1; id <= Charms.Count; id++) pd.SetBool("gotCharm_" + id, charms0[id]);
                mod.GrantedAbilities.Clear();
                foreach (var g in granted0) mod.GrantedAbilities.Add(g);
                StoryBeats.SetOn(on0);
                Charms.DevSetPage(page0);
                Plugin.Log.LogInfo($"[AutoTest] 小剧情 结果 {ok}/{all}");
            }
        }
    }
}
