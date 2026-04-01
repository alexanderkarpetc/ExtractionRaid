using System.Collections.Generic;
using UnityEngine;

namespace View
{
    public class ProximityObjectToggler : MonoBehaviour
    {
        [SerializeField] List<GameObject> _objectsToToggle = new();
        [SerializeField] Transform _target;
        [SerializeField] string _targetTag = "Player";
        [SerializeField] float _disableDistance = 5f;
        [SerializeField] bool _disableWhenNear = true;
        [SerializeField] bool _restoreWhenFar = true;
        [SerializeField] Vector3 _distanceOffset;

        bool _isInNearState;

        void Awake()
        {
            ApplyState(false, true);
        }

        void Update()
        {
            if (!TryResolveTarget(out var target))
                return;

            var origin = transform.position + _distanceOffset;
            var sqrDistance = (target.position - origin).sqrMagnitude;
            bool isNear = sqrDistance <= _disableDistance * _disableDistance;

            if (isNear == _isInNearState)
                return;

            _isInNearState = isNear;
            ApplyState(isNear, false);
        }

        bool TryResolveTarget(out Transform target)
        {
            if (_target != null)
            {
                target = _target;
                return true;
            }

            if (string.IsNullOrWhiteSpace(_targetTag))
            {
                target = null;
                return false;
            }

            var targetObject = GameObject.FindWithTag(_targetTag);
            if (targetObject == null)
            {
                target = null;
                return false;
            }

            _target = targetObject.transform;
            target = _target;
            return true;
        }

        void ApplyState(bool isNear, bool force)
        {
            if (!force && isNear && !_disableWhenNear)
                return;

            if (!force && !isNear && !_restoreWhenFar)
                return;

            bool shouldBeActive = !isNear;
            for (int i = 0; i < _objectsToToggle.Count; i++)
            {
                var targetObject = _objectsToToggle[i];
                if (targetObject == null)
                    continue;

                if (targetObject.activeSelf == shouldBeActive)
                    continue;

                targetObject.SetActive(shouldBeActive);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position + _distanceOffset, _disableDistance);
        }
#endif
    }
}
