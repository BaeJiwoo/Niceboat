using UnityEngine;

namespace NiceBoat
{
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 point);
    }
}
