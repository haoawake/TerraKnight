namespace TerraKnight.Items
{
    internal sealed partial class TerraProjectiles
    {
        // Read the same immunity and penetration defaults used by actual hits.
        // Local cooldowns count projectile updates; owner/type cooldowns count ticks.
        internal static float SustainedHitRate(ProjKind kind)
        {
            if(!specs.TryGetValue(kind,out var s)) return 6;
            if(s.TypeCooldown>0) return 60f/s.TypeCooldown;
            if(s.LocalCooldown>0) return 60f*(s.Extra+1)/s.LocalCooldown;
            return 60f/(s.OwnerImmune>0 ? s.OwnerImmune : 10);
        }
        internal static int BaseArmorPen(ProjKind kind) => specs.TryGetValue(kind,out var s) ? s.ArmorPen : 0;
    }
}
