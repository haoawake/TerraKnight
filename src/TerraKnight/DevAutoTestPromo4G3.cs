using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "p4_v5G_spell*_zh"): recorder G's clips of the four spells' class weapons for the 4.4 trailer, each weapon a
    /// take of its own against a room's enemies (the new AI): the Vengeful Spirit's (the Revenant Edge's wraiths, the pulse
    /// rifle's beam and charged wave, the spirit wave split into homing wraiths, the wandering soul), the Howling Wraiths'
    /// (the bouncing deathbell, the organ's three roars, the wailing three, the choir soul), the Shade Soul's (the three cuts
    /// and the thrown sword, the rift ray's marks burst, the shade ghost and its blink, the shade minion) and the Abyss
    /// Shriek's (the yoyo's ripples, the sniper's marks and bombs, the abyss three, the dirge laser).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        enum GUse { Hold, Spirit, Pulse, Rift, Ghost, Sniper, Summon, Melee }

        // ------------------------------------------------------------------ the gear of the stage

        bool gWings;
        float gHopAt;
        ItemType[] gSet = new ItemType[0];
        int gStage = 5;

        /// <summary>
        /// The loadout worn now dressed as a player of that stage and class would be (the boss suites' picks: the stage's best
        /// armour path for the class, its accessory path, DevAutoTestBossLoadouts), with the stage's way to get about: wings
        /// once there are any, else a Cloud in a Bottle and Hermes Boots; life and mana as far as the stage goes.
        /// </summary>
        void GStageGear(int stage, int cls)
        {
            Shop.DevStage = stage;
            gStage = stage;
            bool Usable(ItemType t) { var d = ItemDb.Get(t); return d != null && d.Type == t && d.Stage <= stage; }
            float ArmorScore(ProgressionBalance.ArmorPath path)
            {
                var b = new Bonuses();
                foreach (var t in path.Items) b.Add(ItemDb.Get(t).Bonus);
                var h = ItemDb.Get(path.Items[0]);
                bool same = true;
                foreach (var t in path.Items) if (ItemDb.Get(t).Set != h.Set) same = false;
                if (h.Set != ArmorSet.None && same)
                    b.Add(Equipment.SetBonus(h.Set == ArmorSet.Beetle ? ItemDb.Get(path.Items[1]).SetBonus : h.SetBonus != ArmorSet.None ? h.SetBonus : h.Set));
                if (cls == 3) return (1 + b.MinionDamage) * (1 + b.MaxMinions);
                float damage = cls == 0 ? b.MeleeDamage : cls == 1 ? b.RangedDamage : b.MagicDamage;
                float crit = b.AllCrit + (cls == 0 ? b.MeleeCrit : cls == 1 ? b.RangedCrit : b.MagicCrit);
                return (1 + damage) * (1.04f + crit) * (cls == 0 ? 1 + b.MeleeSpeed : 1);
            }
            ItemType[] armor = new ItemType[0];
            float best = float.MinValue;
            int bestStage = -1;
            foreach (var path in ProgressionBalance.ArmorPaths)
            {
                if (path.Stage > stage || ProgressionBalance.ClassIndex(path.Class) != cls) continue;
                bool ok = true;
                foreach (var t in path.Items) if (!Usable(t)) ok = false;
                if (!ok) continue;
                float score = ArmorScore(path);
                if (score > best || (score == best && path.Stage > bestStage)) { best = score; bestStage = path.Stage; armor = path.Items; }
            }
            var candidates = new List<ItemType>();
            for (int s = Mathf.Min(stage, ProgressionBalance.AccessoryPaths.Length - 1); s >= 0; s--)
                foreach (var t in ProgressionBalance.AccessoryPaths[s][cls])
                    if (Usable(t) && !candidates.Contains(t)) candidates.Add(t);
            var all = new List<ItemStack>();
            foreach (var t in candidates) all.Add(ItemStack.Of(t));
            var acc = new List<ItemType>();
            foreach (var t in candidates) if (!ProgressionBalance.Superseded(t, all) && acc.Count < 5) acc.Add(t);
            // Getting about: the stage's wings, else the bottle and the boots.
            ItemDef wing = null;
            foreach (var d in ItemDb.All)
                if (d != null && d.Bonus.WingSlot > 0 && d.Stage <= stage && Usable(d.Type) && !GLunar(d.Type) && d.Rare <= 8 && (wing == null || d.Stage > wing.Stage || (d.Stage == wing.Stage && d.Rare > wing.Rare))) wing = d;
            gWings = wing != null && stage >= 3;
            if (gWings) acc.Add(wing.Type);
            else acc.Add(ItemType.CloudinaBottle);
            if (acc.Count < Inventory.AccessorySlots && !acc.Contains(ItemType.HermesBoots) && Usable(ItemType.HermesBoots)) acc.Add(ItemType.HermesBoots);
            Equip(armor, acc.ToArray());
            var inv = mod.Inventory;
            inv.RefreshEffective();
            Equipment.Update(inv);
            var stats = mod.Stats;
            stats.LifeCrystals = new[] { 2, 5, 8, 11, 15, 15 }[stage];
            stats.LifeFruits = new[] { 0, 0, 0, 0, 6, 20 }[stage];
            stats.ManaCrystals = new[] { 1, 2, 3, 5, 7, 9 }[stage];
            stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            stats.FullHeal();
            var sb = new System.Text.StringBuilder();
            foreach (var t in armor) sb.Append(ItemDb.Get(t).Name).Append(' ');
            sb.Append('|');
            foreach (var t in acc) sb.Append(' ').Append(ItemDb.Get(t).Name);
            Plugin.Log.LogInfo($"[AutoTest] 录制G 阶段 {stage} 职业 {cls}：{sb}，翅膀 {gWings}");
        }

        /// <summary>The bag of a player of that stage and class: its weapons of the stage and the ones before it (the hotbar shows them).</summary>
        void GStageBag(int stage, int cls)
        {
            var inv = mod.Inventory;
            for (int i = 0; i < inv.Slots.Length; i++) if (!Inventory.IsCoinSlot(i)) inv.Slots[i] = default;
            int k = 0;
            for (int s = Mathf.Min(stage, ProgressionBalance.Core.Length - 1); s >= 0 && k < 6; s--)
                foreach (var t in ProgressionBalance.Core[s][cls])
                {
                    var d = ItemDb.Get(t);
                    if (d == null || d.Type != t || GLunar(t) || d.IsAccessory || d.IsArmor) continue;
                    if (k >= 6) break;
                    inv.Slots[k++] = ItemStack.Of(t, d.MaxStack > 1 ? Mathf.Min(d.MaxStack, 99) : 1);
                }
            inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.MusketBall, 999);
            inv.Selected = 0;
        }

        /// <summary>The Moon Lord's things (the lunar wings and the like): never the stage gear's.</summary>
        static bool GLunar(ItemType t)
        {
            string n = t.ToString();
            return n.Contains("Solar") || n.Contains("Vortex") || n.Contains("Nebula") || n.Contains("Stardust") || n.Contains("Lunar") || n == "WingsMoonLord";
        }

        /// <summary>
        /// A weapon of a spell's in hand: its class's loadout, dressed for the stage, its bag only the spell's weapons (the
        /// hotbar shows the set) and the rounds a gun of the stage fires.
        /// </summary>
        IEnumerator GTake(ItemType weapon, int loadout)
        {
            mod.DevHold = null;
            for (float t = 0f; t < 1.5f && mod.IsUsingItem; t += Time.deltaTime) { BKeep(); yield return null; }
            for (int k = 0; k < 20 && !mod.TrySwitchLoadout(loadout); k++) yield return null;
            yield return null;
            GStageGear(gStage, loadout);
            var inv = mod.Inventory;
            for (int i = 0; i < inv.Slots.Length; i++) if (!Inventory.IsCoinSlot(i)) inv.Slots[i] = default;
            int sel = 0;
            for (int i = 0; i < gSet.Length; i++)
            {
                inv.Slots[i] = ItemStack.Of(gSet[i]);
                if (gSet[i] == weapon) sel = i;
            }
            if (System.Array.IndexOf(gSet, weapon) < 0) { inv.Slots[gSet.Length] = ItemStack.Of(weapon); sel = gSet.Length; }
            inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.MusketBall, 999);
            inv.Selected = sel;
            yield return null;
        }

        /// <summary>
        /// One frame of keeping a distance from an enemy: on wings (BSteer), or on foot: the near side kept, walked to and
        /// fro, a jump (and the bottle's second) only for an enemy high up, a step, or to get over to the other side.
        /// </summary>
        void GSteer(HeroController hero, Vector2 at, float dist, bool roam = true)
        {
            if (gWings) { BSteer(hero, at, dist, roam); return; }
            var col = hero.GetComponent<BoxCollider2D>();
            Vector2 c = col.bounds.center;
            if (Physics2D.Raycast(new Vector2(at.x, c.y), Vector2.right * bSide, dist + 1f, BTerrain).collider != null)
            {
                bSide = -bSide;
                bSwapAt = Time.time + Random.Range(3f, 4.5f);
            }
            float wantX = at.x + bSide * dist;
            float dx = wantX - c.x;
            if (Mathf.Abs(dx) > 1.2f) bMove = dx > 0 ? 1 : -1;
            else if (Mathf.Abs(dx) < 0.4f) bMove = 0;
            bool crossing = Mathf.Abs(c.x - at.x) < 1.6f && Mathf.Sign(c.x - at.x) != bSide && bMove != 0;
            var ahead = Physics2D.Raycast(new Vector2(c.x, col.bounds.min.y + 0.3f), Vector2.right * bMove, 1f, BTerrain);
            bool want = hero.cState.onGround && Time.time > gHopAt && (at.y - c.y > 2.6f || crossing || (bMove != 0 && ahead.collider != null));
            if (want)
            {
                Player.TerraMovement.DevGroundJump(hero);
                Player.TerraMovement.DevJump = true;
                gHopAt = Time.time + 1.6f;
            }
            else if (Time.time > gHopAt - 1.15f) Player.TerraMovement.DevJump = false;
            Player.TerraMovement.DevMove = bMove;
            if (bMove == 0) { if (at.x > c.x && !hero.cState.facingRight) hero.FaceRight(); else if (at.x < c.x && hero.cState.facingRight) hero.FaceLeft(); }
        }

        /// <summary>
        /// A weapon fought with for a take: the nearest enemy taken and kept, the aim eased onto it, the hero keeping `dist` on
        /// one side of it (flying over now and then), the weapon used its own way (GUse).
        /// </summary>
        IEnumerator GWeaponTake(HeroController hero, string clip, ItemType weapon, int loadout, float seconds, float dist, GUse use)
        {
            yield return GTake(weapon, loadout);
            var col = hero.GetComponent<BoxCollider2D>();
            if (use == GUse.Summon)
            {
                Minions.Instance?.Clear();
                foreach (var b in new[] { Player.PlayerBuffs.WanderingSoul, Player.PlayerBuffs.ChoirSoul, Player.PlayerBuffs.ShadeSoul, Player.PlayerBuffs.DirgeSoul })
                    if (Player.PlayerBuffs.Has(b)) Player.PlayerBuffs.Remove(b);
                MinionEngage.DevAlways = true;
            }
            aimInit = false;
            var first = GNearestFoe(col.bounds.center, 24f, null);
            SmoothAim(first != null ? BCentre(first) : (Vector2)col.bounds.center + new Vector2(4f, 0.5f), 99f);
            StartRec(clip);
            float fought = 0f, waited = 0f, nextCast = 0f, splitAt = -1f, phase = 0f;
            int casts = 0, shots = 0;
            bool altHeld = false, blinked = false;
            HealthManager target = null;
            float lastFoe = Time.time;
            while (fought < seconds && waited < 2f)
            {
                BKeep();
                mod.DevAltPress = false;
                Vector2 c = col.bounds.center;
                var foes = BFoes(c, 30f);
                BToughen(foes);
                if (target == null || target.isDead || target.hp <= 0 || !foes.Contains(target) || Vector2.Distance(BCentre(target), c) > 16f)
                    target = GNearestFoe(c, 24f, foes);
                if (target != null)
                {
                    fought += Time.deltaTime;
                    phase += Time.deltaTime;
                    lastFoe = Time.time;
                    Vector2 at = BCentre(target);
                    SmoothAim(at, use == GUse.Ghost ? 5f : 9f);
                    bool ready = mod.DevReady && !mod.IsUsingItem;
                    switch (use)
                    {
                        case GUse.Spirit:
                            // The wave cast; every other one split into its wraiths before it bursts.
                            mod.DevHold = false;
                            if (ready && Time.time >= nextCast) { mod.DevUseSelected(aimNow); casts++; nextCast = Time.time + 0.85f; if (casts % 2 == 0) splitAt = Time.time + 0.2f; }
                            if (splitAt > 0f && Time.time >= splitAt) { mod.DevAltPress = true; splitAt = -1f; }
                            break;
                        case GUse.Pulse:
                            // Beams, then a charge held two seconds (the big wave goes by itself), then beams again.
                            float cyc = phase % 5.2f;
                            if (cyc < 2.3f)
                            {
                                if (altHeld) { mod.DevAltHold = false; altHeld = false; }
                                mod.DevHold = true;
                                if (ready) mod.DevUseSelected(aimNow);
                            }
                            else
                            {
                                mod.DevHold = false;
                                if (!altHeld && ready) { mod.DevAltHold = true; altHeld = true; mod.DevUseSelected(aimNow, alt: true); }
                                if (cyc > 4.6f && altHeld) { mod.DevAltHold = false; }
                            }
                            break;
                        case GUse.Rift:
                            // Five rays to stack the marks, then the right button bursts them.
                            mod.DevHold = true;
                            if (ready)
                            {
                                if (shots < 5) { mod.DevUseSelected(aimNow); shots++; }
                                else { mod.DevUseSelected(aimNow, alt: true); shots = 0; }
                            }
                            break;
                        case GUse.Ghost:
                            // Held: the ghost out over the enemy, the shocks closing on it; let go, it drifts back. The
                            // last time out, the right button: the hero blinks to it.
                            float g = phase % 3.4f;
                            bool hold = g < 2.0f;
                            mod.DevHold = hold;
                            if (hold && ready) mod.DevUseSelected(aimNow);
                            if (!blinked && fought > seconds - 2.2f && g > 1.2f && g < 1.3f) { mod.DevAltPress = true; blinked = true; }
                            break;
                        case GUse.Sniper:
                            // Aimed (the right button held): each shot marks, the next on the marked one calls the bombs.
                            if (!altHeld && phase > 0.6f) { mod.DevAltHold = true; altHeld = true; }
                            mod.DevHold = true;
                            if (ready) mod.DevUseSelected(aimNow);
                            break;
                        case GUse.Summon:
                            mod.DevHold = false;
                            if (ready && casts < 2) { mod.DevUseSelected((Vector2)c + new Vector2(casts == 0 ? 1.5f : -1.5f, 1.6f)); casts++; }
                            break;
                        default:
                            mod.DevHold = true;
                            if (ready) mod.DevUseSelected(aimNow);
                            break;
                    }
                    if (use == GUse.Melee) GSteerMelee(hero, at);
                    else if (use == GUse.Sniper) GSteer(hero, at, dist, false);
                    else GSteer(hero, at, dist);
                }
                else
                {
                    waited += Time.deltaTime;
                    mod.DevHold = false;
                    SmoothAim(c + new Vector2(-bSide * 5f, 0.8f), 4f);
                    if (gWings) BSteer(hero, c + new Vector2(-bSide * dist, -bLift), dist, false);
                    else { Player.TerraMovement.DevMove = 0; Player.TerraMovement.DevJump = false; }
                }
                yield return null;
            }
            mod.DevHold = null;
            mod.DevAltHold = null;
            mod.DevAltPress = false;
            BStop();
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { BKeep(); yield return null; }
            StopRec();
            MinionEngage.DevAlways = false;
            Plugin.Log.LogInfo($"[AutoTest] 录制G 武器 {clip}：{ItemDb.Get(weapon).Name} 打了 {fought:F1}s，等了 {waited:F1}s，至今击杀 {bKills}");
        }

        static HealthManager GNearestFoe(Vector2 c, float range, List<HealthManager> foes)
        {
            HealthManager best = null;
            float bd = float.MaxValue;
            foreach (var hm in foes ?? BFoes(c, range))
            {
                float d = Vector2.Distance(BCentre(hm), c);
                if (d < bd) { bd = d; best = hm; }
            }
            return best;
        }

        /// <summary>Close in for a blade: level with the enemy, a body's length off its near side, over to the other side now and then.</summary>
        void GSteerMelee(HeroController hero, Vector2 at)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            Vector2 c = col.bounds.center;
            if (Time.time > bSwapAt) { bSide = -bSide; bSwapAt = Time.time + Random.Range(2.8f, 4f); }
            if (Physics2D.Raycast(new Vector2(at.x, c.y), Vector2.right * bSide, 2.6f, BTerrain).collider != null) { bSide = -bSide; bSwapAt = Time.time + 3f; }
            float wantX = at.x + bSide * 1.7f;
            bool crossing = Mathf.Abs(c.x - at.x) < 1.4f && Mathf.Sign(c.x - at.x) != bSide;
            var down = Physics2D.Raycast(c, Vector2.down, 40f, BTerrain);
            float ground = down.collider != null ? down.point.y : c.y - 6f;
            float wantY = Mathf.Max(ground + 0.9f, at.y + (crossing ? 3.2f : 0.1f));
            float dx = wantX - c.x;
            if (Mathf.Abs(dx) > 0.8f) bMove = dx > 0 ? 1 : -1;
            else if (Mathf.Abs(dx) < 0.25f) bMove = 0;
            Player.TerraMovement.DevMove = bMove;
            float dy = wantY - c.y, vy = rb.linearVelocity.y;
            if (gWings)
            {
                bool rise = dy > 0.4f || (dy > -0.6f && vy < -2f);
                if (rise && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
                Player.TerraMovement.DevJump = rise;
            }
            else
            {
                // On foot: a jump for an enemy up high or to get over it, not more than one in a second and a half.
                if (hero.cState.onGround && Time.time > gHopAt && (dy > 1.4f || crossing))
                {
                    Player.TerraMovement.DevGroundJump(hero);
                    Player.TerraMovement.DevJump = true;
                    gHopAt = Time.time + 1.5f;
                }
                else if (Time.time > gHopAt - 1.1f) Player.TerraMovement.DevJump = false;
            }
            // Facing the enemy while the blade swings.
            if (bMove == 0) { if (at.x > c.x && !hero.cState.facingRight) hero.FaceRight(); else if (at.x < c.x && hero.cState.facingRight) hero.FaceLeft(); }
        }

        /// <summary>Into a room for a spell's takes: the new AI, enemies toughened (so each weapon has someone to show on).</summary>
        IEnumerator GWeaponRoom(string room, Vector2? start, float tough)
        {
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            EnemyRespawn.Multiplier = 6;
            yield return BEnter(room);
            var hero = HeroController.instance;
            if (!start.HasValue && BGroundRow(room, 10f, out var mid, out float floor, out int count))
            {
                // Beside the room's thickest crowd on a floor, on the side with room.
                float Room(int d) { var h = Physics2D.Raycast(new Vector2(mid.x, floor + 0.8f), Vector2.right * d, 40f, BTerrain); return h.collider != null ? h.distance : 40f; }
                int side = Room(-1) > Room(1) ? -1 : 1;
                start = new Vector2(mid.x + side * Mathf.Min(7f, Room(side) - 1.5f), floor + 0.1f);
                bSide = side;
                Plugin.Log.LogInfo($"[AutoTest] 录制G {room}：{count} 只在 {mid:F1}，地面 {floor:F1}，站在 {start.Value:F1}");
            }
            if (start.HasValue)
            {
                hero.transform.position = Clear(hero, start.Value);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            }
            bToughK = tough;
            yield return new WaitForSeconds(0.8f);
        }

        /// <summary>The Vengeful Spirit's four.</summary>
        IEnumerator GSpellSoulWave(string room, Vector2? start)
        {
            gStage = 1;
            gSet = SoulWave.Weapons;
            yield return GWeaponRoom(room, start, 6f);
            var hero = HeroController.instance;
            yield return GWeaponTake(hero, "v5G_spell_revenant_edge", ItemType.RevenantEdge, 0, 7f, 1.7f, GUse.Melee);
            yield return GWeaponTake(hero, "v5G_spell_pulse_rifle", ItemType.RevenantPulseRifle, 1, 8f, 6.5f, GUse.Pulse);
            yield return GWeaponTake(hero, "v5G_spell_vengeful_spirit", ItemType.VengefulSpiritFocus, 2, 8f, 6.5f, GUse.Spirit);
            yield return GWeaponTake(hero, "v5G_spell_wandering_soul", ItemType.WanderingSoulStaff, 3, 8f, 5f, GUse.Summon);
        }

        /// <summary>The Howling Wraiths' four.</summary>
        IEnumerator GSpellHowl(string room, Vector2? start)
        {
            gStage = 1;
            gSet = Howl.Weapons;
            yield return GWeaponRoom(room, start, 6f);
            var hero = HeroController.instance;
            yield return GWeaponTake(hero, "v5G_spell_deathbell", ItemType.WailingDeathbell, 0, 7f, 5f, GUse.Hold);
            yield return GWeaponTake(hero, "v5G_spell_pipe_organ", ItemType.GhostPipeOrgan, 1, 7f, 6f, GUse.Hold);
            yield return GWeaponTake(hero, "v5G_spell_howling_wraiths", ItemType.HowlingWraithsFocus, 2, 8f, 6.5f, GUse.Hold);
            yield return GWeaponTake(hero, "v5G_spell_elegy_mask", ItemType.ElegyMask, 3, 8f, 5f, GUse.Summon);
        }

        /// <summary>The Shade Soul's four.</summary>
        IEnumerator GSpellShade(string room, Vector2? start)
        {
            gStage = 2;
            gSet = Shade.Weapons;
            yield return GWeaponRoom(room, start, 6f);
            var hero = HeroController.instance;
            yield return GWeaponTake(hero, "v5G_spell_shade_edge", ItemType.ShadeEdge, 0, 7f, 1.7f, GUse.Melee);
            yield return GWeaponTake(hero, "v5G_spell_rift_rifle", ItemType.ShadeRiftRifle, 1, 8f, 6.5f, GUse.Rift);
            yield return GWeaponTake(hero, "v5G_spell_shade_ghost", ItemType.ShadeSoulFocus, 2, 9f, 6f, GUse.Ghost);
            yield return GWeaponTake(hero, "v5G_spell_shade_staff", ItemType.ShadeSoulStaff, 3, 8f, 5f, GUse.Summon);
        }

        /// <summary>The Abyss Shriek's four.</summary>
        IEnumerator GSpellAbyss(string room, Vector2? start)
        {
            gStage = 4;
            gSet = Abyss.Weapons;
            yield return GWeaponRoom(room, start, 6f);
            var hero = HeroController.instance;
            yield return GWeaponTake(hero, "v5G_spell_abyss_yoyo", ItemType.AbyssYoyo, 0, 8f, 4.5f, GUse.Hold);
            yield return GWeaponTake(hero, "v5G_spell_void_sniper", ItemType.VoidSniper, 1, 8f, 8f, GUse.Sniper);
            yield return GWeaponTake(hero, "v5G_spell_abyss_staff", ItemType.AbyssStaff, 2, 8f, 6.5f, GUse.Hold);
            yield return GWeaponTake(hero, "v5G_spell_dirge_mask", ItemType.DirgeMask, 3, 8f, 5f, GUse.Summon);
        }

        // ------------------------------------------------------------------ the suites

        IEnumerator P4_v5G_spellsoul(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return GSpellSoulWave("Crossroads_15", null);
        }

        IEnumerator P4_v5G_spellhowl(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return GSpellHowl("Crossroads_40", null);
        }

        IEnumerator P4_v5G_spellshade(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return GSpellShade("Ruins1_01", null);
        }

        IEnumerator P4_v5G_spellabyss(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.7f;
            yield return GSpellAbyss("Deepnest_East_07", null);
        }

        /// <summary>
        /// The spells themselves (the mod's own: Hollow Knight's casts are gone, each spell is the class weapon of its magic
        /// set, 复仇之魂 the white wave, 嚎叫幽灵 the wailing three; the Shade Soul's and the Abyss Shriek's magic weapons
        /// the shade ghost and the abyss three), each a take of its own in a room of its own, at its stage.
        /// </summary>
        IEnumerator P4_v5G_vanilla(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            gStage = 1;
            gSet = SoulWave.Weapons;
            yield return GWeaponRoom("Crossroads_05", null, 6f);
            yield return GWeaponTake(HeroController.instance, "v5G_vanilla_vengeful", ItemType.VengefulSpiritFocus, 2, 9f, 6.5f, GUse.Spirit);
            gSet = Howl.Weapons;
            yield return GWeaponRoom("Mines_03", null, 6f);
            yield return GWeaponTake(HeroController.instance, "v5G_vanilla_howl", ItemType.HowlingWraithsFocus, 2, 9f, 6.5f, GUse.Hold);
            gStage = 2;
            gSet = Shade.Weapons;
            yield return GWeaponRoom("Crossroads_15", null, 6f);
            yield return GWeaponTake(HeroController.instance, "v5G_vanilla_shade", ItemType.ShadeSoulFocus, 2, 10f, 6f, GUse.Ghost);
            gStage = 4;
            gSet = Abyss.Weapons;
            Player.TerraCamera.Zoom = 1.7f;
            yield return GWeaponRoom("Fungus3_02", null, 6f);
            yield return GWeaponTake(HeroController.instance, "v5G_vanilla_abyss", ItemType.AbyssStaff, 2, 9f, 6.5f, GUse.Hold);
        }

        /// <summary>The Dirge Mask's take alone, in a room of enemies still standing.</summary>
        IEnumerator P4_v5G_dirge(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.7f;
            gStage = 4;
            gSet = Abyss.Weapons;
            yield return GWeaponRoom("Deepnest_East_07", null, 6f);
            yield return GWeaponTake(HeroController.instance, "v5G_spell_dirge_mask", ItemType.DirgeMask, 3, 8f, 5f, GUse.Summon);
        }

        /// <summary>The Shade Soul's takes redone (its four in the City, its magic weapon in a bright room).</summary>
        IEnumerator P4_v5G_shadefix(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return GSpellShade("Ruins1_01", null);
            gStage = 2;
            gSet = Shade.Weapons;
            yield return GWeaponRoom("Crossroads_15", null, 6f);
            yield return GWeaponTake(HeroController.instance, "v5G_vanilla_shade", ItemType.ShadeSoulFocus, 2, 10f, 6f, GUse.Ghost);
        }
    }
}
