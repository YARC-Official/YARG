namespace YARG.Gameplay.Visuals
{
    public abstract class BaseElement : GameplayBehaviour, IPoolable
    {
        public Pool ParentPool { get; set; }

        /// <summary>
        /// The time at which the element resides.
        /// </summary>
        public abstract double ElementTime { get; }
        /// <summary>
        /// How many move units the element should be kept for past the default remove point.
        /// Should be positive.
        /// </summary>
        protected virtual float RemovePointOffset => 0f;

        protected bool Initialized { get; private set; }

        protected override void GameplayAwake()
        {
            // Hide everything at the start
            HideElement();
        }

        public void EnableFromPool()
        {
            gameObject.SetActive(true);

            InitializeElement();
            Initialized = true;

            // Force update the position once just in case to prevent flickering
            Update();
        }

        protected abstract void InitializeElement();
        protected abstract void UpdateElement();
        protected abstract void HideElement();

        protected abstract bool UpdateElementPosition();

        protected void Update()
        {
            // Skip if not initialized
            if (!Initialized) return;

            if (!UpdateElementPosition())
            {
                return;
            }

            UpdateElement();
        }

        public virtual void DisableIntoPool()
        {
            HideElement();

            Initialized = false;
            gameObject.SetActive(false);
        }
    }
}