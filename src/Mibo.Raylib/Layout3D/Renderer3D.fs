namespace Mibo.Layout3D

open System.Collections.Generic
open System.Numerics
open Mibo.Elmish.Graphics3D

module CellGridRenderer3D =

  let inline render
    (grid: CellGrid3D<'T>)
    ([<InlineIfLambda>] renderCell: Vector3 -> 'T -> unit)
    : unit =
    grid
    |> CellGrid3D.iter(fun x y z content ->
      let worldPos = CellGrid3D.getWorldPos x y z grid
      renderCell worldPos content)

  let inline renderVolume
    (bounds: BoundingBox)
    (grid: CellGrid3D<'T>)
    ([<InlineIfLambda>] renderCell: Vector3 -> 'T -> unit)
    : unit =
    grid
    |> CellGrid3D.iterVolume bounds (fun x y z content ->
      let worldPos = CellGrid3D.getWorldPos x y z grid
      renderCell worldPos content)

  let inline renderWithIndices
    (grid: CellGrid3D<'T>)
    ([<InlineIfLambda>] renderCell: int -> int -> int -> Vector3 -> 'T -> unit)
    : unit =
    grid
    |> CellGrid3D.iter(fun x y z content ->
      let worldPos = CellGrid3D.getWorldPos x y z grid
      renderCell x y z worldPos content)

  /// <summary>
  /// Renders a cell grid using GPU instancing. Cells are grouped by a key function,
  /// and each group is emitted as a single <c>DrawMeshInstanced</c> command.
  /// This drastically reduces draw calls when many cells share the same mesh/material.
  /// </summary>
  /// <param name="getKey">Groups cells by this key. Cells with the same key are instanced together.</param>
  /// <param name="getMeshAndMaterial">Resolves the mesh and material for a given cell content value.</param>
  /// <param name="getTransform">Computes the world transform matrix for a cell.</param>
  /// <param name="grid">The cell grid to render.</param>
  /// <param name="buffer">The render buffer to add commands to.</param>
  let renderInstanced
    (getKey: 'T -> 'K)
    (getMeshAndMaterial: 'T -> Raylib_cs.Mesh * Material3D)
    (getTransform: Vector3 -> 'T -> Matrix4x4)
    (grid: CellGrid3D<'T>)
    (buffer: RenderBuffer3D)
    : unit =
    let groups = Dictionary<'K, struct (ResizeArray<Matrix4x4> * 'T)>()

    grid
    |> CellGrid3D.iter(fun x y z content ->
      let worldPos = CellGrid3D.getWorldPos x y z grid
      let key = getKey content
      let transform = getTransform worldPos content

      match groups.TryGetValue key with
      | true, struct (transforms, _) -> transforms.Add transform
      | false, _ ->
        let list = ResizeArray<Matrix4x4>()
        list.Add transform
        groups[key] <- struct (list, content))

    for KeyValue(_, struct (transforms, sample)) in groups do
      if transforms.Count > 0 then
        let mesh, material = getMeshAndMaterial sample
        let arr = transforms.ToArray()
        buffer.Add(Command3D.drawMeshInstanced mesh arr material arr.Length)

    groups.Clear()
