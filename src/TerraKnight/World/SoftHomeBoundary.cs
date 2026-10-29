using TerraKnight.Effects;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>Quiet motes on the actual rectangle, with no continuously lit line or screen-wide light.</summary>
    internal sealed class SoftHomeBoundary : MonoBehaviour
    {
        static Material material;
        ParticleSystem particles;
        float emissionClock;
        public Color Tint;
        internal static SoftHomeBoundary Create(SpriteRenderer source)
        {
            var go = new GameObject("Home Soft Particle Boundary");
            go.transform.SetParent(source.transform, false);
            var boundary = go.AddComponent<SoftHomeBoundary>();
            boundary.particles = go.AddComponent<ParticleSystem>();
            boundary.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = boundary.particles.main;
            main.playOnAwake = false; main.loop = true; main.duration = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f; main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(.045f, .095f); main.maxParticles = 220;
            var emission = boundary.particles.emission; emission.enabled = false;
            var shape = boundary.particles.shape; shape.enabled = false;
            var fade = boundary.particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.55f, .25f), new GradientAlphaKey(.35f, .7f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = boundary.particles.GetComponent<ParticleSystemRenderer>();
            if (material == null)
            {
                material = new Material(TerraParticles.AdditiveMaterial()) { name = "Home Soft Boundary Motes" };
                material.mainTexture = TerrariaAssets.Texture("Extra_98");
            }
            renderer.sharedMaterial = material; renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = LightMap.Order + 1;
            boundary.particles.Play();
            return boundary;
        }
        void Update()
        {
            if (particles == null) return;
            const float half = (Stations.HomeRadius + .5f) * CellGrid.CellSize;
            const float centreY = 1.5f * CellGrid.CellSize;
            emissionClock += Mathf.Min(Time.deltaTime, .1f) * 72f;
            int count = Mathf.FloorToInt(emissionClock); emissionClock -= count;
            for (int i = 0; i < count; i++)
            {
                float t = Random.value * 8f * half;
                Vector3 p;
                if (t < 2f * half) p = new Vector3(-half + t, centreY - half, 0);
                else if (t < 4f * half) p = new Vector3(half, centreY - half + t - 2f * half, 0);
                else if (t < 6f * half) p = new Vector3(half - (t - 4f * half), centreY + half, 0);
                else p = new Vector3(-half, centreY + half - (t - 6f * half), 0);
                var c = Color.Lerp(Tint, new Color(.8f, .85f, .9f), .28f); c.a = .42f;
                var emit = new ParticleSystem.EmitParams { position = p, velocity = new Vector3(Random.Range(-.025f,.025f), Random.Range(-.02f,.02f), 0), startColor = c };
                particles.Emit(emit, 1);
            }
        }
    }
}
