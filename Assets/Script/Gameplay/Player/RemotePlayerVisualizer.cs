using System;
using UnityEngine;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Networking;

namespace YARG.Gameplay.Player
{
    /// <summary>
    /// Drives a simulated view of a remote player using aggregated gameplay snapshots
    /// instead of per-input replication. The simulator keeps the remote track, combo,
    /// and star power indicators aligned with the authoritative stats broadcast by the
    /// owning client.
    /// </summary>
    public class RemotePlayerSimulation : MonoBehaviour
    {
        private TrackPlayer _trackPlayer;
        private NetworkPlayerData _networkPlayerData;
        private BaseStats _stats = null!;

        private int _noteCursor;
        private int _resolvedHits;
        private int _resolvedMisses;
        private uint _lastSnapshotSequence;

        private bool _lastStarPowerActive;

        private SoloSection? _remoteSoloSection;
        private bool _hasRemoteSoloSection;
        private int _lastSoloSequence = -1;
        private bool _wasSoloActive;

        private bool _isInitialized;

        public void Initialize(TrackPlayer trackPlayer, NetworkPlayerData networkPlayerData)
        {
            _trackPlayer = trackPlayer;
            _networkPlayerData = networkPlayerData;

            if (_networkPlayerData == null || _trackPlayer == null || _networkPlayerData.isLocalPlayer)
            {
                enabled = false;
                return;
            }

            _trackPlayer.RegisterRemoteSimulation(this);
            _stats = _trackPlayer.BaseStats;

            ResetSimulation();
            _isInitialized = true;
            ApplyRemoteState(0d);
        }

        internal void ApplyRemoteState(double localInputTime)
        {
            _ = localInputTime;

            if (!_isInitialized || _networkPlayerData == null || _trackPlayer == null)
            {
                return;
            }

            bool snapshotReset = _networkPlayerData.LastGameplaySnapshotSequence < _lastSnapshotSequence;
            bool countersReset = _networkPlayerData.NotesHit < _resolvedHits || _networkPlayerData.NotesMissed < _resolvedMisses;

            if (snapshotReset || countersReset)
            {
                ResetSimulation();
            }

            ApplySnapshotToStats();
            UpdateRemoteSoloState();

            bool starPowerChanged = _networkPlayerData.IsStarPowerActive != _lastStarPowerActive;
            if (starPowerChanged)
            {
                _lastStarPowerActive = _networkPlayerData.IsStarPowerActive;
                _trackPlayer.ApplyRemoteStarPowerState(_lastStarPowerActive);
            }
            ProcessNoteDiffs();

            _trackPlayer.UpdateRemoteCountdown();

            _lastSnapshotSequence = _networkPlayerData.LastGameplaySnapshotSequence;
        }

        private void OnDestroy()
        {
            if (_trackPlayer != null)
            {
                _trackPlayer.RegisterRemoteSimulation(null);
            }
        }

