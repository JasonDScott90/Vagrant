using System;
using UnityEngine;

/// Generic health. Put it on anything that can be hurt. DeerAI adds one automatically.
public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    public float Current { get; private set; }
    public bool IsDead { get; private set; }

    /// (damage, position the damage came from)
    public event Action<float, Vector3> Damaged;
    public event Action Died;

    void Awake()
    {
        Current = maxHealth;
    }

    public void SetMax(float value)
    {
        maxHealth = value;
        Current = value;
    }

    /// Restore health without exceeding the maximum.
    public void Heal(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }

    public void TakeDamage(float amount, Vector3 fromPosition)
    {
        if (IsDead) return;

        Current -= amount;
        if (Damaged != null) Damaged(amount, fromPosition);

        if (Current <= 0f)
        {
            Current = 0f;
            IsDead = true;
            if (Died != null) Died();
        }
    }
}
