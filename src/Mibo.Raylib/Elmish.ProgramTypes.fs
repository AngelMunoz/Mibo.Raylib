namespace Mibo.Elmish

open System

type GameConfig = {
  mutable Width: int
  mutable Height: int
  mutable Title: string
  mutable TargetFPS: int
}

type Program<'Model, 'Msg> = {
  Init: GameContext -> struct ('Model * Cmd<'Msg>)
  Update: 'Msg -> 'Model -> struct ('Model * Cmd<'Msg>)
  Subscribe: GameContext -> 'Model -> Sub<'Msg>
  Config: (GameConfig -> unit) list
  Renderers: (unit -> IRenderer<'Model>) list
  Tick: (GameTime -> 'Msg) voption
  FixedStep: FixedStepConfig<'Msg> voption
  DispatchMode: DispatchMode
}
