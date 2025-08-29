using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Chart.Events;
using YARG.Core.Logging;
using AnimationType = YARG.Core.Chart.AnimationEvent.AnimationType;

namespace YARG.Venue.Characters
{
    public partial class VenueCharacter
    {
        private HashSet<string>                                              _availableAnimations      = new();
        private Dictionary<int, string>                                      _animationHashToAnimation = new();
        private Dictionary<int, string>                                      _stateHashToFullName      = new();
        private Dictionary<AnimationType, AnimationEventInfo>                _animationLookup          = new();
        private Dictionary<string, int>                                      _layerNameToIndex         = new();
        private Dictionary<string, int>                                      _leftHandPositionHashes   = new();
        private ChordShape                                                   _currentChordShape;

        private readonly AnimationEvents _animationEvents = new();
        private readonly List<string>    _triggerNames = new();
        private readonly HashSet<int>    _floatHashes = new();
        private readonly HashSet<int>    _boolHashes = new();
        private readonly HashSet<int>    _intHashes = new();

        private readonly Dictionary<string, int> _hashCache = new();


        private void PopulateAnimationData()
        {
            foreach (var parm in _animator.parameters)
            {
                if (parm.type == AnimatorControllerParameterType.Trigger)
                {
                    _triggerNames.Add(parm.name);
                }

                // TODO: The inconsistency here with the above is annoying
                if (parm.type == AnimatorControllerParameterType.Float)
                {
                    _floatHashes.Add(parm.nameHash);
                }

                if (parm.type == AnimatorControllerParameterType.Bool)
                {
                    _boolHashes.Add(parm.nameHash);
                }

                if (parm.type == AnimatorControllerParameterType.Int)
                {
                    _intHashes.Add(parm.nameHash);
                }
            }

            // Populate AnimationEvents with AnimationEventInfo


            // Reverse _layerStates to get a lookup of state names to layer indexes
            var layerDict = new Dictionary<string, List<int>>();

            foreach (var layer in _layerStates)
            {
                var index = _animator.GetLayerIndex(layer.Key);
                foreach (var stateName in layer.Value)
                {
                    layerDict.TryAdd(stateName, new List<int>());
                    layerDict[stateName].Add(index);
                }
            }

            if (_animationStates.Dictionary.Keys.Count > 0)
            {
                // If _animationStates is populated, use it to populate the AnimationEvents
                var animationDict = _animationStates.Dictionary;
                foreach (var stateType in animationDict.Keys)
                {
                    var stateName = animationDict[stateType];
                    var hash = Animator.StringToHash(stateName);
                    var hasTrigger = _triggerNames.Contains(stateName);
                    if (!layerDict.TryGetValue(stateName, out var layerList))
                    {
                        YargLogger.LogDebug($"Venue specified invalid state name: {stateName} for {stateType}");
                        continue;
                    }

                    foreach (var layer in layerList)
                    {
                        _animationEvents.Add(stateType, stateName, hash, layer, hasTrigger);
                    }
                }
            }
            else
            {
                foreach (var layer in _layerStates)
                {
                    var index = _animator.GetLayerIndex(layer.Key);

                    foreach (var state in layer.Value)
                    {
                        var hash = Animator.StringToHash(state);
                        bool hasTrigger = _triggerNames.Contains(state);

                        if (TryGetAnimationStateForName(state, out var animState))
                        {
                            _animationEvents.Add(animState, state, hash, index, hasTrigger);
                        }
                    }
                }
            }

            if (_animationEvents.HasState(AnimationStateType.Slap))
            {
                _hasSlap = true;
            }
        }

