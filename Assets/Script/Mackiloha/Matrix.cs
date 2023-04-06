using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mackiloha
{
    public struct Matrix
    {
        /*             (Column Major Order)
         *  |       X       Y       Z       W
         *  |       1       0       0       0   Right
         *  |       0       1       0       0   Up
         *  |       0       0       1       0   Forward
         *  |       0       0       0       1   Position
         */
        
        public static Matrix Identity()
        {
            return new Matrix()
            {
                M11 = 1.0f,
                M22 = 1.0f,
                M33 = 1.0f,
                M44 = 1.0f
            };
        }

        public Matrix Transpose() => new Matrix()
        {
            M11 = this.M11,
            M12 = this.M21,
            M13 = this.M31,
            M14 = this.M41,

            M21 = this.M12,
            M22 = this.M22,
            M23 = this.M32,
            M24 = this.M42,

            M31 = this.M13,
            M32 = this.M23,
            M33 = this.M33,
            M34 = this.M43,

            M41 = this.M14,
            M42 = this.M24,
            M43 = this.M34,
            M44 = this.M44
        };

        public float[,] GetRawMatrix => new float[,]
        {
            { M11, M12, M13, M14 },
            { M21, M22, M23, M24 },
            { M31, M32, M33, M34 },
            { M41, M42, M43, M44 },
        };

        public static Matrix FromStream(AwesomeReader ar)
        {
            // Reads from stream, usually embedded inside milo directories and mesh files
            Matrix mat = new Matrix();

            mat.M11 = ar.ReadSingle(); // M11
            mat.M12 = ar.ReadSingle(); // M12
            mat.M13 = ar.ReadSingle(); // M13

            mat.M21 = ar.ReadSingle(); // M21
            mat.M22 = ar.ReadSingle(); // M22
            mat.M23 = ar.ReadSingle(); // M23

            mat.M31 = ar.ReadSingle(); // M31
            mat.M32 = ar.ReadSingle(); // M32
            mat.M33 = ar.ReadSingle(); // M33

            mat.M41 = ar.ReadSingle(); // M41
            mat.M42 = ar.ReadSingle(); // M42
            mat.M43 = ar.ReadSingle(); // M43
            mat.M44 = 1.0f;            // M44 - Implicit

            return mat;
        }

        public static Matrix operator *(Matrix a, Matrix b) => new Matrix()
        {
            M11 = (a.M11 * b.M11) + (a.M12 * b.M21) + (a.M13 * b.M31) + (a.M14 * b.M41),
            M12 = (a.M11 * b.M12) + (a.M12 * b.M22) + (a.M13 * b.M32) + (a.M14 * b.M42),
            M13 = (a.M11 * b.M13) + (a.M12 * b.M23) + (a.M13 * b.M33) + (a.M14 * b.M43),
            M14 = (a.M11 * b.M14) + (a.M12 * b.M24) + (a.M13 * b.M34) + (a.M14 * b.M44),

            M21 = (a.M21 * b.M11) + (a.M22 * b.M21) + (a.M23 * b.M31) + (a.M24 * b.M41),
            M22 = (a.M21 * b.M12) + (a.M22 * b.M22) + (a.M23 * b.M32) + (a.M24 * b.M42),
            M23 = (a.M21 * b.M13) + (a.M22 * b.M23) + (a.M23 * b.M33) + (a.M24 * b.M43),
            M24 = (a.M21 * b.M14) + (a.M22 * b.M24) + (a.M23 * b.M34) + (a.M24 * b.M44),

            M31 = (a.M31 * b.M11) + (a.M32 * b.M21) + (a.M33 * b.M31) + (a.M34 * b.M41),
            M32 = (a.M31 * b.M12) + (a.M32 * b.M22) + (a.M33 * b.M32) + (a.M34 * b.M42),
            M33 = (a.M31 * b.M13) + (a.M32 * b.M23) + (a.M33 * b.M33) + (a.M34 * b.M43),
            M34 = (a.M31 * b.M14) + (a.M32 * b.M24) + (a.M33 * b.M34) + (a.M34 * b.M44),

            M41 = (a.M41 * b.M11) + (a.M42 * b.M21) + (a.M43 * b.M31) + (a.M44 * b.M41),
            M42 = (a.M41 * b.M12) + (a.M42 * b.M22) + (a.M43 * b.M32) + (a.M44 * b.M42),
            M43 = (a.M41 * b.M13) + (a.M42 * b.M23) + (a.M43 * b.M33) + (a.M44 * b.M43),
            M44 = (a.M41 * b.M14) + (a.M42 * b.M24) + (a.M43 * b.M34) + (a.M44 * b.M44)
        };
        
        public float M11; // Right
        public float M12;
        public float M13;
        public float M14;

        public float M21; // Up
        public float M22;
        public float M23;
        public float M24;

        public float M31; // Forward
        public float M32;
        public float M33;
        public float M34;

        public float M41; // Position
        public float M42;
        public float M43;
        public float M44;
    }
}
