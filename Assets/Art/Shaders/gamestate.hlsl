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

#endif