        /// <summary>
        /// Gets an animation state type for a given animation name
        /// <br /><br />
        /// <b>NOTE:</b> This is for backwards compatibility with early draft venues, it will eventually be removed
        /// </summary>
        /// <param name="name"></param>
        /// <param name="outVar"></param>
        /// <returns></returns>
        private static bool TryGetAnimationStateForName(string name, out AnimationStateType outVar)
        {
            // Deal with venues that don't have separate hard/soft animations
            var adjustedName = name switch
            {
                "SnareLeft"   => "SnareLeftHard",
                "SnareRight"  => "SnareRightHard",
                "Crash1Left"  => "Crash1LeftHard",
                "Crash1Right" => "Crash1RightHard",
                "Crash2Left"  => "Crash2LeftHard",
                "Crash2Right" => "Crash2RightHard",
                _             => name
            };

            AnimationStateType? animType = adjustedName switch
            {
                // Generic
                "Idle"            => AnimationStateType.Idle,
                "Playing"         => AnimationStateType.Playing,
                "IdleRealtime"    => AnimationStateType.IdleRealtime,
                "Intense"         => AnimationStateType.Intense,
                "Mellow"          => AnimationStateType.Mellow,
                // Drums
                "Kick"            => AnimationStateType.Kick,
                "OpenHat"         => AnimationStateType.OpenHiHat,
                "CloseHat"        => AnimationStateType.CloseHiHat,
                "SnareLeftHard"   => AnimationStateType.SnareLhHard,
                "SnareRightHard"  => AnimationStateType.SnareRhHard,
                "SnareLeftSoft"   => AnimationStateType.SnareLhSoft,
                "SnareRightSoft"  => AnimationStateType.SnareRhSoft,
                "HihatLeft"       => AnimationStateType.HihatLeftHand,
                "HihatRight"      => AnimationStateType.HihatRightHand,
                "PercussionRight" => AnimationStateType.PercussionRightHand,
                "Crash1LeftHard"  => AnimationStateType.Crash1LhHard,
                "Crash1LeftSoft"  => AnimationStateType.Crash1LhSoft,
                "Crash2LeftHard"  => AnimationStateType.Crash2LhHard,
                "Crash2LeftSoft"  => AnimationStateType.Crash2LhSoft,
                "Tom1Left"        => AnimationStateType.Tom1LeftHand,
                "Tom1Right"       => AnimationStateType.Tom1RightHand,
                "Tom2Left"        => AnimationStateType.Tom2LeftHand,
                "Tom2Right"       => AnimationStateType.Tom2RightHand,
                "FloorTomLeft"    => AnimationStateType.FloorTomLeftHand,
                "FloorTomRight"   => AnimationStateType.FloorTomRightHand,
                "RideLeft"        => AnimationStateType.RideLh,
                "RideRight"       => AnimationStateType.RideRh,
                // Five fret
                "HandPosition1"        => AnimationStateType.LeftHandPosition1,
                "HandPosition2"        => AnimationStateType.LeftHandPosition2,
                "HandPosition3"        => AnimationStateType.LeftHandPosition3,
                "HandPosition4"        => AnimationStateType.LeftHandPosition4,
                "HandPosition5"        => AnimationStateType.LeftHandPosition5,
                "HandPosition6"        => AnimationStateType.LeftHandPosition6,
                "HandPosition7"        => AnimationStateType.LeftHandPosition7,
                "HandPosition8"        => AnimationStateType.LeftHandPosition8,
                "HandPosition9"        => AnimationStateType.LeftHandPosition9,
                "HandPosition10"       => AnimationStateType.LeftHandPosition10,
                "HandPosition11"       => AnimationStateType.LeftHandPosition11,
                "HandPosition12"       => AnimationStateType.LeftHandPosition12,
                "HandPosition13"       => AnimationStateType.LeftHandPosition13,
                "HandPosition14"       => AnimationStateType.LeftHandPosition14,
                "HandPosition15"       => AnimationStateType.LeftHandPosition15,
                "HandPosition16"       => AnimationStateType.LeftHandPosition16,
                "HandPosition17"       => AnimationStateType.LeftHandPosition17,
                "HandPosition18"       => AnimationStateType.LeftHandPosition18,
                "HandPosition19"       => AnimationStateType.LeftHandPosition19,
                "HandPosition20"       => AnimationStateType.LeftHandPosition20,
                "HandPositionOne" => AnimationStateType.LeftHandPosition1,
                "HandPositionTwo" => AnimationStateType.LeftHandPosition2,
                "HandPositionThree" => AnimationStateType.LeftHandPosition3,
                "HandPositionFour" => AnimationStateType.LeftHandPosition4,
                "HandPositionFive" => AnimationStateType.LeftHandPosition5,
                "HandPositionSix" => AnimationStateType.LeftHandPosition6,
                "HandPositionSeven" => AnimationStateType.LeftHandPosition7,
                "HandPositionEight" => AnimationStateType.LeftHandPosition8,
                "HandPositionNine" => AnimationStateType.LeftHandPosition9,
                "HandPositionTen" => AnimationStateType.LeftHandPosition10,
                "HandPositionEleven" => AnimationStateType.LeftHandPosition11,
                "HandPositionTwelve" => AnimationStateType.LeftHandPosition12,
                "HandPositionThirteen" => AnimationStateType.LeftHandPosition13,
                "HandPositionFourteen" => AnimationStateType.LeftHandPosition14,
                "HandPositionFifteen" => AnimationStateType.LeftHandPosition15,
                "HandPositionSixteen" => AnimationStateType.LeftHandPosition16,
                "HandPositionSeventeen" => AnimationStateType.LeftHandPosition17,
                "HandPositionEighteen" => AnimationStateType.LeftHandPosition18,
                "HandPositionNineteen" => AnimationStateType.LeftHandPosition19,
                "HandPositionTwenty" => AnimationStateType.LeftHandPosition20,
                "StrumUp"                  => AnimationStateType.StrumUp,
                "StrumDown"                => AnimationStateType.StrumDown,
                "DefaultSingleLow"         => AnimationStateType.LhSingleLow,
                "DefaultSingleLowVibrato"  => AnimationStateType.LhSingleLowVibrato,
                "DefaultSingleHigh"        => AnimationStateType.LhSingleHigh,
                "DefaultSingleHighVibrato" => AnimationStateType.LhSingleHighVibrato,
                "DefaultChordLow"          => AnimationStateType.LhChordLow,
                "DefaultChordLowVibrato"   => AnimationStateType.LhChordLowVibrato,
                "DefaultChordHigh"         => AnimationStateType.LhChordHigh,
                "DefaultChordHighVibrato"  => AnimationStateType.LhChordHighVibrato,
                "DefaultOpen"              => AnimationStateType.LhOpenChord,
                "ChordA"                   => AnimationStateType.LhChordA,
                "ChordC"                   => AnimationStateType.LhChordC,
                "ChordD"                   => AnimationStateType.LhChordD,
                "DropD"                    => AnimationStateType.LhChordDropD,
                "DropDOpen"                => AnimationStateType.LhDropDOpen,
                "DropDVibrato"             => AnimationStateType.LhChordDropDVibrato,
                "DropD2"                   => AnimationStateType.LhChordDropD2,
                // Bass-only alternate RH animations
                "Pick"                     => AnimationStateType.Pick,
                "Slap"                     => AnimationStateType.Slap,
                "Finger"                   => AnimationStateType.Finger,
                _ => null
            };

            if (animType.HasValue)
            {
                outVar = animType.Value;
                return true;
            }

            outVar = default;
            return false;
        }

