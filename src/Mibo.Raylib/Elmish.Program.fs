namespace Mibo.Elmish

open System

module Program =
  let mkProgram init update = {
    Init = init
    Update = update
    Subscribe = (fun _ctx _model -> Sub.none)
    Config = []
    Renderers = []
    Tick = ValueNone
    FixedStep = ValueNone
    DispatchMode = DispatchMode.Immediate
  }

  let withConfig
    (configure: GameConfig -> unit)
    (program: Program<'Model, 'Msg>)
    =
    {
      program with
          Config = configure :: program.Config
    }

  let withRenderer
    (factory: unit -> IRenderer<'Model>)
    (program: Program<'Model, 'Msg>)
    =
    {
      program with
          Renderers = factory :: program.Renderers
    }

  let withTick map program = { program with Tick = ValueSome map }

  let withFixedStep cfg program =
    if cfg.StepSeconds <= 0.0f then
      invalidArg (nameof cfg.StepSeconds) "StepSeconds must be > 0"

    if cfg.MaxStepsPerFrame <= 0 then
      invalidArg (nameof cfg.MaxStepsPerFrame) "MaxStepsPerFrame must be > 0"

    {
      program with
          FixedStep = ValueSome cfg
    }

  let withDispatchMode mode program = { program with DispatchMode = mode }

  let withSubscription subscribe program = {
    program with
        Subscribe = subscribe
  }

  let withAssets program = program
