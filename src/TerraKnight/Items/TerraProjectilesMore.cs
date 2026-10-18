namespace TerraKnight.Items
{
    /// <summary>
    /// Where the projectiles in the other partial files (TerraProjectiles*.cs) join the spec
    /// table: each family adds its kinds with Ai.Custom and its own update, draw, hit and death.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static partial void RegisterMore()
        {
            RegisterSwords();
            RegisterSpears();
            RegisterCraftingSupplement();
            RegisterBoomerangs();
            RegisterRanged();
            RegisterMagic();
            RegisterMagic2();
            RegisterMagic3();
            RegisterMagic4();
            RegisterLunar();
            RegisterHeld();
            RegisterIce();
            RegisterWhip();
            RegisterMinionShots();
            RegisterSentryShots();
            RegisterWhip2();
            RegisterAccessories();
            RegisterArmor();
            RegisterYoyos();
            RegisterFlails();
            RegisterMelee2();
            RegisterPhase();
            RegisterMelee3();
            RegisterHeld2();
            RegisterJavelins();
            RegisterMonk();
            RegisterThrown2();
            RegisterFlint();
            RegisterChains();
            RegisterDrills();
            RegisterThrown3();
            RegisterGuns();
            RegisterHeldBows();
            RegisterGuns2();
            RegisterRockets();
            RegisterCelebration();
            RegisterDarts();
            RegisterFlames();
            RegisterAmmo3();
            RegisterMagic5();
            RegisterMagic6();
            RegisterMagic7();
            RegisterMagic8();
            RegisterPal();
            RegisterGuns3();
            RegisterReflect();
        }
    }
}
