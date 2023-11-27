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
    /// until it's finished loading, and also provides hooks for initialization as it loads.
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
        // Private interface for initialization from GameManager
        private interface IGameplayBehaviour
        {
            UniTask GameplayLoad();
            UniTask GameplayStart();
        }

        public abstract class GameplayBehaviourImpl : MonoBehaviour, IGameplayBehaviour
        {
            protected GameManager GameManager { get; private set; }

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

                // Disable until the song starts
                enabled = GameManager.IsSongStarted;

                GameplayAwake();

                // Ensure initialization occurs even if the song manager has already started
                if (GameManager.IsSongStarted)
                {
                    await GameplayLoadAsync();
                    await GameplayStartAsync();
                }
            }

#if UNITY_EDITOR // Only used for detecting incorrect usages
            // Protected to warn when hidden by an inheriting class
            // "The Unity message 'Start' is empty."
            [SuppressMessage("Performance", "UNT0001", Justification = "Deliberately empty for usage detection.")]
            protected void Start() { }
#endif

            // Protected to warn when hidden by an inheriting class
            protected void OnDestroy()
            {
                GameplayDestroy();

                if (GameManager == null) return;

                GameManager._gameplayBehaviours.Remove(this);
            }

            protected virtual void GameplayAwake()
            {
            }

            protected virtual void GameplayDestroy()
            {
            }

            // Private interface thunks for GameManager initialization
            UniTask IGameplayBehaviour.GameplayLoad()
            {
                return GameplayLoadAsync();
            }

            UniTask IGameplayBehaviour.GameplayStart()
            {
                enabled = true;
                return GameplayStartAsync();
            }

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

            protected virtual void GameplayLoad()
            {
            }

            protected virtual void GameplayStart()
            {
            }
        }
    }
}