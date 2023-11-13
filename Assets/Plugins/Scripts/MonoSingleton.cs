namespace UnityEngine
{
    /// <summary>
    /// A singleton instance of a <see cref="MonoBehaviour"/>.
    /// </summary>
    /// <remarks>
    /// When inheriting, do *NOT* use <c>Awake()</c> or <c>OnDestroy()</c>!
    /// Override the <see cref="SingletonAwake"/> and <see cref="SingletonDestroy"/> methods instead.
    /// </remarks>
    public abstract class MonoSingleton<TBehaviour> : MonoBehaviour
        where TBehaviour : MonoSingleton<TBehaviour>
    {
        /// <summary>
        /// The singleton instance for <typeparamref name="TBehaviour"/>.
        /// </summary>
        public static TBehaviour Instance { get; private set; } = null;

        private void Awake()
        {
            // Destroy if duplicate
            if (Instance != null)
            {
                Debug.LogWarning($"Duplicate {GetType()} instance was created! Attached to game object {gameObject.name}", this);
                Destroy(gameObject);
                return;
            }

            // Initialize and assign the instance
            Instance = (TBehaviour)this;
            SingletonAwake();
        }

        private void OnDestroy()
        {
            // Ignore if duplicate
            if (Instance != this)
                return;

            // Destroy and remove the instance
            Instance = null;
            SingletonDestroy();
        }

        /// <summary>
        /// Called when the singleton instance is initializing.
        /// </summary>
        protected virtual void SingletonAwake() { }

        /// <summary>
        /// Called when the singleton instance is being destroyed.
        /// </summary>
        protected virtual void SingletonDestroy() { }
    }
}