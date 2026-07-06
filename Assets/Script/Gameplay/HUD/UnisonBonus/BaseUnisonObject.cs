using System;
using System.Collections.Generic;
using DG.Tweening;
using YARG.Core;

namespace YARG.Gameplay.HUD
{
    public abstract class BaseUnisonObject : GameplayBehaviour
    {
        protected List<bool> ParticipantFailState = new();
        protected List<int> ParticipantTotalNotes = new();
        protected List<int> ParticipantNotesHit  = new();

        protected float ParticipantProgress(int engineId) =>
            YargMath.InverseLerpF(0f, ParticipantTotalNotes[engineId], ParticipantNotesHit[engineId]);

        public virtual void ResetState()
        {
            ParticipantFailState.Clear();
            ParticipantTotalNotes.Clear();
            ParticipantNotesHit.Clear();
        }

        public virtual void AddParticipant(int participantId, int totalNotes)
        {
            ParticipantTotalNotes.Insert(participantId, totalNotes);
            ParticipantNotesHit.Insert(participantId, 0);
            ParticipantFailState.Insert(participantId, false);
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