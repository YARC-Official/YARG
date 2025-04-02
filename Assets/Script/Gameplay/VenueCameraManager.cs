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

        private new Camera camera;
        private float originalFactor;
        private UniversalRenderPipelineAsset UniversalRenderPipelineAsset;

        private void Awake()
        {
            camera = GetComponent<Camera>();
            UniversalRenderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            originalFactor = UniversalRenderPipelineAsset.renderScale;

            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            RenderPipelineManager.endCameraRendering += OnPostCameraRender;
        }

        void OnDestroy()
        {
            UniversalRenderPipelineAsset.renderScale = originalFactor;
        }

        void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == camera)
            {
                if (UniversalRenderPipelineAsset != null)
                {
                    UniversalRenderPipelineAsset.renderScale = renderScale;
                }
            }
        }

        void OnPostCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == camera)
            {
                UniversalRenderPipelineAsset.renderScale = originalFactor;
            }
        }
    }
}
