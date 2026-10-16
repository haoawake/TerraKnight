## Launching

- **Playing the mod**: double-click `泰拉圣巢.bat` (the TerraKnight launcher) in the Hollow Knight game folder.
- **Playing vanilla**: launch the game from Steam as usual; the mod is not loaded.

The launcher first turns on BepInEx's master switch (`enabled` in `doorstop_config.ini`), starts the game through Steam, and switches it back off ten seconds after the game is up. Doorstop reads this switch only at the moment the game starts, so closing the launcher window partway through never leaves the mod switched on.

## Controls

Terraria key bindings (they apply only in the modded game and do not change vanilla's key settings):

| Action | Key |
|---|---|
| Move | A / D (arrow keys also work) |
| Jump | Space (Z also works) |
| Use item (swing / dig / place) | Left mouse button; hold to keep using |
| Select hotbar slot | Number keys 1-0 or scroll wheel |
| Inventory | Esc. "Settings" in the bottom-right corner opens Hollow Knight's settings menu; "Terraria Settings" toggles Autofire and changes key bindings (when rebinding you can also press the middle or side mouse buttons to bind an action to the mouse; Move and Jump work when bound to side buttons too) |
| Pick up / put down / swap | Left-click in the inventory. While the inventory is open, clicking outside it with an item on the cursor uses that item directly (swing, dig, place) |
| Throw items | T throws the stack selected in your hand (the one on the cursor while the inventory is open); with an item on the cursor, right-clicking outside the inventory also throws it |
| Equip | Right-click armour or an accessory in the inventory to put it on (the item it replaces goes into the slot it came from); Shift+left-click an item in the equipment slots to move it back to the inventory |
| Take one | Right-click a stack to take one onto the cursor; hold to take them faster and faster |
| Favourite | Alt+left-click. Favourited items cannot be trashed or sold with Ctrl |
| Trash | Ctrl+left-click (sells the item instead while an NPC shop is open). The trash can is below the rightmost column of inventory slots, and you can take items back out of it |
| Crafting | The crafting menu in the bottom-left corner of the inventory: click to take one, hold to keep taking; scroll wheel to switch |
| Pylon teleport | Right-click beside a pylon to open the map, then click a pylon icon to teleport |
| Smart Cursor | Ctrl toggles it (while it is on, a pickaxe picks blocks automatically and highlights them in yellow; holding platforms lays platforms and stairs automatically) |
| Shield of Cthulhu dash | Double-tap A or D |
| Grappling hook | E (when you have a hook in your inventory); Jump releases it |
| Quick heal / quick mana | H / J (drinks the best healing / mana potion in your inventory) |
| Switch loadout | F1 / F2 / F3 / F4 (melee / ranged / magic / summon), or the buttons next to the equipment slots in the inventory |
| Talk to town NPCs | Right-click an NPC within reach; click "Shop" to buy and sell, Esc ends the conversation. The Guide's "Crafting": put a material in the slot above the crafting menu, and the crafting menu lists every recipe that uses it and the crafting station each one needs (view only; you cannot craft here) |
| Sell items | While a shop is open, Ctrl+left-click or Shift+left-click an item in your inventory, or put the item on the cursor into an empty shop slot |
| Bestiary | Click "Bestiary" in the inventory to expand it (it is collapsed each time you open the inventory), click an area on the left to filter, scroll to page through the entries; with many drops, scroll with the mouse over the drops to see them all (a scroll bar shows at the side); × in the top-right corner closes it |
| Share equipment | Alt+left-click in the equipment slots |
| Set minion target | Right-click an enemy while holding a staff (click again to cancel) |
| Remove buffs | Right-click the icon in the buff bar below the hotbar |
| Drop through platforms / climb ropes | Hold S (down) to drop through platforms; on a rope, press W / S to grab it and climb up or down |
| Full-screen map | M (Terraria-style world map; hover the mouse to see what things are) |
| Map display mode | Tab switches between minimap / overlay map / hidden; numpad +/- (or = -) zooms, PageUp/PageDown sets opacity |
| Record the scene (for troubleshooting) | F8: writes the scene, your position, the tile and colliders under the mouse, and the tiles the item in your hand can act on to the log (`[现场]`); while you move, your position is logged automatically every 3 seconds (`[位置]`) |
| Chat (multiplayer) | Enter opens it, Enter sends, Esc cancels. Commands as in Terraria: `/p message` sends to your team only, plus `/me`, `/roll`, `/playing`, `/death`, `/alldeath`, `/pvpdeath`, `/allpvpdeath`, `/help` |
| PvP / teams (multiplayer) | With the inventory open, the crossed swords to the left of the equipment slots toggle PvP, and the six shields below them pick a team (none, red, green, blue, yellow, pink) |

All the Terraria keys in the table above (Move, Jump, Drop, Inventory, Grappling hook, Quick heal / Quick mana, Auto Select, Smart Cursor, Map, Chat, Hotbar 1-10, Loadout 1-4) can be changed in "Terraria Settings", and the changes are saved in the `Keys` section of the config file; combinations such as Shift / Ctrl / Alt + click in the inventory are not among them. Keys bound in Hollow Knight's own settings work as usual.

Block placement and reach follow Terraria: a block must touch an existing block (or have a wall behind it), and the range is 5 tiles to the left and right of your character and 3 tiles up and down.

Hollow Knight's movement abilities are Terraria accessories, and they take effect only when equipped in the accessory slots on the right of the inventory. You get each one automatically when you obtain the matching ability in vanilla. The feel, particles and sounds all follow the Terraria source code, and equipped accessories are drawn on your character:

| Vanilla | Terraria accessory | Effect |
|---|---|---|
| Mothwing Cloak | Shield of Cthulhu | Double-tap a direction to dash (starting at 14.5 pixels/frame, with afterimages); the first enemy you hit takes 30 damage and you bounce off it; +2 defense |
| Mantis Claw | Climbing Claws | Hold toward a wall to slide down slowly (hold Down to slide faster), wall jump, puffs of dust while sliding |
| Monarch Wings | Cloud in a Bottle | Double jump, with a puff of cloud bursting under your feet |
| Sprintmaster (charm) | Hermes Boots | After reaching normal top speed, you slowly accelerate up to 6 pixels/frame, with dust under your feet and a running sound |
| Desolate Dive (spell) | Ram Rune | In the air, hold Down and jump to slam downward; landing hurts nearby enemies, and landing on a floor that vanilla breaks with Desolate Dive smashes it open, and you fall on through |

The Knight's own jump, wall-jump, wall-slide and hurt sounds, and the hard landing, are left out; Terraria has none of them.

**The Nail**: you start with a "Nail" in your inventory (an amber quest item, kept permanently like the Dream Nail: it cannot be sold, thrown or trashed, and it comes back if it goes missing). While you hold it, the left mouse button performs vanilla Hollow Knight's nail attack: hold to keep slashing (at vanilla's attack interval), hold W to slash upward, hold S in the air to slash downward, and down slashes bounce you off spikes and enemies. It can parry (for example Revek in the Resting Grounds), pull levers and break walls; its damage follows the unlocked stage (14, 26, 42, 60, 85 and 125 for Stages 0 to 5), and each of the Nailsmith's upgrades (Sharpened, Channelled, Coiled, Pure Nail) adds 25%, so the Pure Nail deals double. Its hits show damage numbers and can land critical strikes like a Terraria weapon's, while the vanilla parry, down-slash bounce and Soul gain stay as they are, and the recoil after a hit matches the vanilla nail's. The Nail and the Dream Nail do not appear in the crafting menu. Revek can also be dispersed with any weapon: hitting him just before or after he strikes counts as a parry.

