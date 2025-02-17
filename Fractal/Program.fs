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

    override this.ToString() =
        match this with
        | Palette  -> "palette"
        | Time -> "iTime"
        | Zoom -> "zoom"
        | Offset -> "offset"
        | C  -> "c"
        | ScreenDimensions -> "screenDims"

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

[<EntryPoint>]
let main args =
    // Initialization
    //--------------------------------------------------------------------------------------
    let screenWidth = 400
    let screenHeight = 450

    Raylib.SetConfigFlags (ConfigFlags.Msaa4xHint ||| ConfigFlags.ResizableWindow) // Enable Multi Sampling Anti Aliasing 4x (if available)
    Raylib.InitWindow(screenWidth, screenHeight, "raylib [shaders] example - julia sets")

    // Load shader
    let shaderPath = Path.Combine(__SOURCE_DIRECTORY__, "shader.glsl")
    let shaderCode = File.ReadAllText shaderPath
    let shader = Raylib.LoadShaderFromMemory(null, shaderCode) // NOTE: Defining 0 (NULL) for vertex shader forces usage of internal default vertex shader

    // Create a RenderTexture2D to be used for render to texture
    let target = Raylib.LoadRenderTexture(screenWidth, screenHeight)

    // Set uniforms
    // -------------------------------------------------------------------------------------
    let screenDimsLoc = Raylib.GetShaderLocation(shader, Uniform.ScreenDimensions |> string)
    let paletteLoc = Raylib.GetShaderLocation(shader, Uniform.Palette |> string)
    let iTimeLoc = Raylib.GetShaderLocation(shader, Uniform.Time |> string)
    let zoomLoc = Raylib.GetShaderLocation(shader, Uniform.Zoom |> string)
    let offsetLoc = Raylib.GetShaderLocation(shader, Uniform.Offset |> string)
    let cLoc = Raylib.GetShaderLocation(shader, Uniform.C |> string)

    let screenDims = Vector2(float32 screenWidth, float32 screenHeight)
    Raylib.SetShaderValue(
        shader,
        screenDimsLoc,
        screenDims,
        ShaderUniformDataType.Vec2
    )

    Raylib.SetShaderValueV(shader, paletteLoc, Palettes.basePalette, ShaderUniformDataType.Vec3, Palettes.basePalette.Length)
    Raylib.SetShaderValue(shader, iTimeLoc, 0f, ShaderUniformDataType.Float)

    let mutable zoom = 1f
    let mutable offset = Vector2.Zero
    let mutable c = Vector2(-0.8f, 0.156f)

    Raylib.SetShaderValue(shader, zoomLoc, zoom, ShaderUniformDataType.Float)
    Raylib.SetShaderValue(shader, offsetLoc, offset, ShaderUniformDataType.Vec2)
    Raylib.SetShaderValue(shader, cLoc, c, ShaderUniformDataType.Vec2)

    Raylib.SetTargetFPS 120
    // -------------------------------------------------------------------------------------

    // Main game loop
    while not <| Raylib.WindowShouldClose() do
        // Update uniforms
        // -------------------------------------------------------------------------------------
        // Time
        let time = float32 <| Raylib.GetTime()
        Raylib.SetShaderValue(shader, iTimeLoc, time, ShaderUniformDataType.Float)

        // Window size
        let width = Raylib.GetScreenWidth()
        let height = Raylib.GetScreenHeight()
        let screenDims = Vector2(float32 width, float32 height)

        target.Texture.Width <- width
        target.Texture.Height <- height
        Raylib.SetShaderValue(shader, screenDimsLoc, screenDims, ShaderUniformDataType.Vec2)

        let deltaTime = Raylib.GetFrameTime()
        // Zoom
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

        // Offset
        // if Raylib.IsKeyDown KeyboardKey.Left |> CBool.op_Implicit then offset <- offset + Vector2(-deltaTime, 0f) / zoom
        // if Raylib.IsKeyDown KeyboardKey.Up |> CBool.op_Implicit then offset <- offset + Vector2(0f, deltaTime) / zoom
        // if Raylib.IsKeyDown KeyboardKey.Right |> CBool.op_Implicit then offset <- offset + Vector2(deltaTime, 0f) / zoom
        // if Raylib.IsKeyDown KeyboardKey.Down |> CBool.op_Implicit then offset <- offset + Vector2(0f, -deltaTime) / zoom
        if Raylib.IsMouseButtonDown MouseButton.Left |> CBool.op_Implicit then
            let mouseDelta = Raylib.GetMouseDelta()
            offset <- offset - mouseDelta * Vector2(1f, -1f) / (screenDims * zoom)

        Raylib.SetShaderValue(shader, offsetLoc, offset, ShaderUniformDataType.Vec2)

        // C
        if Raylib.IsKeyDown KeyboardKey.KpAdd |> CBool.op_Implicit || Raylib.IsKeyDown KeyboardKey.Equal |> CBool.op_Implicit then
            c <- c + Vector2(0f, 0.0001f) * deltaTime
        if Raylib.IsKeyDown KeyboardKey.KpSubtract |> CBool.op_Implicit || Raylib.IsKeyDown KeyboardKey.Minus |> CBool.op_Implicit then
            c <- c - Vector2(0f, 0.0001f) * deltaTime

        Raylib.SetShaderValue(shader, cLoc, c, ShaderUniformDataType.Vec2)
        // -------------------------------------------------------------------------------------


        // Draw
        //----------------------------------------------------------------------------------
        Raylib.BeginDrawing()
        Raylib.ClearBackground Color.Black

        // Using a render texture to draw fractal
        // Enable drawing to texture
        Raylib.BeginTextureMode target
        Raylib.ClearBackground Color.Black

        // Draw a rectangle in shader mode to be used as shader canvas
        // NOTE: Rectangle uses font Color.white character texture coordinates,
        // so shader can not be applied here directly because input vertexTexCoord
        // do not represent full screen coordinates (space where want to apply shader)
        Raylib.DrawRectangle(0, 0, height, width, Color.Black)
        Raylib.EndTextureMode()

        // Draw the saved texture and rendered fractal with shader
        // NOTE: We do not invert texture on Y, already considered inside shader
        Raylib.BeginShaderMode shader
        Raylib.DrawTexture(target.Texture, 0, 0, Color.White)
        Raylib.EndShaderMode()

        Raylib.EndDrawing()
        //----------------------------------------------------------------------------------

    // De-Initialization
    //--------------------------------------------------------------------------------------
    Raylib.UnloadShader shader
    Raylib.UnloadRenderTexture target

    Raylib.CloseWindow()
    0
