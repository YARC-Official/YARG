﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using TrombLoader.Helpers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Chart;
using YARG.Data;
using YARG.Input;
using YARG.Serialization.Parser;
using YARG.Settings;
using YARG.Song;
using YARG.UI;
using YARG.Venue;

namespace YARG.PlayMode
{
    public class Play : MonoBehaviour
    {
        public static Play Instance { get; private set; }

        public static float speed = 1f;

        public const float SONG_START_OFFSET = -2f;
        public const float SONG_END_DELAY = 2f;

        public delegate void BeatAction();

        public static event BeatAction BeatEvent;

        public delegate void SongStateChangeAction(SongEntry songInfo);

        public static event SongStateChangeAction OnSongStart;
        public static event SongStateChangeAction OnSongEnd;

        public delegate void PauseStateChangeAction(bool pause);

        public static event PauseStateChangeAction OnPauseToggle;

        public bool SongStarted { get; private set; }

        [field: SerializeField]
        public Camera DefaultCamera { get; private set; }

        private OccurrenceList<string> audioLowering = new();
        private OccurrenceList<string> audioReverb = new();
        private Dictionary<SongStem, (float percent, bool enabled)> audioPitchBend = new(
            AudioHelpers.PitchBendAllowedStems.Select((stem) => new KeyValuePair<SongStem, (float, bool)>(stem, (0f, false)))
        );

        private int stemsReverbed;

        private bool audioRunning;
        private float realSongTime;
        public float SongTime => realSongTime - PlayerManager.AudioCalibration * speed - (float) Song.Delay;

        private float audioLength;
        public float SongLength { get; private set; }

        public YargChart chart;

        [Space]
        [SerializeField]
        private GameObject playResultScreen;

        [SerializeField]
        private RawImage playCover;

        [SerializeField]
        private GameObject scoreDisplay;

        private int beatIndex = 0;

        // tempo (updated throughout play)
        public float CurrentBeatsPerSecond { get; private set; } = 0f;
        public float CurrentTempo => CurrentBeatsPerSecond * 60; // BPM

        private List<AbstractTrack> _tracks;

        public bool endReached { get; private set; } = false;

        private bool _paused = false;

        public bool Paused
        {
            get => _paused;
            set
            {
                // disable pausing once we reach end of song
                if (endReached) return;

                _paused = value;

                GameUI.Instance.pauseMenu.SetActive(value);

                if (value)
                {
                    Time.timeScale = 0f;

                    GameManager.AudioManager.Pause();

                    if (GameUI.Instance.videoPlayer.enabled)
                    {
                        GameUI.Instance.videoPlayer.Pause();
                    }
                }
                else
                {
                    Time.timeScale = 1f;

                    GameManager.AudioManager.Play();

                    if (GameUI.Instance.videoPlayer.enabled)
                    {
                        GameUI.Instance.videoPlayer.Play();
                    }
                }

                OnPauseToggle?.Invoke(_paused);
            }
        }

        public SongEntry Song => GameManager.Instance.SelectedSong;

        private bool playingRhythm = false;
        private bool playingVocals = false;

        private void Awake()
        {
            Instance = this;

            ScoreKeeper.Reset();
            StarScoreKeeper.Reset();

            // Force the music player to disable, and hide the help bar
            // This is mostly for "Test Play" mode
            Navigator.Instance.ForceHideMusicPlayer();
            Navigator.Instance.PopAllSchemes();

            // Song
            StartSong();
        }

