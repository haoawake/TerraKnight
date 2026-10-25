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
        internal static bool DevNoRealKeys;
        static int boundVersion = -1;
        /// <summary>The keys this mod added to each of the Knight's actions (so a new binding can take the old one's place).</summary>
        static readonly System.Collections.Generic.Dictionary<PlayerAction, BindingSource> added = new System.Collections.Generic.Dictionary<PlayerAction, BindingSource>();
        /// <summary>The Terraria key each action should answer to, ours or the game's own.</summary>
        static readonly System.Collections.Generic.Dictionary<PlayerAction, BindingSource> wanted = new System.Collections.Generic.Dictionary<PlayerAction, BindingSource>();

        public static void Apply(InputHandler input, bool inPlay)
        {
            var a = input != null ? input.inputActions : null;
            if (a == null) return;

            // The game rebuilds its action set when bindings change; add ours to each new one, and again when they are rebound.
            // Its own keyboard menu clears and refills the same actions (InputHandler.MapKeyboardLayoutFromGameSettings):
            // a key of ours gone from its action puts them all back too.
            if (boundFor != a || boundVersion != TerraKeys.Version || Lost())
            {
                if (boundFor != a) { added.Clear(); wanted.Clear(); }
                boundFor = a;
                boundVersion = TerraKeys.Version;
                Bind(a.left, TerraKeys.Action.Left);
                Bind(a.right, TerraKeys.Action.Right);
                Bind(a.up, TerraKeys.Action.Up);
                Bind(a.down, TerraKeys.Action.Down);
                Bind(a.jump, TerraKeys.Action.Jump);
                // The Nail item's charge and arts (Items.NailArts) hear the game's attack on the Use Item key.
                Bind(a.attack, TerraKeys.Action.UseItem);
                // M is Terraria's fullscreen map (UI.FullMap); Tab keeps the game's quick map.
            }

            // Typing (the LAN chat, a name or an address in the multiplayer menu): no key moves the Knight or the menus.
            bool typing = Items.Casino.IsOpen || DevNoRealKeys || Net.NetChat.Typing || Net.NetMenu.TextFocused || Items.InventoryGui.TypingName;
            if (a.Enabled == typing) a.Enabled = !typing;

            // On only for the Nail item's nail arts (its charge and the art when let go); its swing stays the item's own.
            // Not over the interface, nor resting on a bench: a click there is the bag's (a charm put on), and the bench's
            // Resting state stands the player up on the game's attack (ListenForAttack → GET UP).
            bool seated = PlayerData.instance != null && PlayerData.instance.atBench;
            a.attack.Enabled = !typing && Items.NailArts.Ready && !Items.InventoryGui.MouseOverUi && !seated;
            a.cast.Enabled = false;
            a.quickCast.Enabled = false;
            a.superDash.Enabled = false;
            a.dreamNail.Enabled = false;
            a.dash.Enabled = false;       // the Shield of Cthulhu dashes on a double tap instead
            a.openInventory.Enabled = false;
            // Escape belongs to the Terraria inventory while playing; menus still get it.
            a.pause.Enabled = !inPlay;
        }

        /// <summary>Dev: a key on one of the Knight's actions that the game's own bindings put there (not this mod).</summary>
        public static bool DevGameOwns(PlayerAction action, Key key)
        {
            var source = new KeyBindingSource(key);
            return action.HasBinding(source) && !(added.TryGetValue(action, out var mine) && mine == source);
        }

        static bool Lost()
        {
            foreach (var kv in wanted) if (!kv.Key.HasBinding(kv.Value)) return true;
            return false;
        }

        static void Bind(PlayerAction action, TerraKeys.Action which)
        {
            if (added.TryGetValue(action, out var old))
            {
                action.RemoveBinding(old);
                added.Remove(action);
            }
            var source = TerraKeys.ToInControlSource(TerraKeys.Get(which));
            if (source == null) { wanted.Remove(action); return; }
            wanted[action] = source;
            if (action.HasBinding(source)) return;
            action.AddBinding(source);
            added[action] = source;
        }
    }
}
