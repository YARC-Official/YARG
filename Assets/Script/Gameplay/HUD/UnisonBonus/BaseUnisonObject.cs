using System.Collections.Generic;

namespace YARG.Gameplay.HUD
{
    public abstract class BaseUnisonObject : GameplayBehaviour
    {
        protected Dictionary<int, bool> ParticipantFailState = new();

        protected Dictionary<int, float> ParticipantProgress = new();

        public virtual void ResetState()
        {
            ParticipantProgress.Clear();
            ParticipantFailState.Clear();
        }

        public virtual void SetParticipants(List<int> participants)
        {
            foreach (int participant in participants)
            {
                ParticipantProgress[participant] = 0f;
                ParticipantFailState[participant] = false;
            }
        }

        public virtual void SetProgress(int engineId, float progress)
        {
            if (!ParticipantFailState[engineId])
            {
                ParticipantProgress[engineId] = progress;
            }
        }

        public virtual void FailUnison(int engineId)
        {
            ParticipantFailState[engineId] = true;
        }
    }
}