        private void ApplySnapshotToStats()
        {
            int totalNotes = _trackPlayer.GetRemoteNoteCount();
            _stats.TotalNotes = Math.Max(_stats.TotalNotes, totalNotes);

            _stats.NotesHit = Mathf.Clamp(_networkPlayerData.NotesHit, 0, totalNotes);
            _stats.Combo = Math.Max(0, _networkPlayerData.CurrentCombo);
            _stats.MaxCombo = Math.Max(_stats.MaxCombo, _networkPlayerData.CurrentStreak);

            int sanitizedScore = Math.Max(0, _networkPlayerData.CurrentScore);
            int totalSoloBonus = Math.Max(0, _networkPlayerData.SoloTotalBonus);
            if (sanitizedScore >= totalSoloBonus)
            {
                _stats.CommittedScore = sanitizedScore - totalSoloBonus;
                _stats.SoloBonuses = totalSoloBonus;
            }
            else
            {
                _stats.CommittedScore = sanitizedScore;
                _stats.SoloBonuses = 0;
            }
            _stats.PendingScore = 0;
            _stats.NoteScore = _stats.CommittedScore;
            _stats.SustainScore = 0;
            _stats.MultiplierScore = 0;
            _stats.BandBonusScore = 0;

            int baseMultiplier = Mathf.Min((_stats.Combo / 10) + 1, _trackPlayer.BaseEngine.BaseParameters.MaxMultiplier);
            baseMultiplier = Math.Max(baseMultiplier, 1);
            int effectiveMultiplier = baseMultiplier;
            if (_networkPlayerData.IsStarPowerActive)
            {
                effectiveMultiplier = Math.Min(baseMultiplier * 2, _trackPlayer.BaseEngine.BaseParameters.MaxMultiplier * 2);
            }

            _stats.ScoreMultiplier = effectiveMultiplier;
            _stats.BandMultiplier = effectiveMultiplier;
            _stats.IsStarPowerActive = _networkPlayerData.IsStarPowerActive;

            uint gaugeTicks = _trackPlayer.BaseEngine != null ? _trackPlayer.BaseEngine.TicksPerFullSpBar : 0u;
            if (gaugeTicks > 0)
            {
                int targetTicks = Mathf.Clamp(Mathf.RoundToInt(_networkPlayerData.StarPowerAmount * gaugeTicks), 0, (int)gaugeTicks);
                _stats.StarPowerTickAmount = (uint)targetTicks;
            }
            else if (_stats.TotalStarPowerTicks > 0)
            {
                int fallbackTicks = Mathf.Clamp(Mathf.RoundToInt(_networkPlayerData.StarPowerAmount * _stats.TotalStarPowerTicks), 0,
                    (int)_stats.TotalStarPowerTicks);
                _stats.StarPowerTickAmount = (uint)fallbackTicks;
            }
            else
            {
                _stats.StarPowerTickAmount = 0;
            }

            int totalStarPowerPhrases = Mathf.Max(0, _networkPlayerData.TotalStarPowerPhrases);
            if (totalStarPowerPhrases > 0)
            {
                _stats.TotalStarPowerPhrases = totalStarPowerPhrases;
            }

            int phrasesClamp = _stats.TotalStarPowerPhrases > 0 ? _stats.TotalStarPowerPhrases : totalStarPowerPhrases;
            if (phrasesClamp > 0)
            {
                _stats.StarPowerPhrasesHit = Mathf.Clamp(_networkPlayerData.StarPowerPhrasesHit, 0, phrasesClamp);
            }
            else
            {
                _stats.StarPowerPhrasesHit = Mathf.Max(0, _networkPlayerData.StarPowerPhrasesHit);
            }

            if (_stats is GuitarStats guitarStats)
            {
                guitarStats.Overstrums = Mathf.Max(0, _networkPlayerData.Overstrums);
                guitarStats.HoposStrummed = Mathf.Max(0, _networkPlayerData.HoposStrummed);
                guitarStats.GhostInputs = Mathf.Max(0, _networkPlayerData.GhostInputs);
            }

            if (_stats is DrumsStats drumsStats)
            {
                drumsStats.Overhits = Mathf.Max(0, _networkPlayerData.Overhits);
                drumsStats.GhostsHit = Mathf.Clamp(_networkPlayerData.GhostsHit, 0, drumsStats.TotalGhosts);
                drumsStats.AccentsHit = Mathf.Clamp(_networkPlayerData.AccentsHit, 0, drumsStats.TotalAccents);
                drumsStats.DynamicsBonus = Mathf.Max(0, _networkPlayerData.DynamicsBonus);
            }

            if (_stats is KeysStats keysStats)
            {
                keysStats.Overhits = Mathf.Max(0, _networkPlayerData.Overhits);
            }

            _stats.BandBonusScore = Mathf.Max(0, _networkPlayerData.BandBonusScore);

            if (_stats.TotalNotes > 0)
            {
                _stats.Stars = Mathf.Clamp01(_stats.Percent) * 5f;
            }
        }

        private void UpdateRemoteSoloState()
        {
            int sequence = _networkPlayerData.SoloSequence;
            bool soloActive = _networkPlayerData.SoloActive;
            int noteCount = Math.Max(0, _networkPlayerData.SoloNoteCount);
            int notesHit = Mathf.Clamp(_networkPlayerData.SoloNotesHit, 0, noteCount > 0 ? noteCount : 0);
            int lastBonus = Math.Max(0, _networkPlayerData.SoloLastBonus);
            var trackView = _trackPlayer.TrackView;

            if (sequence > _lastSoloSequence)
            {
                _lastSoloSequence = sequence;
                _hasRemoteSoloSection = false;
            }

            if (soloActive)
            {
                if (!_hasRemoteSoloSection && noteCount > 0)
                {
                    _remoteSoloSection = new SoloSection(0, 0, noteCount);
                    _remoteSoloSection.NotesHit = notesHit;
                    trackView?.StartSolo(_remoteSoloSection);
                    _hasRemoteSoloSection = true;
                }
                else if (_hasRemoteSoloSection && _remoteSoloSection != null)
                {
                    _remoteSoloSection.NotesHit = notesHit;
                }
            }

            if (!soloActive && _wasSoloActive && _hasRemoteSoloSection)
            {
                trackView?.EndSolo(lastBonus);
                _hasRemoteSoloSection = false;
                _remoteSoloSection = null;
            }
            else if (!soloActive)
            {
                _hasRemoteSoloSection = false;
                _remoteSoloSection = null;
            }

            _wasSoloActive = soloActive;
        }

        private void ProcessNoteDiffs()
        {
            int targetHits = Math.Max(0, _networkPlayerData.NotesHit);
            int targetMisses = Math.Max(0, _networkPlayerData.NotesMissed);

            while (_resolvedHits < targetHits)
            {
                if (!_trackPlayer.ResolveRemoteNote(ref _noteCursor, true))
                {
                    break;
                }
                _resolvedHits++;
            }

            while (_resolvedMisses < targetMisses)
            {
                if (!_trackPlayer.ResolveRemoteNote(ref _noteCursor, false))
                {
                    break;
                }
                _resolvedMisses++;
            }
        }

        private void ResetSimulation()
        {
            if (_trackPlayer != null)
            {
                _trackPlayer.ApplyRemoteStarPowerState(false);
                _trackPlayer.ResetRemoteSimulationState();
            }
            _noteCursor = 0;
            _resolvedHits = 0;
            _resolvedMisses = 0;
            _lastSnapshotSequence = 0;
            _lastStarPowerActive = false;
            _hasRemoteSoloSection = false;
            _lastSoloSequence = -1;
            _wasSoloActive = false;
            _remoteSoloSection = null;
        }
    }
}