        private bool TryGetNameForAnimationType(AnimationType type, out string outVar)
        {
            var result = _animationLookup.TryGetValue(type, out var info);
            if (result)
            {
                outVar = info.Name;
                return true;
            }

            outVar = null;
            return false;
        }

        private class AnimationEvents
        {
            private readonly Dictionary<AnimationStateType, List<AnimationEventInfo>>     _lookup       = new();
            private readonly Dictionary<int, AnimationEventInfo>                          _lookupByHash = new();
            private readonly Dictionary<int, AnimationEventInfo>                          _lookupByFullPath = new();
            private readonly Dictionary<string, AnimationEventInfo>                       _lookupByName = new();
            private readonly Dictionary<AnimationType, AnimationStateType> _typeToState  = BuildTypeToState();

            public void Add(AnimationEventInfo info)
            {
                if (!_lookup.TryGetValue(info.Type, out var list))
                {
                    list = new List<AnimationEventInfo> { info };
                    _lookup.Add(info.Type, list);
                }
                else
                {
                    if (!list.Contains(info))
                    {
                        list.Add(info);
                    }
                    else
                    {
                        return;
                    }
                }

                _lookupByHash.TryAdd(info.Hash, info);
                _lookupByFullPath.TryAdd(info.FullPathHash, info);
                _lookupByName.TryAdd(info.Name, info);
            }

