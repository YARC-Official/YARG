#nullable enable
using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Logging;
using YARG.Gameplay.Player;

namespace YARG.Gameplay
{
    public partial class GameManager
    {
        // TODO: Move this class definition somewhere more appropriate
        public class UnisonEvent : IEquatable<UnisonEvent>
        {

            public double Time { get; }
            public double TimeEnd { get; }
            public int PartCount { get; private set; }
            public int SuccessCount { get; private set; }
            public List<TrackPlayer> Players { get; private set; }

            public bool Equals(UnisonEvent other) => Time.Equals(other.Time) && TimeEnd.Equals(other.TimeEnd);
            // public bool Equals(double startTime, double endTime) => Time.Equals(startTime) && TimeEnd.Equals(endTime);
            // public override bool Equals(object obj) => Equals(obj as UnisonEvent);
            public override int GetHashCode() => HashCode.Combine(Time, TimeEnd);

            public UnisonEvent(double time, double timeEnd)
            {
                Time = time;
                TimeEnd = timeEnd;
                PartCount = 0;
                SuccessCount = 0;
                Players = new List<TrackPlayer>();
            }

            public void AddPlayer(TrackPlayer trackPlayer)
            {
                if (Players.Contains(trackPlayer))
                {
                    return;
                }
                Players.Add(trackPlayer);
                PartCount++;
            }

            // Returns true if all players succesfully completed the unison
            public bool Success(TrackPlayer trackPlayer)
            {
                if (Players.Contains(trackPlayer))
                {
                    SuccessCount++;
                }

                if (SuccessCount == Players.Count)
                {
                    // TODO: Do something other than log the successful unison
                    YargLogger.LogDebug("Unison phrase successfully completed");
                    return true;
                }
                // If SuccessCount is ever greater than the number of players, something has gone seriously wrong
                YargLogger.Assert(SuccessCount <= Players.Count);
                return false;
            }
        }

        private List<UnisonEvent> _unisonEvents = new();

        public struct StarPowerSection : IEquatable<StarPowerSection>
        {
            public double Time;
            public double TimeEnd;
            public Instrument Instrument;
            public TrackPlayer Player;

            public StarPowerSection(double time, double timeEnd, Instrument instrument, TrackPlayer player)
            {
                Time = time;
                TimeEnd = timeEnd;
                Instrument = instrument;
                Player = player;
            }

            public bool Equals(StarPowerSection other) => Time.Equals(other.Time) && TimeEnd.Equals(other.TimeEnd);

            public override bool Equals(object obj) => obj is StarPowerSection other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Time, TimeEnd);
        }

        public delegate void UnisonPhrasesReadyEvent(List<UnisonEvent> unisonEvents);

        public delegate void UnisonPhraseSuccessEvent();
        public UnisonPhrasesReadyEvent? OnUnisonPhrasesReady;
        public UnisonPhraseSuccessEvent? OnUnisonPhraseSuccess;

        private Dictionary<TrackPlayer, List<StarPowerSection>> _starPowerSections = new();
        private List<TrackPlayer> _reportedPlayers = new();
        private bool _unisonsReady = false;

        private int _trackPlayerCount = 0;

        // This takes a TrackPlayer because vocals can't participate in unisons
        public void AddStarPowerSections(List<Phrase> phrases, TrackPlayer player)
        {
            // Doing this every time is silly, it should be somewhere else in GameManager
            foreach (var p in _players)
            {
                if (p.GetType() != typeof(VocalsPlayer))
                {
                    _trackPlayerCount++;
                }
            }

            _reportedPlayers.Add(player);
            foreach (var phrase in phrases)
            {
                if (phrase.Type == PhraseType.StarPower)
                {
                    var spPhrase = new StarPowerSection(phrase.Time, phrase.TimeEnd, player.Player.Profile.CurrentInstrument, player);
                    if (_starPowerSections.ContainsKey(player))
                    {
                        _starPowerSections[player].Add(spPhrase);
                    }
                    else
                    {
                        _starPowerSections.Add(player, new List<StarPowerSection> { spPhrase });
                    }
                }
            }

            // If there's only one player, there is nothing to compare
            if (_reportedPlayers.Count < 2)
            {
                return;
            }

            // Find unisons, if they exist
            foreach (var p in _reportedPlayers)
            {
                var spList = _starPowerSections[p];
                foreach (var candidate in _starPowerSections.Keys)
                {
                    if (candidate.Equals(p))
                    {
                        // Don't want to compare with the same player
                        continue;
                    }

                    foreach (var sp in _starPowerSections[candidate])
                    {
                        if (!spList.Contains(sp))
                        {
                            continue;
                        }
                        var unison = new UnisonEvent(sp.Time, sp.TimeEnd);
                        if (_unisonEvents.Contains(unison))
                        {
                            var ueIndex = _unisonEvents.IndexOf(unison);
                            _unisonEvents[ueIndex].AddPlayer(p);
                        }
                        else
                        {
                            _unisonEvents.Add(unison);
                            unison.AddPlayer(p);
                        }
                    }
                }
            }

            // If there are still more reports to come, return now
            if (_reportedPlayers.Count < _trackPlayerCount)
            {
                return;
            }

            // All players are in, so clean up the candidate unisons with only one part
            // Those are SP events that turned out not to be unisons
            for (int i = 0; i < _unisonEvents.Count; i++)
            {
                if (_unisonEvents[i].PartCount < 2)
                {
                    _unisonEvents.Remove(_unisonEvents[i]);
                }
            }

            var unisonCount = 0;
            foreach (var unison in _unisonEvents)
            {
                if (unison.PartCount > 1)
                {
                    unisonCount++;
                }
            }
            // If we reach this point, all players are accounted for
            _unisonsReady = true;
            OnUnisonPhrasesReady?.Invoke(_unisonEvents);
            YargLogger.LogFormatDebug("Created {0} unison events for {1} players", unisonCount, _trackPlayerCount);
        }

        public void StarPowerPhraseHit(TrackPlayer trackPlayer, double time)
        {
            // Find the relevant unison and increment its SuccessCount
            foreach (var unison in _unisonEvents)
            {
                // The SP phrases for each instrument end at different times,
                // so an exact match is impossible
                if (unison.Time < time && time < unison.TimeEnd)
                {
                    if (unison.Success(trackPlayer))
                    {
                        // Success returned true, so all the other players
                        // were also successful
                        OnUnisonPhraseSuccess?.Invoke();
                        AwardStarPowerBonus(unison);
                    }
                }
            }
        }

        private void AwardStarPowerBonus(UnisonEvent unison)
        {
            foreach (var player in unison.Players)
            {
                player.BaseEngine.GainStarPower(player.BaseEngine.TicksPerQuarterSpBar);
            }
        }

        public List<UnisonEvent>? GetUnisonEvents()
        {
            if (_unisonsReady)
            {
                return _unisonEvents;
            }
            return null;
        }
    }
}