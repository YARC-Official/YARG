// Accessors for the YARG game state global texture.
//
// The texture is written by Assets/Script/Gameplay/TextureManager.cs and is
// APPEND-ONLY: new fields must always be appended after the existing ones.
// Never reorder or remove entries - these accessors address texels by index,
// so appending keeps every existing accessor (and every shader already
// using them) working unchanged.
//
// Layout:
//   texel 0 = song length (seconds)
//   texel 1 = song position (seconds)
//   texel 2 = fail meter value (0.0-1.0)
//   texel 3 = song progress, normalized (0.0-1.0)
//   texel 4 = countdown time (seconds until song starts, 0 once playing)
//   texel 5 = paused (0 or 1)
//   texel 6 = practice mode (0 or 1)
//   texel 7 = playback speed
//   texel 8 = beat phase, audio timing (0.0-1.0)
//   texel 9 = measure phase, audio timing (0.0-1.0)
//   texel 10 = star power active, any player (0 or 1)
//   texel 11 = star power charge, highest player (0.0-1.0)
//   texel 12 = crowd intensity (0.0-1.0)
//   texel 13 = band accuracy, average note hit % (0.0-1.0)
//   texel 14 = band combo multiplier, average player (>= 1)
//   texel 15 = stars earned incl. progress into next star (0.0-6.0)

#ifndef YARG_GAMESTATE_INCLUDED
#define YARG_GAMESTATE_INCLUDED

texture2D _Yarg_GameStateTex;

// Raw indexed access - prefer the named accessors below
float YargGameState(int index)
{
    return _Yarg_GameStateTex.Load(int3(index, 0, 0)).x;
}

float YargGameStateSongLength()
{
    return YargGameState(0);
}

float YargGameStateSongPosition()
{
    return YargGameState(1);
}

float YargGameStateFailMeter()
{
    return YargGameState(2);
}

float YargGameStateSongProgress()
{
    return YargGameState(3);
}

float YargGameStateCountdown()
{
    return YargGameState(4);
}

float YargGameStatePaused()
{
    return YargGameState(5);
}

float YargGameStatePracticeMode()
{
    return YargGameState(6);
}

float YargGameStateSongSpeed()
{
    return YargGameState(7);
}

float YargGameStateBeatPhase()
{
    return YargGameState(8);
}

float YargGameStateMeasurePhase()
{
    return YargGameState(9);
}

float YargGameStateStarPowerActive()
{
    return YargGameState(10);
}

float YargGameStateStarPowerCharge()
{
    return YargGameState(11);
}

float YargGameStateCrowdIntensity()
{
    return YargGameState(12);
}

float YargGameStateBandAccuracy()
{
    return YargGameState(13);
}

float YargGameStateComboMultiplier()
{
    return YargGameState(14);
}

float YargGameStateStars()
{
    return YargGameState(15);
}

#endif
