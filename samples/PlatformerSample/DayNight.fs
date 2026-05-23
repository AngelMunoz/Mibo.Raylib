module PlatformerSample.DayNight

open System
open System.Numerics
open Raylib_cs
open PlatformerSample.Constants

// -------------------------------------------------------------
// Day / Night Cycle
// -------------------------------------------------------------

type State = {
  TimeOfDay: float32
  DayDuration: float32
}

let initial = {
  TimeOfDay = 12.0f
  DayDuration = 60.0f
}

let inline update dt state =
  let hoursPerSecond = 24.0f / state.DayDuration
  { state with TimeOfDay = (state.TimeOfDay + dt * hoursPerSecond) % 24.0f }

let inline lerpColor (a: Color) (b: Color) (t: float32) =
  let t = Math.Clamp(t, 0.0f, 1.0f)
  Color(
    byte(float32 a.R + t * (float32 b.R - float32 a.R)),
    byte(float32 a.G + t * (float32 b.G - float32 a.G)),
    byte(float32 a.B + t * (float32 b.B - float32 a.B)),
    255uy
  )

let getSkyColors time : Color * Color =
  let midnightTop = Color(10uy, 10uy, 30uy)
  let midnightBot = Color(20uy, 20uy, 40uy)
  let dayTop = Color(100uy, 149uy, 237uy)
  let dayBot = Color(173uy, 216uy, 230uy)
  let sunsetTop = Color(50uy, 50uy, 100uy)
  let sunsetBot = Color(255uy, 80uy, 50uy)

  if time < 6.0f then
    midnightTop, midnightBot
  elif time < 8.0f then
    let t = (time - 6.0f) / 2.0f
    lerpColor midnightTop dayTop t, lerpColor midnightBot dayBot t
  elif time < 16.0f then
    dayTop, dayBot
  elif time < 18.0f then
    let t = (time - 16.0f) / 2.0f
    lerpColor dayTop sunsetTop t, lerpColor dayBot sunsetBot t
  elif time < 20.0f then
    let t = (time - 18.0f) / 2.0f
    lerpColor sunsetTop midnightTop t, lerpColor sunsetBot midnightBot t
  else
    midnightTop, midnightBot

let getAmbientColor time : Color =
  let top, bot = getSkyColors time
  let avg =
    (int top.R + int top.G + int top.B + int bot.R + int bot.G + int bot.B)
    / 6
    |> float32
  let intensity = MathF.Max(avg / 255.0f, 0.12f)
  Color(
    byte(intensity * 255.0f),
    byte(intensity * 245.0f),
    byte(intensity * 230.0f),
    255uy
  )

let getSunIntensity time : float32 =
  if time < 6.0f || time > 18.0f then 0.0f
  elif time < 8.0f then (time - 6.0f) / 2.0f
  elif time < 16.0f then 1.0f
  else (18.0f - time) / 2.0f

let getMoonIntensity time : float32 =
  if time > 6.0f && time < 18.0f then 0.0f
  elif time < 8.0f then 1.0f - (time - 6.0f) / 2.0f
  elif time < 16.0f then 0.0f
  elif time < 18.0f then (time - 16.0f) / 2.0f
  else 1.0f

let orbitalPositions (centerX: float32) (state: State) =
  let centerY = groundLevel - 200.0f
  let radiusX = 500.0f
  let radiusY = 200.0f
  let sunAngle = (state.TimeOfDay - 18.0f) / 24.0f * MathF.PI * 2.0f
  let moonAngle = sunAngle + MathF.PI
  let sunX = centerX + radiusX * MathF.Cos(sunAngle)
  let sunY = centerY + radiusY * MathF.Sin(sunAngle)
  let moonX = centerX + radiusX * MathF.Cos(moonAngle)
  let moonY = centerY + radiusY * MathF.Sin(moonAngle)
  Vector2(sunX, sunY), Vector2(moonX, moonY)