        private void StartSong()
        {
            GameUI.Instance.SetLoadingText("Loading audio...");
            Song.LoadAudio(GameManager.AudioManager, speed);

            // Get song length
            audioLength = GameManager.AudioManager.AudioLengthF;
            SongLength = audioLength;

            GameUI.Instance.SetLoadingText("Loading chart...");

            // Load chart (from midi, upgrades, etc.)
            LoadChart();

            // Adjust song length if needed
            // The [end] event is allowed to make the chart shorter (but not longer)
            for (int i = chart.events.Count - 1; i > 0; i--)
            {
                var chartEvent = chart.events[i];
                if (chartEvent.name != "end")
                {
                    continue;
                }

                if (chartEvent.time < SongLength)
                {
                    SongLength = chartEvent.time;
                    break;
                }
            }

            // The song length must include all notes in the chart
            foreach (var part in chart.AllParts)
            {
                foreach (var difficulty in part)
                {
                    if (difficulty.Count < 1)
                    {
                        continue;
                    }

                    var lastNote = difficulty[^1];
                    if (lastNote.EndTime > SongLength)
                    {
                        SongLength = lastNote.EndTime;
                    }
                }
            }

            // Finally, append some additional time so the song doesn't just end immediately
            SongLength += SONG_END_DELAY * speed;

            GameUI.Instance.SetLoadingText("Spawning tracks...");

            // Spawn tracks
            _tracks = new List<AbstractTrack>();
            int trackIndex = 0;
            foreach (var player in PlayerManager.players)
            {
                if (player.chosenInstrument == null)
                {
                    // Skip players that are sitting out
                    continue;
                }

                // Temporary, will make a better system later
                if (player.chosenInstrument == "rhythm")
                {
                    playingRhythm = true;
                }

                // Temporary, same here
                if (player.chosenInstrument is "vocals" or "harmVocals")
                {
                    playingVocals = true;
                }

                string trackPath = player.inputStrategy.GetTrackPath();

                if (trackPath == null)
                {
                    continue;
                }

                var prefab = Addressables.LoadAssetAsync<GameObject>(trackPath).WaitForCompletion();
                var track = Instantiate(prefab, new Vector3(trackIndex * 25f, 100f, 0f), prefab.transform.rotation);
                _tracks.Add(track.GetComponent<AbstractTrack>());
                _tracks[trackIndex].player = player;

                trackIndex++;
            }

            // Load background (venue, video, image, etc.)
            LoadBackground();

            SongStarted = true;

            // Hide loading screen
            GameUI.Instance.loadingContainer.SetActive(false);

            realSongTime = SONG_START_OFFSET * speed;
            StartCoroutine(StartAudio());

            OnSongStart?.Invoke(Song);
        }

        private void LoadBackground()
        {
            var typePathPair = VenueLoader.GetVenuePath(Song);
            if (typePathPair == null)
            {
                return;
            }

            var type = typePathPair.Value.Type;
            var path = typePathPair.Value.Path;

            switch (type)
            {
                case VenueType.Yarground:
                    var bundle = AssetBundle.LoadFromFile(path);

                    // KEEP THIS PATH LOWERCASE
                    // Breaks things for other platforms, because Unity
                    var bg = bundle.LoadAsset<GameObject>(BundleBackgroundManager.BACKGROUND_PREFAB_PATH
                        .ToLowerInvariant());

                    // Fix for non-Windows machines
                    // Probably there's a better way to do this.
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
					Renderer[] renderers = bg.GetComponentsInChildren<Renderer>();

					foreach (Renderer renderer in renderers) {
						Material[] materials = renderer.sharedMaterials;

						for (int i = 0; i < materials.Length; i++) {
							Material material = materials[i];
							material.shader = Shader.Find(material.shader.name);
						}
					}
#endif

                    var bgInstance = Instantiate(bg);

                    bgInstance.GetComponent<BundleBackgroundManager>().Bundle = bundle;
                    break;
                case VenueType.Video:
                    GameUI.Instance.videoPlayer.url = path;
                    GameUI.Instance.videoPlayer.enabled = true;
                    GameUI.Instance.videoPlayer.Prepare();
                    break;
                case VenueType.Image:
                    var png = ImageHelper.LoadTextureFromFile(path);
                    GameUI.Instance.background.texture = png;
                    break;
            }
        }

        private void LoadChart()
        {
            // Parse

            MoonSong moonSong = null;
            if (Song.NotesFile.EndsWith(".chart"))
            {
                Debug.Log("Reading .chart file");
                moonSong = ChartReader.ReadChart(Path.Combine(Song.Location, Song.NotesFile));
            }

            chart = new YargChart(moonSong);
            if (Song.NotesFile.EndsWith(".mid"))
            {
                // Parse
                var parser = new MidiParser(Song);
                chart.InitializeArrays();
                parser.Parse(chart);
            }
            else if (Song.NotesFile.EndsWith(".chart"))
            {
                var handler = new BeatHandler(moonSong);
                handler.GenerateBeats();
                chart.beats = handler.Beats;
            }

            // initialize current tempo
            if (chart.beats.Count > 2)
            {
                CurrentBeatsPerSecond = chart.beats[1].Time - chart.beats[0].Time;
            }
        }

