﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.ProKeys.Engines;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Gameplay.Visuals;
using YARG.Settings;
using TMPro;

namespace YARG.Gameplay.Visuals
{
    public class ProKeysChordBarElement : TrackElement<ProKeysPlayer>
    {
        public ProKeysNote NoteRef { get; set; }

        public override double ElementTime => NoteRef.Time;

        [SerializeField]
        private float _middlePadding;
        [SerializeField]
        private float _endOffsets;

        [Space]
        [SerializeField]
        private Transform _container;
        [SerializeField]
        private Transform _middleModel;
        [SerializeField]
        private Transform _leftModel;
        [SerializeField]
        private Transform _rightModel;
        [Space]
        [SerializeField]
        private Transform _canvas;
        
        [SerializeField]
        private TextMeshPro _leftText;

        string[] chord_call = {};

        int chord_qnty = 0;
        int[] chord_notes = new int[25];

        protected override void InitializeElement()
        {
            // Get the min and max keys
            int? min = null;
            int? max = null;
            foreach (var note in NoteRef.AllNotes)
            {
                if (min is null || note.Key < min)
                {
                    min = note.Key;
                }

                if (max is null || note.Key > max)
                {
                    max = note.Key;
                }
            }

            // Subtract range shift offset because that will be applied to the container
            var minPos = Player.GetNoteX(min!.Value) - _middlePadding - Player.RangeShiftOffset;
            var maxPos = Player.GetNoteX(max!.Value) + _middlePadding - Player.RangeShiftOffset;

            var size = maxPos - minPos;
            var mid = (minPos + maxPos) / 2f;

            // Transform the middle model
            var cachedTransform = _middleModel.transform;
            cachedTransform.localScale = new Vector3(size, 1f, 1f);
            cachedTransform.localPosition = new Vector3(mid, 0f, 0f);

            // Transform the end models
            _leftModel.localPosition = _leftModel.localPosition.WithX(minPos - _endOffsets);
            _rightModel.localPosition = _rightModel.localPosition.WithX(maxPos + _endOffsets);

            if (SettingsManager.Settings.LearningGuides.Value) {
                ShowChordName();
            }
            else
            {
                _leftText.text = "";
            }
            // Update the container to the proper range shift offset
            UpdateXPosition();
        }

        public void UpdateXPosition()
        {
            _container.localPosition = _container.localPosition.WithX(Player.RangeShiftOffset);
        }

        public void CheckForChordHit()
        {
            // If the note was fully hit, remove the chord bar
            if (NoteRef.WasFullyHit())
            {
                ParentPool.Return(this);
            }
        }

