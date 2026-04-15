using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ManagedBass;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using YARG.Core;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Core.Extensions;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Core.Parsing;
using YARG.Integration;
using YARG.Gameplay;
using YARG.Helpers.Extensions;
using YARG.Menu.Dialogs;
using YARG.Playback;
using AnimationEvent = YARG.Core.Chart.AnimationEvent;
using Random = System.Random;

#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

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
		private List<Animator> _animators;
		private Dictionary<string, int> _paramhash = new Dictionary<string, int>();

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
		public int _animationBPM = 120;
		public float _BPMAdjust = 1;
		public int _leadingFramesLighting;
		public int _leadingFramesPostProcessing;
		public int _leadingFramesStage;
		public int _leadingFramesCrowd;
		public int _leadingFramesCamera;
		public int _leadingFramesBeatline;
		public int _leadingFramesGuitar;
		public int _leadingFramesProGuitar;
		public int _leadingFramesBass;
		public int _leadingFramesProBass;
		public int _leadingFramesDrums;
		public int _leadingFramesDrumAnim;
		public int _leadingFramesKeys;
		public int _leadingFramesProKeys;
		public int _leadingFramesVocals;
		public int _leadingFramesHarmony1;
		public int _leadingFramesHarmony2;

		//standard chart
		private List<LightingEvent> _lightingEvents;
		private int _lightingEventIndex;
		private string _currentLight;
		private string _prevLight;
		private string _nextLight;
		private bool _lightBlended;
		private List<PostProcessingEvent> _postProcessingEvents;
		private int _postProcessingEventIndex;
		private string _currentPP;
		private string _prevPP;
		private string _nextPP;
		private bool _PPBlended;
		private List<StageEffectEvent> _stageEvents;
		private int _stageEventIndex;
		private string _currentStage;
		private List<CrowdEvent> _crowdEvents;
		private int _crowdEventIndex;
		private int _crowdLimit;
		private int _crowdHappiness;
		private int _crowdState;
		private int _prevCrowd;
		private bool _crowdClap;
		private bool _prevClap;
		private bool _crowdClapOff = false;
		private List<string> _crowdStateNames = new List<string>
		{
			"CrowdRealtime", "CrowdMellow", "CrowdNormal", "CrowdIntense"
		};
		private List<CameraCutEvent> _cameraCuts;
		private int _cameraCutIndex;
		//private string _currentCamPriority;
		//private string _currentCamConstraint;
		private string _currentCamSubject;
		private List<string> _cameraSubjectNames;
		private List<string> _onlyClose = new List<string>
		{
			"BehindNoDrum", "NearNoDrum", "Guitar", "GuitarBehind", "GuitarCloseup", "DrumsBehind", "DrumsCloseupHand",
			"DrumsCloseupHead", "Bass", "BassBehind", "BassCloseup", "BassCloseupHead", "Vocals", "VocalsCloseup",
			"VocalsBehind", "Keys", "KeysBehind", "KeysCloseupHand", "KeysCloseupHead", "DrumsVocals", "BassDrums",
			"DrumsGuitar", "BassVocalsBehind", "BassVocals", "GuitarVocalsBehind", "GuitarVocals", "KeysVocalsBehind",
			"KeysVocals", "BassGuitarBehind", "BassGuitar","BassKeysBehind", "BassKeys", "GuitarKeysBehind", "GuitarKeys"
		};
		private List<string> _noClose = new List<string>
		{
			"Crowd", "Stage", "AllBehind", "AllFar", "AllNear", "BehindNoDrum", "NearNoDrum", "Guitar", "GuitarBehind",
			"DrumsBehind", "Bass", "BassBehind", "Vocals", "VocalsBehind", "Keys", "KeysBehind", "DrumsVocals",
			"BassDrums", "DrumsGuitar", "BassVocalsBehind", "BassVocals", "GuitarVocalsBehind", "GuitarVocals",
			"KeysVocalsBehind", "KeysVocals", "BassGuitarBehind","BassGuitar", "BassKeysBehind", "BassKeys",
			"GuitarKeysBehind", "GuitarKeys"
		};
		private List<string> _onlyFar = new List<string>{ "Crowd", "Stage", "AllFar" };
		private List<string> _noBehind = new List<string>
		{
			"Stage", "AllBehind", "AllFar", "AllNear", "NearNoDrum", "Guitar", "GuitarCloseup", "DrumsCloseupHand",
			"DrumsCloseupHead", "Bass", "BassCloseup", "BassCloseupHead", "Vocals", "VocalsCloseup", "VocalsBehind",
			"Keys", "KeysBehind", "KeysCloseupHand", "KeysCloseupHead", "DrumsVocals", "BassDrums", "DrumsGuitar",
			"BassVocals", "GuitarVocals", "KeysVocals", "BassGuitar", "BassKeys", "GuitarKeys"
		};
		private List<Beatline> _beatList;
		private int _beatIndex;
		private string _currentBeat;
		private List<TempoChange> _tempoList;
		private int _tempoIndex;
		private double _currentTempo;

		//notes
		private List<GuitarNote> _guitarNoteList;
		private int _guitarNoteIndex;

		private List<string> _guitarNoteNames = new List<string>
		{
			"gGreen", "gRed", "gYellow", "gBlue", "gOrange", "gBlack", "gOpen"
		};
		private List<ProGuitarNote> _proGuitarNoteList;

		private List<string> _proGuitarStringNames = new List<string>
		{
			"ELo", "A", "D", "G", "B", "EHi"
		};
		private int _proGuitarNoteIndex;
		private List<GuitarNote> _bassNoteList;
		private int _bassNoteIndex;
		private List<string> _bassNoteNames = new List<string>
		{
			"bGreen", "bRed", "bYellow", "bBlue", "bOrange", "bBlack", "bOpen"
		};
		private List<ProGuitarNote> _proBassNoteList;
		private int _proBassNoteIndex;
		private List<DrumNote> _drumNoteList;
		private int _drumNoteIndex;
		private List<string> _drumNoteNames = new List<string>
		{
			"dKick", "dRed", "dYellow", "dBlue", "dGreen", "dYellowCym", "dBlueCym", "dGreenCym"
		};
		private List<AnimationEvent> _drumAnimList;
		private int _drumAnimIndex;
		private List<GuitarNote> _keysNoteList;
		private int _keysNoteIndex;
		private List<string> _keysNoteNames = new List<string>
		{
			"kGreen", "kRed", "kYellow", "kBlue", "kOrange"
		};
		private List<ProKeysNote> _proKeysNoteList;
		private int _proKeysNoteIndex;
		private List<string> _proKeysNoteNames = new List<string>
		{
			"C3", "C3#", "D3", "E3b", "E3", "F3", "F3#", "G3", "G3#", "A3", "B3b", "B3",
			"C4", "C4#", "D4", "E4b", "E4", "F4", "F4#", "G4", "G4#", "A4", "B4b", "B4", "C5"
		};
		private List<VocalNote> _vocalNoteList;
		private int _vocalNoteIndex;
		private string _currentVocalNote;
		private List<string> _vocalNoteNames = new List<string>
		{
			"Unpitched", "C1", "C1#", "D1", "E1b", "E1", "F1", "F1#", "G1", "G1#", "A1", "B1b", "B1",
			"C2", "C2#", "D2", "E2b", "E2", "F2", "F2#", "G2", "G2#", "A2", "B2b", "B2",
			"C3", "C3#", "D3", "E3b", "E3", "F3", "F3#", "G3", "G3#", "A3", "B3b", "B3",
			"C4", "C4#", "D4", "E4b", "E4", "F4", "F4#", "G4", "G4#", "A4", "B4b", "B4", "C5"
		};
		private List<VocalNote> _harmony0NoteList;
		private int _harmony0NoteIndex;
		private string _currentHarmony0Note;
		private List<VocalNote> _harmony1NoteList;
		private int _harmony1NoteIndex;
		private string _currentHarmony1Note;
		private List<VocalNote> _harmony2NoteList;
		private int _harmony2NoteIndex;
		private string _currentHarmony2Note;

		//happiness
		private float _happiness;
		private float _prevHappiness;
		private string _currentHappAnim;
		private string _prevHappAnim;


		protected override void OnChartLoaded(SongChart chart)
		{
			_tempoList = chart.SyncTrack.Tempos;

			_animators = new List<Animator>();

			if (_lightingEnable == true)
			{
				if (_lightingAnimator != null)
				{
					_animators.Add(_lightingAnimator);
					foreach (AnimatorControllerParameter param in _lightingAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				_lightingEvents = chart.VenueTrack.Lighting;
			}
			if (_postProcessingEnable == true)
			{
				if (_postProcessingAnimator != null && (!_animators.Contains(_postProcessingAnimator)))
				{
					_animators.Add(_postProcessingAnimator);
					foreach (AnimatorControllerParameter param in _postProcessingAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				_postProcessingEvents = chart.VenueTrack.PostProcessing;
			}
			if (_stageFXEnable == true)
			{
				if (_stageFXAnimator != null && (!_animators.Contains(_stageFXAnimator)))
				{
					_animators.Add(_stageFXAnimator);
					foreach (AnimatorControllerParameter param in _stageFXAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				_stageEvents = chart.VenueTrack.Stage;
			}
			if (_crowdEnable == true)
			{
				if (_crowdAnimator != null && (!_animators.Contains(_crowdAnimator)))
				{
					_animators.Add(_crowdAnimator);
					foreach (AnimatorControllerParameter param in _crowdAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				_crowdEvents = chart.CrowdEvents;
			}
			if (_cameraEnable == true)
			{
				if (_cameraAnimator != null && (!_animators.Contains(_cameraAnimator)))
				{
					_animators.Add(_cameraAnimator);
					foreach (AnimatorControllerParameter param in _cameraAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				_cameraSubjectNames = Enum.GetNames(typeof(CameraCutEvent.CameraCutSubject)).ToList();
				_cameraSubjectNames.RemoveAt(_cameraSubjectNames.Count - 1);
				_cameraCuts = chart.VenueTrack.CameraCuts;
			}
			if (_beatlineEnable == true)
			{
				if (_beatlineAnimator != null && (!_animators.Contains(_beatlineAnimator)))
				{
					_animators.Add(_beatlineAnimator);
					foreach (AnimatorControllerParameter param in _beatlineAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				_beatList = chart.SyncTrack.Beatlines;
			}
			if (_happinessEnable == true)
			{
				if (_happinessAnimator != null && (!_animators.Contains(_happinessAnimator)))
				{
					_animators.Add(_happinessAnimator);
					foreach (AnimatorControllerParameter param in _happinessAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				_prevHappAnim = "Init";
			}
			if (_guitarNotesEnable)
			{
				if (_guitarAnimator != null && (!_animators.Contains(_guitarAnimator)))
				{
					_animators.Add(_guitarAnimator);
					foreach (AnimatorControllerParameter param in _guitarAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				var guitarId = chart.FiveFretGuitar.GetDifficulty(Difficulty.Expert);
				_guitarNoteList = guitarId.Notes;
			}
			if (_proGuitarNotesEnable)
			{
				if (_proGuitarAnimator != null && (!_animators.Contains(_proGuitarAnimator)))
				{
					_animators.Add(_proGuitarAnimator);
					foreach (AnimatorControllerParameter param in _proGuitarAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				var proGuitar22 = chart.ProGuitar_22Fret.GetDifficulty(Difficulty.Expert);
				_proGuitarNoteList = proGuitar22.Notes;
				if (_proGuitarNoteList.Count == 0)
				{
					var proGuitar17 = chart.ProGuitar_17Fret.GetDifficulty(Difficulty.Expert);
					_proGuitarNoteList = proGuitar17.Notes;
				}
			}

			if (_bassNotesEnable)
			{
				if (_bassAnimator != null && (!_animators.Contains(_bassAnimator)))
				{
					_animators.Add(_bassAnimator);
					foreach (AnimatorControllerParameter param in _bassAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				var bassId = chart.FiveFretBass.GetDifficulty(Difficulty.Expert);
				_bassNoteList = bassId.Notes;
			}

			if (_proBassNotesEnable)
			{
				if (_proBassAnimator != null && (!_animators.Contains(_proBassAnimator)))
				{
					_animators.Add(_proBassAnimator);
					foreach (AnimatorControllerParameter param in _proBassAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				var proBass22 = chart.ProBass_22Fret.GetDifficulty(Difficulty.Expert);
				_proBassNoteList = proBass22.Notes;
				if (_proBassNoteList.Count == 0)
				{
					var proGuitar17 = chart.ProBass_17Fret.GetDifficulty(Difficulty.Expert);
					_proBassNoteList = proGuitar17.Notes;
				}
			}

			if (_drumNotesEnable || _drumAnimEnable)
			{
				if (_drumAnimator != null && (!_animators.Contains(_drumAnimator)))
				{
					_animators.Add(_drumAnimator);
					foreach (AnimatorControllerParameter param in _drumAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				if (_drumAnimAnimator != null && (!_animators.Contains(_drumAnimAnimator)))
				{
					_animators.Add(_drumAnimAnimator);
					foreach (AnimatorControllerParameter param in _drumAnimAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				var drumsId = chart.ProDrums.GetDifficulty(Difficulty.Expert);
				var drumsTrack = chart.GetDrumsTrack(Instrument.ProDrums);
				if (drumsId.Notes.Count == 0)
				{
					drumsId = chart.FourLaneDrums.GetDifficulty(Difficulty.Expert);
					drumsTrack = chart.GetDrumsTrack(Instrument.FourLaneDrums);
					YargLogger.LogDebug("Venue Animator: No pro drums, getting 4L");
				}
				_drumAnimList = drumsTrack.Animations.AnimationEvents;
				_drumNoteList = drumsId.Notes;
			}

			if (_keysNotesEnable)
			{
				if (_keysAnimator != null && (!_animators.Contains(_keysAnimator)))
				{
					_animators.Add(_keysAnimator);
					foreach (AnimatorControllerParameter param in _keysAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				var keysId = chart.Keys.GetDifficulty(Difficulty.Expert);
				_keysNoteList = keysId.Notes;
			}

			if (_proKeysNotesEnable)
			{
				if (_proKeysAnimator != null && (!_animators.Contains(_proKeysAnimator)))
				{
					_animators.Add(_proKeysAnimator);
					foreach (AnimatorControllerParameter param in _proKeysAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				var proKeysId = chart.ProKeys.GetDifficulty(Difficulty.Expert);
				_proKeysNoteList = proKeysId.Notes;
			}

			if (_vocalNotesEnable)
			{
				if (_vocalAnimator != null && (!_animators.Contains(_vocalAnimator)))
				{
					_animators.Add(_vocalAnimator);
					foreach (AnimatorControllerParameter param in _vocalAnimator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				var harmony0Id = chart.Harmony.Parts[0].CloneAsInstrumentDifficulty();
				_vocalNoteList = new List<VocalNote>();
				foreach (var note in harmony0Id.Notes)
				{
					var h0phraseClone = note.Clone();
					//phraseClone.RemovePercussionChildNotes();

					foreach (var h0phraseNote in h0phraseClone.ChildNotes)
					{
						_vocalNoteList.Add(h0phraseNote);
					}
				}
				if (_vocalNoteList.Count == 0)
				{
					var vocalsId = chart.Vocals.Parts[0].CloneAsInstrumentDifficulty();
					foreach (var note in vocalsId.Notes)
					{
						var vphraseClone = note.Clone();
						//phraseClone.RemovePercussionChildNotes();

						foreach (var vphraseNote in vphraseClone.ChildNotes)
						{
							_vocalNoteList.Add(vphraseNote);
						}
					}
				}
			}

			if (_harmony1NotesEnable)
			{
				if (_harmony1Animator != null && (!_animators.Contains(_harmony1Animator)))
				{
					_animators.Add(_harmony1Animator);
					foreach (AnimatorControllerParameter param in _harmony1Animator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				var harmony1Id = chart.Harmony.Parts[1].CloneAsInstrumentDifficulty();
				_harmony1NoteList = new List<VocalNote>();
				foreach (var note in harmony1Id.Notes)
				{
					var h1phraseClone = note.Clone();
					//phraseClone.RemovePercussionChildNotes();

					foreach (var h1phraseNote in h1phraseClone.ChildNotes)
					{
						_harmony1NoteList.Add(h1phraseNote);
					}
				}
			}

			if (_harmony2NotesEnable)
			{
				if (_harmony2Animator != null && (!_animators.Contains(_harmony2Animator)))
				{
					_animators.Add(_harmony2Animator);
					foreach (AnimatorControllerParameter param in _harmony2Animator.parameters)
					{
						if (!_paramhash.ContainsKey(param.name))
						{
							int hash = Animator.StringToHash(param.name);
							_paramhash.Add(param.name, hash);
						}
					}
				}
				var harmony2Id = chart.Harmony.Parts[2].CloneAsInstrumentDifficulty();
				_harmony2NoteList = new List<VocalNote>();
				foreach (var note in harmony2Id.Notes)
				{
					var h2phraseClone = note.Clone();
					//phraseClone.RemovePercussionChildNotes();

					foreach (var h2phraseNote in h2phraseClone.ChildNotes)
					{
						_harmony2NoteList.Add(h2phraseNote);
					}
				}
			}
		}

		private void Update()
		{
			if (_lightingEnable && _lightingAnimator == null || _postProcessingEnable && _postProcessingAnimator == null
				|| _stageFXEnable && _stageFXAnimator == null || _crowdEnable && _crowdAnimator == null ||
				_happinessEnable && _happinessAnimator == null || _beatlineEnable && _beatlineAnimator == null ||
				_guitarNotesEnable && _guitarAnimator == null || _proGuitarNotesEnable && _proGuitarAnimator == null ||
				_bassNotesEnable && _bassAnimator == null || _proBassNotesEnable && _proBassAnimator == null ||
				_keysNotesEnable && _keysAnimator == null || _proKeysNotesEnable && _proKeysAnimator == null ||
				_vocalNotesEnable && _vocalAnimator == null || _harmony1NotesEnable && _harmony1Animator == null ||
				_harmony2NotesEnable && _harmony2Animator == null)
			{
				YargLogger.LogWarning($"Venue Animator cancelled: One or more enabled sections is missing an animator!");
				return;
			}

			if (GameManager.Paused)
			{
				return;
			}

			while (_tempoList.Count > 0 && _tempoIndex < _tempoList.Count &&
				_tempoList[_tempoIndex].Time <= GameManager.VisualTime)
			{
				_currentTempo = _tempoList[_tempoIndex].BeatsPerMinute;
				_tempoIndex++;
				_BPMAdjust = (float)_currentTempo / (float)_animationBPM;
				foreach (var animator in _animators)
				{
					CheckFloat(animator, "BPMAdjust", _BPMAdjust);
				}
			}

			if (_lightingEnable == true)
			{
				while (_lightingEventIndex < _lightingEvents.Count &&
					_lightingEvents[_lightingEventIndex].Time - _leadingFramesLighting/60 <= GameManager.VisualTime)
				{
					var lighting = _lightingEvents[_lightingEventIndex];

					_prevLight = _currentLight;
					switch (lighting.Type)
					{
						case LightingType.Default:
						case LightingType.Intro:
							_currentLight = "LightDefault";
							break;
						default:
							_currentLight = lighting.Type.ToString();
							break;
					}

					if (_lightingEventIndex + 1 < _lightingEvents.Count)
					{
						var nextlight = _lightingEvents[_lightingEventIndex + 1];
						var i = _lightingEventIndex + 1;

						while (i < _lightingEvents.Count && (nextlight.Type == LightingType.KeyframeFirst ||
						                                     nextlight.Type == LightingType.KeyframeNext ||
						                                     nextlight.Type == LightingType.KeyframePrevious))
						{
							nextlight = _lightingEvents[i];
							//YargLogger.LogDebug($"Venue Animator Crossfade: Next light is keyframe, skipping to index {i}");
							i++;
						}

						switch (nextlight.Type)
						{
							case LightingType.Default:
							case LightingType.Intro:
								_nextLight = "LightDefault";
								break;
							default:
								_nextLight = nextlight.Type.ToString();
								break;
						}

						if (_currentLight == _prevLight && _currentLight != "KeyframeFirst" &&
						    _currentLight != "KeyframeNext" &&  _currentLight != "KeyframePrevious" &&
						    _lightingEventIndex + 1 < _lightingEvents.Count)
						{
							RollRandom(_lightingAnimator);
							float time = (float)(nextlight.Time - lighting.Time);
							CheckBlend(time, _lightingAnimator, _nextLight, _currentLight, "Lighting");
							_lightBlended = true;
							//YargLogger.LogDebug($"Venue Animator Lighting Blend: {_currentLight} to {_nextLight} for {time} seconds");
							_prevLight = _currentLight;
							_lightingEventIndex++;
						}

						else if ((_currentLight == "Frenzy" || _currentLight == "CoolAutomatic" ||
					          _currentLight == "WarmAutomatic") && _lightingEventIndex + 1 == _lightingEvents.Count)
						{
							RollRandom(_lightingAnimator);
							CheckFloat(_lightingAnimator, "BPMAdjust", 0f);
							CheckTrigger(_lightingAnimator, _currentLight);
							YargLogger.LogDebug($"Venue Animator Lighting: Song over, don't animate {_currentLight}");
							_prevLight = _currentLight;
							_lightingEventIndex++;
						}

						else if (_lightBlended == false)
						{
							RollRandom(_lightingAnimator);
							CheckTrigger(_lightingAnimator, _currentLight);
							//YargLogger.LogDebug($"Venue Animator Lighting: {_currentLight}");
							_prevLight = _currentLight;
							_lightingEventIndex++;
						}

						else
						{
							_lightingEventIndex++;
						}
					}

					else
					{
						CheckTrigger(_lightingAnimator, _currentLight);
						YargLogger.LogDebug($"Venue Animator Lighting: {_currentLight}");
						_prevLight = _currentLight;
						_lightingEventIndex++;
					}
				}
			}

			if (_postProcessingEnable == true)
			{
				while (_postProcessingEventIndex < _postProcessingEvents.Count &&
					   _postProcessingEvents[_postProcessingEventIndex].Time - _leadingFramesPostProcessing/60 <= GameManager.VisualTime)
				{
					var PP = _postProcessingEvents[_postProcessingEventIndex];

					switch (PP.Type)
					{
						case PostProcessingType.Default:
							_currentPP = "PPDefault";
							break;
						default:
							_currentPP = PP.Type.ToString();
							break;
					}

					if (_postProcessingEventIndex + 1 < _postProcessingEvents.Count)
					{
						var nextpp = _postProcessingEvents[_postProcessingEventIndex + 1];

						switch (nextpp.Type)
						{
							case PostProcessingType.Default:
								_nextPP = "PPDefault";
								break;
							default:
								_nextPP = nextpp.Type.ToString();
								break;
						}

						if (_currentPP == _prevPP && _postProcessingEventIndex + 1 < _postProcessingEvents.Count
						  && _currentPP != _nextPP)
						{
							RollRandom(_postProcessingAnimator);
							float time = (float)(nextpp.Time - PP.Time);
							CheckBlend(time, _postProcessingAnimator, _nextPP, _currentPP, "Post Processing");
							_PPBlended = true;
							//YargLogger.LogDebug($"Venue Animator Post Processing Blend: {_currentPP} to {_nextPP} for {time} seconds");
							_prevPP = _currentPP;
							_postProcessingEventIndex++;
						}

						else if (_PPBlended == false)
						{
							RollRandom(_postProcessingAnimator);
							CheckTrigger(_postProcessingAnimator, _currentPP);
							//YargLogger.LogDebug($"Venue Animator Lighting: {_currentLight}");
							_prevPP = _currentPP;
							_postProcessingEventIndex++;
						}

						else
						{
							_postProcessingEventIndex++;
						}
					}

					else
					{
						CheckTrigger(_postProcessingAnimator, _currentPP);
						//YargLogger.LogDebug($"Venue Animator Lighting: {_currentLight}");
						_prevPP = _currentPP;
						_postProcessingEventIndex++;
					}
				}
			}

			if (_stageFXEnable == true)
			{
				while (_stageEventIndex < _stageEvents.Count && _stageEvents[_stageEventIndex].Time -
				       _leadingFramesStage/60 <= GameManager.VisualTime)
				{
					var stage =  _stageEvents[_stageEventIndex];
					if (stage.Effect == StageEffect.FogOn)
					{
						RollRandom(_stageFXAnimator);
						CheckBool(_stageFXAnimator, "Fog", true, 1f, true);
						//YargLogger.LogDebug($"Venue Animator Stage: Fog On");
					}
					if (stage.Effect == StageEffect.FogOff)
					{
						CheckBool(_stageFXAnimator, "Fog", false, 1f, true);
						//YargLogger.LogDebug($"Venue Animator Stage: Fog Off");
					}
					if (stage.Effect == StageEffect.BonusFx)
					{
						RollRandom(_stageFXAnimator);
						CheckTrigger(_stageFXAnimator, "BonusFx");
						//YargLogger.LogDebug($"Venue Animator Stage: Bonus FX");
					}
					_stageEventIndex++;
				}
			}

			if (_crowdEnable == true || _happinessEnable == true)
			{
				if (_prevHappiness != GameManager.EngineManager.Happiness)
				{
					_happiness = GameManager.EngineManager.Happiness;
				}
			}

			if (_happinessEnable == true)
			{
				if (_prevHappiness != _happiness)
				{
					CheckFloat(_happinessAnimator, "Happiness", _happiness);
					//YargLogger.LogDebug($"Venue Animator Happiness: {happiness}");
					if(_happiness > 0.666f) {_currentHappAnim = "HappyHigh";}
					if(_happiness < 0.666f) {_currentHappAnim = "HappyMed";}
					if(_happiness < 0.333f) {_currentHappAnim = "HappyLow";}
					if (_currentHappAnim != _prevHappAnim)
					{
						RollRandom(_happinessAnimator);
						CheckBool(_happinessAnimator, _currentHappAnim, true, 1f, true);
						CheckBool(_happinessAnimator, _prevHappAnim, false, 1f, true);
						//YargLogger.LogDebug($"Venue Animator Happiness: {_currentHappAnim}");
					}
					_prevHappAnim = _currentHappAnim;
				}
			}

			if (_crowdEnable == true)
			{
				while (_crowdEventIndex < _crowdEvents.Count &&
				       _crowdEvents[_crowdEventIndex].Time - _leadingFramesCrowd/60 <= GameManager.VisualTime)
				{
					var crowd = _crowdEvents[_crowdEventIndex];
					switch (crowd.CrowdState)
					{
						case CrowdState.Realtime:
							_crowdLimit = 0;
							//YargLogger.LogDebug($"Venue Animator Crowd Limit: CrowdRealtime");
							break;
						case CrowdState.Mellow:
							_crowdLimit = 1;
							//YargLogger.LogDebug($"Venue Animator Crowd Limit: CrowdMellow");
							break;
						case CrowdState.Normal:
							_crowdLimit = 2;
							//YargLogger.LogDebug($"Venue Animator Crowd Limit: CrowdNormal");
							break;
						case CrowdState.Intense:
							_crowdLimit = 3;
							//YargLogger.LogDebug($"Venue Animator Crowd Limit: CrowdIntense");
							break;
						default:
							break;
					}

					switch (crowd.ClapState)
					{
						case ClapState.NoClap:
							_crowdClapOff = true;
							RollRandom(_crowdAnimator);
							CheckBool(_crowdAnimator, "CrowdClap", false, 1f, true);
							//YargLogger.LogDebug($"Venue Animator Crowd: Clap disabled by chart");
							break;
						case ClapState.Clap:
							_crowdClapOff = false;
							if (_happiness == 1f)
							{
								RollRandom(_crowdAnimator);
								CheckBool(_crowdAnimator, "CrowdClap", true, 1f, true);
							}
							//YargLogger.LogDebug($"Venue Animator Crowd: Clap enabled by chart");
							break;
						default:
							break;
					}

					_crowdEventIndex++;
				}

				if (_prevHappiness != _happiness)
				{
					if(_happiness > 0.666f) {_crowdHappiness = 3;}
					if(_happiness < 0.666f) {_crowdHappiness = 2;}
					if(_happiness < 0.333f) {_crowdHappiness = 1;}
					if(_happiness == 1f) {_crowdClap = true;}
					if(_happiness < 1f) {_crowdClap = false;}
				}

				_crowdState = Math.Min(_crowdLimit, _crowdHappiness);

				if (_crowdState != _prevCrowd)
				{
					RollRandom(_crowdAnimator);
					CheckTrigger(_crowdAnimator, _crowdStateNames[_crowdState]);
					YargLogger.LogDebug($"Venue Animator Crowd: {_crowdStateNames[_crowdState]}");
				}

				if (_crowdClap != _prevClap && _crowdClapOff == false)
				{
					RollRandom(_crowdAnimator);
					CheckBool(_crowdAnimator, "CrowdClap", _crowdClap, 1f, true);
					YargLogger.LogDebug($"Venue Animator Crowd Clap: {_crowdClap}");
				}
				_prevCrowd = _crowdState;
				_prevClap = _crowdClap;
			}

			if (_crowdEnable == true || _happinessEnable == true)
			{
				if (_prevHappiness != GameManager.EngineManager.Happiness)
				{
					_prevHappiness = GameManager.EngineManager.Happiness;
				}
			}

			if (_cameraEnable == true)
			{
				while (_cameraCutIndex < +_cameraCuts.Count &&
					   _cameraCuts[_cameraCutIndex].Time - _leadingFramesCamera/60 <= GameManager.VisualTime)
				{
					var cam = _cameraCuts[_cameraCutIndex];

					var camrandom = Enum.GetValues(typeof(CameraCutEvent.CameraCutSubject)).Cast<int>().ToList();

					if (cam.Subject == CameraCutEvent.CameraCutSubject.Random)
					{
						var choices = cam.RandomChoices;
						if (choices.Count > 0)
						{
							_currentCamSubject = choices.Pick().ToString();
						}
						else
						{
							_currentCamSubject = GetRandomCam(cam.Constraint).ToString();
						}
					}
					else
					{
						_currentCamSubject = cam.Subject.ToString();
					}

					RollRandom(_cameraAnimator);
					CheckTrigger(_cameraAnimator, _currentCamSubject);
					//YargLogger.LogDebug($"Venue Animator Camera: {_currentCamSubject}");
					_cameraCutIndex++;
				}
			}

			if (_beatlineEnable == true)
			{
				while (_beatIndex < _beatList.Count &&
				       _beatList[_beatIndex].Time - _leadingFramesBeatline/60 <= GameManager.VisualTime)
				{
					var Beat = _beatList[_beatIndex];
					_currentBeat = Beat.Type.ToString();
					RollRandom(_beatlineAnimator);
					CheckTrigger(_beatlineAnimator, _currentBeat);
					//YargLogger.LogDebug($"Venue Animator Beat: {_currentBeat}");
					_beatIndex++;
				}
			}

			if (_guitarNotesEnable)
			{
				while (_guitarNoteList.Count > 0 && _guitarNoteIndex < _guitarNoteList.Count &&
					_guitarNoteList[_guitarNoteIndex].Time - _leadingFramesGuitar/60 <= GameManager.VisualTime)
				{
					if (_guitarNoteIndex >= _guitarNoteList.Count)
					{
						break;
					}
					var gNote = _guitarNoteList[_guitarNoteIndex];
					byte[] gNoteByte = BitConverter.GetBytes(gNote.NoteMask);
					BitArray gNoteMask = new BitArray(gNoteByte);

					for (int i = 0; i < 7 && i < gNoteMask.Length; i++)
					{
						bool gNoteOn = gNoteMask[i];
						RollRandom(_guitarAnimator);
						CheckBool(_guitarAnimator, _guitarNoteNames[i], gNoteOn, (float)gNote.TimeLength, false);
						/*if (gNoteOn)
						{
							YargLogger.LogDebug($"Venue Animator Bass: {_guitarNoteNames[i]}");
						}*/
					}
					_guitarNoteIndex++;
				}
			}

			if (_proGuitarNotesEnable)
			{
				while (_proGuitarNoteList.Count > 0 && _proGuitarNoteIndex < _proGuitarNoteList.Count &&
				       _proGuitarNoteList[_proGuitarNoteIndex].Time - _leadingFramesProGuitar/60 <= GameManager.VisualTime)
				{
					if (_proGuitarNoteIndex >= _proGuitarNoteList.Count)
					{
						break;
					}
					var pgNote = _proGuitarNoteList[_proGuitarNoteIndex];
					foreach (var note in pgNote.AllNotes)
					{
						RollRandom(_proGuitarAnimator);
						CheckBool(_proGuitarAnimator, "pg" + _proGuitarStringNames[note.String] + note.Fret, true, (float)note.TimeLength, false);
						//YargLogger.LogDebug($"Venue Animator Pro Guitar pg{_proGuitarStringNames[note.String]}{note.Fret}");
					}
					_proGuitarNoteIndex++;
				}
			}

			if (_bassNotesEnable)
			{
				while (_bassNoteList.Count > 0 && _bassNoteIndex < _bassNoteList.Count &&
				       _bassNoteList[_bassNoteIndex].Time - _leadingFramesBass/60 <= GameManager.VisualTime)
				{
					if (_bassNoteIndex >= _bassNoteList.Count)
					{
						break;
					}
					var bNote = _bassNoteList[_bassNoteIndex];
					byte[] bNoteByte = BitConverter.GetBytes(bNote.NoteMask);
					BitArray bNoteMask = new BitArray(bNoteByte);

					for (int i = 0; i < 7 && i < bNoteMask.Length; i++)
					{
						bool bNoteOn = bNoteMask[i];
						RollRandom(_bassAnimator);
						CheckBool(_bassAnimator, _bassNoteNames[i], bNoteOn, (float)bNote.TimeLength, false);
						/*if (bNoteOn)
						{
							YargLogger.LogDebug($"Venue Animator Bass: {_bassNoteNames[i]}");
						}*/
					}
					_bassNoteIndex++;
				}
			}

			if (_proBassNotesEnable)
			{
				while (_proBassNoteList.Count > 0 && _proBassNoteIndex < _proBassNoteList.Count &&
				       _proBassNoteList[_proBassNoteIndex].Time - _leadingFramesProBass/60 <= GameManager.VisualTime)
				{
					if (_proBassNoteIndex >= _proBassNoteList.Count)
					{
						break;
					}
					var pbNote = _proBassNoteList[_proBassNoteIndex];
					foreach (var note in pbNote.AllNotes)
					{
						RollRandom(_proBassAnimator);
						CheckBool(_proBassAnimator, "pb" + _proGuitarStringNames[note.String] + note.Fret, true, (float)note.TimeLength, false);
						//YargLogger.LogDebug($"Venue Animator Pro Bass: pb{_proGuitarStringNames[note.String]}{note.Fret}");
					}
					_proBassNoteIndex++;
				}
			}

			if (_drumNotesEnable)
			{
				while (_drumNoteList.Count > 0 && _drumNoteIndex < _drumNoteList.Count &&
				       _drumNoteList[_drumNoteIndex].Time - _leadingFramesDrums/60 <= GameManager.VisualTime)
				{
					if (_drumNoteIndex >= _drumNoteList.Count)
					{
						break;
					}
					var dNote = _drumNoteList[_drumNoteIndex];
					int dPads = 0;
					foreach (var drum in dNote.AllNotes)
					{
	                    dPads |= (1 << drum.Pad);
					}
					byte[] dNoteByte = BitConverter.GetBytes(dPads);
					BitArray dNoteMask = new BitArray(dNoteByte);

					for (int i = 0; i < 8 && i < dNoteMask.Length; i++)
					{
						if (dNoteMask[i])
						{
							RollRandom(_drumAnimator);
							CheckTrigger(_drumAnimator, _drumNoteNames[i]);
							//YargLogger.LogDebug($"Venue Animator Drum Note: {_drumNoteNames[i]}");
						}
					}
					_drumNoteIndex++;
				}
			}

			if (_drumAnimEnable)
			{
				while (_drumAnimIndex < _drumAnimList.Count &&
				       _drumAnimList[_drumAnimIndex].Time - _leadingFramesDrumAnim/60 <= GameManager.VisualTime)
				{
					if (_drumAnimIndex >= _drumAnimList.Count)
					{
						break;
					}
					var dAnim = _drumAnimList[_drumAnimIndex];

					if (dAnim.Type == AnimationEvent.AnimationType.OpenHiHat)
					{
						RollRandom(_drumAnimAnimator);
						CheckBool(_drumAnimAnimator, "OpenHiHat", true, (float)dAnim.TimeLength, false);
						//YargLogger.LogDebug($"Venue Animator Drum Anim: OpenHiHat");
					}
					else
					{
						RollRandom(_drumAnimAnimator);
						CheckTrigger(_drumAnimAnimator, dAnim.Type.ToString());
						//YargLogger.LogDebug($"Venue Animator Drum Anim: {dAnim.Type.ToString()}");
					}
					_drumAnimIndex++;
				}
			}

			if (_keysNotesEnable)
			{
				while (_keysNoteList.Count > 0 && _keysNoteIndex < _keysNoteList.Count &&
				       _keysNoteList[_keysNoteIndex].Time - _leadingFramesKeys/60 <= GameManager.VisualTime)
				{
					if (_keysNoteIndex >= _keysNoteList.Count)
					{
						break;
					}
					var kNote = _keysNoteList[_keysNoteIndex];
					byte[] kNoteByte = BitConverter.GetBytes(kNote.NoteMask);
					BitArray kNoteMask = new BitArray(kNoteByte);

					for (int i = 0; i < 5 && i < kNoteMask.Length; i++)
					{
						bool kNoteOn = kNoteMask[i];
						RollRandom(_keysAnimator);
						CheckBool(_keysAnimator, _keysNoteNames[i], kNoteOn, (float)kNote.TimeLength, false);
						/*if (kNoteOn)
						{
							YargLogger.LogDebug($"Venue Animator Keys: {_keysNoteNames[i]}");
						}*/
					}
					_keysNoteIndex++;
				}
			}

			if (_proKeysNotesEnable)
			{
				while (_proKeysNoteList.Count > 0 && _proKeysNoteIndex < _proKeysNoteList.Count &&
				       _proKeysNoteList[_proKeysNoteIndex].Time - _leadingFramesProKeys/60 <= GameManager.VisualTime)
				{
					if (_proKeysNoteIndex >= _proKeysNoteList.Count)
					{
						break;
					}
					var pkNote = _proKeysNoteList[_proKeysNoteIndex];
					byte[] pkNoteByte = BitConverter.GetBytes(pkNote.NoteMask);
					BitArray pkNoteMask = new BitArray(pkNoteByte);

					for (int i = 0; i < 25 && i < pkNoteMask.Length; i++)
					{
						bool pkNoteOn = pkNoteMask[i];
						RollRandom(_proKeysAnimator);
						CheckBool(_proKeysAnimator, "pk" + _proKeysNoteNames[i], pkNoteOn, (float)pkNote.TimeLength, false);
						/*if (pkNoteOn)
						{
							YargLogger.LogDebug($"Venue Animator Pro Keys: pk{_proKeysNoteNames[i]}");
						}*/
					}
					_proKeysNoteIndex++;
				}
			}

			if (_vocalNotesEnable)
			{
				if (_vocalNoteList.Count > 0)
				{
					while (_vocalNoteList.Count > 0 && _vocalNoteIndex < _vocalNoteList.Count &&
					       _vocalNoteList[_vocalNoteIndex].Time - _leadingFramesVocals/60 <= GameManager.VisualTime)
					{
						if (_vocalNoteIndex >= _vocalNoteList.Count)
						{
							break;
						}
						var vNote = _vocalNoteList[_vocalNoteIndex];
						if (vNote.IsNonPitched)
						{
							CheckTrigger(_vocalAnimator, "VocalUnpitched");
							//YargLogger.LogDebug($"Venue Animator Vocal Trigger: VocalUnpitched");
						}
						RollRandom(_vocalAnimator);
						CheckBool(_vocalAnimator, "VocalNote", true, 1f, true);
						StartCoroutine(VocalPitchUpdate(((float)vNote.TotalTimeLength), _vocalAnimator, vNote, "Vocal"));
						_vocalNoteIndex++;
					}
				}
			}

			if (_harmony1NotesEnable)
			{
				if (_harmony1NoteList.Count > 0)
				{
					while (_harmony1NoteList.Count > 0 && _harmony1NoteIndex < _harmony1NoteList.Count &&
					       _harmony1NoteList[_harmony1NoteIndex].Time - _leadingFramesHarmony1/60 <= GameManager.VisualTime)
					{
						if (_harmony1NoteIndex >= _harmony1NoteList.Count)
						{
							break;
						}
						var h1Note = _harmony1NoteList[_harmony1NoteIndex];
						if (h1Note.IsNonPitched)
						{
							CheckTrigger(_harmony1Animator, "Har1Unpitched");
							//YargLogger.LogDebug($"Venue Animator Har1 Trigger: Har1Unpitched");
						}
						RollRandom(_harmony1Animator);
						CheckBool(_harmony1Animator, "Har1Note", true, 1f, true);
						StartCoroutine(VocalPitchUpdate(((float)h1Note.TotalTimeLength), _harmony1Animator, h1Note, "Har1"));
						_harmony1NoteIndex++;
					}
				}
			}

			if (_harmony2NotesEnable)
			{
				if (_harmony2NoteList.Count > 0)
				{
					while (_harmony2NoteList.Count > 0 && _harmony2NoteIndex < _harmony2NoteList.Count &&
					       _harmony2NoteList[_harmony2NoteIndex].Time - _leadingFramesHarmony2/60 <= GameManager.VisualTime)
					{
						if (_harmony2NoteIndex >= _harmony2NoteList.Count)
						{
							break;
						}
						var h2Note = _harmony2NoteList[_harmony2NoteIndex];
						if (h2Note.IsNonPitched)
						{
							CheckTrigger(_harmony2Animator, "Har2Unpitched");
							//YargLogger.LogDebug($"Venue Animator Har2 Trigger: Har2Unpitched");
						}
						RollRandom(_harmony2Animator);
						CheckBool(_harmony2Animator, "Har2Note", true, 1f, true);
						StartCoroutine(VocalPitchUpdate(((float)h2Note.TotalTimeLength), _harmony2Animator, h2Note, "Har2"));
						_harmony2NoteIndex++;
					}
				}
			}
		}

		private string GetRandomCam(CameraCutEvent.CameraCutConstraint constraint)
		{
			var camlist = _cameraSubjectNames;

			if (constraint.HasFlag(CameraCutEvent.CameraCutConstraint.OnlyClose))
			{
				var modlist = camlist.Intersect(_onlyClose).ToList();
				camlist = modlist;
			}
			if (constraint.HasFlag(CameraCutEvent.CameraCutConstraint.OnlyFar))
			{
				var modlist = camlist.Intersect(_onlyFar).ToList();
				camlist = modlist;
			}
			if (constraint.HasFlag(CameraCutEvent.CameraCutConstraint.NoClose))
			{
				var modlist = camlist.Intersect(_noClose).ToList();
				camlist = modlist;
			}
			if (constraint.HasFlag(CameraCutEvent.CameraCutConstraint.NoBehind))
			{
				var modlist = camlist.Intersect(_noBehind).ToList();
				camlist = modlist;
			}

			var index = UnityEngine.Random.Range(0, camlist.Count);
			return camlist[index];
		}

		private void CheckTrigger(Animator animator, string parameter)
		{
			if (_paramhash.TryGetValue(parameter, out int hash))
			{
				animator.SetTrigger(hash);
				//YargLogger.LogDebug($"Venue Animator Trigger {parameter}, successfully set");
			}
		}

		private void CheckBool(Animator animator, string parameter, bool value, float time, bool indefinite)
		{
			if (_paramhash.TryGetValue(parameter, out int hash))
			{
				animator.SetBool(hash, value);
				if (!indefinite)
				{
					StartCoroutine(BoolOff(time, animator, parameter));
				}
				//YargLogger.LogDebug($"Venue Animator {parameter} On, successfully set");
			}
		}

		private void CheckFloat(Animator animator, string parameter, float value)
		{
			if (_paramhash.TryGetValue(parameter, out int hash))
			{
				animator.SetFloat(hash, value);
				//YargLogger.LogDebug($"Venue Animator Float {parameter} {value}, successfully set");
			}
		}

		private void RollRandom(Animator animator)
		{
			if (_paramhash.TryGetValue("RNG", out int hash))
			{
				float value = UnityEngine.Random.Range(100f, 1f);
				value = MathF.Round(value);
				animator.SetFloat(hash, value);
				//YargLogger.LogDebug($"Venue Animator RNG {value}, successfully set on {animator}");
			}
		}

		private void CheckBlend(float time, Animator animator, string next, string current, string type)
		{
			if (_paramhash.TryGetValue(next, out int hash))
			{
				int layer = animator.GetLayerIndex(type);
				if (layer == -1)
				{
					layer = 0;
				}
				animator.CrossFadeInFixedTime(hash, time, layer);
				StartCoroutine(BlendEnd(time, type, next));
				//YargLogger.LogDebug($"Venue Animator {type} Crossfade from {current} to {next} on layer {layer} for {time} seconds");
			}
		}

		private IEnumerator VocalPitchUpdate(float time, Animator animator, VocalNote note, string instrument)
		{
			float timer = 0f;
			int prevPitch = 0;
			int currentPitch = 0;
			while (timer < time)
			{
				timer += Time.deltaTime;
				currentPitch = Mathf.Clamp(Mathf.RoundToInt(note.PitchAtSongTime(GameManager.SongTime)), 35, 100) - 35;
				if (currentPitch != prevPitch)
				{
					CheckTrigger(animator, instrument + _vocalNoteNames[currentPitch]);
					//YargLogger.LogDebug($"Venue Animator {instrument} Trigger: {instrument}{_vocalNoteNames[currentPitch]}");
					prevPitch = currentPitch;
				}
				CheckFloat(animator, instrument + "Pitch", note.PitchAtSongTime(GameManager.SongTime));
				//YargLogger.LogDebug($"Venue Animator Vocals Float: {note.PitchAtSongTime(GameManager.SongTime)}");
				yield return null;
			}
			CheckBool(animator, instrument + "Note", false, 1f, true);
			//YargLogger.LogDebug($"Venue Animator {instrument}Note Off");
		}

		private IEnumerator BoolOff(float time, Animator animator, string parameter)
		{
			yield return new WaitForSeconds(time);
			animator.SetBool(parameter, false);
			//YargLogger.LogDebug($"Venue Animator {parameter} Off");
		}

		private IEnumerator BlendEnd(float time, string type, string next)
		{
			yield return new WaitForSeconds(time + 0.005f);
			if (type == "Lighting")
			{
				_currentLight = next;
				_lightBlended = false;
			}
			if (type == "Post Processing")
			{
				_currentPP = next;
				_PPBlended = false;
			}
			//YargLogger.LogDebug($"Venue Animator {type} Blend Ended");
		}
	}
}