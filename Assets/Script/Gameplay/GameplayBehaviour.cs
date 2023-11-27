using System.Diagnostics.CodeAnalysis;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Chart;

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
            UniTask OnChartLoaded(SongChart chart);
            UniTask OnSongLoaded();
            UniTask OnSongStarted();
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
                    await OnChartLoadedAsync(GameManager.Chart);
                    await OnSongLoadedAsync();
                    await OnSongStartedAsync();
                }
            }

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
            UniTask IGameplayBehaviour.OnChartLoaded(SongChart chart)
            {
                return OnChartLoadedAsync(chart);
            }

            UniTask IGameplayBehaviour.OnSongLoaded()
            {
                return OnSongLoadedAsync();
            }

            UniTask IGameplayBehaviour.OnSongStarted()
            {
                enabled = true;
                return OnSongStartedAsync();
            }

            // Default async implementations which call the synchronous versions
            protected virtual UniTask OnChartLoadedAsync(SongChart chart)
            {
                OnChartLoaded(chart);
                return UniTask.CompletedTask;
            }

            protected virtual UniTask OnSongLoadedAsync()
            {
                OnSongLoaded();
                return UniTask.CompletedTask;
            }

            protected virtual UniTask OnSongStartedAsync()
            {
                OnSongStarted();
                return UniTask.CompletedTask;
            }

            protected virtual void OnChartLoaded(SongChart chart)
            {
            }

            protected virtual void OnSongLoaded()
            {
            }

            protected virtual void OnSongStarted()
            {
            }
        }
    }
}