            public void Add(AnimationEventInfo[] infos)
            {
                foreach (var info in infos)
                {
                    Add(info);
                }
            }

            public void Add(AnimationStateType type, string name, int hash, int layer, bool hasTrigger)
            {
                Add(new AnimationEventInfo(type, name, hash, layer, hasTrigger));
            }

            public void Add(AnimationType type, string name, int hash, int layer, bool hasTrigger)
            {
                Add(GetStateForAnimationType(type), name, hash, layer, hasTrigger);
            }

            public bool TryGet(int hash, out AnimationEventInfo info)
            {
                return _lookupByHash.TryGetValue(hash, out info);
            }

            // TODO: Add fallbacks for missing animations that have reasonable alternatives
            public bool TryGet(string name, out AnimationEventInfo info)
            {
                return _lookupByName.TryGetValue(name, out info);
            }

            public bool TryGet(AnimationStateType type, out List<AnimationEventInfo> infos)
            {
                if (!_lookup.TryGetValue(type, out infos))
                {
                    return false;
                }

                // Check that there are actually states in the list
                if (infos.Count > 0)
                {
                    return true;
                }

                return false;
            }

            public bool TryGet(AnimationType type, out List<AnimationEventInfo> infos)
            {
                if (!_lookup.TryGetValue(_typeToState[type], out infos))
                {
                    return false;
                }

                // Check that there are actually states in the list
                if (infos.Count > 0)
                {
                    return true;
                }

                return false;
            }

            public bool HasState(AnimationStateType type)
            {
                return _lookup.ContainsKey(type);
            }

            public bool HasState(AnimationType type)
            {
                return _lookup.ContainsKey(_typeToState[type]);
            }

            public bool HasState(string name)
            {
                return _lookupByName.ContainsKey(name);
            }

            public bool HasState(int hash)
            {
                return _lookupByHash.ContainsKey(hash);
            }

            public bool TryGetFullPath(int hash, out AnimationEventInfo info)
            {
                return _lookupByFullPath.TryGetValue(hash, out info);
            }

            private static Dictionary<AnimationType, AnimationStateType> BuildTypeToState()
            {
                var result = new Dictionary<AnimationType, AnimationStateType>();

                foreach (var kind in Enum.GetValues(typeof(AnimationType)))
                {
                    result.Add((AnimationType) kind, GetStateForAnimationType((AnimationType) kind));
                }

                return result;
            }

