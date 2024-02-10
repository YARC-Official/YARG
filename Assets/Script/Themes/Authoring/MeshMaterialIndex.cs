using System;
using UnityEngine;

namespace YARG.Themes
{
    [Serializable]
    public struct MeshMaterialIndex
    {
        public MeshRenderer Mesh;
        public int MaterialIndex;
    }

    [Serializable]
    public struct MeshEmissionMaterialIndex
    {
        public MeshRenderer Mesh;
        public int MaterialIndex;

        [Space]
        public float EmissionMultiplier;
        public float EmissionAddition;
    }
}