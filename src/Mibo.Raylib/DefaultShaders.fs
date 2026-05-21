module Mibo.Elmish.DefaultShaders

open System.Numerics
open Raylib_cs

let sepiaTintFragment =
  """#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;
uniform sampler2D texture0;
uniform vec4 tintColor;
uniform float tintAmount;

void main()
{
    vec4 texel = texture(texture0, fragTexCoord) * fragColor;
    finalColor = mix(texel, tintColor, tintAmount);
}
"""

let loadSepiaTintShader() =
  Raylib.LoadShaderFromMemory(null, sepiaTintFragment)

// ------------------------------------------------------------------
// 3D Phong Lighting Shader
// ------------------------------------------------------------------

let phong3DVertex =
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

let phong3DFragment =
  """#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragNormal;
in vec3 fragWorldPos;

out vec4 finalColor;

uniform sampler2D texture0;

uniform vec3 ambientColor;
uniform float ambientIntensity;
uniform vec3 dirLightDir;
uniform vec3 dirLightColor;
uniform float dirLightIntensity;
uniform int pointLightCount;
uniform vec3 pointLightPos0;
uniform vec3 pointLightPos1;
uniform vec3 pointLightPos2;
uniform vec3 pointLightPos3;
uniform vec3 pointLightColor0;
uniform vec3 pointLightColor1;
uniform vec3 pointLightColor2;
uniform vec3 pointLightColor3;
uniform float pointLightRadius0;
uniform float pointLightRadius1;
uniform float pointLightRadius2;
uniform float pointLightRadius3;

void main()
{
    vec4 texColor = texture(texture0, fragTexCoord) * fragColor;
    vec3 normal = normalize(fragNormal);
    vec3 light = ambientColor * ambientIntensity;

    // Directional light (sun / moon)
    float dirDiff = max(dot(normal, -normalize(dirLightDir)), 0.0);
    light += dirLightColor * dirLightIntensity * dirDiff;

    // Point lights (torches)
    for (int i = 0; i < pointLightCount && i < 4; i++)
    {
        vec3 pos;
        vec3 col;
        float radius;

        if (i == 0) { pos = pointLightPos0; col = pointLightColor0; radius = pointLightRadius0; }
        else if (i == 1) { pos = pointLightPos1; col = pointLightColor1; radius = pointLightRadius1; }
        else if (i == 2) { pos = pointLightPos2; col = pointLightColor2; radius = pointLightRadius2; }
        else { pos = pointLightPos3; col = pointLightColor3; radius = pointLightRadius3; }

        vec3 toLight = pos - fragWorldPos;
        float dist = length(toLight);
        if (dist < radius)
        {
            vec3 L = normalize(toLight);
            float diff = max(dot(normal, L), 0.0);
            float atten = 1.0 - (dist / radius);
            light += col * diff * atten;
        }
    }

    finalColor = vec4(texColor.rgb * light, texColor.a);
}
"""

let loadPhong3DShader() =
  Raylib.LoadShaderFromMemory(phong3DVertex, phong3DFragment)
