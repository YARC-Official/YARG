using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Data;
using YARG.Song;

namespace YARG.PlayMode
{
    public class VenueManager : MonoBehaviour
    {
        public static event Action<string> OnEventReceive;

        private const string VENUE_PREFIX = "venue_";

        private int _eventIndex = 0;
        private List<EventInfo> _venueEvents = new();
        private EventInfo CurrentEvent => _eventIndex < _venueEvents.Count ? _venueEvents[_eventIndex] : null;

        private void Start()
        {
            // Disable updates until the song starts
            enabled = false;
            Play.OnChartLoaded += OnChartLoaded;
            Play.OnSongStart += OnSongStart;
        }

        private void OnChartLoaded(YargChart chart)
        {
            Play.OnChartLoaded -= OnChartLoaded;

            // Queue up events
            foreach (var eventInfo in chart.events)
            {
                if (eventInfo.name.StartsWith(VENUE_PREFIX))
                {
                    _venueEvents.Add(eventInfo);
                }
            }
        }

        private void OnSongStart(SongEntry song)
        {
            Play.OnSongStart -= OnSongStart;

            // Enable updates
            enabled = true;
        }

        private void Update()
        {
            // Update venue events
            while (CurrentEvent != null && CurrentEvent.time <= Play.Instance.SongTime)
            {
                OnEventReceive?.Invoke(name);
                _eventIndex++;
            }
        }
    }
}