using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public sealed class SixFretGuitarNoteElement : NoteElement<GuitarNote, SixFretGuitarPlayer>
    {
        // Lane note type for 3-lane rendering
        public enum LaneNoteType
        {
            None,   // Open/Wildcard (full-width)
            Up,     // "Up" row note (black normal / white lefty)
            Down,   // "Down" row note (white normal / black lefty)
            Barre   // Both rows in same lane pair
        }

        // Theme model mapping count (Up/Down/Bar × Strum/HOPO/Tap + Open + OpenHopo + Wildcard)
        private const int THEME_MODEL_COUNT = 3 * 3 + 3;

        [Space]
        [SerializeField]
        private SustainLine _normalSustainLine;
        [SerializeField]
        private SustainLine _openSustainLine;
        [SerializeField]
        private SustainLine _wildcardSustainLine;

        private SustainLine _sustainLine;

        /// <summary>
        /// Lane note type determined at spawn time by SixFretGuitarPlayer.
        /// </summary>
        public LaneNoteType LaneType = LaneNoteType.None;

        protected override float RemovePointOffset => (float) NoteRef.TimeLength * Player.NoteSpeed;

        public override void SetThemeModels(
            Dictionary<ThemeNoteType, GameObject> models,
            Dictionary<ThemeNoteType, GameObject> starPowerModels)
        {
            CreateNoteGroupArrays(THEME_MODEL_COUNT);



            var offset = (int) ThemeNoteType.SixFretDown;
            // Map (LaneType, GuitarNoteType) to ThemeNoteType
            for (var i = ThemeNoteType.SixFretDown; i <= ThemeNoteType.SixFretBarreHOPO; ++i)
            {
                AssignNoteGroup(models, starPowerModels, (int) i - offset, i);
            }

            AssignNoteGroup(models, starPowerModels, THEME_MODEL_COUNT - 3, ThemeNoteType.Open);
            AssignNoteGroup(models, starPowerModels, THEME_MODEL_COUNT - 2, ThemeNoteType.OpenHOPO);
            AssignNoteGroup(models, starPowerModels, THEME_MODEL_COUNT - 1, ThemeNoteType.Wildcard);
        }

        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = IsStarPowerVisible ? StarPowerNoteGroups : NoteGroups;
            int modelIndex = -1;

            // Open/Wildcard: full-width, position at center
            if (NoteRef.Fret == (int) SixFretGuitarFret.Open)
            {
                transform.localPosition = Vector3.zero;

                modelIndex = NoteRef.Type switch
                {
                    GuitarNoteType.Strum => THEME_MODEL_COUNT - 3, // Open
                    GuitarNoteType.Hopo or GuitarNoteType.Tap => THEME_MODEL_COUNT - 2, // OpenHOPO
                    _ => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
                };

                _sustainLine = _openSustainLine;
            }
            else if (NoteRef.Fret == (int) SixFretGuitarFret.Wildcard)
            {
                transform.localPosition = Vector3.zero;
                modelIndex = THEME_MODEL_COUNT - 1; // Wildcard
                _sustainLine = _wildcardSustainLine;
            }
            else
            {
                // 3-lane note: position based on lane index (0-2)
                int laneIndex = Player.GetLaneIndex((SixFretGuitarFret) NoteRef.Fret);
                transform.localPosition = new Vector3(GetElementX(laneIndex, 3), 0f, 0f);

                // Map LaneType + NoteType to model index
                modelIndex = (int) (LaneType switch
                {
                    LaneNoteType.Up => NoteRef.Type switch
                    {
                        GuitarNoteType.Strum => ThemeNoteType.SixFretUp,
                        GuitarNoteType.Hopo => ThemeNoteType.SixFretUpHOPO,
                        GuitarNoteType.Tap => ThemeNoteType.SixFretUpTap,
                        _ => throw new ArgumentOutOfRangeException(String.Format("Invalid note type {0}",NoteRef.Type))
                    },
                    LaneNoteType.Down => NoteRef.Type switch
                    {
                        GuitarNoteType.Strum => ThemeNoteType.SixFretDown,
                        GuitarNoteType.Hopo => ThemeNoteType.SixFretDownHOPO,
                        GuitarNoteType.Tap => ThemeNoteType.SixFretDownTap,
                        _ => throw new ArgumentOutOfRangeException(String.Format("Invalid note type {0}",NoteRef.Type))
                    },
                    LaneNoteType.Barre => NoteRef.Type switch
                    {
                        GuitarNoteType.Strum => ThemeNoteType.SixFretBarre,
                        GuitarNoteType.Tap => ThemeNoteType.SixFretBarreTap,
                        GuitarNoteType.Hopo => ThemeNoteType.SixFretBarreHOPO,
                        _ => throw new ArgumentOutOfRangeException(String.Format("Invalid note type {0}",NoteRef.Type))
                    },
                    _ => throw new ArgumentOutOfRangeException()
                }) - (int) ThemeNoteType.SixFretDown;

                _sustainLine = _normalSustainLine;
            }

            NoteGroup = noteGroups[modelIndex];
            NoteGroup.SetActive(true);
            NoteGroup.Initialize();

            if (NoteRef.IsSustain)
            {
                _sustainLine.gameObject.SetActive(true);
                float len = (float) NoteRef.TimeLength * Player.NoteSpeed;
                _sustainLine.Initialize(len);
            }

            UpdateColor();

            if (LaneType == LaneNoteType.Barre && NoteRef.Fret >= (int)SixFretGuitarFret.White1)
            {
                // In a barre we're converting one of the elements into barre note and hiding a sibling
                HideElement();
            }
        }

        public override void HitNote()
        {
            base.HitNote();

            if (NoteRef.IsSustain)
            {
                HideNotes();
            }
            else
            {
                ParentPool.Return(this);
            }
        }


        public override void MissNote()
        {
            base.MissNote();

            UpdateColor();
        }

        protected override void UpdateElement()
        {
            base.UpdateElement();

            UpdateSustain();
        }

        protected override void OnNoteStateChanged()
        {
            base.OnNoteStateChanged();

            UpdateColor();
        }

        public override void OnStarPowerUpdated()
        {
            base.OnStarPowerUpdated();

            UpdateColor();
        }

        protected override bool CalcStarPowerVisible()
        {
            if (!NoteRef.IsStarPower)
            {
                return false;
            }
            return !(((GuitarEngineParameters) Player.BaseParameters).NoStarPowerOverlap && Player.BaseStats.IsStarPowerActive);
        }

        private void UpdateSustain()
        {
            _sustainLine.UpdateSustainLine();
        }

        private void UpdateColor()
        {
            var colors = Player.Player.ColorProfile.SixFretGuitar;
            bool isSp = IsStarPowerVisible;

            // Use System.Drawing.Color explicitly to avoid UnityEngine.Color ambiguity
            System.Drawing.Color primaryColor, primaryNoSp;
            System.Drawing.Color secondaryColor = default, secondaryNoSp = default;

            if (NoteRef.Fret == (int) SixFretGuitarFret.Open || NoteRef.Fret == (int) SixFretGuitarFret.Wildcard)
            {
                primaryColor = isSp ? colors.GetNoteStarPowerColor(NoteRef.Fret) : colors.GetNoteColor(NoteRef.Fret);
                primaryNoSp = colors.GetNoteColor(NoteRef.Fret);
            }
            else
            {
                // Determine primary/secondary based on LaneType
                var colorType = LaneType;
                if (colorType == LaneNoteType.Up && LeftyFlip)
                {
                    colorType = LaneNoteType.Down;
                }
                else if (colorType == LaneNoteType.Down && LeftyFlip)
                {
                    colorType = LaneNoteType.Up;
                }

                switch (colorType)
                {
                    case LaneNoteType.Up:
                        primaryColor = isSp ? colors.BlackNoteStarPower : colors.BlackNote;
                        primaryNoSp = colors.BlackNote;
                        secondaryColor = primaryColor;
                        secondaryNoSp = primaryNoSp;
                        break;
                    case LaneNoteType.Down:
                        primaryColor = isSp ? colors.WhiteNoteStarPower : colors.WhiteNote;
                        primaryNoSp = colors.WhiteNote;
                        secondaryColor = primaryColor;
                        secondaryNoSp = primaryNoSp;
                        break;
                    case LaneNoteType.Barre:
                        primaryColor = isSp ? colors.BlackNoteStarPower : colors.BlackNote;
                        primaryNoSp = colors.BlackNote;
                        secondaryColor = isSp ? colors.WhiteNoteStarPower : colors.WhiteNote;
                        secondaryNoSp = colors.WhiteNote;
                        if (LeftyFlip)
                        {
                            (primaryColor, secondaryColor) = (secondaryColor, primaryColor);
                            (primaryNoSp, secondaryNoSp) = (secondaryNoSp, primaryNoSp);
                        }
                        break;
                    default:
                        return;
                }
            }

            if (NoteRef.WasMissed)
            {
                primaryColor = colors.Miss;
                primaryNoSp = colors.Miss;
            }

            if (!NoteRef.WasHit)
            {
                NoteGroup.SetColorWithEmission(primaryColor.ToUnityColor(), primaryNoSp.ToUnityColor());

                // Apply secondary color
                NoteGroup.SetSecondaryColor(secondaryColor.ToUnityColor(), secondaryNoSp.ToUnityColor());

                NoteGroup.SetMetalColor(colors.GetMetalColor(isSp).ToUnityColor());
            }

            if (!NoteRef.IsSustain) return;

            _sustainLine.SetState(SustainState, primaryColor.ToUnityColor());
            _sustainLine.SetSecondaryColor(secondaryColor.ToUnityColor());
        }

        protected override void HideElement()
        {
            HideNotes();

            _normalSustainLine.gameObject.SetActive(false);
            _openSustainLine.gameObject.SetActive(false);
            _wildcardSustainLine.gameObject.SetActive(false);
        }
    }
}
