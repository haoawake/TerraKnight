using UnityEngine;

namespace TerraKnight.World
{
    internal enum DifficultyLevel { Normal = 1, Hard = 2, Expert = 3, Master = 4, Hollow = 5 }

    /// <summary>
    /// The save's difficulty, chosen when it is started (UI.CreativeMenu), as Terraria's worlds
    /// are: the enemies' life and the damage they deal are the mod's own tuning, which is Master,
    /// times a share of it: a quarter on Normal, half on Hard, three quarters on Expert. The share
    /// of damage is applied before defense; defense uses Terraria's difficulty-specific effectiveness. Saves from
    /// before there was a choice are Master, which is what they were played at. Hollow inherits
    /// Master's rules and additionally multiplies native hits by their original mask count.
    /// </summary>
    internal static class GameDifficulty
    {
        public static DifficultyLevel Current = DifficultyLevel.Master;
        public static bool MasterRules => Current >= DifficultyLevel.Master;
        public static bool UsesMaskDamage => Current == DifficultyLevel.Hollow;

        /// <summary>The share of Master's enemy life and damage the current save's enemies have.</summary>
        public static float EnemyFactor => FactorOf(Current);

        // Terraria.Main.CalculateDamagePlayersTake: normal .5, expert .75, master 1.
        // The mod's extra Hard setting keeps normal defense rules.
        public static float DefenseEffectiveness => Current switch
        {
            DifficultyLevel.Master or DifficultyLevel.Hollow => 1f,
            DifficultyLevel.Expert => .75f,
            _ => .5f,
        };

        public static float FactorOf(DifficultyLevel d) => d switch
        {
            DifficultyLevel.Normal => 0.25f,
            DifficultyLevel.Hard => 0.5f,
            DifficultyLevel.Expert => 0.75f,
            _ => 1f,
        };

        public static string NameOf(DifficultyLevel d) => Lang.T(d switch
        {
            DifficultyLevel.Normal => "普通",
            DifficultyLevel.Hard => "困难",
            DifficultyLevel.Expert => "专家",
            DifficultyLevel.Hollow => "空洞",
            _ => "大师",
        });

        /// <summary>Terraria's colours for its difficulties: white, then Expert's orange (Main.mcColor) and Master's red (Main.hcColor), Hard between.</summary>
        public static Color ColorOf(DifficultyLevel d) => d switch
        {
            DifficultyLevel.Normal => Color.white,
            DifficultyLevel.Hard => new Color(1f, 0.88f, 0.45f),
            DifficultyLevel.Expert => new Color(1f, 0.6f, 0f),
            DifficultyLevel.Hollow => new Color(.65f, .85f, 1f),
            _ => new Color(1f, 0.15f, 0.1f),
        };

        /// <summary>The world creation screen's icon for it (Hard has none of its own: Normal's).</summary>
        public static string IconOf(DifficultyLevel d) => "UI/WorldCreation/IconDifficulty" + d switch
        {
            DifficultyLevel.Expert => "Expert",
            DifficultyLevel.Master or DifficultyLevel.Hollow => "Master",
            _ => "Normal",
        };

        /// <summary>A save's stored value; 0 (a save from before the choice) or anything else unknown is Master.</summary>
        public static DifficultyLevel Parse(int v) => v >= 1 && v <= 5 ? (DifficultyLevel)v : DifficultyLevel.Master;
    }
}
