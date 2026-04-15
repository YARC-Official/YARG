using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(YARG.Venue.VenueAnimator))]
[CanEditMultipleObjects]
public class VenueAnimator : UnityEditor.Editor
{
	SerializedProperty _lightingAnimator;
	SerializedProperty _postProcessingAnimator;
	SerializedProperty _stageFXAnimator;
	SerializedProperty _crowdAnimator;
	SerializedProperty _cameraAnimator;
	SerializedProperty _beatlineAnimator;
	SerializedProperty _happinessAnimator;
	SerializedProperty _guitarAnimator;
	SerializedProperty _proGuitarAnimator;
	SerializedProperty _bassAnimator;
	SerializedProperty _proBassAnimator;
	SerializedProperty _drumAnimator;
	SerializedProperty _drumAnimAnimator;
	SerializedProperty _keysAnimator;
	SerializedProperty _proKeysAnimator;
	SerializedProperty _vocalAnimator;
	SerializedProperty _harmony1Animator;
	SerializedProperty _harmony2Animator;
	SerializedProperty _animationBPM;
	SerializedProperty _lightingEnable;
	SerializedProperty _postProcessingEnable;
	SerializedProperty _stageFXEnable;
	SerializedProperty _crowdEnable;
	SerializedProperty _guitarNotesEnable;
	SerializedProperty _proGuitarNotesEnable;
	SerializedProperty _bassNotesEnable;
	SerializedProperty _proBassNotesEnable;
	SerializedProperty _drumNotesEnable;
	SerializedProperty _drumAnimEnable;
	SerializedProperty _keysNotesEnable;
	SerializedProperty _proKeysNotesEnable;
	SerializedProperty _vocalNotesEnable;
	SerializedProperty _harmony1NotesEnable;
	SerializedProperty _harmony2NotesEnable;
	SerializedProperty _beatlineEnable;
	SerializedProperty _happinessEnable;
	SerializedProperty _cameraEnable;
	SerializedProperty _leadingFramesLighting;
	SerializedProperty _leadingFramesPostProcessing;
	SerializedProperty _leadingFramesStage;
	SerializedProperty _leadingFramesCrowd;
	SerializedProperty _leadingFramesCamera;
	SerializedProperty _leadingFramesBeatline;
	SerializedProperty _leadingFramesGuitar;
	SerializedProperty _leadingFramesProGuitar;
	SerializedProperty _leadingFramesBass;
	SerializedProperty _leadingFramesProBass;
	SerializedProperty _leadingFramesDrums;
	SerializedProperty _leadingFramesDrumAnim;
	SerializedProperty _leadingFramesKeys;
	SerializedProperty _leadingFramesProKeys;
	SerializedProperty _leadingFramesVocals;
	SerializedProperty _leadingFramesHarmony1;
	SerializedProperty _leadingFramesHarmony2;

	bool showLighting, showPostProcessing, showFX, showCrowd, showCamera, showBeatlines, showHappiness, showNotes,
		listParameters, listLighting, listPostProcessing, listFX, listCrowd, listCamera, listHappiness, listBeat,
		listGuitar, listProGuitar, listBass, listProBass, listDrums, listDrumAnim, listKeys, listProKeys,
		listVocals, listHarmony1, listHarmony2 = false;

