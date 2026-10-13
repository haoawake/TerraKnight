using InControl;

namespace TerraKnight.Player
{
    /// <summary>
    /// Terraria's keyboard controls on top of the Knight's input actions. Extra bindings are added
    /// in memory only (the game saves bindings only when they're changed in its own menu), and the
    /// actions for abilities the Terraria player doesn't have are switched off so A, S and D no
    /// longer cast, super dash or dream nail. Escape no longer pauses during play; it opens the
    /// Terraria inventory, whose Settings button opens the game's pause menu.
    /// </summary>
    internal static class TerraControls
    {
        static HeroActions boundFor;

        public static void Apply(InputHandler input, bool inPlay)
        {
            var a = input != null ? input.inputActions : null;
            if (a == null) return;

            // The game rebuilds its action set when bindings change; add ours to each new one.
            if (boundFor != a)
            {
                boundFor = a;
                Bind(a.left, Key.A);
                Bind(a.right, Key.D);
                Bind(a.up, Key.W);
                Bind(a.down, Key.S);
                Bind(a.jump, Key.Space);
                // M is Terraria's fullscreen map (UI.FullMap); Tab keeps the game's quick map.
            }

            a.attack.Enabled = false;
            a.cast.Enabled = false;
            a.quickCast.Enabled = false;
            a.superDash.Enabled = false;
            a.dreamNail.Enabled = false;
            a.dash.Enabled = false;       // the Shield of Cthulhu dashes on a double tap instead
            a.openInventory.Enabled = false;
            // Escape belongs to the Terraria inventory while playing; menus still get it.
            a.pause.Enabled = !inPlay;
        }

        static void Bind(PlayerAction action, Key key)
        {
            var source = new KeyBindingSource(key);
            if (!action.HasBinding(source)) action.AddBinding(source);
        }
    }
}
