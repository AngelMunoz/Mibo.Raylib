open Raylib_cs
open System
open System.Numerics
open System.Runtime.CompilerServices
open FSharp.NativeInterop

#nowarn "9"
#nowarn "3391"

[<Extension>]
type CBoolExtensions =
  [<Extension>]
  static member inline AsBool(c: CBool) : bool = CBool.op_Implicit(c)

// =============================================================================
// NativePtr helpers — void* with DisableRuntimeMarshalling requires explicit
// fixed + NativePtr.toVoidPtr.  Keep them here so they don't pollute the flow.
// =============================================================================
[<AutoOpen>]
module NativeHelpers =

  let setShaderInt (shader: Shader) (loc: int) (value: int) =
    use p = fixed &value

    Raylib.SetShaderValue(
      shader,
      loc,
      NativePtr.toVoidPtr p,
      ShaderUniformDataType.Int
    )

  let setShaderFloat (shader: Shader) (loc: int) (value: float32) =
    use p = fixed &value

    Raylib.SetShaderValue(
      shader,
      loc,
      NativePtr.toVoidPtr p,
      ShaderUniformDataType.Float
    )

  let setShaderVec3 (shader: Shader) (loc: int) (v: Vector3) =
    use p = fixed &v

    Raylib.SetShaderValue(
      shader,
      loc,
      NativePtr.toVoidPtr p,
      ShaderUniformDataType.Vec3
    )

  let setShaderVec4 (shader: Shader) (loc: int) (v: Vector4) =
    use p = fixed &v

    Raylib.SetShaderValue(
      shader,
      loc,
      NativePtr.toVoidPtr p,
      ShaderUniformDataType.Vec4
    )

  let setShaderIntArray (shader: Shader) (loc: int) (arr: int array) =
    use p = fixed &arr.[0]

    Raylib.SetShaderValue(
      shader,
      loc,
      NativePtr.toVoidPtr p,
      ShaderUniformDataType.Int
    )

  let rlSetUniformInt (loc: int) (value: int) =
    use p = fixed &value

    Rlgl.SetUniform(
      loc,
      NativePtr.toVoidPtr p,
      int ShaderUniformDataType.Int,
      1
    )

// =============================================================================
// Constants — same as C
// =============================================================================
let MAX_LIGHTS = 16
let LIGHT_DIRECTIONAL = 0
let LIGHT_POINT = 1
let LIGHT_SPOT = 2
let SUN_SHADOW_RES = 2048
let SPOT_SHADOW_RES = 1024
let DEG2RAD = MathF.PI / 180.0f
let RAD2DEG = 180.0f / MathF.PI

// =============================================================================
// Shader source — verbatim copy of the C files
// =============================================================================
let lightingVS =
  """#version 330

in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

uniform mat4 mvp;
uniform mat4 matModel;
uniform mat4 matNormal;

out vec3 fragPosition;
out vec2 fragTexCoord;
out vec4 fragColor;
out vec3 fragNormal;

void main()
{
    fragPosition = vec3(matModel*vec4(vertexPosition, 1.0));
    fragTexCoord = vertexTexCoord;
    fragColor    = vertexColor;
    fragNormal   = normalize(vec3(matNormal*vec4(vertexNormal, 1.0)));
    gl_Position  = mvp*vec4(vertexPosition, 1.0);
}"""

