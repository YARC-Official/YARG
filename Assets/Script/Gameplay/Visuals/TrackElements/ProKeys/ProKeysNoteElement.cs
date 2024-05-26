using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.ProKeys;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public sealed class ProKeysNoteElement : NoteElement<ProKeysNote, ProKeysPlayer>
    {
        [Space]
        [SerializeField]
        private SustainLine _sustainLine;

        // Make sure the remove it later if it has a sustain
        protected override float RemovePointOffset => (float) NoteRef.TimeLength * Player.NoteSpeed;

        public override void SetThemeModels(
            Dictionary<ThemeNoteType, GameObject> models,
            Dictionary<ThemeNoteType, GameObject> starPowerModels)
        {
            CreateNoteGroupArrays(1);
            AssignNoteGroup(models, starPowerModels, 0, ThemeNoteType.Normal);
        }

        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = NoteRef.IsStarPower ? StarPowerNoteGroups : NoteGroups;

            // Set the position
            transform.localPosition = Vector3.zero;
            UpdateNoteX();

            NoteGroup = noteGroups[0];
            NoteGroup.SetActive(true);
            NoteGroup.Initialize();

            // Set line length
            if (NoteRef.IsSustain)
            {
                _sustainLine.gameObject.SetActive(true);

                float len = (float) NoteRef.TimeLength * Player.NoteSpeed;
                _sustainLine.Initialize(len);
            }

            // Set note and sustain color
            UpdateColor();
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

        public void UpdateNoteX()
        {
            var t = transform;
            t.localPosition = t.localPosition
                .WithX(Player.GetNoteX(NoteRef.Key));
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

        private void UpdateSustain()
        {
            _sustainLine.UpdateSustainLine(Player.NoteSpeed * GameManager.SongSpeed);
        }

        private void UpdateColor()
        {
            var colors = Player.Player.ColorProfile.ProKeys;

            var isBlackKey = ProKeysUtilities.IsBlackKey(NoteRef.Key % 12);

            var colorNoStarPower = isBlackKey
                ? colors.BlackNote.ToUnityColor()
                : colors.WhiteNote.ToUnityColor();

            var colorStarPower = isBlackKey
                ? colors.BlackNoteStarPower.ToUnityColor()
                : colors.WhiteNoteStarPower.ToUnityColor();

            var color = NoteRef.IsStarPower
                ? colorStarPower
                : colorNoStarPower;

            NoteGroup.SetColorWithEmission(color, colorNoStarPower);

            if (!NoteRef.IsSustain) return;

            _sustainLine.SetState(SustainState, colorNoStarPower);
        }

        protected override void HideElement()
        {
            HideNotes();

            _sustainLine.gameObject.SetActive(false);
        }
    }
}