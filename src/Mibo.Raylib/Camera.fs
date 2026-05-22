namespace Mibo.Elmish

open System.Numerics

/// <summary>
/// A universal Camera definition containing View and Projection matrices.
/// </summary>
/// <remarks>
/// This struct is renderer-agnostic - both 2D and 3D renderers use the same type.
/// Use the <see cref="T:Mibo.Elmish.Camera2D"/> or <see cref="T:Mibo.Elmish.Camera3D"/> modules to create cameras.
/// </remarks>
/// <example>
/// <code>
/// // 2D camera centered on player
/// let camera = Camera2D.create playerPos 1.0f viewportSize
///
/// // 3D camera looking at origin
/// let camera = Camera3D.lookAt position Vector3.Zero Vector3.Up fov aspect 0.1f 1000f
/// </code>
/// </example>
type Camera = {
  /// The view matrix (camera position/rotation, transforms world to view space).
  View: Matrix4x4
  /// The projection matrix (perspective/orthographic, transforms view to clip space).
  Projection: Matrix4x4
}

/// <summary>
/// Represents a 3D ray with an origin position and normalized direction.
/// </summary>
[<Struct>]
type Ray = {
  Position: Vector3
  Direction: Vector3
}

/// <summary>
/// Helper functions for 2D Cameras (Orthographic projection).
/// </summary>
/// <remarks>
/// Use these for top-down, side-scrolling, or any 2D game rendering.
/// </remarks>
module Camera2D =

  /// <summary>Calculates the visible world bounds for the camera.</summary>
  /// <param name="camera">The camera to compute bounds for.</param>
  /// <param name="width">Viewport width in pixels.</param>
  /// <param name="height">Viewport height in pixels.</param>
  /// <remarks>Useful for 2D culling (QuadTree queries, sprite visibility checks). Returns a <c>Raylib_cs.Rectangle</c> in world coordinates covering the visible area.</remarks>
  let viewportBounds (camera: Camera) (width: float32) (height: float32) : Raylib_cs.Rectangle =
    let mutable inverseView = Matrix4x4()
    Matrix4x4.Invert(camera.View, &inverseView) |> ignore
    let tl = Vector2.Transform(Vector2.Zero, inverseView)
    let br = Vector2.Transform(Vector2(width, height), inverseView)

    // Handle rotation/scale making tl/br not min/max
    let minX = min tl.X br.X
    let maxX = max tl.X br.X
    let minY = min tl.Y br.Y
    let maxY = max tl.Y br.Y

    Raylib_cs.Rectangle(minX, minY, maxX - minX, maxY - minY)

  /// <summary>
  /// Creates a standard 2D Camera centered on the position.
  /// </summary>
  /// <param name="position">Center of the camera in world units</param>
  /// <param name="zoom">Scale factor (1.0 = pixel perfect, 2.0 = 2x zoom in)</param>
  /// <param name="viewportSize">The size of the screen/viewport in pixels as Vector2(width, height)</param>
  /// <example>
  /// <code>
  /// let camera = Camera2D.create playerPosition 1.0f (Vector2(800f, 600f))
  /// </code>
  /// </example>
  let create
    (position: Vector2)
    (zoom: float32)
    (viewportSize: Vector2)
    : Camera =
    let vpW = viewportSize.X
    let vpH = viewportSize.Y

    // Transform: Translate to origin (0,0) -> Scale -> Translate to Screen Center
    let view =
      Matrix4x4.CreateTranslation(-position.X, -position.Y, 0.0f)
      * Matrix4x4.CreateScale(zoom, zoom, 1.0f)
      * Matrix4x4.CreateTranslation(vpW * 0.5f, vpH * 0.5f, 0.0f)

    let projection =
      Matrix4x4.CreateOrthographicOffCenter(0.0f, vpW, vpH, 0.0f, 0.0f, 1.0f)

    { View = view; Projection = projection }

  /// Converts a screen position (pixels) to world position using the camera.
  ///
  /// Useful for mouse picking in 2D games.
  let screenToWorld (camera: Camera) (screenPos: Vector2) : Vector2 =
    let mutable invertedView = Matrix4x4()
    Matrix4x4.Invert(camera.View, &invertedView) |> ignore
    Vector2.Transform(screenPos, invertedView)

  /// Converts a world position to screen position (pixels).
  let worldToScreen (camera: Camera) (worldPos: Vector2) : Vector2 =
    Vector2.Transform(worldPos, camera.View)