let lightingFS =
  """#version 330

// Input vertex attributes (from vertex shader)
in vec3 fragPosition;
in vec2 fragTexCoord;
in vec4 fragColor;
in vec3 fragNormal;

// Input uniform values
uniform sampler2D texture0;
uniform vec4 colDiffuse;

// Output fragment color
out vec4 finalColor;

// NOTE: Add your custom variables here

#define     MAX_LIGHTS              16
#define     LIGHT_DIRECTIONAL       0
#define     LIGHT_POINT             1
#define     LIGHT_SPOT              2

struct Light {
    int enabled;
    int type;
    vec3 position;
    vec3 target;
    vec4 color;
    float attenuation;
    float innerCutoff;
    float outerCutoff;
};

// Input lighting values
uniform Light lights[MAX_LIGHTS];
uniform vec4 ambient;
uniform vec3 viewPos;

// Shadow mapping for the sun light
uniform sampler2D shadowMap;
uniform mat4 lightVP;
uniform int shadowMapResolution;
uniform int shadowPass;

// Spot light shadow mapping
uniform sampler2D spotShadowMap;
uniform mat4 spotLightVP;
uniform int spotShadowMapResolution;

float ShadowCalc(vec3 worldPos, vec3 normal, vec3 lightDir, sampler2D smap, mat4 vp, int res)
{
    vec4 lp = vp * vec4(worldPos, 1.0);
    vec3 proj = lp.xyz / lp.w;
    proj = proj * 0.5 + 0.5;
    if (proj.z > 1.0) return 0.0;
    if (proj.x < 0.0 || proj.x > 1.0 || proj.y < 0.0 || proj.y > 1.0) return 0.0;

    float bias = max(0.0005 * (1.0 - dot(normal, lightDir)), 0.0001);
    float shadow = 0.0;
    vec2 texel = 1.0 / vec2(res);
    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            float d = texture(smap, proj.xy + vec2(x,y) * texel).r;
            shadow += (proj.z - bias > d) ? 1.0 : 0.0;
        }
    }
    return shadow / 9.0;
}


void main()
{
    if (shadowPass == 1) { finalColor = vec4(1.0); return; }
    // Texel color fetching from texture sampler
    vec4 texelColor = texture(texture0, fragTexCoord);
    vec3 lightDot = vec3(0.0);
    vec3 normal = normalize(fragNormal);
    vec3 viewD = normalize(viewPos - fragPosition);
    vec3 specular = vec3(0.0);

    vec4 tint = colDiffuse*fragColor;

    // NOTE: Implement here your fragment shader code

    float sunShadow  = 0.0;
    float spotShadow = 0.0;

    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        vec3 light = vec3(0.0);
        float attenuation = 1.0;
        
        if (lights[i].enabled == 1)
        {

            if (lights[i].type == LIGHT_DIRECTIONAL)
            {
                light = -normalize(lights[i].target - lights[i].position);
                sunShadow = ShadowCalc(fragPosition, normal, light, shadowMap, lightVP, shadowMapResolution);
            }


            if (lights[i].type == LIGHT_POINT)
            {
                vec3 toLight = lights[i].position - fragPosition;
                float dist = length(toLight);
                light = normalize(toLight);
                attenuation = 1.0 / (1.0 + lights[i].attenuation * dist * dist);

            }

            if (lights[i].type == LIGHT_SPOT){
                vec3 toLight = lights[i].position - fragPosition;
                float dist = length(toLight);
                light = normalize(toLight);

                attenuation = 1.0 / (1.0 + lights[i].attenuation * dist * dist);

                vec3 coneAxis = normalize(lights[i].target - lights[i].position);
                float theta = dot(-light, coneAxis);

                float epsilon = lights[i].innerCutoff - lights[i].outerCutoff;
                float coneFactor = clamp((theta - lights[i].outerCutoff)/epsilon, 0.0, 1.0);
                attenuation *= coneFactor;

                spotShadow = ShadowCalc(fragPosition, normal, light, spotShadowMap, spotLightVP, spotShadowMapResolution);
            }

            float NdotL = max(dot(normal, light), 0.0);
            lightDot += lights[i].color.rgb*NdotL*attenuation;


            float specCo = 0.0;
            //if (NdotL > 0.0) specCo = pow(max(0.0, dot(viewD, reflect(-(light), normal))), 16.0); // 16 refers to shine
            if (NdotL > 0.0) specCo = pow(max(0.0, dot(viewD, reflect(-(light), normal))), 64.0) * 0.3;

            specular += specCo;
        }
    }

    // Combine per-light shadows into one so objects cast a single shadow
    float shadow = max(sunShadow, spotShadow);
    lightDot *= (1.0 - shadow);

    finalColor = (texelColor*((tint + vec4(specular, 1.0))*vec4(lightDot, 1.0)));
    finalColor += texelColor*(ambient/10.0)*tint;

    // Gamma correction
    finalColor = pow(finalColor, vec4(1.0/2.2));
}"""

// =============================================================================
// Light type — mirrors the GLSL struct
// =============================================================================
type Light() =
  member val Type = 0 with get, set
  member val Enabled = 0 with get, set
  member val Position = Vector3.Zero with get, set
  member val Target = Vector3.Zero with get, set
  member val Color = Color.White with get, set
  member val Attenuation = 0.0f with get, set
  member val InnerCutoff = 0.0f with get, set
  member val OuterCutoff = 0.0f with get, set
  member val EnabledLoc = 0 with get, set
  member val TypeLoc = 0 with get, set
  member val PositionLoc = 0 with get, set
  member val TargetLoc = 0 with get, set
  member val ColorLoc = 0 with get, set
  member val AttenuationLoc = 0 with get, set
  member val InnerCutoffLoc = 0 with get, set
  member val OuterCutoffLoc = 0 with get, set

