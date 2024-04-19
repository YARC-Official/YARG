#ifndef YARG_INSTANCE_TRANSFORM_INCLUDED
#define YARG_INSTANCE_TRANSFORM_INCLUDED

// https://gist.github.com/ArieLeo/d7e6bc5485caa9ba99cd3a59d0f53404

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
    // 100 is used as a placeholder size, the actual size
    // comes from script when setting the array
    float4x4 _InstancedTransform[100];
#endif

// Dummy function that passes through the given input.
// This is needed in order to correctly include the setup procedure below;
// the configuration that sets that up is in the succeeding shader graph node.
void Passthrough_float(in float3 In, out float3 Out)
{
    Out = In;
}

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
// Converts individual object transform components into a transform matrix.
inline float4x4 TRSMatrix(float3 position, float4 rotation, float3 scale)
{
    float4x4 m = 0.0;

    m[0][0] = (1.0 - 2.0 * (rotation.y * rotation.y + rotation.z * rotation.z)) * scale.x;
    m[1][0] = (rotation.x * rotation.y + rotation.z * rotation.w) * scale.x * 2.0;
    m[2][0] = (rotation.x * rotation.z - rotation.y * rotation.w) * scale.x * 2.0;
    m[3][0] = 0.0;

    m[0][1] = (rotation.x * rotation.y - rotation.z * rotation.w) * scale.y * 2.0;
    m[1][1] = (1.0 - 2.0 * (rotation.x * rotation.x + rotation.z * rotation.z)) * scale.y;
    m[2][1] = (rotation.y * rotation.z + rotation.x * rotation.w) * scale.y * 2.0;
    m[3][1] = 0.0;

    m[0][2] = (rotation.x * rotation.z + rotation.y * rotation.w) * scale.z * 2.0;
    m[1][2] = (rotation.y * rotation.z - rotation.x * rotation.w) * scale.z * 2.0;
    m[2][2] = (1.0 - 2.0 * (rotation.x * rotation.x + rotation.y * rotation.y)) * scale.z;
    m[3][2] = 0.0;

    m[0][3] = position.x;
    m[1][3] = position.y;
    m[2][3] = position.z;
    m[3][3] = 1.0;

    return m;
}

// Sets up global properties to work properly with instancing.
inline void SetUnityMatrices(uint instanceID, inout float4x4 objectToWorld, inout float4x4 worldToObject)
{
#ifdef YARG_USE_TRANSFORM_STRUCT
    InstancedTransform transform = _InstancedTransform[instanceID];
    objectToWorld = mul(objectToWorld, TRSMatrix(transform.position.xyz, transform.rotation, transform.scale.xyz));
#else
    objectToWorld = mul(objectToWorld, _InstancedTransform[instanceID]);
#endif

    float3x3 w2oRotation;
    w2oRotation[0] = objectToWorld[1].yzx * objectToWorld[2].zxy - objectToWorld[1].zxy * objectToWorld[2].yzx;
    w2oRotation[1] = objectToWorld[0].zxy * objectToWorld[2].yzx - objectToWorld[0].yzx * objectToWorld[2].zxy;
    w2oRotation[2] = objectToWorld[0].yzx * objectToWorld[1].zxy - objectToWorld[0].zxy * objectToWorld[1].yzx;

    float det = dot(objectToWorld[0].xyz, w2oRotation[0]);
    w2oRotation = transpose(w2oRotation);
    w2oRotation *= rcp(det);
    float3 w2oPosition = mul(w2oRotation, -objectToWorld._14_24_34);

    worldToObject._11_21_31_41 = float4(w2oRotation._11_21_31, 0.0f);
    worldToObject._12_22_32_42 = float4(w2oRotation._12_22_32, 0.0f);
    worldToObject._13_23_33_43 = float4(w2oRotation._13_23_33, 0.0f);
    worldToObject._14_24_34_44 = float4(w2oPosition, 1.0f);
}
#endif

void setup()
{
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
    SetUnityMatrices(unity_InstanceID, unity_ObjectToWorld, unity_WorldToObject);
#endif
}

#endif