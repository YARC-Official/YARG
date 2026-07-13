using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay;

namespace YARG.Venue
{
    public class VenueAnimator : GameplayBehaviour
    {
        public Animator _lightingAnimator;
        public Animator _postProcessingAnimator;
        public Animator _stageFXAnimator;
        public Animator _crowdAnimator;
        public Animator _cameraAnimator;
        public Animator _beatlineAnimator;
        public Animator _happinessAnimator;
        public Animator _guitarAnimator;
        public Animator _proGuitarAnimator;
        public Animator _bassAnimator;
        public Animator _proBassAnimator;
        public Animator _drumAnimator;
        public Animator _drumAnimAnimator;
        public Animator _keysAnimator;
        public Animator _proKeysAnimator;
        public Animator _vocalAnimator;
        public Animator _harmony1Animator;
        public Animator _harmony2Animator;

        //toggles
        public bool _lightingEnable;
        public bool _postProcessingEnable;
        public bool _stageFXEnable;
        public bool _crowdEnable;
        public bool _cameraEnable;
        public bool _beatlineEnable;
        public bool _happinessEnable;
        public bool _guitarNotesEnable;
        public bool _proGuitarNotesEnable;
        public bool _bassNotesEnable;
        public bool _proBassNotesEnable;
        public bool _drumNotesEnable;
        public bool _drumAnimEnable;
        public bool _keysNotesEnable;
        public bool _proKeysNotesEnable;
        public bool _vocalNotesEnable;
        public bool _harmony1NotesEnable;
        public bool _harmony2NotesEnable;

        //settings
        public int   _animationBPM = 120;
        public float _BPMAdjust    = 1;
        public int   _leadingFramesLighting;
        public int   _leadingFramesPostProcessing;
        public int   _leadingFramesStage;
        public int   _leadingFramesCrowd;
        public int   _leadingFramesCamera;
        public int   _leadingFramesBeatline;
        public int   _leadingFramesGuitar;
        public int   _leadingFramesProGuitar;
        public int   _leadingFramesBass;
        public int   _leadingFramesProBass;
        public int   _leadingFramesDrums;
        public int   _leadingFramesDrumAnim;
        public int   _leadingFramesKeys;
        public int   _leadingFramesProKeys;
        public int   _leadingFramesVocals;
        public int   _leadingFramesHarmony1;
        public int   _leadingFramesHarmony2;

        private static readonly List<string> GuitarNoteNames = new()
        {
            "gGreen",
            "gRed",
            "gYellow",
            "gBlue",
            "gOrange",
            "gBlack",
            "gOpen"
        };

        private static readonly List<string> ProGuitarStringNames = new()
        {
            "ELo",
            "A",
            "D",
            "G",
            "B",
            "EHi"
        };

        private static readonly List<string> BassNoteNames = new()
        {
            "bGreen",
            "bRed",
            "bYellow",
            "bBlue",
            "bOrange",
            "bBlack",
            "bOpen"
        };

        private static readonly List<string> DrumNoteNames = new()
        {
            "dKick",
            "dRed",
            "dYellow",
            "dBlue",
            "dGreen",
            "dYellowCym",
            "dBlueCym",
            "dGreenCym"
        };

        private static readonly List<string> KeysNoteNames = new()
        {
            "kGreen",
            "kRed",
            "kYellow",
            "kBlue",
            "kOrange"
        };

        private static readonly List<string> ProKeysNoteNames = new()
        {
            "C3",
            "C3#",
            "D3",
            "E3b",
            "E3",
            "F3",
            "F3#",
            "G3",
            "G3#",
            "A3",
            "B3b",
            "B3",
            "C4",
            "C4#",
            "D4",
            "E4b",
            "E4",
            "F4",
            "F4#",
            "G4",
            "G4#",
            "A4",
            "B4b",
            "B4",
            "C5"
        };

        private static readonly List<string> VocalNoteNames = new()
        {
            "Unpitched",
            "C1",
            "C1#",
            "D1",
            "E1b",
            "E1",
            "F1",
            "F1#",
            "G1",
            "G1#",
            "A1",
            "B1b",
            "B1",
            "C2",
            "C2#",
            "D2",
            "E2b",
            "E2",
            "F2",
            "F2#",
            "G2",
            "G2#",
            "A2",
            "B2b",
            "B2",
            "C3",
            "C3#",
            "D3",
            "E3b",
            "E3",
            "F3",
            "F3#",
            "G3",
            "G3#",
            "A3",
            "B3b",
            "B3",
            "C4",
            "C4#",
            "D4",
            "E4b",
            "E4",
            "F4",
            "F4#",
            "G4",
            "G4#",
            "A4",
            "B4b",
            "B4",
            "C5"
        };