// =============================================================================
// ShadowCaster — bundles depth target + camera + shader bindings
// =============================================================================
type ShadowCaster() =
  member val Camera = Camera3D() with get, set
  member val Target = Unchecked.defaultof<RenderTexture2D> with get, set
  member val VpLoc = 0 with get, set
  member val MapLoc = 0 with get, set
  member val Slot = 0 with get, set

// =============================================================================
// Helpers — 1:1 mapping of C static functions
// =============================================================================
let mutable private lightsCount = 0

let createLight
  (lightType: int)
  (position: Vector3)
  (target: Vector3)
  (color: Color)
  (attenuation: float32)
  (shader: Shader)
  =
  let light = Light()

  if lightsCount >= MAX_LIGHTS then
    light
  else
    light.Enabled <- 1
    light.Type <- lightType
    light.Position <- position
    light.Target <- target
    light.Color <- color
    light.Attenuation <- attenuation
    light.InnerCutoff <- -1.0f
    light.OuterCutoff <- -1.0f

    let i = lightsCount

    light.EnabledLoc <-
      Raylib.GetShaderLocation(shader, sprintf "lights[%d].enabled" i)

    light.TypeLoc <-
      Raylib.GetShaderLocation(shader, sprintf "lights[%d].type" i)

    light.PositionLoc <-
      Raylib.GetShaderLocation(shader, sprintf "lights[%d].position" i)

    light.TargetLoc <-
      Raylib.GetShaderLocation(shader, sprintf "lights[%d].target" i)

    light.ColorLoc <-
      Raylib.GetShaderLocation(shader, sprintf "lights[%d].color" i)

    light.AttenuationLoc <-
      Raylib.GetShaderLocation(shader, sprintf "lights[%d].attenuation" i)

    light.InnerCutoffLoc <-
      Raylib.GetShaderLocation(shader, sprintf "lights[%d].innerCutoff" i)

    light.OuterCutoffLoc <-
      Raylib.GetShaderLocation(shader, sprintf "lights[%d].outerCutoff" i)

    lightsCount <- lightsCount + 1
    light

let updateLightValues (shader: Shader) (light: Light) =
  setShaderInt shader light.EnabledLoc light.Enabled
  setShaderInt shader light.TypeLoc light.Type
  setShaderVec3 shader light.PositionLoc light.Position
  setShaderVec3 shader light.TargetLoc light.Target

  setShaderVec4
    shader
    light.ColorLoc
    (Vector4(
      float32 light.Color.R / 255.0f,
      float32 light.Color.G / 255.0f,
      float32 light.Color.B / 255.0f,
      float32 light.Color.A / 255.0f
    ))

  setShaderFloat shader light.AttenuationLoc light.Attenuation
  setShaderFloat shader light.InnerCutoffLoc light.InnerCutoff
  setShaderFloat shader light.OuterCutoffLoc light.OuterCutoff

let loadShadowmapRenderTexture (width: int) (height: int) =
  let t = ref(RenderTexture2D())
  t.Value <- RenderTexture2D(Id = Rlgl.LoadFramebuffer())

  t.Value <-
    RenderTexture2D(
      Id = t.Value.Id,
      Texture = Texture2D(Width = width, Height = height)
    )

  Rlgl.EnableFramebuffer(t.Value.Id)
  let depthId = Rlgl.LoadTextureDepth(width, height, false)

  t.Value <-
    RenderTexture2D(
      Id = t.Value.Id,
      Texture = t.Value.Texture,
      Depth =
        Texture2D(
          Id = depthId,
          Width = width,
          Height = height,
          Format = enum<PixelFormat> 19,
          Mipmaps = 1
        )
    )

  Rlgl.FramebufferAttach(
    t.Value.Id,
    depthId,
    FramebufferAttachType.Depth,
    FramebufferAttachTextureType.Texture2D,
    0
  )

  Rlgl.DisableFramebuffer()
  t.Value

let unloadShadowmapRenderTexture(t: RenderTexture2D) =
  Rlgl.UnloadTexture(t.Depth.Id)
  Rlgl.UnloadFramebuffer(t.Id)

let createShadowCaster
  (shader: Shader)
  (resolution: int)
  (vpName: string)
  (mapName: string)
  (resName: string)
  (slot: int)
  (projection: CameraProjection)
  (fovy: float32)
  =
  let sc = ShadowCaster()
  sc.Target <- loadShadowmapRenderTexture resolution resolution
  sc.VpLoc <- Raylib.GetShaderLocation(shader, vpName)
  sc.MapLoc <- Raylib.GetShaderLocation(shader, mapName)
  sc.Slot <- slot
  setShaderInt shader (Raylib.GetShaderLocation(shader, resName)) resolution

  sc.Camera <-
    Camera3D(Up = Vector3.UnitY, FovY = fovy, Projection = projection)

  sc

