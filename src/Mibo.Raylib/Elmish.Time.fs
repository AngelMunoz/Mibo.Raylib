namespace Mibo.Elmish

open System

[<Struct>]
type GameTime = {
    TotalTime: TimeSpan
    ElapsedGameTime: TimeSpan
    IsRunningSlowly: bool
}

[<Struct>]
type DispatchMode =
    | Immediate
    | FrameBounded

[<Struct>]
type FixedStepConfig<'Msg> = {
    StepSeconds: float32
    MaxStepsPerFrame: int
    MaxFrameSeconds: float32 voption
    Map: float32 -> 'Msg
}

module FixedStep =
    let inline compute
        (stepSeconds: float32)
        (maxStepsPerFrame: int)
        (maxFrameSeconds: float32)
        (accumulatorSeconds: float32)
        (deltaSeconds: float32)
        : struct (float32 * int * bool) =

        let dt =
            if deltaSeconds < 0.0f then 0.0f
            elif deltaSeconds > maxFrameSeconds then maxFrameSeconds
            else deltaSeconds

        let mutable acc = accumulatorSeconds + dt
        let mutable steps = 0

        if stepSeconds <= 0.0f || maxStepsPerFrame <= 0 then
            struct (accumulatorSeconds, 0, false)
        else
            while steps < maxStepsPerFrame && acc >= stepSeconds do
                acc <- acc - stepSeconds
                steps <- steps + 1

            let dropped = (steps = maxStepsPerFrame) && (acc >= stepSeconds)

            if dropped then
                acc <- 0.0f

            struct (acc, steps, dropped)
