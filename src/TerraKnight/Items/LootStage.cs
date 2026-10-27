using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>A drop-table choice only. Never substitute it for ability, shop, crafting or enemy scaling progress.</summary>
    internal static class LootStage
    {
        public static int Selection { get; private set; } = -1;
        static int hostStage = -1;
        public static bool CanChoose => !Net.NetSession.Active || Net.NetSession.Hosting;
        public static bool FollowsProgress => CanChoose && Selection < 0;
        public static int Current => !CanChoose && hostStage >= 0 ? hostStage
            : Selection < 0 ? Shop.UnlockedStage() : Mathf.Clamp(Selection, 0, Shop.UnlockedStage());

        public static bool Choose(int stage)
        {
            if (!CanChoose || stage < -1 || stage > Shop.UnlockedStage()) return false;
            Selection = stage;
            // Save with the world. Joining somebody else's world never overwrites the personal choice.
            int slot = Save.ModSave.DevStateSlot;
            if (slot >= 0 && TerraMod.Instance != null) Save.ModSave.Write(slot, TerraMod.Instance);
            return true;
        }
        public static void Load(int stage) => Selection = stage >= -1 && stage <= 5 ? stage : -1;
        public static void Reset() { Selection = -1; hostStage = -1; }
        public static void ReceiveHost(int stage) { if (stage >= 0 && stage <= 5) hostStage = stage; }
        public static void ClearHost() => hostStage = -1;
    }
}
