using State;
using UnityEngine;

namespace View
{
    public class DestructibleView : MonoBehaviour
    {
        [SerializeField] float _maxHp = 100f;

        public EId EId { get; private set; }
        public float MaxHp => _maxHp;

        public void Initialize(EId id)
        {
            EId = id;
        }
    }
}
