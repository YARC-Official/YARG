using System;

namespace Mackiloha
{
    public struct Matrix4
    {
        public float M11;
        public float M12;
        public float M13;
        public float M14;

        public float M21;
        public float M22;
        public float M23;
        public float M24;

        public float M31;
        public float M32;
        public float M33;
        public float M34;

        public float M41;
        public float M42;
        public float M43;
        public float M44;

        public static Matrix4 Identity()
        {
            return new Matrix4()
            {
                M11 = 1.0f,
                M22 = 1.0f,
                M33 = 1.0f,
                M44 = 1.0f
            };
        }
    }
}
