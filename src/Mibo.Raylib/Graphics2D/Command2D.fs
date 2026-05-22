namespace Mibo.Elmish.Graphics2D

open System
open System.Numerics
open Raylib_cs

/// <summary>
/// Factory functions that create <see cref="T:Mibo.Elmish.Graphics2D.IRenderCommand2D"/> commands
/// for all raylib 2D drawing operations.
/// </summary>
/// <remarks>
/// Each function returns a command that can be added to a <see cref="T:Mibo.Elmish.Graphics2D.RenderBuffer2D"/>.
/// Styling parameters (layer, color, thickness) are grouped for partial application —
/// bind them once and reuse across multiple draws.
/// </remarks>
module Command2D =

  /// <summary>State required to render a 2D sprite via DrawTexturePro.</summary>
  [<Struct>]
  type SpriteState = {
    Texture: Texture2D
    Dest: Rectangle
    Source: Rectangle
    Origin: Vector2
    Rotation: float32
    Color: Color
    Layer: int<RenderLayer>
  }

  /// <summary>State required to render 2D text via raylib's DrawTextEx.</summary>
  [<Struct>]
  type TextState = {
    Font: Font
    Text: string
    Position: Vector2
    FontSize: float32
    Spacing: float32
    Color: Color
    Layer: int<RenderLayer>
  }

  // ═══════════════════════════════════════════════════════════════════
  // Sprite & Text
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type SpriteCommand(s: SpriteState) =
    interface IRenderCommand2D with
      member _.Layer = s.Layer
      member _.Render _ =
        Raylib.DrawTexturePro(s.Texture, s.Source, s.Dest, s.Origin, s.Rotation, s.Color)

  [<Struct>]
  type TextCommand(t: TextState) =
    interface IRenderCommand2D with
      member _.Layer = t.Layer
      member _.Render _ =
        Raylib.DrawTextEx(t.Font, t.Text, t.Position, t.FontSize, t.Spacing, t.Color)

  let inline sprite (state: SpriteState) : IRenderCommand2D = SpriteCommand(state)
  let inline text (state: TextState) : IRenderCommand2D = TextCommand(state)

  // ═══════════════════════════════════════════════════════════════════
  // Rectangles
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type FillRectCommand(rect: Rectangle, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawRectangleRec(rect, color)

  [<Struct>]
  type RectOutlineCommand(rect: Rectangle, thickness: float32, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawRectangleLinesEx(rect, thickness, color)

  [<Struct>]
  type FillRectRoundedCommand(rect: Rectangle, roundness: float32, segments: int, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawRectangleRounded(rect, roundness, segments, color)

  [<Struct>]
  type RectRoundedOutlineCommand(rect: Rectangle, roundness: float32, segments: int, thickness: float32, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawRectangleRoundedLinesEx(rect, roundness, segments, thickness, color)

  [<Struct>]
  type RectGradientVCommand(x: int, y: int, w: int, h: int, top: Color, bottom: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawRectangleGradientV(x, y, w, h, top, bottom)

  [<Struct>]
  type RectGradientHCommand(x: int, y: int, w: int, h: int, left: Color, right: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawRectangleGradientH(x, y, w, h, left, right)

  [<Struct>]
  type RectGradientCommand(rect: Rectangle, tl: Color, bl: Color, tr: Color, br: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawRectangleGradientEx(rect, tl, bl, tr, br)

  let inline fillRect (layer: int<RenderLayer>, color: Color) (rect: Rectangle) : IRenderCommand2D =
    FillRectCommand(rect, color, layer)

  let inline rectOutline (layer: int<RenderLayer>, color: Color, thickness: float32) (rect: Rectangle) : IRenderCommand2D =
    RectOutlineCommand(rect, thickness, color, layer)

  let inline fillRectRounded (layer: int<RenderLayer>, color: Color, roundness: float32, segments: int) (rect: Rectangle) : IRenderCommand2D =
    FillRectRoundedCommand(rect, roundness, segments, color, layer)

  let inline rectRoundedOutline (layer: int<RenderLayer>, color: Color, roundness: float32, segments: int, thickness: float32) (rect: Rectangle) : IRenderCommand2D =
    RectRoundedOutlineCommand(rect, roundness, segments, thickness, color, layer)

  let inline rectGradientV (layer: int<RenderLayer>) (x: int, y: int, w: int, h: int, top: Color, bottom: Color) : IRenderCommand2D =
    RectGradientVCommand(x, y, w, h, top, bottom, layer)

  let inline rectGradientH (layer: int<RenderLayer>) (x: int, y: int, w: int, h: int, left: Color, right: Color) : IRenderCommand2D =
    RectGradientHCommand(x, y, w, h, left, right, layer)

  let inline rectGradient (layer: int<RenderLayer>) (rect: Rectangle, tl: Color, bl: Color, tr: Color, br: Color) : IRenderCommand2D =
    RectGradientCommand(rect, tl, bl, tr, br, layer)

  // ═══════════════════════════════════════════════════════════════════
  // Circles & Ellipses
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type FillCircleCommand(center: Vector2, radius: float32, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawCircleV(center, radius, color)

  [<Struct>]
  type CircleOutlineCommand(center: Vector2, radius: float32, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawCircleLinesV(center, radius, color)

  [<Struct>]
  type CircleSectorCommand(center: Vector2, radius: float32, startAngle: float32, endAngle: float32, segments: int, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawCircleSector(center, radius, startAngle, endAngle, segments, color)

  [<Struct>]
  type CircleSectorOutlineCommand(center: Vector2, radius: float32, startAngle: float32, endAngle: float32, segments: int, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawCircleSectorLines(center, radius, startAngle, endAngle, segments, color)

  [<Struct>]
  type CircleGradientCommand(centerX: int, centerY: int, radius: float32, inner: Color, outer: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawCircleGradient(centerX, centerY, radius, inner, outer)

  [<Struct>]
  type FillRingCommand(center: Vector2, innerR: float32, outerR: float32, startAngle: float32, endAngle: float32, segments: int, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawRing(center, innerR, outerR, startAngle, endAngle, segments, color)

  [<Struct>]
  type RingOutlineCommand(center: Vector2, innerR: float32, outerR: float32, startAngle: float32, endAngle: float32, segments: int, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawRingLines(center, innerR, outerR, startAngle, endAngle, segments, color)

  [<Struct>]
  type FillEllipseCommand(centerX: int, centerY: int, radiusH: float32, radiusV: float32, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawEllipse(centerX, centerY, radiusH, radiusV, color)

  [<Struct>]
  type EllipseOutlineCommand(centerX: int, centerY: int, radiusH: float32, radiusV: float32, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawEllipseLines(centerX, centerY, radiusH, radiusV, color)

  let inline fillCircle (layer: int<RenderLayer>, color: Color) (center: Vector2, radius: float32) : IRenderCommand2D =
    FillCircleCommand(center, radius, color, layer)

  let inline circleOutline (layer: int<RenderLayer>, color: Color) (center: Vector2, radius: float32) : IRenderCommand2D =
    CircleOutlineCommand(center, radius, color, layer)

  let inline circleSector (layer: int<RenderLayer>, color: Color) (center: Vector2, radius: float32, startAngle: float32, endAngle: float32, segments: int) : IRenderCommand2D =
    CircleSectorCommand(center, radius, startAngle, endAngle, segments, color, layer)

  let inline circleSectorOutline (layer: int<RenderLayer>, color: Color) (center: Vector2, radius: float32, startAngle: float32, endAngle: float32, segments: int) : IRenderCommand2D =
    CircleSectorOutlineCommand(center, radius, startAngle, endAngle, segments, color, layer)

  let inline circleGradient (layer: int<RenderLayer>) (centerX: int, centerY: int, radius: float32, inner: Color, outer: Color) : IRenderCommand2D =
    CircleGradientCommand(centerX, centerY, radius, inner, outer, layer)

  let inline fillRing (layer: int<RenderLayer>, color: Color) (center: Vector2, innerR: float32, outerR: float32, startAngle: float32, endAngle: float32, segments: int) : IRenderCommand2D =
    FillRingCommand(center, innerR, outerR, startAngle, endAngle, segments, color, layer)

  let inline ringOutline (layer: int<RenderLayer>, color: Color) (center: Vector2, innerR: float32, outerR: float32, startAngle: float32, endAngle: float32, segments: int) : IRenderCommand2D =
    RingOutlineCommand(center, innerR, outerR, startAngle, endAngle, segments, color, layer)

  let inline fillEllipse (layer: int<RenderLayer>, color: Color) (centerX: int, centerY: int, radiusH: float32, radiusV: float32) : IRenderCommand2D =
    FillEllipseCommand(centerX, centerY, radiusH, radiusV, color, layer)

  let inline ellipseOutline (layer: int<RenderLayer>, color: Color) (centerX: int, centerY: int, radiusH: float32, radiusV: float32) : IRenderCommand2D =
    EllipseOutlineCommand(centerX, centerY, radiusH, radiusV, color, layer)

  // ═══════════════════════════════════════════════════════════════════
  // Lines & Curves
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type LineCommand(start: Vector2, finish: Vector2, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawLineV(start, finish, color)

  [<Struct>]
  type LineThickCommand(start: Vector2, finish: Vector2, thickness: float32, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawLineEx(start, finish, thickness, color)

  [<Struct>]
  type LineStripCommand(points: Vector2[], color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawLineStrip(points, points.Length, color)

  [<Struct>]
  type BezierCommand(start: Vector2, control: Vector2, finish: Vector2, thickness: float32, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawLineBezier(start, control, thickness, color)

  let inline line (layer: int<RenderLayer>, color: Color) (start: Vector2, finish: Vector2) : IRenderCommand2D =
    LineCommand(start, finish, color, layer)

  let inline lineThick (layer: int<RenderLayer>, color: Color, thickness: float32) (start: Vector2, finish: Vector2) : IRenderCommand2D =
    LineThickCommand(start, finish, thickness, color, layer)

  let inline lineStrip (layer: int<RenderLayer>, color: Color) (points: Vector2[]) : IRenderCommand2D =
    LineStripCommand(points, color, layer)

  let inline bezier (layer: int<RenderLayer>, color: Color, thickness: float32) (start: Vector2, control: Vector2, finish: Vector2) : IRenderCommand2D =
    BezierCommand(start, control, finish, thickness, color, layer)

  // ═══════════════════════════════════════════════════════════════════
  // Triangles & Polygons
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type TriangleCommand(v1: Vector2, v2: Vector2, v3: Vector2, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawTriangle(v1, v2, v3, color)

  [<Struct>]
  type TriangleFanCommand(points: Vector2[], color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawTriangleFan(points, points.Length, color)

  [<Struct>]
  type TriangleStripCommand(points: Vector2[], color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawTriangleStrip(points, points.Length, color)

  [<Struct>]
  type FillPolyCommand(center: Vector2, sides: int, radius: float32, rotation: float32, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawPoly(center, sides, radius, rotation, color)

  [<Struct>]
  type PolyOutlineCommand(center: Vector2, sides: int, radius: float32, rotation: float32, thickness: float32, color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.DrawPolyLinesEx(center, sides, radius, rotation, thickness, color)

  let inline triangle (layer: int<RenderLayer>, color: Color) (v1: Vector2, v2: Vector2, v3: Vector2) : IRenderCommand2D =
    TriangleCommand(v1, v2, v3, color, layer)

  let inline triangleFan (layer: int<RenderLayer>, color: Color) (points: Vector2[]) : IRenderCommand2D =
    TriangleFanCommand(points, color, layer)

  let inline triangleStrip (layer: int<RenderLayer>, color: Color) (points: Vector2[]) : IRenderCommand2D =
    TriangleStripCommand(points, color, layer)

  let inline fillPoly (layer: int<RenderLayer>, color: Color) (center: Vector2, sides: int, radius: float32, rotation: float32) : IRenderCommand2D =
    FillPolyCommand(center, sides, radius, rotation, color, layer)

  let inline polyOutline (layer: int<RenderLayer>, color: Color, thickness: float32) (center: Vector2, sides: int, radius: float32, rotation: float32) : IRenderCommand2D =
    PolyOutlineCommand(center, sides, radius, rotation, thickness, color, layer)

  // ═══════════════════════════════════════════════════════════════════
  // Camera, Shader, Target
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type BeginCameraCommand(camera: Camera2D, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render ctx = ctx.BeginCamera(camera)

  [<Struct>]
  type EndCameraCommand(cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render ctx = ctx.EndCamera()

  [<Struct>]
  type BeginShaderCommand(shader: Shader, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render ctx = ctx.BeginShader(shader)

  [<Struct>]
  type EndShaderCommand(cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render ctx = ctx.EndShader()

  [<Struct>]
  type BeginTargetCommand(target: RenderTexture2D, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.BeginTextureMode(target)

  [<Struct>]
  type EndTargetCommand(cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.EndTextureMode()

  let inline beginCamera (layer: int<RenderLayer>) (camera: Camera2D) : IRenderCommand2D =
    BeginCameraCommand(camera, layer)

  let inline endCamera (layer: int<RenderLayer>) : IRenderCommand2D =
    EndCameraCommand(layer)

  let inline beginShader (layer: int<RenderLayer>) (shader: Shader) : IRenderCommand2D =
    BeginShaderCommand(shader, layer)

  let inline endShader (layer: int<RenderLayer>) : IRenderCommand2D =
    EndShaderCommand(layer)

  let inline beginTarget (layer: int<RenderLayer>) (target: RenderTexture2D) : IRenderCommand2D =
    BeginTargetCommand(target, layer)

  let inline endTarget (layer: int<RenderLayer>) : IRenderCommand2D =
    EndTargetCommand(layer)

  // ═══════════════════════════════════════════════════════════════════
  // Render State
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type SetBlendCommand(mode: BlendMode, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Rlgl.SetBlendMode(mode)

  [<Struct>]
  type SetScissorCommand(x: int, y: int, w: int, h: int, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ =
        Rlgl.EnableScissorTest()
        Rlgl.Scissor(x, y, w, h)

  [<Struct>]
  type ClearScissorCommand(cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Rlgl.DisableScissorTest()

  [<Struct>]
  type SetLineWidthCommand(width: float32, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Rlgl.SetLineWidth(width)

  [<Struct>]
  type SetViewportCommand(x: int, y: int, w: int, h: int, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Rlgl.Viewport(x, y, w, h)

  let inline setBlend (layer: int<RenderLayer>) (mode: BlendMode) : IRenderCommand2D =
    SetBlendCommand(mode, layer)

  let inline setScissor (layer: int<RenderLayer>) (x: int, y: int, w: int, h: int) : IRenderCommand2D =
    SetScissorCommand(x, y, w, h, layer)

  let inline clearScissor (layer: int<RenderLayer>) : IRenderCommand2D =
    ClearScissorCommand(layer)

  let inline setLineWidth (layer: int<RenderLayer>) (width: float32) : IRenderCommand2D =
    SetLineWidthCommand(width, layer)

  let inline setViewport (layer: int<RenderLayer>) (x: int, y: int, w: int, h: int) : IRenderCommand2D =
    SetViewportCommand(x, y, w, h, layer)

  // ═══════════════════════════════════════════════════════════════════
  // Escape Hatches
  // ═══════════════════════════════════════════════════════════════════

  [<Struct>]
  type DrawImmediateCommand(action: unit -> unit, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render ctx = ctx.DrawImmediate(action)

  [<Struct>]
  type ClearCommand(color: Color, cmdLayer: int<RenderLayer>) =
    interface IRenderCommand2D with
      member _.Layer = cmdLayer
      member _.Render _ = Raylib.ClearBackground(color)

  let inline drawImmediate (layer: int<RenderLayer>) (action: unit -> unit) : IRenderCommand2D =
    DrawImmediateCommand(action, layer)

  let inline clear (layer: int<RenderLayer>) (color: Color) : IRenderCommand2D =
    ClearCommand(color, layer)
