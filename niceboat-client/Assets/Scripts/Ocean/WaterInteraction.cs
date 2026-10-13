using UnityEngine;

namespace NiceBoat
{
    [RequireComponent(typeof(BuoyantBody))]
    public sealed class WaterInteraction : MonoBehaviour
    {
        public WaterEffects effects;
        public bool boat;
        public Vector3 stern = new(0f, -0.25f, -2.1f);
        public Vector3 bow = new(0f, -0.25f, 2.3f);
        BuoyantBody floating;
        float nextRipple, nextSpray, nextImpact;
        Vector3 previousTrail;
        bool hasTrail, wasReversing;

        void Awake()
        {
            floating = GetComponent<BuoyantBody>();
            if (effects == null) effects = FindFirstObjectByType<WaterEffects>();
        }

        void Update()
        {
            if (effects == null || floating.Body == null || floating.WetFraction < 0.1f)
            { hasTrail = false; return; }
            float speed = Vector3.ProjectOnPlane(floating.Body.linearVelocity, Vector3.up).magnitude;
            bool reversing = Vector3.Dot(floating.Body.linearVelocity, transform.forward) < -0.1f;
            Vector3 direction = reversing ? -transform.forward : transform.forward;
            if (boat) RecordTrail(speed, reversing);
            if (Time.time >= nextRipple && (speed > 0.35f || !boat))
            {
                float strength = boat ? Mathf.Lerp(0.13f, 0.52f, speed / 7f) : 0.13f;
                // Use travel direction so reversing leaves the wake on the trailing end.
                effects.EmitRipple(boat ? transform.TransformPoint(reversing ? bow : stern) : transform.position, direction, strength, boat);
                if (boat && speed > 1f)
                    effects.EmitBow(transform.TransformPoint(reversing ? stern : bow),direction,strength * 0.85f);
                nextRipple = Time.time + (boat ? 0.24f : 1.65f);
            }
            if (boat && speed > 3f && Time.time >= nextSpray)
            {
                effects.EmitSpray(transform.TransformPoint(reversing ? stern : bow), -direction * speed * 0.16f, 2);
                nextSpray = Time.time + 0.12f;
            }
        }

        void RecordTrail(float speed, bool reversing)
        {
            var field = effects.interactionField;
            if (field == null || !field.IsReady || speed < 0.35f) { hasTrail = false; return; }
            Vector3 point = transform.TransformPoint(reversing ? bow : stern);
            point.y = 0f;
            if (!hasTrail || wasReversing != reversing)
            { previousTrail = point; hasTrail = true; wasReversing = reversing; return; }
            float distance = Vector3.Distance(previousTrail, point);
            if (distance > 4f) { previousTrail = point; return; }
            // Distance spacing keeps coverage consistent at different frame rates/speeds.
            if (distance < 0.28f) return;
            float motion = Mathf.Clamp01(speed / 7f);
            float radius = Mathf.Lerp(0.48f, 0.85f, motion);
            float deposit = Mathf.Min(distance / (radius * 2f), 1f);
            field.DepositSegment(previousTrail, point, radius,
                Mathf.Lerp(0.12f, 0.48f, motion) * deposit, Mathf.Lerp(0.28f, 0.85f, motion) * deposit);
            previousTrail = point;
        }

        void OnDisable() => hasTrail = false;

        void OnCollisionEnter(Collision collision)
        {
            if (effects == null || Time.time < nextImpact || collision.relativeVelocity.magnitude < 0.6f) return;
            Vector3 point = collision.GetContact(0).point;
            point.y = effects.ocean.Height(point, Time.time);
            effects.EmitRipple(point, transform.forward, 1f, false);
            effects.EmitSpray(point, collision.relativeVelocity * 0.15f, 10);
            nextImpact = Time.time + 0.25f;
        }
    }
}
