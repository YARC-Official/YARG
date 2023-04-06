using System;
using System.Collections.Generic;
using System.Text;

namespace Mackiloha.Song
{
    public enum DreamscapeCamera : int
    {
        None,
        kP9DreamSlow = 1 << 16,
        kP9DreamFast = 1 << 17,
        kP9DreamPsych = 1 << 18,
        kP9DreamAllConfigs = (1 << 18) | (1 << 17) | (1 << 16)
    }

    public class P9SongPref : MiloObject
    {
        public string Venue { get; set; }
        public List<string> MiniVenues { get; } = new List<string>();
        public List<string> Scenes { get; } = new List<string>();

        public string DreamscapeOutfit { get; set; }
        public string StudioOutfit { get; set; }

        public List<string> GeorgeInstruments { get; } = new List<string>();
        public List<string> JohnInstruments { get; } = new List<string>();
        public List<string> PaulInstruments { get; } = new List<string>();
        public List<string> RingoInstruments { get; } = new List<string>();

        public string Tempo { get; set; }
        public string SongClips { get; set; }
        public string DreamscapeFont { get; set; }

        // TBRB specific
        public string GeorgeAmp { get; set; }
        public string JohnAmp { get; set; }
        public string PaulAmp { get; set; }
        public string Mixer { get; set; }
        public DreamscapeCamera DreamscapeCamera { get; set; }

        public string LyricPart { get; set; }

        // GDRB specific
        public string NormalOutfit { get; set; }
        public string BonusOutfit { get; set; }
        public string DrumSet { get; set; }
        public string Era { get; set; }
        public string CamDirectory { get; set; }
        public string MediaDirectory { get; set; }
        public string SongIntroCam { get; set; }
        public string WinCam { get; set; }

        public override string Type => "P9SongPref";
    }
}
