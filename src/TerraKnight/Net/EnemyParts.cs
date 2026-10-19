using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TerraKnight.Net
{
    internal static partial class Enemies
    {
        struct PartState
        {
            public string Path;
            public ushort Name;
            public Looks.State Look;
        }

        static readonly Dictionary<HealthManager, (float At, List<(Transform Tr, string Path)> Parts)> partLists = new Dictionary<HealthManager, (float, List<(Transform, string)>)>();

        /// <summary>
        /// The enemy's own parts (not those of another enemy under it): every child drawn (sprites, meshes, particles) or
        /// hurting, by its places among its siblings down from the enemy. Looked up again each second (the game adds and
        /// takes away children as a fight goes: what it spawns under a boss is a SceneMirror copy, never a part).
        /// </summary>
        static List<(Transform Tr, string Path)> PartsOf(HealthManager hm)
        {
            if (partLists.TryGetValue(hm, out var c) && Time.unscaledTime - c.At < 1f) return c.Parts;
            var parts = new List<(Transform, string)>();
            foreach (var tr in hm.GetComponentsInChildren<Transform>(true))
            {
                if (tr == hm.transform || tr.GetComponentInParent<HealthManager>() != hm || SceneMirror.IsSpawned(tr)) continue;
                if (tr.GetComponent<Renderer>() == null && tr.GetComponent<DamageHero>() == null) continue;
                var path = new List<string>();
                for (var p = tr; p != hm.transform; p = p.parent) path.Add(p.GetSiblingIndex().ToString());
                path.Reverse();
                parts.Add((tr, string.Join("/", path)));
            }
            partLists[hm] = (Time.unscaledTime, parts);
            return parts;
        }

        // Child sprites and attack objects are often enabled only by the boss's FSM.
        // A frozen replica must receive those states as well as the root's pose.
        static void WriteParts(BinaryWriter w, HealthManager hm)
        {
            var parts = PartsOf(hm);
            int n = 0;
            foreach (var (tr, _) in parts) if (tr != null) n++;
            w.Write((ushort)n);
            foreach (var (tr, path) in parts)
            {
                if (tr == null) continue;
                w.Write(path);
                w.Write(Looks.Hash(tr.name));
                Looks.Write(w, Looks.Of(tr, tr.localPosition, tr.localEulerAngles.z));
            }
        }

        static PartState[] ReadParts(BinaryReader r)
        {
            var parts = new PartState[r.ReadUInt16()];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = new PartState { Path = r.ReadString(), Name = r.ReadUInt16(), Look = Looks.Read(r) };
            return parts;
        }

        static void ApplyParts(Puppet puppet, PartState[] parts)
        {
            foreach (var p in parts)
            {
                var tr = puppet.Hm.transform;
                foreach (var segment in p.Path.Split('/'))
                {
                    if (!int.TryParse(segment, out int index) || index < 0 || index >= tr.childCount) { tr = null; break; }
                    tr = tr.GetChild(index);
                }
                // Not the same object here (the children differ): left alone rather than another one made to look like it.
                if (tr == null || Looks.Hash(tr.name) != p.Name || tr.GetComponentInParent<HealthManager>() != puppet.Hm) continue;
                if (p.Look.On)
                    for (var parent = tr.parent; parent != null && parent != puppet.Hm.transform; parent = parent.parent)
                        if (!parent.gameObject.activeSelf) parent.gameObject.SetActive(true);
                Looks.Apply(tr, p.Look, true, true, false);
            }
        }
    }
}
