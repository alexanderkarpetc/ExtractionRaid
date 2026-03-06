using State;
using UnityEngine;

namespace View
{
    public class DestructibleView : MonoBehaviour
    {
        [SerializeField] float _maxHp = 100f;

        WorldHealthBar _healthBar;

        public EId EId { get; private set; }
        public float MaxHp => _maxHp;

        public void Initialize(EId id)
        {
            EId = id;
            _healthBar = WorldHealthBar.Create(transform);
        }

        public void OnDamaged(float currentHp, float maxHp)
        {
            if (_healthBar != null)
                _healthBar.UpdateHealth(currentHp, maxHp);
        }
    }
}