let drawScene (plane: Model) (cube: Model) (cubePositions: Vector3[]) =
  Raylib.DrawModel(plane, Vector3.Zero, 1.0f, Color.LightGray)

  for i in 0 .. cubePositions.Length - 1 do
    Raylib.DrawModel(cube, cubePositions.[i], 1.0f, Color.Red)

let renderShadowPass
  (sc: ShadowCaster)
  (shader: Shader)
  (plane: Model)
  (cube: Model)
  (cubePositions: Vector3[])
  =
  Raylib.BeginTextureMode(sc.Target)
  Raylib.ClearBackground(Color.White)
  Raylib.BeginMode3D(sc.Camera)

  let vp =
    Raymath.MatrixMultiply(
      Rlgl.GetMatrixModelview(),
      Rlgl.GetMatrixProjection()
    )

  Raylib.SetShaderValueMatrix(shader, sc.VpLoc, vp)
  drawScene plane cube cubePositions
  Raylib.EndMode3D()
  Raylib.EndTextureMode()

let bindShadowMap(sc: ShadowCaster) =
  Rlgl.ActiveTextureSlot(sc.Slot)
  Rlgl.EnableTexture(sc.Target.Depth.Id)
  rlSetUniformInt sc.MapLoc sc.Slot

// =============================================================================
// Main — line-by-line translation of C main()
// =============================================================================
[<EntryPoint>]
let main _ =
  let screenWidth = 800
  let screenHeight = 400

  Raylib.InitWindow(
    screenWidth,
    screenHeight,
    "raylib - shadow mapping (sun + spot)"
  )
  |> ignore

  Raylib.DisableCursor()

  // ---- Camera ----
  let mutable camera =
    Camera3D(
      Position = Vector3(10.0f, 10.0f, 10.0f),
      Target = Vector3(0.0f, 1.0f, 0.0f),
      Up = Vector3.UnitY,
      FovY = 60.0f,
      Projection = CameraProjection.Perspective
    )

  // ---- Shader ----
  let mutable shader = Raylib.LoadShaderFromMemory(lightingVS, lightingFS)

  // shader.locs[SHADER_LOC_VECTOR_VIEW] = GetShaderLocation(shader, "viewPos")
  let locViewPos = Raylib.GetShaderLocation(shader, "viewPos")
  NativePtr.set shader.Locs (int ShaderLocationIndex.VectorView) locViewPos

  // float ambient[4] = { 0.1f, 0.1f, 0.1f, 1.0f }
  // SetShaderValue(shader, GetShaderLocation(shader, "ambient"), ambient, SHADER_UNIFORM_VEC4)
  let ambient = Vector4(0.1f, 0.1f, 0.1f, 1.0f)
  setShaderVec4 shader (Raylib.GetShaderLocation(shader, "ambient")) ambient

  let shadowPassLoc = Raylib.GetShaderLocation(shader, "shadowPass")

  // ---- Lights ----
  let lights = Array.init MAX_LIGHTS (fun _ -> Light())

  // 0: directional sun
  let mutable sunDir = Raymath.Vector3Normalize(Vector3(-1.0f, -1.0f, -0.5f))

  lights.[0] <-
    createLight LIGHT_DIRECTIONAL Vector3.Zero sunDir Color.White 0.0f shader

  updateLightValues shader lights.[0]

  // 1: spot light shining down
  let spotPos = Vector3(10.0f, 10.0f, 5.0f)
  let spotDir = Vector3(0.0f, -1.0f, 0.0f)

  let spotTarget =
    Raymath.Vector3Add(spotPos, Raymath.Vector3Normalize(spotDir))

  lights.[1] <-
    createLight
      LIGHT_SPOT
      spotPos
      spotTarget
      Color.White
      (1.0f / (30.0f * 30.0f))
      shader

  lights.[1].InnerCutoff <- MathF.Cos(30.0f * DEG2RAD)
  lights.[1].OuterCutoff <- MathF.Cos(45.0f * DEG2RAD)
  updateLightValues shader lights.[1]

  // ---- Shadow casters ----
  let sun =
    createShadowCaster
      shader
      SUN_SHADOW_RES
      "lightVP"
      "shadowMap"
      "shadowMapResolution"
      10
      CameraProjection.Orthographic
      30.0f

  let spot =
    createShadowCaster
      shader
      SPOT_SHADOW_RES
      "spotLightVP"
      "spotShadowMap"
      "spotShadowMapResolution"
      11
      CameraProjection.Perspective
      (MathF.Acos(lights.[1].OuterCutoff) * RAD2DEG * 2.0f)

  spot.Camera <-
    Camera3D(
      Position = lights.[1].Position,
      Target = lights.[1].Target,
      Up = Vector3(1.0f, 0.0f, 0.0f),
      FovY = spot.Camera.FovY,
      Projection = spot.Camera.Projection
    )

  // ---- Scene ----
  let mutable plane =
    Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(20.0f, 20.0f, 1, 1))

  let mutable cube =
    Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1.0f, 2.0f, 1.0f))

  // plane.materials[0].shader = shader
  let pmPtr = NativePtr.add plane.Materials 0
  let mutable pm = NativePtr.read pmPtr
  pm.Shader <- shader
  NativePtr.write pmPtr pm

  // cube.materials[0].shader = shader
  let cmPtr = NativePtr.add cube.Materials 0
  let mutable cm = NativePtr.read cmPtr
  cm.Shader <- shader
  NativePtr.write cmPtr cm

  let cubePositions = [|
    Vector3(-3.0f, 1.0f, 0.0f)
    Vector3(0.0f, 1.0f, -2.0f)
    Vector3(3.0f, 1.0f, 1.0f)
    Vector3(1.0f, 1.0f, 3.0f)
  |]

  Raylib.SetTargetFPS(60)

  // ---- Game loop ----
  let mutable running = true

  while running do
    if Raylib.WindowShouldClose().AsBool() then
      running <- false

    // ---- Update ----
    use camPtr = fixed &camera
    Raylib.UpdateCamera(camPtr, CameraMode.Free)

    let t = Raylib.GetTime() |> float32

    // Rotate the sun direction
    sunDir <-
      Raymath.Vector3Normalize(
        Vector3(MathF.Cos(t * 0.3f), -1.0f, MathF.Sin(t * 0.3f))
      )

    lights.[0].Target <- sunDir
    updateLightValues shader lights.[0]

    // Orbit the spot light
    lights.[1].Position <-
      Vector3(MathF.Cos(t) * 8.0f, 10.0f, MathF.Sin(t) * 8.0f)

    lights.[1].Target <-
      Raymath.Vector3Add(lights.[1].Position, Vector3(0.0f, -1.0f, 0.0f))

    updateLightValues shader lights.[1]

    spot.Camera <-
      Camera3D(
        Position = lights.[1].Position,
        Target = lights.[1].Target,
        Up = spot.Camera.Up,
        FovY = spot.Camera.FovY,
        Projection = spot.Camera.Projection
      )

    // Push view position
    let viewPos = camera.Position

    setShaderVec3
      shader
      (NativePtr.get shader.Locs (int ShaderLocationIndex.VectorView))
      viewPos

    // Sun shadow camera follows the player
    sun.Camera <-
      Camera3D(
        Position =
          Raymath.Vector3Add(
            camera.Position,
            Raymath.Vector3Scale(sunDir, -30.0f)
          ),
        Target = camera.Position,
        Up = sun.Camera.Up,
        FovY = sun.Camera.FovY,
        Projection = sun.Camera.Projection
      )

    // ---- Shadow passes ----
    setShaderInt shader shadowPassLoc 1

    renderShadowPass sun shader plane cube cubePositions
    renderShadowPass spot shader plane cube cubePositions

    // ---- Bind shadow maps for the main pass ----
    setShaderInt shader shadowPassLoc 0

    Rlgl.EnableShader(shader.Id)
    bindShadowMap sun
    bindShadowMap spot

    // ---- Main camera (the visible frame) ----
    Raylib.BeginDrawing()
    Raylib.ClearBackground(Color.RayWhite)
    Raylib.BeginMode3D(camera)
    drawScene plane cube cubePositions
    Raylib.EndMode3D()

    Raylib.DrawText(
      "Shadow mapping: directional sun + spot light",
      10,
      10,
      18,
      Color.Black
    )

    Raylib.DrawFPS(10, 35)
    Raylib.EndDrawing()

  // ---- Cleanup ----
  Raylib.UnloadModel(plane)
  Raylib.UnloadModel(cube)
  Raylib.UnloadShader(shader)
  unloadShadowmapRenderTexture sun.Target
  unloadShadowmapRenderTexture spot.Target
  Raylib.CloseWindow()

  0
