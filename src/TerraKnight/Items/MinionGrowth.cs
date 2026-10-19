namespace TerraKnight.Items
{
    // Terraria 1.4.5.8 Projectile.Damage: growth is applied to gear-scaled damage
    // once, before tags/defense. A counter uses one slot; its body uses none.
    internal static class MinionGrowth
    {
        internal static int Abigail(int damage, int counters, bool hardmode) =>
            (int)(damage * (1f + System.Math.Max(0, counters - 1) * (hardmode ? 1.3f : .55f)));

        internal static int Tiger(int damage, int gems) =>
            (int)(damage * (1f + System.Math.Max(0, gems - 1) * .4f));

        internal static int TigerPounce(int damage, int gems) =>
            (int)(damage * (1.5f + System.Math.Max(0, gems - 1) * .4f));
    }
}
