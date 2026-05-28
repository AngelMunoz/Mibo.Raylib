namespace Mibo.Elmish.Graphics3D.Pipelines

open Raylib_cs

/// <summary>
/// Built-in GLSL shader generators for the Forward PBR pipeline.
/// </summary>
/// <remarks>
/// Fragment shaders use uniform arrays for point lights. The array size is
/// determined at pipeline creation time (default 8) because GLSL requires
/// compile-time constants for array declarations.
/// </remarks>
module Shaders =

  let forwardVertex =
    """#version 330

in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

out vec2 fragTexCoord;
out vec4 fragColor;
out vec3 fragNormal;
out vec3 fragWorldPos;

uniform mat4 mvp;
uniform mat4 matModel;

void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    fragNormal = mat3(transpose(inverse(matModel))) * vertexNormal;
    fragWorldPos = (matModel * vec4(vertexPosition, 1.0)).xyz;
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
"""

  let depthShadowVertex =
    """#version 330

in vec3 vertexPosition;
in vec3 vertexNormal;
in vec2 vertexTexCoord;
in vec4 vertexColor;

uniform mat4 mvp;
uniform mat4 matModel;

out vec3 fragPosition;
out vec2 fragTexCoord;
out vec4 fragColor;
out vec3 fragNormal;

void main()
{
    fragPosition = vec3(matModel * vec4(vertexPosition, 1.0));
    fragTexCoord = vertexTexCoord;
    fragColor    = vertexColor;
    fragNormal   = normalize(vec3(mat3(transpose(inverse(matModel))) * vertexNormal));
    gl_Position  = mvp * vec4(vertexPosition, 1.0);
}
"""

  let depthShadowFragment =
    """#version 330

out vec4 finalColor;

void main()
{
    finalColor = vec4(1.0);
}
"""

  let postProcessVertex =
    """#version 330

in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec4 vertexColor;

out vec2 fragTexCoord;

uniform mat4 mvp;

void main()
{
    fragTexCoord = vertexTexCoord;
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
"""

  let postProcessFragment =
    """#version 330

in vec2 fragTexCoord;
out vec4 finalColor;

uniform sampler2D texture0;

void main()
{
    finalColor = texture(texture0, fragTexCoord);
}
"""

  let forwardFragmentFmt
    (maxPointLights: int)
    (maxSpotLights: int)
    =
    $"""#version 330

const float PI = 3.14159265359;

in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragNormal;
in vec3 fragWorldPos;

out vec4 finalColor;

uniform sampler2D texture0; // albedo
uniform sampler2D texture1; // metalness
uniform sampler2D texture2; // normal map
uniform sampler2D texture3; // roughness
uniform sampler2D texture4; // emission

uniform vec4 albedoColor;
uniform float roughness;
uniform float metallic;
uniform vec4 emissionColor;
uniform float opacity;
uniform vec2 tiling;
uniform int useNormalMap;

uniform vec3 ambientColor;
uniform float ambientIntensity;

uniform vec3 dirLightDir;
uniform vec3 dirLightColor;
uniform float dirLightIntensity;
uniform int dirLightCastsShadows;

uniform int pointLightCount;
uniform vec3 pointLightPos[{maxPointLights}];
uniform vec3 pointLightColor[{maxPointLights}];
uniform float pointLightRadius[{maxPointLights}];

uniform int spotLightCount;
uniform vec3 spotLightPos[{maxSpotLights}];
uniform vec3 spotLightDir[{maxSpotLights}];
uniform vec3 spotLightColor[{maxSpotLights}];
uniform float spotLightIntensity[{maxSpotLights}];
uniform float spotLightRadius[{maxSpotLights}];
uniform float spotLightInnerCutoff[{maxSpotLights}];
uniform float spotLightOuterCutoff[{maxSpotLights}];

uniform vec3 cameraPos;
uniform sampler2D shadowMap;
uniform mat4 lightViewProj;
uniform int shadowPass;

vec3 getNormal()
{{
    if (useNormalMap == 0)
        return normalize(fragNormal);

    vec3 tangentNormal = texture(texture2, fragTexCoord * tiling).xyz * 2.0 - 1.0;
    vec3 N = normalize(fragNormal);
    vec3 T = normalize(cross(N, vec3(0.0, 1.0, 0.0)));
    if (length(cross(N, vec3(0.0, 1.0, 0.0))) < 0.001)
        T = normalize(cross(N, vec3(1.0, 0.0, 0.0)));
    vec3 B = cross(N, T);
    mat3 TBN = mat3(T, B, N);
    return normalize(TBN * tangentNormal);
}}

float distributionGGX(vec3 N, vec3 H, float r)
{{
    float a = r * r;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    float denom = NdotH2 * (a2 - 1.0) + 1.0;
    return a2 / max(PI * denom * denom, 0.0001);
}}

float geometrySchlickGGX(float NdotV, float r)
{{
    float k = ((r + 1.0) * (r + 1.0)) / 8.0;
    return NdotV / max(NdotV * (1.0 - k) + k, 0.0001);
}}

float geometrySmith(vec3 N, vec3 V, vec3 L, float r)
{{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    return geometrySchlickGGX(NdotV, r) * geometrySchlickGGX(NdotL, r);
}}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{{
    return F0 + (1.0 - F0) * pow(max(1.0 - cosTheta, 0.0), 5.0);
}}

float computeDirShadow(vec3 worldPos)
{{
    if (dirLightCastsShadows == 0)
        return 1.0;

    vec4 shadowCoord = lightViewProj * vec4(worldPos, 1.0);
    vec3 projCoord = shadowCoord.xyz / shadowCoord.w;
    projCoord = projCoord * 0.5 + 0.5;

    if (projCoord.z > 1.0) return 0.0;
    if (projCoord.x < 0.0 || projCoord.x > 1.0 || projCoord.y < 0.0 || projCoord.y > 1.0)
        return 0.0;

    float bias = max(0.0005 * (1.0 - dot(normalize(fragNormal), normalize(-dirLightDir))), 0.0001);
    float shadow = 0.0;
    vec2 texel = 1.0 / vec2(textureSize(shadowMap, 0));
    for (int x = -1; x <= 1; x++) {{
        for (int y = -1; y <= 1; y++) {{
            float d = texture(shadowMap, projCoord.xy + vec2(float(x), float(y)) * texel).r;
            shadow += (projCoord.z - bias > d) ? 0.0 : 1.0;
        }}
    }}
    return shadow / 9.0;
}}

vec3 calcPBR(vec3 V, vec3 N, vec3 L, vec3 radiance, vec3 albedo, float r, float m)
{{
    vec3 H = normalize(V + L);

    vec3 F0 = mix(vec3(0.04), albedo, m);
    float D = distributionGGX(N, H, r);
    float G = geometrySmith(N, V, L, r);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 num = D * G * F;
    float denom = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0);
    vec3 spec = num / max(denom, 0.0001);

    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - m);
    float NdotL = max(dot(N, L), 0.0);

    return (kD * albedo / PI + spec) * radiance * NdotL;
}}

void main()
{{
    if (shadowPass == 1) {{ finalColor = vec4(1.0); return; }}

    vec2 uv = fragTexCoord * tiling;
    vec4 texColor = texture(texture0, uv) * albedoColor * fragColor;
    vec3 albedo = texColor.rgb;
    vec3 normal = getNormal();

    float r = texture(texture3, uv).r * roughness + (1.0 - texture(texture3, uv).r) * roughness;
    r = clamp(r, 0.04, 1.0);
    float m = texture(texture1, uv).r * metallic + (1.0 - texture(texture1, uv).r) * metallic;
    m = clamp(m, 0.0, 1.0);

    vec3 V = normalize(cameraPos - fragWorldPos);

    // Ambient
    vec3 ambient = ambientColor * albedo * ambientIntensity;

    // Directional light (PBR)
    float dirShadow = computeDirShadow(fragWorldPos);
    vec3 L = normalize(-dirLightDir);
    vec3 radiance = dirLightColor * dirLightIntensity;
    vec3 dirResult = calcPBR(V, normal, L, radiance, albedo, r, m) * dirShadow;

    // Point lights (PBR)
    vec3 pointResult = vec3(0.0);
    int count = min(pointLightCount, {maxPointLights});
    for (int i = 0; i < count; i++)
    {{
        vec3 toLight = pointLightPos[i] - fragWorldPos;
        float dist = length(toLight);
        if (dist < pointLightRadius[i])
        {{
            vec3 pL = normalize(toLight);
            float atten = 1.0 - (dist / pointLightRadius[i]);
            vec3 pRadiance = pointLightColor[i] * atten;
            pointResult += calcPBR(V, normal, pL, pRadiance, albedo, r, m);
        }}
    }}

    // Spot lights (PBR)
    vec3 spotResult = vec3(0.0);
    int sCount = min(spotLightCount, {maxSpotLights});
    for (int i = 0; i < sCount; i++)
    {{
        vec3 toLight = spotLightPos[i] - fragWorldPos;
        float dist = length(toLight);
        if (dist < spotLightRadius[i])
        {{
            vec3 sL = normalize(toLight);
            float theta = dot(sL, normalize(-spotLightDir[i]));
            float epsilon = spotLightInnerCutoff[i] - spotLightOuterCutoff[i];
            float intensity = clamp((theta - spotLightOuterCutoff[i]) / max(epsilon, 0.0001), 0.0, 1.0);
            float distAtten = 1.0 - (dist / spotLightRadius[i]);
            vec3 sRadiance = spotLightColor[i] * spotLightIntensity[i] * intensity * distAtten;
            spotResult += calcPBR(V, normal, sL, sRadiance, albedo, r, m);
        }}
    }}

    vec3 emission = emissionColor.rgb;
    // Emission map modulation
    vec4 emTex = texture(texture4, uv);
    emission *= emTex.rgb;

    vec3 result = ambient + dirResult + pointResult + spotResult + emission;
    float alpha = texColor.a * opacity;
    finalColor = vec4(result, alpha);
}}
"""

  /// <summary>
  /// Loads the built-in forward PBR vertex + fragment shader.
  /// The fragment shader is generated with the specified light array sizes.
  /// </summary>
  let loadForwardShader
    (maxPointLights: int)
    (maxSpotLights: int)
    : Shader =
    Raylib.LoadShaderFromMemory(
      forwardVertex,
      forwardFragmentFmt maxPointLights maxSpotLights
    )

  /// <summary>Loads the depth-only shadow pass vertex + fragment shader (C example compatible).</summary>
  let loadDepthShadowShader() : Shader =
    Raylib.LoadShaderFromMemory(depthShadowVertex, depthShadowFragment)

  /// <summary>Loads the built-in fullscreen post-process vertex + fragment shader.</summary>
  let loadPostProcessShader() : Shader =
    Raylib.LoadShaderFromMemory(postProcessVertex, postProcessFragment)
