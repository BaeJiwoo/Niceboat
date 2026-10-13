using UnityEngine;
using UnityEngine.InputSystem;

namespace NiceBoat
{
    public sealed class BoatFollowCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 35f;
        public float height = 36f;
        public float yawOffset = -12f;
        public float lookAhead = 11f;
        public float followSharpness = 3f;
        Vector3 velocity;
        float yaw;
        float yawVelocity;
        bool initialized;

        void LateUpdate()
        {
            if (target == null) return;
            if (!initialized) { yaw = target.eulerAngles.y; initialized = true; }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                yawOffset += ((keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f)) * 65f * Time.deltaTime;
            }
            if (Mouse.current != null) distance = Mathf.Clamp(distance - Mouse.current.scroll.ReadValue().y * 0.025f, 20f, 44f);
            yaw = Mathf.SmoothDampAngle(yaw, target.eulerAngles.y, ref yawVelocity, 1.4f);
            float zoom = distance / 35f;
            var heading = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var lookAt = target.position + Vector3.up * 0.5f + heading * (lookAhead * zoom);
            var offset = Quaternion.Euler(0f, yaw + yawOffset, 0f) * new Vector3(0f, height * zoom, -distance);
            var desired = lookAt + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 1f / followSharpness);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookAt - transform.position),
                1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        }
    }
}
