using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Hollow Knight's lifeblood (PlayerData.healthBlue: a lifeblood cocoon broken, the Lifeblood charms at a bench) as two
    /// silver hearts of shield over the life, which a hit takes first and which are gone once broken, and two mana stars more
    /// kept until death. Lifeblood got again fills the shield again (the stars stay two). Once the shield is broken the
    /// game's own blue masks (which the Terraria hits never touch) are let go, so the next lifeblood is seen as new.
    /// </summary>
    internal static class Lifeblood
    {
        public const int ShieldLife = 40, ManaStars = 40;
        static int lastBlue;
        static bool granted;
        public static int DevGranted;

        /// <summary>Each frame in play, before the maximums: lifeblood got (more blue masks than a frame ago), or its shield broken.</summary>
        public static void Tick(TerraStats st, PlayerData pd)
        {
            if (st == null || pd == null) return;
            int blue = pd.healthBlue;
            if (blue > lastBlue)
            {
                st.Shield = ShieldLife;
                st.TempMana = ManaStars;
                granted = true;
                DevGranted++;
                UI.CenterNotice.Show(Lang.T("生命血：获得两颗银心护盾和两颗临时魔力星"));
                Plugin.Log.LogInfo($"[生命血] 蓝面具 {lastBlue} → {blue}：护盾 {st.Shield}，临时魔力 {st.TempMana}");
            }
            else if (granted && st.Shield <= 0 && blue > 0)
            {
                // The shield broken: the blue masks behind it go too.
                pd.healthBlue = 0;
                blue = 0;
            }
            if (blue == 0) granted = false;
            lastBlue = blue;
        }

        /// <summary>Death: the shield and the stars gone (the game drops its blue masks itself).</summary>
        public static void OnDeath(TerraStats st)
        {
            if (st == null) return;
            st.Shield = 0;
            st.TempMana = 0;
            granted = false;
        }
    }
}