        private IEnumerator StartAudio()
        {
            while (realSongTime < 0f)
            {
                // Wait until the song time is 0
                yield return null;
            }

            float? startVideoIn = null;
            if (GameUI.Instance.videoPlayer.enabled)
            {
                // Set the chart start offset here (if ini)
                if (Song is IniSongEntry ini)
                {
                    if (ini.VideoStartOffset < 0)
                    {
                        startVideoIn = Mathf.Abs(ini.VideoStartOffset / 1000f);
                    }
                    else
                    {
                        GameUI.Instance.videoPlayer.time = ini.VideoStartOffset / 1000.0;
                    }
                }

                // Play the video if a start time wasn't defined
                if (startVideoIn == null)
                {
                    GameUI.Instance.videoPlayer.Play();
                }
            }

            GameManager.AudioManager.Play();

            GameManager.AudioManager.SongEnd += OnEndReached;
            audioRunning = true;

            if (startVideoIn != null)
            {
                // Wait, then start on time
                yield return new WaitForSeconds(startVideoIn.Value);
                GameUI.Instance.videoPlayer.Play();
            }
        }

        private void Update()
        {
            if (!SongStarted)
            {
                return;
            }

            // Pausing
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Paused = !Paused;
            }

            if (Paused)
            {
                return;
            }

            // Update this every frame to make sure all notes are spawned at the same time.
            float audioTime = GameManager.AudioManager.CurrentPositionF;
            if (audioRunning && audioTime < audioLength)
            {
                realSongTime = audioTime;
            }
            else
            {
                // We need to update the song time ourselves if the audio finishes before the song actually ends
                realSongTime += Time.deltaTime * speed;
            }

            UpdateAudio(new[]
            {
                "guitar", "realGuitar"
            }, new[]
            {
                "guitar"
            });

            // Swap what tracks depending on what instrument is playing
            if (playingRhythm)
            {
                // Mute rhythm
                UpdateAudio(new[]
                {
                    "rhythm",
                }, new[]
                {
                    "rhythm"
                });

                // Mute bass
                UpdateAudio(new[]
                {
                    "bass", "realBass"
                }, new[]
                {
                    "bass",
                });
            }
            else
            {
                // Mute bass
                UpdateAudio(new[]
                {
                    "bass", "realBass"
                }, new[]
                {
                    "bass", "rhythm"
                });
            }

            // Mute keys
            UpdateAudio(new[]
            {
                "keys", "realKeys"
            }, new[]
            {
                "keys"
            });

            // Mute drums
            UpdateAudio(new[]
            {
                "drums", "realDrums"
            }, new[]
            {
                "drums", "drums_1", "drums_2", "drums_3", "drums_4"
            });

            // Update whammy pitch state
            UpdateWhammyPitch();

            // Update beats
            while (chart.beats.Count > beatIndex && chart.beats[beatIndex].Time <= SongTime)
            {
                foreach (var track in _tracks)
                {
                    if (!track.IsStarPowerActive || !GameManager.AudioManager.Options.UseStarpowerFx)
                        continue;

                    GameManager.AudioManager.PlaySoundEffect(SfxSample.Clap);
                    break;
                }

                BeatEvent?.Invoke();
                beatIndex++;

                if (beatIndex < chart.beats.Count)
                {
                    CurrentBeatsPerSecond = 1 / (chart.beats[beatIndex].Time - chart.beats[beatIndex - 1].Time);
                }
            }

            // End song
            if (!endReached && realSongTime >= SongLength)
            {
                endReached = true;
                StartCoroutine(EndSong(true));
            }
        }

        private void OnEndReached()
        {
            audioLength = GameManager.AudioManager.CurrentPositionF;
            audioRunning = false;
        }

