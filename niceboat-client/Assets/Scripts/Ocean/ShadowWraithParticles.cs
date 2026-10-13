using UnityEngine;
using UnityEngine.Rendering;

namespace NiceBoat
{
    public sealed class ShadowWraithParticles : MonoBehaviour
    {
        public ParticleSystem mainSmoke, outerSmoke, fragments, bursts;
        [Range(0.25f, 2f)] public float density = 1f;
        [Range(0.3f, 2f)] public float smokeSize = 1f;
        [Range(0.1f, 2f)] public float riseSpeed = 0.55f;
        [Range(0f, 0.5f)] public float trailStrength = 0.14f;
        [Min(1)] public int mainBudget = 40, outerBudget = 28, fragmentBudget = 10, burstBudget = 48;
        [Range(0.25f, 1f)] public float mobileDensity = 0.65f;
        [Tooltip("Use the project's Mobile URP asset. Quality indices can be stripped and renumbered in builds.")]
        public RenderPipelineAsset mobilePipeline;
        [Tooltip("Multiplies the shared smoke material's charcoal color.")]
        public Color smokeTint = Color.white;
        bool dirty = true;
        RenderPipelineAsset configuredPipeline;

        public int ParticleCount => mainSmoke.particleCount + outerSmoke.particleCount + fragments.particleCount + bursts.particleCount;
        public int Capacity => mainSmoke.main.maxParticles + outerSmoke.main.maxParticles + fragments.main.maxParticles + bursts.main.maxParticles;
        public bool IsAlive => mainSmoke.IsAlive(false) || outerSmoke.IsAlive(false) || fragments.IsAlive(false) || bursts.IsAlive(false);
        void OnValidate() { dirty = true; }

        public void ApplySettings()
        {
            var current = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            if (!dirty && configuredPipeline == current) return;
            dirty = false; configuredPipeline = current;
            float budget = mobilePipeline != null && current == mobilePipeline ? mobileDensity : 1f;
            Configure(mainSmoke, mainBudget, 18f, 0.9f, 1.6f, budget);
            Configure(outerSmoke, outerBudget, 12f, 0.95f, 1.7f, budget);
            Configure(fragments, fragmentBudget, 3f, 0.08f, 0.23f, budget);
            var main = bursts.main; main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(burstBudget * budget));
        }

        void Configure(ParticleSystem system, int cap, float rate, float small, float large, float budget)
        {
            var main = system.main;
            main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(cap * budget));
            main.startSize = new ParticleSystem.MinMaxCurve(small * smokeSize, large * smokeSize);
            main.startColor = smokeTint;
            var emission = system.emission; emission.rateOverTime = rate * density * budget;
        }

        public void ResetForSpawn()
        {
            ApplySettings(); Clear();
            mainSmoke.Play(false); outerSmoke.Play(false); fragments.Play(false); bursts.Play(false);
            // Fill only the local cloud at the new position; never transport old world-space smoke.
            mainSmoke.Simulate(0.7f, false, false, true); mainSmoke.Play(false);
            outerSmoke.Simulate(0.5f, false, false, true); outerSmoke.Play(false);
        }

        public void Flow(Vector3 velocity)
        {
            ApplySettings();
            SetFlow(mainSmoke, velocity, riseSpeed);
            SetFlow(outerSmoke, velocity, riseSpeed * 0.65f);
        }

        void SetFlow(ParticleSystem system, Vector3 velocity, float up)
        {
            var flow = system.velocityOverLifetime;
            flow.x = -velocity.x * trailStrength; flow.z = -velocity.z * trailStrength; flow.y = up;
        }

        public void Scatter(Vector3 point, bool death)
        {
            int count = Mathf.Min(death ? 30 : 8, bursts.main.maxParticles);
            for (int i = 0; i < count; i++)
            {
                var radial = Random.onUnitSphere;
                bursts.Emit(new ParticleSystem.EmitParams {
                    position = point + radial * (death ? 0.5f : 0.12f),
                    velocity = radial * (death ? 2.4f : 1.6f) + Vector3.up * 0.45f,
                    startSize = Random.Range(0.45f, death ? 1.05f : 0.7f) * smokeSize,
                    startLifetime = Random.Range(0.65f, death ? 1.8f : 1f), startColor = smokeTint }, 1);
            }
            if (!death) return;
            mainSmoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            outerSmoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            fragments.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            bursts.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        public void Clear()
        {
            mainSmoke.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            outerSmoke.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            fragments.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            bursts.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