        public void ShowChordName()
        {
            //Chordname
            string chord_name;
            _leftText.text = "";
            _leftText.textStyle = TMP_Style.NormalStyle;

            //Use the first note of the array to find the root
            string GetRoot(int note_slot) //note_slot being the note's position
            {
                return (chord_notes[note_slot] % 12) switch 
                {
                    0 => "C",
                    1 => "C♯",
                    2 => "D",
                    3 => "Eb",
                    4 => "E",
                    5 => "F",
                    6 => "F♯",
                    7 => "G",
                    8 => "G♯",
                    9 => "A",
                    10 => "Bb",
                    11 => "B",

                    _ => "X"
                };
            }

            foreach (var note in NoteRef.AllNotes)
            {
                for (int i = 0; i < 4; i++)
                {
                    chord_notes[i] = 0;
                }
                Array.Resize(ref chord_call, chord_call.Length + 1);

                foreach (var child in note.AllNotes)
                {
                    if (note.IsChord == true)
                    {
                        //Get the number of notes in the chord and equate to a int value in the array
                        chord_qnty += 1;
                        chord_notes[chord_qnty - 1] = child.Key;
                    }
                }
                if (chord_qnty <= 2)
                {
                }
                else
                {
                    Array.Sort(chord_notes, 0, chord_qnty);
                }

                //Using the lowest note, find the first and second interval for three note chords
                chord_name = GetRoot(0);
                if (chord_qnty == 3)
                {
                    switch (chord_notes[1] - chord_notes[0])
                    {
                        case 2: //major 2nd
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 5: //diminished
                                    chord_name += "<sup>sus2</sup>";
                                    break;
                                default: //minor
                                    chord_name = "";
                                    break;
                            };
                            break;
                        case 3: //minor 3rd
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 3: //diminished
                                    chord_name += "<sup>dim</sup>";
                                    break;
                                case 4: //minor
                                    chord_name += "m";
                                    break;
                                case 5: //major, 1st inversion
                                    chord_name = GetRoot(2);
                                    break;
                                case 6: //major, 1st inversion
                                    chord_name = GetRoot(2) + "dim";
                                    break;
                                case 7: //minor 7 no 5
                                    chord_name += "m<sup>7no5</sup>";
                                    break;
                                case 8: //minor 7 no 5
                                    chord_name += "m<sup>M7no5</sup>";
                                    break;
                                default:
                                    //chord_name = "an unlisted minor 3rd interval chord.";
                                    chord_name = "";
                                    break;
                            }
                            break;
                        case 4: //major 3rd
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 2:
                                    chord_name += "<sup>b5</sup>";
                                    break;
                                case 3: //major
                                    chord_name += "";
                                    break;
                                case 4: //augmented
                                    chord_name += "<sup>aug</sup>";
                                    break;
                                case 5: //minor, 1st inversion
                                    chord_name = GetRoot(2) + "m";
                                    break;
                                case 6:
                                    chord_name += "<sup>7no5</sup>";
                                    break;
                                case 7:
                                    chord_name += "M<sup>7no5</sup>";
                                    break;
                                case 8:
                                    chord_name += "<sup>no5</sup>";
                                    break;
                                default:
                                    chord_name = "";
                                    break;
                            }
                            break;
                        case 5: //perfect fourth
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 2: //minor, 2nd inversion
                                    chord_name += "<sup>sus4</sup>";
                                    break;
                                case 3: //minor, 2nd inversion
                                    chord_name = GetRoot(1) + "m";
                                    break;
                                case 4: //major, 2nd inversion
                                    chord_name = GetRoot(1);
                                    break;
                                case 5: //sus2 inversion
                                    chord_name = GetRoot(2) + "sus2";
                                    break;
                                case 7: //POWER!! 5
                                    chord_name = GetRoot(1) + "5/" + GetRoot(0);
                                    break;
                                default:
                                    chord_name = "";
                                    break;
                            }
                            break;
                        case 6: //diminished
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 3:
                                    chord_name += "dim7";
                                    break;
                                case 4:
                                    chord_name += "m7b5";
                                    break;
                                default:
                                    chord_name += "";
                                    break;
                            }
                            break;
                        case 7: //perfect fifth
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 3: //minor 7th with no 3
                                    chord_name += "m<sup>7no3</sup>";
                                    break;
                                case 4: //major 7 no 3
                                    chord_name += "<sup>7no3</sup>";
                                    break;
                                case 5: //POWER!! 5
                                    chord_name += "5";
                                    break;
                                default:
                                    chord_name = "";
                                    break;
                            }
                            break;
                        default:
                            chord_name = "";
                            break;
                    }
                    _leftText.text = chord_name;
                }
                else if (chord_qnty == 4)
                {
                    switch (chord_notes[1] - chord_notes[0])
                    {
                        case 2: //major 2nd
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 2: //C D E
                                    switch (chord_notes[3] - chord_notes[2])
                                    {
                                        case 3:
                                            chord_name += "<sup>add9</sup>";
                                            break;
                                        case 4:
                                            chord_name += "<sup>add9#5</sup>";
                                            break;
                                        case 5:
                                            chord_name = GetRoot(2) + "m<sup>sus4<sup>";
                                            break;
                                        case 6:
                                            chord_name += "<sup>7add9no5</sup>";
                                            break;
                                        case 7:
                                            chord_name += "M<sup>7add9no5</sup>";
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chord_name = "";
                                            break;
                                    }
                                    break;
                                case 3: //C D F
                                    switch (chord_notes[3] - chord_notes[2])
                                    {
                                        case 3:
                                            chord_name = GetRoot(1) + "m7b5/" + GetRoot(0);
                                            break;
                                        case 4:
                                            chord_name = GetRoot(1) + "m7/" + GetRoot(0);
                                            break;
                                        case 5:
                                            chord_name = GetRoot(3) + "m<sup>sus4</sup>/" + GetRoot(0);
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chord_name = "";
                                            break;
                                    }
                                    break;
                                case 4: //C D F#
                                    switch (chord_notes[3] - chord_notes[2])
                                    {
                                        case 2:
                                            chord_name = GetRoot(1) + "<sup>7b5</sup>/" + GetRoot(0);
                                            break;
                                        case 3:
                                            chord_name = GetRoot(1) + "<sup>7</sup>/" + GetRoot(0);
                                            break;
                                        case 4:
                                            chord_name = GetRoot(1) + "7#5/" + GetRoot(0);
                                            break;
                                        case 5:
                                            chord_name = GetRoot(3) + "m<sup>b9</sup>/" + GetRoot(0);
                                            break;
                                        default:
                                            chord_name = "";
                                            break;
                                    }
                                    break;
                                case 5: //C D G
                                    switch (chord_notes[3] - chord_notes[2])
                                    {
                                        case 3:
                                            chord_name += "7<sup>sus2</sup>";
                                            break;
                                        case 4:
                                            chord_name += "M7<sup>sus2</sup>";
                                            break;
                                        case 5:
                                            chord_name += "<sup>sus2</sup>";
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chord_name = "";
                                            break;
                                    }
                                    break;
                                case 7: 
                                    chord_name = "";
                                    break;
                                default:
                                    //chord_name = "an unlisted minor 2nd interval chord.";
                                    chord_name = "";
                                    break;
                            }
                            break;
                        case 3: //minor 3rd
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 2: //C D# F
                                    switch (chord_notes[3] - chord_notes[2])
                                    {
                                        case 2:
                                            chord_name += "m4";
                                            break;
                                        case 3:
                                            chord_name = GetRoot(2) + "m<sup>7</sup>/" + GetRoot(0);
                                            break;
                                        case 4:
                                            chord_name = GetRoot(2) + "<sup>7</sup>/" + GetRoot(0);
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chord_name = "";
                                            break;
                                    }
                                    break;
                                case 3: //C D# F#
                                    switch (chord_notes[3] - chord_notes[2])
                                    {
                                        case 2:
                                            chord_name = GetRoot(3) + "<sup>7</sup>/" + GetRoot(0);
                                            break;
                                        case 3:
                                            chord_name += "dim7";
                                            break;
                                        case 4:
                                            chord_name += "m7b5";
                                            break;
                                        case 5:
                                            chord_name += "mM7b5";
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chord_name = "";
                                            break;
                                    }
                                    break;
                                case 4: //minor
                                    switch (chord_notes[3] - chord_notes[2])
                                    {
                                        case 1:
                                            chord_name = GetRoot(3) + "M<sup>7</sup>/" + GetRoot(0);
                                            break;
                                        case 2:
                                            chord_name += "m<sup>6</sup>";
                                            break;
                                        case 3:
                                            chord_name += "m<sup>7</sup>";
                                            break;
                                        case 4:
                                            chord_name += "mM<sup>7</sup>";
                                            break;
                                        case 5:
                                            chord_name += "m";
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chord_name = "";
                                            break;
                                    }
                                    break;
                                case 5: //C D# G#
                                    switch (chord_notes[3] - chord_notes[2])
                                    {
                                        case 1:
                                            chord_name = GetRoot(2) + "<sup>addb9</sup>/" + GetRoot(0);
                                            break;
                                        case 2:
                                            chord_name = GetRoot(2) + "<sup>add9</sup>/" + GetRoot(0);
                                            break;
                                        case 3:
                                            chord_name = GetRoot(2);
                                            break;
                                        case 4:
                                            chord_name += "mM<sup>7</sup>/";
                                            break;
                                        case 5:
                                            chord_name += "m";
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chord_name = "";
                                            break;
                                    }
                                    break;
                                case 7: //minor 7 no 5
                                    chord_name += "<sup>m7no5</sup>";
                                    break;
                                default:
                                    //chord_name = "an unlisted minor 3rd interval chord.";
                                    chord_name += "";
                                    break;
                            }
                            break;
                        case 4: //major 3rd
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 1: //C E F
                                    switch (chord_notes[3] - chord_notes[2])
                                    {
                                        case 2:
                                            chord_name += "4";
                                            break;
                                        case 4:
                                            chord_name = GetRoot(2) + "M<sup>7</sup>/" + GetRoot(0);
                                            break;
                                        case 6:
                                            chord_name += "<sup>b9no7</sup>";
                                            break;
                                        case 7:
                                            chord_name += "M<sup>7add9no5</sup>";
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chord_name += "???";
                                            break;
                                    }
                                    break;
                                case 3: //C E G
                                    switch (chord_notes[3] - chord_notes[2])
                                    {
                                        case 2:
                                            chord_name += "<sup>6</sup>";
                                            break;
                                        case 3:
                                            chord_name += "<sup>7</sup>";
                                            break;
                                        case 4:
                                            chord_name += "M<sup>7</sup>";
                                            break;
                                        case 5:
                                            chord_name += "";
                                            break;
                                        case 6:
                                            chord_name += "<sup>b9no7</sup>";
                                            break;
                                        case 7:
                                            chord_name += "M<sup>7add9no5</sup>";
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chord_name += "???";
                                            break;
                                    }
                                    break;
                                case 4: //augmented
                                    chord_name += "<sup>aug</sup>";
                                    break;
                                case 5: //major, 1st inversion
                                    chord_name = GetRoot(2) + "m";
                                    break;
                                default:
                                    chord_name += "";
                                    break;
                            }
                            break;
                        case 5: //perfect fourth
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 2: //sus4
                                    chord_name += "<sup>sus4</sup>";
                                    break;
                                case 3: //minor, 2nd inversion
                                    chord_name = GetRoot(1) + "m";
                                    break;
                                case 4: //major, 2nd inversion
                                    chord_name = GetRoot(1) + "M";
                                    break;
                                case 5: //sus2 inversion
                                    chord_name = GetRoot(2) + "<sup>sus2</sup>";
                                    break;
                                default:
                                    //chord_name = "an unlisted minor perfect 4th chord.";
                                    chord_name += "";
                                    break;
                            }
                            break;
                        case 6: //diminished
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 3: //minor 7th with no 3
                                    chord_name += "<sup>m7no3</sup>";
                                    break;
                                case 4: //minor 7 flat 5
                                    chord_name += "m<sup>7no3</sup>";
                                    break;
                                default:
                                    chord_name += "";
                                    break;
                            }
                            break;
                        case 7: //perfect fifth
                            switch (chord_notes[2] - chord_notes[1])
                            {
                                case 3: //minor 7th with no 3
                                    chord_name += "<sup>m7no3</sup>";
                                    break;
                                case 4: //major 7 no 3
                                    chord_name += "<sup>7no3</sup>";
                                    break;
                                default:
                                    chord_name += "";
                                    break;
                            }
                            break;
                        default:
                            chord_name = "";
                            break;
                    }
                    _leftText.text = chord_name;
                }
                else if (chord_qnty > 4)
                {
                    _leftText.text = "!!!";
                }

                if (chord_notes[3] - chord_notes[0] > 12 || chord_notes[2] - chord_notes[0] > 12)
                {
                    _leftText.text = ">8va (" + (chord_notes[3] - chord_notes[0]) + ")";
                }

                chord_qnty = 0;
            }
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}