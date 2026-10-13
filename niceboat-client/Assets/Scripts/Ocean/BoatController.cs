using UnityEngine;
using UnityEngine.InputSystem;

namespace NiceBoat
{
    [RequireComponent(typeof(BuoyantBody))]
    public sealed class BoatController : MonoBehaviour
    {
        public float acceleration = 4.2f;
        public float topSpeed = 9f;
        public float reverseSpeed = 3f;
        public float steeringAcceleration = 1.8f;
        public float lateralResistance = 2.2f;
        public float navigationRadius = 95f;
        public Transform propeller;
        public Vector2 Control { get; private set; }
        public bool Braking { get; private set; }
        public float Speed => floating != null && floating.Body != null
            ? Vector3.ProjectOnPlane(floating.Body.linearVelocity, Vector3.up).magnitude : 0f;
        public BuoyantBody Floating => floating;
        BuoyantBody floating;
        Vector3 spawn;
        Quaternion spawnRotation;

        void Awake()
        {
            floating = GetComponent<BuoyantBody>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
        }

        void Update()
        {
            Vector2 control = Vector2.zero;
            bool brake = false;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                control.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                          - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                control.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                          - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
                brake = keyboard.spaceKey.isPressed;
                if (keyboard.rKey.wasPressedThisFrame) ResetBoat();
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > control.sqrMagnitude) control = stick;
                brake |= pad.buttonSouth.isPressed;
                if (pad.buttonNorth.wasPressedThisFrame) ResetBoat();
            }
            SetControlInput(control, brake);
            if (propeller != null) propeller.Rotate(Vector3.forward, control.y * 1500f * Time.deltaTime, Space.Self);
        }

        public void SetControlInput(Vector2 input, bool brake = false)
        {
            Control = Vector2.ClampMagnitude(input, 1f);
            Braking = brake;
        }

        void FixedUpdate()
        {
            ApplyDrive();
            if (transform.position.y < -8f || new Vector2(transform.position.x, transform.position.z).magnitude > navigationRadius)
                ResetBoat();
        }

        public void ApplyDrive()
        {
            if (floating == null) Awake();
            var body = floating.Body;
            if (body == null || floating.WetFraction < 0.1f) return;
            var forward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            float forwardSpeed = Vector3.Dot(body.linearVelocity, forward);
            float limit = Control.y < 0f ? reverseSpeed : topSpeed;
            float thrustScale = Mathf.Clamp01(1f - Mathf.Max(0f, forwardSpeed * Mathf.Sign(Control.y)) / limit);
            body.AddForce(forward * (Control.y * acceleration * thrustScale), ForceMode.Acceleration);
            float rudder = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 2f) * Mathf.Sign(forwardSpeed);
            body.AddTorque(Vector3.up * (Control.x * steeringAcceleration * rudder), ForceMode.Acceleration);
            body.AddForce(-right * (Vector3.Dot(body.linearVelocity, right) * lateralResistance), ForceMode.Acceleration);
            if (Braking) body.AddForce(-Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up) * 2.8f, ForceMode.Acceleration);
        }

        public void ResetBoat()
        {
            if (floating == null || floating.Body == null) return;
            floating.Body.position = spawn;
            floating.Body.rotation = spawnRotation;
            floating.Body.linearVelocity = Vector3.zero;
            floating.Body.angularVelocity = Vector3.zero;
            SetControlInput(Vector2.zero);
        }
    }
}
