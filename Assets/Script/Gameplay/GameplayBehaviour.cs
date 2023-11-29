using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace YARG.Gameplay
{
    /// <summary>
    /// A <see cref="MonoBehaviour"/> that interacts with the <see cref="GameManager"/>.
    /// </summary>
    /// <remarks>
    /// This class provides guarantees regarding using <see cref="GameManager"/>, such as disabling updates
    /// until it's finished loading, and also provides hooks for various events.
    /// <br/>
    /// When inheriting, do *NOT* use <c>Awake()</c> or <c>OnDestroy()</c>!
    /// Override the <see cref="GameplayAwake"/> and <see cref="GameplayDestroy"/> methods instead.
    /// </remarks>
    public abstract class GameplayBehaviour : GameManager.GameplayBehaviourImpl
    {
        // Empty class for exposing outside of GameManager
    }

    public partial class GameManager
    {
        // Private interface for interaction from GameManager
        private interface IGameplayBehaviour
        {
            Object UnityObject { get; } 

            UniTask GameplayLoad();
            UniTask GameplayStart();
            void GameplayUpdate();

            void SetPaused(bool paused);
            void SetSpeed(float speed);
            void SeekToTime(double songTime);
        }

        public abstract class GameplayBehaviourImpl : MonoBehaviour, IGameplayBehaviour
        {
            protected GameManager GameManager { get; private set; }

            private bool _enabled;

            // Protected to warn when hidden by an inheriting class
            // "The Unity message 'Awake' has an incorrect signature."
            [SuppressMessage("Type Safety", "UNT0006", Justification = "UniTaskVoid is a compatible return type.")]
            protected async UniTaskVoid Awake()
            {
                // Retrieve the game manager
                GameManager = FindObjectOfType<GameManager>();
                if (GameManager == null)
                {
                    Debug.LogWarning($"Gameplay object {gameObject.name} was instantiated outside of the gameplay scene!");
                    Destroy(gameObject);
                    return;
                }

                GameManager._gameplayBehaviours.Add(this);

                // Call Awake first to ensure everything is initialized,
                // otherwise setting `enabled` will call GameplayDisable first
                GameplayAwake();

                // Disable until the song starts
                enabled = GameManager.IsSongStarted;

                // Ensure initialization occurs even if the song manager has already started
                if (GameManager.IsSongStarted)
                {
                    await GameplayLoadAsync();
                    await GameplayStartAsync();
                }
            }

            // Protected to warn when hidden by an inheriting class
            protected void OnEnable()
            {
                _enabled = enabled;
                GameplayEnable();
            }

            // Protected to warn when hidden by an inheriting class
            protected void OnDisable()
            {
                _enabled = false;
                GameplayDisable();
            }

#if UNITY_EDITOR // Only used for detecting incorrect usages
            // Protected to warn when hidden by an inheriting class
            // "The Unity message 'Start' is empty."
            [SuppressMessage("Performance", "UNT0001", Justification = "Deliberately empty for usage detection.")]
            protected void Start() { }

            // Protected to warn when hidden by an inheriting class
            // "The Unity message 'Start' is empty."
            [SuppressMessage("Performance", "UNT0001", Justification = "Deliberately empty for usage detection.")]
            protected void Update() { }
#endif

            // Protected to warn when hidden by an inheriting class
            protected void OnDestroy()
            {
                GameplayDestroy();

                if (GameManager == null) return;

                // Handled by GameManager to make enumeration simpler
                // GameManager._gameplayBehaviours.Remove(this);
            }

            protected virtual void GameplayAwake() { }
            protected virtual void GameplayDestroy() { }

            protected virtual void GameplayEnable() { }
            protected virtual void GameplayDisable() { }

            // Private interface thunks
            Object IGameplayBehaviour.UnityObject => this;

            UniTask IGameplayBehaviour.GameplayLoad() => GameplayLoadAsync();

            UniTask IGameplayBehaviour.GameplayStart()
            {
                enabled = true;
                return GameplayStartAsync();
            }

            void IGameplayBehaviour.GameplayUpdate()
            {
                if (!_enabled) return;
                GameplayUpdate();
            }

            void IGameplayBehaviour.SetPaused(bool paused) => SetPaused(paused);
            void IGameplayBehaviour.SetSpeed(float speed) => SetSpeed(speed);
            void IGameplayBehaviour.SeekToTime(double songTime) => SeekToTime(songTime);

            // Default async implementations which call the synchronous versions
            protected virtual UniTask GameplayLoadAsync()
            {
                GameplayLoad();
                return UniTask.CompletedTask;
            }

            protected virtual UniTask GameplayStartAsync()
            {
                GameplayStart();
                return UniTask.CompletedTask;
            }

            protected virtual void GameplayLoad() { }
            protected virtual void GameplayStart() { }
            protected virtual void GameplayUpdate() { }

            protected virtual void SetPaused(bool paused) { }
            protected virtual void SetSpeed(float speed) { }
            protected virtual void SeekToTime(double songTime) { }
        }
    }
}