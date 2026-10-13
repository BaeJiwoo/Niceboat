using UnityEngine;

namespace NiceBoat
{
    public sealed class ShadowWraithEyes : MonoBehaviour
    {
        public Renderer[] eyes, halos;
        [ColorUsage(false, true)] public Color color = new Color(1f, 0.012f, 0.003f);
        [Range(0f, 12f)] public float brightness = 3f;
        [Range(0f, 1f)] public float haloStrength = 0.18f;
        [Range(0.3f, 2f)] public float eyeSize = 1f;
        MaterialPropertyBlock properties;
        static readonly int Tint = Shader.PropertyToID("_Color"), Intensity = Shader.PropertyToID("_Intensity");

        public void Animate(float time, float hit, float death)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            transform.localScale = Vector3.one * eyeSize;
            float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 1.15f)), 12f);
            float intensity = brightness * (1f + pulse * 0.35f + hit * 2f) * (1f - death);
            properties.SetColor(Tint, color);
            properties.SetFloat(Intensity, intensity);
            foreach (var eye in eyes) eye.SetPropertyBlock(properties);
            properties.SetFloat(Intensity, intensity * haloStrength);
            foreach (var halo in halos) halo.SetPropertyBlock(properties);
        }
    }
}
