using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Hollow Knight's lifeblood (PlayerData.healthBlue: a lifeblood cocoon broken, the Lifeblood charms at a bench) as two
    /// silver hearts of shield over the life, which a hit takes first and which are gone once broken, and two mana stars more
    /// kept until death. Lifeblood got again fills the shield again (the stars stay two). The native blue-mask count is
    /// retained independently for the Abyss door; only native rest/death rules clear it, not a broken Terraria shield.
    /// </summary>
    internal static class Lifeblood
    {
        public const int ShieldLife = 40, ManaStars = 40;
        static int lastBlue;
        static int collected;
        static PlayerData ownerData;
        static TerraStats ownerStats;
        public static int DevGranted;

        internal static int OnNativeRefresh(PlayerData pd) => ownerData == pd && ownerStats == TerraMod.Instance?.Stats && !pd.atBench ? collected : 0;

        /// <summary>Each frame before maximums: newly collected native blue masks refill the Terraria rewards.</summary>
        public static void Tick(TerraStats st, PlayerData pd)
        {
            if (st == null || pd == null) return;
            if (ownerData != pd || ownerStats != st) {
                ownerData = pd; ownerStats = st;
                collected = lastBlue = 0;
            }
            if (pd.atBench) {
                collected = lastBlue = pd.healthBlue = 0;
                return;
            }
            int blue = pd.healthBlue;
            if (blue > lastBlue)
            {
                collected += blue - lastBlue;
                st.Shield = ShieldLife;
                st.TempMana = ManaStars;
                DevGranted++;
                UI.CenterNotice.Show(Lang.F("生命血：累计 {0} 点；获得两颗银心护盾和两颗临时魔力星", collected));
                Plugin.Log.LogInfo($"[生命血] 蓝面具 {lastBlue} → {blue}：护盾 {st.Shield}，临时魔力 {st.TempMana}");
            }
            // CharmUpdate/MaxHealth during scene entry can rebuild the native masks. Collection is independent
            // of that refresh and of Terraria shield damage; mirror it for the untouched native door FSM.
            pd.healthBlue = lastBlue = collected;
        }

        /// <summary>Death: the shield and the stars gone (the game drops its blue masks itself).</summary>
        public static void OnDeath(TerraStats st)
        {
            if (st == null) return;
            st.Shield = 0;
            st.TempMana = 0;
            collected = lastBlue = 0;
            if (PlayerData.instance != null) PlayerData.instance.healthBlue = 0;
        }
    }
}
