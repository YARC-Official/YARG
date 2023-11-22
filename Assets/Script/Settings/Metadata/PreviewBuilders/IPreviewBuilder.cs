using Cysharp.Threading.Tasks;
using UnityEngine;

namespace YARG.Settings.Metadata
{
    public interface IPreviewBuilder
    {
        public UniTask BuildPreviewWorld(Transform worldContainer);

        public UniTask BuildPreviewUI(Transform uiContainer);
    }
}