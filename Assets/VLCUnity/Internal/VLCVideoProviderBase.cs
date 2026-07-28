using System;
using UnityEngine;

namespace LibVLCSharp
{
    public abstract class VLCVideoProviderBase : MonoBehaviour
    {
        public abstract event Action<RenderTexture> OnTextureResized;
        public abstract RenderTexture OutputTexture { get; protected set; }
    }
}
