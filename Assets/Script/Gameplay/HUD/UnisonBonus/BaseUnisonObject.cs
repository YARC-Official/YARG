using System.Collections.Generic;
using YARG.Core;

namespace YARG.Gameplay.HUD
{
    public abstract class BaseUnisonObject : GameplayBehaviour
    {
        protected Dictionary<int, bool> ParticipantFailState  = new();
        protected Dictionary<int, int>  ParticipantTotalNotes = new();
        protected Dictionary<int, int>  ParticipantNotesHit   = new();

        protected float ParticipantProgress(int engineId) =>
            YargMath.InverseLerpF(0f, ParticipantTotalNotes[engineId], ParticipantNotesHit[engineId]);

        public virtual void ResetState()
        {
            ParticipantTotalNotes.Clear();
            ParticipantNotesHit.Clear();
            ParticipantFailState.Clear();
        }

        public virtual void AddParticipant(int participantId, int totalNotes)
        {
            ParticipantTotalNotes[participantId] = totalNotes;
            ParticipantNotesHit[participantId] = 0;
            ParticipantFailState[participantId] = false;
        }

        public virtual void SetNotesHit(int engineId, int notesHit)
        {
            if (!ParticipantFailState[engineId])
            {
                ParticipantNotesHit[engineId] = notesHit;
            }
        }

        public virtual void FailUnison(int engineId)
        {
            ParticipantFailState[engineId] = true;
        }
    }
}