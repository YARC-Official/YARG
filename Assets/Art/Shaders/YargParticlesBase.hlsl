#ifndef YARG_PARTICLES_BASE_INCLUDED
#define YARG_PARTICLES_BASE_INCLUDED

// Define our custom implementations
half3 YargGetWorldSpaceNormalizeViewDir(float3 positionWS)
{
    if (_YargHighwaysN > 0)
    {
        float3 V = YargWorldSpaceCameraPos(positionWS).xyz - positionWS;
        return half3(normalize(V));
    }

    if (IsPerspectiveProjection())
    {
        // Perspective
        float3 V = GetCurrentViewPosition() - positionWS;
        return half3(normalize(V));
    }
    else
    {
        // Orthographic
        return half3(-GetViewForwardDir());
    }
}

VertexPositionInputs YargGetVertexPositionInputs(float3 positionOS)
{
    VertexPositionInputs input;
    input.positionWS = TransformObjectToWorld(positionOS);
    input.positionVS = YargTransformWorldToView(input.positionWS);
    input.positionCS = YargTransformWorldToHClip(input.positionWS);

    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;

    return input;
}

#endif // YARG_PARTICLES_BASE_INCLUDED
