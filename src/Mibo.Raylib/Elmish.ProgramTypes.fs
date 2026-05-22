namespace Mibo.Elmish

open System

/// <summary>
/// Configuration for game window and framerate settings.
/// </summary>
/// <remarks>
/// Config callbacks return a new GameConfig with desired changes applied.
/// Use <see cref="M:Mibo.Elmish.Program.withConfig"/> to register callbacks.
/// </remarks>
[<Struct>]
type GameConfig = {
  /// Window width in pixels. Default: 800.
  Width: int
  /// Window height in pixels. Default: 600.
  Height: int
  /// Window title.
  Title: string
  /// Target frames per second. 0 = unlimited.
  TargetFPS: int
}

module GameConfig =
  let defaultConfig = {
    Width = 800
    Height = 600
    Title = "Mibo Raylib"
    TargetFPS = 60
  }

/// <summary>
/// The Elmish program record that defines the complete game architecture.
/// </summary>
/// <remarks>
/// A program ties together initialization, update logic, subscriptions, and rendering.
/// Use the <see cref="T:Mibo.Elmish.Program"/> module functions to construct and configure programs.
/// </remarks>
type Program<'Model, 'Msg> = {
  /// <summary>Creates initial model and commands when the game starts.</summary>
  Init: GameContext -> struct ('Model * Cmd<'Msg>)
  /// <summary>Handles messages and returns updated model and commands.</summary>
  Update: 'Msg -> 'Model -> struct ('Model * Cmd<'Msg>)
  /// <summary>Returns subscriptions based on current model state.</summary>
  Subscribe: GameContext -> 'Model -> Sub<'Msg>
  /// <summary>
  /// List of configuration callbacks that transform the default GameConfig.
  /// </summary>
  /// <remarks>Each callback receives current config and returns a modified copy.</remarks>
  Config: (GameConfig -> GameConfig) list
  /// <summary>List of renderer factories for drawing.</summary>
  Renderers: (unit -> IRenderer<'Model>) list
  /// <summary>Optional function to generate a message each frame.</summary>
  Tick: (GameTime -> 'Msg) voption
  /// <summary>
  /// Optional framework-managed fixed timestep configuration.
  /// </summary>
  FixedStep: FixedStepConfig<'Msg> voption
  /// <summary>
  /// Controls when dispatched messages become eligible for processing.
  /// </summary>
  /// <remarks>
  /// See <see cref="T:Mibo.Elmish.DispatchMode"/>.
  /// </remarks>
  DispatchMode: DispatchMode
  /// <summary>Optional base path for asset loading. Set via <see cref="M:Mibo.Elmish.Program.withAssetsBasePath"/>.</summary>
  AssetsBasePath: string voption
  /// <summary>Whether the input service is enabled. Set via <see cref="M:Mibo.Elmish.Program.withInput"/>.</summary>
  HasInput: bool
  /// <summary>Whether an input mapper service is enabled. Set via <see cref="M:Mibo.Elmish.Program.withInputMapper"/>.</summary>
  HasInputMapper: bool
}
