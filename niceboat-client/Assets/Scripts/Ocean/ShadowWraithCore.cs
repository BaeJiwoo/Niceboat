using UnityEngine;

namespace NiceBoat
{
    // Reusable procedural animation; no rig, per-frame allocations or material instances.
    public sealed class ShadowWraithCore : MonoBehaviour
    {
        public Transform[] lobes;
        [Range(0f, 0.3f)] public float breathing = 0.09f;
        [Min(0f)] public float motionSpeed = 0.85f;
        [Range(0f, 1f)] public float scatterDistance = 0.6f;
        [Range(0f, 0.6f)] public float surfaceDissolve = 0.37f;
        Vector3[] positions, scales;
        Quaternion[] rotations;
        Renderer[] renderers;
        MaterialPropertyBlock properties;
        static readonly int Dissolve = Shader.PropertyToID("_Dissolve"), Phase = Shader.PropertyToID("_Phase");

        void Initialize()
        {
            if (properties != null) return;
            properties = new MaterialPropertyBlock();
            positions = new Vector3[lobes.Length]; scales = new Vector3[lobes.Length];
            rotations = new Quaternion[lobes.Length]; renderers = new Renderer[lobes.Length];
            for (int i = 0; i < lobes.Length; i++)
            {
                positions[i] = lobes[i].localPosition; scales[i] = lobes[i].localScale;
                rotations[i] = lobes[i].localRotation; renderers[i] = lobes[i].GetComponent<Renderer>();
            }
        }

        public void Animate(float time, Vector3 localVelocity, float hit, float death)
        {
            Initialize();
            for (int i = 0; i < lobes.Length; i++)
            {
                float t = time * motionSpeed + i * 2.3f;
                var drift = new Vector3(Mathf.Sin(t), Mathf.Cos(t * 0.73f), Mathf.Sin(t * 0.83f));
                lobes[i].localPosition = positions[i] + drift * (0.08f + hit * scatterDistance + death * 0.7f)
                    - localVelocity * (0.035f + i * 0.014f) - Vector3.up * death * 0.55f;
                lobes[i].localScale = scales[i] * (1f + Mathf.Sin(t * 1.4f) * breathing + hit * 0.16f) * Mathf.Max(0.015f, 1f - death);
                lobes[i].localRotation = rotations[i] * Quaternion.Euler(Mathf.Sin(t) * 7f, Mathf.Sin(t * 0.6f) * 12f, Mathf.Cos(t) * 6f);
                properties.SetFloat(Dissolve, surfaceDissolve + Mathf.Sin(t * 0.8f) * 0.025f + death * 1.1f + hit * 0.2f);
                properties.SetFloat(Phase, i * 1.73f);
                renderers[i].SetPropertyBlock(properties);
            }
        }
    }
}
