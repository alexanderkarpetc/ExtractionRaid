using State;
using UnityEngine;

namespace View
{
    public class BotView : MonoBehaviour, IDamageableView
    {
        [SerializeField] Transform _weaponPivot;

        string _currentWeaponPrefabId;
        GameObject _currentWeaponModel;
        WorldHealthBar _healthBar;

        public EId EId { get; private set; }
        public string TypeId { get; private set; }

        public void Initialize(EId id, string typeId, string weaponPrefabId)
        {
            EId = id;
            TypeId = typeId;
            _healthBar = WorldHealthBar.Create(transform);

            if (!string.IsNullOrEmpty(weaponPrefabId))
                SwapWeaponModel(weaponPrefabId);
        }

        public void OnDamaged(float currentHp, float maxHp)
        {
            if (_healthBar != null)
                _healthBar.UpdateHealth(currentHp, maxHp);
        }

        public void SyncFromState(BotEntityState state)
        {
            transform.position = state.Position;

            if (state.FacingDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(state.FacingDirection, Vector3.up);

            if (_weaponPivot != null && state.AimDirection.sqrMagnitude > 0.001f)
                _weaponPivot.rotation = Quaternion.LookRotation(state.AimDirection, Vector3.up);
        }

        void SwapWeaponModel(string prefabId)
        {
            if (_currentWeaponModel != null)
                Destroy(_currentWeaponModel);

            _currentWeaponPrefabId = prefabId;

            var prefab = Resources.Load<GameObject>("Prefabs/Weapons/" + prefabId);
            if (prefab == null) return;

            if (_weaponPivot == null) return;

            _currentWeaponModel = Instantiate(prefab, _weaponPivot);
            _currentWeaponModel.transform.localPosition = Vector3.zero;
            _currentWeaponModel.transform.localRotation = Quaternion.identity;
        }
    }
}
