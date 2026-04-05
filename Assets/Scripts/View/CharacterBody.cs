using UnityEngine;

namespace View
{
    /// <summary>
    /// Shared character visual attachment logic — weapon, armor, roll animation.
    /// Lives on the character MODEL prefab. PlayerView/BotView delegate to this.
    /// Decouples character logic (view scripts) from character visuals (mesh/skeleton).
    /// </summary>
    public class CharacterBody : MonoBehaviour
    {
        [SerializeField] Transform _weaponPivot;
        [SerializeField] Transform _helmetSlot;
        [SerializeField] Transform _armorSlot;
        [SerializeField] Transform _capsuleVisual;
        [SerializeField] Animator _animator;

        string _currentWeaponPrefabId;
        GameObject _currentWeaponModel;
        WeaponView _currentWeaponView;

        string _currentHelmetPrefabId;
        GameObject _currentHelmetModel;

        string _currentArmorPrefabId;
        GameObject _currentArmorModel;

        bool _wasRollingLastFrame;
        bool _wasMovingLastFrame;

        // ── Public access ──────────────────────────────────

        public Transform WeaponPivot => _weaponPivot;
        public WeaponView WeaponView => _currentWeaponView;
        public Transform MuzzlePoint => _currentWeaponView != null ? _currentWeaponView.MuzzlePoint : null;
        public Animator Animator => _animator;

        public void SyncAnimatorState(bool isRolling, bool isMoving)
        {
            if (_animator == null)
            {
                _wasRollingLastFrame = isRolling;
                _wasMovingLastFrame = isMoving;
                return;
            }

            _animator.SetBool("Run", !isRolling && isMoving);

            if (isRolling && !_wasRollingLastFrame)
            {
                _animator.ResetTrigger("Roll");
                _animator.SetTrigger("Roll");
            }
            else if (!isRolling && _wasRollingLastFrame)
            {
                _animator.ResetTrigger("Roll");
            }

            if (!isRolling && !isMoving && (_wasRollingLastFrame || _wasMovingLastFrame))
            {
                _animator.ResetTrigger("Idle");
                _animator.SetTrigger("Idle");
            }

            _wasRollingLastFrame = isRolling;
            _wasMovingLastFrame = isMoving;
        }

        // ── Weapon ─────────────────────────────────────────

        /// <summary>Equip weapon model. Returns the new WeaponView (or null).</summary>
        public WeaponView SwapWeaponModel(string prefabId)
        {
            if (_currentWeaponModel != null)
                Destroy(_currentWeaponModel);

            _currentWeaponPrefabId = prefabId;
            _currentWeaponView = null;

            if (string.IsNullOrEmpty(prefabId) || _weaponPivot == null)
                return null;

            var prefab = Resources.Load<GameObject>("Prefabs/Weapons/" + prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterBody] Weapon prefab not found: Prefabs/Weapons/{prefabId}");
                return null;
            }

            _currentWeaponModel = Instantiate(prefab, _weaponPivot);
            _currentWeaponModel.transform.localPosition = Vector3.zero;
            _currentWeaponModel.transform.localRotation = Quaternion.identity;

            _currentWeaponView = _currentWeaponModel.GetComponent<WeaponView>();
            return _currentWeaponView;
        }

        public void ClearWeaponModel()
        {
            if (_currentWeaponModel != null)
                Destroy(_currentWeaponModel);

            _currentWeaponPrefabId = null;
            _currentWeaponModel = null;
            _currentWeaponView = null;
        }

        public string CurrentWeaponPrefabId => _currentWeaponPrefabId;

        // ── Helmet ─────────────────────────────────────────

        public void SwapHelmetModel(string prefabId)
        {
            if (prefabId == _currentHelmetPrefabId) return;
            ClearHelmetModel();
            _currentHelmetPrefabId = prefabId;
            if (string.IsNullOrEmpty(prefabId) || _helmetSlot == null) return;

            var prefab = Resources.Load<GameObject>("Prefabs/Armor/" + prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterBody] Helmet prefab not found: Prefabs/Armor/{prefabId}");
                return;
            }

            _currentHelmetModel = Instantiate(prefab, _helmetSlot);
            _currentHelmetModel.transform.localPosition = Vector3.zero;
            _currentHelmetModel.transform.localRotation = Quaternion.identity;
        }

        public void ClearHelmetModel()
        {
            if (_currentHelmetModel != null)
                Destroy(_currentHelmetModel);
            _currentHelmetPrefabId = null;
            _currentHelmetModel = null;
        }

        /// <summary>Detach helmet for fly-off effect. Returns the GO without destroying it.</summary>
        public GameObject DetachHelmetModel()
        {
            var model = _currentHelmetModel;
            _currentHelmetPrefabId = null;
            _currentHelmetModel = null;
            return model;
        }

        // ── Body Armor ─────────────────────────────────────

        public void SwapArmorModel(string prefabId)
        {
            if (prefabId == _currentArmorPrefabId) return;
            ClearArmorModel();
            _currentArmorPrefabId = prefabId;
            if (string.IsNullOrEmpty(prefabId) || _armorSlot == null) return;

            var prefab = Resources.Load<GameObject>("Prefabs/Armor/" + prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterBody] Armor prefab not found: Prefabs/Armor/{prefabId}");
                return;
            }

            _currentArmorModel = Instantiate(prefab, _armorSlot);
            _currentArmorModel.transform.localPosition = Vector3.zero;
            _currentArmorModel.transform.localRotation = Quaternion.identity;
        }

        public void ClearArmorModel()
        {
            if (_currentArmorModel != null)
                Destroy(_currentArmorModel);
            _currentArmorPrefabId = null;
            _currentArmorModel = null;
        }

        // ── Roll animation ─────────────────────────────────

        /// <summary>Drive roll visual. Call every frame from SyncFromState.</summary>
        public void SyncRollVisual(bool isRolling, Vector3 rollDirection, Transform characterTransform)
        {
            if (_capsuleVisual != null)
                _capsuleVisual.localRotation = Quaternion.identity;
        }
    }
}