        private void UpdateAudio(string[] trackNames, string[] stemNames)
        {
            if (SettingsManager.Settings.MuteOnMiss.Data)
            {
                // Get total amount of players with the instrument (and the amount lowered)
                int amountWithInstrument = 0;
                int amountLowered = 0;

                for (int i = 0; i < trackNames.Length; i++)
                {
                    amountWithInstrument += PlayerManager.PlayersWithInstrument(trackNames[i]);
                    amountLowered += audioLowering.GetCount(trackNames[i]);
                }

                // Skip if no one is playing the instrument
                if (amountWithInstrument <= 0)
                {
                    return;
                }

                // Lower all volumes to a minimum of 5%
                float percent = 1f - (float) amountLowered / amountWithInstrument;
                foreach (var name in stemNames)
                {
                    var stem = AudioHelpers.GetStemFromName(name);

                    GameManager.AudioManager.SetStemVolume(stem, percent * 0.95f + 0.05f);
                }
            }

            // Reverb audio with starpower

            if (GameManager.AudioManager.Options.UseStarpowerFx)
            {
                GameManager.AudioManager.ApplyReverb(SongStem.Song, stemsReverbed > 0);

                foreach (var name in stemNames)
                {
                    var stem = AudioHelpers.GetStemFromName(name);

                    bool applyReverb = audioReverb.GetCount(name) > 0;

                    // Drums have multiple stems so need to reverb them all if it is drums
                    switch (stem)
                    {
                        case SongStem.Drums:
                            GameManager.AudioManager.ApplyReverb(SongStem.Drums, applyReverb);
                            GameManager.AudioManager.ApplyReverb(SongStem.Drums1, applyReverb);
                            GameManager.AudioManager.ApplyReverb(SongStem.Drums2, applyReverb);
                            GameManager.AudioManager.ApplyReverb(SongStem.Drums3, applyReverb);
                            GameManager.AudioManager.ApplyReverb(SongStem.Drums4, applyReverb);
                            break;
                        default:
                            GameManager.AudioManager.ApplyReverb(stem, applyReverb);
                            break;
                    }
                }
            }
        }

        public IEnumerator EndSong(bool showResultScreen)
        {
            // Dispose of all audio
            GameManager.AudioManager.SongEnd -= OnEndReached;
            GameManager.AudioManager.UnloadSong();

            // Call events
            OnSongEnd?.Invoke(Song);

            // Unpause just in case
            Time.timeScale = 1f;

            OnSongEnd?.Invoke(Song);

            // run animation + save if we've reached end of song
            if (showResultScreen)
            {
                yield return playCover
                    .DOFade(1f, 1f)
                    .WaitForCompletion();

                // save scores and destroy tracks
                foreach (var track in _tracks)
                {
                    track.SetPlayerScore();
                    Destroy(track.gameObject);
                }

                _tracks.Clear();
                // save MicPlayer score and destroy it
                if (MicPlayer.Instance != null)
                {
                    MicPlayer.Instance.SetPlayerScore();
                    Destroy(MicPlayer.Instance.gameObject);
                }

                // show play result screen; this is our main focus now
                playResultScreen.SetActive(true);
            }

            scoreDisplay.SetActive(false);
        }

        public void LowerAudio(string name)
        {
            audioLowering.Add(name);
        }

        public void RaiseAudio(string name)
        {
            audioLowering.Remove(name);
        }

        public void ReverbAudio(string name, bool apply)
        {
            if (apply)
            {
                stemsReverbed++;
                audioReverb.Add(name);
            }
            else
            {
                stemsReverbed--;
                audioReverb.Remove(name);
            }
        }

        public void TrackWhammyPitch(string name, float delta, bool enable)
        {
            var stem = name switch
            {
                "guitar" or "realGuitar" => SongStem.Guitar,
                "bass" or "realBass" => SongStem.Bass,
                "rhythm" => SongStem.Rhythm,
                _ => SongStem.Song
            };
            if (!audioPitchBend.TryGetValue(stem, out var current))
                return;

            // Accumulate delta
            // We take in a delta value to account for multiple players on the same part,
            // if we used absolute then there would be no way to prevent the pitch jittering
            // due to two players whammying at the same time
            current.percent += delta;
            current.enabled = enable;
            audioPitchBend[stem] = current;
        }

        private void UpdateWhammyPitch()
        {
            // Set pitch bend
            foreach (var (stem, current) in audioPitchBend)
            {
                float percent = current.enabled ? Mathf.Clamp(current.percent, 0f, 1f) : 0f;
                // The pitch is always set regardless of the enable state, seems like it prevents
                // issues with whammiable stems becoming muddy over time
                GameManager.AudioManager.SetWhammyPitch(stem, percent);
            }
        }

        public void Exit(bool toSongSelect = true)
        {
            if (!endReached)
            {
                endReached = true;
                StartCoroutine(EndSong(false));
            }

            MainMenu.showSongSelect = toSongSelect;
            GameManager.Instance.LoadScene(SceneIndex.MENU);
        }
    }
}