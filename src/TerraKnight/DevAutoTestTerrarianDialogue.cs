using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TeamCherry.Localization;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator TerrarianDialogueTest()
        {
            if (!DevCopy.Verified) throw new System.InvalidOperationException("Isolated saves required");
            // Scene warps are test setup, not a fall from the previous scene.
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            int all = 0, ok = 0;
            var failures = new List<string>();
            void Check(string label, bool pass)
            {
                all++; if (pass) ok++; else failures.Add(label);
                if (!pass) Plugin.Log.LogError("[AutoTest] 泰拉身份 " + label + " 不通过");
            }
            var originalLanguage = Language.CurrentLanguage();
            var languageField = AccessTools.Field(typeof(Language), "_currentLanguage");
            var sheetsField = AccessTools.Field(typeof(Language), "_currentEntrySheets");
            var originals = (Dictionary<string, Dictionary<string, string>>)sheetsField.GetValue(null);
            var rows = TerrarianDialogue.All;
            var exported = new List<object>();
            // Change only the in-memory selector, never SwitchLanguage / saved preferences.
            try
            {
                foreach (var row in rows)
                {
                    int split = row.Key.IndexOf('/');
                    string sheet = row.Key.Substring(0, split), key = row.Key.Substring(split + 1);
                    Check("native key " + row.Key, originals.ContainsKey(sheet) && originals[sheet].ContainsKey(key));
                    foreach (var code in new[] { LanguageCode.ZH, LanguageCode.ZH_TW, LanguageCode.EN,
                        LanguageCode.FR, LanguageCode.DE, LanguageCode.ES, LanguageCode.IT,
                        LanguageCode.PT, LanguageCode.RU, LanguageCode.JA, LanguageCode.KO })
                    {
                        languageField.SetValue(null, code);
                        string expected = code == LanguageCode.ZH || code == LanguageCode.ZH_TW ? row.Value.Zh : row.Value.En;
                        string actual = Language.Get(key, sheet);
                        Check("Language.Get " + code + " " + row.Key, actual == expected && !string.IsNullOrWhiteSpace(actual));
                    }
                    exported.Add(new { Id = row.Key, row.Value.Zh, row.Value.En, row.Value.Reason });
                }
                languageField.SetValue(null, LanguageCode.ZH);
                foreach (var sheet in originals)
                    foreach (var item in sheet.Value)
                        if (!rows.ContainsKey(sheet.Key + "/" + item.Key))
                            Check("preserved " + sheet.Key + "/" + item.Key, Language.Get(item.Key, sheet.Key) == item.Value);
            }
            finally { languageField.SetValue(null, originalLanguage); }

            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1f);
            var boxes = Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b != null && AccessTools.Field(typeof(DialogueBox), "textMesh").GetValue(b) != null).ToArray();
            Check("native dialogue boxes found", boxes.Length > 0);
            bool wasBroadcast = DialogueBroadcast.Setting.Value;
            try
            {
                foreach (bool broadcast in new[] { false, true })
                {
                    DialogueBroadcast.Setting.Value = broadcast;
                    foreach (var box in boxes)
                        foreach (string id in new[] { "Hornet/HORNET_ABYSS_ASCENT_01", "Lore Tablets/TUT_TAB_01",
                            "Lore Tablets/SENSE_TAB_01", "Minor NPC/KING_ABYSS_FINAL", "Dreamers/DREAMERS_EC2_1" })
                        {
                            int slash = id.IndexOf('/');
                            box.SetConversation(id.Substring(slash + 1), id.Substring(0, slash));
                            var mesh = AccessTools.Field(typeof(DialogueBox), "textMesh").GetValue(box);
                            string actual = AccessTools.Property(mesh.GetType(), "text").GetValue(mesh, null) as string;
                            Check("native text mesh " + box.name + " broadcast=" + broadcast + " " + id,
                                actual == Language.Get(id.Substring(slash + 1), id.Substring(0, slash)));
                            box.HideText();
                        }
                }
                DialogueBroadcast.Setting.Value = true;
                var plain = boxes.FirstOrDefault(b => b.name == "Text");
                Check("plain dialogue box found", plain != null);
                if (plain != null)
                {
                    DialogueBroadcast.Clear();
                    int told = DialogueBroadcast.DevTold;
                    plain.StartConversation("TUT_TAB_01", "Lore Tablets");
                    yield return null;
                    Check("tablet broadcast pipeline", DialogueBroadcast.DevTold == told + 1
                        && DialogueBroadcast.DevLastConvo == "TUT_TAB_01");
                    float until = Time.realtimeSinceStartup + 12f;
                    while (DialogueBroadcast.DevWaiting > 0 && Time.realtimeSinceStartup < until) yield return null;
                    Check("tablet broadcast drained", DialogueBroadcast.DevWaiting == 0);
                }
            }
            finally { DialogueBroadcast.Setting.Value = wasBroadcast; HudDialogue.Close(); DialogueBroadcast.Clear(); }

            // Exercise real interaction triggers, beyond merely assigning a mesh.
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1f);
            var elder = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Elderbug");
            Check("Elderbug encounter found", elder != null);
            if (elder != null)
            {
                DialogueBroadcast.Setting.Value = true;
                HeroController.instance.transform.position = Clear(HeroController.instance,
                    (Vector2)elder.position + new Vector2(-1.5f, 0.5f));
                yield return new WaitForSeconds(1f);
                int told = DialogueBroadcast.DevTold;
                for (int i = 0; i < 6 && DialogueBroadcast.DevTold == told; i++)
                {
                    yield return Tap(UnityEngine.InputSystem.Key.W, 0.2f);
                    yield return new WaitForSeconds(1f);
                }
                Check("Elderbug native interaction", DialogueBroadcast.DevTold > told);
                Check("Elderbug interaction uses adapted key", TerrarianDialogue.All.ContainsKey("Elderbug/" + DialogueBroadcast.DevLastConvo));
                Plugin.Log.LogInfo("[AutoTest] 泰拉身份 老虫子实际会话 " + DialogueBroadcast.DevLastConvo);
                while (DialogueBroadcast.DevWaiting > 0) yield return null;
                HudDialogue.Close(); DialogueBroadcast.Clear();
            }
            yield return GoTo("Tutorial_01", EntryGateOf("Tutorial_01"));
            yield return new WaitForSeconds(1f);
            var tablets = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.IndexOf("Tablet", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && t.gameObject.activeInHierarchy).ToArray();
            Plugin.Log.LogInfo("[AutoTest] 泰拉身份 原生石碑物件 " + string.Join("; ", tablets.Select(t => t.name + " " + t.position)));
            Check("native tablet encounter found", tablets.Length > 0);
            if (tablets.Length > 0)
            {
                var tablet = tablets[0];
                HeroController.instance.transform.position = Clear(HeroController.instance,
                    (Vector2)tablet.position + new Vector2(-0.5f, 0.5f));
                yield return new WaitForSeconds(1f);
                int told = DialogueBroadcast.DevTold;
                for (int i = 0; i < 6 && DialogueBroadcast.DevTold == told; i++)
                {
                    yield return Tap(UnityEngine.InputSystem.Key.W, 0.2f);
                    yield return new WaitForSeconds(1f);
                }
                Check("tablet native interaction", DialogueBroadcast.DevTold > told);
                Check("tablet interaction uses adapted key", TerrarianDialogue.All.ContainsKey("Lore Tablets/" + DialogueBroadcast.DevLastConvo));
                Plugin.Log.LogInfo("[AutoTest] 泰拉身份 石碑实际会话 " + DialogueBroadcast.DevLastConvo);
                while (DialogueBroadcast.DevWaiting > 0) yield return null;
                HudDialogue.Close(); DialogueBroadcast.Clear();
            }
            DialogueBroadcast.Setting.Value = wasBroadcast;

            Check("player display name Chinese", Lang.Lookup("泰拉人", "en-US") == "Terrarian");
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "terrarian-dialogue-runtime.json"),
                JsonConvert.SerializeObject(new { Overrides = exported, Passed = ok, Total = all, Failures = failures }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 泰拉身份台词与碑文 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
