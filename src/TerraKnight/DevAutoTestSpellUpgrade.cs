using System.Collections;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator SpellUpgradeTest()
        {
            if (!DevCopy.Verified) throw new System.InvalidOperationException("Isolated save required");
            int checks = 0, failures = 0;
            void Check(string name, bool ok) { checks++; if (!ok) failures++; Plugin.Log.LogInfo($"[AutoTest] SPELLUP {name} {(ok ? "通过" : "不通过")}"); }
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            PlayerData.instance.fireballLevel = 1;
            PlayerData.instance.screamLevel = 1;
            foreach (string scene in new[] { "Ruins1_31b", "Abyss_12" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(2f);
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (f.gameObject.scene.name == scene) DumpFsm(f);
                if (scene == "Ruins1_31b")
                {
                    var shaman = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .First(f => f.FsmName == "Ruins Shaman");
                    var hero = HeroController.instance;
                    hero.RegainControl(); hero.StartAnimationControl(); hero.FaceRight();
                    Put(hero, (Vector2)shaman.transform.position + new Vector2(-2, 1));
                    yield return new WaitForSecondsRealtime(1f);
                    var binding = Player.TerraKeys.Get(Player.TerraKeys.Action.Interact);
                    try {
                        Player.TerraKeys.Set(Player.TerraKeys.Action.Interact, Key.F8);
                        yield return Tap(Key.F8, .1f);
                    } finally { Player.TerraKeys.Set(Player.TerraKeys.Action.Interact, binding); }
                    float until = Time.realtimeSinceStartup + 30f, next = 0;
                    string last = null;
                    while (Time.realtimeSinceStartup < until)
                    {
                        if (last != shaman.ActiveStateName) { last = shaman.ActiveStateName; Plugin.Log.LogInfo("[AutoTest] SPELLUP shade-state " + last + " hero=" + HeroController.instance.transform.position); }
                        if (Time.realtimeSinceStartup >= next)
                        {
                            next = Time.realtimeSinceStartup + 1f;
                            var choice = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                .FirstOrDefault(f => f.name == "Text YN" && f.FsmName == "Dialogue Page Control");
                            if (choice != null && choice.ActiveStateName == "Ready for Input") yield return SelectTeacherChoice(choice, 1);
                            else yield return Tap(Key.Space, .08f);
                        }
                        yield return null;
                    }
                    Plugin.Log.LogInfo("[AutoTest] SPELLUP shade result=" + PlayerData.instance.fireballLevel);
                    Check("shade-interact-confirm-upgrades", PlayerData.instance.fireballLevel == 2);
                    Check("shade-control-restored", HeroController.instance.CanInput());
                    Check("shade-weapons-granted", mod.GrantedAbilities.Contains("fireballLevel2.shade.v1"));
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (f.name.Contains("Fireball") || f.FsmName == "Ruins Shaman") DumpFsm(f);
                }
                else
                {
                    var altar = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .First(f => f.FsmName == "Scream Get");
                    var hero = HeroController.instance;
                    Check("outside-altar-blocked", !Player.SpellAltars.TryShriek(hero));
                    var zone = altar.GetComponent<Collider2D>();
                    Put(hero, zone.bounds.center);
                    yield return new WaitForSecondsRealtime(1f);
                    Plugin.Log.LogInfo("[AutoTest] SPELLUP shriek-zone=" + (Player.SpellAltars.ShriekAltar(hero) != null) + " grounded=" + hero.cState.onGround);
                    Check("native-altar-zone", Player.SpellAltars.ShriekAltar(hero) != null);
                    PlayerData.instance.screamLevel = 0;
                    Check("without-white-howl-blocked", !Player.SpellAltars.TryShriek(hero));
                    PlayerData.instance.screamLevel = 1;
                    var binding = Player.TerraKeys.Get(Player.TerraKeys.Action.Interact);
                    try {
                        Player.TerraKeys.Set(Player.TerraKeys.Action.Interact, Key.F8);
                        yield return Tap(Key.F8, .1f);
                    } finally { Player.TerraKeys.Set(Player.TerraKeys.Action.Interact, binding); }
                    // SG Antic finishes its native casting animation before the scene receives SCREAM GET.
                    yield return new WaitForSecondsRealtime(1.5f);
                    Check("interaction-starts-native-ceremony", altar.ActiveStateName != "In" && altar.ActiveStateName != "Idle");
                    yield return new WaitForSecondsRealtime(23f);
                    Plugin.Log.LogInfo("[AutoTest] SPELLUP shriek result=" + PlayerData.instance.screamLevel + " state=" + altar.ActiveStateName + " control=" + hero.CanInput());
                    Check("shriek-upgrades", PlayerData.instance.screamLevel == 2);
                    Check("shriek-control-restored", hero.CanInput());
                    Check("shriek-weapons-granted", mod.GrantedAbilities.Contains("screamLevel2.abyss.v1"));
                    int gifts = Items.Abyss.DevGiven;
                    Check("learned-shriek-cannot-repeat", !Player.SpellAltars.TryShriek(hero));
                    Items.Abyss.GrantReward(mod, PlayerData.instance);
                    Check("no-duplicate-weapons", Items.Abyss.DevGiven == gifts);
                }
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(DevCopy.SaveDir, "spell-upgrade-check.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { checks, failures, fireballLevel = PlayerData.instance.fireballLevel, screamLevel = PlayerData.instance.screamLevel }, Newtonsoft.Json.Formatting.Indented));
        }
    }
}
