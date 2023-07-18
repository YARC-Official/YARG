using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class FiveFretVisualNote : VisualNote<GuitarNote, FiveFretPlayer>
    {
        // TODO: Move this to player profiles
        public static readonly Color[] Colors =
        {
            Color.magenta,
            Color.green,
            Color.red,
            Color.yellow,
            Color.blue,
            new(1f, 0.5f, 0f),
        };

        [SerializeField]
        private NoteGroup _strumGroup;
        [SerializeField]
        private NoteGroup _hopoGroup;
        [SerializeField]
        private NoteGroup _tapGroup;

        protected override void InitializeNote()
        {
            transform.localPosition = new Vector3(
                BasePlayer.TRACK_WIDTH / 5f * NoteRef.Fret - BasePlayer.TRACK_WIDTH / 2f - 1f / 5f,
                0f, 0f);

            var noteGroup = NoteRef.Type switch
            {
                GuitarNoteType.Strum => _strumGroup,
                GuitarNoteType.Hopo  => _hopoGroup,
                GuitarNoteType.Tap   => _tapGroup,
                _                    => throw new Exception("Unreachable.")
            };

            noteGroup.SetActive(true);
            noteGroup.ColoredMaterial.color = Colors[NoteRef.Fret];
        }

        protected override void HideNote()
        {
            _strumGroup.SetActive(false);
            _hopoGroup.SetActive(false);
            _tapGroup.SetActive(false);
        }
    }
}