            private static AnimationStateType GetStateForAnimationType(AnimationType type)
            {
                return type switch
                {
                    // Drums
                    AnimationType.Kick                => AnimationStateType.Kick,
                    AnimationType.OpenHiHat           => AnimationStateType.OpenHiHat,
                    AnimationType.CloseHiHat          => AnimationStateType.CloseHiHat,
                    AnimationType.HihatLeftHand       => AnimationStateType.HihatLeftHand,
                    AnimationType.HihatRightHand      => AnimationStateType.HihatRightHand,
                    AnimationType.SnareLhHard         => AnimationStateType.SnareLhHard,
                    AnimationType.SnareRhHard         => AnimationStateType.SnareRhHard,
                    AnimationType.SnareLhSoft         => AnimationStateType.SnareLhSoft,
                    AnimationType.SnareRhSoft         => AnimationStateType.SnareRhSoft,
                    AnimationType.PercussionRightHand => AnimationStateType.PercussionRightHand,
                    AnimationType.Crash1LhHard        => AnimationStateType.Crash1LhHard,
                    AnimationType.Crash1LhSoft        => AnimationStateType.Crash1LhSoft,
                    AnimationType.Crash1RhHard        => AnimationStateType.Crash1RhHard,
                    AnimationType.Crash1RhSoft        => AnimationStateType.Crash1RhSoft,
                    AnimationType.Crash2LhHard        => AnimationStateType.Crash2LhHard,
                    AnimationType.Crash2LhSoft        => AnimationStateType.Crash2LhSoft,
                    AnimationType.Crash2RhHard        => AnimationStateType.Crash2RhHard,
                    AnimationType.Crash2RhSoft        => AnimationStateType.Crash2RhSoft,
                    AnimationType.Crash1Choke         => AnimationStateType.Crash1Choke,
                    AnimationType.Crash2Choke         => AnimationStateType.Crash2Choke,
                    AnimationType.RideRh              => AnimationStateType.RideRh,
                    AnimationType.RideLh              => AnimationStateType.RideLh,
                    AnimationType.Tom1LeftHand        => AnimationStateType.Tom1LeftHand,
                    AnimationType.Tom1RightHand       => AnimationStateType.Tom1RightHand,
                    AnimationType.Tom2LeftHand        => AnimationStateType.Tom2LeftHand,
                    AnimationType.Tom2RightHand       => AnimationStateType.Tom2RightHand,
                    AnimationType.FloorTomLeftHand    => AnimationStateType.FloorTomLeftHand,
                    AnimationType.FloorTomRightHand   => AnimationStateType.FloorTomRightHand,
                    // Five Fret
                    AnimationType.LeftHandPosition1  => AnimationStateType.LeftHandPosition1,
                    AnimationType.LeftHandPosition2  => AnimationStateType.LeftHandPosition2,
                    AnimationType.LeftHandPosition3  => AnimationStateType.LeftHandPosition3,
                    AnimationType.LeftHandPosition4  => AnimationStateType.LeftHandPosition4,
                    AnimationType.LeftHandPosition5  => AnimationStateType.LeftHandPosition5,
                    AnimationType.LeftHandPosition6  => AnimationStateType.LeftHandPosition6,
                    AnimationType.LeftHandPosition7  => AnimationStateType.LeftHandPosition7,
                    AnimationType.LeftHandPosition8  => AnimationStateType.LeftHandPosition8,
                    AnimationType.LeftHandPosition9  => AnimationStateType.LeftHandPosition9,
                    AnimationType.LeftHandPosition10 => AnimationStateType.LeftHandPosition10,
                    AnimationType.LeftHandPosition11 => AnimationStateType.LeftHandPosition11,
                    AnimationType.LeftHandPosition12 => AnimationStateType.LeftHandPosition12,
                    AnimationType.LeftHandPosition13 => AnimationStateType.LeftHandPosition13,
                    AnimationType.LeftHandPosition14 => AnimationStateType.LeftHandPosition14,
                    AnimationType.LeftHandPosition15 => AnimationStateType.LeftHandPosition15,
                    AnimationType.LeftHandPosition16 => AnimationStateType.LeftHandPosition16,
                    AnimationType.LeftHandPosition17 => AnimationStateType.LeftHandPosition17,
                    AnimationType.LeftHandPosition18 => AnimationStateType.LeftHandPosition18,
                    AnimationType.LeftHandPosition19 => AnimationStateType.LeftHandPosition19,
                    AnimationType.LeftHandPosition20 => AnimationStateType.LeftHandPosition20,

                    // Default case
                    _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unexpected animation type: {type}")
                };
            }
        }

        private class AnimationEventInfo
        {
            public AnimationEventInfo(AnimationStateType type, string name, int hash, int layer, bool hasTrigger)
            {
                Type = type;
                Name = name;
                Hash = hash;
                Layer = layer;
                HasTrigger = hasTrigger;
                FullPathHash = Animator.StringToHash($"{Layer}.{Name}");
            }

            public readonly AnimationStateType Type;
            public readonly string             Name;
            public readonly int                Hash;
            public readonly int                Layer;
            public readonly bool               HasTrigger;
            public readonly int                FullPathHash;

