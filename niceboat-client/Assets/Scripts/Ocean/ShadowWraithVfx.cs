using UnityEngine;

namespace NiceBoat
{
    public sealed class ShadowWraithVfx : MonoBehaviour
    {
        public enum AnimationState { Idle, Move, Hit, Death, Dormant }
        public Transform visualRoot;
        public ShadowWraithCore core;
        public ShadowWraithEyes eyes;
        public ShadowWraithParticles smoke;
        public Renderer groundShadow;
        public OceanSurface ocean;
        [Range(0f, 0.5f)] public float hoverAmount = 0.16f;
        [Min(0.1f)] public float hoverSpeed = 1.1f;
        [Min(0.1f)] public float hitDuration = 0.42f, collapseDuration = 0.85f;
        [Range(0f, 1f)] public float shadowOpacity = 0.28f;
        public AnimationState State { get; private set; } = AnimationState.Dormant;
        public bool DeathComplete => State == AnimationState.Dormant;
        Vector3 lastPosition, velocity;
        float hitAt = -100f, deathAt;
        MaterialPropertyBlock shadowProperties;
        static readonly int Tint = Shader.PropertyToID("_Color");

        public void ResetForSpawn()
        {
            if (ocean == null) ocean = FindFirstObjectByType<OceanSurface>();
            visualRoot.gameObject.SetActive(true); groundShadow.gameObject.SetActive(true);
            lastPosition = transform.position; velocity = Vector3.zero; hitAt = -100f;
            State = AnimationState.Idle;
            smoke.ResetForSpawn(); Animate(0f, 0f);
        }

        public void PlayHit(Vector3 point)
        {
            if (State == AnimationState.Death || State == AnimationState.Dormant) return;
            hitAt = Time.time; State = AnimationState.Hit; smoke.Scatter(point, false);
        }

        public void PlayDeath()
        {
            deathAt = Time.time; State = AnimationState.Death;
            smoke.Scatter(transform.position + Vector3.up * 1.7f, true);
        }

        void LateUpdate()
        {
            if (State == AnimationState.Dormant) return;
            var delta = (transform.position - lastPosition) / Mathf.Max(0.001f, Time.deltaTime);
            lastPosition = transform.position; delta.y = 0f;
            velocity = Vector3.Lerp(velocity, Vector3.ClampMagnitude(delta, 8f), 1f - Mathf.Exp(-Time.deltaTime * 6f));
            float hit = Mathf.Clamp01(1f - (Time.time - hitAt) / hitDuration);
            float death = State == AnimationState.Death ? Mathf.Clamp01((Time.time - deathAt) / collapseDuration) : 0f;
            if (State != AnimationState.Death)
            {
                State = hit > 0f ? AnimationState.Hit : velocity.sqrMagnitude > 0.08f ? AnimationState.Move : AnimationState.Idle;
                smoke.Flow(velocity);
            }
            Animate(hit, death);
            if (State == AnimationState.Death && death >= 1f && !smoke.IsAlive) Suspend();
        }

        void Animate(float hit, float death)
        {
            float hover = Mathf.Sin(Time.time * hoverSpeed) * hoverAmount;
            visualRoot.localPosition = Vector3.up * hover;
            core.Animate(Time.time, transform.InverseTransformDirection(velocity), hit, death);
            eyes.Animate(Time.time, hit, death);
            var position = transform.position;
            position.y = (ocean != null ? ocean.Height(position, Time.time) : position.y) + 0.08f;
            groundShadow.transform.position = position;
            groundShadow.transform.localScale = Vector3.one * (3.5f - hover * 0.6f);
            if (shadowProperties == null) shadowProperties = new MaterialPropertyBlock();
            shadowProperties.SetColor(Tint, new Color(0.01f, 0.02f, 0.025f, shadowOpacity * (1f - hover) * (1f - death)));
            groundShadow.SetPropertyBlock(shadowProperties);
        }

        public void Suspend()
        {
            State = AnimationState.Dormant; smoke.Clear();
            visualRoot.gameObject.SetActive(false); groundShadow.gameObject.SetActive(false);
        }
        void OnDisable() { if (smoke != null && visualRoot != null && groundShadow != null) Suspend(); }
    }
}
