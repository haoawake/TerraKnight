using UnityEngine;
namespace TerraKnight.Player
{
    /// <summary>Fast travel is allowed; committing a teleport destroys a carried intact flower.</summary>
    internal static class DelicateFlower
    {
        public const string Line = "娇嫩的花在传送中损毁了";
        public static int DevBroken;
        public static bool Carried
        {
            get
            {
                var pd=PlayerData.instance;
                return pd!=null && pd.hasXunFlower && !pd.xunFlowerBroken;
            }
        }
        public static bool OnTeleport()
        {
            if(!Carried) return false;
            PlayerData.instance.SetBool("xunFlowerBroken",true);
            DevBroken++;
            // Reconcile the item immediately, including a flower on the cursor or hotbar.
            Items.HkItems.Tick(TerraMod.Instance);
            Plugin.Log.LogInfo("[送花] 传送继续，娇嫩的花已损毁");
            var hero=HeroController.SilentInstance;
            if(hero!=null)UI.CombatText.Show(hero.transform.position+Vector3.up*1.2f,Lang.T(Line),UI.CombatText.Notice);
            return true;
        }
    }
}
