using UnityEngine;

namespace NiceBoat
{
    [DefaultExecutionOrder(-100)]
    public sealed class OceanSurface : MonoBehaviour
    {
        public Renderer waterRenderer;
        public float seaLevel;
        public float amplitude = 0.24f;
        public float wavelength = 14f;
        public float speed = 1.1f;
        static readonly int Clock = Shader.PropertyToID("_NiceBoatOceanTime");
        static readonly int UseClock = Shader.PropertyToID("_NiceBoatUseOceanTime");

        void OnEnable()
        {
            RefreshSettings();
            if (Application.isPlaying) Shader.SetGlobalFloat(UseClock, 1f);
        }

        void Update() => Shader.SetGlobalFloat(Clock, Time.time);
        void OnDisable() => Shader.SetGlobalFloat(UseClock, 0f);

        public void RefreshSettings()
        {
            if (waterRenderer == null || waterRenderer.sharedMaterial == null) return;
            var material = waterRenderer.sharedMaterial;
            amplitude = material.GetFloat("_WaveAmplitude");
            wavelength = material.GetFloat("_WaveLength");
            speed = material.GetFloat("_WaveSpeed");
        }

        // Keep these two phases in sync with SummerOcean.shader's vertex stage.
        public float Height(Vector3 world, float time)
        {
            float k = 2f * Mathf.PI / Mathf.Max(wavelength, 0.001f);
            float a = (world.x * 0.8f + world.z * 0.6f) * k + time * speed;
            float b = (world.x * -0.6f + world.z * 0.8f) * k * 1.61f + time * speed * 1.23f;
            return seaLevel + amplitude * (Mathf.Sin(a) + 0.5f * Mathf.Sin(b));
        }

        public float VerticalVelocity(Vector3 world, float time)
        {
            float k = 2f * Mathf.PI / Mathf.Max(wavelength, 0.001f);
            float a = (world.x * 0.8f + world.z * 0.6f) * k + time * speed;
            float b = (world.x * -0.6f + world.z * 0.8f) * k * 1.61f + time * speed * 1.23f;
            return amplitude * speed * (Mathf.Cos(a) + 0.615f * Mathf.Cos(b));
        }
    }
}
