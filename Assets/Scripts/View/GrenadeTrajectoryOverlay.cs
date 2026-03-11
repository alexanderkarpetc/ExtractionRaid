using Constants;
using State;
using UnityEngine;

namespace View
{
    public class GrenadeTrajectoryOverlay : MonoBehaviour
    {
        LineRenderer _trajectoryLine;
        LineRenderer _radiusLine;
        Vector3 _landingPoint;
        bool _hasLanding;

        const int RadiusSegments = 48;

        void Awake()
        {
            _trajectoryLine = gameObject.AddComponent<LineRenderer>();
            _trajectoryLine.startWidth = 0.05f;
            _trajectoryLine.endWidth = 0.05f;
            _trajectoryLine.material = new Material(Shader.Find("Sprites/Default"));
            _trajectoryLine.startColor = new Color(1f, 0.4f, 0f, 0.8f);
            _trajectoryLine.endColor = new Color(1f, 0.2f, 0f, 0.3f);
            _trajectoryLine.positionCount = 0;
            _trajectoryLine.useWorldSpace = true;

            var radiusGo = new GameObject("ExplosionRadiusCircle");
            radiusGo.transform.SetParent(transform);
            _radiusLine = radiusGo.AddComponent<LineRenderer>();
            _radiusLine.startWidth = 0.04f;
            _radiusLine.endWidth = 0.04f;
            _radiusLine.material = new Material(Shader.Find("Sprites/Default"));
            _radiusLine.startColor = new Color(1f, 0.15f, 0f, 0.5f);
            _radiusLine.endColor = new Color(1f, 0.15f, 0f, 0.5f);
            _radiusLine.loop = true;
            _radiusLine.positionCount = 0;
            _radiusLine.useWorldSpace = true;
        }

        public void UpdateTrajectory(PlayerEntityState player)
        {
            if (player == null || !player.IsInGrenadeMode || !player.GrenadeThrowCharging)
            {
                Hide();
                return;
            }

            var aimDir = player.AimDirection;
            if (aimDir.sqrMagnitude < 0.001f)
                aimDir = player.FacingDirection;

            var horizontalDir = new Vector3(aimDir.x, 0f, aimDir.z).normalized;

            var gravityVec = Physics.gravity;
            float speed = GrenadeConstants.ComputeThrowSpeed(
                player.GrenadeTargetDistance, Mathf.Abs(gravityVec.y));
            float rad = GrenadeConstants.UpwardAngle * Mathf.Deg2Rad;
            var throwDir = (horizontalDir * Mathf.Cos(rad) +
                            Vector3.up * Mathf.Sin(rad)).normalized;

            var velocity = throwDir * speed;
            var pos = player.Position + Vector3.up * GrenadeConstants.LaunchHeight + horizontalDir * 0.5f;

            var points = new Vector3[GrenadeConstants.MaxTrajectorySegments];
            int count = 0;
            _hasLanding = false;

            for (int i = 0; i < GrenadeConstants.MaxTrajectorySegments; i++)
            {
                points[count++] = pos;

                var nextPos = pos + velocity * GrenadeConstants.TrajectoryTimeStep;
                velocity += gravityVec * GrenadeConstants.TrajectoryTimeStep;

                if (Physics.Linecast(pos, nextPos, out var hit))
                {
                    points[count++] = hit.point;
                    _landingPoint = hit.point;
                    _hasLanding = true;
                    break;
                }

                pos = nextPos;
            }

            if (!_hasLanding)
            {
                _landingPoint = points[count - 1];
                _hasLanding = true;
            }

            _trajectoryLine.positionCount = count;
            _trajectoryLine.SetPositions(points);

            UpdateRadiusCircle();
        }

        void UpdateRadiusCircle()
        {
            if (!_hasLanding)
            {
                _radiusLine.positionCount = 0;
                return;
            }

            var radius = GrenadeConstants.ExplosionRadius;
            _radiusLine.positionCount = RadiusSegments;
            var center = _landingPoint + Vector3.up * 0.05f;

            for (int i = 0; i < RadiusSegments; i++)
            {
                float angle = (float)i / RadiusSegments * Mathf.PI * 2f;
                var point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _radiusLine.SetPosition(i, point);
            }
        }

        public void Hide()
        {
            _hasLanding = false;
            if (_trajectoryLine != null)
                _trajectoryLine.positionCount = 0;
            if (_radiusLine != null)
                _radiusLine.positionCount = 0;
        }

        void OnDrawGizmos()
        {
            if (!_hasLanding) return;

            Gizmos.color = new Color(1f, 0.2f, 0f, 0.4f);
            Gizmos.DrawWireSphere(_landingPoint, GrenadeConstants.ExplosionRadius);
        }
    }
}