            public bool IsSameLayer(AnimationEventInfo other)
            {
                return Layer == other.Layer;
            }

            // AnimationEventInfo is only equals if all fields match
            public override bool Equals(object obj)
            {
                return obj is AnimationEventInfo other &&
                    Hash == other.Hash &&
                    Type == other.Type &&
                    Name == other.Name &&
                    Layer == other.Layer;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Type, Name, Hash, Layer);
            }

            public override string ToString()
            {
                return $"{Type} ({Name})";
            }

            // We also need ==
            public static bool operator ==(AnimationEventInfo left, AnimationEventInfo right)
            {
                return left?.Equals(right) ?? right is null;
            }

            public static bool operator !=(AnimationEventInfo left, AnimationEventInfo right)
            {
                return !(left == right);
            }
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public enum AnimationStateType
        {
            // Generic
            Idle,
            Playing,
            // Charted animation states
            IdleRealtime,
            Intense,
            Mellow,
            // Drums
            Kick,
            OpenHiHat,
            CloseHiHat,
            SnareLhHard,
            SnareRhHard,
            SnareLhSoft,
            SnareRhSoft,
            HihatLeftHand,
            HihatRightHand,
            PercussionRightHand,
            Crash1LhHard,
            Crash1LhSoft,
            Crash1RhHard,
            Crash1RhSoft,
            Crash2RhHard,
            Crash2RhSoft,
            Crash1Choke,
            Crash2Choke,
            RideRh,
            RideLh,
            Crash2LhHard,
            Crash2LhSoft,
            Tom1LeftHand,
            Tom1RightHand,
            Tom2LeftHand,
            Tom2RightHand,
            FloorTomLeftHand,
            FloorTomRightHand,
            // Five fret
            LeftHandPosition1,
            LeftHandPosition2,
            LeftHandPosition3,
            LeftHandPosition4,
            LeftHandPosition5,
            LeftHandPosition6,
            LeftHandPosition7,
            LeftHandPosition8,
            LeftHandPosition9,
            LeftHandPosition10,
            LeftHandPosition11,
            LeftHandPosition12,
            LeftHandPosition13,
            LeftHandPosition14,
            LeftHandPosition15,
            LeftHandPosition16,
            LeftHandPosition17,
            LeftHandPosition18,
            LeftHandPosition19,
            LeftHandPosition20,
            StrumUp,
            StrumDown,
            // Hand shapes
            LhSingleHigh,
            LhSingleLow,
            LhChordHigh,
            LhChordLow,
            LhOpenChord,
            LhSingleHighVibrato,
            LhSingleLowVibrato,
            LhChordHighVibrato,
            LhChordLowVibrato,
            LhChordA,
            LhChordC,
            LhChordD,
            LhChordDropD,
            LhDropDOpen,
            LhChordDropDVibrato,
            LhChordDropD2,
            // Bass only alternate right hand anims
            Slap,
            Pick,
            Finger,
            IdleIntense,
            PlayingSolo,
            // Vocal Animations
            MouthOpen,
            MouthClose
        }

        private AnimationStateType? GetAnimationStateForHandMap(HandMap.HandMapType handMap)
        {
            return handMap switch
            {
                HandMap.HandMapType.ChordA => AnimationStateType.LhChordA,
                HandMap.HandMapType.ChordC => AnimationStateType.LhChordC,
                HandMap.HandMapType.ChordD => AnimationStateType.LhChordD,
                HandMap.HandMapType.DropD => AnimationStateType.LhChordDropD,
                HandMap.HandMapType.DropD2 => AnimationStateType.LhChordDropD2,
                _ => null
            };
        }

