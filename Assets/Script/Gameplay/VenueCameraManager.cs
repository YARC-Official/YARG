using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG
{
    [ExecuteInEditMode]
    public class VenueCameraManager : MonoBehaviour
    {
        [Range(0.01F, 1.0F)]
        public float renderScale = 1.0F;

        private Camera _renderCamera;
        private float _originalFactor;
        private UniversalRenderPipelineAsset UniversalRenderPipelineAsset;

        private void Awake()
        {
            _renderCamera = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            var cameraData = _renderCamera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing = AntialiasingMode.None;
            switch (GraphicsManager.Instance.VenueAA)
            {
                case VenueAA.None:
                    break;
                case VenueAA.FXAA:
                    cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case VenueAA.MSAA:
                    cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    break;
                case VenueAA.FSR3:
                    _renderCamera.gameObject.AddComponent<FSRCameraManager>();
                    break;
            }
            RenderPipelineManager.endCameraRendering += OnPostCameraRender;
            UniversalRenderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            _originalFactor = UniversalRenderPipelineAsset.renderScale;
        }

        void OnDestroy()
        {
            UniversalRenderPipelineAsset.renderScale = _originalFactor;
        }

        void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == _renderCamera)
            {
                UniversalRenderPipelineAsset.renderScale = renderScale;
            }
        }

        void OnPostCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == _renderCamera)
            {
                UniversalRenderPipelineAsset.renderScale = _originalFactor;
            }
        }
    }
}
