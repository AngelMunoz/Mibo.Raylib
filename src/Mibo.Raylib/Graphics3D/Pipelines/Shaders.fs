namespace Mibo.Elmish.Graphics3D.Pipelines

open Raylib_cs

/// <summary>
/// Built-in GLSL shader generators for the reference Clustered Forward+ pipeline.
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

  let shadowPassVertex =
    """#version 330

in vec3 vertexPosition;

uniform mat4 lightMvp;

void main()
{
    gl_Position = lightMvp * vec4(vertexPosition, 1.0);
}
"""

  let shadowPassFragment =
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

  let forwardFragmentFmt (maxPointLights: int) (cascadeCount: int) =
    let cascadeArray = String.init cascadeCount (fun _ -> "")
    let shadowMapSamplers =
      if cascadeCount > 0 then
        String.concat "" [for i in 0..cascadeCount-1 -> $"uniform sampler2D shadowMap{i};"]
      else
        ""
    let shadowMatrices =
      if cascadeCount > 0 then
        String.concat "" [for i in 0..cascadeCount-1 -> $"uniform mat4 shadowMatrix{i};"]
      else
        ""
    let cascadeSplitDecl =
      if cascadeCount > 1 then
        $"uniform float cascadeSplits[{cascadeCount - 1}];"
      else
        ""

    $"""#version 330

in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragNormal;
in vec3 fragWorldPos;

out vec4 finalColor;

uniform sampler2D texture0; // albedo
uniform sampler2D texture1; // normal map (optional)

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

uniform vec3 cameraPos;
uniform float shadowBias;
uniform float normalShadowBias;

{shadowMapSamplers}
{shadowMatrices}
{cascadeSplitDecl}

vec3 getNormal()
{{
    if (useNormalMap == 0)
        return normalize(fragNormal);

    vec3 tangentNormal = texture(texture1, fragTexCoord * tiling).xyz * 2.0 - 1.0;
    return normalize(fragNormal + tangentNormal * 0.5);
}}

float sampleShadowMap(sampler2D shadowMap, vec4 shadowCoord, float bias)
{{
    vec3 projCoord = shadowCoord.xyz / shadowCoord.w;
    projCoord = projCoord * 0.5 + 0.5;

    if (projCoord.z > 1.0 || projCoord.x < 0.0 || projCoord.x > 1.0 || projCoord.y < 0.0 || projCoord.y > 1.0)
        return 1.0;

    float closestDepth = texture(shadowMap, projCoord.xy).r;
    float currentDepth = projCoord.z;
    return currentDepth - bias > closestDepth ? 0.0 : 1.0;
}}

float sampleShadowMapPCF(sampler2D shadowMap, vec4 shadowCoord, float bias)
{{
    vec3 projCoord = shadowCoord.xyz / shadowCoord.w;
    projCoord = projCoord * 0.5 + 0.5;

    if (projCoord.z > 1.0 || projCoord.x < 0.0 || projCoord.x > 1.0 || projCoord.y < 0.0 || projCoord.y > 1.0)
        return 1.0;

    float currentDepth = projCoord.z;
    vec2 texelSize = 1.0 / vec2(textureSize(shadowMap, 0));
    float shadow = 0.0;

    for (int x = -1; x <= 1; ++x)
    {{
        for (int y = -1; y <= 1; ++y)
        {{
            vec2 sampleCoord = projCoord.xy + vec2(float(x), float(y)) * texelSize;
            float pcfDepth = texture(shadowMap, sampleCoord).r;
            shadow += currentDepth - bias > pcfDepth ? 0.0 : 1.0;
        }}
    }}
    shadow /= 9.0;
    return shadow;
}}

int getCascadeIndex(vec3 worldPos)
{{
    if ({cascadeCount} <= 1) return 0;

    float viewDepth = length(cameraPos - worldPos);

    {String.concat "\n    " [
      for i in 0..(cascadeCount-2) ->
        $"if (viewDepth < cascadeSplits[{i}]) return {i};"
    ]}
    return {cascadeCount - 1};
}}

float computeDirShadow(vec3 worldPos, vec3 normal)
{{
    if (dirLightCastsShadows == 0 || {cascadeCount} == 0)
        return 1.0;

    int cascadeIdx = getCascadeIndex(worldPos);
    vec4 shadowCoord = vec4(0.0);
    float bias = shadowBias + normalShadowBias * (1.0 - max(dot(normalize(normal), -normalize(dirLightDir)), 0.0));

    {String.concat "\n    " [
      for i in 0..(cascadeCount-1) ->
        $"if (cascadeIdx == {i}) shadowCoord = shadowMatrix{i} * vec4(worldPos, 1.0);"
    ]}

    {String.concat "\n    " [
      for i in 0..(cascadeCount-1) ->
        $"if (cascadeIdx == {i}) return sampleShadowMapPCF(shadowMap{i}, shadowCoord, bias);"
    ]}

    return 1.0;
}}

void main()
{{
    vec2 uv = fragTexCoord * tiling;
    vec4 texColor = texture(texture0, uv) * albedoColor * fragColor;
    vec3 normal = getNormal();

    vec3 light = ambientColor * ambientIntensity;

    float dirShadow = computeDirShadow(fragWorldPos, normal);
    float dirDiff = max(dot(normal, -normalize(dirLightDir)), 0.0);
    light += dirLightColor * dirLightIntensity * dirDiff * dirShadow;

    int count = min(pointLightCount, {maxPointLights});
    for (int i = 0; i < count; i++)
    {{
        vec3 toLight = pointLightPos[i] - fragWorldPos;
        float dist = length(toLight);
        if (dist < pointLightRadius[i])
        {{
            vec3 L = normalize(toLight);
            float diff = max(dot(normal, L), 0.0);
            float atten = 1.0 - (dist / pointLightRadius[i]);
            light += pointLightColor[i] * diff * atten;
        }}
    }}

    vec3 result = texColor.rgb * light + emissionColor.rgb;
    float alpha = texColor.a * opacity;
    finalColor = vec4(result, alpha);
}}
"""

  /// <summary>
  /// Loads the built-in forward PBR vertex + fragment shader.
  /// The fragment shader is generated with the specified max point light array size and cascade count.
  /// </summary>
  let loadForwardShader (maxPointLights: int) (cascadeCount: int) : Shader =
    Raylib.LoadShaderFromMemory(forwardVertex, forwardFragmentFmt maxPointLights cascadeCount)

  /// <summary>Loads the shadow pass vertex + fragment shader.</summary>
  let loadShadowShader() : Shader =
    Raylib.LoadShaderFromMemory(shadowPassVertex, shadowPassFragment)

  /// <summary>Loads the built-in fullscreen post-process vertex + fragment shader.</summary>
  let loadPostProcessShader() : Shader =
    Raylib.LoadShaderFromMemory(postProcessVertex, postProcessFragment)