**Dream Nail**: when you get the Dream Nail in vanilla (in the Seer's dream), a "Dream Nail" appears in your inventory (an amber quest item whose icon is vanilla's Dream Nail). It is kept permanently: it cannot be sold, thrown out with T or right-click, or put in the trash can; if your inventory is full when you close it, it pushes out the ordinary item in the last slot and stays in your inventory; and if it ever goes missing (not in the save and not in any chest), it comes back automatically after one second. It can be stored in chests. Select it on the hotbar and hold it, and the left or right mouse button acts as Hollow Knight's Dream Nail button, with everything after that handled by vanilla: hold it while on the ground to start charging (your character stands firm in place), and once fully charged it automatically swings vanilla's Dream Nail light. Enemies it hits speak their dream thoughts and give Soul, and entering dreams (Warrior Dreams, dream bosses) works just as with vanilla's Dream Nail. Hold S as you swing to set a Dreamgate, and hold W as you swing to warp to the Dreamgate (this needs a Dreamgate and Essence). Getting hit interrupts the charge (as in vanilla). The Dream Nail's swing also deals melee damage based on the currently unlocked stage: 40, 75, 120, 150, 200 and 300 for Stages 0 to 5, 50% more once the Seer has awoken the Dream Nail. It benefits from melee damage bonuses and can land critical strikes. In a Dreamer's dream, once a Terraria weapon or the nail has struck the Dreamer down, holding the left or right button with the Dream Nail in hand is vanilla's focus that drains the Dreamer (a note in the middle of the screen says so). As in vanilla, Monomon rises, and her dream opens, only after you inspect her tank and talk to Quirrel.

