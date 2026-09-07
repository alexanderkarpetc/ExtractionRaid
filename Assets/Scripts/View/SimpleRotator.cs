using UnityEngine;

namespace View
{
    public sealed class SimpleRotator : MonoBehaviour
    {
        private enum RotationAxis
        {
            X,
            Y,
            Z
        }

        [SerializeField] private RotationAxis _axis = RotationAxis.Y;
        [SerializeField] private float _speed = 90f;

        private void Update()
        {
            Vector3 axis = _axis switch
            {
                RotationAxis.X => Vector3.right,
                RotationAxis.Y => Vector3.up,
                RotationAxis.Z => Vector3.forward,
                _ => Vector3.up
            };

            transform.Rotate(axis, _speed * Time.deltaTime, Space.Self);
        }
    }
}
