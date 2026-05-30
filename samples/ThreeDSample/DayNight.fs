module ThreeDSample.DayNight

open System
open System.Numerics
open Raylib_cs

let inline lerpColor (a: Color) (b: Color) (t: float32) =
  let t = Math.Clamp(t, 0.0f, 1.0f)

  Color(
    byte(float32 a.R + t * (float32 b.R - float32 a.R)),
    byte(float32 a.G + t * (float32 b.G - float32 a.G)),
    byte(float32 a.B + t * (float32 b.B - float32 a.B)),
    255uy
  )

let getSkyColor time : Color =
  if time < 6.0f then
    Color(10uy, 10uy, 30uy)
  elif time < 8.0f then
    lerpColor
      (Color(10uy, 10uy, 30uy))
      (Color(100uy, 149uy, 237uy))
      ((time - 6.0f) / 2.0f)
  elif time < 16.0f then
    Color(100uy, 149uy, 237uy)
  elif time < 18.0f then
    lerpColor
      (Color(100uy, 149uy, 237uy))
      (Color(50uy, 50uy, 100uy))
      ((time - 16.0f) / 2.0f)
  elif time < 20.0f then
    lerpColor
      (Color(50uy, 50uy, 100uy))
      (Color(10uy, 10uy, 30uy))
      ((time - 18.0f) / 2.0f)
  else
    Color(10uy, 10uy, 30uy)

let getAmbientColor time : Color =
  if time < 5.0f || time > 19.0f then
    Color(40uy, 50uy, 120uy)
  elif time < 7.0f then
    let t = (time - 5.0f) / 2.0f
    let r = byte(int(15.0f + t * 80.0f))
    let g = byte(int(20.0f + t * 100.0f))
    let b = byte(int(45.0f + t * 110.0f))
    Color(r, g, b)
  elif time < 17.0f then
    Color(95uy, 130uy, 155uy)
  elif time < 19.0f then
    let t = (time - 17.0f) / 2.0f
    let r = byte(int(95.0f + t * 40.0f))
    let g = byte(int(130.0f + t * 50.0f))
    let b = byte(int(155.0f + t * 60.0f))
    Color(r, g, b)
  else
    Color(40uy, 50uy, 120uy)

let getAmbientIntensity time : float32 =
  let color = getAmbientColor time
  let avg = (float32 color.R + float32 color.G + float32 color.B) / 3.0f
  MathF.Max(avg / 255.0f * 0.7f, 0.05f)

// ---------------------------------------------------------------------------
// Single directional light on a ~190° arc.
// Sun: 6h–18h, Moon: 18h–6h. The extra 10° at each end handles the
// fade-out/fade-in transition between cycles. One light, one shadow caster.
// ---------------------------------------------------------------------------

[<Literal>]
let private arcDegrees = 190.0f

[<Literal>]
let private fadeDegrees = 10.0f

/// Returns normalized light direction for a single celestial body
/// on a 190° arc. t is 0..1 across the 12h half-cycle.
let private celestialArc (t: float32) (arcRadius: float32) : Vector3 =
  let startAngle = -5.0f * MathF.PI / 180.0f
  let endAngle = 185.0f * MathF.PI / 180.0f
  let angle = startAngle + t * (endAngle - startAngle)

  let pos =
    Vector3(
      MathF.Cos(angle) * arcRadius,
      -MathF.Sin(angle) * arcRadius * 0.6f,
      MathF.Sin(angle * 0.5f) * arcRadius * 0.5f
    )

  Vector3.Normalize(pos)

/// Single light direction — sun (6h–18h) or moon (18h–6h).
let getPrimaryLightDirection (time: float32) (arcRadius: float32) : Vector3 =
  if time >= 6.0f && time <= 18.0f then
    celestialArc ((time - 6.0f) / 12.0f) arcRadius
  else
    let t =
      if time > 18.0f then
        (time - 18.0f) / 12.0f
      else
        (time + 6.0f) / 12.0f

    celestialArc t arcRadius

let getPrimaryLightColor(time: float32) : Color =
  if time >= 6.0f && time <= 18.0f then
    if time < 8.0f then
      lerpColor
        (Color(255uy, 150uy, 80uy))
        (Color(255uy, 245uy, 210uy))
        ((time - 6.0f) / 2.0f)
    elif time < 16.0f then
      Color(255uy, 245uy, 210uy)
    else
      lerpColor
        (Color(255uy, 245uy, 210uy))
        (Color(255uy, 120uy, 60uy))
        ((time - 16.0f) / 2.0f)
  else
    Color(160uy, 190uy, 230uy)

/// Light intensity with fade at arc edges.
/// The ~10° overlap at dawn/dusk creates a smooth transition where
/// the setting body fades out as the rising body fades in.
let getPrimaryLightIntensity(time: float32) : float32 =
  if time >= 6.0f && time <= 18.0f then
    let t = (time - 6.0f) / 12.0f

    if t * arcDegrees < fadeDegrees then
      t * arcDegrees / fadeDegrees
    elif (1.0f - t) * arcDegrees < fadeDegrees then
      (1.0f - t) * arcDegrees / fadeDegrees
    else
      1.0f
  else
    let t =
      if time > 18.0f then
        (time - 18.0f) / 12.0f
      else
        (time + 6.0f) / 12.0f

    let maxMoon = 0.3f

    if t * arcDegrees < fadeDegrees then
      t * arcDegrees / fadeDegrees * maxMoon
    elif (1.0f - t) * arcDegrees < fadeDegrees then
      (1.0f - t) * arcDegrees / fadeDegrees * maxMoon
    else
      maxMoon
