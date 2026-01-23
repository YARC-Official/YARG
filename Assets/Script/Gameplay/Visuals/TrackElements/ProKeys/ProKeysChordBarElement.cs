using System;
using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Core.Chart;
using YARG.Settings;
using TMPro;
using Cysharp.Text;

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

        private string[] _chordCall = {};

        private int   _chordQnty  = 0;
        private int[] _chordNotes = new int[25];

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
            using var chordName = new Utf16ValueStringBuilder(true);
            _leftText.text = "";
            _leftText.textStyle = TMP_Style.NormalStyle;

            foreach (var note in NoteRef.AllNotes)
            {
                for (int i = 0; i < 4; i++)
                {
                    _chordNotes[i] = 0;
                }
                Array.Resize(ref _chordCall, _chordCall.Length + 1);

                foreach (var child in note.AllNotes)
                {
                    if (note.IsChord)
                    {
                        //Get the number of notes in the chord and equate to a int value in the array
                        _chordQnty += 1;
                        _chordNotes[_chordQnty - 1] = child.Key;
                    }
                }
                if (_chordQnty <= 2)
                {
                }
                else
                {
                    Array.Sort(_chordNotes, 0, _chordQnty);
                }

                //Using the lowest note, find the first and second interval for three note chords
                chordName.Append(GetRoot(0));
                if (_chordQnty == 3)
                {
                    switch (_chordNotes[1] - _chordNotes[0])
                    {
                        case 2: //major 2nd
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 5: //diminished
                                    chordName.Append("<sup>sus2</sup>");
                                    break;
                                default: //minor
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        case 3: //minor 3rd
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 3: //diminished
                                    chordName.Append("<sup>dim</sup>");
                                    break;
                                case 4: //minor
                                    chordName.Append('m');
                                    break;
                                case 5: //major, 1st inversion
                                    chordName.Clear();
                                    chordName.Append(GetRoot(2));
                                    break;
                                case 6: //diminished, 1st inversion
                                    chordName.Clear();
                                    chordName.Append(GetRoot(2) + "dim");
                                    break;
                                case 7: //minor 7 no 5
                                    chordName.Append("m<sup>7no5</sup>");
                                    break;
                                case 8: //minor 7 no 5
                                    chordName.Append("m<sup>M7no5</sup>");
                                    break;
                                default:
                                    //chord_name = "an unlisted minor 3rd interval chord.";
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        case 4: //major 3rd
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 2:
                                    chordName.Append("<sup>b5</sup>");
                                    break;
                                case 3: //major
                                    break;
                                case 4: //augmented
                                    chordName.Append("<sup>aug</sup>");
                                    break;
                                case 5: //minor, 1st inversion
                                    chordName.Clear();
                                    chordName.Append(GetRoot(2) + "m");
                                    break;
                                case 6:
                                    chordName.Append("<sup>7no5</sup>");
                                    break;
                                case 7:
                                    chordName.Append("M<sup>7no5</sup>");
                                    break;
                                case 8:
                                    chordName.Append("<sup>no5</sup>");
                                    break;
                                default:
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        case 5: //perfect fourth
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 2: //minor, 2nd inversion
                                    chordName.Append("<sup>sus4</sup>");
                                    break;
                                case 3: //minor, 2nd inversion
                                    chordName.Clear();
                                    chordName.Append(GetRoot(1) + "m");
                                    break;
                                case 4: //major, 2nd inversion
                                    chordName.Clear();
                                    chordName.Append(GetRoot(1));
                                    break;
                                case 5: //sus2 inversion
                                    chordName.Clear();
                                    chordName.Append(GetRoot(2) + "sus2");
                                    break;
                                case 7: //POWER!! 5
                                    chordName.Clear();
                                    chordName.Append(GetRoot(1) + "5/" + GetRoot(0));
                                    break;
                                default:
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        case 6: //diminished
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 3:
                                    chordName.Append("dim7");
                                    break;
                                case 4:
                                    chordName.Append("m7b5");
                                    break;
                                default:
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        case 7: //perfect fifth
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 3: //minor 7th with no 3
                                    chordName.Append("m<sup>7no3</sup>");
                                    break;
                                case 4: //major 7 no 3
                                    chordName.Append("<sup>7no3</sup>");
                                    break;
                                case 5: //POWER!! 5
                                    chordName.Append('5');
                                    break;
                                default:
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        default:
                            chordName.Clear();
                            break;
                    }
                    _leftText.SetText(chordName);
                }
                else if (_chordQnty == 4)
                {
                    switch (_chordNotes[1] - _chordNotes[0])
                    {
                        case 2: //major 2nd
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 2: //C D E
                                    switch (_chordNotes[3] - _chordNotes[2])
                                    {
                                        case 3:
                                            chordName.Append("<sup>add9</sup>");
                                            break;
                                        case 4:
                                            chordName.Append("<sup>add9#5</sup>");
                                            break;
                                        case 5:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(2) + "m<sup>sus4<sup>");
                                            break;
                                        case 6:
                                            chordName.Append("<sup>7add9no5</sup>");
                                            break;
                                        case 7:
                                            chordName.Append("M<sup>7add9no5</sup>");
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chordName.Clear();
                                            break;
                                    }
                                    break;
                                case 3: //C D F
                                    switch (_chordNotes[3] - _chordNotes[2])
                                    {
                                        case 3:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(1) + "m7b5/" + GetRoot(0));
                                            break;
                                        case 4:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(1) + "m7/" + GetRoot(0));
                                            break;
                                        case 5:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(3) + "m<sup>sus4</sup>/" + GetRoot(0));
                                            break;
                                        default:
                                            chordName.Clear();
                                            break;
                                    }
                                    break;
                                case 4: //C D F#
                                    switch (_chordNotes[3] - _chordNotes[2])
                                    {
                                        case 2:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(1) + "<sup>7b5</sup>/" + GetRoot(0));
                                            break;
                                        case 3:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(1) + "<sup>7</sup>/" + GetRoot(0));
                                            break;
                                        case 4:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(1) + "7#5/" + GetRoot(0));
                                            break;
                                        case 5:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(3) + "m<sup>b9</sup>/" + GetRoot(0));
                                            break;
                                        default:
                                            chordName.Clear();
                                            break;
                                    }
                                    break;
                                case 5: //C D G
                                    switch (_chordNotes[3] - _chordNotes[2])
                                    {
                                        case 3:
                                            chordName.Append("7<sup>sus2</sup>");
                                            break;
                                        case 4:
                                            chordName.Append("M7<sup>sus2</sup>");
                                            break;
                                        case 5:
                                            chordName.Append("<sup>sus2</sup>");
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chordName.Clear();
                                            break;
                                    }
                                    break;
                                case 7: //C D A
                                    chordName.Clear();
                                    break;
                                default:
                                    //chord_name = "an unlisted minor 2nd interval chord.";
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        case 3: //minor 3rd
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 2: //C D# F
                                    switch (_chordNotes[3] - _chordNotes[2])
                                    {
                                        case 2:
                                            chordName.Append("m4");
                                            break;
                                        case 3:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(3) + "<sup>6</sup>/" + GetRoot(0));
                                            break;
                                        case 4:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(2) + "<sup>7</sup>/" + GetRoot(0));
                                            break;
                                        default:
                                            chordName.Clear();
                                            break;
                                    }
                                    break;
                                case 3: //C D# F#
                                    switch (_chordNotes[3] - _chordNotes[2])
                                    {
                                        case 2:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(3) + "<sup>7</sup>/" + GetRoot(0));
                                            break;
                                        case 3:
                                            chordName.Append("dim7");
                                            break;
                                        case 4:
                                            chordName.Append("m7b5");
                                            break;
                                        case 5:
                                            chordName.Append("mM7b5");
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chordName.Clear();
                                            break;
                                    }
                                    break;
                                case 4: //minor
                                    switch (_chordNotes[3] - _chordNotes[2])
                                    {
                                        case 1:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(3) + "M<sup>7</sup>/" + GetRoot(0));
                                            break;
                                        case 2:
                                            chordName.Append("m<sup>6</sup>");
                                            break;
                                        case 3:
                                            chordName.Append("m<sup>7</sup>");
                                            break;
                                        case 4:
                                            chordName.Append("mM<sup>7</sup>");
                                            break;
                                        case 5:
                                            chordName.Append("m");
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chordName.Clear();
                                            break;
                                    }
                                    break;
                                case 5: //C D# G#
                                    switch (_chordNotes[3] - _chordNotes[2])
                                    {
                                        case 1:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(2) + "<sup>addb9</sup>/" + GetRoot(0));
                                            break;
                                        case 2:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(2) + "<sup>add9</sup>/" + GetRoot(0));
                                            break;
                                        case 3:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(2) + "<sup>add#2</sup>/" + GetRoot(0));
                                            break;
                                        case 4:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(2));
                                            break;
                                        case 5:
                                            chordName.Append('m');
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chordName.Clear();
                                            break;
                                    }
                                    break;
                                case 7: //minor 7 no 5
                                    chordName.Append("<sup>m7no5</sup>");
                                    break;
                                default:
                                    //chord_name = "an unlisted minor 3rd interval chord.";
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        case 4: //major 3rd
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 1: //C E F
                                    switch (_chordNotes[3] - _chordNotes[2])
                                    {
                                        case 2:
                                            chordName.Append('4');
                                            break;
                                        case 4:
                                            chordName.Clear();
                                            chordName.Append(GetRoot(2) + "M<sup>7</sup>/" + GetRoot(0));
                                            break;
                                        case 6:
                                            chordName.Append("<sup>b9no7</sup>");
                                            break;
                                        case 7:
                                            chordName.Append("M<sup>7add9no5</sup>");
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chordName.Clear();
                                            break;
                                    }
                                    break;
                                case 3: //C E G
                                    switch (_chordNotes[3] - _chordNotes[2])
                                    {
                                        case 2:
                                            chordName.Append("<sup>6</sup>");
                                            break;
                                        case 3:
                                            chordName.Append("<sup>7</sup>");
                                            break;
                                        case 4:
                                            chordName.Append("M<sup>7</sup>");
                                            break;
                                        case 5:
                                            break;
                                        case 6:
                                            chordName.Append("<sup>b9no7</sup>");
                                            break;
                                        case 7:
                                            chordName.Append("<sup>9no7</sup>");
                                            break;
                                        default:
                                            //chord_name = "an unlisted minor 2nd interval chord.";
                                            chordName.Clear();
                                            break;
                                    }
                                    break;
                                case 4: //augmented
                                    chordName.Append("<sup>aug</sup>");
                                    break;
                                case 5: //major, 1st inversion
                                    chordName.Clear();
                                    chordName.Append(GetRoot(2) + "m");
                                    break;
                                default:
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        case 5: //perfect fourth
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 2: //sus4
                                    chordName.Append("<sup>sus4</sup>");
                                    break;
                                case 3: //minor, 2nd inversion
                                    chordName.Clear();
                                    chordName.Append(GetRoot(1) + "m");
                                    break;
                                case 4: //major, 2nd inversion
                                    chordName.Clear();
                                    chordName.Append(GetRoot(1));
                                    break;
                                case 5: //sus2 inversion
                                    chordName.Clear();
                                    chordName.Append(GetRoot(2) + "<sup>sus2</sup>");
                                    break;
                                default:
                                    //chord_name = "an unlisted minor perfect 4th chord.";
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        case 6: //diminished
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 3: //minor 7th with no 3
                                    chordName.Append("<sup>m7no3</sup>");
                                    break;
                                case 4: //minor 7 flat 5
                                    chordName.Append("m<sup>7no3</sup>");
                                    break;
                                default:
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        case 7: //perfect fifth
                            switch (_chordNotes[2] - _chordNotes[1])
                            {
                                case 3: //minor 7th with no 3
                                    chordName.Append("<sup>m7no3</sup>");
                                    break;
                                case 4: //major 7 no 3
                                    chordName.Append("<sup>7no3</sup>");
                                    break;
                                default:
                                    chordName.Clear();
                                    break;
                            }
                            break;
                        default:
                            chordName.Clear();
                            break;
                    }
                    _leftText.SetText(chordName);
                }
                else if (_chordQnty > 4)
                {
                    _leftText.text = "!!!";
                }

                if (_chordNotes[3] - _chordNotes[0] > 12 || _chordNotes[2] - _chordNotes[0] > 12)
                {
                    _leftText.text = ">8va (" + (_chordNotes[3] - _chordNotes[0]) + ")";
                }

                _chordQnty = 0;
            }

            return;

            //Use the first note of the array to find the root
            string GetRoot(int noteSlot) //noteSlot being the note's position
            {
                return (_chordNotes[noteSlot] % 12) switch
                {
                    0  => "C",
                    1  => "C♯",
                    2  => "D",
                    3  => "Eb",
                    4  => "E",
                    5  => "F",
                    6  => "F♯",
                    7  => "G",
                    8  => "G♯",
                    9  => "A",
                    10 => "Bb",
                    11 => "B",

                    _ => "X"
                };
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