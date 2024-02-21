using System;
using YARG.Core.Song;

namespace YARG.Integration
{
    public static class GameStateFetcher
    {
        public struct State
        {
            public SceneIndex CurrentScene;
            public SongEntry SongEntry;
            public bool Paused;
        }

        public static event Action<State> GameStateChange;

        private static State _current;

        public static void SetSceneIndex(SceneIndex scene)
        {
            _current = new State
            {
                CurrentScene = scene,
                SongEntry = null,
                Paused = false
            };

            GameStateChange?.Invoke(_current);
        }

        public static void SetSongEntry(SongEntry metadata)
        {
            _current.SongEntry = metadata;

            GameStateChange?.Invoke(_current);
        }

        public static void SetPaused(bool paused)
        {
            _current.Paused = paused;

            GameStateChange?.Invoke(_current);
        }
    }
}