open System.IO
open System.Numerics
open Raylib_cs

#nowarn 3391

[<RequireQualifiedAccess>]
type Uniform =
    | Palette
    | Time
    | Zoom
    | Offset
    | C
    | ScreenDimensions
    | UseDouble

    override this.ToString() =
        match this with
        | Palette  -> "palette"
        | Time -> "iTime"
        | Zoom -> "zoom"
        | Offset -> "offset"
        | C  -> "c"
        | ScreenDimensions -> "screenDims"
        | UseDouble -> "useDouble"


module Palettes =
    let basePalette =
        // Vector3(0.067f, 0.196f, 0.478f) -> Blue replacement
        // Vector3(0.898f, 0.898f, 0.898f) -> Another white
        [| Vector3(0f, 0f, 0f)
           Vector3(0.078f, 0.129f, 0.239f)
           Vector3(0.988f, 0.639f, 0.067f)
           Vector3(1f, 1f, 1f)
           Vector3(0.988f, 0.639f, 0.067f)
           Vector3(0.078f, 0.129f, 0.239f)
           Vector3(0f, 0f, 0f) |]

    let itBurnsTheEyes =
        [| Vector3(1f, 0f, 0f)
           Vector3(0f, 1f, 0f)
           Vector3(1f, 1f, 1f)
           Vector3(0f, 0f, 1f)
           Vector3(0f, 0f, 0f) |]

    let bof =
        [| Vector3(1f, 0.843f, 0f)  // Gold
           Vector3(0f, 0f, 0f)      // Black
           Vector3(0f, 0f, 1f)      // Blue
           Vector3(1f, 1f, 1f)      (* White *) |]

module CBool =
    let toBool (cBool: CBool) : bool = cBool

