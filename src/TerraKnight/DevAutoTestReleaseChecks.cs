using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator ReleaseChecksTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int all = 0, ok = 0;
            void Check(string name, bool pass, string detail = "")
            {
                all++; if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] RELEASE_CHECK {name} {(pass ? "通过" : "不通过")} {detail}");
            }
            // The current source, not a historical Wiki snapshot, is the candidate under test.
            Check("unique-item-ids", Enum.GetValues(typeof(ItemType)).Cast<ItemType>().Distinct().Count() == Enum.GetValues(typeof(ItemType)).Length);
            Check("quest-only-acquisition", QuestFishCatalog.All.All(q => Fishing.FishOnly(q.Fish)));
            Check("quest-assets-embedded", QuestFishCatalog.All.Select((q, i) => typeof(Plugin).Assembly.GetManifestResourceInfo("TerraKnight.hk_QuestFish_Q" + (i+1).ToString("D2") + ".png") != null).All(x => x));
            int previousStage = Shop.DevStage;
            Shop.DevStage = 1;
            var cold = EnemyDrops.DevRecords.Where(r => !EnemyDrops.IsBoss(r)).SelectMany(r => BestiaryHabitats.Of(r).Where(h => World.Areas.AreaName(h.Scene) == "呼啸悬崖").Select(h => new { Record=r, h.Scene })).ToArray();
            Check("shiverthorn-stage1-route", cold.Any(p => EnemyDrops.At(p.Record,1,false,false,false,p.Scene).Any(d => d.Type == ItemType.Shiverthorn && d.Chance > 0)), "habitats="+cold.Length);
            Shop.DevStage = 0;
            Check("shiverthorn-no-stage0-leak", cold.All(p => !EnemyDrops.At(p.Record,0,false,false,false,p.Scene).Any(d => d.Type == ItemType.Shiverthorn && d.Chance > 0)));
            Shop.DevStage = previousStage;
            Plugin.Log.LogInfo("[AutoTest] RELEASE_CHECK progression and vanilla recipe audits");
            yield return ProgressionAuditTest(false, stageMilestones: true);
            yield return VanillaRecipesTest();
            yield return GoTo("Town", "left1");
            yield return new WaitForSecondsRealtime(2f);
            var inv = mod.Inventory;
            mod.ApplyCreative(false); inv.Infinite = false;
            System.Array.Clear(inv.Slots, 0, inv.Slots.Length);
            foreach (var q in QuestFishCatalog.All) inv.Slots[10 + Array.IndexOf(QuestFishCatalog.All, q)] = ItemStack.Of(q.Fish, 1);
            Angler.Saved = true; Angler.Pending = 2; Angler.QuestDay = -1; Angler.QuestDone = false;
            Angler.Quest = ItemType.HkQuestFish36; Angler.QuestsDone = 17;
            Save.ModSave.Write(slot, mod);
            System.Array.Clear(inv.Slots, 0, inv.Slots.Length); Angler.Clear();
            Save.ModSave.Read(slot, mod);
            Check("quest-inventory-save-roundtrip", QuestFishCatalog.All.All(q => inv.Slots.Count(s => !s.IsEmpty && s.Type == q.Fish) == 1));
            Check("angler-state-save-roundtrip", Angler.Saved && Angler.Pending == 2 && !Angler.QuestDone && Angler.Quest == ItemType.HkQuestFish36 && Angler.QuestsDone == 17);
            Check("inventory-save-control", GameHooks.PlayerHasControl());
            Plugin.Log.LogInfo("[AutoTest] RELEASE_CHECK forging");
            yield return ForgeTest();
            v301All = v301Ok = 0;
            yield return PrefixKeepTest();
            yield return CraftRuleTest();
            Check("prefix-crafting-regression", v301All > 0 && v301Ok == v301All, $"{v301Ok}/{v301All}");
            yield return FoodTest();
            Plugin.Log.LogInfo($"[AutoTest] RELEASE_CHECK 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
