using System;
using Mirror;
using UnityEngine;
using YARG.Networking;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Handles local-authority gameplay snapshot replication for multiplayer sessions.
    /// </summary>
    public class MultiplayerGameplaySync : MonoBehaviour
    {
        private const double MIN_CHANGED_SNAPSHOT_INTERVAL = 0.09d;
        private const double MAX_UNCHANGED_SNAPSHOT_INTERVAL = 0.45d;
        private const float STAR_POWER_DELTA_EPSILON = 0.0025f;

        private readonly struct GameplaySnapshot
        {
            public readonly int Score;
            public readonly int Combo;
            public readonly int Streak;
            public readonly bool StarPowerActive;
            public readonly float StarPowerAmount;
            public readonly int StarPowerPhrasesHit;
            public readonly int TotalStarPowerPhrases;
            public readonly int NotesHit;
            public readonly int NotesMissed;
            public readonly int Overstrums;
            public readonly int HoposStrummed;
            public readonly int Overhits;
            public readonly int GhostInputs;
            public readonly int GhostsHit;
            public readonly int AccentsHit;
            public readonly int DynamicsBonus;
            public readonly int BandBonusScore;
            public readonly bool SoloActive;
            public readonly int SoloSequence;
            public readonly int SoloNoteCount;
            public readonly int SoloNotesHit;
            public readonly int SoloLastBonus;
            public readonly int SoloTotalBonus;
            public readonly double SongTime;
            public readonly double ClientNetworkTime;

            public GameplaySnapshot(int score, int combo, int streak, bool starPowerActive, float starPowerAmount,
                int starPowerPhrasesHit, int totalStarPowerPhrases, int notesHit, int notesMissed, int overstrums,
                int hoposStrummed, int overhits, int ghostInputs, int ghostsHit, int accentsHit, int dynamicsBonus,
                int bandBonusScore, bool soloActive, int soloSequence, int soloNoteCount, int soloNotesHit,
                int soloLastBonus, int soloTotalBonus, double songTime, double clientNetworkTime)
            {
                Score = score;
                Combo = combo;
                Streak = streak;
                StarPowerActive = starPowerActive;
                StarPowerAmount = starPowerAmount;
                StarPowerPhrasesHit = starPowerPhrasesHit;
                TotalStarPowerPhrases = totalStarPowerPhrases;
                NotesHit = notesHit;
                NotesMissed = notesMissed;
                Overstrums = overstrums;
                HoposStrummed = hoposStrummed;
                Overhits = overhits;
                GhostInputs = ghostInputs;
                GhostsHit = ghostsHit;
                AccentsHit = accentsHit;
                DynamicsBonus = dynamicsBonus;
                BandBonusScore = bandBonusScore;
                SoloActive = soloActive;
                SoloSequence = soloSequence;
                SoloNoteCount = soloNoteCount;
                SoloNotesHit = soloNotesHit;
                SoloLastBonus = soloLastBonus;
                SoloTotalBonus = soloTotalBonus;
                SongTime = songTime;
                ClientNetworkTime = clientNetworkTime;
            }

            public bool DiffersFrom(in GameplaySnapshot other)
            {
                if (Score != other.Score || Combo != other.Combo || Streak != other.Streak)
                {
                    return true;
                }

                if (StarPowerActive != other.StarPowerActive)
                {
                    return true;
                }

                if (Mathf.Abs(StarPowerAmount - other.StarPowerAmount) > STAR_POWER_DELTA_EPSILON)
                {
                    return true;
                }

                if (StarPowerPhrasesHit != other.StarPowerPhrasesHit ||
                    TotalStarPowerPhrases != other.TotalStarPowerPhrases)
                {
                    return true;
                }

                if (NotesHit != other.NotesHit || NotesMissed != other.NotesMissed)
                {
                    return true;
                }

                if (Overstrums != other.Overstrums || HoposStrummed != other.HoposStrummed)
                {
                    return true;
                }

                if (Overhits != other.Overhits)
                {
                    return true;
                }

                if (GhostInputs != other.GhostInputs || GhostsHit != other.GhostsHit)
                {
                    return true;
                }

                if (AccentsHit != other.AccentsHit || DynamicsBonus != other.DynamicsBonus)
                {
                    return true;
                }

                if (BandBonusScore != other.BandBonusScore)
                {
                    return true;
                }

                if (SoloActive != other.SoloActive || SoloSequence != other.SoloSequence)
                {
                    return true;
                }

                if (SoloNoteCount != other.SoloNoteCount || SoloNotesHit != other.SoloNotesHit)
                {
                    return true;
                }

                if (SoloLastBonus != other.SoloLastBonus || SoloTotalBonus != other.SoloTotalBonus)
                {
                    return true;
                }

                return false;
            }
        }

        private bool _isMultiplayer;
        private NetworkPlayerData _localPlayerData;
        private bool _hasLastSnapshot;
        private GameplaySnapshot _lastSnapshot;
        private uint _snapshotSequence;

        private void Start()
        {
            // Check if we're in multiplayer mode
            if (YargNetworkManager.Instance == null || !YargNetworkManager.Instance.isNetworkActive)
            {
                _isMultiplayer = false;
                Destroy(this);
                return;
            }

            _isMultiplayer = true;

            // Get local player's NetworkPlayerData
            var allPlayers = YargNetworkManager.Instance.GetAllPlayers();
            foreach (var playerData in allPlayers)
            {
                if (playerData != null && playerData.isLocalPlayer)
                {
                    _localPlayerData = playerData;
                    break;
                }
            }

            if (_localPlayerData == null)
            {
                Debug.LogWarning("[MultiplayerGameplaySync] Could not find local NetworkPlayerData");
                Destroy(this);
                return;
            }

            // Reset game state at start
            _localPlayerData.CmdResetGameState();
            ResetSnapshotCache();

            Debug.Log("[MultiplayerGameplaySync] Initialized - using local authority gameplay snapshots");
        }

        private void ResetSnapshotCache()
        {
            _hasLastSnapshot = false;
            _lastSnapshot = default;
            _snapshotSequence = 0;
        }

        /// <summary>
        /// Submit the current local gameplay state for replication to other clients.
        /// The caller is responsible for providing song/network timestamps in the same frame.
        /// </summary>
        public void SubmitLocalSnapshot(int score, int combo, int streak, bool starPowerActive, float starPowerAmount,
            int starPowerPhrasesHit, int totalStarPowerPhrases, int notesHit, int notesMissed, int overstrums,
            int hoposStrummed, int overhits, int ghostInputs, int ghostsHit, int accentsHit, int dynamicsBonus,
            int bandBonusScore, bool soloActive, int soloSequence, int soloNoteCount, int soloNotesHit,
            int soloLastBonus, int soloTotalBonus, double songTime, double clientNetworkTime, bool forceSend = false)
        {
            if (!_isMultiplayer || _localPlayerData == null || !_localPlayerData.isLocalPlayer)
            {
                return;
            }

            int sanitizedScore = Math.Max(0, score);
            int sanitizedCombo = Math.Max(0, combo);
            int sanitizedStreak = Math.Max(0, streak);
            int sanitizedNotesHit = Math.Max(0, notesHit);
            int sanitizedNotesMissed = Math.Max(0, notesMissed);
            int sanitizedOverstrums = Math.Max(0, overstrums);
            int sanitizedHoposStrummed = Math.Max(0, hoposStrummed);
            int sanitizedOverhits = Math.Max(0, overhits);
            int sanitizedGhostInputs = Math.Max(0, ghostInputs);
            int sanitizedGhostsHit = Math.Max(0, ghostsHit);
            int sanitizedAccentsHit = Math.Max(0, accentsHit);
            int sanitizedDynamicsBonus = Math.Max(0, dynamicsBonus);
            int sanitizedBandBonusScore = Math.Max(0, bandBonusScore);
            int sanitizedTotalStarPowerPhrases = Math.Max(0, totalStarPowerPhrases);
            int sanitizedStarPowerPhrasesHit = Math.Max(0, starPowerPhrasesHit);
            if (sanitizedTotalStarPowerPhrases > 0)
            {
                sanitizedStarPowerPhrasesHit = Math.Min(sanitizedStarPowerPhrasesHit, sanitizedTotalStarPowerPhrases);
            }
            int sanitizedSoloSequence = soloSequence;
            int sanitizedSoloNoteCount = Math.Max(0, soloNoteCount);
            int sanitizedSoloNotesHit = Mathf.Clamp(soloNotesHit, 0, sanitizedSoloNoteCount == 0 ? 0 : sanitizedSoloNoteCount);
            int sanitizedSoloLastBonus = Math.Max(0, soloLastBonus);
            int sanitizedSoloTotalBonus = Math.Max(0, soloTotalBonus);

            // Ensure values stay monotonic so the server doesn't reject snapshots due to engine bookkeeping churn.
            if (_hasLastSnapshot)
            {
                sanitizedScore = Math.Max(sanitizedScore, _lastSnapshot.Score);
                sanitizedStreak = Math.Max(sanitizedStreak, _lastSnapshot.Streak);
                sanitizedNotesHit = Math.Max(sanitizedNotesHit, _lastSnapshot.NotesHit);
                sanitizedNotesMissed = Math.Max(sanitizedNotesMissed, _lastSnapshot.NotesMissed);
                sanitizedOverstrums = Math.Max(sanitizedOverstrums, _lastSnapshot.Overstrums);
                sanitizedHoposStrummed = Math.Max(sanitizedHoposStrummed, _lastSnapshot.HoposStrummed);
                sanitizedOverhits = Math.Max(sanitizedOverhits, _lastSnapshot.Overhits);
                sanitizedGhostInputs = Math.Max(sanitizedGhostInputs, _lastSnapshot.GhostInputs);
                sanitizedGhostsHit = Math.Max(sanitizedGhostsHit, _lastSnapshot.GhostsHit);
                sanitizedAccentsHit = Math.Max(sanitizedAccentsHit, _lastSnapshot.AccentsHit);
                sanitizedDynamicsBonus = Math.Max(sanitizedDynamicsBonus, _lastSnapshot.DynamicsBonus);
                sanitizedBandBonusScore = Math.Max(sanitizedBandBonusScore, _lastSnapshot.BandBonusScore);
                sanitizedStarPowerPhrasesHit = Math.Max(sanitizedStarPowerPhrasesHit, _lastSnapshot.StarPowerPhrasesHit);
                sanitizedTotalStarPowerPhrases = Math.Max(sanitizedTotalStarPowerPhrases, _lastSnapshot.TotalStarPowerPhrases);
                sanitizedSoloTotalBonus = Math.Max(sanitizedSoloTotalBonus, _lastSnapshot.SoloTotalBonus);

                if (sanitizedSoloSequence == _lastSnapshot.SoloSequence)
                {
                    sanitizedSoloNotesHit = Math.Max(sanitizedSoloNotesHit, _lastSnapshot.SoloNotesHit);
                    sanitizedSoloNoteCount = Math.Max(sanitizedSoloNoteCount, _lastSnapshot.SoloNoteCount);
                }
            }

            float clampedStarPower = Mathf.Clamp01(starPowerAmount);

            var snapshot = new GameplaySnapshot(sanitizedScore, sanitizedCombo, sanitizedStreak, starPowerActive,
                clampedStarPower, sanitizedStarPowerPhrasesHit, sanitizedTotalStarPowerPhrases, sanitizedNotesHit,
                sanitizedNotesMissed, sanitizedOverstrums, sanitizedHoposStrummed, sanitizedOverhits,
                sanitizedGhostInputs, sanitizedGhostsHit, sanitizedAccentsHit, sanitizedDynamicsBonus,
                sanitizedBandBonusScore, soloActive, sanitizedSoloSequence, sanitizedSoloNoteCount,
                sanitizedSoloNotesHit, sanitizedSoloLastBonus, sanitizedSoloTotalBonus, songTime,
                clientNetworkTime);

            bool shouldSend = forceSend || !_hasLastSnapshot;

            if (!shouldSend && _hasLastSnapshot)
            {
                double elapsed = snapshot.ClientNetworkTime - _lastSnapshot.ClientNetworkTime;
                bool changed = snapshot.DiffersFrom(_lastSnapshot);

                if (changed)
                {
                    shouldSend = elapsed >= MIN_CHANGED_SNAPSHOT_INTERVAL;
                }
                else
                {
                    shouldSend = elapsed >= MAX_UNCHANGED_SNAPSHOT_INTERVAL;
                }
            }

            if (!shouldSend)
            {
                return;
            }

            _snapshotSequence++;
            _localPlayerData.CmdSubmitGameplaySnapshot(snapshot.Score, snapshot.Combo, snapshot.Streak,
                snapshot.StarPowerActive, snapshot.StarPowerAmount, snapshot.StarPowerPhrasesHit,
                snapshot.TotalStarPowerPhrases, snapshot.NotesHit, snapshot.NotesMissed, snapshot.Overstrums,
                snapshot.HoposStrummed, snapshot.Overhits, snapshot.GhostInputs, snapshot.GhostsHit,
                snapshot.AccentsHit, snapshot.DynamicsBonus, snapshot.BandBonusScore, snapshot.SoloActive,
                snapshot.SoloSequence, snapshot.SoloNoteCount, snapshot.SoloNotesHit, snapshot.SoloLastBonus,
                snapshot.SoloTotalBonus, snapshot.SongTime, snapshot.ClientNetworkTime, _snapshotSequence);

            _lastSnapshot = snapshot;
            _hasLastSnapshot = true;
        }
        
        private void OnDestroy()
        {
            ResetSnapshotCache();
        }
    }
}