	void OnEnable()
	{
		_lightingAnimator = serializedObject.FindProperty("_lightingAnimator");
		_postProcessingAnimator = serializedObject.FindProperty("_postProcessingAnimator");
		_stageFXAnimator = serializedObject.FindProperty("_stageFXAnimator");
		_crowdAnimator = serializedObject.FindProperty("_crowdAnimator");
		_cameraAnimator = serializedObject.FindProperty("_cameraAnimator");
		_beatlineAnimator = serializedObject.FindProperty("_beatlineAnimator");
		_happinessAnimator = serializedObject.FindProperty("_happinessAnimator");
		_guitarAnimator = serializedObject.FindProperty("_guitarAnimator");
		_proGuitarAnimator = serializedObject.FindProperty("_proGuitarAnimator");
		_bassAnimator = serializedObject.FindProperty("_bassAnimator");
		_proBassAnimator = serializedObject.FindProperty("_proBassAnimator");
		_drumAnimator = serializedObject.FindProperty("_drumAnimator");
		_drumAnimAnimator = serializedObject.FindProperty("_drumAnimAnimator");
		_keysAnimator = serializedObject.FindProperty("_keysAnimator");
		_proKeysAnimator = serializedObject.FindProperty("_proKeysAnimator");
		_vocalAnimator = serializedObject.FindProperty("_vocalAnimator");
		_harmony1Animator = serializedObject.FindProperty("_harmony1Animator");
		_harmony2Animator = serializedObject.FindProperty("_harmony2Animator");

		_lightingEnable = serializedObject.FindProperty("_lightingEnable");
		_postProcessingEnable = serializedObject.FindProperty("_postProcessingEnable");
		_stageFXEnable = serializedObject.FindProperty("_stageFXEnable");
		_crowdEnable = serializedObject.FindProperty("_crowdEnable");
		_cameraEnable = serializedObject.FindProperty("_cameraEnable");
		_beatlineEnable = serializedObject.FindProperty("_beatlineEnable");
		_happinessEnable = serializedObject.FindProperty("_happinessEnable");
		_guitarNotesEnable = serializedObject.FindProperty("_guitarNotesEnable");
		_proGuitarNotesEnable = serializedObject.FindProperty("_proGuitarNotesEnable");
		_bassNotesEnable = serializedObject.FindProperty("_bassNotesEnable");
		_proBassNotesEnable = serializedObject.FindProperty("_proBassNotesEnable");
		_drumNotesEnable = serializedObject.FindProperty("_drumNotesEnable");
		_drumAnimEnable = serializedObject.FindProperty("_drumAnimEnable");
		_keysNotesEnable = serializedObject.FindProperty("_keysNotesEnable");
		_proKeysNotesEnable = serializedObject.FindProperty("_proKeysNotesEnable");
		_vocalNotesEnable = serializedObject.FindProperty("_vocalNotesEnable");
		_harmony1NotesEnable = serializedObject.FindProperty("_harmony1NotesEnable");
		_harmony2NotesEnable = serializedObject.FindProperty("_harmony2NotesEnable");

		_animationBPM = serializedObject.FindProperty("_animationBPM");
		_leadingFramesLighting = serializedObject.FindProperty("_leadingFramesLighting");
		_leadingFramesPostProcessing = serializedObject.FindProperty("_leadingFramesPostProcessing");
		_leadingFramesStage = serializedObject.FindProperty("_leadingFramesStage");
		_leadingFramesCrowd = serializedObject.FindProperty("_leadingFramesCrowd");
		_leadingFramesCamera = serializedObject.FindProperty("_leadingFramesCamera");
		_leadingFramesGuitar = serializedObject.FindProperty("_leadingFramesGuitar");
		_leadingFramesProGuitar = serializedObject.FindProperty("_leadingFramesProGuitar");
		_leadingFramesBass = serializedObject.FindProperty("_leadingFramesBass");
		_leadingFramesProBass = serializedObject.FindProperty("_leadingFramesProBass");
		_leadingFramesDrums = serializedObject.FindProperty("_leadingFramesDrums");
		_leadingFramesDrumAnim = serializedObject.FindProperty("_leadingFramesDrumAnim");
		_leadingFramesKeys = serializedObject.FindProperty("_leadingFramesKeys");
		_leadingFramesProKeys = serializedObject.FindProperty("_leadingFramesProKeys");
		_leadingFramesVocals = serializedObject.FindProperty("_leadingFramesVocals");
		_leadingFramesHarmony1 = serializedObject.FindProperty("_leadingFramesHarmony1");
		_leadingFramesHarmony2 = serializedObject.FindProperty("_leadingFramesHarmony2");
		_leadingFramesBeatline = serializedObject.FindProperty("_leadingFramesBeatline");
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		EditorGUILayout.PropertyField(_animationBPM);
		listParameters = EditorGUILayout.Foldout(listParameters, "Animator Parameters (for reference)");
		if(listParameters)
		{
			EditorGUILayout.HelpBox(message:"If added to one animator attached to this instance of the script, BPMAdjust will need to be added to all attached animators to not spam warnings in a user's log file.", MessageType.Info);
			EditorGUILayout.LabelField("Float");
			EditorGUILayout.SelectableLabel("BPMAdjust", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
			EditorGUILayout.LabelField("Float (1-100, rounded to whole number)");
			EditorGUILayout.SelectableLabel("RNG", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
			EditorGUILayout.Space();
		}

		showLighting = EditorGUILayout.BeginFoldoutHeaderGroup(showLighting, "Lighting Cues");
		if(showLighting)
		{
			EditorGUILayout.PropertyField(_lightingEnable);

			if(_lightingEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_lightingAnimator);
				EditorGUILayout.PropertyField(_leadingFramesLighting, new GUIContent("Leading Frames (60 FPS)"));
				listLighting = EditorGUILayout.Foldout(listLighting, "Lighting Cue Triggers (for reference)");
				if(listLighting)
				{
					EditorGUILayout.HelpBox(message:"Suggestions: Transitions between different light cues should be instant or near-instant, and keyframe transitions last roughly a quarter of a beat.", MessageType.None);
					EditorGUILayout.LabelField("Keyframed");
					EditorGUILayout.HelpBox(message:"Add multiple states in a cycle, transition with the keyframe triggers. Most usually have two states, with LightDefault being the exception with three. This does not have to be strictly followed.", MessageType.None);
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("LightDefault", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Verse", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Chorus", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("CoolManual", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("WarmManual", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Dischord", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("KeyframeNext", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("KeyframePrevious", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("KeyframeFirst", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Automatic");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Harmony", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Searchlights", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Sweep", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("WarmAutomatic", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("CoolAutomatic", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Silhouettes", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("SilhouettesSpotlight", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Frenzy", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BigRockEnding", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Stomp", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("FlareFast", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("FlareSlow", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BlackoutFast", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("BlackoutSlow", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BlackoutSpotlight", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("StrobeFast", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("StrobeSlow", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.HelpBox(message:"Animation states will need to be on either the base layer or a layer named 'Lighting' for blended transitions to work!", MessageType.Info);
					EditorGUILayout.Space();
				}
			}
		}
		EditorGUILayout.EndFoldoutHeaderGroup();

		showPostProcessing = EditorGUILayout.BeginFoldoutHeaderGroup(showPostProcessing, "Post Processing");
		if(showPostProcessing)
		{
			EditorGUILayout.PropertyField(_postProcessingEnable);
			if (_postProcessingEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_postProcessingAnimator);
				EditorGUILayout.PropertyField(_leadingFramesPostProcessing, new GUIContent("Leading Frames (60 FPS)"));
				listPostProcessing = EditorGUILayout.Foldout(listPostProcessing, "Post Processing Triggers (for reference)");
				if(listPostProcessing)
				{
					EditorGUILayout.LabelField("Basic");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("PPDefault", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Bloom", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Bright", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Contrast", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Posterize", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("PhotoNegative", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Mirror", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Color Effects");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("BlackAndWhite", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("SepiaTone", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("SilverTone", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Choppy_BlackAndWhite", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("PhotoNegative_RedAndBlack", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Polarized_BlackAndWhite", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Polarized_RedAndBlue", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Desaturated_Blue", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Desaturated_Red", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Contrast_Red", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Contrast_Green", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Contrast_Blue", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Grainy");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Grainy_Film", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Grainy_ChromaticAbberation", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Scanlines");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Scanlines", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Scanlines_BlackAndWhite", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Scanlines_Blue", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Scanlines_Security", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Trails");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Trails", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Trails_Long", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Trails_Desaturated", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Trails_Flickery", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Trails_Spacey", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.HelpBox(message:"Animation states will need to be on either the base layer or a layer named 'Post Processing' for blended transitions to work!", MessageType.Info);
					EditorGUILayout.Space();
				}
			}
		}
		EditorGUILayout.EndFoldoutHeaderGroup();

		showFX = EditorGUILayout.BeginFoldoutHeaderGroup(showFX, "Fog and FX");
		if(showFX)
		{
			EditorGUILayout.PropertyField(_stageFXEnable);
			if (_stageFXEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_stageFXAnimator);
				EditorGUILayout.PropertyField(_leadingFramesStage, new GUIContent("Leading Frames (60 FPS)"));
				listFX = EditorGUILayout.Foldout(listFX, "Fog and FX Triggers (for reference)");
				if (listFX)
				{
					EditorGUILayout.LabelField("Bool");
					EditorGUILayout.SelectableLabel("Fog", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Trigger");
					EditorGUILayout.SelectableLabel("BonusFx", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					EditorGUILayout.Space();
				}
			}

		}
		EditorGUILayout.EndFoldoutHeaderGroup();

		showCrowd = EditorGUILayout.BeginFoldoutHeaderGroup(showCrowd, "Crowd");
		if(showCrowd)
		{
			EditorGUILayout.PropertyField(_crowdEnable);
			if (_crowdEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_crowdAnimator);
				EditorGUILayout.PropertyField(_leadingFramesCrowd, new GUIContent("Leading Frames (60 FPS)"));
				listCrowd = EditorGUILayout.Foldout(listCrowd, "Crowd Parameters (for reference)");
				if (listCrowd)
				{
					EditorGUILayout.LabelField("Bool");
					EditorGUILayout.SelectableLabel("CrowdClap", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Trigger");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("CrowdNone", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("CrowdRealtime", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("CrowdMellow", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("CrowdNormal", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("CrowdIntense", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
				}
			}
		}
		EditorGUILayout.EndFoldoutHeaderGroup();

		showCamera = EditorGUILayout.BeginFoldoutHeaderGroup(showCamera, "Camera Cuts");
		if(showCamera)
		{
			EditorGUILayout.PropertyField(_cameraEnable);
			if (_cameraEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_cameraAnimator);
				EditorGUILayout.PropertyField(_leadingFramesCamera, new GUIContent("Leading Frames (60 FPS)"));
				listCamera = EditorGUILayout.Foldout(listCamera, "Camera Triggers (for reference)");
				if (listCamera)
				{
					EditorGUILayout.LabelField("Subject");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Crowd", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Stage", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("AllBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("AllFar", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("AllNear", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BehindNoDrum", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("NearNoDrum", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Guitar", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Drums", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("DrumsKick", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("DrumsBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("DrumsCloseupHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("DrumsCloseupHead", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Bass", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BassBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BassCloseup", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("BassCloseupHead", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Vocals", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalsCloseup", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalsBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Keys", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("KeysBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("KeysCloseupHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("KeysCloseupHead", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("DrumsVocals", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BassDrums", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("DrumsGuitar", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BassVocalsBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("BassVocals", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("GuitarVocalsBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("GuitarVocals", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("KeysVocalsBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("KeysVocals", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BassGuitarBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BassGuitar", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("BassKeysBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("BassKeys", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("GuitarKeysBehind", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("GuitarKeys", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
				}
			}
		}
		EditorGUILayout.EndFoldoutHeaderGroup();

		showBeatlines = EditorGUILayout.BeginFoldoutHeaderGroup(showBeatlines, "Beatlines");
		if (showBeatlines)
		{
			EditorGUILayout.PropertyField(_beatlineEnable);
			if (_beatlineEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_beatlineAnimator);
				EditorGUILayout.PropertyField(_leadingFramesBeatline, new GUIContent("Leading Frames (60 FPS)"));
				listBeat = EditorGUILayout.Foldout(listBeat, "Beatline Triggers (for reference)");
				if(listBeat)
				{
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Measure", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Strong", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Weak", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
				}
			}
		}
		EditorGUILayout.EndFoldoutHeaderGroup();

		showHappiness = EditorGUILayout.BeginFoldoutHeaderGroup(showHappiness, "Happiness");
		if(showHappiness)
		{
			EditorGUILayout.PropertyField(_happinessEnable);
			if (_happinessEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_happinessAnimator);
				listHappiness = EditorGUILayout.Foldout(listHappiness, "Happiness Parameters (for reference)");
				if(listHappiness)
				{
					EditorGUILayout.LabelField("Bool");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("HappyLow", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("HappyMed", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("HappyHigh", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Float");
					EditorGUILayout.SelectableLabel("Happiness", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					EditorGUILayout.Space();
				}
			}
		}
		EditorGUILayout.EndFoldoutHeaderGroup();

		showNotes = EditorGUILayout.BeginFoldoutHeaderGroup(showNotes, "Notes");
		if(showNotes)
		{
			EditorGUILayout.PropertyField(_guitarNotesEnable);
			if (_guitarNotesEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_guitarAnimator);
				EditorGUILayout.PropertyField(_leadingFramesGuitar, new GUIContent("Leading Frames (60 FPS)"));
				listGuitar = EditorGUILayout.Foldout(listGuitar, "Guitar 5L Bools (for reference)");
				if(listGuitar)
				{
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("gGreen", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("gRed", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("gYellow", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("gBlue", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("gOrange", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("gOpen", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_proGuitarNotesEnable);
			if (_proGuitarNotesEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_proGuitarAnimator);
				EditorGUILayout.PropertyField(_leadingFramesProGuitar, new GUIContent("Leading Frames (60 FPS)"));
				listProGuitar = EditorGUILayout.Foldout(listProGuitar, "Pro Guitar Bools (for reference)");
				if (listProGuitar)
				{
					EditorGUILayout.LabelField("All need individual bools with each fret number, e.g. pgG5, pgG6, pgG7");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("pgELo(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pgA(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pgD(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pgG(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pgB(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pgEHi(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_bassNotesEnable);
			if (_bassNotesEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_bassAnimator);
				EditorGUILayout.PropertyField(_leadingFramesBass, new GUIContent("Leading Frames (60 FPS)"));
				listBass = EditorGUILayout.Foldout(listBass, "Bass 5L Bools (for reference)");
				if(listBass)
				{
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("bGreen", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("bRed", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("bYellow", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("bBlue", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("bOrange", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("bOpen", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_proBassNotesEnable);
			if (_proBassNotesEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_proBassAnimator);
				EditorGUILayout.PropertyField(_leadingFramesProBass, new GUIContent("Leading Frames (60 FPS)"));
				listProBass = EditorGUILayout.Foldout(listProBass, "Pro Bass Bools (for reference)");
				if (listProBass)
				{
					EditorGUILayout.LabelField("All of these need individual bools with each number, e.g. pbG5");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("pbELo(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pbA(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pbD(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pbG(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pbB(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pbEHi(0-22)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_drumNotesEnable);
			if (_drumNotesEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_drumAnimator);
				EditorGUILayout.PropertyField(_leadingFramesDrums, new GUIContent("Leading Frames (60 FPS)"));
				listDrums = EditorGUILayout.Foldout(listDrums, "Drum Triggers (for reference)");
				if(listDrums)
				{
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("dKick", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("dRed", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("dYellow", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("dBlue", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("dGreen", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("dYellowCym", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("dBlueCym", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("dGreenCym", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_drumAnimEnable);
			if (_drumAnimEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_drumAnimAnimator);
				EditorGUILayout.PropertyField(_leadingFramesDrumAnim, new GUIContent("Leading Frames (60 FPS)"));
				listDrumAnim = EditorGUILayout.Foldout(listDrumAnim, "Drummer Animation Parameters (for reference)");
				if(listDrumAnim)
				{
					EditorGUILayout.LabelField("Trigger");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Kick", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("SnareLhHard", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("SnareRhHard", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("PercussionRightHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("SnareLhSoft", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("SnareRhSoft", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("HihatLeftHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("HihatRightHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Crash1LhHard", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Crash1LhSoft", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Crash1RhHard", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Crash1RhSoft", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Crash2RhHard", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Crash2RhSoft", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Crash1Choke", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Crash2Choke", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("RideRh", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("RideLh", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Crash2LhHard", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Crash2LhSoft", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Tom1LeftHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Tom1RightHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Tom2LeftHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Tom2RightHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("FloorTomLeftHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("FloorTomRightHand", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Bool");
					EditorGUILayout.SelectableLabel("OpenHiHat", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_keysNotesEnable);
			if (_keysNotesEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_keysAnimator);
				EditorGUILayout.PropertyField(_leadingFramesKeys, new GUIContent("Leading Frames (60 FPS)"));
				listKeys = EditorGUILayout.Foldout(listKeys, "Keys 5L Bool Names (for reference)");
				if(listKeys)
				{
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("kGreen", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("kRed", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("kYellow", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("kBlue", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("kOrange", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_proKeysNotesEnable);
			if (_proKeysNotesEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_proKeysAnimator);
				EditorGUILayout.PropertyField(_leadingFramesProKeys, new GUIContent("Leading Frames (60 FPS)"));
				listProKeys = EditorGUILayout.Foldout(listProKeys, "Pro Keys Bool Names (for reference)");
				if (listProKeys)
				{
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("pkC3", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkC3#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkD3", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkE3b", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkE3", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("pkF3", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkF3#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkG3", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkG3#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkA3", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("pkB3b", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkB3", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkC4", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkC4#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkD4", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("pkE4b", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkE4", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkF4", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkF4#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkG4", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("pkG4#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkA4", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkB4b", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkB4", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("pkC5", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_vocalNotesEnable);
			if (_vocalNotesEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_vocalAnimator);
				EditorGUILayout.PropertyField(_leadingFramesVocals, new GUIContent("Leading Frames (60 FPS)"));
				listVocals = EditorGUILayout.Foldout(listVocals, "Vocal Parameter Names (for reference)");
				if (listVocals)
				{
					EditorGUILayout.LabelField("Bool");
					EditorGUILayout.SelectableLabel("VocalNote", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Float, ranges from 36 to 84, use instead of triggers if pitch bending is wanted");
					EditorGUILayout.SelectableLabel("VocalPitch", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Triggers, repeat until reaching C5, e.g. VocalC2 to B2, VocalC3 to B3");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("VocalUnpitched", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("VocalC1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalC1#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalD1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalE1b", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("VocalE1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalF1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalF1#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalG1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("VocalG1#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalA1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalB1b", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("VocalB1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_harmony1NotesEnable);
			if (_harmony1NotesEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_harmony1Animator);
				EditorGUILayout.PropertyField(_leadingFramesHarmony1, new GUIContent("Leading Frames (60 FPS)"));
				listHarmony1 = EditorGUILayout.Foldout(listHarmony1, "Harmony 1 Parameter Names (for reference)");
				if (listHarmony1)
				{
					EditorGUILayout.LabelField("Bool");
					EditorGUILayout.SelectableLabel("Har1Note", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Float, ranges from 36 to 84, use instead of triggers if pitch bending is wanted");
					EditorGUILayout.SelectableLabel("Har1Pitch", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Triggers, repeat until reaching C5, e.g. Har1C2 to B2, Har1C3 to B3");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Har1Unpitched", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Har1C1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har1C1#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har1D1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har1E1b", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Har1E1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har1F1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har1F1#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har1G1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Har1G1#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har1A1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har1B1b", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har1B1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
			EditorGUILayout.Space();
			EditorGUILayout.PropertyField(_harmony2NotesEnable);
			if (_harmony2NotesEnable.boolValue)
			{
				EditorGUILayout.PropertyField(_harmony2Animator);
				EditorGUILayout.PropertyField(_leadingFramesHarmony2, new GUIContent("Leading Frames (60 FPS)"));
				listHarmony2 = EditorGUILayout.Foldout(listHarmony2, "Harmony 2 Parameter Names (for reference)");
				if (listHarmony2)
				{
					EditorGUILayout.LabelField("Bool");
					EditorGUILayout.SelectableLabel("Har2Note", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Float, ranges from 36 to 84, use instead of triggers if pitch bending is wanted");
					EditorGUILayout.SelectableLabel("Har2Pitch", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Triggers, repeat until reaching C5, e.g. Har2C2 to B2, Har2C3 to B3");
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Har2Unpitched", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Har2C1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har2C1#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har2D1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har2E1b", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Har2E1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har2F1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har2F1#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har2G1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.SelectableLabel("Har2G1#", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har2A1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har2B1b", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
						EditorGUILayout.SelectableLabel("Har2B1", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
					}
					EditorGUILayout.EndHorizontal();
				}
			}
		}

		serializedObject.ApplyModifiedProperties();
	}
}