using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace NiceBoat
{
    [RequireComponent(typeof(BoatController))]
    public sealed class BoatCannon : MonoBehaviour
    {
        public Transform barrel, muzzle;
        public Mesh projectileMesh;
        public Material projectileMaterial, effectMaterial;
        public WaterEffects waterEffects;
        [Min(0.1f)] public float reloadSeconds = 0.85f;
        [Min(1f)] public float muzzleSpeed = 26f;
        [Range(0f, 30f)] public float elevation = 8f;
        [Min(0f)] public float hitImpulse = 55f;
        public LayerMask hitLayers = Physics.DefaultRaycastLayers;
        public const int Capacity = 8;
        const float Radius = 0.14f, Lifetime = 4f;

        sealed class Shot
        {
            public Transform visual;
            public TrailRenderer trail;
            public Vector3 position, velocity;
            public float remaining;
        }

        readonly Shot[] shots = new Shot[Capacity];
        readonly RaycastHit[] hits = new RaycastHit[64];
        BoatController boat;
        Transform pool;
        ParticleSystem particles;
        Vector3 barrelRest;
        float nextFire, kick;
        public int ShotsFired { get; private set; }
        public int WaterHits { get; private set; }
        public int SolidHits { get; private set; }
        public int ActiveShots { get; private set; }
        public Vector3 LastLaunchVelocity { get; private set; }
        public float ReloadProgress => Mathf.Clamp01(1f - (nextFire - Time.time) / Mathf.Max(0.1f, reloadSeconds));
        public event Action<Vector3> Fired;
        public event Action<Vector3, bool> Impacted;

        // Stabilize aim against buoyancy roll/pitch, while always following the bow heading.
        public Vector3 LaunchDirection
        {
            get
            {
                var body = boat != null && boat.Floating != null ? boat.Floating.Body : null;
                var forward = Vector3.ProjectOnPlane((body != null ? body.rotation : transform.rotation) * Vector3.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                float angle = elevation * Mathf.Deg2Rad;
                return forward * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
            }
        }

        void Awake()
        {
            boat = GetComponent<BoatController>();
            if (waterEffects == null) waterEffects = FindFirstObjectByType<WaterEffects>();
            if (barrel != null) barrelRest = barrel.localPosition;
            pool = new GameObject("Cannon projectile pool").transform;
            for (int i = 0; i < Capacity; i++)
            {
                var visual = new GameObject("Cannonball " + i);
                visual.transform.SetParent(pool, false);
                visual.transform.localScale = Vector3.one * Radius;
                visual.AddComponent<MeshFilter>().sharedMesh = projectileMesh;
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = projectileMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var trail = visual.AddComponent<TrailRenderer>();
                trail.sharedMaterial = effectMaterial;
                trail.time = 0.16f; trail.minVertexDistance = 0.12f;
                trail.startWidth = 0.11f; trail.endWidth = 0.015f;
                trail.startColor = new Color(0.92f, 0.81f, 0.55f, 0.75f);
                trail.endColor = new Color(0.7f, 0.73f, 0.65f, 0f);
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.emitting = false;
                shots[i] = new Shot { visual = visual.transform, trail = trail };
                visual.SetActive(false);
            }
            var puff = new GameObject("Cannon flash and impact pool");
            puff.transform.SetParent(pool, false);
            particles = puff.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = true; main.maxParticles = 96;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = effectMaterial;
            particles.Play();
        }

        void Update()
        {
            bool fire = Keyboard.current != null && Keyboard.current.fKey.isPressed;
            fire |= Mouse.current != null && Mouse.current.leftButton.isPressed;
            fire |= Gamepad.current != null && Gamepad.current.rightTrigger.isPressed;
            if (fire) TryFire();
        }

        public bool TryFire()
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f || Time.time < nextFire || muzzle == null || pool == null) return false;
            Shot available = null;
            foreach (var shot in shots) if (shot.remaining <= 0f) { available = shot; break; }
            if (available == null) return false;
            var direction = LaunchDirection;
            if (barrel != null) barrel.rotation = Quaternion.LookRotation(direction, Vector3.up);
            var point = muzzle.position;
            available.position = point;
            // A fixed launch speed keeps the range predictable while turning or reversing.
            available.velocity = direction * muzzleSpeed;
            available.remaining = Lifetime;
            available.visual.position = point;
            available.visual.gameObject.SetActive(true);
            available.trail.Clear(); available.trail.emitting = true;
            nextFire = Time.time + Mathf.Max(0.1f, reloadSeconds);
            LastLaunchVelocity = available.velocity;
            ShotsFired++; ActiveShots++; kick = 1f;
            if (boat.Floating.Body != null && !boat.Floating.Body.isKinematic)
                boat.Floating.Body.AddForce(-Vector3.ProjectOnPlane(direction, Vector3.up) * 24f, ForceMode.Impulse);
            Puff(point, direction, true);
            Fired?.Invoke(point);
            return true;
        }

        void FixedUpdate()
        {
            var ocean = boat.Floating.ocean;
            float dt = Time.fixedDeltaTime;
            foreach (var shot in shots)
            {
                if (shot.remaining <= 0f) continue;
                Vector3 from = shot.position;
                Vector3 to = from + shot.velocity * dt + Physics.gravity * (0.5f * dt * dt);
                Vector3 travel = to - from;
                float distance = travel.magnitude, solidFraction = 2f;
                RaycastHit nearest = default;
                int count = Physics.SphereCastNonAlloc(from, Radius, travel.normalized, hits, distance,
                    hitLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].collider.transform.IsChildOf(transform)) continue;
                    float fraction = hits[i].distance / Mathf.Max(distance, 0.0001f);
                    if (fraction < solidFraction) { solidFraction = fraction; nearest = hits[i]; }
                }
                float waterFraction = 2f;
                if (ocean != null && to.y - Radius <= ocean.Height(to, Time.fixedTime))
                {
                    float low = 0f, high = 1f;
                    for (int i = 0; i < 8; i++)
                    {
                        float middle = (low + high) * 0.5f;
                        var p = Vector3.Lerp(from, to, middle);
                        if (p.y - Radius > ocean.Height(p, Time.fixedTime)) low = middle;
                        else high = middle;
                    }
                    waterFraction = high;
                }
                if (waterFraction <= 1f && waterFraction < solidFraction)
                {
                    var point = Vector3.Lerp(from, to, waterFraction);
                    point.y = ocean.Height(point, Time.fixedTime);
                    if (waterEffects != null)
                    {
                        waterEffects.EmitRipple(point, shot.velocity, 1f, false);
                        waterEffects.EmitSpray(point, Vector3.up * 2.5f, 16);
                        if (waterEffects.interactionField != null)
                            waterEffects.interactionField.DepositSegment(point, point, 1.5f, 0.8f, 1f);
                    }
                    WaterHits++; Impacted?.Invoke(point, true); Retire(shot);
                }
                else if (solidFraction <= 1f)
                {
                    if (nearest.rigidbody != null && !nearest.rigidbody.isKinematic)
                        nearest.rigidbody.AddForceAtPosition(shot.velocity.normalized * hitImpulse, nearest.point, ForceMode.Impulse);
                    Puff(nearest.point, nearest.normal, false);
                    SolidHits++; Impacted?.Invoke(nearest.point, false); Retire(shot);
                }
                else
                {
                    shot.position = to;
                    shot.velocity += Physics.gravity * dt;
                    shot.visual.position = to;
                    shot.remaining -= dt;
                    if (shot.remaining <= 0f) Retire(shot);
                }
            }
        }

        void LateUpdate()
        {
            if (barrel == null) return;
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 5f);
            barrel.localPosition = barrelRest - Vector3.forward * (kick * 0.16f);
            barrel.rotation = Quaternion.LookRotation(LaunchDirection, Vector3.up);
        }

        void Puff(Vector3 point, Vector3 direction, bool flash)
        {
            for (int i = 0; i < 12; i++)
            {
                bool spark = i < 3;
                var emit = new ParticleSystem.EmitParams {
                    position = point, velocity = direction * (spark ? 3f : 0.7f) + UnityEngine.Random.insideUnitSphere * 0.7f,
                    startLifetime = spark ? 0.09f : 0.45f,
                    startSize = spark ? 0.38f : UnityEngine.Random.Range(0.18f, 0.4f),
                    startColor = flash && spark ? new Color(1f, 0.79f, 0.36f) : new Color(0.65f, 0.62f, 0.51f, 0.5f) };
                particles.Emit(emit, 1);
            }
        }

        void Retire(Shot shot)
        {
            shot.remaining = 0f;
            if (shot.trail != null) { shot.trail.emitting = false; shot.trail.Clear(); }
            if (shot.visual != null) shot.visual.gameObject.SetActive(false);
            ActiveShots--;
        }

        void OnDisable()
        {
            foreach (var shot in shots) if (shot != null && shot.remaining > 0f) Retire(shot);
            if (particles != null) particles.Clear();
        }

        void OnDestroy() { if (pool != null) Destroy(pool.gameObject); }
    }
}
