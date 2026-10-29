using TerraKnight.Effects;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>Bright, additive green sparkles rising from storage and crafting stations connected to a home.</summary>
    internal static class HomeConnectionHighlight
    {
        static Material particleMaterial;

        static Material Material()
        {
            if (particleMaterial != null) return particleMaterial;
            particleMaterial = new Material(TerraParticles.AdditiveMaterial()) { name = "Home Green Sparkle Light" };
            particleMaterial.mainTexture = TerrariaAssets.Texture("Extra_98");
            return particleMaterial;
        }

        public static void Draw(ref ParticleSystem particles, SpriteRenderer source, bool connected)
        {
            if (!connected || source == null || !source.enabled || source.sprite == null)
            {
                if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }
            if (particles == null)
            {
                var go = new GameObject("Home Connected Green Particles");
                go.transform.SetParent(source.transform, false);
                particles = go.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false;
                main.loop = true;
                main.duration = 1.4f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.4f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(.055f, .11f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(.28f, .85f, .4f, .45f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = 18;
                var emission = particles.emission;
                emission.rateOverTime = 7f;
                var shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                var velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = new ParticleSystem.MinMaxCurve(-.09f, .09f);
                velocity.y = new ParticleSystem.MinMaxCurve(.18f, .42f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                var fade = particles.colorOverLifetime;
                fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] {new GradientColorKey(new Color(.18f,1f,.3f),0f),new GradientColorKey(new Color(.6f,1f,.7f),1f)},
                    new[] {new GradientAlphaKey(0f,0f),new GradientAlphaKey(.65f,.18f),new GradientAlphaKey(.4f,.65f),new GradientAlphaKey(0f,1f)});
                fade.color = gradient;
                var size = particles.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f,new AnimationCurve(new Keyframe(0f,.35f),new Keyframe(.3f,1f),new Keyframe(1f,.15f)));
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = Material();
                renderer.sortingLayerID = source.sortingLayerID;
                // Above the world's dark light map, so this is visibly luminous even in dark rooms.
                renderer.sortingOrder = LightMap.Order + 1;
            }
            var bounds = source.sprite.bounds;
            particles.transform.localPosition = new Vector3(bounds.center.x,bounds.center.y,-.0001f);
            var spread = particles.shape;
            spread.scale = new Vector3(Mathf.Max(.25f,bounds.size.x*.85f),Mathf.Max(.25f,bounds.size.y*.75f),.01f);
            if (!particles.isPlaying) {particles.Play(true);particles.Emit(4);}
        }
    }
}
