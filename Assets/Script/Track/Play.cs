using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using TrombLoader.Helpers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Data;
using YARG.Gameplay;
using YARG.Player.Navigation;
using YARG.Serialization.Parser;
using YARG.Settings;
using YARG.Song;
using YARG.Menu;
using YARG.Venue;

namespace YARG.PlayMode
{
    /*
     * THIS IS A DUMMY CLASS FOR NOW
     */

    public class Play : MonoBehaviour
    {
        public static Play Instance { get; private set; }

        public static float speed = 1f;

        public const float SONG_START_OFFSET = -2f;
        public const float SONG_END_DELAY = 2f;

        public delegate void BeatAction();

        public static event BeatAction BeatEvent;

        public delegate void SongStateChangeAction(SongEntry songInfo);

        private static event SongStateChangeAction _onSongStart;
        public static event SongStateChangeAction OnSongStart
        {
            add
            {
                _onSongStart += value;

                // Invoke now if already started, this event is only fired once
                if (Instance?.SongStarted ?? false)
                    value?.Invoke(Instance.Song);
            }
            remove => _onSongStart -= value;
        }

        private static event SongStateChangeAction _onSongEnd;
        public static event SongStateChangeAction OnSongEnd
        {
            add
            {
                _onSongEnd += value;

                // Invoke now if already ended, this event is only fired once
                if (Instance?.endReached ?? false)
                    value?.Invoke(Instance.Song);
            }
            remove => _onSongEnd -= value;
        }

        // public delegate void ChartLoadAction(SongChart chart);

        // private static event ChartLoadAction _onChartLoaded;
        // public static event ChartLoadAction OnChartLoaded
        // {
        //     add
        //     {
        //         _onChartLoaded += value;
                
        //         // Invoke now if already loaded, this event is only fired once
        //         var chart = Instance?.chart;
        //         if (chart != null)
        //             value?.Invoke(chart);
        //     }
        //     remove => _onChartLoaded -= value;
        // }

        public delegate void PauseStateChangeAction(bool pause);

        public static event PauseStateChangeAction OnPauseToggle;

        public bool SongStarted { get; private set; }

        public float SongLength { get; private set; }
        public float SongTime { get; private set; }

        [field: SerializeField]
        public Camera DefaultCamera { get; private set; }

        // tempo (updated throughout play)
        public float CurrentBeatsPerSecond { get; private set; } = 0f;
        public float CurrentTempo => CurrentBeatsPerSecond * 60; // BPM

        // private List<AbstractTrack> _tracks;

        public bool endReached { get; private set; } = false;

        private bool _paused = false;

        public bool Paused
        {
            get => _paused;
            set
            {
                // // disable pausing once we reach end of song
                // if (endReached) return;
                //
                // _paused = value;
                //
                // GameUI.Instance.pauseMenu.SetActive(value);
                //
                // if (value)
                // {
                //     Time.timeScale = 0f;
                //
                //     GlobalVariables.AudioManager.Pause();
                //
                //     if (GameUI.Instance.videoPlayer.enabled)
                //     {
                //         GameUI.Instance.videoPlayer.Pause();
                //     }
                // }
                // else
                // {
                //     Time.timeScale = 1f;
                //
                //     GlobalVariables.AudioManager.Play();
                //
                //     if (GameUI.Instance.videoPlayer.enabled)
                //     {
                //         GameUI.Instance.videoPlayer.Play();
                //     }
                // }
                //
                // OnPauseToggle?.Invoke(_paused);
            }
        }

        public SongEntry Song => GlobalVariables.Instance.CurrentSong;

        private void Awake()
        {
            Instance = this;
        }

        public IEnumerator EndSong(bool showResultScreen)
        {
            // // Dispose of all audio
            // GlobalVariables.AudioManager.SongEnd -= OnEndReached;
            // GlobalVariables.AudioManager.UnloadSong();
            //
            // // Unpause just in case
            // Time.timeScale = 1f;
            //
            // // Call events
            // _onSongEnd?.Invoke(Song);
            //
            // // run animation + save if we've reached end of song
            // if (showResultScreen)
            // {
            //     yield return playCover
            //         .DOFade(1f, 1f)
            //         .WaitForCompletion();
            //
            //     // save scores and destroy tracks
            //     foreach (var track in _tracks)
            //     {
            //         track.SetPlayerScore();
            //         Destroy(track.gameObject);
            //     }
            //
            //     _tracks.Clear();
            //     // save MicPlayer score and destroy it
            //     if (MicPlayer.Instance != null)
            //     {
            //         MicPlayer.Instance.SetPlayerScore();
            //         Destroy(MicPlayer.Instance.gameObject);
            //     }
            //
            //     // show play result screen; this is our main focus now
            //     // playResultScreen.SetActive(true);
            // }
            //
            // // scoreDisplay.SetActive(false);
            yield return null;
        }

        public void LowerAudio(string name)
        {
            // audioLowering.Add(name);
        }

        public void RaiseAudio(string name)
        {
            // audioLowering.Remove(name);
        }

        public void ReverbAudio(string name, bool apply)
        {
            // if (apply)
            // {
            //     stemsReverbed++;
            //     audioReverb.Add(name);
            // }
            // else
            // {
            //     stemsReverbed--;
            //     audioReverb.Remove(name);
            // }
        }

        public void TrackWhammyPitch(string name, float delta, bool enable)
        {
            // var stem = name switch
            // {
            //     "guitar" or "realGuitar" => SongStem.Guitar,
            //     "bass" or "realBass" => SongStem.Bass,
            //     "rhythm" => SongStem.Rhythm,
            //     _ => SongStem.Song
            // };
            // if (!audioPitchBend.TryGetValue(stem, out var current))
            //     return;
            //
            // // Accumulate delta
            // // We take in a delta value to account for multiple players on the same part,
            // // if we used absolute then there would be no way to prevent the pitch jittering
            // // due to two players whammying at the same time
            // current.percent += delta;
            // current.enabled = enable;
            // audioPitchBend[stem] = current;
        }

        public void Exit(bool toSongSelect = true)
        {
            if (!endReached)
            {
                endReached = true;
                StartCoroutine(EndSong(false));
            }

            // MainMenu.showSongSelect = toSongSelect;
            GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
        }
    }
}