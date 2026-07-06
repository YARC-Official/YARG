using System;
using System.Collections.Generic;
using DG.Tweening;
using YARG.Core;

namespace YARG.Gameplay.HUD
{
    public abstract class BaseUnisonObject : GameplayBehaviour
    {
        protected bool[] ParticipantFailState;
        protected int[]  ParticipantTotalNotes;
        protected int[]  ParticipantNotesHit;
        protected int    ParticipantCount;

        protected float ParticipantProgress(int engineId) =>
            YargMath.InverseLerpF(0f, ParticipantTotalNotes[engineId], ParticipantNotesHit[engineId]);

        public void Initialize(int playerCount)
        {
            ParticipantFailState = new bool[playerCount];
            ParticipantTotalNotes = new int[playerCount];
            ParticipantNotesHit = new int[playerCount];
        }

        public virtual void ResetState()
        {
            Array.Clear(ParticipantFailState, 0, ParticipantCount);
            Array.Clear(ParticipantTotalNotes, 0, ParticipantCount);
            Array.Clear(ParticipantNotesHit, 0, ParticipantCount);
            ParticipantCount = 0;
        }

        public virtual void AddParticipant(int participantId, int totalNotes)
        {
            ParticipantTotalNotes[participantId] = totalNotes;
            ParticipantNotesHit[participantId] = 0;
            ParticipantFailState[participantId] = false;
            ParticipantCount++;
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