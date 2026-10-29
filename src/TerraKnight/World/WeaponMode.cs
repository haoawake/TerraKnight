namespace TerraKnight.World
{
    /// <summary>Which weapons a save plays with, chosen when its world is created.</summary>
    internal enum WeaponKind { Vanilla = 0, New = 1 }

    /// <summary>
    /// The save's weapons (the user's 10-27 "把新的武器跟旧的武器分为两个模式，新建世界的时候可以选择…不要把新武器搞到旧存档里"):
    /// Terraria's own (every save from before the choice), or the remade ones (Items.BladeArts.Enabled: the melee swings and
    /// their right-click arts, the spears, yoyos, flails, thrown weapons, shortswords, lances, monk staffs and the Zenith, the
    /// whips' lashes, the magic weapons' casts). Set when a save is read or a new one starts (Save.ModSave), written with the
    /// save; it stays what the world was made with.
    /// </summary>
    internal static class WeaponMode
    {
        public static WeaponKind Current = WeaponKind.Vanilla;

        public static bool New => Current == WeaponKind.New;

        public static WeaponKind Parse(int v) => v == (int)WeaponKind.New ? WeaponKind.New : WeaponKind.Vanilla;

        public static string NameOf(WeaponKind k) => k == WeaponKind.New ? Lang.T("新版武器") : Lang.T("原版武器");
    }
}
