using System.Collections.Generic;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight.Player
{
    /// <summary>
    /// Where the game wants the Knight's own nail or his Dream Nail, items of the bag here (the user's): coming near one
    /// without the item in hand, a line in the chat in the bottom-left corner says which and how, once a room. The Dream
    /// Nail's: a warrior's remains (Ghost Warrior NPC: its dream), a dream's way in (Dream Enter: the Dreamers', the dream
    /// bosses' remains), a whispering root (Dream Plant: its essence). The nail's: a bounce shroom (BounceShroom: the down
    /// slash bounces off it), Revek (parried by the nail).
    /// </summary>
    internal static class NailHints
    {
        enum Kind { Warrior, DreamEnter, Root, Shroom, Revek }

        static readonly List<(Transform T, Kind K)> targets = new List<(Transform, Kind)>();
        static readonly HashSet<(string, Kind)> told = new HashSet<(string, Kind)>();
        static string scannedFor;
        static float nextScan, nextLook;
        static int scans;
        const float Near = 6f;
        public static int DevHints;

        static bool Dream(Kind k) => k == Kind.Warrior || k == Kind.DreamEnter || k == Kind.Root;

        /// <summary>Each frame in play: the room's targets found once, the player's distance to them looked at four times a second.</summary>
        public static void Tick(HeroController hero)
        {
            if (hero == null || TerraMod.Instance == null || !GameHooks.PlayerHasControl()) return;
            var gm = GameManager.UnsafeInstance;
            string scene = gm != null ? gm.sceneName : null;
            if (scene == null) return;
            float now = Time.unscaledTime;
            // Looked over as the room comes in, and twice more as its late parts appear (2 s, 6 s); not every frame.
            if (scene != scannedFor) { told.RemoveWhere(t => t.Item1 != scene); scannedFor = scene; scans = 0; nextScan = now; }
            if (scans < 3 && now >= nextScan)
            {
                Scan();
                scans++;
                nextScan = now + (scans == 1 ? 2f : 4f);
            }
            if (now < nextLook || targets.Count == 0) return;
            nextLook = now + 0.25f;
            Vector2 at = hero.transform.position;
            foreach (var (t, k) in targets)
            {
                if (t == null || !t.gameObject.activeInHierarchy || told.Contains((scene, k))) continue;
                if (((Vector2)t.position - at).sqrMagnitude > Near * Near) continue;
                if (Dream(k) ? Items.DreamNailItem.Holding() : Items.NailItem.Holding()) continue;
                told.Add((scene, k));
                string line = Line(k);
                if (line == null) continue;
                DevHints++;
                Plugin.Log.LogInfo($"[提示] 靠近 {t.name}：{line}");
                Net.NetChat.Add(line, new Color(1f, 0.94f, 0.08f));
            }
        }

        static string Line(Kind k)
        {
            var pd = PlayerData.instance;
            bool hasDream = pd != null && pd.hasDreamNail;
            if (Dream(k) && !hasDream) return Lang.T("这里要用梦之钉：梦之钉在安息之地获得");
            switch (k)
            {
                case Kind.Warrior: return Lang.T("战士的遗骸：把梦之钉拿在手上对它挥一下，进入战士之梦");
                case Kind.DreamEnter: return Lang.T("把梦之钉拿在手上对它挥一下，进入梦境");
                case Kind.Root: return Lang.T("低语之根：把梦之钉拿在手上打它，收集飘出的梦境精华");
                case Kind.Shroom: return Lang.T("弹跳蘑菇：拿着骨钉在空中按住下键下劈，能踩着它弹起来");
                case Kind.Revek: return Lang.T("复仇者：拿着骨钉在它扑过来时挥砍，把它弹开");
                default: return null;
            }
        }

        static void Scan()
        {
            targets.Clear();
            for (int i = 0; i < USM.sceneCount; i++)
            {
                var s = USM.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (var root in s.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        string n = t.name;
                        if (n.StartsWith("Ghost Warrior NPC")) targets.Add((t, Kind.Warrior));
                        else if (n.StartsWith("Dream Enter")) targets.Add((t, Kind.DreamEnter));
                        else if (n.StartsWith("Dream Plant")) targets.Add((t, Kind.Root));
                        else if (n.StartsWith("Revek")) targets.Add((t, Kind.Revek));
                        else if (t.GetComponent<BounceShroom>() != null) targets.Add((t, Kind.Shroom));
                    }
            }
        }
    }
}
