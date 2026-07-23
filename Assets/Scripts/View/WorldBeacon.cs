using UnityEngine;

namespace View
{
    /// <summary>
    /// Reusable always-on world beacon VFX: an additive ground light pool
    /// (<c>VFX/QuestGroundGlow</c>) + a camera-billboarded vertical glow column
    /// (<c>VFX/QuestBeam</c>). Procedural — unit meshes scaled by transform, bloom-friendly.
    /// Public fields drive color / size / intensity so callers can tune live (e.g. from a
    /// ViewCheats section). Spawned by <see cref="ExtractionBeaconPresenter"/>. The quest
    /// marker keeps its own equivalent VFX inside <see cref="NpcQuestIndicator"/>.
    /// </summary>
    public class WorldBeacon : MonoBehaviour
    {
        public Color Color = new(0.30f, 0.95f, 0.80f, 1f); // cyan/teal

        public float GroundRadius = 4.8f;
        public float GroundY = 0.06f;
        public float GroundSoftFade = 0.5f;
        public float GroundAlphaMin = 0.45f;
        public float GroundAlphaMax = 0.80f;

        public float BeamHeight = 13.5f;
        public float BeamHalfWidth = 1.2f;
        public float BeamBaseY = 0.10f;
        public float BeamAlphaMin = 0.45f;
        public float BeamAlphaMax = 0.85f;

        public float PulseHz = 0.7f;

        static readonly int PropColor = Shader.PropertyToID("_Color");
        static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        static readonly int PropSoftFade = Shader.PropertyToID("_SoftFade");

        static Mesh s_beamMesh, s_groundMesh;

        GameObject _beam, _ground;
        Material _beamMat, _groundMat;

        public static WorldBeacon Create(Vector3 worldPos, string name = "WorldBeacon")
        {
            var go = new GameObject(name);
            go.transform.position = worldPos;
            return go.AddComponent<WorldBeacon>();
        }

        void Awake()
        {
            BuildGround();
            BuildBeam();
        }

        void BuildGround()
        {
            var shader = Shader.Find("VFX/QuestGroundGlow");
            if (shader == null)
            {
                Debug.LogWarning("[WorldBeacon] Shader 'VFX/QuestGroundGlow' not found; ground glow disabled.");
                return;
            }
            _ground = new GameObject("GroundGlow");
            _ground.transform.SetParent(transform, false);
            var mf = _ground.AddComponent<MeshFilter>();
            mf.sharedMesh = s_groundMesh != null ? s_groundMesh : (s_groundMesh = BuildGroundMesh());
            var mr = _ground.AddComponent<MeshRenderer>();
            DisableExtras(mr);
            _groundMat = new Material(shader);
            mr.sharedMaterial = _groundMat;
        }

        void BuildBeam()
        {
            var shader = Shader.Find("VFX/QuestBeam");
            if (shader == null)
            {
                Debug.LogWarning("[WorldBeacon] Shader 'VFX/QuestBeam' not found; beam disabled.");
                return;
            }
            _beam = new GameObject("Beam");
            _beam.transform.SetParent(transform, false);
            var mf = _beam.AddComponent<MeshFilter>();
            mf.sharedMesh = s_beamMesh != null ? s_beamMesh : (s_beamMesh = BuildBeamMesh());
            var mr = _beam.AddComponent<MeshRenderer>();
            DisableExtras(mr);
            _beamMat = new Material(shader);
            mr.sharedMaterial = _beamMat;
        }

        static void DisableExtras(MeshRenderer mr)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        // Unit vertical quad: x∈[-0.5,0.5], y∈[0,1], Z=0. Size = transform.localScale.
        static Mesh BuildBeamMesh()
        {
            var verts = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 1f, 0f) };
            var uvs  = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            var tris = new[] { 0, 2, 1, 0, 3, 2 };
            var m = new Mesh { name = "WorldBeaconBeam" };
            m.vertices = verts; m.uv = uvs; m.triangles = tris; m.RecalculateBounds();
            return m;
        }

        // Unit horizontal quad on XZ (normal +Y), x,z∈[-0.5,0.5]. Radius = transform.localScale.
        static Mesh BuildGroundMesh()
        {
            var verts = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
            var uvs  = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            var tris = new[] { 0, 1, 2, 0, 2, 3 };
            var m = new Mesh { name = "WorldBeaconGround" };
            m.vertices = verts; m.uv = uvs; m.triangles = tris; m.RecalculateBounds();
            return m;
        }

        void LateUpdate()
        {
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseHz * Mathf.PI * 2f);

            if (_ground != null)
            {
                _ground.transform.localPosition = new Vector3(0f, GroundY, 0f);
                _ground.transform.localScale = new Vector3(GroundRadius * 2f, 1f, GroundRadius * 2f);
            }
            if (_groundMat != null)
            {
                _groundMat.SetColor(PropColor, Color);
                _groundMat.SetFloat(PropSoftFade, GroundSoftFade);
                _groundMat.SetFloat(PropAlpha, Mathf.Lerp(GroundAlphaMin, GroundAlphaMax, breath));
            }

            if (_beam != null)
            {
                _beam.transform.localPosition = new Vector3(0f, BeamBaseY, 0f);
                _beam.transform.localScale = new Vector3(BeamHalfWidth * 2f, BeamHeight, 1f);
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 toCam = cam.transform.position - _beam.transform.position;
                    float yaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
                    _beam.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }
            if (_beamMat != null)
            {
                _beamMat.SetColor(PropColor, Color);
                _beamMat.SetFloat(PropAlpha, Mathf.Lerp(BeamAlphaMin, BeamAlphaMax, breath));
            }
        }

        void OnDestroy()
        {
            if (_beamMat != null) Destroy(_beamMat);
            if (_groundMat != null) Destroy(_groundMat);
        }
    }
}
