using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.Terraria;
using TerraKnight.UI;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// The mod's runtime: Terraria stats, inventory and HUD, using the selected item with the
    /// mouse (swinging weapons, mining, placing blocks), accessories, and the Terraria character
    /// drawn over the hero. Lives for the whole session.
    /// </summary>
    internal sealed class TerraMod : MonoBehaviour
    {
        const int MineThreshold = 100;
        /// <summary>Hermes Boots: Terraria accRunSpeed 6 px/tick.</summary>
        const float BootsRunSpeed = 6f;

        public static TerraMod Instance { get; private set; }

        /// <summary>Where the saves are: the game's own folder, or (a second copy of the game run for a LAN test) its own.</summary>
        public static string SaveDir => DevCopy.SaveDir ?? Application.persistentDataPath;

        public readonly Inventory Inventory = new Inventory();
        public readonly TerraStats Stats = new TerraStats();
        /// <summary>Knight ability flags already turned into their accessory, for this save.</summary>
        public readonly HashSet<string> GrantedAbilities = new HashSet<string>();
        public DevAutoTest AutoTest;
        /// <summary>Frame rate to hold during gameplay; 0 leaves the game's own setting.</summary>
        public int TargetFrameRate;
        public bool SmartCursor { get; private set; }

        /// <summary>Dev: switch the smart cursor as Ctrl would.</summary>
        public void DevSetSmartCursor(bool on) => SmartCursor = on;

        /// <summary>Which way the player looks: towards the cursor while using an item.</summary>
        public float UseDirection { get; private set; } = 1f;
        public bool IsUsingItem => swingItem != null;

        /// <summary>Dev: whether the Terraria player is drawn now, and where.</summary>
        public string DevPlayerDrawn => playerSprite == null ? "-" : $"{(playerSprite.gameObject.activeSelf ? "显示" : "隐藏")} {playerSprite.transform.position:F1}";

        AudioSource audioSource;
        /// <summary>The game's mixer bus the mod's sounds go through (set once the hero exists).</summary>
        public UnityEngine.Audio.AudioMixerGroup SoundGroup => audioSource.outputAudioMixerGroup;
        public MeleeHits Melee => melee;
        int finalFractalShots;
        /// <summary>Dev: aim thrown weapons here instead of at the mouse.</summary>
        public Vector2? DevAim;
        /// <summary>Dev: falls don't hurt (a test dropping the player down a shaft).</summary>
        public bool DevNoFallDamage;
        SpriteRenderer highlight;
        TerraPlayerSprite playerSprite;
        MeleeHits melee;
        ItemDef swingItem;
        Sprite swingSprite;
        float swingTime, swingDuration, bladeLength;
        float useCooldown;
        /// <summary>The shooting swords' own itemTime: a swing shoots only once it has run out.</summary>
        float shotCooldown;
        ItemType readyCueItem;

        /// <summary>Whether the use button is still down (what Player.channel waits on); the tests stand in for it.</summary>
        public bool UseHeld => DevHold ?? (!DevNoRealMouse && Mouse.current != null && Mouse.current.leftButton.isPressed);
        /// <summary>Dev: the autotest is driving; the real mouse buttons are ignored (a click from whoever is at the computer can't steer it).</summary>
        public static bool DevNoRealMouse;
        public bool? DevHold;
        Vector2Int miningCell = new Vector2Int(int.MinValue, 0);
        int miningDamage;
        float abilityCheckTimer;

        public bool WantsCursor
        {
            get
            {
                var gm = GameManager.UnsafeInstance;
                return TerrainSystem.Current != null && GameHooks.IsGameplay(gm) && !gm.isPaused;
            }
        }

        void Awake()
        {
            Instance = this;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            highlight = new GameObject("Terra Highlight").AddComponent<SpriteRenderer>();
            highlight.transform.SetParent(transform, false);
            highlight.sprite = Items.SmartCursor.Highlight();
            // Over the scenery (and the player), as Terraria draws it in its interface layer.
            highlight.sortingOrder = 32010;
            highlight.gameObject.SetActive(false);

            playerSprite = TerraPlayerSprite.Create(transform);
            playerSprite.gameObject.SetActive(false);
            melee = new MeleeHits(transform);
            ZenithSwords.Create(transform);
            GrapplingHook.Create(transform);
            Effects.TerraParticles.Create(transform);
            Effects.FxBatch.Create(transform);
            WorldItems.Create(transform);
            Chests.Create(transform);
            Stations.Create(transform);
            SwordFx.Create(transform);
            TerraProjectiles.Create(transform);
            Minions.Create(transform);
            Whips.Create(transform);
            FallingSand.Create(transform);
            LiveWorld.Create(transform);
            EnemyRespawn.Create(transform);
            ResetPlayer();
        }

        /// <summary>This save is in creative mode (UI.CreativeMenu): items never run out and anything can be made, free.</summary>
        public bool Creative { get; private set; }

        /// <summary>Dev: every save as if creative (the autotests' setting).</summary>
        public static bool DevAllInfinite;

        /// <summary>The save's mode: creative turns on the endless items and the free crafting of anything.</summary>
        public void ApplyCreative(bool creative)
        {
            Creative = creative;
            Inventory.Infinite = creative || DevAllInfinite;
            Inventory.BagPerLoadout = creative;
        }

        /// <summary>A creative save's world, once the save is in: the whole map shown, every bench's pylon there to teleport to.</summary>
        public void ApplyCreativeWorld()
        {
            if (!Creative) return;
            UI.WorldMap.RevealWorld();
            Pylons.UnlockAllForCreative();
            Pylons.UnlockTollBenches(PlayerData.instance);
        }

        public void SetInfiniteItems(bool infinite)
        {
            Inventory.Infinite = infinite;
            ResetPlayer();
        }

        /// <summary>
        /// A fresh Terraria character: the starting tools, a grappling hook, the Climbing Claws and a
        /// Cloud in a Bottle, 100 life, 20 mana. Everything else comes from the crafting column, the
        /// shops and the drops (all of it, free, while testing with infinite items).
        /// </summary>
        public void ResetPlayer()
        {
            Inventory.Clear();
            Inventory.Slots[0] = ItemStack.Of(ItemType.CopperShortsword);
            Inventory.Slots[1] = ItemStack.Of(ItemType.CopperPickaxe);
            // A bow with a hundred arrows (in the first ammo slot) and ten torches to light the way.
            Inventory.Slots[2] = ItemStack.Of(ItemType.WoodenBow);
            Inventory.Slots[3] = ItemStack.Of(ItemType.Torch, 10);
            Inventory.Slots[Inventory.Size] = ItemStack.Of(ItemType.WoodenArrow, 100);
            // A start to get about with: the grappling hook (E) in the bag, the Climbing Claws and the Cloud in a
            // Bottle worn. They are marked shared (favourite), so every loadout keeps them; the Mantis Claw and the
            // Monarch Wings give nothing more later (GrantAbilityAccessories skips what is already owned).
            Inventory.Slots[Inventory.HotbarSize] = ItemStack.Of(ItemType.GrapplingHook);
            Inventory.Accessories[0] = ItemStack.Of(ItemType.ClimbingClaws);
            Inventory.Accessories[0].Favorite = true;
            Inventory.Accessories[1] = ItemStack.Of(ItemType.CloudinaBottle);
            Inventory.Accessories[1].Favorite = true;
            Inventory.RefreshEffective();
            GrantedAbilities.Clear();
            Pylons.Clear();
            WorldItems.Clear();
            Stats.LifeCrystals = 0;
            Stats.ManaCrystals = 0;
            Stats.LifeFruits = 0;
            Stats.Boosts.Clear();
            Chests.Clear();
            Stations.Clear();
            Stats.UpdateMaximums(PlayerData.instance, 0);
            Stats.FullHeal();
            Stats.PotionSickness = 0f;
            TerraMovement.ResetState();
            TerraProjectiles.Instance?.Clear();
            SwordFx.Instance?.Clear();
            Minions.Instance?.Clear();
            Whips.Instance?.Clear();
            PlayerBuffs.Clear();
            SummonTags.Clear();
            Town.TownNpcs.Clear();
            Bestiary.Clear();
            Shop.SpareCopper = 0;
            EnemyDebuffs.Clear();
        }

        public void OnSceneReady(GameManager gm)
        {
            LiveWorld.Instance?.OnSceneReady();
            Pylons.OnSceneReady();
            BossArena.OnSceneReady(gm);
            // The minions follow the player into the new room; a crack in progress ends.
            Minions.Instance?.OnSceneReady();
            Whips.Instance?.Clear();
            AutoTest?.OnSceneReady(gm);
        }

        void Update()
        {
            Lang.Tick();
            TerraKeys.Tick();
            Net.NetSession.Tick();
            float dt = Time.deltaTime;
            useCooldown -= dt;
            if (PanicTicks > 0f) PanicTicks -= dt * 60f;
            if (shotCooldown > 0f)
            {
                shotCooldown -= dt;
                // EmitMaxManaEffect: the ready ding for the shooting swords, while still holding one.
                if (shotCooldown <= 0f && Inventory.Current.Type == readyCueItem) ReadyCue();
            }
            CombatText.Tick(dt);
            UpdateRevolver(dt);
            UpdateSwing(dt);
            ApplyAccessories();

            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            bool gameplay = hero != null && GameHooks.IsGameplay(gm);
            if (gm != null) TerraControls.Apply(InputHandler.Instance, gameplay && !gm.isPaused && TerrainSystem.Current != null);
            Net.NetChat.Tick(gameplay && !gm.isPaused);
            // A new game's summoning out of the portal (no fall down King's Pass).
            if (hero != null && gm != null && !gm.isPaused) OpeningPortal.Tick(hero);
            if (gm != null && gm.isPaused) lastPausedFrame = Time.frameCount;
            if (gameplay && !gm.isPaused && TerrainSystem.Current != null) InventoryGui.Tick();
            if (gameplay)
            {
                var rb = hero.GetComponent<Rigidbody2D>();
                Equipment.StandingStill = rb != null && rb.linearVelocity.sqrMagnitude < 0.01f;
                Stats.UsingItem = IsUsingItem;
                Stats.Tick(dt, Equipment.StandingStill);
                GravityGlobe.Tick(hero);
                GravityGlobe.Begin(hero);
                try
                {
                    TerraMovement.Apply(hero);
                    TerraMovement.PollDownDash(hero);
                }
                finally
                {
                    GravityGlobe.End(hero);
                }
                TerraMovement.FlameTrailStep(hero, rb.linearVelocity / TerraMovement.PxT, dt * 60f);
                // Upside down the fall is measured the other way (the feet at the top of the box).
                var box = hero.GetComponent<BoxCollider2D>().bounds;
                if (GravityGlobe.Flipped) UpdateFallDamage(hero, -box.max.y, -rb.linearVelocity.y);
                else UpdateFallDamage(hero, box.min.y, rb.linearVelocity.y);
                SceneMasks.Tick(hero);
                EnemyDrops.Tick(hero);
                PotionBuffs.Tick(hero);
                Bestiary.Tick(hero);
                Pylons.Tick(hero);
                InfoAccessories.Tick(Inventory, hero);
                Town.TownNpcs.Tick(hero);
                Town.TownNpcs.Draw();
                if (Creative) Shop.KeepCreativeGeo(hero);
                BossArena.Tick();
                GrantAbilityAccessories(dt);
                HoldFrameRate();
            }

            var terrain = TerrainSystem.Current;
            if (gameplay)
            {
                SceneMood.KeepLight();
                UI.WorldMap.Tick();
                UI.WorldMap.UpdateMini();
            }
            // Terraria's fullscreen map takes the input while it's open.
            if (gameplay && terrain != null) FullMap.Update();
            if (FullMap.Open)
            {
                highlight.gameObject.SetActive(false);
                return;
            }
            if (gameplay && terrain != null && !gm.isPaused && !Net.NetChat.BlocksKeys && !UI.IngameOptions.Open) HandleEscape();
            if (terrain == null || !GameHooks.PlayerHasControl() || Mouse.current == null)
            {
                highlight.gameObject.SetActive(false);
                return;
            }

            // While the chat line is open the keys type into it.
            if (!Net.NetChat.BlocksKeys) ReadKeys();
            if (!MouseWorld(out var mouse)) return;
            // A right click on a townsperson in reach starts talking to it (Main.HoverOverNPCs).
            bool talked = !DevNoRealMouse && !InventoryGui.MouseOverUi && Mouse.current.rightButton.wasPressedThisFrame && Town.TownNpcs.TryRightClick(mouse);
            // A right click on an own sentry in reach puts it away (TryDespawningProjectile, 1.4).
            talked = talked || (!DevNoRealMouse && !InventoryGui.MouseOverUi && Mouse.current.rightButton.wasPressedThisFrame && Minions.Instance != null && Minions.Instance.TryDespawnSentry(mouse));
            // A right click on a chest in reach opens it (Player.TileInteractionsUse); on the pet Pal, a pat (Player.PetAnimal).
            talked = talked || (!DevNoRealMouse && !InventoryGui.MouseOverUi && Mouse.current.rightButton.wasPressedThisFrame && Chests.TryRightClick(mouse));
            talked = talked || (!DevNoRealMouse && !InventoryGui.MouseOverUi && Mouse.current.rightButton.wasPressedThisFrame && Minions.Instance != null && Minions.Instance.TryPet(hero, mouse));
            GrapplingHook.Instance?.ReadKey(hero, GrapplingHook.HookItem(Inventory), mouse);

            AutoSelect(hero, DevAim ?? mouse);
            Player.SpotReport.Tick(hero, mouse);
            // Terraria: with the inventory open, the item on the cursor is the one used in the world.
            Inventory.UseMouse = InventoryGui.Open && !Inventory.Mouse.IsEmpty;
            var def = Inventory.Current.Def;
            // Over the hotbar (closed) or any panel (open), a click is the interface's, not a use.
            bool overUi = InventoryGui.MouseOverUi || UI.IngameOptions.MouseOver;
            if (!DevNoRealMouse && !overUi && !talked && Mouse.current.rightButton.wasPressedThisFrame) MeleeSpecials.RightLatch = true;
            bool usable = false, smart = false;
            var cell = CellGrid.WorldToCell(mouse);
            var bounds = hero.GetComponent<BoxCollider2D>().bounds;
            AimPoint = mouse;
            // A pickaxe aimed at a geo rock knocks geo out of it, the way it would break a pot.
            Collider2D rock = !overUi && def.PickPower > 0 ? GeoRockAt(mouse, bounds, def) : null;
            if (!overUi && rock == null && (def.PickPower > 0 || def.Places != CellKind.Empty))
                usable = FindTarget(hero, def, mouse, out cell, out smart);
            // Terraria marks only the smart cursor's pick.
            highlight.gameObject.SetActive(usable && smart);
            if (usable && smart)
            {
                var c = CellGrid.CellCenter(cell.x, cell.y);
                highlight.transform.position = new Vector3(c.x, c.y, CellVisuals.BlockZ - 0.001f);
            }

            // Potions and crystals take a click each; everything else keeps going while held.
            bool once = def.HealLife > 0 || def.HealMana > 0 || def.LifeCrystal || def.ManaCrystal || !Equipment.AutoReuses(def);
            bool pressed = !DevNoRealMouse && (once ? Mouse.current.leftButton.wasPressedThisFrame : Mouse.current.leftButton.isPressed);
            // The Axe strums a note on each click, by how far off the cursor is (Pl:46946-46971), whether or not it swings now.
            if (!overUi && !talked && !DevNoRealMouse && def.Type == ItemType.TheAxe && Mouse.current.leftButton.wasPressedThisFrame) StrumGuitar(mouse);
            if (!overUi && pressed && useCooldown <= 0f && def.IsUsable)
            {
                AltFunction = false;
                Use(def, cell, usable, mouse, rock);
            }
            // ItemCheck_Inner: a use whose useTime is shorter than its animation shoots again each useTime until
            // the animation ends, whether or not the button is still held (the Paintball Gun's three a click).
            else if (useCooldown <= 0f && def.IsUsable && IsUsingItem && swingItem == def && !AltFunction && def.Shoot != ProjKind.None && !def.Channel
                     && def.Class != DamageClass.Melee && def.UseTime < def.UseAnimation && !Equipment.AutoReuses(def))
                Use(def, cell, usable, DevAim ?? mouse, rock);
            // ItemCheck_ManageRightClickFeatures: an item with another use takes it on the right button (held, for the Fury).
            else if (!overUi && !talked && !DevNoRealMouse && def.AltUse && Mouse.current.rightButton.isPressed && useCooldown <= 0f && !IsUsingItem)
            {
                AltFunction = true;
                Use(def, cell, usable, mouse, rock);
            }
            // A right click with a summon staff picks (or drops) the minions' target, swinging the staff
            // (ItemCheck_ManageRightClickFeatures; not while holding an item on the open inventory's cursor).
            else if (!overUi && !talked && !DevNoRealMouse && def.IsSummon && Mouse.current.rightButton.wasPressedThisFrame && !IsUsingItem && useCooldown <= 0f
                     && !(InventoryGui.Open && !Inventory.Mouse.IsEmpty))
                AimMinions(def, mouse);
        }

        /// <summary>Where the held item points (the mouse on the gameplay plane).</summary>
        public Vector2 AimPoint { get; private set; }

        /// <summary>A geo rock under the mouse and within the pickaxe's reach.</summary>
        static Collider2D GeoRockAt(Vector2 mouse, Bounds hitbox, ItemDef pick)
        {
            foreach (var col in Physics2D.OverlapPointAll(mouse))
            {
                if (col.GetComponentInParent<GeoRock>() == null) continue;
                if (InReach(hitbox, pick, CellGrid.WorldToCell(col.ClosestPoint(hitbox.center)))) return col;
            }
            return null;
        }

        void LateUpdate()
        {
            if (TerrainSystem.Current != null) ResourceHud.HideGameHud();
            PosePlayer();
            // The other players of a LAN game, drawn after this one (the player sprite's per-frame statics are this one's first).
            Net.NetSession.PosePlayers();
        }

        /// <summary>How the player was drawn this frame, for the other players of a LAN game.</summary>
        public Net.PoseRecord NetPose;

        /// <summary>Counts the uses (each swing's start): a PvP swing hits a player once.</summary>
        public int UseSerial { get; private set; }

        /// <summary>
        /// Escape opens Terraria's inventory, as in Terraria, even at a bench or in a conversation.
        /// The key press that closes the pause menu must not reopen the inventory.
        /// </summary>
        void HandleEscape()
        {
            var kb = Keyboard.current;
            if (kb == null || !TerraKeys.Pressed(TerraKeys.Action.Inventory) || Time.frameCount - lastPausedFrame <= 2) return;
            // Talking (not shopping): Esc ends the conversation.
            if (Town.TownNpcs.ChatOpen)
            {
                Town.TownNpcs.Close(true);
                return;
            }
            InventoryGui.Open = !InventoryGui.Open;
            if (!InventoryGui.Open) ReturnOrDropMouseItem();
            PlaySound(InventoryGui.Open ? "Menu_Open" : "Menu_Close", 0.7f);
        }

        void ReadKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            // The keys are Terraria's Controls (TerraKeys, rebindable in the Terraria settings).
            for (int i = 0; i < 10; i++)
                if (TerraKeys.Pressed(TerraKeys.Action.Hotbar1 + i)) Inventory.Selected = i;
            // The wheel picks a hotbar slot; with the inventory open it scrolls the crafting column instead.
            float scroll = InventoryGui.Open ? 0f : Mouse.current.scroll.ReadValue().y;
            if (scroll > 0.1f) Inventory.Selected = (Inventory.Selected + Inventory.HotbarSize - 1) % Inventory.HotbarSize;
            else if (scroll < -0.1f) Inventory.Selected = (Inventory.Selected + 1) % Inventory.HotbarSize;

            // Shield of Cthulhu: double-tap a direction to dash, as in Terraria.
            int tap = TerraKeys.Pressed(TerraKeys.Action.Left) || kb.leftArrowKey.wasPressedThisFrame ? -1
                    : TerraKeys.Pressed(TerraKeys.Action.Right) || kb.rightArrowKey.wasPressedThisFrame ? 1 : 0;
            if (tap != 0)
            {
                if (tap == lastTapDirection && Time.unscaledTime - lastTapTime < DoubleTapWindow) TerraMovement.RequestDash(tap);
                lastTapDirection = tap;
                lastTapTime = Time.unscaledTime;
            }
            // The armor set key: double-tapping down (KeyDoubleTap, Pl:25245-25254), the Vortex stealth and the Forbidden storm.
            if (TerraKeys.Pressed(TerraKeys.Action.Down) || kb.downArrowKey.wasPressedThisFrame)
            {
                if (Time.unscaledTime - lastDownTap < DoubleTapWindow) ArmorEffects.KeyDoubleTapDown();
                lastDownTap = Time.unscaledTime;
            }
            // Ctrl toggles Terraria's smart cursor (either Ctrl while it is left at Ctrl).
            if (TerraKeys.Pressed(TerraKeys.Action.SmartCursor)
                || (TerraKeys.Get(TerraKeys.Action.SmartCursor) == Key.LeftCtrl && kb.rightCtrlKey.wasPressedThisFrame && !TerraKeys.Listening.HasValue))
            {
                SmartCursor = !SmartCursor;
                PlaySound("Menu_Tick", 0.7f);
            }
            // Terraria's quick heal and quick mana: the best potion for the job.
            if (TerraKeys.Pressed(TerraKeys.Action.QuickHeal)) QuickUse(d => d.HealLife > 0);
            // Throw (T, the old default and the usual binding): the held stack, or the cursor's.
            if (TerraKeys.Pressed(TerraKeys.Action.Throw)) ThrowHeld(false);
            if (TerraKeys.Pressed(TerraKeys.Action.QuickMana)) QuickUse(d => d.HealMana > 0);
            // Loadouts 1-4 (vanilla F1-F3; F4 for the fourth), with the inventory open or not.
            for (int i = 0; i < 4; i++)
                if (TerraKeys.Pressed(TerraKeys.Action.Loadout1 + i)) TrySwitchLoadout(i);
        }

        /// <summary>
        /// Player.TrySwitchingLoadout: not while an item is in use (or its reuse delay runs) or
        /// when dead; then the Menu_Tick, the slots' flash and a puff of cloud and sparkles.
        /// </summary>
        public bool TrySwitchLoadout(int index)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead) return false;
            if (IsUsingItem || useCooldown > 0f) return false;
            if (!Inventory.SwitchLoadout(index)) return false;
            PlaySound("Menu_Tick", 0.7f);
            InventoryGui.RecordLoadoutChange();
            LoadoutChangeDust(hero);
            Plugin.Log.LogInfo($"[配装] 切换到配装{index + 1}");
            return true;
        }

        /// <summary>
        /// ParticleOrchestrator.Spawn_LoadOutChange: 40 cloud puffs and 5 pastel sparkles over the
        /// player (hitbox less its top 6 px), drifting up to 1.44 px a tick, with his own motion.
        /// </summary>
        void LoadoutChangeDust(HeroController hero)
        {
            var p = Effects.TerraParticles.Instance;
            if (p == null) return;
            var topLeft = TerraMovement.TopLeft(hero) + new Vector2(0f, 6f);
            var v = hero.GetComponent<Rigidbody2D>().linearVelocity / TerraMovement.PxT;
            v.y = -v.y;
            for (int i = 0; i < 45; i++)
            {
                bool sparkle = i >= 40;
                int d = p.NewDust(topLeft, 0, 0, sparkle ? 43 : 16, 0f, 0f, sparkle ? 0 : 120, Random.value * 0.8f + 0.8f);
                if (d < 0) continue;
                ref var dust = ref p[d];
                dust.Position = topLeft + new Vector2(Random.value * 20f, Random.value * 36f);
                float a = Random.Range(-1f, 1f) * 2f * Mathf.PI * 0.1f;
                var up = new Vector2(0f, -36f * Random.value * 0.04f);
                dust.Velocity = new Vector2(up.x * Mathf.Cos(a) - up.y * Mathf.Sin(a), up.x * Mathf.Sin(a) + up.y * Mathf.Cos(a)) + v * 2f * Random.value;
                dust.NoGravity = true;
                if (sparkle) dust.Color = Color.HSVToRGB(Random.value, 0.3f, 1f);
            }
        }

        /// <summary>Drinks the strongest potion of a kind from the inventory (Player.QuickHeal / QuickMana).</summary>
        void QuickUse(System.Func<ItemDef, bool> kind)
        {
            int best = -1;
            for (int i = 0; i < Inventory.Size; i++)
            {
                var s = Inventory.Slots[i];
                if (s.IsEmpty || !kind(s.Def)) continue;
                if (best < 0 || s.Def.HealLife + s.Def.HealMana > Inventory.Slots[best].Def.HealLife + Inventory.Slots[best].Def.HealMana) best = i;
            }
            if (best >= 0 && Drink(Inventory.Slots[best].Def)) Inventory.ConsumeAt(best);
        }

        /// <summary>A potion's effect; false when it can't be drunk now (potion sickness, nothing to restore).</summary>
        /// <summary>
        /// A drink (Player.ItemCheck_UseBuffPotion / ApplyLifeAndOrMana / ApplyPotionDelay): a healing
        /// one not during potion sickness (nor at full life when that is all it does), its sickness
        /// after (60 s; the Restoration Potion 45, the Mushroom 30, the Strange Brew 40-70; three
        /// quarters with the Philosopher's Stone); mana; and its buff (Honey kept on its own timer;
        /// a flask takes the place of another).
        /// </summary>
        bool Drink(ItemDef def)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            bool onlyLife = def.HealLife > 0 && def.HealMana <= 0 && def.BuffType <= 0;
            if (def.PotionSick && Stats.PotionSickness > 0f) return false;
            if (onlyLife && Stats.Life >= Stats.LifeMax) return false;
            if (def.HealMana > 0 && def.HealLife <= 0 && def.BuffType <= 0 && Stats.Mana >= Stats.ManaMax) return false;
            if (def.HealLife > 0)
            {
                int amount = def.Type == ItemType.StrangeBrew ? Random.Range(def.HealLife, 121) : def.HealLife;
                int healed = Stats.Heal(amount);
                CombatText.Show(hero.transform.position + Vector3.up * 0.9f, healed.ToString(), CombatText.Heal);
                if (def.Type == ItemType.StrangeBrew)
                {
                    // Pl:52531-52550: a short spell untouchable, by chance.
                    float roll = Random.value;
                    int ticks = roll <= 0.1f ? 240 : roll <= 0.3f ? 120 : roll <= 0.6f ? 60 : 0;
                    if (ticks > 0) TerraHurt.Invulnerable(hero, ticks);
                }
            }
            if (def.PotionSick)
            {
                float seconds = def.Type == ItemType.RestorationPotion ? 45f : def.Type == ItemType.Mushroom ? 30f
                              : def.Type == ItemType.StrangeBrew ? Random.Range(2400, 4201) / 60f : 60f;
                Stats.PotionSickness = Equipment.Current.PStone ? seconds * 0.75f : seconds;
            }
            if (def.HealMana > 0)
            {
                int restored = Stats.RestoreMana(def.HealMana);
                CombatText.Show(hero.transform.position + Vector3.up * 0.9f, restored.ToString(), CombatText.Mana);
            }
            if (def.BuffType > 0) PotionBuffs.Give(def.BuffType, def.BuffTime);
            PlaySound(def.UseSound ?? "Item_3");
            return true;
        }

        /// <summary>
        /// The cell the held tool acts on: the one under the mouse, or with smart cursor on, the
        /// block Terraria's pickaxe smart cursor picks (<see cref="Items.SmartCursor"/>). Blocks are
        /// always placed under the mouse (Terraria's smart block placement is off by default).
        /// smart tells whether the smart cursor chose it, which is when Terraria marks the tile.
        /// </summary>
        bool FindTarget(HeroController hero, ItemDef def, Vector2 mouse, out Vector2Int cell, out bool smart)
        {
            var bounds = hero.GetComponent<BoxCollider2D>().bounds;
            cell = CellGrid.WorldToCell(mouse);
            smart = false;
            // A platform line keeps its direction only while the button is held with smart cursor on.
            bool platforms = def.Places == CellKind.Platform;
            if (!SmartCursor || !platforms || Mouse.current == null || !Mouse.current.leftButton.isPressed) PlatformStairs.Unlock();
            if (SmartCursor && platforms)
            {
                if (PlatformStairs.SmartTarget(mouse, ReachBox(bounds, def), out var step))
                {
                    cell = step;
                    smart = true;
                    return CanPlace(step, CellKind.Platform);
                }
                // TileTargetDesired: while a line is locked, nothing goes where the mouse is instead.
                if (PlatformStairs.Locked) return false;
            }
            if (SmartCursor && def.PickPower > 0
                && Items.SmartCursor.PickaxeTarget(bounds, mouse, c => InReach(bounds, def, c), out var target))
            {
                cell = target;
                smart = true;
                return true;
            }
            return InReach(bounds, def, cell) && CanAct(def, cell);
        }

        /// <summary>Dev: whether an item aimed at a point has something to act on (FindTarget), and which cell.</summary>
        public bool DevCanUse(ItemType type, Vector2 aim, out Vector2Int cell, out bool smart)
        {
            var hero = HeroController.instance;
            cell = default;
            smart = false;
            return hero != null && FindTarget(hero, ItemDb.Get(type), aim, out cell, out smart);
        }

        /// <summary>Dev: the block the pickaxe's smart cursor would pick for a mouse position.</summary>
        public bool DevSmartTarget(Vector2 mouse, out Vector2Int cell)
        {
            bool saved = SmartCursor;
            SmartCursor = true;
            bool ok = FindTarget(HeroController.instance, ItemDb.Get(ItemType.CopperPickaxe), mouse, out cell, out bool smart) && smart;
            SmartCursor = saved;
            return ok;
        }

        /// <summary>
        /// Terraria's reach (TileReachCheckSettings.GetTileRegion): the player's hitbox in tiles,
        /// grown by tileRangeX = 5 and tileRangeY = 3 plus the item's tileBoost. One tile is one cell.
        /// </summary>
        /// <summary>The cells <see cref="InReach"/> allows, as a rectangle.</summary>
        static RectInt ReachBox(Bounds hitbox, ItemDef def)
        {
            const float c = CellGrid.CellSize;
            // blockRange (the Builder potion) reaches further only with blocks.
            int extra = def.Places != CellKind.Empty ? Equipment.Current.BlockRange : 0;
            int rx = 5 + def.TileBoost + extra, ry = 3 + def.TileBoost + extra;
            int lx = Mathf.FloorToInt(hitbox.min.x / c) - rx, hx = Mathf.CeilToInt(hitbox.max.x / c) - 1 + rx;
            int ly = Mathf.FloorToInt(hitbox.min.y / c) - ry, hy = Mathf.CeilToInt(ReachTop(hitbox) / c) - 1 + ry;
            return new RectInt(lx, ly, hx - lx + 1, hy - ly + 1);
        }

        static bool InReach(Bounds hitbox, ItemDef def, Vector2Int cell)
        {
            const float c = CellGrid.CellSize;
            // blockRange (the Builder potion) reaches further only with blocks.
            int extra = def.Places != CellKind.Empty ? Equipment.Current.BlockRange : 0;
            int rx = 5 + def.TileBoost + extra, ry = 3 + def.TileBoost + extra;
            int lx = Mathf.FloorToInt(hitbox.min.x / c) - rx, hx = Mathf.CeilToInt(hitbox.max.x / c) - 1 + rx;
            int ly = Mathf.FloorToInt(hitbox.min.y / c) - ry, hy = Mathf.CeilToInt(ReachTop(hitbox) / c) - 1 + ry;
            return cell.x >= lx && cell.x <= hx && cell.y >= ly && cell.y <= hy;
        }

        /// <summary>The hitbox top for reach: 26 px higher on the Portable Stool (ResizeHitbox, Pl:29437-29452).</summary>
        static float ReachTop(Bounds hitbox) => hitbox.max.y + (TerraMovement.StoolActive ? TerraMovement.StoolLift : 0f);

        static bool CanAct(ItemDef def, Vector2Int cell) =>
            def.PickPower > 0 ? TerraWorld.CanDig(cell.x, cell.y) : def.Places != CellKind.Empty && CanPlace(cell, def.Places);

        void Use(ItemDef def, Vector2Int cell, bool usable, Vector2 mouse, Collider2D rock)
        {
            var hero = HeroController.SilentInstance;
            float scale = Equipment.UseScale(def);
            useCooldown = (def.ShootsOnSwingStart ? def.UseAnimation : def.UseTime) * scale;
            UseDirection = mouse.x < hero.transform.position.x ? -1f : 1f;

            if (def.IsSummon)
            {
                // A staff (1.4.5: no mana): its buff, slots freed, the minion at the cursor; swung, held
                // to summon again after the use and its reuse delay of 2 ticks.
                // A sentry staff can't be used without ground under the cursor (nothing happens).
                if (Minions.Instance == null || !Minions.Instance.Summon(def, DevAim ?? mouse))
                {
                    useCooldown = 0f;
                    return;
                }
                useCooldown = def.UseAnimation + 2f / 60f;
                StartSwing(def);
                return;
            }
            if (def.IsWhip)
            {
                // A whip: the swing's length from melee speed and the whip buffs (whole ticks), the crack
                // shot toward the cursor on its first tick, alive for exactly that long.
                int anim = Mathf.Max(1, (int)(Mathf.Round(def.UseAnimation * 60f) * Equipment.WhipScale()));
                useCooldown = anim / 60f;
                StartSwing(def);
                swingDuration = anim / 60f;
                Whips.Instance?.Strike(def, DevAim ?? mouse, anim);
                return;
            }

            // Things drunk or used up on the spot.
            if (def.HealLife > 0 || def.HealMana > 0 || def.BuffType > 0)
            {
                if (Drink(def)) Inventory.ConsumeSelected();
                return;
            }
            if (def.Boost != PermanentBoost.None)
            {
                if (PotionBuffs.UseBooster(def, hero)) Inventory.ConsumeSelected();
                return;
            }
            if (def.Type == ItemType.RecallPotion || def.Type == ItemType.TeleportationPotion)
            {
                if (def.Type == ItemType.RecallPotion ? Pylons.Recall() : PotionBuffs.TeleportRandom(hero))
                {
                    PlaySound(def.UseSound ?? "Item_3");
                    Inventory.ConsumeSelected();
                }
                return;
            }
            if (def.PlacesChest)
            {
                if (Chests.TryPlace(hero, DevAim ?? mouse)) Inventory.ConsumeSelected();
                return;
            }
            if (def.PlacesStation != StationKind.None)
            {
                if (Stations.TryPlace(def.PlacesStation, hero, DevAim ?? mouse)) Inventory.ConsumeSelected();
                return;
            }
            if (def.LifeCrystal || def.ManaCrystal)
            {
                UseCrystal(def, hero);
                return;
            }
            if (def.ShootsOnSwingStart)
            {
                // ItemCheck: only a swing that starts with itemTime run out shoots, and that sets it again.
                if (shotCooldown <= 0f && Fire(def, hero, mouse))
                {
                    shotCooldown = def.UseTime * scale;
                    readyCueItem = def.Type;
                }
            }
            // ItemCheck_Shoot's per-item cut-off: past its shots a use fires nothing more, the swing going on.
            else if (def.Shoot != ProjKind.None && def.ShotsPerUse > 0 && IsUsingItem && swingItem == def && shotsThisUse >= def.ShotsPerUse) { }
            // A drill whose head is already out mines on (its strokes are the item's own, every useTime).
            else if (def.Shoot != ProjKind.None && CountShot(def) && !Fire(def, hero, mouse) && !(def.PickPower > 0 && TerraProjectiles.Instance != null && TerraProjectiles.Instance.Holds(def.Shoot))) return;

            if (!IsUsingItem || swingItem != def) StartSwing(def);
            if (rock != null)
            {
                // Mining goes at the pickaxe's speed times pickSpeed (ApplyItemTime(pick, pickSpeed)).
                useCooldown *= Equipment.PickTime;
                melee.Strike(rock, def, UseDirection);
                PlaySound("Tink_" + Random.Range(0, 3));
            }
            else if (def.PickPower > 0 && (Chests.TryMine(def, DevAim ?? mouse, hero) || Stations.TryMine(def, DevAim ?? mouse, hero)))
                useCooldown *= Equipment.PickTime;
            else if (def.PickPower > 0 && usable)
            {
                useCooldown *= Equipment.PickTime;
                Mine(def, cell);
            }
            else if (def.Places != CellKind.Empty && usable)
            {
                // tileSpeed (the Builder potion, Ambrosia): blocks go down faster (itemTime = useTime / tileSpeed).
                useCooldown /= 1f + Equipment.Current.TileSpeed;
                TerraWorld.Set(cell.x, cell.y, def.Places);
                Inventory.ConsumeSelected();
                PlaySound("Dig_" + Random.Range(0, 3));
                // With the smart cursor on, a platform takes the slope its neighbours make: stairs.
                if (def.Places == CellKind.Platform && SmartCursor) PlatformStairs.Spin(cell.x, cell.y);
                FallingSand.Instance?.Check(cell.x, cell.y);
            }
        }

        /// <summary>
        /// A use that shoots: ammo from the inventory for bows and guns (Terraria adds the ammo's
        /// damage and speed to the weapon's), mana for magic, the item itself when thrown.
        /// False when there's nothing to shoot with.
        /// </summary>
        bool Fire(ItemDef def, HeroController hero, Vector2 mouse)
        {
            ItemDef ammo = null;
            int ammoSlot = -1;
            if (def.UseAmmo != AmmoKind.None)
            {
                ammoSlot = Inventory.FindAmmo(def.UseAmmo, Equipment.Current.AmmoCycling, AccessoryEffects.AmmoOffset);
                if (ammoSlot < 0) return false;
                ammo = Inventory.Slots[ammoSlot].Def;
            }
            if (TerraProjectiles.Instance != null && TerraProjectiles.Instance.Holds(def.Shoot)) return false;
            // Mana is paid as a use starts: the Aqua Scepter's second stream in the same use is free.
            bool freeShot = def.ManaEveryOtherShot && IsUsingItem && swingItem == def;
            if (def.Mana > 0 && !freeShot && !def.NoUseMana && !CheckMana(def)) return false;
            if (def.Mana > 0 && def.PayManaAfterCheck && Stats.Mana < Equipment.ManaCost(def)) return false;

            Vector2 center = hero.GetComponent<BoxCollider2D>().bounds.center;
            Vector2 dir = mouse - center;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.right * UseDirection;
            dir.Normalize();
            shotDir = dir;
            float speed = def.ShootSpeed + (ammo != null ? ammo.ShootSpeed : 0f);
            // ItemCheck_Shoot (Pl:47466-47469): speed /= meleeSpeed, the use-time multiplier: a melee item's shots go faster
            // with melee speed, as its use gets shorter (a spear's reach stays). NoMeleeSpeedVelocityScaling's projectiles
            // (the monk staffs, jousting lances, 973, 985, 1082, 1083) aren't shot from here.
            if (def.Class == DamageClass.Melee) speed /= Equipment.UseScale(def);
            // PickAmmo: the ammo's own projectile, if it has one (a Fallen Star hasn't: the Star Cannon's star).
            var kind = ammo != null && ammo.Shoot != ProjKind.None ? ammo.Shoot : def.Shoot;
            // Molten Fury lights wooden arrows; the Magic Quiver speeds arrows up.
            if (def.Type == ItemType.MoltenFury && kind == ProjKind.WoodenArrow) kind = ProjKind.FlamingArrow;
            // The Molten Quiver (hasMoltenQuiver, Pl:54178-54182): wooden arrows go up as flaming ones, 2 more ammo damage.
            bool moltenQuiver = Equipment.Current.MoltenQuiver && kind == ProjKind.WoodenArrow;
            if (moltenQuiver) kind = ProjKind.FlamingArrow;
            // The Magic Quiver: arrows 10% faster, and moving twice a tick (extraUpdates 1).
            bool quiver = ammo != null && ammo.Ammo == AmmoKind.Arrow && Equipment.Current.FastArrows;
            if (quiver) speed *= 1.1f;
            // The Archery potion (PickAmmo): arrows 20% faster, up to 20 px a tick.
            if (ammo != null && ammo.Ammo == AmmoKind.Arrow && Equipment.Current.Archery && speed < 20f) speed = Mathf.Min(speed * 1.2f, 20f);
            int damage = Equipment.Damage(def, ammo);
            if (moltenQuiver) damage += (int)(2 * (1f + Equipment.Current.RangedDamage + Equipment.Current.ArrowDamage));
            // The Nano Bullet hits a quarter harder (PickAmmo, on the total).
            if (kind == ProjKind.NanoBullet) damage = (int)(damage * 1.25f);
            // PickAmmo: the quiver's 10% goes on the weapon's knockback only, then the ammo's is added.
            float knockback = Equipment.Knockback(def) * (quiver ? 1.1f : 1f) + (ammo != null ? ammo.Knockback : 0f);
            bool meleeShot = def.Class == DamageClass.Melee;
            // Shot from the player's middle (ItemCheck_Shoot's pointPosition).
            var from = center;
            var vel = dir * speed;
            // The Minishark sprays: up to 0.4 px off on each axis (Player.cs:47912).
            if (def.Type == ItemType.Minishark) vel += new Vector2(Random.Range(-40, 41) * 0.01f, Random.Range(-40, 41) * 0.01f);
            int useTicks = Mathf.RoundToInt(def.UseAnimation * Equipment.UseScale(def) * 60f);
            // What this use fires is of the weapon's class (the hit-time accessories go by it).
            TerraProjectiles.SpawnClass = def.Class;
            if (ShootPatterns.Try(new ShootPatterns.Shot
                {
                    Def = def, Ammo = ammo, Kind = kind, From = from, Mouse = mouse, Vel = vel, Speed = speed, Knockback = knockback,
                    Crit = Equipment.Crit(def), Damage = damage, UseTicks = useTicks, Melee = meleeShot, Hero = hero, Extra = quiver ? 1 : 0,
                })) { }
            else if (def.Type == ItemType.Starfury) StarfuryStar(hero, mouse, damage, Equipment.Crit(def), speed);
            else if (def.Pellets > 0)
            {
                // The Boomstick: 3 or 4 pellets for one ammo, each up to 1.4 px off per axis.
                for (int n = Random.Range(3, 5); n > 0; n--)
                    TerraProjectiles.Instance?.Shoot(kind, from, vel + new Vector2(Random.Range(-35, 36) * 0.04f, Random.Range(-35, 36) * 0.04f), damage, knockback, Equipment.Crit(def), 0, useTicks);
            }
            else TerraProjectiles.Instance?.Shoot(kind, from, vel, damage, knockback, Equipment.Crit(def), quiver ? 1 : 0, useTicks, meleeShot);
            TerraProjectiles.SpawnClass = DamageClass.None;

            if (ammo != null) SpendAmmo(def, ammo, ammoSlot);
            if (def.Consumable && def.Ammo == AmmoKind.None) Inventory.ConsumeSelected();
            return true;
        }

        /// <summary>A round or arrow used up for a shot, unless something saves it.</summary>
        void SpendAmmo(ItemDef def, ItemDef ammo, int ammoSlot)
        {
            // Each source of saving is its own roll; any one keeps the ammo (the worn gear, the
            // quiver's fifth for arrows, the weapon's own: a third for the Minishark).
            float save = Equipment.Current.AmmoSave;
            if (ammo.Ammo == AmmoKind.Arrow && Equipment.Current.FastArrows) save = 1f - (1f - save) * 0.8f;
            // The Ammo Reservation potion: a fifth of shots free (ammoPotion).
            if (Equipment.Current.AmmoPotion) save = 1f - (1f - save) * 0.8f;
            save = 1f - (1f - save) * (1f - def.Bonus.AmmoSave);
            // The Endless Quiver and Musket Pouch aren't consumable; blocks and materials shot (the Sandgun's sand, a Fallen Star) are.
            if ((ammo.Consumable || ammo.MaxStack > 1) && Random.value >= save) Inventory.ConsumeAt(ammoSlot);
            // The Chaos Cylinder (Pl:54212-54221): the next stack for the next shot (any at random), back to the first
            // when the use and 2 s more go by without one.
            var cycle = Equipment.Current.AmmoCycling;
            if (cycle != AmmoCycleMode.Off && cycle != AmmoCycleMode.Simple)
            {
                AccessoryEffects.AmmoCooldown = Mathf.RoundToInt(def.UseAnimation * 60f) + 120;
                AccessoryEffects.AmmoOffset = cycle == AmmoCycleMode.Random ? Random.Range(0, int.MaxValue) : AccessoryEffects.AmmoOffset + 1;
            }
        }

        /// <summary>Player.HasAmmo: some of what the item shoots is left.</summary>
        public bool HasAmmo(ItemDef def) => def.UseAmmo == AmmoKind.None || Inventory.FindAmmo(def.UseAmmo, Equipment.Current.AmmoCycling, AccessoryEffects.AmmoOffset) >= 0;

        /// <summary>
        /// Player.PickAmmo for a held-out weapon's own shots (AI_075): the next ammo's projectile
        /// (wooden arrows lit by the Molten Quiver), its speed added to the given one (10% more
        /// through the Magic Quiver), the use's damage and knockback; spent as a shot from the item
        /// would be. False when there's none.
        /// </summary>
        public bool PickAmmo(ItemDef def, ref ProjKind kind, ref float speed, out int damage, out float knockback)
        {
            damage = 0;
            knockback = 0f;
            int slot = Inventory.FindAmmo(def.UseAmmo, Equipment.Current.AmmoCycling, AccessoryEffects.AmmoOffset);
            if (slot < 0) return false;
            var ammo = Inventory.Slots[slot].Def;
            if (ammo.Shoot != ProjKind.None) kind = ammo.Shoot;
            speed += ammo.ShootSpeed;
            bool moltenQuiver = Equipment.Current.MoltenQuiver && kind == ProjKind.WoodenArrow;
            if (moltenQuiver) kind = ProjKind.FlamingArrow;
            bool quiver = ammo.Ammo == AmmoKind.Arrow && Equipment.Current.FastArrows;
            if (quiver) speed *= 1.1f;
            if (ammo.Ammo == AmmoKind.Arrow && Equipment.Current.Archery && speed < 20f) speed = Mathf.Min(speed * 1.2f, 20f);
            damage = Equipment.Damage(def, ammo);
            if (moltenQuiver) damage += (int)(2 * (1f + Equipment.Current.RangedDamage + Equipment.Current.ArrowDamage));
            knockback = Equipment.Knockback(def) * (quiver ? 1.1f : 1f) + ammo.Knockback;
            SpendAmmo(def, ammo, slot);
            return true;
        }

        /// <summary>
        /// The last shot's direction. A held-out weapon keeps pointing along it until the next
        /// shot (ItemCheck_Shoot sets itemRotation once per shot), not after the cursor.
        /// </summary>
        Vector2 shotDir;

        /// <summary>Player.CheckMana(item, pay: true): the item's mana, the Mana Flower drinking a potion first if short.</summary>
        public bool CheckMana(ItemDef def)
        {
            int cost = Equipment.ManaCost(def);
            if (Stats.Mana < cost && Equipment.Current.ManaFlower) QuickUse(d => d.HealMana > 0);
            return Stats.SpendMana(cost);
        }

        /// <summary>
        /// What a held-out projectile does to its player each tick (AI_075, AI_100: SetDummyItemTime,
        /// itemRotation, ChangeDir): the item stays in use, turned to face along dir (world), held out
        /// along it or, with holdUp, raised (useStyle 4).
        /// </summary>
        public void KeepUsing(ItemDef def, Vector2 dir, bool holdUp = false, float face = 0f)
        {
            if (swingItem != def)
            {
                swingItem = def;
                // SetDummyItemTime keeps a shown item in hand (the Harpoon, the Piranha Gun); the rest are drawn by their projectiles.
                swingSprite = def.NoUseGraphic ? null : ItemSprite(def.Type);
                bladeLength = 0f;
            }
            swingDuration = 2f / 60f;
            swingTime = holdUp ? swingDuration * 0.5f : 0f;
            if (dir.sqrMagnitude > 1e-6f) shotDir = dir.normalized;
            if (face != 0f) UseDirection = face;
            else if (Mathf.Abs(dir.x) > 1e-4f) UseDirection = dir.x < 0f ? -1f : 1f;
        }

        /// <summary>
        /// SetDummyItemTime for a swung item its projectile drives (the Flint): drawn at that point
        /// of its swing (0 the start, 1 the end), facing that way, the use kept going a tick more.
        /// </summary>
        public void HoldSwing(ItemDef def, float progress, float face)
        {
            if (swingItem != def)
            {
                swingItem = def;
                swingSprite = def.NoUseGraphic ? null : ItemSprite(def.Type);
                bladeLength = swingSprite != null ? swingSprite.bounds.size.magnitude * Equipment.ItemScale(def) : 0f;
            }
            swingDuration = 2f / 60f;
            swingTime = 0f;
            heldProgress = Mathf.Clamp01(progress);
            if (face != 0f) UseDirection = face;
        }

        /// <summary>How far through its swing the item in use is drawn (0..1).</summary>
        float SwingProgress => heldProgress >= 0f ? heldProgress : Mathf.Clamp01(swingTime / swingDuration);

        float heldProgress = -1f;

        /// <summary>
        /// The Axe's note (Item_47): the cursor's distance over half the screen's shorter side, made
        /// -1..1 and rounded to one of the six notes either way (Player.musicNotes).
        /// </summary>
        public void StrumGuitar(Vector2 mouse)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var cam = Camera.main;
            float half = (cam != null ? cam.orthographicSize * Mathf.Min(1f, cam.aspect) : 8.5f);
            float k = Mathf.Min(1f, Vector2.Distance(mouse, hero.GetComponent<BoxCollider2D>().bounds.center) / half) * 2f - 1f;
            k = Mathf.Round(Mathf.Clamp(k, -1f, 1f) * 6f) / 6f;
            PlaySound("Item_47", 1f, k);
            DevNotes++;
        }

        public static int DevNotes;

        /// <summary>Dev: a glowmask is drawn over the item in hand.</summary>
        public bool DevHeldGlow => playerSprite != null && playerSprite.HeldGlow != null;

        /// <summary>Whether this use is the item's other one (altFunctionUse 2: a right click).</summary>
        public bool AltFunction { get; private set; }

        int shotsThisUse;

        /// <summary>Shots so far in the use in progress, this one included (1 for its first).</summary>
        public int ShotsThisUse => shotsThisUse;

        /// <summary>Counts the shot about to be fired in this use (a new use starts again at 1); always true.</summary>
        bool CountShot(ItemDef def)
        {
            if (!IsUsingItem || swingItem != def) shotsThisUse = 0;
            shotsThisUse++;
            return true;
        }

        /// <summary>Player.reuseDelay: no next use for that many ticks.</summary>
        public void ReuseDelay(int ticks) => useCooldown = Mathf.Max(useCooldown, ticks / 60f);

        /// <summary>Player.revolverCritChanceBonus: the Revolver's extra crit chance, 0-20.</summary>
        public static int RevolverCrit;
        float revolverTicks;

        /// <summary>
        /// Pl:43001-43027, each tick: with the Revolver in hand, a use let go of mid-way runs down
        /// twice as fast and adds 1 to its crit chance; otherwise one time in three it loses 2. Held
        /// 0-20 while the Revolver is out.
        /// </summary>
        void UpdateRevolver(float dt)
        {
            revolverTicks += dt * 60f;
            bool revolver = Inventory.Current.Type == ItemType.Revolver;
            for (; revolverTicks >= 1f; revolverTicks--)
            {
                if (revolver && IsUsingItem && swingItem.Type == ItemType.Revolver && !UseHeld)
                {
                    swingTime += 1f / 60f;
                    useCooldown -= 1f / 60f;
                    RevolverCrit++;
                }
                else if (Random.Range(0, 3) == 0) RevolverCrit -= 2;
                if (revolver) RevolverCrit = Mathf.Clamp(RevolverCrit, 0, 20);
            }
        }

        /// <summary>
        /// Starfury's star (ItemCheck_Shoot): from 600 px above the player, beside the cursor on
        /// the player's side by up to 200 px, at 25 px a tick straight at it, half again the damage
        /// and no knockback; it passes through everything down to the cursor's height (or the first
        /// open spot above it).
        /// </summary>
        void StarfuryStar(HeroController hero, Vector2 mouse, int damage, float crit, float speed)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            float side = mouse.x < b.min.x ? 1f : mouse.x <= b.max.x && Random.Range(0, 2) == 0 ? 1f : -1f;
            var from = new Vector2(mouse.x + 10f * Effects.TerraParticles.Px + Random.Range(0, 201) * Effects.TerraParticles.Px * side, b.center.y + 600f * Effects.TerraParticles.Px);
            var back = (from - mouse).normalized;
            var floor = mouse;
            for (int g = 0; g < 60 && floor.y < from.y && TerraWorld.IsSolidAt(floor); g++) floor += back * 16f * Effects.TerraParticles.Px;
            TerraProjectiles.Instance?.ShootStar(from, (mouse - from).normalized * speed, (int)(damage * 1.5f), crit, floor.y);
        }

        /// <summary>EmitMaxManaEffect: the MaxMana ding and five sparkles over the player.</summary>
        void ReadyCue()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            PlaySound("MaxMana", 0.7f);
            var p = Effects.TerraParticles.Instance;
            if (p == null) return;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var topLeft = Effects.TerraParticles.ToPx(new Vector2(b.min.x, b.max.y));
            for (int i = 0; i < 5; i++)
            {
                int d = p.NewDust(topLeft, 20, 42, 45, 0f, 0f, 255, Random.Range(20, 26) * 0.1f);
                if (d < 0) continue;
                p[d].NoGravity = true;
                p[d].Velocity *= 0.5f;
            }
        }

        /// <summary>Life crystal: 20 more maximum life (and 20 life); mana crystal: 20 more mana.</summary>
        void UseCrystal(ItemDef def, HeroController hero)
        {
            if (def.LifeCrystal && Stats.LifeCrystals < TerraStats.MaxLifeCrystals)
            {
                Stats.LifeCrystals++;
                Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
                Stats.Heal(20);
                CombatText.Show(hero.transform.position + Vector3.up * 0.9f, "20", CombatText.Heal);
            }
            else if (def.ManaCrystal && Stats.ManaCrystals < TerraStats.MaxManaCrystals)
            {
                Stats.ManaCrystals++;
                Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
                Stats.RestoreMana(20);
                CombatText.Show(hero.transform.position + Vector3.up * 0.9f, "20", CombatText.Mana);
            }
            else return;
            Inventory.ConsumeSelected();
            PlaySound(def.UseSound ?? "Item_4");
        }

        float weakPickNotice;

        /// <summary>
        /// Terraria mining: each hit adds pick power until the cell reaches 100. The scene's own
        /// rock deeper in Hallownest needs a stronger pickaxe (Areas.PickRequired), as Terraria's
        /// ores and bricks do: too weak a pick only tinks.
        /// </summary>
        public void Mine(ItemDef pick, Vector2Int cell)
        {
            if (cell != miningCell)
            {
                miningCell = cell;
                miningDamage = 0;
            }
            var kind = TerraWorld.Get(cell.x, cell.y);
            if (kind == CellKind.Original)
            {
                var region = TerraWorld.RegionAt(cell.x, cell.y);
                int need = Areas.PickRequired(Areas.TierOf(region?.SceneName));
                if (pick.PickPower < need)
                {
                    PlaySound("Tink_" + Random.Range(0, 3));
                    MineDust(cell, kind, 1);
                    if (Time.time > weakPickNotice)
                    {
                        weakPickNotice = Time.time + 2f;
                        var c = CellGrid.CellCenter(cell.x, cell.y);
                        CombatText.Show(new Vector3(c.x, c.y + 0.4f, 0f), Lang.F("需要 {0}% 镐力", need), CombatText.Notice);
                    }
                    return;
                }
            }
            int hardness = ItemDb.Hardness(kind);
            miningDamage += pick.PickPower * hardness;
            PlaySound((hardness > 1 ? "Dig_" : "Tink_") + Random.Range(0, 3));
            // WorldGen.KillTile with fail: a few dust of the tile's kind on every hit, more when it breaks.
            if (miningDamage < MineThreshold)
            {
                MineDust(cell, kind, 3);
                return;
            }

            miningDamage = 0;
            TerraWorld.Set(cell.x, cell.y, CellKind.Empty);
            var drop = ItemDb.DropFor(kind);
            // WorldGen.KillTile_DropItems: the block pops out of the tile's middle.
            if (drop != ItemType.None && !Inventory.Infinite) WorldItems.Drop(ItemStack.Of(drop), CellGrid.CellCenter(cell.x, cell.y));
            PlaySound("Dig_" + Random.Range(0, 3));
            MineDust(cell, kind, 10);
            // Sand above, or rope hanging under it, may have lost what held it.
            FallingSand.Instance?.Check(cell.x, cell.y + 1);
        }

        /// <summary>Tile dust: dirt 0, stone 1, wood 7, sand 32, torch 6 (WorldGen.KillTile_MakeTileDust).</summary>
        internal static void MineDust(Vector2Int cell, CellKind kind, int count)
        {
            var p = Effects.TerraParticles.Instance;
            if (p == null) return;
            int type = TileDustType(kind);
            var corner = Effects.TerraParticles.ToPx(new Vector2(cell.x * CellGrid.CellSize, (cell.y + 1) * CellGrid.CellSize));
            for (int i = 0; i < count; i++) p.NewDust(corner, 16, 16, type);
        }

        /// <summary>The dust a block of this kind gives (WorldGen.KillTile_MakeTileDust).</summary>
        internal static int TileDustType(CellKind kind) =>
            kind == CellKind.Dirt ? 0 : kind == CellKind.Wood || kind == CellKind.Platform || kind == CellKind.Rope ? 7
            : kind == CellKind.Sand ? 32 : kind == CellKind.Torch ? 6 : kind == CellKind.MagicIce ? 67 : 1;

        /// <summary>
        /// Terraria's placement rules. A plain block: the cell is free and either touches a tile
        /// on one of its four sides or has a wall behind it (here: was dug out of the scene's
        /// terrain). A torch: a block under it or beside it, or a wall. Rope: hangs from a block
        /// or rope above, or continues rope below. Platforms: also beside anything diagonally, and,
        /// like blocks (Collision.EmptyTile), never inside the player or an enemy.
        /// </summary>
        public static bool CanPlace(Vector2Int cell, CellKind kind = CellKind.Stone)
        {
            int x = cell.x, y = cell.y;
            if (!TerraWorld.InWorld(x, y) || TerraWorld.Get(x, y) != CellKind.Empty) return false;
            switch (kind)
            {
                case CellKind.Torch:
                    if (!(TerraWorld.IsSolid(x, y - 1) || TerraWorld.IsSolid(x - 1, y) || TerraWorld.IsSolid(x + 1, y) || TerraWorld.IsDug(x, y))) return false;
                    return true; // nothing collides with a torch
                case CellKind.Rope:
                    return TerraWorld.IsSolid(x, y + 1) || TerraWorld.Get(x, y + 1) == CellKind.Rope || TerraWorld.Get(x, y - 1) == CellKind.Rope;
            }
            bool anchored = TerraWorld.IsOccupied(x - 1, y) || TerraWorld.IsOccupied(x + 1, y)
                            || TerraWorld.IsOccupied(x, y - 1) || TerraWorld.IsOccupied(x, y + 1)
                            || TerraWorld.IsDug(x, y);
            // Platforms (PlaceThing_Tiles_BlockPlacementForAssortedThings' fallback): anything in
            // the 3×3 around, diagonals and torches included, or a wall behind a side neighbour.
            if (kind == CellKind.Platform && !anchored)
            {
                for (int dx = -1; dx <= 1 && !anchored; dx++)
                    for (int dy = -1; dy <= 1 && !anchored; dy++)
                        if ((dx != 0 || dy != 0) && TerraWorld.IsOccupied(x + dx, y + dy)) anchored = true;
                anchored |= TerraWorld.IsDug(x - 1, y) || TerraWorld.IsDug(x + 1, y) || TerraWorld.IsDug(x, y - 1) || TerraWorld.IsDug(x, y + 1);
            }
            if (!anchored) return false;
            // Never place a block inside the hero (layer 9) or an enemy (layer 11).
            var center = CellGrid.CellCenter(x, y);
            var size = Vector2.one * (CellGrid.CellSize * 0.9f);
            return Physics2D.OverlapBox(center, size, 0f, (1 << 9) | (1 << 11)) == null;
        }

        /// <summary>
        /// Equipped accessories: Terraria's movement for the boots and the shield, the Knight's own
        /// double jump and wall slide (on Terraria numbers) for the bottle and the claws.
        /// </summary>
        void ApplyAccessories()
        {
            Equipment.Update(Inventory);
            var bonus = Equipment.Current;
            PlayerHooks.Abilities.Dash = false;
            TerraMovement.JumpCloud = bonus.JumpCloud || Inventory.HasEquipped(AccessoryEffect.CloudInABottle);
            TerraMovement.JumpSandstorm = bonus.JumpSandstorm;
            TerraMovement.JumpBlizzard = bonus.JumpBlizzard;
            TerraMovement.JumpFart = bonus.JumpFart;
            TerraMovement.JumpSail = bonus.JumpSail;
            PlayerHooks.Abilities.DoubleJump = TerraMovement.JumpCloud || TerraMovement.JumpSandstorm || TerraMovement.JumpBlizzard || TerraMovement.JumpFart
                                               || TerraMovement.JumpSail;
            // spikedBoots: the Climbing Claws and the Shoe Spikes one each.
            TerraMovement.SpikedBoots = bonus.SpikedBoots + (Inventory.HasEquipped(AccessoryEffect.ClimbingClaws) ? 1 : 0);
            PlayerHooks.Abilities.WallCling = TerraMovement.SpikedBoots > 0;
            // Sets apply after accessories: the Crystal Assassin's dash, the Solar dash while shielded.
            TerraMovement.DashKind = ArmorEffects.DashKind(bonus.DashKind);
            TerraMovement.Magiluminescence = bonus.Magiluminescence;
            TerraMovement.PortableStool = bonus.PortableStool;
            // The fastest boots' accRunSpeed (Hermes 6, Spectre 6, Lightning 6.75).
            TerraMovement.AccRunSpeed = Mathf.Max(Inventory.HasEquipped(AccessoryEffect.HermesBoots) ? BootsRunSpeed : 3f, bonus.AccRunSpeed);
            Stats.Defense = bonus.Defense;
            Stats.LifeRegenBonus = bonus.LifeRegen;
            Stats.ManaRegenBonus = bonus.ManaRegenBonus;
            Stats.Endurance = bonus.Endurance;
            Stats.ShinyStone = bonus.ShinyStone;
            Stats.LifeForce = bonus.LifeForce;
            Stats.ManaRegenBuff = bonus.ManaRegenBuff;
            Stats.UpdateMaximums(PlayerData.instance, bonus.ManaMax);
            // Panic! (the Panic Necklace after a hit) adds 1 to moveSpeed.
            TerraMovement.MoveSpeed = 1f + bonus.MoveSpeed + (PanicTicks > 0f ? 1f : 0f);
            TerraMovement.JumpBoost = bonus.JumpBoost;
            TerraMovement.JumpSpeedBoost = bonus.JumpSpeedBoost;
            TerraMovement.AutoJump = bonus.AutoJump;
            TerraMovement.RocketKind = Mathf.Max(bonus.RocketKind, bonus.RocketBoots ? 1 : 0);
            // The wings' numbers by their slot; the merfolk can't fly (Pl:3976-3984, 25913-25916).
            var wing = Equipment.MerfolkNow ? null : TerraWings.Get(bonus.WingSlot);
            TerraMovement.Wing = wing;
            TerraMovement.WingTimeMax = wing != null ? wing.FlyTime : 0;
            TerraMovement.EmpressBrooch = bonus.EmpressBrooch;
            TerraMovement.RunDust = bonus.RunDust;
            TerraMovement.FairyDust = bonus.FairyDust;
            TerraMovement.DesertBoots = bonus.DesertBoots;
            TerraMovement.FlameTrail = bonus.FlameTrail;
            TerraMovement.Werewolf = Equipment.WerewolfNow;
            TerraMovement.DownDash = bonus.DownDash;
            TerraMovement.WingRunSpeed = wing != null ? wing.Speed : 0f;
            TerraMovement.FlyingCarpet = bonus.FlyingCarpet;
            // Philosopher's Stone: potion sickness left is rescaled as it goes on or comes off (AdjustRemainingPotionSickness).
            if (bonus.PStone != pStoneWorn)
            {
                Stats.PotionSickness *= bonus.PStone ? 0.75f : 1f / 0.75f;
                pStoneWorn = bonus.PStone;
            }
            UpdateHelmetLight(bonus.Light);

            // Diagnostics: a player lost their accessories once without knowing why; note every change.
            var sb = new System.Text.StringBuilder();
            foreach (var a in Inventory.EffectiveAccessories) if (!a.IsEmpty) sb.Append(a.Def.Name).Append(' ');
            if (sb.Length > 0) sb.Append($"（配装{Inventory.CurrentLoadout + 1}）");
            string equipped = sb.ToString();
            if (equipped != lastEquipped)
            {
                Plugin.Log.LogInfo($"[饰品] 装备的饰品变为：{(equipped.Length > 0 ? equipped : "无")}（之前：{(lastEquipped ?? "-")}），场景 {GameManager.UnsafeInstance?.sceneName}");
                lastEquipped = equipped;
            }
        }

        string lastEquipped;
        bool pStoneWorn;
        SpriteRenderer helmetLight;

        /// <summary>Ticks of Panic! left (480 after a hit with the Panic Necklace).</summary>
        public float PanicTicks { get; set; }

        SpriteRenderer heldTorchLight;

        /// <summary>
        /// ItemCheck_EmitHeldItemLight: a torch in hand lights up around it as a placed one does,
        /// at its flame (12 px out, 14 px up from where it is held), in use or not; not under water.
        /// </summary>
        void UpdateHeldTorchLight(ItemDef held, Bounds b, float facing)
        {
            var hero = HeroController.SilentInstance;
            bool on = held != null && held.Places == CellKind.Torch && hero != null && playerSprite.gameObject.activeSelf && !hero.cState.swimming;
            if (on && heldTorchLight == null)
            {
                heldTorchLight = Glow.CreateLight(transform, CellVisuals.TorchLight);
                heldTorchLight.gameObject.AddComponent<Glow.Flicker>();
            }
            if (heldTorchLight == null) return;
            if (heldTorchLight.gameObject.activeSelf != on) heldTorchLight.gameObject.SetActive(on);
            if (!on) return;
            heldTorchLight.transform.position = new Vector3(b.center.x + facing * 0.5f, b.center.y + 0.3f, Glow.Z);
        }

        /// <summary>The mining helmet's light: a glow around the player's head while it's worn.</summary>
        void UpdateHelmetLight(bool on)
        {
            var hero = HeroController.SilentInstance;
            on &= hero != null && playerSprite.gameObject.activeSelf;
            if (on && helmetLight == null) helmetLight = Glow.CreateLight(transform, new Color(1f, 0.95f, 0.85f, 0.16f));
            if (helmetLight == null) return;
            if (helmetLight.gameObject.activeSelf != on) helmetLight.gameObject.SetActive(on);
            if (!on) return;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            helmetLight.transform.position = new Vector3(b.center.x, b.max.y - 0.1f, Glow.Z);
        }

        const float DoubleTapWindow = 15f / 60f; // Terraria's dash double-tap window
        int lastTapDirection;
        float lastDownTap = -10f;
        float lastTapTime;
        int lastPausedFrame = -10;

        /// <summary>
        /// Player.dropItemCheck / DropSelectedItem: the whole stack flies out of the player (see
        /// <see cref="WorldItems.Throw"/>), then nothing is used for 10 ticks. With the inventory
        /// open it is the cursor's stack, else the selected one. A favourite refuses: the Throw key
        /// leaves it be; a right click puts it back in the inventory and throws only what doesn't fit.
        /// </summary>
        public void ThrowHeld(bool rightClick)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || hero.cState.dead || IsUsingItem || useCooldown > 0f || !GameHooks.PlayerHasControl()) return;
            bool fromMouse = InventoryGui.Open && !Inventory.Mouse.IsEmpty;
            if (rightClick && !fromMouse) return;
            ref var stack = ref fromMouse ? ref Inventory.Mouse : ref Inventory.Slots[Inventory.Held];
            if (stack.IsEmpty) return;
            if (stack.Favorite)
            {
                if (!rightClick) return;
                int left = Inventory.Pickup(stack.Type, stack.Count, true);
                if (left < stack.Count) PlaySound("Grab", 0.7f);
                stack.Count = left;
                if (left <= 0)
                {
                    stack = default;
                    return;
                }
            }
            WorldItems.Throw(stack, hero);
            stack = default;
            useCooldown = 10f / 60f;
        }

        /// <summary>
        /// The inventory closing with an item on the cursor (Player.GetOrDropItem): what fits goes
        /// back in (the Grab sound, no popup), the rest drops at the player with a random hop.
        /// </summary>
        public void ReturnOrDropMouseItem()
        {
            var m = Inventory.Mouse;
            if (m.IsEmpty) return;
            int left = Inventory.Pickup(m.Type, m.Count, m.Favorite);
            if (left < m.Count) PlaySound("Grab", 0.7f);
            Inventory.Mouse = default;
            var hero = HeroController.SilentInstance;
            if (left > 0 && hero != null) WorldItems.Drop(ItemStack.Of(m.Type, left), hero.GetComponent<BoxCollider2D>().bounds.center);
            else if (left > 0) Inventory.Mouse = ItemStack.Of(m.Type, left);
        }

        /// <summary>The inventory's Settings button: Terraria's in-game settings (UI.IngameOptions).</summary>
        public void OpenSettings()
        {
            InventoryGui.Open = false;
            ReturnOrDropMouseItem();
            UI.IngameOptions.Show();
        }

        /// <summary>The game's real pause and options menu (the in-game settings' "游戏菜单").</summary>
        public void OpenGameMenu()
        {
            InventoryGui.Open = false;
            ReturnOrDropMouseItem();
            var gm = GameManager.UnsafeInstance;
            if (gm == null) return;
            // The pause action is off during play (Escape opens the inventory); switch it back on now
            // so the menu can be closed with Escape.
            TerraControls.Apply(InputHandler.Instance, false);
            gm.StartCoroutine(gm.PauseGameToggleByMenu());
        }

        /// <summary>
        /// Picking up one of the Knight's abilities (Mothwing Cloak, Mantis Claw, Monarch Wings,
        /// buying Sprintmaster) gives the Terraria accessory that does the same job instead.
        /// </summary>
        void GrantAbilityAccessories(float dt)
        {
            abilityCheckTimer -= dt;
            if (abilityCheckTimer > 0f) return;
            abilityCheckTimer = 1f;
            var pd = PlayerData.instance;
            if (pd == null) return;
            foreach (var (flag, item) in PlayerHooks.AbilityItems)
            {
                if (GrantedAbilities.Contains(flag) || !pd.GetBool(flag)) continue;
                GrantedAbilities.Add(flag);
                if (Inventory.Owns(item)) continue;
                Inventory.Add(item);
                var hero = HeroController.SilentInstance;
                if (hero != null) PopupText.Show(ItemDb.Get(item), 1, hero.GetComponent<BoxCollider2D>().bounds.center);
                PlaySound("Grab");
                Plugin.Log.LogInfo($"{flag} -> {ItemDb.Get(item).Name}");
            }
        }

        /// <summary>Holds the configured frame rate; the game resets vsync and its cap from its own settings.</summary>
        void HoldFrameRate()
        {
            if (TargetFrameRate <= 0) return;
            if (QualitySettings.vSyncCount != 0) QualitySettings.vSyncCount = 0;
            if (Application.targetFrameRate != TargetFrameRate) Application.targetFrameRate = TargetFrameRate;
        }

        /// <summary>
        /// Dev: one use of an item aimed at a point, as a click there would be (the item goes in
        /// the last hotbar slot for it; what was there comes back afterwards).
        /// </summary>
        public void DevUse(ItemType type, Vector2 aim)
        {
            var def = ItemDb.Get(type);
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            int slot = Inventory.HotbarSize - 1;
            var saved = Inventory.Slots[slot];
            int savedSelected = Inventory.Selected, savedOverride = Inventory.Override;
            Inventory.Slots[slot] = ItemStack.Of(type, def.MaxStack > 1 ? 10 : 1);
            Inventory.Selected = slot;
            Inventory.Override = -1;
            AimPoint = aim;
            var cell = CellGrid.WorldToCell(aim);
            bool usable = (def.PickPower > 0 || def.Places != CellKind.Empty) && FindTarget(hero, def, aim, out cell, out _);
            useCooldown = 0f;
            Use(def, cell, usable, aim, null);
            Inventory.Slots[slot] = saved;
            Inventory.Selected = savedSelected;
            Inventory.Override = savedOverride;
        }

        /// <summary>
        /// Dev: a use of the selected item aimed at a point, as a click there (the stack is used up as usual).
        /// With target, a pickaxe or a block looks for its cell the way a click does (FindTarget), so it mines or places.
        /// </summary>
        public void DevUseSelected(Vector2 aim, bool alt = false, bool target = false)
        {
            var def = Inventory.Current.Def;
            var hero = HeroController.SilentInstance;
            if (hero == null || def == null) return;
            AimPoint = aim;
            useCooldown = 0f;
            AltFunction = alt && def.AltUse;
            var cell = CellGrid.WorldToCell(aim);
            bool usable = target && (def.PickPower > 0 || def.Places != CellKind.Empty) && FindTarget(hero, def, aim, out cell, out _);
            Use(def, cell, usable, aim, null);
        }

        /// <summary>The staff's right-click use: a swing with its sound, no summon; the minion target picked or dropped.</summary>
        void AimMinions(ItemDef def, Vector2 mouse)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            UseDirection = mouse.x < hero.transform.position.x ? -1f : 1f;
            useCooldown = def.UseAnimation;
            StartSwing(def);
            Minions.Instance?.AimTarget(mouse);
        }

        /// <summary>Dev: a right click with a staff at a point (the minion target).</summary>
        public void DevAimMinions(ItemType staff, Vector2 at)
        {
            useCooldown = 0f;
            AimMinions(ItemDb.Get(staff), at);
        }

        /// <summary>Dev: where the drawn player is, and whether it's shown.</summary>
        public string DevSprite => playerSprite == null ? "无" : $"{playerSprite.transform.position:F2} 显示={playerSprite.gameObject.activeInHierarchy}";

        /// <summary>Dev: whether the next use may start (the held button's repeat).</summary>
        public bool DevReady => useCooldown <= 0f;

        /// <summary>Dev: swing an item towards a side without the mouse.</summary>
        public void TestSwing(ItemType type, float direction)
        {
            UseDirection = direction;
            StartSwing(ItemDb.Get(type));
        }

        /// <summary>Dev: a pickaxe use aimed at one object, as when clicking a geo rock.</summary>
        public void TestStrike(Collider2D target, float direction)
        {
            var pick = ItemDb.Get(ItemType.CopperPickaxe);
            UseDirection = direction;
            StartSwing(pick);
            melee.Strike(target, pick, direction);
            PlaySound("Tink_" + Random.Range(0, 3));
        }

        void StartSwing(ItemDef def)
        {
            swingItem = def;
            heldProgress = -1f;
            // Items not drawn in hand (the Zenith) still swing the arm.
            swingSprite = def.NoUseGraphic ? null : ItemSprite(def.Type);
            // Terraria draws held swords diagonally from the handle, so the blade spans the diagonal.
            bladeLength = swingSprite != null ? swingSprite.bounds.size.magnitude * Equipment.ItemScale(def) : 0f;
            swingDuration = Mathf.Max(def.UseAnimation * Equipment.UseScale(def), 0.1f);
            swingTime = 0f;
            BeginUse(def);
            UpdateSwing(0f);
        }

        /// <summary>The start of each use, the first or a repeat while the button is held.</summary>
        void BeginUse(ItemDef def)
        {
            UseSerial++;
            if (def.UseSound != null) PlaySound(def.UseSound, 0.6f * def.UseSoundVolume);
            if (!def.NoMelee) melee.Begin(def, UseDirection);
            finalFractalShots = 0;
            // Swords that shoot (their slashes, the stab, Light's Bane's cut, the leaf) turn the
            // player to the cursor and shoot once as each use starts (shootsEveryUse).
            var hero = HeroController.SilentInstance;
            if (hero != null && def.Fx != MeleeFx.None && def.Fx != MeleeFx.Muramasa && def.Fx != MeleeFx.Volcano)
            {
                Vector2 aim = DevAim ?? AimPoint;
                UseDirection = aim.x < hero.transform.position.x ? -1f : 1f;
                SwordFx.Instance?.OnUse(def, hero, UseDirection, aim);
            }
        }

        /// <summary>Dev: Shift held for Auto Select.</summary>
        public static bool DevAutoSelect;

        /// <summary>
        /// Terraria's Auto Select (Player.SmartSelectLookup with the selection override, 1.4.5):
        /// while Shift is held the hand takes the tool for what the cursor is on. A block in a
        /// pickaxe's reach: the first pickaxe. Anything else, the air or a block out of reach: the
        /// first torch, or past 200 px (12.5 tiles, height counted 1.3 times) the last one unless a
        /// torch is selected already. Nothing to take: the selected item stays. It changes only
        /// between uses (CanChangeSelectedItemImmediately) and lets go with Shift; the item on the
        /// cursor (inventory open) comes first, and over the interface Shift is the interface's.
        /// </summary>
        void AutoSelect(HeroController hero, Vector2 mouse)
        {
            if (IsUsingItem) return;
            var kb = Keyboard.current;
            bool held = DevAutoSelect || (!DevNoRealMouse && kb != null && TerraKeys.Held(TerraKeys.Action.AutoSelect) && !Net.NetChat.Typing);
            if (!held || InventoryGui.MouseOverUi || (InventoryGui.Open && !Inventory.Mouse.IsEmpty))
            {
                Inventory.Override = -1;
                return;
            }
            int pick = -1, firstTorch = -1, lastTorch = -1;
            for (int i = 0; i < Inventory.Size; i++)
            {
                var s = Inventory.Slots[i];
                if (s.IsEmpty) continue;
                if (pick < 0 && s.Def.PickPower > 0) pick = i;
                if (s.Def.Places == CellKind.Torch)
                {
                    if (firstTorch < 0) firstTorch = i;
                    lastTorch = i;
                }
            }
            var bounds = hero.GetComponent<BoxCollider2D>().bounds;
            var cell = CellGrid.WorldToCell(mouse);
            int slot = Inventory.Selected;
            if (TerraWorld.Get(cell.x, cell.y) != CellKind.Empty && pick >= 0 && InReach(bounds, Inventory.Slots[pick].Def, cell)) slot = pick;
            else
            {
                float dx = (mouse.x - bounds.center.x) * 32f, dy = (mouse.y - bounds.center.y) * 32f * 1.3f;
                bool far = dx * dx + dy * dy > 200f * 200f;
                var selected = Inventory.Slots[Inventory.Selected];
                if (!far && firstTorch >= 0) slot = firstTorch;
                else if (far && lastTorch >= 0 && (selected.IsEmpty || selected.Def.Places != CellKind.Torch)) slot = lastTorch;
            }
            Inventory.Override = slot == Inventory.Selected ? -1 : slot;
        }

        /// <summary>The item being swung or used right now (null when none).</summary>
        public ItemDef SwingItem => swingItem;

        /// <summary>TryTogglingShield raising it: itemAnimation, itemTime and reuseDelay all 0.</summary>
        public void CancelUse()
        {
            if (swingItem == null) return;
            swingItem = null;
            melee.End();
            useCooldown = 0f;
        }

        /// <summary>Player.ApplyAttackCooldown(frames): no swing hits an enemy for that long.</summary>
        public void AttackCooldown(int ticks) => MeleeHits.NoHitUntil = Mathf.Max(MeleeHits.NoHitUntil, Time.time + ticks / 60f);

        float swingDustTicks;

        /// <summary>
        /// Player.itemRotation for a swing (useStyle 1), in Terraria's radians (clockwise): from
        /// behind the head to the feet in front over the use.
        /// </summary>
        public float ItemRotation
        {
            get
            {
                if (swingItem == null || swingDuration <= 0f) return 0f;
                float remaining = 1f - SwingProgress;
                return (remaining - 0.5f) * -UseDirection * 3.5f - UseDirection * 0.3f;
            }
        }

        /// <summary>The hand holding a swung item, in the world.</summary>
        public Vector2 HandWorld
        {
            get
            {
                var hero = HeroController.SilentInstance;
                return hero == null ? Vector2.zero : (Vector2)hero.transform.position + new Vector2(0.1f * UseDirection, -0.15f);
            }
        }

        /// <summary>
        /// The Zenith's shots: one at the start of the use and one every useTime, three in all,
        /// each turning the player to face the cursor (Player.ItemCheck_Shoot).
        /// </summary>
        void ThrowFinalFractals(HeroController hero)
        {
            int due = Mathf.Min(3, Mathf.FloorToInt(swingTime / Mathf.Max(swingItem.UseTime, 0.01f)) + 1);
            while (finalFractalShots < due)
            {
                Vector2 center = hero.GetComponent<BoxCollider2D>().bounds.center;
                Vector2 aim = DevAim ?? (MouseWorld(out var m) ? m : center + Vector2.right * UseDirection * 5f);
                UseDirection = aim.x < center.x ? -1f : 1f;
                ZenithSwords.Instance?.Throw(finalFractalShots, swingItem, center, aim);
                finalFractalShots++;
            }
        }

        void UpdateSwing(float dt)
        {
            if (swingItem == null) return;
            var hero = HeroController.SilentInstance;
            swingTime += dt;
            if (hero == null || swingTime >= swingDuration)
            {
                // Holding the button keeps swinging, like Terraria's auto-reuse.
                bool overUi = InventoryGui.Open && InventoryGui.MouseOverUi;
                // Shooting items start each use themselves (they need ammo or mana for it).
                if (hero != null && !DevNoRealMouse && Mouse.current != null && Mouse.current.leftButton.isPressed && !overUi && swingItem.Shoot == ProjKind.None && Equipment.AutoReuses(swingItem)
                    && !swingItem.IsSummon && !swingItem.IsWhip
                    && swingItem == Inventory.Current.Def && GameHooks.PlayerHasControl())
                {
                    swingTime = 0f;
                    BeginUse(swingItem);
                }
                else
                {
                    swingItem = null;
                    heldProgress = -1f;
                    melee.End();
                    return;
                }
            }
            if (swingItem.ShootsFinalFractal) ThrowFinalFractals(hero);
            if (swingItem.NoMelee) return;
            Vector2 p = hero.transform.position;
            if (swingItem.Thrust)
            {
                // useStyle 3: the item's own box once it is out (ItemCheck_GetMeleeHitbox), Terraria px to world.
                float remaining = 1f - SwingProgress;
                if (swingSprite != null && MeleeSpecials.ThrustBox(swingItem, TerraMovement.TopLeft(hero), UseDirection < 0f ? -1 : 1, remaining,
                        swingSprite.rect.width, swingSprite.rect.height, Equipment.ItemScale(swingItem), out var r))
                    melee.TickBox(new Rect(Effects.TerraParticles.ToWorld(new Vector2(r.x, r.y + r.height)), r.size * Effects.TerraParticles.Px));
            }
            else melee.Tick(p + new Vector2(0.1f * UseDirection, -0.15f), bladeLength);
            // ItemCheck_EmitUseVisuals: the items' motes off the blade, once a tick.
            swingDustTicks += dt * 60f;
            for (; swingDustTicks >= 1f; swingDustTicks -= 1f)
            {
                var rb = hero.GetComponent<Rigidbody2D>();
                var v = rb != null ? rb.linearVelocity / TerraMovement.PxT : Vector2.zero;
                MeleeSpecials.SwingAnimMax = Mathf.Max(1, Mathf.RoundToInt(swingDuration * 60f));
                MeleeSpecials.SwingAnim = Mathf.RoundToInt((1f - SwingProgress) * MeleeSpecials.SwingAnimMax);
                MeleeSpecials.SwingDust(swingItem, melee.SwingBox, UseDirection, new Vector2(v.x, -v.y));
            }
        }

        /// <summary>Hides the Knight and poses the Terraria character over the hero's collider.</summary>
        void PosePlayer()
        {
            var hero = HeroController.SilentInstance;
            if (hero != null) TerraHurt.Tick(hero, playerSprite);
            // The Knight is never drawn (forceRenderingOff), but its enabled flag is left to the game: where the game
            // hides the Knight (a top entrance's wait, as the opening fall into King's Pass; a dream gate; its cutscenes'
            // SetVisibility), the Terraria character is hidden too. A dead player is drawn as the pieces he fell apart into.
            var knight = hero != null ? hero.GetComponent<MeshRenderer>() : null;
            if (knight != null && !knight.forceRenderingOff) knight.forceRenderingOff = true;
            bool show = hero != null && !hero.cState.dead && TerrainSystem.Current != null && GameHooks.IsGameplay(GameManager.UnsafeInstance)
                        && (knight == null || knight.enabled) && !OpeningPortal.HideHero;
            if (playerSprite.gameObject.activeSelf != show) playerSprite.gameObject.SetActive(show);
            if (!show)
            {
                NetPose.Shown = false;
                if (hero != null) NetPose.Box = hero.GetComponent<BoxCollider2D>().bounds;
                return;
            }

            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            TerraMovement.TickVisuals(Time.deltaTime);
            // After a step up the drawn player eases up onto the step, as Terraria does.
            var feet = new Vector2(col.bounds.center.x, col.bounds.min.y + TerraMovement.StepOffset);
            // The Gravity Globe turned: the feet at the top of the box, drawn upside down.
            playerSprite.Upside = GravityGlobe.Flipped;
            if (GravityGlobe.Flipped) feet = new Vector2(col.bounds.center.x, col.bounds.max.y);
            // On the Portable Stool the player is drawn 26 px up, the feet staying put (PlayerDrawSet, PDS:429-430).
            var realFeet = feet;
            if (TerraMovement.StoolActive) feet.y += TerraMovement.StoolLift;
            float facing = IsUsingItem ? UseDirection : hero.cState.facingRight ? 1f : -1f;
            // The sandstorm jump spins the player (direction flipped every 4 ticks) unless an item is in use.
            bool spinning = TerraMovement.Sandstorm && !IsUsingItem;
            if (spinning) facing = TerraMovement.SpinFacing;
            // Terraria blinks the player out while immune after a hit.
            float opacity = TerraHurt.Opacity(hero, Time.deltaTime) * OpeningPortal.HeroOpacity;
            bool grounded = hero.cState.onGround;
            // Player.PlayerFrame: sliding down a wall and the shield dash in the air have their own body
            // frames; on the flying carpet the body takes frame 6 and the legs stand.
            int bodyOverride = !grounded && hero.cState.wallSliding ? 3 : !grounded && (TerraMovement.EocDash > 0f || TerraMovement.OnCarpet || spinning) ? 6 : -1;
            // With wings, in the air rising or holding jump: body frame 6 (Player.cs:36845); standing on the board or the Starboard: 0.
            if (bodyOverride < 0 && !grounded && TerraMovement.HasWings && (rb.linearVelocity.y > 0f || TerraMovement.JumpHeldNow)) bodyOverride = 6;
            bool standing = TerraMovement.WingStanding(hero);
            if (standing) bodyOverride = 0;
            DrawCarpet(col, facing);
            // What is in effect, shared items included (GetEffectiveArmor), is what's drawn.
            var armor = Inventory.EffectiveArmor;
            var equips = new TerraPlayerSprite.Equips
            {
                Head = armor[0].IsEmpty ? 0 : armor[0].Def.ArmorSheet,
                Body = armor[1].IsEmpty ? 0 : armor[1].Def.ArmorSheet,
                // The Flinx Fur Coat is a robe: its own legs (229) are drawn instead of the worn ones (SetMatch, wearsRobe).
                Legs = !armor[1].IsEmpty && armor[1].Type == ItemType.FlinxFurCoat ? 229 : armor[2].IsEmpty ? 0 : armor[2].Def.ArmorSheet,
                // GetHairSettings (Pl:16902-17043): of the hardmode heads only the Cobalt Hat (hatHair) and the Hallowed Headgear (fullHair).
                HairUnderHead = !armor[0].IsEmpty && (armor[0].Type == ItemType.MiningHelmet || armor[0].Type == ItemType.JungleHat
                                                     || armor[0].Type == ItemType.CobaltHat || armor[0].Type == ItemType.HallowedHeadgear),
                IceBarrier = Equipment.IceBarrierNow,
                BrainCounter = TerraHurt.BrainDodgeTicks,
            };
            ArmorLook(ref equips);
            // The werewolf at night and the merfolk in acid wear their own head, body and legs, without capes (Pl:36175-36210).
            if (Equipment.WerewolfNow || Equipment.MerfolkNow)
            {
                bool wolf = Equipment.WerewolfNow;
                equips.Head = wolf ? 38 : 39;
                equips.Body = wolf ? 21 : 22;
                equips.Legs = wolf ? 20 : 21;
                equips.HairUnderHead = false;
                equips.HideCapes = true;
            }
            // Player.UpdateVisibleAccessories: each worn accessory's sheets, a later slot over an earlier one.
            foreach (var a in Inventory.EffectiveAccessories)
            {
                if (a.IsEmpty) continue;
                var d = a.Def;
                if (d.Shoe > 0) equips.Shoe = d.Shoe;
                if (d.Waist > 0) equips.Waist = d.Waist;
                if (d.Balloon > 0) equips.Balloon = d.Balloon;

                if (d.Shield > 0) equips.Shield = d.Shield;
                if (d.Neck > 0) equips.Neck = d.Neck;
                if (d.Back > 0) equips.Back = d.Back;
                if (d.HandOn > 0) equips.HandOn = d.HandOn;
                if (d.HandOff > 0) equips.HandOff = d.HandOff;
                if (d.Face > 0) equips.Face = d.Face;
                if (d.Front > 0) equips.Front = d.Front;
            }
            // Pl:37087-37091: the Sergeant United Shield in hand and not out is worn as shield 9.
            if (MeleeSpecials.BouncingShieldInHand) equips.Shield = 9;
            var hold = new TerraPlayerSprite.Hold();
            var stab = swingItem != null && swingItem.Fx == MeleeFx.Shortsword && SwordFx.Instance != null ? SwordFx.Instance.StabDirection : Vector2.zero;
            if (stab != Vector2.zero)
            {
                // The stab: the front arm reaches along it (Terraria px, y down).
                hold.Out = true;
                hold.Angle = Mathf.Clamp(Mathf.Atan2(-stab.y, Mathf.Abs(stab.x) < 0.01f ? 0.01f : stab.x * facing) * Mathf.Rad2Deg, -85f, 85f);
            }
            else if (swingItem != null && swingItem.Thrust)
            {
                hold.Thrust = true;
                hold.Item = swingItem;
            }
            else if (swingItem != null && swingItem.HoldOut)
            {
                var to = shotDir != Vector2.zero ? shotDir : AimPoint - (Vector2)col.bounds.center;
                hold.Out = true;
                hold.Angle = Mathf.Atan2(to.y, Mathf.Abs(to.x) < 0.01f ? 0.01f : to.x * facing) * Mathf.Rad2Deg;
                hold.Angle = Mathf.Clamp(hold.Angle, -85f, 85f);
                hold.Staff = swingItem.StaffHold || swingItem.Type == ItemType.AmethystStaff || swingItem.Type == ItemType.RubyStaff || swingItem.Type == ItemType.DiamondStaff;
                hold.Offset = swingItem.HoldOffset;
                hold.Item = swingItem;
            }
            if (--coverCheckFrames <= 0)
            {
                coverCheckFrames = 4;
                var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
                Vector3 center = col.bounds.center;
                playerSprite.OutlineStrength = cam != null && TerraWorld.CoveredByScenery(center, cam.transform.position) ? 1f : 0f;
            }
            // A hold style while nothing is in use (the umbrellas over the head, the Zombie Arm, the Breathing Reed).
            Sprite shown = swingSprite;
            var heldDef = Inventory.Current.Def;
            if (swingItem == null && MeleeSpecials.HoldLocation(heldDef, out var holdLoc, out var holdRot, out var holdBody))
            {
                hold.Idle = true;
                hold.IdleLoc = holdLoc;
                hold.IdleRot = holdRot;
                hold.IdleBody = holdBody;
                shown = ItemSprite(heldDef.Type);
            }
            UpdateHeldTorchLight(heldDef, col.bounds, facing);
            hold.ShieldUp = MeleeSpecials.ShieldRaised;
            var glowDef = swingItem ?? (hold.Idle ? heldDef : null);
            playerSprite.HeldGlow = glowDef != null && glowDef.Glow != null ? GlowSprite(glowDef) : null;
            var poseVel = GravityGlobe.Flipped ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) : rb.linearVelocity;
            float poseSwing = IsUsingItem ? (swingItem != null && swingItem.HoldUp ? 0.5f : SwingProgress) : -1f;
            float poseScale = swingItem != null ? Equipment.ItemScale(swingItem) : hold.Idle ? Equipment.ItemScale(heldDef) : 1f;
            int poseLeg = TerraMovement.OnCarpet || spinning || standing ? 0 : -1;
            bool poseSeated = PlayerData.instance != null && PlayerData.instance.atBench;
            playerSprite.Pose(feet, poseVel, grounded, facing, poseSwing, shown, poseScale, opacity, bodyOverride, equips, TerraMovement.ShadowCount, hold, poseLeg, poseSeated);
            NetPose = new Net.PoseRecord
            {
                Shown = true, Feet = feet, Velocity = poseVel, Grounded = grounded, Upside = GravityGlobe.Flipped, Seated = poseSeated,
                WingAir = !grounded && !GrapplingHook.IsLatched, Glow = playerSprite.HeldGlow != null, Facing = facing, Swing = poseSwing,
                ItemScale = poseScale, Opacity = opacity,
                Item = shown == null ? ItemType.None : hold.Idle ? heldDef.Type : swingItem != null ? swingItem.Type : ItemType.None,
                BodyOverride = bodyOverride, LegOverride = poseLeg, Shadows = TerraMovement.ShadowCount,
                Wing = TerraMovement.Wing != null ? TerraMovement.Wing.Slot : 0, WingFrame = TerraWings.Frame,
                Equips = equips, Hold = hold, Box = col.bounds,
            };
            // DrawPlayer_09_Wings: drawn behind the player through the effect batch (their frames, glowmasks and copies differ by slot).
            if (playerSprite.gameObject.activeInHierarchy && TerraMovement.Wing != null && Effects.FxBatch.Instance != null)
            {
                // Turned over by the Gravity Globe the wings hang upside down with the player.
                var centrePx = Effects.TerraParticles.ToPx(playerSprite.transform.position);
                if (GravityGlobe.Flipped) Effects.FxBatch.Instance.MirrorAboutY = centrePx.y;
                TerraWings.Draw(Effects.FxBatch.Instance, TerraMovement.Wing, centrePx,
                    facing < 0f ? -1 : 1, opacity, !grounded && !GrapplingHook.IsLatched);
                Effects.FxBatch.Instance.MirrorAboutY = null;
            }
            if (playerSprite.gameObject.activeInHierarchy)
            {
                AccessoryLayers.Draw(Effects.FxBatch.Instance, equips, Equipment.Current, Effects.TerraParticles.ToPx(playerSprite.transform.position),
                    Effects.TerraParticles.ToPx(realFeet), facing < 0f ? -1 : 1, opacity, TerraPlayerSprite.BodyFrameNow, TerraPlayerSprite.BobPx);
                ArmorEffects.Draw(Effects.FxBatch.Instance, Effects.TerraParticles.ToPx(col.bounds.center), facing < 0f ? -1 : 1, opacity);
            }
        }

        /// <summary>
        /// The armor sets' look (SetArmorEffectVisuals, Pl:37285-37456; PDS:500-810): the set's
        /// copies, Holy Protection, the Spectre ghost, the stealth, and the lunar glowmasks by the
        /// worn sheets.
        /// </summary>
        static void ArmorLook(ref TerraPlayerSprite.Equips e)
        {
            var set = Equipment.FamilyOf(Equipment.FullSet);
            e.Fx = set switch
            {
                ArmorSet.Cobalt => TerraPlayerSprite.ArmorFx.Shadow,
                ArmorSet.Adamantite => TerraPlayerSprite.ArmorFx.Outlines,
                ArmorSet.Hallowed => TerraPlayerSprite.ArmorFx.Outlines | TerraPlayerSprite.ArmorFx.Shadow,
                ArmorSet.Chlorophyte => TerraPlayerSprite.ArmorFx.ShadowSubtle,
                ArmorSet.Shroomite => TerraPlayerSprite.ArmorFx.Shadow,
                ArmorSet.Forbidden => TerraPlayerSprite.ArmorFx.ShadowLokis | TerraPlayerSprite.ArmorFx.OutlinesForbidden,
                ArmorSet.Spooky => TerraPlayerSprite.ArmorFx.ShadowSubtle | TerraPlayerSprite.ArmorFx.Outlines,
                ArmorSet.Solar => TerraPlayerSprite.ArmorFx.Shadow | TerraPlayerSprite.ArmorFx.Outlines,
                ArmorSet.Vortex => TerraPlayerSprite.ArmorFx.Shadow,
                ArmorSet.Nebula => TerraPlayerSprite.ArmorFx.ShadowLokis | TerraPlayerSprite.ArmorFx.Outlines,
                ArmorSet.Stardust => TerraPlayerSprite.ArmorFx.Outlines,
                _ => TerraPlayerSprite.ArmorFx.None,
            };
            e.SocialGhost = set == ArmorSet.Spectre;
            e.Holy = PlayerBuffs.Has(ArmorEffects.HolyProtection);
            e.StealthKind = (byte)(set == ArmorSet.Shroomite ? 1 : set == ArmorSet.Vortex ? 2 : 0);
            e.Stealth = ArmorEffects.Stealth;
            // PlayerDrawSet:1563-1600: the Psycho Knife's hiding looks like the Shroomite set's.
            if (MeleeSpecials.PsychoHeld)
            {
                e.StealthKind = 1;
                e.Stealth = MeleeSpecials.Stealth;
            }
            e.HeadGlow = e.Head == 169 ? "Glow_15" : e.Head == 170 ? "Glow_16" : e.Head == 189 ? "Glow_184" : null;
            e.LegsGlow = e.Legs == 110 ? "Glow_199" : e.Legs == 111 ? "Glow_17" : e.Legs == 130 ? "Glow_187" : null;
            e.BodyGlow = e.Body == 175 || e.Body == 176 || e.Body == 190 ? e.Body : 0;
            e.BodyGlowArms = e.Body == 176 || e.Body == 190;
        }

        /// <summary>
        /// PlayerDrawLayers.DrawPlayer_03_Carpet: FlyingCarpet's six 64x20 frames under the player,
        /// centred 28 px below the middle of his hitbox (origin 15 px down the frame), behind him,
        /// mirrored when facing left.
        /// </summary>
        void DrawCarpet(BoxCollider2D col, float facing)
        {
            int frame = TerraMovement.CarpetFrame;
            if (frame < 0 || Effects.FxBatch.Instance == null) return;
            var sprite = Effects.FxBatch.Frame("FlyingCarpet", 0, frame * 20, 64, 20, 32f, 15f);
            var center = Effects.TerraParticles.ToPx(col.bounds.center);
            float light = 0.874f;
            Effects.FxBatch.Instance.Draw(sprite, center + new Vector2(0f, 28f), new Color(light, light, light, 1f), 0f, 1f, flipX: facing < 0f);
        }

        int coverCheckFrames;

        float fallPeak;
        bool wasGrounded = true;
        bool fallReset;

        /// <summary>A fall starts again from here (Terraria resets fallStart while hooked).</summary>
        public void ResetFall() => fallReset = true;

        /// <summary>
        /// Terraria fall damage: measured from where the player stopped rising, 10 per tile beyond
        /// 25 tiles. Scene entries, hazard respawns and other moves the game makes are not falls.
        /// </summary>
        void UpdateFallDamage(HeroController hero, float feetY, float vy)
        {
            bool grounded = hero.cState.onGround;
            if (fallReset || !GameHooks.PlayerHasControl() || hero.cState.hazardRespawning || hero.cState.dead)
            {
                fallPeak = feetY;
                wasGrounded = grounded;
                fallReset = false;
                return;
            }
            if (grounded && !wasGrounded)
            {
                int tiles = Mathf.FloorToInt((fallPeak - feetY) / CellGrid.CellSize);
                // 25 tiles (35 with the Frog Leg's extraFall) before it hurts; never with wings on (!hasWings).
                int safe = 25 + Equipment.Current.ExtraFall;
                if (tiles > safe && !DevNoFallDamage && !Equipment.Current.NoFallDamage && !TerraMovement.HasWings && !TerraHurt.DodgeFall(hero))
                {
                    int raw = (tiles - safe) * 10;
                    TerraHurt.BeforeHurt();
                    int damage = Stats.TakeDirect(raw);
                    TerraHurt.AfterHurt(hero, raw, damage, true);
                    CombatText.Show(hero.transform.position + Vector3.up * 0.9f, damage.ToString(), CombatText.PlayerHurt);
                    TerraHurt.LastDamage = damage;
                    if (!Stats.IsDead) PlaySound(Equipment.WerewolfNow ? "NPC_Hit_6" : "Player_Hit_" + Random.Range(0, 3));
                    // Out of life: let the game kill the hero through its normal damage path.
                    if (Stats.IsDead) hero.TakeDamage(hero.gameObject, GlobalEnums.CollisionSide.bottom, 1, 1);
                }
            }
            if (grounded || vy > 0f) fallPeak = feetY;
            wasGrounded = grounded;
        }

        /// <summary>The mouse projected onto the gameplay plane (z = 0); the main camera is perspective.</summary>
        public static bool MouseWorld(out Vector2 world)
        {
            world = default;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null || Mouse.current == null) return false;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return false;
            float t = -ray.origin.z / ray.direction.z;
            world = ray.origin + ray.direction * t;
            return true;
        }

        /// <summary>
        /// LegacySoundPlayer's rules: Item_1 is any of the three swing sounds, item sounds get a
        /// little random pitch, and a sound starting again cuts its last play short (except the
        /// few that may overlap).
        /// </summary>
        /// <summary>A sound at a pitch (XNA's -1..1, an octave down to an octave up): the instruments' notes (Main.musicPitch).</summary>
        public void PlaySound(string name, float volume, float pitch)
        {
            PlaySound(name, volume);
            var voice = lastPlayed;
            if (voice != null && voice.isPlaying) voice.pitch = Mathf.Pow(2f, Mathf.Clamp(pitch, -1f, 1f));
        }

        AudioSource lastPlayed;

        public void PlaySound(string name, float volume = 1f)
        {
            if (audioSource.outputAudioMixerGroup == null)
            {
                // Route through the game's mixer so its volume settings apply: the bus the hero's
                // "Actors" mixer feeds, not that mixer itself, whose snapshots (set by scenes and
                // gates) can turn actors right down.
                var heroSource = HeroController.SilentInstance != null ? HeroController.SilentInstance.GetComponent<AudioSource>() : null;
                var group = heroSource != null ? heroSource.outputAudioMixerGroup : null;
                if (group != null) audioSource.outputAudioMixerGroup = group.audioMixer.outputAudioMixerGroup ?? group;
            }
            bool item = name.StartsWith("Item_");
            if (name == "Item_1") name = Random.Range(0, 3) switch { 1 => "Item_18", 2 => "Item_19", _ => "Item_1" };
            if (name == "Item_157") volume *= 0.7f;
            // A sound with variants picked at random (the DD2 ones come in threes: SoundID's LegacySoundStyle with a variation range).
            // "_*4": four of them (the monk staffs' swings).
            int star = name.LastIndexOf("_*", System.StringComparison.Ordinal);
            if (star >= 0 && (star + 2 == name.Length || (star + 3 == name.Length && char.IsDigit(name[star + 2]))))
                name = name.Substring(0, star + 1) + Random.Range(0, star + 2 == name.Length ? 3 : name[star + 2] - '0');
            try
            {
                var clip = TerrariaAssets.Sound(name);
                var voice = Voice(name);
                voice.pitch = item ? Mathf.Pow(2f, Random.Range(-6, 7) * 0.01f) : 1f;
                voice.volume = volume;
                voice.clip = clip;
                voice.Play();
                lastPlayed = voice;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"sound {name}: {e.Message}");
            }
        }

        readonly List<AudioSource> voices = new List<AudioSource>();
        readonly Dictionary<string, AudioSource> lastVoice = new Dictionary<string, AudioSource>();
        static readonly HashSet<string> overlapping = new HashSet<string> { "Item_9", "Item_10", "Item_24", "Item_26", "Item_34", "Item_43", "Item_103", "Item_156", "Item_162", "Item_205" };
        int nextVoice;

        /// <summary>A source to play a sound on: the one it last played on (cutting it short), else a free one.</summary>
        AudioSource Voice(string name)
        {
            if (!overlapping.Contains(name) && lastVoice.TryGetValue(name, out var v) && v != null && v.isPlaying && v.clip != null && v.clip.name == "Terraria/" + name)
            {
                v.Stop();
                return v;
            }
            AudioSource pick = null;
            foreach (var s in voices)
                if (!s.isPlaying) { pick = s; break; }
            if (pick == null)
            {
                if (voices.Count < 16)
                {
                    pick = gameObject.AddComponent<AudioSource>();
                    pick.playOnAwake = false;
                    pick.spatialBlend = 0f;
                    voices.Add(pick);
                }
                else pick = voices[nextVoice++ % voices.Count];
            }
            pick.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            lastVoice[name] = pick;
            return pick;
        }

        static readonly Dictionary<ItemType, Sprite> itemSprites = new Dictionary<ItemType, Sprite>();

        static readonly Dictionary<ItemType, Sprite> glowSprites = new Dictionary<ItemType, Sprite>();

        /// <summary>An item's glowmask as a sprite pivoting like the held item (its bottom-left corner).</summary>
        internal static Sprite GlowSprite(ItemDef def)
        {
            if (glowSprites.TryGetValue(def.Type, out var s)) return s;
            try
            {
                var tex = TerrariaAssets.Texture(def.Glow);
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, CellVisuals.PixelsPerUnit);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"glowmask {def.Glow}: {e.Message}");
                s = null;
            }
            glowSprites[def.Type] = s;
            return s;
        }

        internal static Sprite ItemSprite(ItemType type)
        {
            if (itemSprites.TryGetValue(type, out var s)) return s;
            string name = ItemDb.Get(type).Texture;
            var tex = TerrariaAssets.Texture(name);
            // Held items pivot on the handle at the bottom-left corner; an animation sheet shows its first frame.
            s = Sprite.Create(tex, ItemFrames.PixelsOf(tex, name, 0), Vector2.zero, CellVisuals.PixelsPerUnit);
            itemSprites[type] = s;
            return s;
        }

        /// <summary>Terraria shows the pylon's item beside the cursor while it hovers one in reach.</summary>
        static void DrawPylonHover()
        {
            if (!Pylons.HoveringPylon || InventoryGui.Open || Event.current.type != EventType.Repaint) return;
            var icon = TerrariaAssets.Texture(Pylons.HoverIcon);
            var m = Event.current.mousePosition;
            float ui = GameView.Scale;
            GUI.DrawTexture(new Rect(m.x + 14f * ui, m.y + 14f * ui, icon.width * ui, icon.height * ui), icon);
        }

        void OnGUI()
        {
            try { DrawInterface(); }
            // Terraria's cursor over all of it.
            finally { UI.TerraCursor.Draw(); }
        }

        void DrawInterface()
        {
            // Labels never cut their text: the Chinese fallback font is taller than the boxes at some screen sizes.
            GUI.skin.label.clipping = TextClipping.Overflow;
            // The save profiles screen's creative mode and multiplayer buttons (main menu only).
            CreativeMenu.Draw();
            Net.NetMenu.Draw();
            // Fifty save slots: the profiles screen's pages.
            SaveSlots.Draw(CreativeMenu.IsOpen || Net.NetMenu.IsOpen);
            if (TerrainSystem.Current == null || !GameHooks.IsGameplay(GameManager.UnsafeInstance)) return;
            if (FullMap.Open)
            {
                FullMap.Draw();
                ResourceHud.Draw(Stats);
                return;
            }
            UI.WorldMap.DrawMini();
            EnemyHealthBars.Draw();
            CombatText.Draw();
            PopupText.Draw();
            EnemyHover.Draw();
            Net.NetSession.DrawGui();
            DrawPylonHover();
            TerraHurt.DrawDeathText();
            ResourceHud.Draw(Stats);
            InfoAccessories.Draw(Inventory, Stats);
            InventoryGui.Draw(Inventory, SmartCursor);
            BestiaryToast.Draw();
            Net.NetChat.Draw();
            UI.IngameOptions.Draw();
        }
    }
}
