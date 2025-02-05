using UnityEngine;

namespace YARG.Rendering
{

    public static class MaterialPropertyInstance
    {
        private static MaterialPropertyBlock _instance;
        public static MaterialPropertyBlock Instance => _instance ??= new MaterialPropertyBlock();
    }
}
