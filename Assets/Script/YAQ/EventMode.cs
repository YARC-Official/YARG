using System;
using UnityEngine;

namespace YARG
{
    /// <summary>
    /// YAQ Event Mode — strips normal menus and drives song selection from the YAQ LAN queue.
    /// Enable with <c>-yaq-event</c>. Optional <c>-yaq-url ws://127.0.0.1:3000/ws?role=yarg</c>.
    /// </summary>
    public static class EventMode
    {
        public static bool Enabled { get; set; }

        public static string YaqWebSocketUrl { get; set; } =
            "ws://127.0.0.1:3000/ws?role=yarg";

        public static bool IsActive => Enabled || CommandLineArgs.YaqEvent;
    }
}
