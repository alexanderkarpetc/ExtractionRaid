using System;
using State;
using UnityEngine;

namespace View
{
    public class PlayerView : MonoBehaviour, IDamageableView
    {
        [SerializeField] Transform _weaponPivot;

        Transform _muzzlePoint;
        Action<Transform> _onMuzzlePointChanged;
        string _currentWeaponPrefabId;
        GameObject _currentWeaponModel;
        WorldHealthBar _healthBar;

        public EId EId { get; private set; }
        public Transform MuzzlePoint => _muzzlePoint;

        public void Initialize(EId id, Action<Transform> onMuzzlePointChanged)
        {
            EId = id;
            _onMuzzlePointChanged = onMuzzlePointChanged;
            _healthBar = WorldHealthBar.Create(transform);
        }

        public void OnDamaged(float currentHp, float maxHp)
        {
            if (_healthBar != null)
                _healthBar.UpdateHealth(currentHp, maxHp);
        }

        public void SyncFromState(PlayerEntityState state)
        {
            transform.position = state.Position;

            if (state.FacingDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(state.FacingDirection, Vector3.up);
            }

            if (_weaponPivot != null)
            {
                bool hasWeapon = state.EquippedWeapon != null;
                _weaponPivot.gameObject.SetActive(hasWeapon);

                if (hasWeapon)
                {
                    if (state.EquippedWeapon.PrefabId != _currentWeaponPrefabId)
                        SwapWeaponModel(state.EquippedWeapon.PrefabId);

                    if (state.AimDirection.sqrMagnitude > 0.001f)
                    {
                        _weaponPivot.rotation = Quaternion.LookRotation(state.AimDirection, Vector3.up);
                    }
                }
                else if (_currentWeaponPrefabId != null)
                {
                    ClearWeaponModel();
                }
            }
        }

        void SwapWeaponModel(string prefabId)
        {
            if (_currentWeaponModel != null)
                Destroy(_currentWeaponModel);

            _currentWeaponPrefabId = prefabId;

            if (string.IsNullOrEmpty(prefabId)) return;

            var prefab = Resources.Load<GameObject>("Prefabs/Weapons/" + prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[PlayerView] Weapon prefab not found: Prefabs/Weapons/{prefabId}");
                return;
            }

            _currentWeaponModel = Instantiate(prefab, _weaponPivot);
            _currentWeaponModel.transform.localPosition = Vector3.zero;
            _currentWeaponModel.transform.localRotation = Quaternion.identity;

            _muzzlePoint = _currentWeaponModel.transform.Find("MuzzlePoint");
            _onMuzzlePointChanged?.Invoke(_muzzlePoint);
        }

        void ClearWeaponModel()
        {
            if (_currentWeaponModel != null)
                Destroy(_currentWeaponModel);

            _currentWeaponPrefabId = null;
            _currentWeaponModel = null;
            _muzzlePoint = null;
            _onMuzzlePointChanged?.Invoke(null);
        }
    }
}
