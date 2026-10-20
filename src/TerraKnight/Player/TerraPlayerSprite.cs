using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Draws the default Terraria character in place of the Knight, assembled from Terraria's own
    /// body-part sheets the way Terraria 1.4 draws a player without armor: tinted parts on 40x56
    /// frames, composite torso and arms, frame choice from movement and item use.
    /// </summary>
    internal sealed class TerraPlayerSprite : MonoBehaviour
    {
        const int FrameW = 40, FrameH = 56;
        const float Ppu = CellVisuals.PixelsPerUnit;
        const float BaseZ = 0.0039f;

        // Terraria's default character colours (Player field initialisers).
        static readonly Color HairColor = C(215, 90, 55);
        static readonly Color SkinColor = C(255, 125, 90);
        static readonly Color EyeColor = C(105, 90, 75);
        static readonly Color ShirtColor = C(175, 165, 140);
        static readonly Color UnderShirtColor = C(160, 180, 215);
        static readonly Color PantsColor = C(255, 230, 175);
        static readonly Color ShoeColor = C(160, 105, 60);

        enum Sheet { Vertical, Composite, Hair, Eyelid, Shield, Wing, Balloon, FrontBack, FrontFront, IceBarrier }

        /// <summary>Which of the character's colours a layer takes (none: drawn white).</summary>
        enum Part { None, Hair, Skin, Eye, Shirt, UnderShirt, Pants, Shoe }

        /// <summary>A character's colours (Player.hairColor, skinColor, eyeColor, shirtColor, underShirtColor, pantsColor, shoeColor).</summary>
        public struct Palette
        {
            public Color Hair, Skin, Eye, Shirt, UnderShirt, Pants, Shoe;

            public Color Of(int part) => part switch
            {
                1 => Hair, 2 => Skin, 3 => Eye, 4 => Shirt, 5 => UnderShirt, 6 => Pants, 7 => Shoe, _ => Color.white,
            };
        }

        static Palette P(Color hair, Color skin, Color eye, Color shirt, Color under, Color pants, Color shoe) =>
            new Palette { Hair = hair, Skin = skin, Eye = eye, Shirt = shirt, UnderShirt = under, Pants = pants, Shoe = shoe };

        /// <summary>The looks to choose from in multiplayer: Terraria's default character first.</summary>
        public static readonly Palette[] Looks =
        {
            P(HairColor, SkinColor, EyeColor, ShirtColor, UnderShirtColor, PantsColor, ShoeColor),
            P(C(60, 40, 30), C(220, 160, 120), C(60, 90, 160), C(60, 110, 200), C(230, 230, 230), C(50, 50, 70), C(40, 30, 30)),
            P(C(240, 220, 120), C(255, 190, 150), C(60, 140, 80), C(200, 60, 60), C(250, 240, 220), C(90, 70, 50), C(60, 40, 30)),
            P(C(30, 30, 35), C(190, 120, 80), C(120, 70, 40), C(70, 160, 90), C(220, 210, 160), C(80, 90, 110), C(50, 50, 50)),
            P(C(200, 200, 210), C(255, 210, 180), C(150, 60, 200), C(130, 70, 180), C(200, 180, 230), C(60, 40, 90), C(90, 60, 120)),
            P(C(230, 110, 170), C(255, 175, 140), C(200, 80, 120), C(250, 200, 80), C(255, 250, 230), C(150, 90, 60), C(110, 70, 40)),
            P(C(70, 170, 220), C(240, 170, 130), C(40, 120, 180), C(30, 40, 60), C(90, 200, 220), C(40, 50, 70), C(20, 20, 30)),
            P(C(150, 40, 40), C(170, 110, 80), C(180, 140, 40), C(240, 140, 40), C(90, 60, 40), C(110, 80, 60), C(70, 50, 40)),
        };

        /// <summary>Dresses the character in a look (its layers' colours).</summary>
        public void SetPalette(Palette p)
        {
            void Apply(Layer l)
            {
                if (l.Part != Part.None) l.Tint = p.Of((int)l.Part);
            }
            foreach (var l in backLayers) Apply(l);
            foreach (var l in frontArmLayers) Apply(l);
            foreach (var l in frontShoulderLayers) Apply(l);
        }

        sealed class Layer
        {
            public SpriteRenderer Renderer;
            public string Texture;
            public Sheet Kind;
            public Color Tint;
            public Part Part;
            /// <summary>Leg layers: the two upper strips of the seated legs (k = 0, 1); the layer's own renderer draws the third.</summary>
            public SpriteRenderer[] Strips;
            /// <summary>Rows down the sheet its frames start (a lunar body's glow, the composite sheet's lower half: 4).</summary>
            public int RowOffset;
        }

        /// <summary>SetArmorEffectVisuals' copies (LPR:286-460): afterimages, ghostly outlines, a shimmer.</summary>
        [System.Flags]
        public enum ArmorFx : byte { None = 0, Shadow = 1, ShadowLokis = 2, Outlines = 4, OutlinesForbidden = 8, ShadowSubtle = 16 }

        // Front arm cell per body frame; the back arm is the same column two rows down.
        static readonly Vector2Int[] frontArm =
        {
            new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0), new Vector2Int(6, 0),
            new Vector2Int(2, 1), new Vector2Int(3, 1),
            new Vector2Int(4, 1), new Vector2Int(4, 1), new Vector2Int(4, 1), new Vector2Int(4, 1),
            new Vector2Int(3, 1), new Vector2Int(3, 1), new Vector2Int(3, 1),
            new Vector2Int(5, 1), new Vector2Int(6, 1), new Vector2Int(6, 1), new Vector2Int(5, 1),
            new Vector2Int(3, 1), new Vector2Int(3, 1),
        };
        static readonly bool[] shoulderOverFrontArm =
        {
            true, false, false, true, true, false, true, true, true, true, true, true, true, true, true, true, true, true, true, true,
        };

        /// <summary>Equipped accessories and armor that show on the player.</summary>
        public struct Equips
        {
            /// <summary>The worn accessories' sheets (Item.shoeSlot, waistSlot, ...); 0 for none.</summary>
            public int Shoe, Waist, Balloon, Shield, Neck, Back, HandOn, HandOff;
            /// <summary>Armor sheet numbers (Armor_Head_N, Armor/Armor_N, Armor_Legs_N); 0 for none.</summary>
            public int Head, Body, Legs;
            /// <summary>The helmet leaves the hair showing (hats, the mining helmet).</summary>
            public bool HairUnderHead;
            /// <summary>faceSlot (Acc_Face_N) and frontSlot (Acc_Front_N, the cape's front).</summary>
            public int Face, Front;
            /// <summary>The werewolf's or the merfolk's look: no capes.</summary>
            public bool HideCapes;
            /// <summary>The Frozen Turtle Shell's barrier is up (buff 62).</summary>
            public bool IceBarrier;
            /// <summary>brainOfConfusionDodgeAnimationCounter (300 down to 0).</summary>
            public int BrainCounter;
            /// <summary>The armor set's copies; Holy Protection's pair; the Spectre set's ghost look (PDS:1489-1517).</summary>
            public ArmorFx Fx;
            public bool Holy, SocialGhost;
            /// <summary>The stealth (Player.stealth, 1 seen) and whose tint: 1 Shroomite, 2 Vortex; 0 none.</summary>
            public float Stealth;
            public byte StealthKind;
            /// <summary>The lunar glowmasks: head and legs (Glow_N), the body's composite sheet (its +224 rows), with the arms or not.</summary>
            public string HeadGlow, LegsGlow;
            public int BodyGlow;
            public bool BodyGlowArms;
        }

        /// <summary>How the held item is used this frame.</summary>
        public struct Hold
        {
            /// <summary>Held out towards the cursor (bows, guns, staffs, books) rather than swung.</summary>
            public bool Out;
            /// <summary>Angle of the aim in degrees from straight ahead, positive upwards (already mirrored for facing).</summary>
            public float Angle;
            /// <summary>Staff art points diagonally; turn it to point along the aim.</summary>
            public bool Staff;
            /// <summary>
            /// Main.DrawPlayerItemPos: how far in front of the hand the item's back edge sits (X), and
            /// how much lower it is held (Y), in Terraria pixels.
            /// </summary>
            public Vector2 Offset;
            /// <summary>useStyle 3 (the umbrellas): thrust out level (<see cref="Items.MeleeSpecials.ThrustLocation"/>); the item thrust.</summary>
            public bool Thrust;
            public Items.ItemDef Item;
            /// <summary>
            /// A hold style while not in use: where the item's bottom-left corner sits from the
            /// hitbox's top-left (Terraria px, as if facing right), its turn (clockwise radians) and
            /// the body frame it holds it with.
            /// </summary>
            public bool Idle;
            public Vector2 IdleLoc;
            public float IdleRot;
            public int IdleBody;
            /// <summary>Player.shieldRaised: body frame 10 and the shield 4 px up.</summary>
            public bool ShieldUp;
        }

        readonly List<Layer> backLayers = new List<Layer>();
        readonly List<Layer> frontArmLayers = new List<Layer>();
        readonly List<Layer> frontShoulderLayers = new List<Layer>();
        readonly Dictionary<(string, int, int, int), Sprite> sprites = new Dictionary<(string, int, int, int), Sprite>();
        Transform body, frontGroup, itemRoot;
        SpriteRenderer heldItem, heldGlow;

        /// <summary>The held item's glowmask (its sprite pivoting as the item's does), or null.</summary>
        public Sprite HeldGlow;
        Layer torsoSkin, legSkin, pants, shoes, head, eyeWhites, eyes, eyelid, hair;
        Layer handsOff, accShoes, waist, shield, handsOn, back, balloon, neck;
        // The backpack (behind the wings), the face accessories under the hair and over the head, the cape's front halves, the ice barrier.
        Layer backpack, faceUnder, faceOver, frontBack, frontFront, iceBarrier;
        // The lunar sets' glowmasks, added over their pieces (PDS:500-810).
        Layer headGlow, legsGlow, torsoGlow, backShoulderGlow, backArmGlow, frontArmGlow, frontShoulderGlow;
        readonly List<Layer> backArm = new List<Layer>(), backShoulder = new List<Layer>(), torso = new List<Layer>();
        // Armor: drawn in place of the clothes they cover.
        Layer armorLegs, armorBackArm, armorBackShoulder, armorTorso, armorHead, armorFrontArm, armorFrontShoulder;
        int shownHead = -1, shownBody = -1, shownLegs = -1;
        float legCounter;
        int legFrame;
        int blinkTicks;
        float tickAccumulator;

        // Shield of Cthulhu afterimages: copies of the player drawn where it was a few ticks ago; the
        // Brain of Confusion's dodge uses six of them.
        // Hallowed armor alone wants nine at once (four outlines, three afterimages, Holy Protection's two).
        const int ShadowCount = 10;
        readonly Transform[] ghosts = new Transform[ShadowCount];
        readonly Vector3[] shadowPos = new Vector3[ShadowCount];
        int shadowCycle;
        readonly Transform[][] ghostTransforms = new Transform[ShadowCount][];
        readonly SpriteRenderer[][] ghostRenderers = new SpriteRenderer[ShadowCount][];
        Transform[] liveTransforms;
        SpriteRenderer[] liveRenderers;

        // While scenery covers the player: solid-colour copies nudged a pixel each way, and over
        // them a dimmed copy of the player, all drawn over everything: a dark figure with a pale
        // rim, seen through whatever is in front of him.
        const int OutlineOrder = 32000;
        static readonly Color OutlineColor = new Color(1f, 0.93f, 0.65f);
        static readonly Vector2[] outlineOffsets = { Vector2.right, Vector2.left, Vector2.up, Vector2.down, Vector2.zero };
        const int OutlineGhost = 4;
        readonly Transform[] outlines = new Transform[5];
        readonly Transform[][] outlineTransforms = new Transform[5][];
        readonly SpriteRenderer[][] outlineRenderers = new SpriteRenderer[5][];
        float outlineAlpha;

        /// <summary>
        /// The copies are objects of their own beside the player's: hidden with him (a portal's
        /// wait, a scene's hidden entry), so they don't stay drawn where he last stood.
        /// </summary>
        void OnDisable()
        {
            outlineAlpha = 0f;
            foreach (var t in outlines) if (t != null) t.gameObject.SetActive(false);
            foreach (var t in ghosts) if (t != null) t.gameObject.SetActive(false);
        }

        /// <summary>The player's opacity this frame: the outline blinks with him while he is immune.</summary>
        float shownOpacity = 1f;
        bool mountMasked;

        /// <summary>Target strength of the outline, 0 (hidden) to 1: set while scenery hides the player.</summary>
        public float OutlineStrength { get; set; }

        /// <summary>Gravity turned over (the Gravity Globe): drawn hanging from the feet, upside down.</summary>
        public bool Upside { get; set; }

        /// <summary>The head bob of the body frame drawn last, Terraria px y down (-2 on the bob frames).</summary>
        public static float BobPx;
        /// <summary>The body frame drawn this frame (for the layers the effect batch draws).</summary>
        public static int BodyFrameNow;

        /// <summary>PlayerDrawSet's raptor keeps helmets/face accessories, not a second humanoid body.</summary>
        internal void MountAppearance(MountDef mount, float facing, Vector2 velocity, bool grounded, Equips equips)
        {
            if (mount == null || !mount.HeadAccessoriesOnly) return;
            var headOffset = Mounts.RaptorHeadOffset(Mounts.VisualFrame(mount, velocity, grounded));
            transform.position += new Vector3(headOffset.x * facing, headOffset.y, 0f) / Ppu;
            bool helmet = Mounts.RaptorHelmet(equips.Head), face = Mounts.RaptorFace(equips.Face);
            mountMasked = true;
            foreach (var sr in liveRenderers)
                sr.enabled = (helmet && (sr == armorHead.Renderer || sr == headGlow.Renderer))
                    || (face && (sr == faceUnder.Renderer || sr == faceOver.Renderer))
                    || sr == heldItem || sr == heldGlow;
            var addon = Mounts.RaptorHelmetOffset(equips.Head);
            var localOffset = new Vector3(addon.x, -addon.y, 0f) / Ppu;
            armorHead.Renderer.transform.localPosition += localOffset;
            headGlow.Renderer.transform.localPosition += localOffset;
            // Copies live beside the main object, so the body mask must also suppress them.
            foreach (var t in outlines) if (t != null) t.gameObject.SetActive(false);
            foreach (var t in ghosts) if (t != null) t.gameObject.SetActive(false);
        }

        public static TerraPlayerSprite Create(Transform parent)
        {
            var go = new GameObject("Terra Player");
            go.transform.SetParent(parent, false);
            var sprite = go.AddComponent<TerraPlayerSprite>();
            sprite.Build();
            return sprite;
        }

        void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            // Draw order of a player with no armor, back to front (LegacyPlayerRenderer's layer list),
            // with the accessory layers where Terraria draws them: the cape, balloons behind it all (the wings
            // are drawn behind the player by TerraWings, through the effect batch).
            backpack = Add(backLayers, "Acc_Back_9", Sheet.Vertical, Color.white);
            back = Add(backLayers, "Acc_Back_2", Sheet.Vertical, Color.white);
            balloon = Add(backLayers, "Acc_Balloon_8", Sheet.Balloon, Color.white);
            torsoSkin = Add(backLayers, "Player_0_3", Sheet.Composite, SkinColor);
            legSkin = Add(backLayers, "Player_0_10", Sheet.Vertical, SkinColor);
            backArm.Add(Add(backLayers, "Player_0_7", Sheet.Composite, SkinColor));
            backArm.Add(Add(backLayers, "Player_0_5", Sheet.Composite, SkinColor));
            backArm.Add(Add(backLayers, "Player_0_8", Sheet.Composite, UnderShirtColor));
            backArm.Add(Add(backLayers, "Player_0_13", Sheet.Composite, ShirtColor));
            armorBackArm = Add(backLayers, "Armor/Armor_1", Sheet.Composite, Color.white);
            backArmGlow = Glow(Add(backLayers, "Armor/Armor_175", Sheet.Composite, GlowTint), 4);
            handsOff = Add(backLayers, "Accessories/Acc_HandsOff_6", Sheet.Composite, Color.white);   // Climbing Claws, back hand
            pants = Add(backLayers, "Player_0_11", Sheet.Vertical, PantsColor);
            shoes = Add(backLayers, "Player_0_12", Sheet.Vertical, ShoeColor);
            armorLegs = Add(backLayers, "Armor_Legs_1", Sheet.Vertical, Color.white);
            legsGlow = Glow(Add(backLayers, "Glow_199", Sheet.Vertical, GlowTint), 0);
            accShoes = Add(backLayers, "Acc_Shoes_6", Sheet.Vertical, Color.white);                   // Hermes Boots
            backShoulder.Add(Add(backLayers, "Player_0_4", Sheet.Composite, UnderShirtColor));
            backShoulder.Add(Add(backLayers, "Player_0_6", Sheet.Composite, ShirtColor));
            armorBackShoulder = Add(backLayers, "Armor/Armor_1", Sheet.Composite, Color.white);
            backShoulderGlow = Glow(Add(backLayers, "Armor/Armor_175", Sheet.Composite, GlowTint), 4);
            torso.Add(Add(backLayers, "Player_0_4", Sheet.Composite, UnderShirtColor));
            torso.Add(Add(backLayers, "Player_0_6", Sheet.Composite, ShirtColor));
            armorTorso = Add(backLayers, "Armor/Armor_1", Sheet.Composite, Color.white);
            torsoGlow = Glow(Add(backLayers, "Armor/Armor_175", Sheet.Composite, GlowTint), 4);
            waist = Add(backLayers, "Acc_Waist_1", Sheet.Vertical, Color.white);                      // Cloud in a Bottle
            neck = Add(backLayers, "Acc_Neck_2", Sheet.Vertical, Color.white);
            head = Add(backLayers, "Player_0_0", Sheet.Vertical, SkinColor);
            eyeWhites = Add(backLayers, "Player_0_1", Sheet.Vertical, Color.white);
            eyes = Add(backLayers, "Player_0_2", Sheet.Vertical, EyeColor);
            eyelid = Add(backLayers, "Player_0_15", Sheet.Eyelid, SkinColor);
            faceUnder = Add(backLayers, "Acc_Face_5", Sheet.Vertical, Color.white);
            hair = Add(backLayers, "Player_Hair_1", Sheet.Hair, HairColor);
            armorHead = Add(backLayers, "Armor_Head_1", Sheet.Vertical, Color.white);
            headGlow = Glow(Add(backLayers, "Glow_15", Sheet.Vertical, GlowTint), 0);
            faceOver = Add(backLayers, "Acc_Face_9", Sheet.Vertical, Color.white);
            frontBack = Add(backLayers, "Acc_Front_5", Sheet.FrontBack, Color.white);
            shield = Add(backLayers, "Acc_Shield_5", Sheet.Shield, Color.white);                      // Shield of Cthulhu

            // The held item sits between the body and the front arm.
            itemRoot = new GameObject("Held Item").transform;
            itemRoot.SetParent(body, false);
            heldItem = new GameObject("Item").AddComponent<SpriteRenderer>();
            heldItem.transform.SetParent(itemRoot, false);
            heldItem.gameObject.SetActive(false);
            // Its glowmask over it, added.
            heldGlow = new GameObject("Item Glow").AddComponent<SpriteRenderer>();
            heldGlow.transform.SetParent(heldItem.transform, false);
            heldGlow.transform.localPosition = new Vector3(0f, 0f, -0.00001f);
            var add = Effects.TerraParticles.AdditiveMaterial();
            if (add != null) heldGlow.sharedMaterial = add;
            heldGlow.gameObject.SetActive(false);

            frontGroup = new GameObject("Front").transform;
            frontGroup.SetParent(body, false);
            foreach (var (tex, tint) in new[] { ("Player_0_7", SkinColor), ("Player_0_8", UnderShirtColor), ("Player_0_13", ShirtColor), ("Player_0_6", ShirtColor) })
            {
                frontArmLayers.Add(MakeLayer(frontGroup, tex, Sheet.Composite, tint));
                frontShoulderLayers.Add(MakeLayer(frontGroup, tex, Sheet.Composite, tint));
            }
            armorFrontArm = MakeLayer(frontGroup, "Armor/Armor_1", Sheet.Composite, Color.white);
            armorFrontShoulder = MakeLayer(frontGroup, "Armor/Armor_1", Sheet.Composite, Color.white);
            frontArmGlow = Glow(MakeLayer(frontGroup, "Armor/Armor_176", Sheet.Composite, GlowTint), 4);
            frontShoulderGlow = Glow(MakeLayer(frontGroup, "Armor/Armor_176", Sheet.Composite, GlowTint), 4);
            handsOn = MakeLayer(frontGroup, "Accessories/Acc_HandsOn_11", Sheet.Composite, Color.white); // Climbing Claws, front hand
            frontFront = MakeLayer(frontGroup, "Acc_Front_5", Sheet.FrontFront, Color.white);
            iceBarrier = MakeLayer(frontGroup, "IceBarrier", Sheet.IceBarrier, Color.white);

            for (int i = 0; i < backLayers.Count; i++) SetZ(backLayers[i].Renderer, i);
            // Made before the shadow and outline copies, so theirs line up with these.
            foreach (var leg in new[] { legSkin, pants, shoes, armorLegs, legsGlow, accShoes }) AddStrips(leg);

            liveTransforms = GetComponentsInChildren<Transform>(true);
            liveRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int g = 0; g < ShadowCount; g++)
            {
                var copy = Instantiate(gameObject, transform.parent);
                copy.name = "Terra Player Shadow " + g;
                Destroy(copy.GetComponent<TerraPlayerSprite>());
                copy.SetActive(false);
                ghosts[g] = copy.transform;
                ghostTransforms[g] = copy.GetComponentsInChildren<Transform>(true);
                ghostRenderers[g] = copy.GetComponentsInChildren<SpriteRenderer>(true);
            }
            for (int o = 0; o < outlines.Length; o++)
            {
                var copy = Instantiate(gameObject, transform.parent);
                copy.name = "Terra Player Outline " + o;
                Destroy(copy.GetComponent<TerraPlayerSprite>());
                copy.SetActive(false);
                outlines[o] = copy.transform;
                outlineTransforms[o] = copy.GetComponentsInChildren<Transform>(true);
                outlineRenderers[o] = copy.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in outlineRenderers[o]) sr.sortingOrder = o == OutlineGhost ? OutlineOrder + 1 : OutlineOrder;
            }
        }

        /// <summary>
        /// PlayerDrawLayers.DrawSittingLegs: seated, a leg layer draws only rows 42-55 of its first
        /// frame, as three strips each moved forward and drawn up over the one below: rows 46-55
        /// six pixels forward and four up (the shins and feet, drawn first), rows 44-45 four
        /// forward and two up, rows 42-43 two forward (the hips, on top). Bent knees, feet out.
        /// </summary>
        static readonly int[] StripTop = { 42, 44, 46 }, StripHeight = { 2, 2, 10 };

        void AddStrips(Layer leg)
        {
            leg.Strips = new SpriteRenderer[2];
            for (int k = 0; k < 2; k++)
            {
                var sr = new GameObject(leg.Texture + " seated " + k).AddComponent<SpriteRenderer>();
                // Under the layer's own renderer: hidden with it (pants under leg armor), mirrored with the body.
                sr.transform.SetParent(leg.Renderer.transform, false);
                sr.gameObject.SetActive(false);
                leg.Strips[k] = sr;
            }
        }

        /// <summary>The glowmasks' colour (250,250,250,0): (byte)(62.5·4), added (PDS:514-526).</summary>
        static readonly Color GlowTint = new Color(250f / 255f, 250f / 255f, 250f / 255f, 1f);

        /// <summary>A glowmask layer: drawn added, hidden until a lunar piece wants it.</summary>
        static Layer Glow(Layer layer, int rowOffset)
        {
            var add = Effects.TerraParticles.AdditiveMaterial();
            if (add != null) layer.Renderer.sharedMaterial = add;
            layer.RowOffset = rowOffset;
            layer.Renderer.gameObject.SetActive(false);
            return layer;
        }

        Layer Add(List<Layer> list, string tex, Sheet kind, Color tint)
        {
            var layer = MakeLayer(body, tex, kind, tint);
            list.Add(layer);
            return layer;
        }

        Layer MakeLayer(Transform parent, string tex, Sheet kind, Color tint)
        {
            var sr = new GameObject(tex).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.color = tint;
            // The default character's colours are all different: the colour a layer is made with says which it takes.
            var part = tint == HairColor ? Part.Hair : tint == SkinColor ? Part.Skin : tint == EyeColor ? Part.Eye : tint == ShirtColor ? Part.Shirt
                     : tint == UnderShirtColor ? Part.UnderShirt : tint == PantsColor ? Part.Pants : tint == ShoeColor ? Part.Shoe : Part.None;
            return new Layer { Renderer = sr, Texture = tex, Kind = kind, Tint = tint, Part = part };
        }

        // Later layers sit nearer the camera; the game sorts transparent sprites by depth.
        static void SetZ(SpriteRenderer sr, int order) =>
            sr.transform.localPosition = new Vector3(0f, 0f, -order * 0.00002f);

        /// <summary>
        /// Updates the frames. feet is the bottom-centre of the hero's collider, velocity in
        /// world units per second, swingProgress 0..1 while an item is in use (negative otherwise).
        /// </summary>
        /// <param name="bodyOverride">Body frame forced by the move in progress (3 sliding down a wall,
        /// 6 for the Shield of Cthulhu dash in the air), or -1.</param>
        /// <param name="shadows">How many dash afterimages to draw (0-3).</param>
        /// <param name="seated">Resting on a bench: Terraria's seated pose (standing frames, legs drawn bent).</param>
        public void Pose(Vector2 feet, Vector2 velocity, bool grounded, float direction, float swingProgress, Sprite item, float itemScale, float opacity,
            int bodyOverride, Equips equips, int shadows, Hold hold = default, int legOverride = -1, bool seated = false)
        {
            // Restore any partial transformation mask before posing the normal character again.
            if (mountMasked)
            {
                foreach (var sr in liveRenderers) sr.enabled = true;
                mountMasked = false;
            }
            UpdateArmor(equips);
            // Terraria's frame sits 4 px below the hitbox bottom: centre 24 px above the feet.
            // Seated on a chair the whole player is drawn 4 px higher (PlayerSittingHelper's
            // seatYOffset -4 + offsetForSeat.Y 0), which puts the thighs 2 px under the seat.
            transform.position = new Vector3(feet.x, feet.y + (Upside ? -1f : 1f) * (24f + (seated ? SeatLift : 0f)) / Ppu, BaseZ);
            body.localScale = new Vector3(direction, Upside ? -1f : 1f, 1f);

            // Animation advances on Terraria's 60 Hz tick.
            tickAccumulator += Time.deltaTime * 60f;
            int ticks = Mathf.FloorToInt(tickAccumulator);
            tickAccumulator -= ticks;
            blinkTicks += ticks;
            for (int t = 0; t < ticks; t++) RecordShadow();

            if (!grounded)
            {
                legCounter = 0f;
                legFrame = 5;
            }
            else if (Mathf.Abs(velocity.x) > 0.05f)
            {
                // legFrameCounter += |velocity.X| * 1.3 per tick, in pixels per tick.
                float pxPerTick = Mathf.Abs(velocity.x) * Ppu / 60f;
                if (legFrame < 7) legFrame = 19;
                legCounter += pxPerTick * 1.3f * ticks;
                while (legCounter > 8f)
                {
                    legCounter -= 8f;
                    legFrame = legFrame >= 19 ? 7 : legFrame + 1;
                }
            }
            else
            {
                legCounter = 0f;
                legFrame = 0;
            }

            if (legOverride >= 0) legFrame = legOverride;
            // Seated: no frame of its own, the standing one (Player.PlayerFrame with no velocity).
            if (seated)
            {
                legCounter = 0f;
                legFrame = 0;
            }
            int bodyFrame = legFrame;
            if (swingProgress >= 0f && hold.Out)
                // useStyle 5 (Player.PlayerFrame): the arm points up past 0.75 rad, down past 0.6 rad, else ahead;
                // the Blowpipe and Blowgun always up, at the mouth.
                bodyFrame = hold.Item != null && (hold.Item.Type == Items.ItemType.Blowpipe || hold.Item.Type == Items.ItemType.Blowgun) ? 2
                          : hold.Angle > 0.75f * Mathf.Rad2Deg ? 2 : hold.Angle < -0.6f * Mathf.Rad2Deg ? 4 : 3;
            else if (swingProgress >= 0f && hold.Thrust)
                bodyFrame = 3;
            else if (swingProgress >= 0f)
                bodyFrame = swingProgress < 0.334f ? 1 : swingProgress < 0.667f ? 2 : 3;
            // PlayerFrame (Pl:36736-36765): a hold style's frame, then the raised shield's, over the jump's and the wings'.
            else if (hold.Idle)
                bodyFrame = hold.IdleBody;
            else if (hold.ShieldUp)
                bodyFrame = 10;
            else if (bodyOverride >= 0 && !seated)
                bodyFrame = bodyOverride;

            // Head bob: on these walk frames the composite parts and eyelid rise by 2 px.
            bool bob = bodyFrame == 7 || bodyFrame == 8 || bodyFrame == 9 || bodyFrame == 14 || bodyFrame == 15 || bodyFrame == 16;
            float bobY = bob ? 2f / Ppu : 0f;
            BobPx = bob ? -2f : 0f;
            BodyFrameNow = bodyFrame;

            var torsoCell = bodyFrame == 5 ? new Vector2Int(1, 0) : new Vector2Int(0, 0);
            var arm = frontArm[bodyFrame];
            var backArmCell = new Vector2Int(arm.x, arm.y + 2);

            Set(torsoSkin, torsoCell, bobY);
            Leg(legSkin, legFrame, seated);
            foreach (var l in backArm) Set(l, backArmCell, bobY);
            Leg(pants, legFrame, seated);
            Leg(shoes, legFrame, seated);
            foreach (var l in backShoulder) Set(l, new Vector2Int(1, 1), bobY);
            foreach (var l in torso) Set(l, torsoCell, bobY);
            Set(head, bodyFrame);
            Set(eyeWhites, bodyFrame);
            Set(eyes, bodyFrame);
            Set(eyelid, EyelidFrame(), bobY);
            Set(hair, Mathf.Max(0, bodyFrame - 6));
            if (shownHead > 0) Set(armorHead, bodyFrame);
            if (faceUnder.Renderer.gameObject.activeSelf) Set(faceUnder, bodyFrame);
            if (faceOver.Renderer.gameObject.activeSelf) Set(faceOver, bodyFrame);
            if (shownLegs > 0) Leg(armorLegs, legFrame, seated);
            if (shownBody > 0)
            {
                Set(armorTorso, torsoCell, bobY);
                Set(armorBackShoulder, new Vector2Int(1, 1), bobY);
                Set(armorBackArm, backArmCell, bobY);
                Set(armorFrontArm, arm, bobY);
                Set(armorFrontShoulder, new Vector2Int(0, 1), bobY);
            }
            // The lunar glowmasks over their pieces: head and legs from Glow_N, the body from its sheet's glow rows.
            bool hg = equips.HeadGlow != null && shownHead > 0 && armorHead.Renderer.gameObject.activeSelf;
            headGlow.Renderer.gameObject.SetActive(hg);
            if (hg)
            {
                headGlow.Texture = equips.HeadGlow;
                Set(headGlow, bodyFrame);
            }
            bool lg = equips.LegsGlow != null && shownLegs > 0;
            legsGlow.Renderer.gameObject.SetActive(lg);
            if (lg)
            {
                legsGlow.Texture = equips.LegsGlow;
                Leg(legsGlow, legFrame, seated);
            }
            bool bg = equips.BodyGlow > 0 && shownBody > 0, ag = bg && equips.BodyGlowArms;
            string bodyGlowTex = "Armor/Armor_" + equips.BodyGlow;
            foreach (var (l, lit) in new[] { (torsoGlow, bg), (backShoulderGlow, ag), (backArmGlow, ag), (frontArmGlow, ag), (frontShoulderGlow, ag) })
            {
                l.Renderer.gameObject.SetActive(lit);
                if (lit) l.Texture = bodyGlowTex;
            }
            if (bg) Set(torsoGlow, torsoCell, bobY);
            if (ag)
            {
                Set(backShoulderGlow, new Vector2Int(1, 1), bobY);
                Set(backArmGlow, backArmCell, bobY);
                Set(frontArmGlow, arm, bobY);
                Set(frontShoulderGlow, new Vector2Int(0, 1), bobY);
            }

            // Accessories, each from its own sheet: shoes and the waist follow the legs, the neck, cape
            // and shield the body, the gloves the composite hands; wings and balloons hang behind.
            accShoes.Renderer.gameObject.SetActive(equips.Shoe > 0);
            if (equips.Shoe > 0)
            {
                accShoes.Texture = "Acc_Shoes_" + equips.Shoe;
                Leg(accShoes, legFrame, seated);
            }
            Show(waist, UseSheet(waist, "Acc_Waist_", equips.Waist), legFrame);
            Show(neck, UseSheet(neck, "Acc_Neck_", equips.Neck), bodyFrame);
            // Back.Sets.DrawInBackpackLayer (quivers, the Hive Pack): behind the wings, not over the body.
            bool pack = System.Array.IndexOf(backpackSheets, equips.Back) >= 0;
            Show(back, !pack && !equips.HideCapes && UseSheet(back, "Acc_Back_", equips.Back), bodyFrame);
            Show(backpack, pack && UseSheet(backpack, "Acc_Back_", equips.Back), bodyFrame);
            // The cape's front (DrawPlayer_32_FrontAcc): its right half behind the arm and shield, its left half in front of the arm.
            bool front = !equips.HideCapes && equips.Front > 0;
            Show(frontBack, front && UseSheet(frontBack, "Acc_Front_", equips.Front), bodyFrame);
            frontFront.Renderer.gameObject.SetActive(front && UseSheet(frontFront, "Acc_Front_", equips.Front));
            if (front) Set(frontFront, bodyFrame);
            // DrawPlayer_25_Shield: raised, 4 px up.
            bool shieldOn = UseSheet(shield, "Acc_Shield_", equips.Shield);
            shield.Renderer.gameObject.SetActive(shieldOn);
            if (shieldOn) Set(shield, bodyFrame, hold.ShieldUp ? 4f / Ppu : 0f);
            // DrawPlayer_35_IceBarrier: Images/IceBarrier's 12 frames of 50×54, a new one every 3 ticks, white, last of all.
            if (equips.IceBarrier) iceTicks += ticks;
            iceBarrier.Renderer.gameObject.SetActive(equips.IceBarrier);
            if (equips.IceBarrier) Show(iceBarrier, 0, (iceTicks / 3) % 12, 0f);
            bool off = UseSheet(handsOff, "Accessories/Acc_HandsOff_", equips.HandOff), on = UseSheet(handsOn, "Accessories/Acc_HandsOn_", equips.HandOn);
            handsOff.Renderer.gameObject.SetActive(off);
            if (off) Set(handsOff, backArmCell, bobY);
            handsOn.Renderer.gameObject.SetActive(on);
            if (on) Set(handsOn, arm, bobY);
            // DrawPlayer_11_Balloons: 52x56 frames on the wall clock (200 ms each), held in the back hand (OffsetsPlayerOffhand).
            Show(balloon, UseSheet(balloon, "Acc_Balloon_", equips.Balloon), System.DateTime.Now.Millisecond % 800 / 200);
            if (equips.Balloon > 0)
            {
                var hand = offhand[bodyFrame];
                Place(balloon, hand.x - 14f, 24f - hand.y);
            }

            // Front shoulder and arm: whichever goes on top depends on the frame.
            bool shoulderOver = shoulderOverFrontArm[bodyFrame];
            int baseOrder = backLayers.Count + 1;
            int group = frontArmLayers.Count + 1; // the clothes, then the armor over them
            for (int i = 0; i < frontArmLayers.Count; i++)
            {
                Set(frontArmLayers[i], arm, bobY);
                Set(frontShoulderLayers[i], new Vector2Int(0, 1), bobY);
                SetZ(frontArmLayers[i].Renderer, baseOrder + (shoulderOver ? 0 : group) + i);
                SetZ(frontShoulderLayers[i].Renderer, baseOrder + (shoulderOver ? group : 0) + i);
            }
            SetZ(armorFrontArm.Renderer, baseOrder + (shoulderOver ? 0 : group) + frontArmLayers.Count);
            SetZ(armorFrontShoulder.Renderer, baseOrder + (shoulderOver ? group : 0) + frontArmLayers.Count);
            SetZ(frontArmGlow.Renderer, baseOrder + (shoulderOver ? 0 : group) + frontArmLayers.Count);
            SetZ(frontShoulderGlow.Renderer, baseOrder + (shoulderOver ? group : 0) + frontArmLayers.Count);
            frontArmGlow.Renderer.transform.localPosition += new Vector3(0f, 0f, -0.00001f);
            frontShoulderGlow.Renderer.transform.localPosition += new Vector3(0f, 0f, -0.00001f);
            // The claws sit on the front hand, over the arm; the cape's front half over them; the ice barrier over everything.
            SetZ(handsOn.Renderer, baseOrder + group * 2);
            SetZ(frontFront.Renderer, baseOrder + group * 2 + 1);
            SetZ(iceBarrier.Renderer, baseOrder + group * 2 + 2);
            PoseItem(swingProgress, item, itemScale, hold);

            float alpha = opacity;
            foreach (var l in backLayers) Tint(l, alpha);
            foreach (var l in frontArmLayers) Tint(l, alpha);
            foreach (var l in frontShoulderLayers) Tint(l, alpha);
            Tint(handsOn, alpha);
            Tint(frontFront, alpha);
            Tint(iceBarrier, 1f);
            Tint(armorFrontArm, alpha);
            Tint(armorFrontShoulder, alpha);
            Tint(frontArmGlow, alpha);
            Tint(frontShoulderGlow, alpha);
            if (heldItem.gameObject.activeSelf) heldItem.color = new Color(1f, 1f, 1f, alpha);
            if (heldGlow.gameObject.activeInHierarchy) heldGlow.color = new Color(250f / 255f, 250f / 255f, 250f / 255f, 1f) * alpha;
            if (equips.StealthKind != 0 || equips.SocialGhost) StealthTint(equips);
            if (DashGlow > 0.01f || DashShade > 0.01f) DashTint();

            if (equips.BrainCounter > 270) PoseBrainCopies(equips.BrainCounter);
            else PoseGhosts(shadows, equips);
            shownOpacity = alpha;
            PoseOutline();
        }

        int iceTicks;

        /// <summary>
        /// PDS:1562-1688 and 1489-1517: the stealth's tint, s = max(0.03, stealth): Shroomite armor
        /// (R·s, G·s, B·(1+10s)/11, A·s), Vortex armor toward (0, .12, .16, 0) as s falls; skin, hair,
        /// clothes and glowmasks ×s². The Spectre set's ghost: skin and clothes gone, armor no more
        /// opaque than gFade (100-150).
        /// </summary>
        void StealthTint(Equips e)
        {
            var armor = new HashSet<Layer> { armorHead, armorTorso, armorBackShoulder, armorBackArm, armorFrontArm, armorFrontShoulder, armorLegs };
            var glows = new HashSet<Layer> { headGlow, legsGlow, torsoGlow, backShoulderGlow, backArmGlow, frontArmGlow, frontShoulderGlow };
            var body = new HashSet<Layer> { torsoSkin, legSkin, pants, shoes, head, eyeWhites, eyes, eyelid, hair };
            foreach (var l in torso) body.Add(l);
            foreach (var l in backArm) body.Add(l);
            foreach (var l in backShoulder) body.Add(l);
            foreach (var l in frontArmLayers) body.Add(l);
            foreach (var l in frontShoulderLayers) body.Add(l);
            float s = Mathf.Max(0.03f, e.Stealth), s2 = s * s;
            float gFade = (100f + 50f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3f))) / 255f;
            void Apply(Layer l, Color c)
            {
                l.Renderer.color = c;
                if (l.Strips != null) foreach (var sr in l.Strips) sr.color = c;
            }
            var all = new List<Layer>(backLayers);
            all.AddRange(frontArmLayers);
            all.AddRange(frontShoulderLayers);
            all.Add(armorFrontArm);
            all.Add(armorFrontShoulder);
            all.Add(frontArmGlow);
            all.Add(frontShoulderGlow);
            foreach (var l in all)
            {
                var c = l.Renderer.color;
                if (e.SocialGhost)
                {
                    if (body.Contains(l)) c.a = 0f;
                    else if (armor.Contains(l)) c.a = Mathf.Min(c.a, gFade);
                }
                if (e.StealthKind == 1)
                {
                    if (armor.Contains(l)) c = new Color(c.r * s, c.g * s, c.b * (1f + 10f * s) / 11f, c.a * s);
                    else if (body.Contains(l) || glows.Contains(l)) c *= s2;
                }
                else if (e.StealthKind == 2)
                {
                    if (armor.Contains(l)) c *= Color.Lerp(Color.white, new Color(0f, 0.12f, 0.16f, 0f), 1f - s);
                    else if (body.Contains(l) || glows.Contains(l)) c *= s2;
                }
                Apply(l, c);
            }
        }

        /// <summary>The Crystal Heart's white light on the player (0 none, 1 full) and the Shade Cloak's darkness (Items.AbilityAccessories).</summary>
        public static float DashGlow, DashShade;

        static readonly Color ShadeColor = new Color(0.05f, 0.03f, 0.09f);

        /// <summary>The player lit white (a Super Dash) or wrapped in shadow (a shadow dash): every layer's colour toward it, its alpha kept.</summary>
        void DashTint()
        {
            var all = new List<Layer>(backLayers);
            all.AddRange(frontArmLayers);
            all.AddRange(frontShoulderLayers);
            all.Add(handsOn);
            all.Add(frontFront);
            all.Add(armorFrontArm);
            all.Add(armorFrontShoulder);
            all.Add(frontArmGlow);
            all.Add(frontShoulderGlow);
            float glow = Mathf.Clamp01(DashGlow) * 0.85f, shade = Mathf.Clamp01(DashShade) * 0.88f;
            foreach (var l in all)
            {
                if (l == null || l.Renderer == null) continue;
                var c = l.Renderer.color;
                var rgb = Color.Lerp(new Color(c.r, c.g, c.b), Color.white, glow);
                rgb = Color.Lerp(rgb, ShadeColor, shade);
                c = new Color(rgb.r, rgb.g, rgb.b, c.a);
                l.Renderer.color = c;
                if (l.Strips != null) foreach (var sr in l.Strips) sr.color = c;
            }
            if (heldItem.gameObject.activeSelf)
            {
                var h = heldItem.color;
                var rgb = Color.Lerp(Color.Lerp(new Color(h.r, h.g, h.b), Color.white, glow), ShadeColor, shade);
                heldItem.color = new Color(rgb.r, rgb.g, rgb.b, h.a);
            }
        }

        /// <summary>Back.Sets.DrawInBackpackLayer: the back sheets drawn as a backpack, behind the wings.</summary>
        static readonly int[] backpackSheets = { 7, 8, 9, 10, 15, 16, 32, 33 };

        /// <summary>
        /// The Brain of Confusion's dodge (LegacyPlayerRenderer:461-472): for its first 30 ticks six
        /// copies of the player fly out round him to 120 px, turning half a circle, fading as they go.
        /// </summary>
        void PoseBrainCopies(int counter)
        {
            float l = Mathf.Clamp01((300 - counter) / 30f);
            for (int g = 0; g < ShadowCount; g++)
            {
                var ghost = ghosts[g];
                bool show = gameObject.activeInHierarchy;
                if (ghost.gameObject.activeSelf != show) ghost.gameObject.SetActive(show);
                if (!show) continue;
                var transforms = ghostTransforms[g];
                var renderers = ghostRenderers[g];
                for (int i = 1; i < transforms.Length && i < liveTransforms.Length; i++)
                {
                    var src = liveTransforms[i];
                    var dst = transforms[i];
                    if (dst.gameObject.activeSelf != src.gameObject.activeSelf) dst.gameObject.SetActive(src.gameObject.activeSelf);
                    dst.localPosition = src.localPosition;
                    dst.localRotation = src.localRotation;
                    dst.localScale = src.localScale;
                }
                for (int i = 0; i < renderers.Length && i < liveRenderers.Length; i++)
                {
                    renderers[i].sprite = liveRenderers[i].sprite;
                    var c = liveRenderers[i].color;
                    c.a *= 1f - l;
                    renderers[i].color = c;
                }
                float a = Mathf.PI * l + g * Mathf.PI / 3f, r = Mathf.Lerp(2f, 120f, l);
                var off = new Vector2(-Mathf.Sin(a) * r, -Mathf.Cos(a) * r) / Ppu;
                ghost.position = new Vector3(transform.position.x + off.x, transform.position.y + off.y, BaseZ + 0.0003f * (g + 1));
            }
        }

        /// <summary>
        /// Puts the worn armor's sheets on and takes off what they cover: body armor replaces the
        /// shirt and undershirt (the skin of the arms and hands still shows), leg armor the pants
        /// and shoes, a helmet the hair (hats keep it).
        /// </summary>
        void UpdateArmor(Equips e)
        {
            if (e.Head != shownHead)
            {
                shownHead = e.Head;
                if (e.Head > 0) armorHead.Texture = "Armor_Head_" + e.Head;
                armorHead.Renderer.gameObject.SetActive(e.Head > 0);
            }
            // The face accessories (Pl:36252-36270, PDL:2585-2867): the blindfold under the hair and the skulls inside the
            // head; the rest over the head, hidden by a helmet but for the flowers; the diving gears hide the helmet and
            // the hair, the Bone Helm the hair.
            bool under = e.Face == 5 || (e.Face >= 10 && e.Face <= 13);
            bool flower = e.Face == 1 || e.Face == 6 || e.Face == 8 || e.Face == 9;
            bool overHelmet = e.Face >= 2 && e.Face <= 4;
            bool noHair = overHelmet || e.Face == 19;
            armorHead.Renderer.gameObject.SetActive(shownHead > 0 && !overHelmet);
            hair.Renderer.gameObject.SetActive((e.Head <= 0 || e.HairUnderHead || overHelmet) && !noHair);
            faceUnder.Renderer.gameObject.SetActive(under && UseSheet(faceUnder, "Acc_Face_", e.Face));
            faceOver.Renderer.gameObject.SetActive(e.Face > 0 && !under && (flower || overHelmet || e.Head <= 0) && UseSheet(faceOver, "Acc_Face_", e.Face));
            if (e.Body != shownBody)
            {
                shownBody = e.Body;
                bool on = e.Body > 0;
                foreach (var l in new[] { armorTorso, armorBackShoulder, armorBackArm, armorFrontArm, armorFrontShoulder })
                {
                    if (on) l.Texture = "Armor/Armor_" + e.Body;
                    l.Renderer.gameObject.SetActive(on);
                }
                // Clothes under the armor: the shirt layers of torso, shoulders and arms.
                foreach (var l in torso) l.Renderer.gameObject.SetActive(!on);
                foreach (var l in backShoulder) l.Renderer.gameObject.SetActive(!on);
                for (int i = 2; i < backArm.Count; i++) backArm[i].Renderer.gameObject.SetActive(!on);
                for (int i = 1; i < frontArmLayers.Count; i++)
                {
                    frontArmLayers[i].Renderer.gameObject.SetActive(!on);
                    frontShoulderLayers[i].Renderer.gameObject.SetActive(!on);
                }
            }
            if (e.Legs != shownLegs)
            {
                shownLegs = e.Legs;
                if (e.Legs > 0) armorLegs.Texture = "Armor_Legs_" + e.Legs;
                armorLegs.Renderer.gameObject.SetActive(e.Legs > 0);
                pants.Renderer.gameObject.SetActive(e.Legs <= 0);
                shoes.Renderer.gameObject.SetActive(e.Legs <= 0);
            }
        }

        void PoseOutline()
        {
            outlineAlpha = Mathf.MoveTowards(outlineAlpha, OutlineStrength, Time.deltaTime * 4f);
            bool show = outlineAlpha > 0.01f && gameObject.activeInHierarchy;
            var color = new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, 0.7f * outlineAlpha * shownOpacity);
            for (int o = 0; o < outlines.Length; o++)
            {
                var root = outlines[o];
                if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
                if (!show) continue;
                var transforms = outlineTransforms[o];
                for (int i = 1; i < transforms.Length && i < liveTransforms.Length; i++)
                {
                    var src = liveTransforms[i];
                    var dst = transforms[i];
                    if (dst.gameObject.activeSelf != src.gameObject.activeSelf) dst.gameObject.SetActive(src.gameObject.activeSelf);
                    dst.localPosition = src.localPosition;
                    dst.localRotation = src.localRotation;
                    dst.localScale = src.localScale;
                }
                var renderers = outlineRenderers[o];
                for (int i = 0; i < renderers.Length && i < liveRenderers.Length; i++)
                {
                    if (o == OutlineGhost)
                    {
                        // A dimmed, solid copy over the outline copies: the outline shows only as a rim.
                        renderers[i].sprite = liveRenderers[i].sprite;
                        var c = liveRenderers[i].color;
                        c.r *= 0.5f;
                        c.g *= 0.5f;
                        c.b *= 0.55f;
                        c.a *= outlineAlpha;
                        renderers[i].color = c;
                    }
                    else
                    {
                        renderers[i].sprite = TerrariaAssets.Silhouette(liveRenderers[i].sprite);
                        renderers[i].color = color;
                    }
                }
                root.position = transform.position + (Vector3)(outlineOffsets[o] / Ppu);
            }
        }

        // Which of the three pieces a dead player falls into each part belongs to (by sheet).
        static readonly string[] headParts = { "Player_0_0", "Player_0_1", "Player_0_2", "Player_0_15", "Player_Hair_1" };
        static readonly string[] legParts = { "Player_0_10", "Player_0_11", "Player_0_12", "Acc_Shoes_6" };

        /// <summary>
        /// Player.KillMe: the player as last drawn comes apart into head, body and legs, which fly
        /// off with the hit and fade. A copy does it; this sprite is hidden while the hero is dead.
        /// </summary>
        public void FallApart(int hitDir)
        {
            var copy = Instantiate(gameObject, transform.parent);
            copy.name = "Terra Player Remains";
            Destroy(copy.GetComponent<TerraPlayerSprite>());
            var root = copy.transform;
            root.position = transform.position;
            // Pivots inside the 40x56 frame (Terraria's draw origins), relative to its centre, y up.
            var head = Piece(root, "Head", 28f - 22.4f);
            var torso = Piece(root, "Body", 0f);
            var legs = Piece(root, "Legs", 28f - 42f);
            foreach (var sr in copy.GetComponentsInChildren<SpriteRenderer>(false))
            {
                string n = sr.gameObject.name;
                var to = System.Array.IndexOf(headParts, n) >= 0 || n.StartsWith("Armor_Head") ? head
                       : System.Array.IndexOf(legParts, n) >= 0 || n.StartsWith("Armor_Legs") ? legs : torso;
                sr.transform.SetParent(to, true);
            }
            copy.AddComponent<Remains>().Begin(new[] { head, torso, legs }, hitDir);
        }

        static Transform Piece(Transform root, string name, float pivotPx)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(0f, pivotPx / Ppu, 0f);
            return t;
        }

        /// <summary>The three pieces of a dead player, flying and fading on Terraria's tick.</summary>
        sealed class Remains : MonoBehaviour
        {
            Transform[] pieces;
            Vector2[] velocity;
            SpriteRenderer[] renderers;
            Color[] colors;
            int alpha;
            float tickAccumulator;

            public void Begin(Transform[] parts, int hitDir)
            {
                pieces = parts;
                velocity = new Vector2[parts.Length];
                // Independent rolls per piece (Terraria y-down: y between -4 and -1.1).
                for (int i = 0; i < parts.Length; i++)
                    velocity[i] = new Vector2(Random.Range(-20, 21) * 0.1f + 2f * hitDir, -Random.Range(-40, -10) * 0.1f);
                renderers = GetComponentsInChildren<SpriteRenderer>(false);
                colors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) colors[i] = renderers[i].color;
            }

            void Update()
            {
                tickAccumulator += Time.deltaTime * 60f;
                while (tickAccumulator >= 1f)
                {
                    tickAccumulator -= 1f;
                    for (int i = 0; i < pieces.Length; i++)
                    {
                        var v = velocity[i];
                        pieces[i].localPosition += new Vector3(v.x, v.y, 0f) / Ppu;
                        // Terraria turns the piece clockwise by vx * 0.1 radians a tick.
                        pieces[i].localRotation *= Quaternion.Euler(0f, 0f, -v.x * 0.1f * Mathf.Rad2Deg);
                        v.y -= 0.1f;
                        v.x *= 0.99f;
                        velocity[i] = v;
                    }
                    alpha = Mathf.Min(255, alpha + 2);
                }
                // Drawn through the immune alpha: fading, then gone once it passes 125.
                float opacity = alpha > 125 ? 0f : (255 - alpha) / 255f;
                if (opacity <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
                for (int i = 0; i < renderers.Length; i++)
                {
                    var c = colors[i];
                    c.a *= opacity;
                    renderers[i].color = c;
                }
            }
        }

        /// <summary>Terraria px the seated player is drawn higher (seatYOffset for a chair).</summary>
        const float SeatLift = 4f;

        /// <summary>A leg layer: its whole frame, or seated, the three strips of DrawSittingLegs.</summary>
        void Leg(Layer layer, int frame, bool seated)
        {
            for (int k = 0; k < 2; k++)
                if (layer.Strips[k].gameObject.activeSelf != seated) layer.Strips[k].gameObject.SetActive(seated);
            if (!seated)
            {
                Set(layer, frame);
                return;
            }
            // Every strip's top lands on row 42 of the frame, (42 - 28) px below its centre; k 2 is
            // the layer's own renderer, the others sit just in front of it.
            var own = layer.Renderer.transform;
            float z = own.localPosition.z;
            layer.Renderer.sprite = StripSprite(layer, 2);
            own.localPosition = new Vector3(6f / Ppu, -14f / Ppu, z);
            for (int k = 0; k < 2; k++)
            {
                var sr = layer.Strips[k];
                sr.sprite = StripSprite(layer, k);
                sr.color = layer.Renderer.color;
                // Relative to the layer's renderer, which is 6 px forward: these are (2 + 2k) px forward.
                sr.transform.localPosition = new Vector3((2f + 2f * k - 6f) / Ppu, 0f, -(2 - k) * 0.000005f);
            }
        }

        /// <summary>Rows StripTop[k] .. + StripHeight[k] of a layer's first frame, pivoted on the top centre.</summary>
        Sprite StripSprite(Layer layer, int k)
        {
            int top = StripTop[k], h = StripHeight[k];
            var key = (layer.Texture, -1 - k, top, h);
            if (sprites.TryGetValue(key, out var s)) return s;
            var tex = TerrariaAssets.Texture(layer.Texture);
            s = Sprite.Create(tex, new Rect(0, tex.height - top - h, FrameW, h), new Vector2(0.5f, 1f), Ppu, 0, SpriteMeshType.FullRect);
            sprites[key] = s;
            return s;
        }

        void Show(Layer layer, bool visible, int frame)
        {
            layer.Renderer.gameObject.SetActive(visible);
            if (visible) Set(layer, frame);
        }

        /// <summary>Points a layer at sheet N of a kind; false (shown nowhere) for 0.</summary>
        static bool UseSheet(Layer layer, string prefix, int n)
        {
            if (n <= 0) return false;
            layer.Texture = prefix + n;
            return true;
        }

        /// <summary>A layer's centre this many px (facing right, y up) off the frame's centre.</summary>
        static void Place(Layer layer, float x, float y)
        {
            var t = layer.Renderer.transform;
            t.localPosition = new Vector3(x / Ppu, y / Ppu, t.localPosition.z);
        }

        /// <summary>Main.OffsetsPlayerOffhand: where the back hand is in each body frame (px from the frame's top-left).</summary>
        static readonly Vector2[] offhand =
        {
            new Vector2(14, 20), new Vector2(14, 20), new Vector2(14, 20), new Vector2(14, 18), new Vector2(14, 20), new Vector2(16, 4), new Vector2(16, 16),
            new Vector2(18, 14), new Vector2(18, 14), new Vector2(18, 14), new Vector2(16, 16), new Vector2(16, 16), new Vector2(16, 16), new Vector2(16, 16),
            new Vector2(14, 14), new Vector2(14, 14), new Vector2(12, 14), new Vector2(14, 16), new Vector2(16, 16), new Vector2(16, 16),
        };

        // Player.UpdateShadows: every third tick the newest position is recorded and the older ones
        // move back a place, so the afterimages trail 3, 6 and 9 ticks behind.
        void RecordShadow()
        {
            shadowCycle++;
            if (shadowCycle == 1) shadowPos[2] = shadowPos[1];
            else if (shadowCycle == 2) shadowPos[1] = shadowPos[0];
            else
            {
                shadowCycle = 0;
                shadowPos[0] = transform.position;
            }
        }

        readonly List<(Vector3 Pos, float Opacity)> ghostList = new List<(Vector3, float)>();
        float ghostFade = 0.1f, ghostDir = 1f;
        float holyCount;

        /// <summary>
        /// The copies of the pose this frame (LPR:286-460): a dash's or the set's three afterimages
        /// at the recorded positions (shadow 0.5, 0.7, 0.9); ShadowLokis' three halfway back; the
        /// outlines' four, 5 × ghostFade px out each way and as faded (ghostFade bouncing 0.1-0.9 by
        /// 0.075 a frame, 0.025 the Forbidden set's); ShadowSubtle's four jittered 2 px at 0.9;
        /// Holy Protection's pair spreading to 30 px. The set's are off while hidden by stealth.
        /// </summary>
        void PoseGhosts(int dashShadows, Equips e)
        {
            ghostList.Clear();
            var here = transform.position;
            var fx = e.StealthKind != 0 && e.Stealth < 1f ? ArmorFx.None : e.Fx;
            float frames = Time.deltaTime * 60f;
            if ((fx & (ArmorFx.Outlines | ArmorFx.OutlinesForbidden)) != 0)
            {
                float step = (fx & ArmorFx.OutlinesForbidden) != 0 ? 0.025f : 0.075f;
                ghostFade += ghostDir * step * frames;
                if (ghostFade < 0.1f) { ghostFade = 0.1f; ghostDir = 1f; }
                else if (ghostFade > 0.9f) { ghostFade = 0.9f; ghostDir = -1f; }
                float o = 5f * ghostFade / Ppu;
                foreach (var d in new[] { new Vector3(o, 0f), new Vector3(-o, 0f), new Vector3(0f, o), new Vector3(0f, -o) }) ghostList.Add((here + d, 1f - ghostFade));
            }
            int afterimages = Mathf.Max(dashShadows, (fx & ArmorFx.Shadow) != 0 ? 3 : 0);
            for (int g = 0; g < afterimages; g++) ghostList.Add((shadowPos[g], 1f - (0.5f + 0.2f * g)));
            if ((fx & ArmorFx.ShadowLokis) != 0)
                for (int g = 0; g < 3; g++) ghostList.Add((Vector3.Lerp(shadowPos[g], here, 0.5f), 1f - Mathf.Lerp(1f, 0.5f + 0.2f * g, 0.5f)));
            if ((fx & ArmorFx.ShadowSubtle) != 0)
                for (int g = 0; g < 4; g++) ghostList.Add((here + new Vector3(Random.Range(-2, 3) / Ppu, Random.Range(-2, 3) / Ppu, 0f), 0.1f));
            holyCount = e.Holy ? Mathf.Min(30f, holyCount + frames) : Mathf.Max(0f, holyCount - frames);
            if (holyCount > 0f)
                for (int side = -1; side <= 1; side += 2) ghostList.Add((here + new Vector3(side * holyCount / Ppu, 0f, 0f), 0.5f + 0.05f * Random.Range(-1f, 1f)));
            PoseCopies();
        }

        void PoseCopies()
        {
            for (int g = 0; g < ShadowCount; g++)
            {
                var ghost = ghosts[g];
                bool show = g < ghostList.Count && gameObject.activeInHierarchy;
                if (ghost.gameObject.activeSelf != show) ghost.gameObject.SetActive(show);
                if (!show) continue;
                var transforms = ghostTransforms[g];
                var renderers = ghostRenderers[g];
                for (int i = 1; i < transforms.Length && i < liveTransforms.Length; i++)
                {
                    var src = liveTransforms[i];
                    var dst = transforms[i];
                    if (dst.gameObject.activeSelf != src.gameObject.activeSelf) dst.gameObject.SetActive(src.gameObject.activeSelf);
                    dst.localPosition = src.localPosition;
                    dst.localRotation = src.localRotation;
                    dst.localScale = src.localScale;
                }
                float opacity = ghostList[g].Opacity;
                for (int i = 0; i < renderers.Length && i < liveRenderers.Length; i++)
                {
                    renderers[i].sprite = liveRenderers[i].sprite;
                    var c = liveRenderers[i].color;
                    c.a *= opacity;
                    renderers[i].color = c;
                }
                // Slightly behind the player so it never covers the player.
                var at = ghostList[g].Pos;
                ghost.position = new Vector3(at.x, at.y, BaseZ + 0.0003f * (g + 1));
            }
        }

        /// <summary>
        /// Terraria's swing (useStyle 1): the item's handle follows three positions by the hand
        /// while it rotates from behind the head to the feet in front. Worked out facing right;
        /// the body's mirrored scale flips it for the left.
        /// </summary>
        void PoseItem(float progress, Sprite item, float scale, Hold hold)
        {
            bool idle = progress < 0f && item != null && hold.Idle;
            if ((progress < 0f && !idle) || item == null)
            {
                heldItem.gameObject.SetActive(false);
                return;
            }
            Vector2 thrustLoc = default;
            float thrustRot = 0f;
            if (!idle && hold.Thrust && !Items.MeleeSpecials.ThrustLocation(hold.Item, 1f - progress, item.rect.width, scale, out thrustLoc, out thrustRot))
            {
                heldItem.gameObject.SetActive(false);
                return;
            }
            if (!idle && hold.Thrust) Items.MeleeSpecials.ThrustDraw(hold.Item, ref thrustLoc, ref thrustRot);
            heldItem.gameObject.SetActive(true);
            heldItem.sprite = item;
            heldItem.transform.localScale = Vector3.one * scale;
            SetZ(heldItem, backLayers.Count);
            heldGlow.gameObject.SetActive(HeldGlow != null);
            if (HeldGlow != null) heldGlow.sprite = HeldGlow;
            if (idle || hold.Thrust)
            {
                // itemLocation from the hitbox's top-left (its centre 10 px right and 21 down, the frame centre 3 above that), the bottom-left corner pivot.
                var loc = idle ? hold.IdleLoc : thrustLoc;
                float rot = idle ? hold.IdleRot : thrustRot;
                itemRoot.localPosition = new Vector3((loc.x - 10f) / Ppu, (21f - loc.y - 3f) / Ppu, 0f);
                itemRoot.localRotation = Quaternion.Euler(0f, 0f, -rot * Mathf.Rad2Deg);
                return;
            }
            if (hold.Out)
            {
                // useStyle 5 (PlayerDrawLayers.DrawPlayer_27_HeldItem): turned about the player's
                // centre (2 px behind it), guns, bows and tomes with their back edge Offset.X px out
                // along the aim and centred on it; staffs from their bottom corner 6 px out, turned
                // 45 degrees so the diagonal art points along the aim. The frame's centre is 3 px
                // above the hitbox centre. The sprite pivots at its bottom-left.
                float iw = item.rect.width, ih = item.rect.height;
                var center = new Vector2(-2f, -3f - hold.Offset.y) / Ppu;
                var aim = new Vector2(Mathf.Cos(hold.Angle * Mathf.Deg2Rad), Mathf.Sin(hold.Angle * Mathf.Deg2Rad));
                if (hold.Staff)
                {
                    itemRoot.localPosition = new Vector3(0f, -3f / Ppu, 0f) + (Vector3)(aim * (6f / Ppu));
                    itemRoot.localRotation = Quaternion.Euler(0f, 0f, hold.Angle - 45f);
                    return;
                }
                var q = Quaternion.Euler(0f, 0f, hold.Angle);
                var corner = (Vector2)(q * (new Vector2(hold.Offset.x, -ih / 2f) * scale / Ppu));
                itemRoot.localPosition = new Vector3(center.x + corner.x, center.y + corner.y, 0f);
                itemRoot.localRotation = q;
                return;
            }

            float w = item.rect.width, h = item.rect.height;
            float remaining = 1f - progress; // Terraria's itemAnimation / itemAnimationMax
            float lx, ly;
            if (remaining < 0.333f)
            {
                float n = w >= 92 ? 38 : w >= 64 ? 28 : w >= 52 ? 24 : w > 32 ? 14 : 10;
                lx = 10 + (w * 0.5f - n);
                ly = 24;
            }
            else if (remaining < 0.666f)
            {
                float nx = w >= 92 ? 38 : w >= 64 ? 28 : w >= 52 ? 24 : w > 32 ? 18 : 10;
                float ny = h > 64 ? 14 : h > 52 ? 12 : h > 32 ? 8 : 10;
                lx = 10 + (w * 0.5f - nx);
                ly = ny;
            }
            else
            {
                float nx = w >= 92 ? 38 : w >= 64 ? 28 : w >= 48 ? 18 : w > 32 ? 14 : 6;
                float ny = h > 64 ? 14 : h > 52 ? 12 : 10;
                lx = 10 - (w * 0.5f - nx);
                ly = ny;
            }
            float rotation = (remaining - 0.5f) * -3.5f - 0.3f; // radians, clockwise on screen

            // itemLocation is relative to the 20x42 hitbox's top-left, whose centre is 10 px right
            // and 21 px down; the frame centre we are parented to is 3 px above the hitbox centre.
            itemRoot.localPosition = new Vector3((lx - 10f) / Ppu, (21f - ly - 3f) / Ppu, 0f);
            itemRoot.localRotation = Quaternion.Euler(0f, 0f, -rotation * Mathf.Rad2Deg);
        }

        int EyelidFrame()
        {
            int t = blinkTicks % 240 - 234;
            if (t < 0) return 0;
            return t <= 1 ? 1 : t <= 3 ? 2 : 1;
        }

        void Set(Layer layer, int frameIndex, float yOffset = 0f) =>
            Show(layer, 0, frameIndex, yOffset);

        void Set(Layer layer, Vector2Int cell, float yOffset = 0f) =>
            Show(layer, cell.x, cell.y, yOffset);

        void Show(Layer layer, int col, int row, float yOffset)
        {
            layer.Renderer.sprite = GetSprite(layer, col, row + layer.RowOffset);
            var p = layer.Renderer.transform.localPosition;
            layer.Renderer.transform.localPosition = new Vector3(0f, yOffset, p.z);
        }

        Sprite GetSprite(Layer layer, int col, int row)
        {
            // Wings come in 86x62 frames, balloons 52x56; everything else is the 40x56 player frame.
            int fw = layer.Kind == Sheet.Wing ? 86 : layer.Kind == Sheet.Balloon ? 52 : FrameW;
            int fh = layer.Kind == Sheet.Wing ? 62 : FrameH;
            int height = layer.Kind == Sheet.Hair ? fh - 2 : fh;
            var key = (layer.Texture, col, row, height);
            Sprite s;
            if (layer.Kind == Sheet.FrontBack || layer.Kind == Sheet.FrontFront || layer.Kind == Sheet.IceBarrier)
            {
                // Halves of a 40x56 frame pivoted on its centre; the barrier's 50x54 frames on (28, 28) (the Frozen texture's
                // half size, PDL:4046), at the frame's centre.
                key = (layer.Texture + "#" + layer.Kind, col, row, height);
                if (sprites.TryGetValue(key, out s)) return s;
                var t = TerrariaAssets.Texture(layer.Texture);
                Rect r;
                Vector2 pv;
                if (layer.Kind == Sheet.IceBarrier)
                {
                    int bh = 54, top = row * bh;
                    r = new Rect(0, t.height - top - bh, 50, bh);
                    pv = new Vector2(28f / 50f, (bh - 28f) / bh);
                }
                else
                {
                    int top = row * FrameH, h2 = Mathf.Min(FrameH, t.height - top);
                    bool right = layer.Kind == Sheet.FrontBack;
                    r = new Rect(right ? 20 : 0, t.height - top - h2, 20, h2);
                    pv = new Vector2(right ? 0f : 1f, (h2 - FrameH * 0.5f) / h2);
                }
                s = Sprite.Create(t, r, pv, Ppu, 0, SpriteMeshType.FullRect);
                sprites[key] = s;
                return s;
            }
            if (sprites.TryGetValue(key, out s)) return s;
            var tex = TerrariaAssets.Texture(layer.Texture);
            // Shields are 44 px wide, the extra pixels in front; the body centre stays at x = 20.
            int w = layer.Kind == Sheet.Shield ? tex.width : fw;
            int x = col * w, yTop = row * fh;
            // Some sheets are a couple of pixels short on the last frame.
            int h = Mathf.Min(height, tex.height - yTop);
            // Pivot on the full frame's centre so every part lines up and mirrors together.
            var pivot = new Vector2(fw * 0.5f / w, (h - fh * 0.5f) / h);
            s = Sprite.Create(tex, new Rect(x, tex.height - yTop - h, w, h), pivot, Ppu, 0, SpriteMeshType.FullRect);
            sprites[key] = s;
            return s;
        }

        static void Tint(Layer layer, float alpha)
        {
            var c = layer.Tint;
            c.a = alpha;
            layer.Renderer.color = c;
            if (layer.Strips != null) foreach (var sr in layer.Strips) sr.color = c;
        }

        static Color C(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);
    }
}
