using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NiceBoat
{
    // A bounded visual history shared by all boats/buoys. No GPU readback at runtime.
    [DefaultExecutionOrder(100)]
    public sealed class WaterInteractionField : MonoBehaviour
    {
        public Material updateMaterial;
        public Transform followTarget;
        [Range(128, 512)] public int resolution = 256;
        [Min(16f)] public float worldSize = 64f;
        [Range(10f, 60f)] public float updatesPerSecond = 30f;
        [Min(0.1f)] public float foamLifetime = 5f;
        [Min(0.1f)] public float disturbanceLifetime = 2.4f;
        public Vector2 drift = new(0.025f, 0.015f);
        public const int StampCapacity = 256;

        static readonly int Map = Shader.PropertyToID("_NiceBoatInteractionMap");
        static readonly int Bounds = Shader.PropertyToID("_NiceBoatInteractionBounds");
        static readonly int Enabled = Shader.PropertyToID("_NiceBoatInteractionEnabled");
        static readonly int FieldBounds = Shader.PropertyToID("_FieldBounds");
        static readonly int PreviousOffset = Shader.PropertyToID("_PreviousOffset");
        static readonly int Evolution = Shader.PropertyToID("_Evolution");
        static readonly int History = Shader.PropertyToID("_MainTex");
        readonly List<Vector3> vertices = new(StampCapacity * 4);
        readonly List<Vector2> uvs = new(StampCapacity * 4);
        readonly List<Vector2> capsules = new(StampCapacity * 4);
        readonly List<Color> colors = new(StampCapacity * 4);
        readonly List<int> indices = new(StampCapacity * 6);
        RenderTexture current, next;
        Material material;
        Mesh stamps;
        CommandBuffer commands;
        Vector2 center;
        float elapsed;
        int allocatedResolution;
        float allocatedSize;

        public bool IsReady => isActiveAndEnabled && current != null && current.IsCreated()
                               && next != null && next.IsCreated();
        public RenderTexture Texture => current;
        public Vector4 WorldBounds => new(center.x - allocatedSize * 0.5f, center.y - allocatedSize * 0.5f,
                                          1f / allocatedSize, 1f / allocatedSize);
        public int PendingStampCount => vertices.Count / 4;
        public int UpdateCount { get; private set; }

        void OnEnable()
        {
            if (!Application.isPlaying || updateMaterial == null || !updateMaterial.shader.isSupported) return;
            allocatedResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 128, 512);
            allocatedSize = Mathf.Max(16f, worldSize);
            material = new Material(updateMaterial) { name = "Water field runtime", hideFlags = HideFlags.DontSave };
            current = CreateTexture("Water interaction history A");
            next = CreateTexture("Water interaction history B");
            if (!current.IsCreated() || !next.IsCreated()) { Release(); return; }
            stamps = new Mesh { name = "Batched water trail stamps" };
            stamps.MarkDynamic();
            commands = new CommandBuffer { name = "NiceBoat water interaction" };
            center = TargetCenter();
            var previous = RenderTexture.active;
            foreach (var target in new[] { current, next })
            {
                RenderTexture.active = target;
                GL.Clear(false, true, new Color(0f, 0f, 0.5f, 0.5f));
            }
            RenderTexture.active = previous;
            Publish();
        }

        RenderTexture CreateTexture(string name)
        {
            var target = new RenderTexture(allocatedResolution, allocatedResolution, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false, antiAliasing = 1, hideFlags = HideFlags.DontSave
            };
            target.Create();
            return target;
        }

        Vector2 TargetCenter()
        {
            Vector3 point = followTarget != null ? followTarget.position : transform.position;
            float cell = allocatedSize / allocatedResolution;
            return new Vector2(Mathf.Round(point.x / cell) * cell, Mathf.Round(point.z / cell) * cell);
        }

        // Capsules connect successive stern positions, including curved/reversed travel.
        public void DepositSegment(Vector3 start, Vector3 end, float radius, float foam, float disturbance)
        {
            if (!IsReady || PendingStampCount >= StampCapacity) return;
            start.y = end.y = 0f;
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length > allocatedSize * 0.5f) return; // Teleports must not paint across the map.
            radius = Mathf.Max(radius, allocatedSize / allocatedResolution);
            Vector3 forward = length > 0.001f ? delta / length : Vector3.forward;
            Vector3 right = new(forward.z, 0f, -forward.x);
            Vector3 middle = (start + end) * 0.5f;
            float half = length * 0.5f, extent = half + radius;
            int first = vertices.Count;
            for (int corner = 0; corner < 4; corner++)
            {
                float x = (corner % 2 == 0 ? -1f : 1f) * radius;
                float y = (corner < 2 ? -1f : 1f) * extent;
                vertices.Add(middle + right * x + forward * y);
                uvs.Add(new Vector2(x, y));
                capsules.Add(new Vector2(half, 1f / radius));
                colors.Add(new Color(Mathf.Clamp01(foam), Mathf.Clamp01(disturbance), 0, 0));
            }
            indices.Add(first); indices.Add(first + 2); indices.Add(first + 1);
            indices.Add(first + 1); indices.Add(first + 2); indices.Add(first + 3);
        }

        void LateUpdate()
        {
            // Unity can invalidate temporary GPU resources on entering Play mode or
            // restoring a graphics device. Recreate and clear both buffers before use.
            if (current != null && (!current.IsCreated() || next == null || !next.IsCreated()))
            {
                Release();
                OnEnable();
            }
            if (!IsReady) return;
            elapsed += Time.deltaTime;
            if (elapsed < 1f / Mathf.Max(10f, updatesPerSecond)) return;
            float dt = elapsed;
            elapsed = 0f;
            Vector2 newCenter = TargetCenter();
            Vector2 offset = (newCenter - center - drift * dt) / allocatedSize;
            center = newCenter;
            material.SetVector(FieldBounds, WorldBounds);
            material.SetVector(PreviousOffset, new Vector4(offset.x, offset.y,
                Mathf.Max(1f / 255f, dt * 0.35f / Mathf.Max(0.1f, foamLifetime)),
                Mathf.Max(1f / 255f, dt * 0.35f / Mathf.Max(0.1f, disturbanceLifetime))));
            material.SetVector(Evolution, new Vector4(Mathf.Exp(-dt / Mathf.Max(0.1f, foamLifetime)),
                Mathf.Exp(-dt / Mathf.Max(0.1f, disturbanceLifetime)), 1f - Mathf.Exp(-dt * 5f),
                2f * allocatedResolution / allocatedSize));
            material.SetTexture(History, current);

            commands.Clear();
            commands.Blit(current, next, material, 0);
            if (vertices.Count > 0)
            {
                stamps.Clear();
                stamps.SetVertices(vertices); stamps.SetUVs(0, uvs); stamps.SetUVs(1, capsules);
                stamps.SetColors(colors); stamps.SetTriangles(indices, 0);
                // Retain advected history while blending all new stamps in one draw.
                commands.SetRenderTarget(next, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
                commands.DrawMesh(stamps, Matrix4x4.identity, material, 0, 1);
            }
            Graphics.ExecuteCommandBuffer(commands);
            (current, next) = (next, current);
            ClearStamps();
            UpdateCount++;
            Publish();
        }

        void Publish()
        {
            Shader.SetGlobalTexture(Map, current);
            Shader.SetGlobalVector(Bounds, WorldBounds);
            Shader.SetGlobalFloat(Enabled, 1f);
        }

        void ClearStamps()
        {
            vertices.Clear(); uvs.Clear(); capsules.Clear(); colors.Clear(); indices.Clear();
        }

        void OnDisable() => Release();

        void Release()
        {
            Shader.SetGlobalFloat(Enabled, 0f);
            Shader.SetGlobalTexture(Map, Texture2D.blackTexture);
            Shader.SetGlobalVector(Bounds, Vector4.zero);
            commands?.Release(); commands = null;
            if (current != null) { current.Release(); Destroy(current); }
            if (next != null) { next.Release(); Destroy(next); }
            if (material != null) Destroy(material);
            if (stamps != null) Destroy(stamps);
            current = next = null; material = null; stamps = null;
            elapsed = 0f; ClearStamps();
        }
    }
}
