## Multiplayer

Modelled on Terraria's LAN multiplayer (default port 7777, up to 16 players, optional password; a different version, a duplicate name or a wrong password is rejected just as in Terraria). Everyone joins with a save of their own choice: items, progress and map are each player's own, and players in the same scene (or in adjacent scenes in the seamless world) see each other and fight together.

**Hosting**: on the save screen, "Multiplayer" at the bottom left → enter a name (defaults to your Steam nickname) and pick a look (colour scheme) → "Host & Play" → password (leave empty for no password), port, max players → "Start". "Server started" and this computer's LAN address appear at the top; then click a save to enter the game as usual.

**Joining**: "Multiplayer" → "Join via IP" → enter the IP address and port of the hosting computer → "Join" → (you are asked for the password if there is one) → once "Connected to ..." appears, click one of your own saves to enter the game.

- The first time a computer hosts, Windows may show a firewall prompt; tick Allow (private networks). If you are not on the same LAN, use a virtual LAN such as Radmin VPN, ZeroTier or Tailscale and enter the address it gives you.
- Returning to the main menu leaves the multiplayer game; when the host leaves, the server shuts down (Terraria's "Local player left. Autoshutdown starting."), and everyone else keeps playing their own save in single player. In game you can also click "Disconnect" in "Terraria Settings".

What is synced:

- **Characters**: position, running and jumping animations, the held item and the swinging / aiming / staff-holding poses, armour, accessories and wings, look colours; a name above the head (in the team colour when on a team), a health bar under the feet after taking damage, and the body breaks into three pieces on death. Other players' head icons are also drawn on the full-screen map and the minimap.
- **Chat**: the latest messages show at the bottom left, and what someone says pops up above their head; joining and leaving, toggling PvP, changing team and deaths are announced in chat (all text uses Terraria's own localisation).
- **Projectiles and minions**: other players' arrows, bullets, magic, yoyos, minions and sentries are drawn on your side.
- **Enemies**: everyone in the same scene sees the same set of enemies. The game of the first player to enter the scene runs the enemies' behaviour (in Terraria the server runs NPCs); on everyone else's side the enemies move with that player's positions and animations and still hurt you on contact. When you hit one, the damage is handed to the controlling player to resolve (knockback too), and its loot is shared as in Terraria: the controlling player's game drops it once, everyone sees it and whoever picks it up keeps it; a boss's treasure bag is each player's own, and so is the Geo. Items dropped on the ground or put in chests keep their prefix and forge level. When the controlling player leaves the scene, the next player in the scene takes over. With `Multiplayer.SyncEnemies` turned off in the config, everyone fights their own enemies.
- **Blocks**: blocks dug out or placed take effect for everyone in real time and are also recorded in each player's own save (everyone is in the same Hallownest).
- **PvP**: two players can damage each other only when both have PvP on and they are not on the same team. Damage follows Terraria's rules (weapon damage ±15%, critical hits double, minus half the defense); when someone is killed by another player, chat shows "X was slain by Y's Z.".
- **Time**: day and night follow the host.
- **World**: everyone plays in the host's world: chests, crafting stations and furniture, items on the ground, Life Crystals and dug or built terrain follow the host, and a chest opens for one player at a time; each player's own save keeps their own world.
- **Boss fights**: a boss acts only on the players inside its arena, teammates outside stay where they are; a boss seal takes every player into the arena (only the user's seal is spent); on Expert and Master a boss has more life by the number of players.

What it cannot do yet: enemies only chase the player who controls them (enemy scripts run only in that player's game); bullets that enemies spit and minions they summon deal damage only on the controlling player's side; boss-fight cutscenes and mechanisms and town NPC shops are separate for each player; there are no Steam friend invites, only direct connection by IP. Both sides need the same version of TerraKnight.

