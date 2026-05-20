module Mibo.Elmish.Graphics2D.DefaultShaders

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