For the starting items see "Start" above; everything else comes from the Dirtmouth NPC shops, crafting recipes and enemy drops. In a normal save you pick things up one by one and can craft only with materials already in your inventory or in chests nearby; in a Creative mode save, items never run out and you can craft anything directly (the dev config `Dev.InfiniteItemsAllSaves` makes every save work this way; off by default). The equipment slots are on the right of the inventory: the top three hold a helmet, chestplate and leggings, the bottom five hold accessories, each empty slot shows a grey silhouette of what goes in it, and the shield on the left shows your total defense.

The great door at the end of King's Pass is closed on a new save (in vanilla you break it open from inside King's Pass with 13 hits). If you reach Dirtmouth by another route (such as the cliffs) without breaking it, it blocks your way back, and from the Dirtmouth side it is hidden behind a foreground pillar; 13 hits with any weapon break it, the same as with the nail. The wooden floor at the start of King's Pass, which you break by striking up from below, counts a swing hit whenever you are within 3 tiles of it (vanilla has a "hero in range" check, and a swing hitbox entering or leaving that range clears it, so the mod widens it to 3 tiles). The Howling Cliffs entrance (the plank-propped stone wall that blocks the way on the left after you head up-left from King's Pass and through the dark, with the first Crawlid behind it) also breaks when you stand against it: vanilla only accepts the hit zone to the right of the wall, and when you stand against the wall your swing reaches only the wall itself, so a hit on the wall itself counts as a hit on it (once per swing).

Vanilla Hollow Knight abilities that have no use here show no vanilla big-text description or button tutorial when you get them: Mothwing Cloak, Mantis Claw, Monarch Wings, Crystal Heart, Shade Cloak, Vengeful Spirit / Desolate Dive / Howling Wraiths (and their upgrades), the three Nail Arts, Dream Nail / Awoken Dream Nail / Dreamgate. Abilities replaced by a Terraria accessory (Mothwing Cloak → Shield of Cthulhu, Isma's Tear → Flipper, etc.) show the usual Terraria pickup notice when the accessory is given; the Hunter's Journal, the King's Brand and the various keys show their vanilla prompts. The Nailmasters Oro, Mato and Sheo only greet you and do not teach Nail Arts; the Nailsmith offers his four upgrades as in vanilla (Pale Ore plus Geo, paid from the inventory and the Piggy Bank together), and each upgrade raises the Nail item's damage.

**Start a new save slot to play.** The mod does not change Hollow Knight's save data, but in an existing save you have no nail and no abilities. After you uninstall the mod, existing saves are exactly as they were.