        private static readonly List<string> CrowdStateNames = new()
        {
            "CrowdRealtime",
            "CrowdMellow",
            "CrowdNormal",
            "CrowdIntense"
        };

        private VenueHashLibrary      _hashLib;
        private AnimatorCommandQueue  _queue;
        private List<AnimatorCommand> _pendingBoolOffs;
        private List<IVenueChannel>   _channels;

        private List<TempoChange> _tempoList;
        private int               _tempoIndex;
        private List<Animator>    _animators;

        protected override void GameplayAwake()
        {
            string lightingLayerName = "Base Layer";
            string postProcessingLayerName = "Base Layer";

            if (_lightingAnimator && _lightingAnimator.GetLayerIndex("Lighting") != -1)
            {
                lightingLayerName = "Lighting";
            }

            if (_postProcessingAnimator && _postProcessingAnimator.GetLayerIndex("Post Processing") != -1)
            {
                postProcessingLayerName = "Post Processing";
            }

            _hashLib = new VenueHashLibrary(
                GuitarNoteNames,
                BassNoteNames,
                DrumNoteNames,
                KeysNoteNames,
                ProGuitarStringNames,
                ProKeysNoteNames,
                VocalNoteNames,
                CrowdStateNames,
                lightingLayerName,
                postProcessingLayerName);

            _queue = new AnimatorCommandQueue();
            _pendingBoolOffs = new List<AnimatorCommand>(64);
            _channels = new List<IVenueChannel>();
            _animators = new List<Animator>();

            // Collect all non-null animators for BPM sync
            if (_lightingAnimator) _animators.Add(_lightingAnimator);
            if (_postProcessingAnimator) _animators.Add(_postProcessingAnimator);
            if (_stageFXAnimator) _animators.Add(_stageFXAnimator);
            if (_crowdAnimator) _animators.Add(_crowdAnimator);
            if (_cameraAnimator) _animators.Add(_cameraAnimator);
            if (_beatlineAnimator) _animators.Add(_beatlineAnimator);
            if (_happinessAnimator) _animators.Add(_happinessAnimator);
            if (_guitarAnimator) _animators.Add(_guitarAnimator);
            if (_proGuitarAnimator) _animators.Add(_proGuitarAnimator);
            if (_bassAnimator) _animators.Add(_bassAnimator);
            if (_proBassAnimator) _animators.Add(_proBassAnimator);
            if (_drumAnimator) _animators.Add(_drumAnimator);
            if (_drumAnimAnimator) _animators.Add(_drumAnimAnimator);
            if (_keysAnimator) _animators.Add(_keysAnimator);
            if (_proKeysAnimator) _animators.Add(_proKeysAnimator);
            if (_vocalAnimator) _animators.Add(_vocalAnimator);
            if (_harmony1Animator) _animators.Add(_harmony1Animator);
            if (_harmony2Animator) _animators.Add(_harmony2Animator);

            // Pre-cache all parameters for the collected animators to avoid logic spikes during gameplay
            foreach (var animator in _animators)
            {
                AnimatorExtensions.RegisterAnimator(animator);
            }

            if (_lightingAnimator)
            {
                int layer = _lightingAnimator.GetLayerIndex("Lighting");
                if (layer == -1)
                {
                    layer = _lightingAnimator.GetLayerIndex("Base Layer");
                }

                if (layer != -1)
                {
                    _hashLib.LightingLayerHash = layer;
                    AnimatorExtensions.RegisterBlendStates(_lightingAnimator, layer, _hashLib.LightingBlendHashes);
                }
            }

            if (_postProcessingAnimator)
            {
                int layer = _postProcessingAnimator.GetLayerIndex("Post Processing");
                if (layer == -1)
                {
                    layer = _postProcessingAnimator.GetLayerIndex("Base Layer");
                }

                if (layer != -1) {
                    _hashLib.PostProcessingLayerHash = layer;
                        AnimatorExtensions.RegisterBlendStates(_postProcessingAnimator, layer, _hashLib.PostProcessingBlendHashes);
                }
            }
        }

