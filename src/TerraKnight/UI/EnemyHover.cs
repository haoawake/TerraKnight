using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Main.HoverOverNPCs: with the cursor over an enemy, "name: life/max" follows the cursor,
    /// 14 px right of and below it (34 more with an item on the cursor), in the slowly pulsing
    /// white of Main.mouseTextColor with a black edge. Life is in Terraria points
    /// (<see cref="EnemyStats"/>); the name is the Hunter's Journal's, in the game's language.
    /// </summary>
    internal static class EnemyHover
    {
        static readonly AccessTools.FieldRef<EnemyDeathEffects, string> playerDataName =
            AccessTools.FieldRefAccess<EnemyDeathEffects, string>("playerDataName");

        /// <summary>Journal entries by the enemy's kill-record name: its convo name (NAME_ + it is the shown name).</summary>
        static Dictionary<string, string> journal;
        /// <summary>The journal's portrait of each record (JournalEntryStats.sprite).</summary>
        static readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();
        static readonly Dictionary<int, string> names = new Dictionary<int, string>();
        static GUIStyle style;

        /// <summary>Main.mouseTextColor: up and down a step a tick between 190 and 255.</summary>
        static int pulse = 255, pulseDir = -1;
        static float pulseClock;

        /// <summary>Dev: hover here (world) instead of the real cursor.</summary>
        public static Vector2? DevAt;
        /// <summary>Dev: what the last frame showed (null when nothing).</summary>
        public static string DevShown { get; private set; }

        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint) return;
            DevShown = null;
            for (pulseClock += Time.unscaledDeltaTime; pulseClock >= 1f / 60f; pulseClock -= 1f / 60f)
            {
                pulse += pulseDir;
                if (pulse >= 255) pulseDir = -1;
                if (pulse <= 190) pulseDir = 1;
            }
            // Over an open panel the panel's own text comes first (Terraria's mouseText already set).
            if (InventoryGui.Open && InventoryGui.MouseOverUi) return;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null || HeroController.SilentInstance == null) return;
            Vector2 world;
            if (DevAt.HasValue) world = DevAt.Value;
            else if (!TerraMod.MouseWorld(out world)) return;

            // Main.DrawMouseOver: an item lying there comes first ("name (count)", in its rarity's colour).
            string text;
            var tint = Color.white;
            if (WorldItems.ItemAt(world, out var lying))
            {
                text = lying.Count > 1 ? $"{lying.Def.Name} ({lying.Count})" : lying.Def.Name;
                tint = ItemDb.RarityColor(lying.Def.Rare);
            }
            else
            {
                var info = EnemyAt(world);
                if (info == null) return;
                var hm = info.Hm;
                text = NameOf(hm);
                int max = EnemyStats.LifeMax(info);
                if (max > 1) text += ": " + EnemyStats.Life(hm, info) + "/" + max;
            }
            DevShown = text;

            float ui = GameView.Scale;
            Vector2 m;
            if (DevAt.HasValue)
            {
                var s = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
                m = new Vector2(s.x, Screen.height - s.y);
            }
            else m = Event.current.mousePosition;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = false };
            style.fontSize = Mathf.RoundToInt(18 * ui);
            var size = style.CalcSize(new GUIContent(text));
            // Terraria puts it 14 px right of and below its own small cursor; here the system's
            // arrow, much bigger, hangs down-right from the hot spot and would cover it, so the
            // text sits up beside the hot spot instead.
            float x = m.x + 14f * ui, y = m.y - size.y - 2f * ui;
            if (InventoryGui.Open && !TerraMod.Instance.Inventory.Mouse.IsEmpty) x += 34f * ui;
            // Kept 40 px off the right edge, and on screen at the top.
            x = Mathf.Min(x, Screen.width - size.x - 40f * ui);
            y = Mathf.Clamp(y, 4f, Screen.height - size.y - 40f * ui);
            float a = pulse / 255f;
            var r = new Rect(x, y, size.x + 4f, size.y);
            style.normal.textColor = new Color(0f, 0f, 0f, a);
            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                    if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox * 1.5f * ui, r.y + oy * 1.5f * ui, r.width, r.height), text, style);
            style.normal.textColor = new Color(tint.r, tint.g, tint.b, a);
            GUI.Label(r, text, style);
        }

        /// <summary>The live, seen enemy whose picture is under a world point; the nearest one's centre wins.</summary>
        static EnemyStats.Info EnemyAt(Vector2 p)
        {
            EnemyStats.Info best = null;
            float bestD = float.MaxValue;
            foreach (var info in EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy || EnemyStats.Defeated(hm)) continue;
                // NPC.frame: the picture's own rectangle, or the hurt box where it has none.
                var rend = hm.GetComponent<Renderer>();
                Bounds b;
                if (rend != null && rend.enabled && rend.isVisible) b = rend.bounds;
                else
                {
                    var col = hm.GetComponent<Collider2D>();
                    if (col == null || !col.enabled || !AnyVisible(hm)) continue;
                    b = col.bounds;
                }
                if (p.x < b.min.x || p.x > b.max.x || p.y < b.min.y || p.y > b.max.y) continue;
                float d = ((Vector2)b.center - p).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = info;
                }
            }
            return best;
        }

        static bool AnyVisible(HealthManager hm)
        {
            foreach (var r in hm.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.isVisible) return true;
            return false;
        }

        /// <summary>The Hunter's Journal name, else the object's own name without its numbering.</summary>
        public static string NameOf(HealthManager hm)
        {
            int id = hm.GetInstanceID();
            if (names.TryGetValue(id, out var name)) return name;
            name = JournalName(hm);
            if (name == null)
            {
                name = hm.gameObject.name;
                int cut = name.IndexOf('(');
                if (cut > 0) name = name.Substring(0, cut);
                name = name.TrimEnd(' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                if (name.Length == 0) name = hm.gameObject.name;
            }
            names[id] = name;
            if (names.Count > 512) names.Clear();
            return name;
        }

        /// <summary>
        /// The dream bosses share their waking selves' journal entries; their own names are on
        /// their title cards.
        /// </summary>
        static readonly (string Object, string Title)[] dreamTitles =
        {
            ("Lost Kin", "INFECTED_KNIGHT_DREAM_MAIN"), ("False Knight Dream", "FALSE_KNIGHT_DREAM_MAIN"),
            ("Dream Mage Lord", "MAGE_LORD_DREAM_MAIN"), ("Absolute Radiance", "ABSOLUTE_RADIANCE_MAIN"),
        };

        static string DreamTitle(string objectName)
        {
            foreach (var (obj, title) in dreamTitles)
                if (objectName.StartsWith(obj) && TeamCherry.Localization.Language.Has(title, "Titles"))
                    return TeamCherry.Localization.Language.Get(title, "Titles");
            return null;
        }

        /// <summary>Enemies with no journal entry but a name of their own (the Shade's death effects carry another enemy's record).</summary>
        static readonly (string Prefix, string Name)[] ownNames = { ("Hollow Shade", "阴影") };

        static string JournalName(HealthManager hm)
        {
            string dream = DreamTitle(hm.gameObject.name);
            if (dream != null) return dream;
            foreach (var (prefix, own) in ownNames)
                if (hm.gameObject.name.StartsWith(prefix)) return Lang.T(own);
            foreach (var (prefix, rec) in nameToRecord)
                if (hm.gameObject.name.StartsWith(prefix) && rec != null) return NameOfRecord(rec);
            var death = hm.GetComponent<EnemyDeathEffects>();
            string record = death != null ? playerDataName(death) : null;
            BuildJournal();
            if (string.IsNullOrEmpty(record) || !journal.ContainsKey(record)) record = RecordFromName(hm.gameObject.name) ?? record;
            if (string.IsNullOrEmpty(record)) return null;
            string convo = journal.TryGetValue(record, out var c) ? c : record.ToUpperInvariant();
            string key = "NAME_" + convo;
            return TeamCherry.Localization.Language.Has(key, "Journal") ? TeamCherry.Localization.Language.Get(key, "Journal") : null;
        }

        /// <summary>Boss objects whose names don't start with their journal record.</summary>
        static readonly Dictionary<string, string> aliases = new Dictionary<string, string>
        {
            { "Giant Fly", "BigFly" }, { "Giant Buzzer", "BigBuzzer" }, { "Radiance", "FinalBoss" },
            { "Oro", "NailBros" }, { "Mato", "NailBros" }, { "Sheo Boss", "Paintmaster" },
            { "Pure Vessel", "HollowKnightPrime" }, { "Nightmare Grimm Boss", "NightmareGrimm" }, { "Zote Boss", "GreyPrince" },
        };

        /// <summary>
        /// Bosses mostly keep no kill record of their own: the longest run of their object name's
        /// first words that is one ("Hornet Boss 1" is Hornet, "False Knight New" the False
        /// Knight), or its first and last word ("Ghost Warrior Marmu" is Marmu).
        /// </summary>
        static string RecordFromName(string objectName)
        {
            int cut = objectName.IndexOf('(');
            if (cut > 0) objectName = objectName.Substring(0, cut);
            objectName = objectName.Trim();
            var words = objectName.Split(new[] { ' ', '_' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int k = words.Length; k >= 1; k--)
            {
                string prefix = string.Join(" ", words, 0, k);
                if (aliases.TryGetValue(prefix, out var alias) && journal.ContainsKey(alias)) return alias;
                string candidate = string.Join("", words, 0, k);
                if (journal.ContainsKey(candidate)) return candidate;
            }
            if (words.Length > 2 && journal.ContainsKey(words[0] + words[words.Length - 1])) return words[0] + words[words.Length - 1];
            return null;
        }

        /// <summary>The enemy's Hunter's Journal kill record ("Crawler"; a boss by its object's name), or null.</summary>
        public static string RecordOf(HealthManager hm)
        {
            if (hm == null) return null;
            // Some objects carry a wrong record (the White Palace's wingmoulds and the Zotelings use
            // the Crawlid's no-op one, Grimm the Hornet's): by name first (spec-hkenemies 1.3).
            string obj = hm.gameObject.name;
            foreach (var (prefix, rec) in nameToRecord)
                if (obj.StartsWith(prefix)) return rec;
            var death = hm.GetComponent<EnemyDeathEffects>();
            string record = death != null ? playerDataName(death) : null;
            BuildJournal();
            if (string.IsNullOrEmpty(record) || !journal.ContainsKey(record)) record = RecordFromName(hm.gameObject.name) ?? record;
            return string.IsNullOrEmpty(record) ? null : record;
        }

        /// <summary>Object names whose journal record is another (null: not an enemy of its own, never counted).</summary>
        static readonly (string Prefix, string Record)[] nameToRecord =
        {
            ("White Palace Fly", "PalaceFly"), ("Ordeal Zoteling", "ZotelingHopper"), ("Zote Balloon", "ZotelingBalloon"), ("Zoteling", "ZotelingBuzzer"),
            ("Nightmare Grimm Boss", "NightmareGrimm"), ("Grimm Boss", "Grimm"), ("Corpse Flukeman", null), ("Flukeman Top", null), ("Flukeman Bot", null),
            ("fluke_baby", null), ("Hollow Shade", null),
            // Their deaths carry the placeholder record (Dummy) or none: the Soul Tyrant's two phases (the dream's, Godhome's;
            // the book keeps it as MageLord@5), the Abyss's Siblings, the Spiny Husks risen from the corpses, Godhome's Crystal
            // and Enraged Guardians, the Absolute Radiance.
            ("Dream Mage Lord", "MageLord"), ("Shade Sibling", "Sibling"), ("Corpse Garden Zombie", "GardenZombie"),
            ("Mega Zombie Beam Miner", "MegaBeamMiner"), ("Zombie Beam Miner Rematch", "MegaBeamMiner"), ("Absolute Radiance", "FinalBoss"),
        };

        /// <summary>A kill record's Hunter's Journal name in the game's language, or null when the journal has none.</summary>
        public static string NameOfRecord(string record)
        {
            if (string.IsNullOrEmpty(record)) return null;
            int at = record.IndexOf('@');
            if (at > 0) record = record.Substring(0, at);
            BuildJournal();
            string convo = journal.TryGetValue(record, out var c) ? c : record.ToUpperInvariant();
            string key = "NAME_" + convo;
            return TeamCherry.Localization.Language.Has(key, "Journal") ? TeamCherry.Localization.Language.Get(key, "Journal") : null;
        }

        /// <summary>The Hunter's Journal portrait of a record ("Hornet@4" is Hornet's), or null.</summary>
        public static Sprite PortraitOf(string record)
        {
            if (string.IsNullOrEmpty(record)) return null;
            int at = record.IndexOf('@');
            if (at > 0) record = record.Substring(0, at);
            BuildJournal();
            return portraits.TryGetValue(record, out var s) && s != null ? s : null;
        }

        /// <summary>A journal text of a record: "DESC_" (the description) or "NOTE_" (the Hunter's notes); null if there is none.</summary>
        public static string JournalText(string record, string prefix)
        {
            if (string.IsNullOrEmpty(record)) return null;
            int at = record.IndexOf('@');
            if (at > 0) record = record.Substring(0, at);
            BuildJournal();
            string convo = journal.TryGetValue(record, out var c) ? c : record.ToUpperInvariant();
            string key = prefix + convo;
            if (!TeamCherry.Localization.Language.Has(key, "Journal")) return null;
            string text = TeamCherry.Localization.Language.Get(key, "Journal");
            // The journal marks line breaks with <br>.
            return string.IsNullOrEmpty(text) ? null : text.Replace("<br>", "\n").Replace("<br/>", "\n");
        }

        /// <summary>Dev: the name an enemy object of this name with no kill record would show.</summary>
        public static string DevNameFor(string objectName)
        {
            string dream = DreamTitle(objectName);
            if (dream != null) return "标题 → " + dream;
            BuildJournal();
            string record = RecordFromName(objectName);
            if (record == null) return "(无)";
            string key = "NAME_" + journal[record];
            return record + " → " + (TeamCherry.Localization.Language.Has(key, "Journal") ? TeamCherry.Localization.Language.Get(key, "Journal") : "(无)");
        }

        static void BuildJournal()
        {
            if (journal == null || journal.Count == 0)
            {
                // The journal's entries live in the inventory pane, loaded with the HUD. Some
                // share a kill record (the Hollow Shade's is the crawlid's): the entry named after
                // the record ("Journal Crawler") is the enemy's own.
                journal = new Dictionary<string, string>();
                var exact = new HashSet<string>();
                foreach (var e in Resources.FindObjectsOfTypeAll<JournalEntryStats>())
                {
                    if (string.IsNullOrEmpty(e.playerDataName) || string.IsNullOrEmpty(e.convoName)) continue;
                    string obj = e.name.Replace("(Clone)", "").Trim();
                    bool own = obj == "Journal " + e.playerDataName || e.convoName.Replace("_", "") == e.playerDataName.ToUpperInvariant();
                    if (exact.Contains(e.playerDataName) || (journal.ContainsKey(e.playerDataName) && !own)) continue;
                    journal[e.playerDataName] = e.convoName;
                    if (e.sprite != null) portraits[e.playerDataName] = e.sprite;
                    if (own) exact.Add(e.playerDataName);
                }
            }
        }
    }
}