/// <summary>
/// Helper functions for 3D Cameras (Perspective projection).
/// </summary>
/// <remarks>
/// Use these for first-person, third-person, or any 3D game rendering.
/// </remarks>
module Camera3D =

  /// <summary>
  /// Creates a camera that looks at a target from a position.
  /// </summary>
  /// <param name="position">Camera position in world space</param>
  /// <param name="target">Point the camera is looking at</param>
  /// <param name="up">Up vector (typically Vector3.UnitY)</param>
  /// <param name="fov">Field of view in radians (e.g., MathF.PI / 4.0f)</param>
  /// <param name="aspectRatio">Width / Height of the viewport</param>
  /// <param name="nearPlane">Near clipping distance (objects closer are not rendered)</param>
  /// <param name="farPlane">Far clipping distance (objects farther are not rendered)</param>
  /// <example>
  /// <code>
  /// let camera = Camera3D.lookAt
  ///     (Vector3(0f, 10f, 20f))  // position
  ///     Vector3.Zero              // target
  ///     Vector3.Up                // up
  ///     (MathF.PI / 4.0f)        // 45° FOV
  ///     (16f / 9f)                // aspect ratio
  ///     0.1f                      // near plane
  ///     1000f                     // far plane
  /// </code>
  /// </example>
  let lookAt
    (position: Vector3)
    (target: Vector3)
    (up: Vector3)
    (fov: float32)
    (aspectRatio: float32)
    (nearPlane: float32)
    (farPlane: float32)
    : Camera =
    {
      View = Matrix4x4.CreateLookAt(position, target, up)
      Projection =
        Matrix4x4.CreatePerspectiveFieldOfView(
          fov,
          aspectRatio,
          nearPlane,
          farPlane
        )
    }

  /// <summary>
  /// Creates an orbiting camera using spherical coordinates.
  /// </summary>
  /// <remarks>
  /// Useful for third-person cameras, inspection views, or editor cameras.
  /// </remarks>
  /// <param name="target">Point the camera orbits around</param>
  /// <param name="yaw">Horizontal rotation angle in radians</param>
  /// <param name="pitch">Vertical rotation angle in radians</param>
  /// <param name="radius">Distance from target</param>
  /// <param name="fov">Field of view in radians</param>
  /// <param name="aspect">Aspect ratio</param>
  /// <param name="near">Near plane</param>
  /// <param name="far">Far plane</param>
  let orbit
    (target: Vector3)
    (yaw: float32)
    (pitch: float32)
    (radius: float32)
    (fov: float32)
    (aspect: float32)
    (near: float32)
    (far: float32)
    : Camera =
    let position =
      Vector3(
        radius * sin(yaw) * cos(pitch),
        radius * sin(pitch),
        radius * cos(yaw) * cos(pitch)
      )
      + target

    lookAt position target Vector3.UnitY fov aspect near far

  /// <summary>
  /// Creates a ray from screen coordinates for mouse/touch picking.
  /// </summary>
  /// <remarks>
  /// The ray originates at the camera's near plane at the screen position
  /// and points into the scene.
  /// </remarks>
  /// <param name="camera">The camera to compute the ray for.</param>
  /// <param name="screenPos">The screen position in pixels.</param>
  /// <param name="viewportWidth">Viewport width in pixels.</param>
  /// <param name="viewportHeight">Viewport height in pixels.</param>
  let screenPointToRay
    (camera: Camera)
    (screenPos: Vector2)
    (viewportWidth: float32)
    (viewportHeight: float32)
    : Ray =
    let mutable invertedViewProj = Matrix4x4()
    Matrix4x4.Invert(camera.View * camera.Projection, &invertedViewProj)
    |> ignore

    let nx = (2.0f * screenPos.X / viewportWidth) - 1.0f
    let ny = 1.0f - (2.0f * screenPos.Y / viewportHeight)

    let nearClip = Vector4(nx, ny, 0.0f, 1.0f)
    let farClip = Vector4(nx, ny, 1.0f, 1.0f)

    let nearWorld = Vector4.Transform(nearClip, invertedViewProj)
    let farWorld = Vector4.Transform(farClip, invertedViewProj)

    let nearPos =
      Vector3(nearWorld.X, nearWorld.Y, nearWorld.Z) / nearWorld.W
    let farPos =
      Vector3(farWorld.X, farWorld.Y, farWorld.Z) / farWorld.W

    let direction = Vector3.Normalize(farPos - nearPos)
    { Position = nearPos; Direction = direction }