[<EntryPoint>]
let main args =
    // Initialization
    //--------------------------------------------------------------------------------------
    let initialWidth = 400
    let initialHeight = 450

    Raylib.SetConfigFlags (ConfigFlags.Msaa4xHint ||| ConfigFlags.ResizableWindow) // Enable Multi Sampling Anti Aliasing 4x (if available)
    Raylib.InitWindow(initialWidth, initialHeight, "raylib [shaders] example - julia sets")

    // Load shaders
    let shaderPath = Path.Combine(__SOURCE_DIRECTORY__, "shader.glsl")
    let shaderCode = File.ReadAllText shaderPath
    let shader = Raylib.LoadShaderFromMemory(null, shaderCode) // NOTE: Defining 0 (NULL) for vertex shader forces usage of internal default vertex shader

    // Create a RenderTexture2D to be used for render to texture
    let mutable target = Raylib.LoadRenderTexture(initialWidth, initialHeight)

    // Set uniforms
    // -------------------------------------------------------------------------------------
    let screenDimsLoc = Raylib.GetShaderLocation(shader, Uniform.ScreenDimensions |> string)
    let paletteLoc = Raylib.GetShaderLocation(shader, Uniform.Palette |> string)
    let iTimeLoc = Raylib.GetShaderLocation(shader, Uniform.Time |> string)
    let zoomLoc = Raylib.GetShaderLocation(shader, Uniform.Zoom |> string)
    let offsetLoc = Raylib.GetShaderLocation(shader, Uniform.Offset |> string)
    let cLoc = Raylib.GetShaderLocation(shader, Uniform.C |> string)
    let useDoubleLoc = Raylib.GetShaderLocation(shader, Uniform.UseDouble |> string)

    Raylib.SetShaderValueV(shader, paletteLoc, Palettes.basePalette, ShaderUniformDataType.Vec3, Palettes.basePalette.Length)
    Raylib.SetShaderValue(shader, iTimeLoc, 0f, ShaderUniformDataType.Float)

    let mutable screenDims = Vector2(float32 initialWidth, float32 initialHeight)
    let mutable zoom = 1f
    let mutable offset = Vector2.Zero
    let mutable c = Vector2(-0.8f, 0.156f)

    Raylib.SetShaderValue(
        shader,
        screenDimsLoc,
        screenDims,
        ShaderUniformDataType.Vec2
    )
    Raylib.SetShaderValue(shader, zoomLoc, zoom, ShaderUniformDataType.Float)
    Raylib.SetShaderValue(shader, offsetLoc, offset, ShaderUniformDataType.Vec2)
    Raylib.SetShaderValue(shader, cLoc, c, ShaderUniformDataType.Vec2)
    Raylib.SetShaderValue(shader, useDoubleLoc, 0, ShaderUniformDataType.Int)

    // -------------------------------------------------------------------------------------
    Raylib.SetTargetFPS 120

    let mutable shouldDraw = true

    // Gamepad dead zones
    let stickDeadZone = 0.1f
    let triggerDeadZone = -0.9f

    // Main game loop
    while not <| Raylib.WindowShouldClose() do
        // Update uniforms
        // -------------------------------------------------------------------------------------
        // Time
        let time = float32 <| Raylib.GetTime()
        Raylib.SetShaderValue(shader, iTimeLoc, time, ShaderUniformDataType.Float)

        // Window resize
        let width = Raylib.GetScreenWidth()
        let height = Raylib.GetScreenHeight()
        if screenDims.X <> float32 width || screenDims.Y <> float32 height then
            target <- Raylib.LoadRenderTexture(width, height)
            screenDims <- Vector2(float32 width, float32 height)
            Raylib.SetShaderValue(shader, screenDimsLoc, screenDims, ShaderUniformDataType.Vec2)
            shouldDraw <- true

        let deltaTime = Raylib.GetFrameTime()

        // ---- Zoom ----
        // Gamepad
        if Raylib.IsGamepadAvailable 0 |> CBool.op_Implicit then
            let leftTrigger = Raylib.GetGamepadAxisMovement(0, GamepadAxis.LeftTrigger)
            let rightTrigger = Raylib.GetGamepadAxisMovement(0, GamepadAxis.RightTrigger)

            if leftTrigger > triggerDeadZone then
                zoom <- zoom * exp (-0.05f * (leftTrigger + 1f))
                shouldDraw <- true
            if rightTrigger > triggerDeadZone then
                zoom <- zoom * exp (0.05f * (rightTrigger + 1f))
                shouldDraw <- true
            Raylib.SetShaderValue(shader, zoomLoc, zoom, ShaderUniformDataType.Float)

        // Mouse
        let toWorldCoordinates zoomLevel (vec: Vector2) = // Same as what is in shader code
            let normalized = vec / screenDims // Map to 0-1 and reverse Y axis
            let normCentered = 2f * normalized - Vector2 1f
            let fixedRatio = normCentered * Vector2(screenDims.X / screenDims.Y, -1f)

            let zoomed = fixedRatio / zoomLevel
            let offsetApplied = zoomed + 2f * offset

            offsetApplied

        let mouseZoom = Raylib.GetMouseWheelMove()
        if mouseZoom <> 0f then
            let screenMousePos = Raylib.GetMousePosition()

            let oldZoom = zoom
            zoom <- zoom * exp (0.1f * mouseZoom)

            let beforeMouseWorldPos = screenMousePos |> toWorldCoordinates oldZoom
            let afterMouseWorldPos = screenMousePos |> toWorldCoordinates zoom

            offset <- offset + (beforeMouseWorldPos - afterMouseWorldPos) / 2f
            Raylib.SetShaderValue(shader, zoomLoc, zoom, ShaderUniformDataType.Float)
            Raylib.SetShaderValue(shader, offsetLoc, offset, ShaderUniformDataType.Vec2)
            shouldDraw <- true

        // ---- Offset ----
        if Raylib.IsGamepadAvailable 0 |> CBool.op_Implicit then
            let leftStick = Vector2(
                Raylib.GetGamepadAxisMovement(0, GamepadAxis.LeftX),
                -Raylib.GetGamepadAxisMovement(0, GamepadAxis.LeftY)
            )

            if leftStick.Length() > stickDeadZone then
                offset <- offset + deltaTime * 0.7f * leftStick / zoom
                shouldDraw <- true
                Raylib.SetShaderValue(shader, offsetLoc, offset, ShaderUniformDataType.Vec2)

        if Raylib.IsMouseButtonDown MouseButton.Left |> CBool.op_Implicit then
            let mouseDelta = Raylib.GetMouseDelta()
            offset <- offset - mouseDelta / Vector2(screenDims.Y, -screenDims.Y) / zoom
            shouldDraw <- true
            Raylib.SetShaderValue(shader, offsetLoc, offset, ShaderUniformDataType.Vec2)

        // ---- C ----
        if Raylib.IsGamepadAvailable 0 |> CBool.op_Implicit then
            let rightStick = Vector2(
                Raylib.GetGamepadAxisMovement(0, GamepadAxis.RightX),
                Raylib.GetGamepadAxisMovement(0, GamepadAxis.RightY)
            )

            if rightStick.Length() > stickDeadZone then
                c <- c + deltaTime * 0.006f * rightStick / zoom
                shouldDraw <- true
                Raylib.SetShaderValue(shader, cLoc, c, ShaderUniformDataType.Vec2)

        if Raylib.IsKeyDown KeyboardKey.KpAdd |> CBool.op_Implicit || Raylib.IsKeyDown KeyboardKey.Equal |> CBool.op_Implicit then
            c <- c + Vector2(0f, 0.1f) * deltaTime / zoom
            shouldDraw <- true
            Raylib.SetShaderValue(shader, cLoc, c, ShaderUniformDataType.Vec2)
        if Raylib.IsKeyDown KeyboardKey.KpSubtract |> CBool.op_Implicit || Raylib.IsKeyDown KeyboardKey.Minus |> CBool.op_Implicit then
            c <- c - Vector2(0f, 0.1f) * deltaTime / zoom
            shouldDraw <- true
            Raylib.SetShaderValue(shader, cLoc, c, ShaderUniformDataType.Vec2)

        // ---- Use double (high-precision) ----
        // Keyboard
        match Raylib.IsKeyPressed KeyboardKey.Q |> CBool.toBool,
              Raylib.IsKeyReleased KeyboardKey.Q |> CBool.toBool with
        | true, false -> // Pressed
            printfn "Activating"
            shouldDraw <- true
            Raylib.SetShaderValue(shader, useDoubleLoc, 1, ShaderUniformDataType.Int) // Activate
        | false, true -> // Released
            shouldDraw <- true
            Raylib.SetShaderValue(shader, useDoubleLoc, 0, ShaderUniformDataType.Int) // Deactivate
        | _ -> ()

        // Gamepad
        if Raylib.IsGamepadAvailable 0 |> CBool.toBool then
            match Raylib.IsGamepadButtonPressed(0, GamepadButton.RightFaceDown) |> CBool.toBool,
                  Raylib.IsGamepadButtonReleased(0, GamepadButton.RightFaceDown) |> CBool.toBool with
            | true, false -> // Pressed
                shouldDraw <- true
                Raylib.SetShaderValue(shader, useDoubleLoc, 1, ShaderUniformDataType.Int) // Activate
            | false, true -> // Released
                shouldDraw <- true
                Raylib.SetShaderValue(shader, useDoubleLoc, 0, ShaderUniformDataType.Int) // Deactivate
            | _ -> ()
        // -------------------------------------------------------------------------------------


        // Draw
        //----------------------------------------------------------------------------------
        Raylib.BeginDrawing()
        Raylib.ClearBackground Color.Black

        if shouldDraw then
            // Using a render texture to draw fractal
            Raylib.BeginTextureMode target
            Raylib.ClearBackground Color.Black

            // Draw using shader
            Raylib.BeginShaderMode shader// Proper way to pass UVs correctly
            let sourceRect = Rectangle(0f, 0f, float32 width, -float32 height) // Flip Y
            Raylib.DrawTextureRec(target.Texture, sourceRect, Vector2.Zero, Color.White)
            Raylib.EndShaderMode()

            Raylib.EndTextureMode()
            shouldDraw <- false

        Raylib.DrawTexture(target.Texture, 0, 0, Color.White)

        let fontSize = 20
        Raylib.DrawText(sprintf "Zoom: %f" zoom, 10, 10, fontSize, Color.White)
        Raylib.DrawText(sprintf "Offset: %A" offset, 10, 10 + (fontSize + 2) * 1, fontSize, Color.White)
        Raylib.DrawText(sprintf "C: %A" c, 10, 10 + (fontSize + 2) * 2, fontSize, Color.White)

        Raylib.EndDrawing()
        //----------------------------------------------------------------------------------

    // De-Initialization
    //--------------------------------------------------------------------------------------
    Raylib.UnloadShader shader
    Raylib.UnloadRenderTexture target

    Raylib.CloseWindow()
    0
