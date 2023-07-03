using System;
using UnityEngine;
using YARG.Song;

namespace YARG.PlayMode
{
    public class VenueManager : MonoBehaviour
    {
        public static event Action<string> OnEventReceive;

        private int _eventIndex;

        private void Start()
        {
            if (!Play.Instance.SongStarted)
            {
                // Disable updates until the song starts
                enabled = false;
                Play.OnSongStart += OnSongStart;
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
            var chart = Play.Instance.chart;

            // Update venue events
            while (chart.events.Count > _eventIndex && chart.events[_eventIndex].time <= Play.Instance.SongTime)
            {
                var name = chart.events[_eventIndex].name;
                if (name.StartsWith("venue_"))
                {
                    OnEventReceive?.Invoke(name);
                }

                _eventIndex++;
            }
        }
    }
}