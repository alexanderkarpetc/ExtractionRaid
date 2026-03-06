using UnityEngine;

namespace State
{
    public class HealthState
    {
        public float CurrentHp;
        public float MaxHp;
        public bool IsAlive;

        public static HealthState Create(float maxHp)
        {
            return new HealthState
            {
                CurrentHp = maxHp,
                MaxHp = maxHp,
                IsAlive = true,
            };
        }
    }
}
