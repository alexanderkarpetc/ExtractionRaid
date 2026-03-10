using System;
using Constants;
using State;
using UnityEngine;

namespace View
{
    public class PlayerView : MonoBehaviour, IDamageableView
    {
        [SerializeField] Transform _weaponPivot;
        [SerializeField] Transform _capsuleVisual;

        Transform _muzzlePoint;
        Action<Transform> _onMuzzlePointChanged;
        string _currentWeaponPrefabId;
        GameObject _currentWeaponModel;
        WorldHealthBar _healthBar;
        WeaponView _currentWeaponView;
        float _rollVisualAngle;

        public EId EId { get; private set; }
        public Transform MuzzlePoint => _muzzlePoint;
        public WeaponView WeaponView => _currentWeaponView;

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

            SyncRollVisual(state);

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

        void SyncRollVisual(PlayerEntityState state)
        {
            if (_capsuleVisual == null) return;

            if (state.IsRolling)
            {
                _rollVisualAngle += (360f / DodgeConstants.Duration) * Time.deltaTime;

                var rollAxis = Vector3.Cross(Vector3.up, state.RollDirection);
                if (rollAxis.sqrMagnitude < 0.001f)
                    rollAxis = Vector3.right;

                _capsuleVisual.localRotation = Quaternion.AngleAxis(
                    _rollVisualAngle,
                    transform.InverseTransformDirection(rollAxis.normalized));
            }
            else if (_rollVisualAngle != 0f)
            {
                _rollVisualAngle = 0f;
                _capsuleVisual.localRotation = Quaternion.identity;
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

            _currentWeaponView = _currentWeaponModel.GetComponent<WeaponView>();
            _muzzlePoint = _currentWeaponView != null ? _currentWeaponView.MuzzlePoint : null;
            _onMuzzlePointChanged?.Invoke(_muzzlePoint);
        }

        void ClearWeaponModel()
        {
            if (_currentWeaponModel != null)
                Destroy(_currentWeaponModel);

            _currentWeaponPrefabId = null;
            _currentWeaponModel = null;
            _currentWeaponView = null;
            _muzzlePoint = null;
            _onMuzzlePointChanged?.Invoke(null);
        }
    }
}
