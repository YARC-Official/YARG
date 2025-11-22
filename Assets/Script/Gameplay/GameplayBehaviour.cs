using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Gameplay
{
    /// <summary>
    /// A <see cref="MonoBehaviour"/> that interacts with the <see cref="Gameplay.GameManager"/>.
    /// </summary>
    /// <remarks>
    /// This class guarantees that no updates will occur until the song has started, and
    /// ensures correct handling of the chart load and song start events.
    /// <br/>
    /// When inheriting, do *NOT* use <c>Awake()</c> or <c>OnDestroy()</c>!
    /// Override the <see cref="GameplayAwake"/> and <see cref="GameplayDestroy"/> methods instead.
    /// </remarks>
    public abstract class GameplayBehaviour : MonoBehaviour
    {
        protected GameManager GameManager { get; private set; }

        // Protected to warn when hidden by an inheriting class
        protected void Awake()
        {
            // Retrieve the game manager
            GameManager = FindAnyObjectByType<GameManager>();
            if (GameManager == null)
            {
                YargLogger.LogFormatWarning("Gameplay object {0} was instantiated outside of the gameplay scene!", gameObject.name);
                Destroy(gameObject);
                return;
            }

            // Disable until the song starts
            // (disable before registering, so that if it's already loaded
            // we're not stuck as disabled from it immediately executing the method)
            enabled = GameManager.IsSongStarted;

            GameManager.ChartLoaded += _OnChartLoaded;
            GameManager.SongLoaded += _OnSongLoaded;
            GameManager.SongStarted += _OnSongStarted;

            GameplayAwake();
        }

        // Protected to warn when hidden by an inheriting class
        protected void OnDestroy()
        {
            // We specifically check if GameManager is *reference* null here,
            // as it gets destroyed before GameplayBehaviours do
            if (ReferenceEquals(GameManager, null))
                return;

            GameplayDestroy();

            GameManager.ChartLoaded -= _OnChartLoaded;
            GameManager.SongLoaded -= _OnSongLoaded;
            GameManager.SongStarted -= _OnSongStarted;
        }

        private void _OnChartLoaded(SongChart chart)
        {
            GameManager.ChartLoaded -= _OnChartLoaded;

            OnChartLoaded(chart);
        }

        private void _OnSongLoaded()
        {
            GameManager.SongLoaded -= _OnSongLoaded;

            OnSongLoaded();
        }

        private void _OnSongStarted()
        {
            GameManager.SongStarted -= _OnSongStarted;
            enabled = true;

            OnSongStarted();
        }

        protected virtual void GameplayAwake()
        {
        }

        protected virtual void GameplayDestroy()
        {
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