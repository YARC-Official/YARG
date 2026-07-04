using System.Collections.Generic;
using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class UnisonIconGroup : BaseUnisonObject
    {
        [SerializeField]
        private UnisonIcon _instrumentIconPrefab;
        private readonly Dictionary<int, UnisonIcon> _icons = new();

        public void InitializeIcon(int engineId, string spritePath)
        {
            var newIcon = Instantiate(_instrumentIconPrefab, transform);
            newIcon.SetIcon(spritePath);
            _icons[engineId] = newIcon;
            newIcon.gameObject.SetActive(false);
        }

        public override void SetParticipants(List<int> participants)
        {
            base.SetParticipants(participants);
            foreach ((int id, var icon) in _icons)
            {
                if (!participants.Contains(id))
                {
                    continue;
                }
                icon.gameObject.SetActive(true);
            }
        }

        public override void SetProgress(int engineId, float progress)
        {
            if (!ParticipantFailState[engineId])
            {
                _icons[engineId].SetProgress(progress);
            }
        }

        public override void FailUnison(int engineId)
        {
            base.FailUnison(engineId);
            _icons[engineId].SetFailState(true);
        }

        public override void ResetState()
        {
            base.ResetState();
            foreach ((int _, var icon) in _icons)
            {
                icon.ResetState();
            }
        }
    }
}