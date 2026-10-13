using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NiceBoat
{
    // One bounded dynamic mesh and one particle pool serve every boat/buoy.
    public sealed class WaterEffects : MonoBehaviour
    {
        public OceanSurface ocean;
        public Material rippleMaterial;
        public Material sprayMaterial;
        public const int Capacity = 72;
        const int Segments = 3;
        struct Ripple
        {
            public Vector3 position;
            public Vector3 forward;
            public float born, life, size, strength, stretch, kind, seed;
        }
        readonly Ripple[] ripples = new Ripple[Capacity];
        readonly List<Vector3> vertices = new(Capacity * 16);
        readonly List<Vector2> uvs = new(Capacity * 16);
        readonly List<Vector3> details = new(Capacity * 16);
        readonly List<Color> colors = new(Capacity * 16);
        readonly List<int> indices = new(Capacity * 54);
        int cursor;
        Mesh mesh;
        ParticleSystem spray;
        public int ActiveRippleCount { get; private set; }
        public int EmittedRippleCount { get; private set; }

        void Awake()
        {
            mesh = new Mesh { name = "Pooled water ripples" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = rippleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var sprayObject = new GameObject("Pooled spray");
            sprayObject.transform.SetParent(transform, false);
            spray = sprayObject.AddComponent<ParticleSystem>();
            spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spray.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = 180;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.gravityModifier = 0.6f;
            var emission = spray.emission;
            emission.enabled = false;
            var shape = spray.shape;
            shape.enabled = false;
            var tint = spray.colorOverLifetime;
            tint.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(0.85f,0.86f,0.72f), 0), new GradientColorKey(new Color(0.64f,0.71f,0.61f), 1) },
                new[] { new GradientAlphaKey(0.65f, 0), new GradientAlphaKey(0f, 1) });
            tint.color = gradient;
            var particleRenderer = spray.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = sprayMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            spray.Play();
        }

        public void EmitRipple(Vector3 position, Vector3 forward, float strength, bool wake)
        {
            AddRipple(position, forward, strength, wake ? 1f : 0f);
        }

        public void EmitBow(Vector3 position, Vector3 forward, float strength)
        {
            AddRipple(position, forward, strength, 2f);
        }

        void AddRipple(Vector3 position, Vector3 forward, float strength, float kind)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            bool wake = kind == 1f, bow = kind == 2f;
            ripples[cursor] = new Ripple { position = position, forward = forward.normalized, born = Time.time,
                life = bow ? 0.65f : (wake ? 3.6f : 1.8f), size = bow ? 0.8f : (wake ? 0.65f : 0.35f),
                strength = Mathf.Clamp01(strength), stretch = wake ? 1.5f : 0.85f,
                kind = kind, seed = (cursor * 0.618034f) % 1f };
            cursor = (cursor + 1) % Capacity;
            EmittedRippleCount++;
        }

        public void EmitSpray(Vector3 position, Vector3 velocity, int count)
        {
            if (spray == null) return;
            for (int i = 0; i < Mathf.Min(count, 16); i++)
            {
                var emit = new ParticleSystem.EmitParams {
                    position = position + Random.insideUnitSphere * 0.14f,
                    velocity = velocity + Vector3.up * Random.Range(1f, 2.3f) + Random.insideUnitSphere * 0.65f };
                spray.Emit(emit, 1);
            }
        }

        void LateUpdate()
        {
            if (ocean == null || mesh == null) return;
            vertices.Clear(); uvs.Clear(); details.Clear(); colors.Clear(); indices.Clear();
            ActiveRippleCount = 0;
            float time = Time.time;
            foreach (var ripple in ripples)
            {
                float age = time - ripple.born;
                if (ripple.life <= 0f || age < 0f || age >= ripple.life) continue;
                ActiveRippleCount++;
                float progress = age / ripple.life;
                float radius = ripple.size + age * (ripple.kind == 2f ? 0.35f : 0.5f);
                float opacity = ripple.strength * (1f - progress) * Mathf.Clamp01(age * 12f);
                Vector3 right = Vector3.Cross(Vector3.up, ripple.forward);
                int start = vertices.Count;
                for (int z = 0; z <= Segments; z++)
                for (int x = 0; x <= Segments; x++)
                {
                    Vector2 uv = new((float)x / Segments, (float)z / Segments);
                    var point = ripple.position + right * ((uv.x * 2f - 1f) * radius)
                        + ripple.forward * ((uv.y * 2f - 1f) * radius * ripple.stretch);
                    point.y = ocean.Height(point, time) + 0.065f;
                    vertices.Add(transform.InverseTransformPoint(point));
                    uvs.Add(uv);
                    details.Add(new Vector3(ripple.kind,ripple.seed,progress));
                    colors.Add(new Color(0.81f, 0.84f, 0.7f, opacity));
                }
                for (int z = 0; z < Segments; z++)
                for (int x = 0; x < Segments; x++)
                {
                    int a = start + z * (Segments + 1) + x;
                    indices.Add(a); indices.Add(a + Segments + 1); indices.Add(a + 1);
                    indices.Add(a + 1); indices.Add(a + Segments + 1); indices.Add(a + Segments + 2);
                }
            }
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetUVs(1, details); mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
