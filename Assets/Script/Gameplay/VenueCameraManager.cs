using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Gameplay
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class VenueCameraManager : MonoBehaviour
    {
        [Range(0.01F, 1.0F)]
        public float renderScale = 1.0F;

        private Camera _renderCamera;
        private float _originalFactor;
        private UniversalRenderPipelineAsset UniversalRenderPipelineAsset;

        private void Awake()
        {
            renderScale = GraphicsManager.Instance.VenueRenderScale;
            _renderCamera = GetComponent<Camera>();
            _renderCamera.allowMSAA = false;
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            var cameraData = _renderCamera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing = AntialiasingMode.None;
            switch (GraphicsManager.Instance.VenueAntiAliasing)
            {
                case VenueAntiAliasingMethod.None:
                    break;
                case VenueAntiAliasingMethod.FXAA:
                    cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case VenueAntiAliasingMethod.MSAA:
                    _renderCamera.allowMSAA = true;
                    cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    break;
                case VenueAntiAliasingMethod.FSR3:
                    _renderCamera.gameObject.AddComponent<FSRCameraManager>();
                    break;
            }
            RenderPipelineManager.endCameraRendering += OnPostCameraRender;
            UniversalRenderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            _originalFactor = UniversalRenderPipelineAsset.renderScale;
        }

        private void OnDestroy()
        {
            UniversalRenderPipelineAsset.renderScale = _originalFactor;
        }

        private void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == _renderCamera)
            {
                UniversalRenderPipelineAsset.renderScale = renderScale;
            }
        }

        private void OnPostCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == _renderCamera)
            {
                UniversalRenderPipelineAsset.renderScale = _originalFactor;
            }
        }
    }
}
