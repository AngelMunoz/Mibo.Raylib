#nowarn "9"

namespace Mibo.Elmish.Graphics3D

open System.Numerics
open FSharp.NativeInterop
open Raylib_cs

/// <summary>
/// Factory functions that create <see cref="T:Mibo.Elmish.Graphics3D.IRenderCommand3D"/> commands
/// for all 3D drawing operations.
/// </summary>
/// <remarks>
/// Each function returns a command that can be added to a <see cref="T:Mibo.Elmish.Graphics3D.RenderBuffer3D"/>.
/// Commands are struct-based for zero-allocation use in the hot path.
/// </remarks>
module Command3D =

  // ═══════════════════════════════════════════════════════════════════
  // Geometry Commands
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type DrawMeshCommand(mesh: Mesh, transform: Matrix4x4, material: Material3D) =
    /// <summary>The mesh to render.</summary>
    member _.Mesh = mesh
    /// <summary>The world transform matrix.</summary>
    member _.Transform = transform
    /// <summary>The material to apply.</summary>
    member _.Material = material

    interface IRenderCommand3D with
      member _.Render ctx = ctx.DrawMesh(mesh, transform, material)

  [<Struct>]
  type DrawModelCommand(model: Model, transform: Matrix4x4) =
    /// <summary>The raylib model to render. Each sub-mesh is drawn with its corresponding material.</summary>
    member _.Model = model
    /// <summary>The world transform matrix.</summary>
    member _.Transform = transform

    interface IRenderCommand3D with
      member _.Render ctx =
        for i = 0 to model.MeshCount - 1 do
          let mesh = NativePtr.get model.Meshes i
          let matIdx = NativePtr.get model.MeshMaterial i
          let mat = NativePtr.get model.Materials matIdx
          let mat3d = Material3D.fromRaylibMaterial mat
          ctx.DrawMesh(mesh, transform, mat3d)

  [<Struct>]
  type DrawBillboardCommand
    (texture: Texture2D, position: Vector3, size: Vector2, color: Color) =
    interface IRenderCommand3D with
      member _.Render ctx =
        ctx.DrawBillboard(texture, position, size, color)

  [<Struct>]
  type DrawLine3DCommand(start: Vector3, finish: Vector3, color: Color) =
    interface IRenderCommand3D with
      member _.Render ctx = ctx.DrawLine3D(start, finish, color)

  [<Struct>]
  type DrawSkinnedMeshCommand
    (mesh: Mesh, transform: Matrix4x4, material: Material3D, bones: Matrix4x4[])
    =
    /// <summary>The mesh to render.</summary>
    member _.Mesh = mesh
    /// <summary>The world transform matrix.</summary>
    member _.Transform = transform
    /// <summary>The material to apply.</summary>
    member _.Material = material
    /// <summary>The bone transform matrices.</summary>
    member _.Bones = bones

    interface IRenderCommand3D with
      member _.Render ctx =
        ctx.DrawSkinnedMesh(mesh, transform, material, bones)

  [<Struct>]
  type DrawMeshInstancedCommand
    (
      mesh: Mesh,
      transforms: Matrix4x4[],
      material: Material3D,
      instanceCount: int
    ) =
    /// <summary>The mesh to render.</summary>
    member _.Mesh = mesh
    /// <summary>Per-instance world transform matrices.</summary>
    member _.Transforms = transforms
    /// <summary>The material to apply.</summary>
    member _.Material = material
    /// <summary>Number of instances to draw.</summary>
    member _.InstanceCount = instanceCount

    interface IRenderCommand3D with
      member _.Render ctx =
        ctx.DrawMeshInstanced(mesh, transforms, material, instanceCount)

  [<Struct>]
  type DrawBillboardBatchCommand
    (
      textures: Texture2D[],
      positions: Vector3[],
      sizes: Vector2[],
      colors: Color[],
      count: int
    ) =
    /// <summary>Textures for each billboard (one per billboard).</summary>
    member _.Textures = textures
    /// <summary>World-space positions for each billboard.</summary>
    member _.Positions = positions
    /// <summary>Sizes (width, height) for each billboard.</summary>
    member _.Sizes = sizes
    /// <summary>Colors for each billboard.</summary>
    member _.Colors = colors
    /// <summary>Number of billboards to draw.</summary>
    member _.Count = count

    interface IRenderCommand3D with
      member _.Render ctx =
        ctx.DrawBillboardBatch(textures, positions, sizes, colors, count)

  let inline drawMesh
    (mesh: Mesh)
    (transform: Matrix4x4)
    (material: Material3D)
    : IRenderCommand3D =
    DrawMeshCommand(mesh, transform, material)

  let inline drawModel
    (model: Model)
    (transform: Matrix4x4)
    : IRenderCommand3D =
    DrawModelCommand(model, transform)

  let inline drawBillboard
    (texture: Texture2D)
    (position: Vector3)
    (size: Vector2)
    (color: Color)
    : IRenderCommand3D =
    DrawBillboardCommand(texture, position, size, color)

  let inline drawLine3D
    (start: Vector3)
    (finish: Vector3)
    (color: Color)
    : IRenderCommand3D =
    DrawLine3DCommand(start, finish, color)

  let inline drawSkinnedMesh
    (mesh: Mesh)
    (transform: Matrix4x4)
    (material: Material3D)
    (bones: Matrix4x4[])
    : IRenderCommand3D =
    DrawSkinnedMeshCommand(mesh, transform, material, bones)

  let inline drawMeshInstanced
    (mesh: Mesh)
    (transforms: Matrix4x4[])
    (material: Material3D)
    (instanceCount: int)
    : IRenderCommand3D =
    DrawMeshInstancedCommand(mesh, transforms, material, instanceCount)

  let inline drawBillboardBatch
    (textures: Texture2D[])
    (positions: Vector3[])
    (sizes: Vector2[])
    (colors: Color[])
    (count: int)
    : IRenderCommand3D =
    DrawBillboardBatchCommand(textures, positions, sizes, colors, count)

  // ═══════════════════════════════════════════════════════════════════
  // Camera Commands
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type BeginCameraCommand(camera: Camera3D) =
    /// <summary>The 3D camera to activate.</summary>
    member _.Camera = camera

    interface IRenderCommand3D with
      member _.Render ctx = ctx.BeginCamera camera

  [<Struct>]
  type EndCameraCommand(dummy: int) =
    interface IRenderCommand3D with
      member _.Render ctx = ctx.EndCamera()

  let inline beginCamera(camera: Camera3D) : IRenderCommand3D =
    BeginCameraCommand(camera)

  let inline endCamera() : IRenderCommand3D = EndCameraCommand(0)

  // ═══════════════════════════════════════════════════════════════════
  // Lighting Commands (Advisory)
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type SetAmbientLightCommand(light: AmbientLight3D) =
    /// <summary>The ambient light to set.</summary>
    member _.Light = light

    interface IRenderCommand3D with
      member _.Render ctx = ctx.SetAmbientLight light

  [<Struct>]
  type AddDirectionalLightCommand(light: DirectionalLight3D) =
    /// <summary>The directional light to add.</summary>
    member _.Light = light

    interface IRenderCommand3D with
      member _.Render ctx = ctx.AddDirectionalLight light

  [<Struct>]
  type AddPointLightCommand(light: PointLight3D) =
    /// <summary>The point light to add.</summary>
    member _.Light = light

    interface IRenderCommand3D with
      member _.Render ctx = ctx.AddPointLight light

  let inline setAmbientLight(light: AmbientLight3D) : IRenderCommand3D =
    SetAmbientLightCommand(light)

  let inline addDirectionalLight(light: DirectionalLight3D) : IRenderCommand3D =
    AddDirectionalLightCommand(light)

  let inline addPointLight(light: PointLight3D) : IRenderCommand3D =
    AddPointLightCommand(light)

  [<Struct>]
  type AddSpotLightCommand(light: SpotLight3D) =
    /// <summary>The spot light to add.</summary>
    member _.Light = light

    interface IRenderCommand3D with
      member _.Render ctx = ctx.AddSpotLight light

  let inline addSpotLight(light: SpotLight3D) : IRenderCommand3D =
    AddSpotLightCommand(light)

  // ═══════════════════════════════════════════════════════════════════
  // Debug Drawing Commands
  // ═══════════════════════════════════════════════════════════════════

  /// <summary>Renders a ground grid using raylib's built-in grid drawing.</summary>
  [<Struct>]
  type DrawGridCommand(slices: int, spacing: float32, color: Color) =
    /// <summary>Number of slices (divisions) in each direction.</summary>
    member _.Slices = slices
    /// <summary>Spacing between grid lines.</summary>
    member _.Spacing = spacing
    /// <summary>Grid line color.</summary>
    member _.Color = color

    interface IRenderCommand3D with
      member _.Render ctx =
        let s = slices
        let sp = spacing
        ctx.DrawImmediate(fun () -> Raylib.DrawGrid(s, sp))

  /// <summary>Renders a bounding box wireframe using raylib's built-in drawing.</summary>
  [<Struct>]
  type DrawBoundingBoxCommand(box: BoundingBox, color: Color) =
    /// <summary>The bounding box to draw.</summary>
    member _.Box = box
    /// <summary>Wireframe color.</summary>
    member _.Color = color

    interface IRenderCommand3D with
      member _.Render ctx =
        let b = box
        let c = color
        ctx.DrawImmediate(fun () -> Raylib.DrawBoundingBox(b, c))

  /// <summary>Renders a point in 3D space using raylib's built-in drawing.</summary>
  [<Struct>]
  type DrawPoint3DCommand(position: Vector3, color: Color) =
    /// <summary>World-space position of the point.</summary>
    member _.Position = position
    /// <summary>Point color.</summary>
    member _.Color = color

    interface IRenderCommand3D with
      member _.Render ctx =
        let p = position
        let c = color
        ctx.DrawImmediate(fun () -> Raylib.DrawPoint3D(p, c))

  /// <summary>Renders a ray in 3D space using raylib's built-in drawing.</summary>
  [<Struct>]
  type DrawRayCommand(ray: Ray, color: Color) =
    /// <summary>The ray to visualize.</summary>
    member _.Ray = ray
    /// <summary>Ray color.</summary>
    member _.Color = color

    interface IRenderCommand3D with
      member _.Render ctx =
        let r = ray
        let c = color
        ctx.DrawImmediate(fun () -> Raylib.DrawRay(r, c))

  let inline drawGrid (slices: int) (spacing: float32) : IRenderCommand3D =
    DrawGridCommand(slices, spacing, Color.Gray)

  let inline drawGridWithColor
    (slices: int)
    (spacing: float32)
    (color: Color)
    : IRenderCommand3D =
    DrawGridCommand(slices, spacing, color)

  let inline drawBoundingBox
    (box: BoundingBox)
    (color: Color)
    : IRenderCommand3D =
    DrawBoundingBoxCommand(box, color)

  let inline drawPoint3D (position: Vector3) (color: Color) : IRenderCommand3D =
    DrawPoint3DCommand(position, color)

  let inline drawRay (ray: Ray) (color: Color) : IRenderCommand3D =
    DrawRayCommand(ray, color)

  // ═══════════════════════════════════════════════════════════════════
  // Escape Hatches
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type DrawImmediateCommand(action: unit -> unit) =
    interface IRenderCommand3D with
      member _.Render ctx = ctx.DrawImmediate action

  let inline drawImmediate(action: unit -> unit) : IRenderCommand3D =
    DrawImmediateCommand(action)
