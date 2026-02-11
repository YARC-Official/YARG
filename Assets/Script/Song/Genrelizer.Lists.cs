using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song
{
    public static partial class Genrelizer
    {
        // Official
        private const string ALTERNATIVE = "alternative";
        private const string AMBIENT_DRONE = "ambient/drone";
        private const string BALLAD = "ballad";
        private const string BLUES = "blues";
        private const string CHILDRENS_MUSIC = "children's music";
        private const string CHIPTUNE = "chiptune";
        private const string CLASSIC_ROCK = "classic rock";
        private const string CLASSICAL = "classical";
        private const string COUNTRY = "country";
        private const string DANCE = "dance";
        private const string DEATH_BLACK_METAL = "death/black metal";
        private const string DISCO = "disco";
        private const string DJENT = "djent";
        private const string DNB_BREAKBEAT_JUNGLE = "dnb/breakbeat/jungle";
        private const string DOOM_METAL = "doom metal";
        private const string DUBSTEP = "dubstep";
        private const string ELECTRONIC = "electronic";
        private const string ELECTRONIC_ROCK = "electronic rock";
        private const string EMO = "emo";
        private const string FOLK = "folk";
        private const string FUSION = "fusion";
        private const string GLAM = "glam";
        private const string GLITCH = "glitch";
        private const string GRINDCORE = "grindcore";
        private const string GROOVE_METAL = "groove metal";
        private const string GRUNGE = "grunge";
        private const string HARD_ROCK = "hard rock";
        private const string HARDCORE_EDM = "hardcore edm";
        private const string HEAVY_METAL = "heavy metal";
        private const string HIP_HOP_RAP = "hip-hop/rap";
        private const string HOLIDAY = "holiday";
        private const string HOUSE = "house";
        private const string IDM = "idm";
        private const string INDIE_ROCK = "indie rock";
        private const string INDUSTRIAL = "industrial";
        private const string INSPIRATIONAL = "inspirational";
        private const string J_POP = "j-pop";
        private const string J_ROCK = "j-rock";
        private const string JAZZ = "jazz";
        private const string K_POP = "k-pop";
        private const string LATIN = "latin";
        private const string MASHUP = "mashup";
        private const string MATH_ROCK = "math rock";
        private const string MELODIC_POWER_METAL = "melodic/power metal";
        private const string METALCORE = "metalcore";
        private const string NOISE = "noise";
        private const string NEW_WAVE = "new wave";
        private const string NOVELTY = "novelty";
        private const string NU_METAL = "nu-metal";
        private const string ORCHESTRAL = "orchestral";
        private const string POP = "pop";
        private const string POP_PUNK = "pop-punk";
        private const string POP_ROCK = "pop-rock";
        private const string POST_HARDCORE = "post-hardcore";
        private const string PROGRESSIVE = "progressive";
        private const string PSYCHEDELIC = "psychedelic";
        private const string PUNK = "punk";
        private const string RNB_SOUL_FUNK = "r&b/soul/funk";
        private const string REGGAE = "reggae";
        private const string ROCK = "rock";
        private const string ROCK_AND_ROLL = "rock & roll";
        private const string SKA = "ska";
        private const string SOUNDTRACK = "soundtrack";
        private const string SOUTHERN_ROCK = "southern rock";
        private const string SURF_ROCK = "surf rock";
        private const string SYNTHPOP_ELECTROPOP = "synthpop/electropop";
        private const string TECHNO = "techno";
        private const string THRASH_SPEED_METAL = "thrash/speed metal";
        private const string TRANCE = "trance";
        private const string TRAP = "trap";
        private const string WORLD = "world";
        private const string OTHER = "other";

        // From Magma, but not official
        private const string METAL = "metal";
        private const string POP_DANCE_ELECTRONIC = "pop/dance/electronic";
        private const string PROG = "prog";
        private const string REGGAE_SKA = "reggae/ska";
        private const string URBAN = "urban";

        


        // Mapping from official genre name to localization key
        public static Dictionary<string, string> GENRE_LOCALIZATION_KEYS = new(StringComparer.OrdinalIgnoreCase)
        {
            { ALTERNATIVE, "Alternative"},
            { AMBIENT_DRONE, "AmbientDrone"},
            { BALLAD, "Ballad"},
            { BLUES, "Blues"},
            { CHILDRENS_MUSIC, "ChildrensMusic"},
            { CHIPTUNE, "Chiptune"},
            { CLASSIC_ROCK, "ClassicRock"},
            { CLASSICAL, "Classical"},
            { COUNTRY, "Country"},
            { DANCE, "Dance"},
            { DEATH_BLACK_METAL, "DeathBlackMetal"},
            { DISCO, "Disco"},
            { DJENT, "Djent"},
            { DNB_BREAKBEAT_JUNGLE, "DnbBreakbeatJungle"},
            { DOOM_METAL, "DoomMetal"},
            { DUBSTEP, "Dubstep"},
            { ELECTRONIC, "Electronic"},
            { ELECTRONIC_ROCK, "ElectronicRock"},
            { EMO, "Emo"},
            { FOLK, "Folk"},
            { FUSION, "Fusion"},
            { GLAM, "Glam"},
            { GLITCH, "Glitch"},
            { GRINDCORE, "Grindcore"},
            { GROOVE_METAL, "GrooveMetal" },
            { GRUNGE, "Grunge"},
            { HARD_ROCK, "HardRock" },
            { HARDCORE_EDM, "HardcoreEDM"},
            { HEAVY_METAL, "HeavyMetal"},
            { HIP_HOP_RAP, "HipHopRap"},
            { HOLIDAY, "Holiday"},
            { HOUSE, "House"},
            { IDM, "IDM"},
            { INDIE_ROCK, "IndieRock"},
            { INDUSTRIAL, "Industrial"},
            { INSPIRATIONAL, "Inspirational"},
            { J_POP, "JPop"},
            { J_ROCK, "JRock"},
            { JAZZ, "Jazz"},
            { K_POP, "KPop"},
            { LATIN, "Latin"},
            { MASHUP, "Mashup"},
            { MATH_ROCK, "MathRock"},
            { MELODIC_POWER_METAL, "MelodicPowerMetal"},
            { METALCORE, "Metalcore"},
            { NOISE, "Noise"},
            { NEW_WAVE, "NewWave"},
            { NOVELTY, "Novelty"},
            { NU_METAL, "NuMetal"},
            { ORCHESTRAL, "Orchestral"},
            { POP, "Pop"},
            { POP_PUNK, "PopPunk"},
            { POP_ROCK, "PopRock"},
            { POST_HARDCORE, "PostHardcore"},
            { PROGRESSIVE, "Progressive"},
            { PSYCHEDELIC, "Psychedelic" },
            { PUNK, "Punk"},
            { RNB_SOUL_FUNK, "RnbSoulFunk"},
            { REGGAE, "Reggae"},
            { ROCK, "Rock"},
            { ROCK_AND_ROLL, "RockAndRoll"},
            { SKA, "Ska" },
            { SOUNDTRACK, "Soundtrack"},
            { SOUTHERN_ROCK, "SouthernRock"},
            { SURF_ROCK, "SurfRock"},
            { SYNTHPOP_ELECTROPOP, "SynthpopElectropop"},
            { TECHNO, "Techno"},
            { THRASH_SPEED_METAL, "ThrashSpeedMetal"},
            { TRANCE, "Trance"},
            { TRAP, "Trap"},
            { WORLD, "World"},
            { OTHER, "Other"}
        };

        public static Dictionary<(string magmaGenre, string magmaSubgenre), (string genre, string subgenre)> MAGMA_MAPPINGS = new(new TupleStringComparer())
        {
            { (ALTERNATIVE, "college"),                 (ALTERNATIVE, "College Rock") },
            { (ALTERNATIVE, "other"),                   (ALTERNATIVE, null) },

            { (BLUES, "acoustic"),                      (BLUES, "Acoustic Blues") },
            { (BLUES, "chicago"),                       (BLUES, "Chicago Blues") },
            { (BLUES, "classic"),                       (BLUES, "Classic Blues") },
            { (BLUES, "contemporary"),                  (BLUES, "Contemporary Blues") },
            { (BLUES, "country"),                       (BLUES, "Country Blues") },
            { (BLUES, "delta"),                         (BLUES, "Delta Blues") },
            { (BLUES, "electric"),                      (BLUES, "Electric Blues") },
            { (BLUES, "other"),                         (BLUES, null) },

            { (COUNTRY, "alternative"),                 (COUNTRY, "Alternative Country") },
            //(COUNTRY, "bluegrass")                    unchanged
            { (COUNTRY, "contemporary"),                (COUNTRY, "Contemporary Country") },
            //(COUNTRY, "honky tonk")                   unchanged
            { (COUNTRY, "outlaw"),                      (COUNTRY, "Outlaw Country") },
            { (COUNTRY, "traditional folk"),            (FOLK, "Traditional Folk") },
            { (COUNTRY, "traditionalfolk"),             (FOLK, "Traditional Folk") },
            { (COUNTRY, "other"),                       (COUNTRY, null) },

            //(GLAM, "goth")                            unchanged
            { (GLAM, "other"),                          (GLAM, null) },

            //(HIP_HOP_RAP, "alternative rap")          unchanged
            { (HIP_HOP_RAP, "alternativerap"),          (HIP_HOP_RAP, "Alternative Rap") },
            { (HIP_HOP_RAP, "gangsta"),                 (HIP_HOP_RAP, "Gangsta Rap") },
            //(HIP_HOP_RAP, "hardcore rap")             unchanged
            { (HIP_HOP_RAP, "hardcorerap"),             (HIP_HOP_RAP, "Hardcore Rap") },
            { (HIP_HOP_RAP, "old school hip hop"),      (HIP_HOP_RAP, "Oldschool Hip-Hop") },
            { (HIP_HOP_RAP, "oldschoolhiphop"),         (HIP_HOP_RAP, "Oldschool Hip-Hop") },
            { (HIP_HOP_RAP, "other"),                   (HIP_HOP_RAP, null) },
            //(HIP_HOP_RAP, "rap")                      unchanged
            //(HIP_HOP_RAP, "trip hop")                 unchanged
            { (HIP_HOP_RAP, "triphop"),                 (HIP_HOP_RAP, "Trip Hop") },
            //(HIP_HOP_RAP, "underground rap")          unchanged
            { (HIP_HOP_RAP, "undergroundrap"),          (HIP_HOP_RAP, "Underground Rap") },

            //(INDIE_ROCK, "lo-fi")                     unchanged
            { (INDIE_ROCK, "lofi"),                     (INDIE_ROCK, "Lo-Fi") },
            { (INDIE_ROCK, "math rock"),                (MATH_ROCK, null) },
            { (INDIE_ROCK, "mathrock"),                 (MATH_ROCK, null) },
            { (INDIE_ROCK, "noise"),                    (NOISE, "Noise Rock") },
            { (INDIE_ROCK, "other"),                    (INDIE_ROCK, null) },
            //(INDIE_ROCK, "post rock")                 unchanged
            { (INDIE_ROCK, "postrock"),                 (INDIE_ROCK, "Post Rock") },

            //(JAZZ, "acid jazz")                       unchanged
            { (JAZZ, "acidjazz"),                       (JAZZ, "Acid Jazz") },
            { (JAZZ, "contemporary"),                   (JAZZ, "Contemporary Jazz") },
            { (JAZZ, "experimental"),                   (JAZZ, "Experimental Jazz") },
            //(JAZZ, "ragtime")                         unchanged
            { (JAZZ, "smooth"),                         (JAZZ, "Smooth Jazz") },
            { (JAZZ, "other"),                          (JAZZ, null) },

            { (METAL, "alternative"),                   (HEAVY_METAL, "Alternative Metal") },
            { (METAL, "black"),                         (DEATH_BLACK_METAL, "Black Metal") },
            { (METAL, "core"),                          (METALCORE, null) },
            { (METAL, "death"),                         (DEATH_BLACK_METAL, "Death Metal") },
            { (METAL, "hair"),                          (HEAVY_METAL, "Hair Metal") },
            { (METAL, "industrial"),                    (INDUSTRIAL, "Industrial Metal") },
            { (METAL, "metal"),                         (HEAVY_METAL, null) },
            { (METAL, "power"),                         (MELODIC_POWER_METAL, "Power Metal") },
            { (METAL, "prog"),                          (HEAVY_METAL, "Progressive Metal") },
            { (METAL, "speed"),                         (THRASH_SPEED_METAL, "Speed Metal") },
            { (METAL, "thrash"),                        (THRASH_SPEED_METAL, "Thrash Metal") },
            { (METAL, "other"),                         (HEAVY_METAL, null) },

            { (NEW_WAVE, "dark wave"),                  (NEW_WAVE, "Darkwave") },
            //(NEW_WAVE, "darkwave"),                   unchanged
            //(NEW_WAVE, "electroclash)                 unchanged
            { (NEW_WAVE, "synthpop"),                   (SYNTHPOP_ELECTROPOP, "Synthpop") },
            { (NEW_WAVE, "other"),                      (NEW_WAVE, null) },

            { (POP_DANCE_ELECTRONIC, "ambient"),        (AMBIENT_DRONE, "Ambient") },
            { (POP_DANCE_ELECTRONIC, "breakbeat"),      (DNB_BREAKBEAT_JUNGLE, "Breakbeat") },
            { (POP_DANCE_ELECTRONIC, "chiptune"),       (CHIPTUNE, null) },
            { (POP_DANCE_ELECTRONIC, "dance"),          (DANCE, null) },
            { (POP_DANCE_ELECTRONIC, "downtempo"),      (ELECTRONIC, "Downtempo") },
            { (POP_DANCE_ELECTRONIC, "dub"),            (DUBSTEP, null) },
            { (POP_DANCE_ELECTRONIC, "drum and bass"),  (DNB_BREAKBEAT_JUNGLE, "Drum and Bass") },
            { (POP_DANCE_ELECTRONIC, "drumandbass"),    (DNB_BREAKBEAT_JUNGLE, "Drum and Bass") },
            { (POP_DANCE_ELECTRONIC, "electronica"),    (ELECTRONIC, "Electronica") },
            { (POP_DANCE_ELECTRONIC, "garage"),         (ELECTRONIC, "Garage") },
            { (POP_DANCE_ELECTRONIC, "hardcore dance"), (HARDCORE_EDM, "Hardcore Dance") },
            { (POP_DANCE_ELECTRONIC, "hardcoredance"),  (HARDCORE_EDM, "Hardcore Dance") },
            { (POP_DANCE_ELECTRONIC, "house"),          (HOUSE, null) },
            { (POP_DANCE_ELECTRONIC, "industrial"),     (INDUSTRIAL, null) },
            { (POP_DANCE_ELECTRONIC, "techno"),         (TECHNO, "") },
            { (POP_DANCE_ELECTRONIC, "trance"),         (TRANCE, "") },
            { (POP_DANCE_ELECTRONIC, "other"),          (ELECTRONIC, null) },

            { (POP_ROCK, "contemporary"),               (POP_ROCK, "Contemporary Pop-Rock") },
            { (POP_ROCK, "disco"),                      (DISCO, null) },
            { (POP_ROCK, "motown"),                     (RNB_SOUL_FUNK, "Motown") },
            { (POP_ROCK, "pop"),                        (POP, "PopRock") },
            { (POP_ROCK, "rhythm and blues"),           (RNB_SOUL_FUNK, "Rhythm and Blues") },
            { (POP_ROCK, "rhythmandblues"),             (RNB_SOUL_FUNK, "Rhythm and Blues") },
            //(POP_ROCK, "soft rock")                   unchanged
            { (POP_ROCK, "softrock"),                   (POP_ROCK, "Soft Rock") },
            { (POP_ROCK, "soul"),                       (RNB_SOUL_FUNK, "Soul") },
            { (POP_ROCK, "teen"),                       (POP, "Teen Pop") },
            { (POP_ROCK, "other"),                      (POP_ROCK, null) },

            { (PROG, "prog rock"),                      (PROGRESSIVE, null) }, // Without other Prog subgenres, this is effectively meaningless
            { (PROG, "progrock"),                       (PROGRESSIVE, null) },

            { (PUNK, "alternative"),                    (PUNK, "Alternative Punk Rock") },
            { (PUNK, "classic"),                        (PUNK, "Classic Punk Rock") },
            { (PUNK, "garage"),                         (PUNK, "Garage Punk") },
            { (PUNK, "hardcore"),                       (PUNK, "Hardcore Punk") },
            { (PUNK, "pop"),                            (POP_PUNK, null) },
            { (PUNK, "other"),                          (PUNK, null) },

            { (RNB_SOUL_FUNK, "disco"),                 (DISCO, null) },
            //(RNB_SOUL_FUNK, "funk")                   unchanged
            //(RNB_SOUL_FUNK, "motown")                 unchanged
            { (RNB_SOUL_FUNK, "other"),                 (RNB_SOUL_FUNK, null) },
            //(RNB_SOUL_FUNK, "rhythm and blues")       unchanged
            //(RNB_SOUL_FUNK, "soul")                   unchanged

            { (REGGAE_SKA, "reggae"),                   (REGGAE, null) },
            { (REGGAE_SKA, "ska"),                      (SKA, null) },
            { (REGGAE_SKA, "other"),                    (SKA, null) }, // Belt & suspenders; should've been caught earlier as a special case

            { (ROCK, "arena"),                          (ROCK, "Arena Rock") },
            { (ROCK, "blues"),                          (ROCK, "Blues Rock") },
            { (ROCK, "folk rock"),                      (FOLK, "Folk Rock") },
            { (ROCK, "folkrock"),                       (FOLK, "Folk Rock") },
            { (ROCK, "funk"),                           (RNB_SOUL_FUNK, "Funk") },
            { (ROCK, "garage"),                         (ROCK, "Garage Rock") },
            { (ROCK, "psychedelic"),                    (ROCK, "Psychedelic Rock") },
            { (ROCK, "reggae"),                         (REGGAE, null) },
            { (ROCK, "rockabilly"),                     (ROCK_AND_ROLL, "Rockabilly") },
            { (ROCK, "rock and roll"),                  (ROCK_AND_ROLL, null) },
            { (ROCK, "rockandroll"),                    (ROCK_AND_ROLL, null) },
            { (ROCK, "ska"),                            (SKA, null) },
            { (ROCK, "surf"),                           (SURF_ROCK, null) },
            { (ROCK, "other"),                          (ROCK, null) },

            { (URBAN, "alternative rap"),               (HIP_HOP_RAP, "Alternative Rap") },
            { (URBAN, "alternativerap"),                (HIP_HOP_RAP, "Alternative Rap") },
            { (URBAN, "downtempo"),                     (ELECTRONIC, "Downtempo") },
            { (URBAN, "drum and bass"),                 (DNB_BREAKBEAT_JUNGLE, "Drum and Bass") },
            { (URBAN, "drumandbass"),                   (DNB_BREAKBEAT_JUNGLE, "Drum and Bass") },
            { (URBAN, "dub"),                           (REGGAE, "dub") },
            { (URBAN, "electronica"),                   (ELECTRONIC, "Electronica") },
            { (URBAN, "gangsta"),                       (HIP_HOP_RAP, "Gangsta Rap") },
            { (URBAN, "garage"),                        (ELECTRONIC, "Garage") },
            { (URBAN, "hardcore dance"),                (HARDCORE_EDM, "Hardcore Dance") },
            { (URBAN, "hardcoredance"),                 (HARDCORE_EDM, "Hardcore Dance") },
            { (URBAN, "hardcore rap"),                  (HIP_HOP_RAP, "Hardcore Rap") },
            { (URBAN, "hardcorerap"),                   (HIP_HOP_RAP, "Hardcore Rap") },
            { (URBAN, "hip hop"),                       (HIP_HOP_RAP, "Hip-Hop") },
            { (URBAN, "hiphop"),                        (HIP_HOP_RAP, "Hip-Hop") },
            { (URBAN, "industrial"),                    (INDUSTRIAL, null) },
            { (URBAN, "old school hip hop"),            (HIP_HOP_RAP, "Oldschool Hip-Hop") },
            { (URBAN, "oldschoolhiphop"),               (HIP_HOP_RAP, "Oldschool Hip-Hop") },
            { (URBAN, "rap"),                           (HIP_HOP_RAP, "Rap") },
            { (URBAN, "trip hop"),                      (HIP_HOP_RAP, "Trip Hop") },
            { (URBAN, "underground rap"),               (HIP_HOP_RAP, "Underground Rap") },
            { (URBAN, "other"),                         (OTHER, "Urban") }, // Urban is deprecated as a genre

            //(OTHER, "a capella")                      unchanged
            //(OTHER, "acoustic")                       unchanged
            { (OTHER, "ambient"),                       (AMBIENT_DRONE, "Ambient") },
            { (OTHER, "breakbeat"),                     (DNB_BREAKBEAT_JUNGLE, "Breakbeat") },
            { (OTHER, "chiptune"),                      (CHIPTUNE, null) },
            { (OTHER, "classical"),                     (CLASSICAL, null) },
            { (OTHER, "contemporary folk"),             (FOLK, "Contemporary Folk") },
            { (OTHER, "contemporaryfolk"),              (FOLK, "Contemporary Folk") },
            { (OTHER, "dance"),                         (DANCE, null) },
            { (OTHER, "electronica"),                   (ELECTRONIC, "Electronica") },
            //(OTHER, "experimental")                   unchanged
            { (OTHER, "house"),                         (HOUSE, null) },
            //(OTHER, "oldies")                         unchanged
            { (OTHER, "techno"),                        (TECHNO, null) },
            { (OTHER, "trance"),                        (TRANCE, null) },
        };

        private class TupleStringComparer : IEqualityComparer<(string genre, string subgenre)>
        {
            public bool Equals((string genre, string subgenre) x, (string genre, string subgenre) y)
            {
                return StringComparer.OrdinalIgnoreCase.Equals(x.Item1, y.Item1) && StringComparer.OrdinalIgnoreCase.Equals(x.Item2, y.Item2);
            }

            public int GetHashCode((string genre, string subgenre) obj)
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2);
            }
        }
    }
}
