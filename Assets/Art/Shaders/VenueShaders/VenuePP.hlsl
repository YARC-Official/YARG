// Mirror params
#pragma multi_compile_local YARG_MIRROR_LEFT YARG_MIRROR_RIGHT YARG_MIRROR_CLOCK_CCW YARG_MIRROR_NONE
float  _YargMirrorStartTime;
float  _YargMirrorWipeLength;
// Posterize params
int    _YargPosterizeSteps;
// Scanline params
float4 _YargScanlineColor;
float  _YargScanlineSize;
float  _YargScanlineIntensity;
float  _YargScanlineEasingPower;
// Trails
float _YargTrailLength;
TEXTURE2D(_YargPrevFrame);


float2 YargVenueMirror(float2 uv)
{
    float elapsedTime = _Time.y - _YargMirrorStartTime;
    float t = saturate(elapsedTime / _YargMirrorWipeLength);

    #if YARG_MIRROR_LEFT
        float mirrorPoint = lerp(1.0, 0.0, t);

        if (uv.x > mirrorPoint)
        {
            uv.x = 1 - uv.x;
        }
    #elif YARG_MIRROR_RIGHT
        float mirrorPoint = lerp(0.0, 0.5, t);

        if (uv.x < mirrorPoint)
        {
            uv.x = 2 * mirrorPoint - uv.x;
        }
    #elif YARG_MIRROR_CLOCK_CCW
        float startAngle = 0.0;
        float endAngle = 2 * 3.14159;
        float currentAngle = lerp(startAngle, endAngle, t);

        float2 centered = uv - float2(0.5, 0.5);
        float pixelAngle = atan2(centered.y, centered.x);
        if (pixelAngle < 0)
        {
            pixelAngle += 2 * 3.14159;
        }

        if (pixelAngle <= currentAngle)
        {
            uv.x = 1.0 - uv.x;
        }
    #else
        if (uv.x < 0.5)
        {
            uv.x = 1 - uv.x;
        }
    #endif

    return uv;
}

half3 YargPosterize(half3 col)
{
    // posterize to n steps of color
    col = floor(col * _YargPosterizeSteps) / _YargPosterizeSteps;
    return col;
}

float ExpInOut(float t)
{
    t = 2.0 * t - 1.0;

    float sign = (t < 0.0) ? -1.0 : 1.0;
    t = sign * (1.0 - pow(1.0 - abs(t), _YargScanlineEasingPower));

    return 0.5 * (t + 1.0);
}

half3 ColorBlend(half3 original, half3 scanline, float t)
{
    // Apply exponential in-out easing to the blend factor
    float easedT = ExpInOut(t) * _YargScanlineIntensity;

    float brightnessBoost = 1.0 + ((1.5 - 1.0) * (1.0 - easedT));

    half3 brightened = original * brightnessBoost;

    brightened = min(brightened, 1.0);

    // Custom blending formula - you can modify this as needed
    // This version creates a more intense effect in the midtones
    half4 result;
    result.r = brightened.r * (1.0 - easedT) + scanline.r * easedT;
    result.g = brightened.g * (1.0 - easedT) + scanline.g * easedT;
    result.b = brightened.b * (1.0 - easedT) + scanline.b * easedT;

    return result;
}


half3 YargScanlines(half3 col, float2 uv)
{
    // Calculate scanline effect
    float scanline = frac(uv.y * _YargScanlineSize);
    // Apply scanline color
    col = ColorBlend(col, _YargScanlineColor, scanline);

    return col;
}

float2 YargVenueUV(float2 uv)
{
    if (_YargMirrorStartTime > 0.0)
    {
        uv = YargVenueMirror(uv);
    }

    return uv;
}

half4 YargVenuePP(half3 col, float2 uvDistorted, float2 uv)
{
    if(_YargPosterizeSteps > 0)
    {
        col = YargPosterize(col);
    }     

    if(_YargScanlineSize > 0)
    {
        col = YargScanlines(col, uvDistorted); 
    }

    if(_YargTrailLength > 0)
    {
        half3 prevCol = SAMPLE_TEXTURE2D(_YargPrevFrame, sampler_LinearClamp, uv).rgb;
        col = col + (1.0 - _YargTrailLength) * prevCol;
    }

    return half4(col, 1.0 /* alpha */);
}