        private AnimationStateType GetDrumAnimationForNote(DrumNote child)
        {
            var pad = (FourLaneDrumPad) child.Pad;
            return pad switch
            {
                FourLaneDrumPad.Kick => AnimationStateType.Kick,
                FourLaneDrumPad.YellowCymbal => AnimationStateType.HihatRightHand,
                FourLaneDrumPad.BlueCymbal => AnimationStateType.RideRh,
                FourLaneDrumPad.GreenCymbal => AnimationStateType.Crash1RhHard,
                FourLaneDrumPad.GreenDrum => AnimationStateType.FloorTomRightHand,
                FourLaneDrumPad.BlueDrum => AnimationStateType.Tom2RightHand,
                FourLaneDrumPad.YellowDrum => AnimationStateType.Tom1RightHand,
                FourLaneDrumPad.RedDrum => AnimationStateType.SnareLhHard,
                _ => throw new ArgumentOutOfRangeException(nameof(pad), pad, "Bad drum pad how?")
            };
        }

        private void SetTrigger(CharacterState.CharacterStateType state)
        {
            SetTrigger(CharacterStateAnimationStates[state]);
        }

        private void SetTrigger(string triggerName)
        {
            // See if this exists, and if so, trigger an animation
            if (_animationEvents.TryGet(triggerName, out var animationEvent))
            {
                if (animationEvent.HasTrigger)
                {
                    _animator.SetTrigger(animationEvent.Hash);
                }
                else
                {
                    // No trigger, so we have to call the animation directly
                    _animator.Play(animationEvent.Hash, animationEvent.Layer, 0f);
                }
            }
            else
            {
                YargLogger.LogFormatDebug("Animation State '{0}' not found", triggerName);
            }
        }

        private void SetTrigger(List<AnimationEventInfo> animations)
        {
            foreach (var animation in animations)
            {
                SetTrigger(animation.Type);
            }
        }

        private void SetTrigger(AnimationStateType state)
        {
            if (_animationEvents.TryGet(state, out var list))
            {
                if (list.Count == 0)
                {
                    // TODO: Change me to trace
                    YargLogger.LogFormatDebug("AnimationStateType {0} found, but has no animations", state);
                    return;
                }

                bool hasTrigger = false;
                int hash = 0;
                foreach (var info in list)
                {
                    if (info.HasTrigger)
                    {
                        hasTrigger = true;
                        hash = info.Hash;
                        continue;
                    }

                    _animator.Play(info.Hash, info.Layer, 0f);
                }

                if (hasTrigger)
                {
                    // We have to special case some of the generic animation states since they are layered with a bool
                    if (IsGenericState(state))
                    {
                        YargLogger.LogFormatDebug("Setting trigger for generic state {0}", state);
                        // First, reset the bools to false (if they exist)
                        SetBool("isMellow", false);
                        SetBool("isIntense", false);

                        // Now reset them if necessary so that transitions can use them to select the correct variety
                        if (IsLayeredState(state))
                        {
                            switch (state)
                            {
                                case AnimationStateType.Mellow:
                                    SetBool("isMellow", true);
                                    break;
                                case AnimationStateType.Intense:
                                    SetBool("isIntense", true);
                                    break;
                            };
                        }
                    }

                    _animator.SetTrigger(hash);
                }
                else if (_animationStateFallbacks.TryGetValue(state, out var fallback))
                {
                    // ew, recursion (provably finite in the case that the fallbacks dict is finite, though)
                    SetTrigger(fallback);
                }
            }
            else
            {
                YargLogger.LogFormatDebug("Animation State '{0}' not found", state);
            }
        }

        private void SetFloat(int hash, float value)
        {
            if (_floatHashes.Contains(hash))
            {
                _animator.SetFloat(hash, value);
            }
        }

        private void SetFloat(string property, float value)
        {
            if (!_hashCache.TryGetValue(property, out var hash))
            {
                hash = Animator.StringToHash(name);
                _hashCache.Add(property, hash);
            }

            SetFloat(hash, value);
        }

        private void SetBool(int hash, bool value)
        {
            if (_boolHashes.Contains(hash))
            {
                _animator.SetBool(hash, value);
            }
        }

        private void SetBool(string property, bool value)
        {
            if (!_hashCache.TryGetValue(property, out var hash))
            {
                hash = Animator.StringToHash(property);
                _hashCache.Add(property, hash);
            }

            SetBool(hash, value);
        }

