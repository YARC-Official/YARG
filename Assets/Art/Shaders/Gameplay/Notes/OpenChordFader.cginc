#ifndef OPENCHORDFADE_CGINC
#define OPENCHORDFADE_CGINC

void Fade_float(float2 uv, float vFlip, float3x3 RegionBounds, float FadedAlpha, float FadeDistance, out float Alpha)
{

    // Start with fully opaque
    Alpha = 1.0;

    float clampedY = clamp(lerp(uv.y, 1.0 - uv.y, vFlip), 0.0, 1.0);
    // float clampedY = clamp(flippedY, 0.0, 1.0);

    // Process each possible region row
    for (int i = 0; i < 3; i++)
    {
        float2 row = RegionBounds[i];
        float lower = row.x;
        float upper = row.y;

        float lowerFade = clamp(lower - FadeDistance, 0.0, 1.0);
        float upperFade = clamp(upper + FadeDistance, 0.0, 1.0);

        // Skip invalid regions
        float isValid = step(0.00001, max(abs(lower), abs(upper)));
        if (isValid < 0.5) continue;

        // Solid region (fully faded)
        if (clampedY >= lower && clampedY <= upper)
        {
            Alpha = FadedAlpha;
            continue;
        }

        // Lower transition zone with smoothstep
        if (clampedY >= lowerFade && clampedY < lower)
        {
            float factor = smoothstep(lowerFade, lower, clampedY);
            float lowerFadeAlpha = lerp(0.5, FadedAlpha, factor);
            Alpha = min(Alpha, lowerFadeAlpha);
        }

        // Upper transition zone with smoothstep
        if (clampedY > upper && clampedY <= upperFade)
        {
            float factor = smoothstep(upperFade, upper, clampedY);
            float upperFadeAlpha = lerp(0.5, FadedAlpha, factor);
            Alpha = min(Alpha, upperFadeAlpha);
        }
    }
}

#endif
