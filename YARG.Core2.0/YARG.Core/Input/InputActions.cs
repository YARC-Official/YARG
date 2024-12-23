namespace YARG.Core.Input
{
    // !DO NOT MODIFY THE VALUES OR ORDER OF THESE ENUMS!
    // Since they are serialized in replays, they *must* remain the same across changes.

    /// <summary>
    /// The actions available for navigating menus.
    /// </summary>
    public enum MenuAction : byte
    {
        /// <summary>Green action button.</summary>
        Green = 0,
        /// <summary>Red action button.</summary>
        Red = 1,
        /// <summary>Yellow action button.</summary>
        Yellow = 2,
        /// <summary>Blue action button.</summary>
        Blue = 3,
        /// <summary>Orange action button.</summary>
        Orange = 4,

        /// <summary>Up navigation button.</summary>
        Up = 5,
        /// <summary>Down navigation button.</summary>
        Down = 6,
        /// <summary>Left navigation button.</summary>
        Left = 7,
        /// <summary>Right navigation button.</summary>
        Right = 8,

        /// <summary>Start action button.</summary>
        Start = 9,
        /// <summary>Select action button.</summary>
        Select = 10,
    }

    /// <summary>
    /// The actions available when playing guitar modes.
    /// </summary>
    public enum GuitarAction : byte
    {
        /// <summary>Generic first fret button.</summary>
        Fret1 = 0,
        /// <summary>Generic second fret button.</summary>
        Fret2 = 1,
        /// <summary>Generic third fret button.</summary>
        Fret3 = 2,
        /// <summary>Generic fourth fret button.</summary>
        Fret4 = 3,
        /// <summary>Generic fifth fret button.</summary>
        Fret5 = 4,
        /// <summary>Generic sixth fret button.</summary>
        Fret6 = 5,

        /// <summary>The up-strum button.</summary>
        StrumUp = 6,
        /// <summary>The down-strum button.</summary>
        StrumDown = 7,

        /// <summary>The whammy axis.</summary>
        Whammy = 8,
        /// <summary>The Star Power action, reported as a button.</summary>
        StarPower = 9,

        /// <summary>(5-fret) Green fret button.</summary>
        /// <remarks>Alias of <see cref="Fret1"/>.</remarks>
        GreenFret = Fret1,
        /// <summary>(5-fret) Red fret button.</summary>
        /// <remarks>Alias of <see cref="Fret2"/>.</remarks>
        RedFret = Fret2,
        /// <summary>(5-fret) Yellow fret button.</summary>
        /// <remarks>Alias of <see cref="Fret3"/>.</remarks>
        YellowFret = Fret3,
        /// <summary>(5-fret) Blue fret button.</summary>
        /// <remarks>Alias of <see cref="Fret4"/>.</remarks>
        BlueFret = Fret4,
        /// <summary>(5-fret) Orange fret button.</summary>
        /// <remarks>Alias of <see cref="Fret5"/>.</remarks>
        OrangeFret = Fret5,

        /// <summary>(6-fret) Black 1 fret button.</summary>
        /// <remarks>Alias of <see cref="Fret1"/>.</remarks>
        Black1Fret = Fret1,
        /// <summary>(6-fret) Black 2 fret button.</summary>
        /// <remarks>Alias of <see cref="Fret2"/>.</remarks>
        Black2Fret = Fret2,
        /// <summary>(6-fret) Black 3 fret button.</summary>
        /// <remarks>Alias of <see cref="Fret3"/>.</remarks>
        Black3Fret = Fret3,
        /// <summary>(6-fret) White 1 fret button.</summary>
        /// <remarks>Alias of <see cref="Fret4"/>.</remarks>
        White1Fret = Fret4,
        /// <summary>(6-fret) White 2 fret button.</summary>
        /// <remarks>Alias of <see cref="Fret5"/>.</remarks>
        White2Fret = Fret5,
        /// <summary>(6-fret) White 3 fret button.</summary>
        /// <remarks>Alias of <see cref="Fret6"/>.</remarks>
        White3Fret = Fret6,
    }

    /// <summary>
    /// The actions available when playing Pro Guitar modes.
    /// </summary>
    public enum ProGuitarAction : byte
    {
        /// <summary>First string's fret number integer.</summary>
        String1_Fret = 0,
        /// <summary>Second string's fret number integer.</summary>
        String2_Fret = 1,
        /// <summary>Third string's fret number integer.</summary>
        String3_Fret = 2,
        /// <summary>Fourth string's fret number integer.</summary>
        String4_Fret = 3,
        /// <summary>Fifth string's fret number integer.</summary>
        String5_Fret = 4,
        /// <summary>Sixth string's fret number integer.</summary>
        String6_Fret = 5,

        /// <summary>First string's strum state, reported as a button.</summary>
        String1_Strum = 6,
        /// <summary>Second string's strum state, reported as a button.</summary>
        String2_Strum = 7,
        /// <summary>Third string's strum state, reported as a button.</summary>
        String3_Strum = 8,
        /// <summary>Fourth string's strum state, reported as a button.</summary>
        String4_Strum = 9,
        /// <summary>Fifth string's strum state, reported as a button.</summary>
        String5_Strum = 10,
        /// <summary>Sixth string's strum state, reported as a button.</summary>
        String6_Strum = 11,

        /// <summary>The Star Power action, reported as a button.</summary>
        StarPower = 12,
    }

    /// <summary>
    /// The actions available when playing Pro Keys modes.
    /// </summary>
    public enum ProKeysAction : byte
    {
        /// <summary>Key 1's press state, reported as a button.</summary>
        Key1 = 0,
        /// <summary>Key 2's press state, reported as a button.</summary>
        Key2 = 1,
        /// <summary>Key 3's press state, reported as a button.</summary>
        Key3 = 2,
        /// <summary>Key 4's press state, reported as a button.</summary>
        Key4 = 3,
        /// <summary>Key 5's press state, reported as a button.</summary>
        Key5 = 4,
        /// <summary>Key 6's press state, reported as a button.</summary>
        Key6 = 5,
        /// <summary>Key 7's press state, reported as a button.</summary>
        Key7 = 6,
        /// <summary>Key 8's press state, reported as a button.</summary>
        Key8 = 7,
        /// <summary>Key 9's press state, reported as a button.</summary>
        Key9 = 8,
        /// <summary>Key 10's press state, reported as a button.</summary>
        Key10 = 9,
        /// <summary>Key 11's press state, reported as a button.</summary>
        Key11 = 10,
        /// <summary>Key 12's press state, reported as a button.</summary>
        Key12 = 11,
        /// <summary>Key 13's press state, reported as a button.</summary>
        Key13 = 12,
        /// <summary>Key 14's press state, reported as a button.</summary>
        Key14 = 13,
        /// <summary>Key 15's press state, reported as a button.</summary>
        Key15 = 14,
        /// <summary>Key 16's press state, reported as a button.</summary>
        Key16 = 15,
        /// <summary>Key 17's press state, reported as a button.</summary>
        Key17 = 16,
        /// <summary>Key 18's press state, reported as a button.</summary>
        Key18 = 17,
        /// <summary>Key 19's press state, reported as a button.</summary>
        Key19 = 18,
        /// <summary>Key 20's press state, reported as a button.</summary>
        Key20 = 19,
        /// <summary>Key 21's press state, reported as a button.</summary>
        Key21 = 20,
        /// <summary>Key 22's press state, reported as a button.</summary>
        Key22 = 21,
        /// <summary>Key 23's press state, reported as a button.</summary>
        Key23 = 22,
        /// <summary>Key 24's press state, reported as a button.</summary>
        Key24 = 23,
        /// <summary>Key 25's press state, reported as a button.</summary>
        Key25 = 24,

        /// <summary>The Star Power action, reported as a button.</summary>
        StarPower = 25,
        /// <summary>The touch effects bar, reported as an axis.</summary>
        TouchEffects = 26,
    }

    /// <summary>
    /// The actions available when playing drums modes.
    /// </summary>
    public enum DrumsAction : byte
    {
        /// <summary>Generic first drum hit velocity.</summary>
        Drum1 = 0,
        /// <summary>Generic second drum hit velocity.</summary>
        Drum2 = 1,
        /// <summary>Generic third drum hit velocity.</summary>
        Drum3 = 2,
        /// <summary>Generic fourth drum hit velocity.</summary>
        Drum4 = 3,

        /// <summary>Generic first cymbal hit velocity.</summary>
        Cymbal1 = 4,
        /// <summary>Generic second cymbal hit velocity.</summary>
        Cymbal2 = 5,
        /// <summary>Generic third cymbal hit velocity.</summary>
        Cymbal3 = 6,

        /// <summary>Kick pedal hit velocity.</summary>
        Kick = 7,

        /// <summary>(4-lane and 5-lane) red drum hit velocity.</summary>
        /// <remarks>Alias of <see cref="Drum1"/>.</remarks>
        RedDrum = Drum1,
        /// <summary>(4-lane only) yellow drum hit velocity.</summary>
        /// <remarks>Alias of <see cref="Drum2"/>.</remarks>
        YellowDrum = Drum2,
        /// <summary>(4-lane and 5-lane) blue drum hit velocity.</summary>
        /// <remarks>Alias of <see cref="Drum3"/>.</remarks>
        BlueDrum = Drum3,
        /// <summary>(4-lane and 5-lane) green drum hit velocity.</summary>
        /// <remarks>Alias of <see cref="Drum4"/>.</remarks>
        GreenDrum = Drum4,

        /// <summary>(4-lane and 5-lane) Yellow cymbal hit velocity.</summary>
        /// <remarks>Alias of <see cref="Cymbal1"/>.</remarks>
        YellowCymbal = Cymbal1,
        /// <summary>(5-lane only) Orange cymbal hit velocity.</summary>
        /// <remarks>Alias of <see cref="Cymbal2"/>.</remarks>
        OrangeCymbal = Cymbal2,
        /// <summary>(4-lane only) Blue cymbal hit velocity.</summary>
        /// <remarks>Alias of <see cref="Cymbal2"/>.</remarks>
        BlueCymbal = Cymbal2,
        /// <summary>(4-lane only) Green cymbal hit velocity. Red cymbal under lefty flip.</summary>
        /// <remarks>Alias of <see cref="Cymbal3"/>.</remarks>
        GreenCymbal = Cymbal3,
    }

    /// <summary>
    /// The actions available when playing vocals modes.
    /// </summary>
    public enum VocalsAction : byte
    {
        /// <summary>The current pitch being sung.</summary>
        Pitch = 0,
        /// <summary>Percussion hit action, reported as a button..</summary>
        Hit = 1,
        /// <summary>Star Power activation, reported as a button.</summary>
        StarPower = 2,
    }
}