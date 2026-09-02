using System.Collections.Generic;
using UnityEngine;

namespace LibVLCSharp
{
    [CreateAssetMenu(fileName = "New VLC Config", menuName = "VLC/Player Configuration")]
    public class VLCPlayerConfiguration : ScriptableObject
    {
        [Header("Caching & Latency")]
        [Tooltip("Caching value for network resources, in milliseconds.")]
        public int NetworkCaching = 1000;

        [Tooltip("Caching value for local files, in milliseconds.")]
        public int FileCaching = 300;

        [Tooltip("Caching value for live streams, in milliseconds.")]
        public int LiveCaching = 300;

        [Header("Network")]
        [Tooltip("Automatically try to reconnect to the stream.")]
        public bool HttpReconnect = false;

        public string[] GetOptions()
        {
            var options = new List<string>();

            if (NetworkCaching >= 0)
                options.Add($"--network-caching={NetworkCaching}");

            if (FileCaching >= 0)
                options.Add($"--file-caching={FileCaching}");

            if (LiveCaching >= 0)
                options.Add($"--live-caching={LiveCaching}");

            if (HttpReconnect)
                options.Add("--http-reconnect");

            return options.ToArray();
        }
    }
}
