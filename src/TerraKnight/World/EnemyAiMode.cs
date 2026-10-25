namespace TerraKnight.World
{
    /// <summary>Which enemy AI a save plays with, chosen when its world is created.</summary>
    internal enum EnemyAiKind { Vanilla = 0, New = 1 }

    /// <summary>
    /// The save's enemy AI: Hollow Knight's own (every save from before the choice), or the new one (EnemyAi), whose
    /// common enemies and elites hunt a Terraria player as fast as he moves. Bosses keep their own fights either way.
    /// Set when a save is read or a new one starts (Save.ModSave), written with the save.
    /// </summary>
    internal static class EnemyAiMode
    {
        public static EnemyAiKind Current = EnemyAiKind.Vanilla;

        public static bool New => Current == EnemyAiKind.New;

        public static EnemyAiKind Parse(int v) => v == (int)EnemyAiKind.New ? EnemyAiKind.New : EnemyAiKind.Vanilla;

        public static string NameOf(EnemyAiKind k) => k == EnemyAiKind.New ? Lang.T("新版") : Lang.T("原版");
    }
}
