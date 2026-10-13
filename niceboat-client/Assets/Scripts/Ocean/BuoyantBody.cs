using UnityEngine;

namespace NiceBoat
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BuoyantBody : MonoBehaviour
    {
        public OceanSurface ocean;
        public Vector3[] floatPoints = { new(-0.7f, -0.55f, -1.4f), new(0.7f, -0.55f, -1.4f),
            new(-0.7f, -0.55f, 0f), new(0.7f, -0.55f, 0f),
            new(-0.55f, -0.55f, 1.4f), new(0.55f, -0.55f, 1.4f) };
        [Min(0.05f)] public float draft = 0.3f;
        public float verticalDamping = 3.5f;
        public float waterDrag = 0.22f;
        public float angularWaterDrag = 1.6f;
        public Vector3 centerOfMass = new(0f, -0.3f, 0f);
        public bool moored;
        public float mooringStrength = 0.55f;
        public float WetFraction { get; private set; }
        public Rigidbody Body { get; private set; }
        Vector3 anchor;

        void Awake()
        {
            if (ocean == null) ocean = FindFirstObjectByType<OceanSurface>();
            Body = GetComponent<Rigidbody>();
            Body.centerOfMass = centerOfMass;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.maxAngularVelocity = 3f;
            anchor = Body.position;
        }

        void FixedUpdate() => ApplyBuoyancy(Time.fixedTime);

        public void ApplyBuoyancy(float time)
        {
            if (ocean == null || floatPoints == null || floatPoints.Length == 0) return;
            if (Body == null) Awake();
            float wet = 0f;
            float pointMass = Body.mass / floatPoints.Length;
            foreach (var local in floatPoints)
            {
                // Rigidbody pose, not its interpolated render transform, drives physics.
                var point = Body.position + Body.rotation * Vector3.Scale(local, transform.lossyScale);
                float depth = ocean.Height(point, time) - point.y;
                if (depth <= 0f) continue;
                float submergence = Mathf.Clamp(depth / Mathf.Max(draft, 0.05f), 0f, 3f);
                float relativeY = Body.GetPointVelocity(point).y - ocean.VerticalVelocity(point, time);
                float lift = Mathf.Max(0f, -Physics.gravity.y * submergence - relativeY * verticalDamping);
                Body.AddForceAtPosition(Vector3.up * (lift * pointMass), point, ForceMode.Force);
                wet += Mathf.Clamp01(submergence);
            }
            WetFraction = wet / floatPoints.Length;
            Vector3 horizontal = Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up);
            Body.AddForce(-horizontal * (waterDrag * WetFraction), ForceMode.Acceleration);
            Body.AddTorque(-Body.angularVelocity * (angularWaterDrag * WetFraction), ForceMode.Acceleration);
            if (moored)
            {
                Vector3 offset = Vector3.ProjectOnPlane(Body.position - anchor, Vector3.up);
                Body.AddForce(-offset * mooringStrength, ForceMode.Acceleration);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (floatPoints == null) return;
            Gizmos.color = new Color(0.2f, 0.9f, 1f);
            foreach (var point in floatPoints) Gizmos.DrawWireSphere(transform.TransformPoint(point), 0.09f);
        }
    }
}
