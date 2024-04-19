using System;
using UnityEngine;
using UnityEngine.Rendering;
using YARG.Core.Logging;

namespace YARG.GraphicsTest.Instancing
{
    public abstract class MeshInstancer : IDisposable
    {
        /// <summary>
        /// The maximum length that array material properties can have.
        /// </summary>
        protected const int ARRAY_LIMIT = 1023;

        protected readonly Mesh _mesh;
        protected readonly Material _material;

        protected readonly MaterialPropertyBlock _properties = new();

        protected int _layer = 0;

        protected ShadowCastingMode _shadowMode = ShadowCastingMode.On;
        protected bool _receiveShadows = true;

        protected LightProbeUsage _lightProbing = LightProbeUsage.BlendProbes;
        protected LightProbeProxyVolume _lightProxy = null;

        private readonly int _instanceLimit;

        public int InstanceCount { get; private set; }
        public bool AtCapacity => InstanceCount >= _instanceLimit;

        public MeshInstancer(Mesh mesh, Material material, int instanceLimit,
            int layer = 0, ShadowCastingMode shadowMode = ShadowCastingMode.On, bool receiveShadows = true,
            LightProbeUsage lightProbing = LightProbeUsage.BlendProbes, LightProbeProxyVolume lightProxy = null)
        {
            _mesh = mesh;
            _material = material;
            _instanceLimit = instanceLimit;

            _layer = layer;

            _shadowMode = shadowMode;
            _receiveShadows = receiveShadows;

            _lightProbing = lightProbing;
            _lightProxy = lightProxy;
        }

        ~MeshInstancer() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
                DisposeManaged();
            DisposeUnmanaged();
        }

        protected virtual void DisposeManaged() {}
        protected virtual void DisposeUnmanaged() {}

        public void BeginDraw()
        {
            InstanceCount = 0;
            OnBeginDraw();
        }

        public void EndDraw()
        {
            if (InstanceCount < 1)
                return;

            OnEndDraw();
        }

        public void RenderInstance(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            if (InstanceCount >= _instanceLimit)
            {
                YargLogger.LogDebug("Attempted to add an instance above the limit!");
                return;
            }

            OnRenderInstance(position, rotation, scale, color);
            InstanceCount++;
        }

        protected virtual void OnBeginDraw() {}
        protected abstract void OnEndDraw();

        protected abstract void OnRenderInstance(Vector3 position, Quaternion rotation, Vector3 scale, Color color);
    }
}