        private void SetInteger(int hash, int value)
        {
            if (_intHashes.Contains(hash))
            {
                _animator.SetInteger(hash, value);
            }
        }

        private void SetInteger(string property, int value)
        {
            if (!_hashCache.TryGetValue(property, out var hash))
            {
                hash = Animator.StringToHash(property);
                _hashCache.Add(property, hash);
            }

            SetInteger(hash, value);
        }

        private static bool IsLayeredState(AnimationStateType state)
        {
            return state is AnimationStateType.Mellow or AnimationStateType.Intense;
        }

        private static bool IsGenericState(AnimationStateType state)
        {
            return state is AnimationStateType.Idle or AnimationStateType.Playing or AnimationStateType.IdleRealtime
                or AnimationStateType.Mellow or AnimationStateType.Intense;
        }

        // TODO: Extend this to more than just what happens to be needed for the test venue
        private static readonly Dictionary<AnimationStateType, AnimationStateType> _animationStateFallbacks = new()
        {
            // Generic states
            { AnimationStateType.IdleRealtime, AnimationStateType.Idle },
            { AnimationStateType.Mellow, AnimationStateType.Playing },
            { AnimationStateType.Intense, AnimationStateType.Playing },

            // Hand maps
            { AnimationStateType.LhChordDropD2, AnimationStateType.LhChordDropD }
        };

        public enum ChordShape
        {
            SingleHigh,
            SingleLow,
            ChordHigh,
            ChordLow,
            OpenChord,
            SingleHighVibrato,
            SingleLowVibrato,
            ChordHighVibrato,
            ChordLowVibrato,
            ChordA,
            ChordC,
            ChordD,
            DropD,
            DropDOpen,
            DropDVibrato,
            DropD2,
        }

        public static readonly Dictionary<ChordShape, AnimationStateType> ChordAnimationStates = new()
        {
            { ChordShape.SingleHigh, AnimationStateType.LhSingleHigh },
            { ChordShape.SingleLow, AnimationStateType.LhSingleLow },
            { ChordShape.ChordHigh, AnimationStateType.LhChordHigh },
            { ChordShape.ChordLow, AnimationStateType.LhChordLow },
            { ChordShape.OpenChord, AnimationStateType.LhOpenChord },
            { ChordShape.SingleHighVibrato, AnimationStateType.LhSingleHighVibrato },
            { ChordShape.SingleLowVibrato, AnimationStateType.LhSingleLowVibrato },
            { ChordShape.ChordHighVibrato, AnimationStateType.LhChordHighVibrato },
            { ChordShape.ChordLowVibrato, AnimationStateType.LhChordLowVibrato },
            { ChordShape.ChordA, AnimationStateType.LhChordA },
            { ChordShape.ChordC, AnimationStateType.LhChordC },
            { ChordShape.ChordD, AnimationStateType.LhChordD },
            { ChordShape.DropD, AnimationStateType.LhChordDropD },
            { ChordShape.DropDOpen, AnimationStateType.LhDropDOpen },
            { ChordShape.DropDVibrato, AnimationStateType.LhChordDropDVibrato },
            { ChordShape.DropD2, AnimationStateType.LhChordDropD2 },
        };

        public static readonly Dictionary<CharacterState.CharacterStateType, AnimationStateType> CharacterStateAnimationStates = new()
        {
            { CharacterState.CharacterStateType.Idle, AnimationStateType.Idle },
            { CharacterState.CharacterStateType.IdleIntense, AnimationStateType.IdleIntense },
            { CharacterState.CharacterStateType.IdleRealtime, AnimationStateType.IdleRealtime },
            { CharacterState.CharacterStateType.Intense, AnimationStateType.Intense },
            { CharacterState.CharacterStateType.Mellow, AnimationStateType.Mellow },
            { CharacterState.CharacterStateType.Play, AnimationStateType.Playing },
            { CharacterState.CharacterStateType.PlaySolo, AnimationStateType.PlayingSolo }
        };
    }
}