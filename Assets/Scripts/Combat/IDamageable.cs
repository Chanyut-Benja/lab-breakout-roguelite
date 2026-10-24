using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float amount);
    void TakeDamage(float amount, Vector2 knockback);
}
