using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    // EmitCorpse does not assign EnemyDeathEffects.corpse when it uses a pool.
    // Capture the actual animation owner in BOTH native branches, without guessing its name.
    [HarmonyPatch(typeof(EnemyDeathEffects), "EmitCorpse")]
    internal static class BossCorpseTracking
    {
        sealed class Emitted { public GameObject Body; }
        sealed class DeathOwner { public string Record, Scene; public GameObject Body; }
        static readonly List<DeathOwner> owners = new List<DeathOwner>();
        static readonly ConditionalWeakTable<EnemyDeathEffects, Emitted> emitted = new ConditionalWeakTable<EnemyDeathEffects, Emitted>();
        static readonly AccessTools.FieldRef<EnemyDeathEffects, GameObject> corpseOf = AccessTools.FieldRefAccess<EnemyDeathEffects, GameObject>("corpse");

        public static GameObject Of(EnemyDeathEffects effects)
        {
            if (effects == null) return null;
            return emitted.TryGetValue(effects, out var value) ? value.Body : corpseOf(effects);
        }

        static void Remember(GameObject body, EnemyDeathEffects effects)
        {
            emitted.GetValue(effects, _ => new Emitted()).Body = body;
            BossLootTiming.Emitted(effects, body);
            owners.RemoveAll(o => o.Body == null || !o.Body.activeInHierarchy);
            var hm = effects.GetComponent<HealthManager>();
            string record = hm != null ? UI.EnemyHover.RecordOf(hm) : null;
            if (EnemyDrops.IsBoss(record) && body != null)
                owners.Add(new DeathOwner { Record = record, Scene = effects.gameObject.scene.name, Body = body });
        }

        public static void Related(string record, string scene, List<GameObject> result)
        {
            owners.RemoveAll(o => o.Body == null || !o.Body.activeInHierarchy);
            foreach (var owner in owners)
                if (owner.Scene == scene && owner.Record == record.Split('@')[0] && !result.Contains(owner.Body)) result.Add(owner.Body);
        }

        public static void Clear() => owners.Clear();

        internal static void Created(GameObject owner, GameObject made)
        {
            if (owner == null || made == null) return;
            string name = made.name.ToLowerInvariant();
            if (!name.Contains("corpse") && !name.StartsWith("ghost death")) return;
            var hm = owner.GetComponent<HealthManager>();
            string record = hm != null ? UI.EnemyHover.RecordOf(hm) : null;
            if (!EnemyDrops.IsBoss(record)) return;
            owners.RemoveAll(o => o.Body == null || !o.Body.activeInHierarchy);
            owners.Add(new DeathOwner { Record = record, Scene = owner.scene.name, Body = made });
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int captured = 0;
            foreach (var instruction in instructions)
            {
                // Native code: LocateFSM(gameObject, "corpse"). The first argument is
                // already on the stack here; retain it for the unchanged native call.
                if (instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, "corpse"))
                {
                    // Reuse the original instruction so its branch labels stay at the
                    // start of the inserted code (Harmony's Label is from game Mono).
                    instruction.opcode = OpCodes.Dup;
                    instruction.operand = null;
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BossCorpseTracking), nameof(Remember)));
                    yield return new CodeInstruction(OpCodes.Ldstr, "corpse");
                    captured++;
                    continue;
                }
                yield return instruction;
            }
            if (captured != 1) throw new System.InvalidOperationException("EnemyDeathEffects.EmitCorpse changed: expected exactly one corpse capture, found " + captured);
        }
    }

    [HarmonyPatch]
    internal static class BossScriptedCorpses
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.CreateObject), "OnEnter")]
        static void Created(HutongGames.PlayMaker.Actions.CreateObject __instance) =>
            BossCorpseTracking.Created(__instance.Owner, __instance.storeObject?.Value);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.SpawnObjectFromGlobalPool), "OnEnter")]
        static void Spawned(HutongGames.PlayMaker.Actions.SpawnObjectFromGlobalPool __instance) =>
            BossCorpseTracking.Created(__instance.Owner, __instance.storeObject?.Value);
    }
}