        protected override void GameplayDestroy()
        {
            AnimatorExtensions.ClearParameterCache();
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _tempoList = chart.SyncTrack.Tempos;
            _tempoIndex = 0;

            _channels.Clear();
            _queue.Reset();

            if (_lightingEnable && _lightingAnimator)
                _channels.Add(new LightingChannel(_lightingAnimator, _hashLib, _leadingFramesLighting));

            if (_postProcessingEnable && _postProcessingAnimator)
                _channels.Add(new PostProcessingChannel(_postProcessingAnimator, _hashLib, _leadingFramesPostProcessing));

            if (_stageFXEnable && _stageFXAnimator)
                _channels.Add(new StageFXChannel(_stageFXAnimator, _hashLib, _leadingFramesStage));

            if (_beatlineEnable && _beatlineAnimator)
                _channels.Add(new BeatlineChannel(_beatlineAnimator, _hashLib, _leadingFramesBeatline));

            if (_cameraEnable && _cameraAnimator)
                _channels.Add(new CameraChannel(_cameraAnimator, _hashLib, _leadingFramesCamera));

            if (_happinessEnable && _happinessAnimator)
                _channels.Add(new HappinessChannel(_happinessAnimator, _hashLib, GameManager.EngineManager));

            if (_crowdEnable && _crowdAnimator)
                _channels.Add(new CrowdChannel(_crowdAnimator, _hashLib, _leadingFramesCrowd, GameManager.EngineManager));

            if (_guitarNotesEnable && _guitarAnimator)
                _channels.Add(new GuitarChannel(_guitarAnimator, _hashLib, _leadingFramesGuitar));

            if (_proGuitarNotesEnable && _proGuitarAnimator)
                _channels.Add(new ProGuitarChannel(_proGuitarAnimator, _hashLib, _leadingFramesProGuitar));

            if (_bassNotesEnable && _bassAnimator)
                _channels.Add(new BassChannel(_bassAnimator, _hashLib, _leadingFramesBass));

            if (_proBassNotesEnable && _proBassAnimator)
                _channels.Add(new ProBassChannel(_proBassAnimator, _hashLib, _leadingFramesProBass));

            if (_drumNotesEnable && _drumAnimator)
                _channels.Add(new DrumChannel(_drumAnimator, _hashLib, _leadingFramesDrums));

            if (_drumAnimEnable && _drumAnimAnimator)
                _channels.Add(new DrumAnimChannel(_drumAnimAnimator, _hashLib, _leadingFramesDrumAnim));

            if (_keysNotesEnable && _keysAnimator)
                _channels.Add(new KeysChannel(_keysAnimator, _hashLib, _leadingFramesKeys));

            if (_proKeysNotesEnable && _proKeysAnimator)
                _channels.Add(new ProKeysChannel(_proKeysAnimator, _hashLib, _leadingFramesProKeys));

            if (_vocalNotesEnable && _vocalAnimator)
                _channels.Add(new VocalChannel(_vocalAnimator, _hashLib, _leadingFramesVocals, 0));

            if (_harmony1NotesEnable && _harmony1Animator)
                _channels.Add(new VocalChannel(_harmony1Animator, _hashLib, _leadingFramesHarmony1, 1));

            if (_harmony2NotesEnable && _harmony2Animator)
                _channels.Add(new VocalChannel(_harmony2Animator, _hashLib, _leadingFramesHarmony2, 2));

            // Initialize all channels
            foreach (var channel in _channels)
            {
                channel.BuildCommands(chart, _queue);
            }

            _queue.Sort();
        }

        private void Update()
        {
            if (GameManager.Paused) return;

            double vt = GameManager.VisualTime;

            // Update BPM Adjust for all animators
            while (_tempoIndex < _tempoList.Count && _tempoList[_tempoIndex].Time <= vt)
            {
                _BPMAdjust = (float) _tempoList[_tempoIndex].BeatsPerMinute / _animationBPM;
                foreach (var animator in _animators)
                {
                    animator.SafeSetFloat(_hashLib.BPMAdjust, _BPMAdjust);
                }
                _tempoIndex++;
            }

            // Flush any pending BoolOff commands from previous frames
            for (int i = _pendingBoolOffs.Count - 1; i >= 0; i--)
            {
                if (_pendingBoolOffs[i].Time <= vt)
                {
                    _pendingBoolOffs[i].Target.SafeSetBool(_pendingBoolOffs[i].ParamHash, false);
                    _pendingBoolOffs.RemoveAt(i);
                }
            }

            // Dispatch chart-driven commands
            _queue.Flush(vt, _pendingBoolOffs, _hashLib);

            // Tick reactive channels
            foreach (var channel in _channels)
            {
                channel.Update(vt);
            }
        }
    }
}