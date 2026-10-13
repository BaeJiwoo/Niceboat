using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace NiceBoat
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class HarborDummyEnemy : MonoBehaviour, IDamageable
    {
        public enum PatrolMode { Circle, Line }
        public BoatController player;
        public OceanSurface ocean;
        public WaterEffects waterEffects;
        public Transform model;
        public Renderer[] skin;
        public Canvas healthCanvas;
        public Image healthFill, damageTrail;
        public Text healthText, damageText;
        public Material burstMaterial;
        [Min(1f)] public float maxHealth = 100f;
        [Min(0.5f)] public float respawnDelay = 3f;
        public PatrolMode patrolMode = PatrolMode.Circle;
        [Min(0.5f)] public float patrolRadius = 3f;
        [Min(0f)] public float patrolSpeed = 1.4f;
        [Min(6f)] public float spawnDistance = 14f;

        public float Health { get; private set; }
        public bool IsAlive { get; private set; }
        public int DeathCount { get; private set; }
        public int SpawnCount { get; private set; }
        public Vector3 PatrolCenter => anchor;
        public float HealthFraction => Mathf.Clamp01(Health / Mathf.Max(1f, maxHealth));
        public float RespawnRemaining => IsAlive ? 0f : Mathf.Max(0f, respawnAt - Time.time);
        public const int BurstCapacity = 96;
        const float Clearance = 1.75f;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly float[] SpawnAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };
        readonly Collider[] overlaps = new Collider[48];
        Rigidbody body;
        CapsuleCollider hitbox;
        ParticleSystem burst;
        HarborAudio audio;
        Camera view;
        MaterialPropertyBlock tint;
        Color[] skinColors;
        Vector3 anchor, axis = Vector3.right;
        float phase, direction = 1f, blockedUntil, nextWake, respawnAt, diedAt, hitAt = -10f, ghostHealth = 1f;
        bool flashing;

        void Awake()
        {
            body = GetComponent<Rigidbody>(); hitbox = GetComponent<CapsuleCollider>();
            body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (player == null) player = FindFirstObjectByType<BoatController>();
            if (ocean == null) ocean = FindFirstObjectByType<OceanSurface>();
            if (waterEffects == null) waterEffects = FindFirstObjectByType<WaterEffects>();
            if (player != null) audio = player.GetComponent<HarborAudio>();
            view = Camera.main;
            tint = new MaterialPropertyBlock();
            skinColors = new Color[skin.Length];
            for (int i = 0; i < skin.Length; i++) skinColors[i] = skin[i].sharedMaterial.GetColor(BaseColor);
            CreateBurst();
            SetVisible(false);
        }

        void Start() { TryRespawn(); }

        void Update()
        {
            if (!IsAlive)
            {
                if (healthCanvas != null && Time.time - diedAt > 0.3f) healthCanvas.enabled = false;
                if (Time.time >= respawnAt && !TryRespawn()) respawnAt = Time.time + 0.5f;
            }
            if (flashing && Time.time - hitAt > 0.12f) SetFlash(false);
            if (Time.time - hitAt > 0.35f) ghostHealth = Mathf.MoveTowards(ghostHealth, HealthFraction, Time.deltaTime * 0.8f);
            if (damageTrail != null) damageTrail.fillAmount = ghostHealth;
            if (damageText != null)
            {
                float age = Time.time - hitAt;
                damageText.enabled = age >= 0f && age < 0.8f;
                damageText.rectTransform.anchoredPosition = new Vector2(0f, 55f + age * 26f);
                damageText.color = new Color(1f, 0.79f, 0.55f, Mathf.Clamp01(1f - age / 0.8f));
            }
        }

        void FixedUpdate()
        {
            if (!IsAlive || ocean == null) return;
            float nextPhase = phase;
            if (Time.time >= blockedUntil) nextPhase += direction * patrolSpeed / Mathf.Max(0.5f, patrolRadius) * Time.fixedDeltaTime;
            var position = PatrolPosition(anchor, axis, nextPhase);
            position.y = ocean.Height(position, Time.fixedTime);
            if (ClearAt(position)) phase = nextPhase;
            else
            {
                direction = -direction; blockedUntil = Time.time + 0.5f;
                position = body.position;
                position.y = ocean.Height(position, Time.fixedTime);
            }
            body.MovePosition(position);
            // Keep the red eyes readable from the player's boat while patrolling sideways.
            var facing = player != null ? player.transform.position - position : position - body.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(facing), 120f * Time.fixedDeltaTime));
            if (waterEffects != null && Time.time >= nextWake)
            {
                waterEffects.EmitRipple(position, transform.forward, 0.22f, false);
                nextWake = Time.time + 0.6f;
            }
        }

        void LateUpdate()
        {
            if (view == null) view = Camera.main;
            if (healthCanvas == null || view == null) return;
            healthCanvas.transform.rotation = view.transform.rotation;
            // Preserve a readable bar across the follow camera's zoom range.
            float distance = Vector3.Distance(view.transform.position, healthCanvas.transform.position);
            healthCanvas.transform.localScale = Vector3.one * Mathf.Clamp(distance * 0.00034f, 0.008f, 0.024f);
        }

        public void TakeDamage(float amount, Vector3 point)
        {
            if (!isActiveAndEnabled || !IsAlive || !(amount > 0f)) return;
            float actual = Mathf.Min(amount, Health);
            Health = Mathf.Max(0f, Health - actual);
            hitAt = Time.time; SetFlash(true);
            if (damageText != null) damageText.text = "-" + actual.ToString("0");
            UpdateHealth(); EmitBurst(point, false);
            if (Health <= 0f) Die();
        }

        void Die()
        {
            IsAlive = false; DeathCount++; diedAt = Time.time;
            respawnAt = Time.time + Mathf.Max(0.5f, respawnDelay);
            SetVisible(false);
            // Briefly retain the empty health bar so the killing hit is legible.
            if (healthCanvas != null) healthCanvas.enabled = true;
            EmitBurst(body.position + Vector3.up, true);
            if (waterEffects != null)
            {
                waterEffects.EmitRipple(body.position, Vector3.forward, 1f, false);
                waterEffects.EmitSpray(body.position, Vector3.up * 2f, 16);
                if (waterEffects.interactionField != null)
                    waterEffects.interactionField.DepositSegment(body.position, body.position, 2f, 0.8f, 1f);
            }
            if (audio != null) audio.PlayEnemyBurst(body.position);
        }

        public bool TryRespawn()
        {
            if (IsAlive || player == null || ocean == null || body == null) return false;
            var forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
            // A bounded search validates the entire patrol, including space below the surface.
            for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < SpawnAngles.Length; i++)
            {
                float distance = Mathf.Max(8f, spawnDistance) + ring * 4f;
                float angle = SpawnAngles[(i + SpawnCount % 3) % SpawnAngles.Length];
                var bearing = Quaternion.Euler(0f, angle, 0f) * forward;
                var spawn = player.transform.position + bearing * distance;
                var candidateAxis = Vector3.Cross(Vector3.up, bearing);
                var center = patrolMode == PatrolMode.Circle ? spawn - candidateAxis * patrolRadius : spawn;
                if (!ClearRoute(center, candidateAxis)) continue;
                anchor = center; axis = candidateAxis; phase = 0f; direction = 1f; blockedUntil = 0f;
                spawn = PatrolPosition(anchor, axis, phase);
                spawn.y = ocean.Height(spawn, Time.fixedTime);
                body.position = spawn; body.rotation = Quaternion.LookRotation(-bearing);
                // Teleport the interpolated pose too; never interpolate a respawn across the harbor.
                transform.SetPositionAndRotation(spawn, body.rotation);
                Health = Mathf.Max(1f, maxHealth); ghostHealth = 1f; hitAt = -10f;
                SetFlash(false); IsAlive = true; SpawnCount++;
                SetVisible(true); UpdateHealth();
                if (damageText != null) damageText.enabled = false;
                if (damageTrail != null) damageTrail.fillAmount = 1f;
                return true;
            }
            return false;
        }

        Vector3 PatrolPosition(Vector3 center, Vector3 right, float angle)
        {
            float radius = Mathf.Max(0.5f, patrolRadius);
            if (patrolMode == PatrolMode.Line) return center + right * (Mathf.Sin(angle) * radius);
            return center + (right * Mathf.Cos(angle) + Vector3.Cross(right, Vector3.up) * Mathf.Sin(angle)) * radius;
        }

        bool ClearRoute(Vector3 center, Vector3 right)
        {
            for (int i = 0; i < 16; i++)
            {
                var point = PatrolPosition(center, right, i * Mathf.PI * 2f / 16f);
                point.y = ocean.Height(point, Time.fixedTime);
                if (!ClearAt(point)) return false;
            }
            return true;
        }

        bool ClearAt(Vector3 point)
        {
            float boundary = Mathf.Max(30f, player.navigationRadius - 3f);
            if (new Vector2(point.x, point.z).sqrMagnitude > boundary * boundary) return false;
            int count = Physics.OverlapCapsuleNonAlloc(point + Vector3.up * 0.6f, point + Vector3.up * 1.9f,
                Clearance, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (!overlaps[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        void SetVisible(bool visible)
        {
            if (model != null) model.gameObject.SetActive(visible);
            if (hitbox != null) hitbox.enabled = visible;
            if (healthCanvas != null) healthCanvas.enabled = visible;
        }

        void UpdateHealth()
        {
            if (healthFill != null) healthFill.fillAmount = HealthFraction;
            if (healthText != null) healthText.text = "DUMMY  " + Health.ToString("0") + " / " + maxHealth.ToString("0");
        }

        void SetFlash(bool active)
        {
            flashing = active;
            for (int i = 0; i < skin.Length; i++)
            {
                tint.SetColor(BaseColor, active ? new Color(0.6f, 0.19f, 0.16f) : skinColors[i]);
                skin[i].SetPropertyBlock(tint);
            }
        }

        void CreateBurst()
        {
            var go = new GameObject("Pooled monster bursts"); go.transform.SetParent(transform, false);
            burst = go.AddComponent<ParticleSystem>();
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = burst.main;
            main.playOnAwake = false; main.loop = true; main.maxParticles = BurstCapacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.6f;
            var emission = burst.emission; emission.enabled = false;
            var shape = burst.shape; shape.enabled = false;
            var fade = burst.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = burst.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = burstMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off;
            burst.Play();
        }

        void EmitBurst(Vector3 point, bool explosion)
        {
            int count = explosion ? 36 : 5;
            for (int i = 0; i < count; i++)
            {
                bool ember = i < (explosion ? 10 : 5);
                var emit = new ParticleSystem.EmitParams {
                    position = point + Random.insideUnitSphere * (explosion ? 0.7f : 0.08f),
                    velocity = Random.insideUnitSphere * (explosion ? 3.8f : 1.4f) + Vector3.up * 2f,
                    startLifetime = explosion ? Random.Range(0.55f, 1.3f) : 0.25f,
                    startSize = ember ? Random.Range(0.12f, 0.28f) : Random.Range(0.5f, 1.1f),
                    startColor = ember ? new Color(1f, 0.12f, 0.025f) : new Color(0.055f, 0.065f, 0.06f, 0.8f) };
                burst.Emit(emit, 1);
            }
            if (explosion) burst.Emit(new ParticleSystem.EmitParams {
                position = point, startLifetime = 0.14f, startSize = 2.6f,
                startColor = new Color(1f, 0.3f, 0.06f), velocity = Vector3.zero }, 1);
        }

        void OnDisable()
        {
            SetVisible(false);
            if (burst != null) burst.Clear();
        }

        void OnEnable() { if (body != null && IsAlive) SetVisible(true); }
    }
}
