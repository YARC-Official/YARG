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

            RenderPipelineManager.beginCameraRendering += OnPreRender;
            RenderPipelineManager.endCameraRendering += OnPostRender;
        }

        void OnDestroy()
        {
            UniversalRenderPipelineAsset.renderScale = originalFactor;
        }

        void OnPreRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == camera)
            {
                if (UniversalRenderPipelineAsset != null)
                {
                    originalFactor = UniversalRenderPipelineAsset.renderScale;
                    UniversalRenderPipelineAsset.renderScale = renderScale;
                }
            }
        }

        void OnPostRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == camera)
            {
                UniversalRenderPipelineAsset.renderScale = originalFactor;
            }
        }
    }
}
