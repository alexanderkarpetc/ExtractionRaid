using Constants;
using Dev;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace View.FogOfWar
{
    /// <summary>
    /// Orchestrates the Fog of War rendering pipeline:
    /// 1. Manages FOV camera + RenderTexture
    /// 2. Drives ray sweep + mesh rebuild each frame
    /// 3. Exposes visibility RT as global shader texture
    ///
    /// Created by PlayerPresenter.SpawnView(), destroyed in Dispose().
    /// No singleton (architecture rule 12).
    /// </summary>
    public class FogOfWarController : MonoBehaviour
    {
        const string GlobalTextureName = "_FoWVisibility";

        static readonly int FoWPrevBlurredId = Shader.PropertyToID("_FoWPrevBlurred");

        Camera _fovCamera;
        RenderTexture _rawRT;
        RenderTexture _blurredRT; // persistent for temporal blend
        FOVMeshBuilder _meshBuilder;
        Material _fovMeshMaterial;
        Collider[] _playerColliders;
        Transform _playerTransform;
        bool _initialized;
        int _currentRTScale;

        public void Initialize(Transform playerTransform)
        {
            _playerTransform = playerTransform;
            _playerColliders = playerTransform.GetComponentsInChildren<Collider>();

            CreateRenderTexture(DevCheats.FoWRTScale);
            CreateFOVCamera();
            CreateMeshBuilder();
            ExcludeFOVLayerFromMainCamera();

            _initialized = true;
        }

        void CreateRenderTexture(int scale)
        {
            if (_rawRT != null)
            {
                _fovCamera.targetTexture = null;
                _rawRT.Release();
            }

            // Match screen aspect ratio so UV mapping aligns 1:1 with main camera
            var mainCam = Camera.main;
            float aspect = mainCam != null ? mainCam.aspect : 16f / 9f;
            int w = Mathf.Max(scale, 64);
            int h = Mathf.Max(Mathf.RoundToInt(w / aspect), 64);
            _rawRT = new RenderTexture(w, h, 16, RenderTextureFormat.R8)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "FOW_RawVisibility"
            };

            if (_blurredRT != null) _blurredRT.Release();
            _blurredRT = new RenderTexture(w, h, 0, RenderTextureFormat.R8)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "FOW_BlurredPersistent"
            };

            _currentRTScale = scale;

            if (_fovCamera != null)
                _fovCamera.targetTexture = _rawRT;
        }

        void CreateFOVCamera()
        {
            var go = new GameObject("FOV Camera");
            go.transform.SetParent(transform, false);

            _fovCamera = go.AddComponent<Camera>();
            _fovCamera.cullingMask = 1 << LayerMask.NameToLayer("FOV");
            _fovCamera.clearFlags = CameraClearFlags.SolidColor;
            _fovCamera.backgroundColor = Color.black;
            _fovCamera.targetTexture = _rawRT;
            _fovCamera.depth = -10;

            // URP camera data — no post processing, no shadows
            var urpData = go.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = false;
            urpData.renderShadows = false;
        }

        void CreateMeshBuilder()
        {
            var meshGo = new GameObject("FOVMesh");
            meshGo.transform.SetParent(_playerTransform, false);
            meshGo.layer = LayerMask.NameToLayer("FOV");

            meshGo.AddComponent<MeshFilter>();
            var renderer = meshGo.AddComponent<MeshRenderer>();
            // Must use URP-compatible shader — built-in "Unlit/Color" may not render in URP on all platforms
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("[FoW] Cannot find 'Universal Render Pipeline/Unlit' shader! " +
                               "FoW mesh will not render. Falling back to Unlit/Color.");
                shader = Shader.Find("Unlit/Color");
            }

            _fovMeshMaterial = new Material(shader);
            _fovMeshMaterial.SetColor("_BaseColor", Color.white);
            _fovMeshMaterial.SetFloat("_Surface", 0); // Opaque
            renderer.material = _fovMeshMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _meshBuilder = meshGo.AddComponent<FOVMeshBuilder>();
        }

        void ExcludeFOVLayerFromMainCamera()
        {
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                int fovLayer = LayerMask.NameToLayer("FOV");
                if (fovLayer >= 0)
                    mainCam.cullingMask &= ~(1 << fovLayer);
            }
        }

        void LateUpdate()
        {
            if (!_initialized) return;
            if (!DevCheats.FOVEnabled || !DevCheats.FogOfWarEnabled)
            {
                ToggleFOVCamera(false);
                // Set white texture so composite has no effect
                Shader.SetGlobalTexture(GlobalTextureName, Texture2D.whiteTexture);
                return;
            }

            // Recreate RT if resolution changed in DevCheats
            if (_currentRTScale != DevCheats.FoWRTScale)
                CreateRenderTexture(DevCheats.FoWRTScale);

            ToggleFOVCamera(true);
            SyncFOVCamera();
            RebuildVisibilityMesh();
            Shader.SetGlobalTexture(GlobalTextureName, _rawRT);
            Shader.SetGlobalTexture(FoWPrevBlurredId, _blurredRT);
        }

        void ToggleFOVCamera(bool on)
        {
            if (_fovCamera != null && _fovCamera.enabled != on)
                _fovCamera.enabled = on;
        }

        void SyncFOVCamera()
        {
            var mainCam = Camera.main;
            if (mainCam == null || _fovCamera == null) return;

            var t = mainCam.transform;
            _fovCamera.transform.SetPositionAndRotation(t.position, t.rotation);
            _fovCamera.orthographic = mainCam.orthographic;
            _fovCamera.orthographicSize = mainCam.orthographicSize;
            _fovCamera.fieldOfView = mainCam.fieldOfView;
            _fovCamera.nearClipPlane = mainCam.nearClipPlane;
            _fovCamera.farClipPlane = mainCam.farClipPlane;
        }

        void RebuildVisibilityMesh()
        {
            if (_meshBuilder == null || _playerTransform == null) return;

            var endpoints = FOVRaySweep.Sweep(
                _playerTransform.position,
                _playerTransform.forward,
                DevCheats.FOVNearRadius,
                DevCheats.FOVFarRadius,
                DevCheats.FOVAngle,
                DevCheats.FOVRayStep,
                BotConstants.VisionBlockingMask,
                _playerColliders);

            _meshBuilder.RebuildMesh(endpoints);
        }

        void OnDestroy()
        {
            if (_rawRT != null)
            {
                _rawRT.Release();
                _rawRT = null;
            }

            if (_blurredRT != null)
            {
                _blurredRT.Release();
                _blurredRT = null;
            }

            if (_fovMeshMaterial != null)
                Destroy(_fovMeshMaterial);

            Shader.SetGlobalTexture(GlobalTextureName, Texture2D.whiteTexture);
        }
    }
}
