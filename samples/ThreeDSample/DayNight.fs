module ThreeDSample.DayNight

open System
open System.Numerics
open Raylib_cs

[<Struct>]
type State = {
  TimeOfDay: float32
  DayDuration: float32
}

let initial = {
  TimeOfDay = 12.0f
  DayDuration = 60.0f
}

let inline update dt state = {
  state with
      TimeOfDay = (state.TimeOfDay + dt * (24.0f / state.DayDuration)) % 24.0f
}

let inline lerpColor (a: Color) (b: Color) (t: float32) =
  let t = Math.Clamp(t, 0.0f, 1.0f)

  Color(
    byte(float32 a.R + t * (float32 b.R - float32 a.R)),
    byte(float32 a.G + t * (float32 b.G - float32 a.G)),
    byte(float32 a.B + t * (float32 b.B - float32 a.B)),
    255uy
  )

let getSkyColor time : Color =
  if time < 6.0f then Color(10uy, 10uy, 30uy)
  elif time < 8.0f then
    lerpColor (Color(10uy, 10uy, 30uy)) (Color(100uy, 149uy, 237uy)) ((time - 6.0f) / 2.0f)
  elif time < 16.0f then Color(100uy, 149uy, 237uy)
  elif time < 18.0f then
    lerpColor (Color(100uy, 149uy, 237uy)) (Color(50uy, 50uy, 100uy)) ((time - 16.0f) / 2.0f)
  elif time < 20.0f then
    lerpColor (Color(50uy, 50uy, 100uy)) (Color(10uy, 10uy, 30uy)) ((time - 18.0f) / 2.0f)
  else Color(10uy, 10uy, 30uy)

let getAmbientColor time : Color =
  if time < 5.0f || time > 19.0f then Color(40uy, 50uy, 120uy)
  elif time < 7.0f then
    let t = (time - 5.0f) / 2.0f
    let r = byte(int(15.0f + t * 80.0f))
    let g = byte(int(20.0f + t * 100.0f))
    let b = byte(int(45.0f + t * 110.0f))
    Color(r, g, b)
  elif time < 17.0f then Color(95uy, 130uy, 155uy)
  elif time < 19.0f then
    let t = (time - 17.0f) / 2.0f
    let r = byte(int(95.0f + t * 40.0f))
    let g = byte(int(130.0f + t * 50.0f))
    let b = byte(int(155.0f + t * 60.0f))
    Color(r, g, b)
  else Color(40uy, 50uy, 120uy)

let getAmbientIntensity time : float32 =
  let color = getAmbientColor time
  let avg = (float32 color.R + float32 color.G + float32 color.B) / 3.0f
  MathF.Max(avg / 255.0f * 0.7f, 0.05f)

let getSunDirection time : Vector3 =
  let angle = (time - 6.0f) / 12.0f * MathF.PI
  Vector3(MathF.Cos(angle) * 0.8f, -MathF.Sin(angle) * 0.6f, MathF.Sin(angle * 0.5f))

let getSunColor time : Color =
  if time < 6.0f || time > 18.0f then Color(0uy, 0uy, 0uy)
  elif time < 8.0f then
    lerpColor (Color(255uy, 150uy, 80uy)) (Color(255uy, 245uy, 210uy)) ((time - 6.0f) / 2.0f)
  elif time < 16.0f then Color(255uy, 245uy, 210uy)
  else
    lerpColor (Color(255uy, 245uy, 210uy)) (Color(255uy, 120uy, 60uy)) ((time - 16.0f) / 2.0f)

let getSunIntensity time : float32 =
  if time < 6.0f || time > 18.0f then 0.0f
  elif time < 8.0f then (time - 6.0f) / 2.0f
  elif time < 16.0f then 1.0f
  else (18.0f - time) / 2.0f

let getMoonDirection time : Vector3 =
  let angle = (time - 18.0f) / 12.0f * MathF.PI
  Vector3(MathF.Cos(angle) * 0.6f, -MathF.Sin(angle) * 0.5f, MathF.Sin(angle * 0.5f))

let getMoonIntensity time : float32 =
  if time > 6.0f && time < 18.0f then 0.0f
  elif time < 8.0f then 1.0f - (time - 6.0f) / 2.0f
  elif time < 16.0f then 0.0f
  else (time - 16.0f) / 2.0f
