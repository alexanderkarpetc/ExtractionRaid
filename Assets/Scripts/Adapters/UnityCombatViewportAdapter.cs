using UnityEngine;

namespace Adapters
{
    public sealed class UnityCombatViewportAdapter : ICombatViewportAdapter
    {
        Camera _camera;

        public void SetCamera(Camera camera) => _camera = camera;

        public bool IsInside(Vector3 worldPosition, float normalizedMargin)
        {
            if (_camera == null)
                return true;

            float margin = Mathf.Clamp(normalizedMargin, 0f, 0.49f);
            var viewport = _camera.WorldToViewportPoint(worldPosition);
            return viewport.z > 0f
                   && viewport.x >= margin && viewport.x <= 1f - margin
                   && viewport.y >= margin && viewport.y <= 1f - margin;
        }
    }
}
