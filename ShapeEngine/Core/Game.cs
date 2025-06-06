
using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using ShapeEngine.Audio;
using ShapeEngine.Color;
using ShapeEngine.Core.Shapes;
using ShapeEngine.Core.Structs;
using ShapeEngine.Input;
using ShapeEngine.StaticLib;
using ShapeEngine.Screen;

namespace ShapeEngine.Core;

public class Game
{
    public static Game CurrentGameInstance { get; private set; } = null!;
    
    #region Static
    public static readonly string CURRENT_DIRECTORY = AppDomain.CurrentDomain.BaseDirectory; // Environment.CurrentDirectory;
    public static OSPlatform OS_PLATFORM { get; private set; } =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatform.OSX :
        OSPlatform.FreeBSD;

    public static bool DebugMode { get; private set; } = false;
    public static bool ReleaseMode { get; private set; } = true;
    
    public static bool IsWindows() => OS_PLATFORM == OSPlatform.Windows;
    public static bool IsLinux() => OS_PLATFORM == OSPlatform.Linux;
    public static bool IsOSX() => OS_PLATFORM == OSPlatform.OSX;
    
    public static bool OSXIsRunningInAppBundle()
    {
        if(!IsOSX()) return false;
        
        string exeDir = AppContext.BaseDirectory.Replace('\\', '/');
        return exeDir.Contains(".app/Contents/MacOS/");
    }
    
    public static bool IsEqual<T>(List<T>? a, List<T>? b) where T : IEquatable<T>
    {
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i])) return false;
        }
        return true;
    }
    public static int GetHashCode<T>(IEnumerable<T> collection)
    {
        HashCode hash = new();
        foreach (var element in collection)
        {
            hash.Add(element);
        }
        return hash.ToHashCode();
    }
    public static T GetItem<T>(List<T> collection, int index)
    {
        int i = ShapeMath.WrapIndex(collection.Count, index);
        return collection[i];
    }

    
    #endregion

    #region Public Members
    public string[] LaunchParams { get; protected set; } = Array.Empty<string>();

    public bool FixedPhysicsEnabled { get; private set; }
    public int FixedPhysicsFramerate { get; private set; }
    public float FixedPhysicsTimestep { get; private set; }
    public GameTime Time { get; private set; } = new GameTime();
    public GameTime FixedTime { get; private set; } = new GameTime();
    public ColorRgba BackgroundColorRgba = ColorRgba.Black;
    public float ScreenEffectIntensity = 1.0f;

    public ShaderContainer? ScreenShaders => gameTexture.Shaders;
    public ShapeCamera Camera
    {
        get => curCamera;
        set
        {
            if (value == curCamera) return;
            curCamera.Deactivate();
            curCamera = value;
            gameTexture.Camera = curCamera;
            curCamera.Activate();
            curCamera.SetSize(Window.CurScreenSize);
        }
    }

    

    public ScreenInfo GameScreenInfo { get; private set; } = new();
    public ScreenInfo GameUiScreenInfo { get; private set; } = new();
    public ScreenInfo UIScreenInfo { get; private set; } = new();
    
    private bool paused = false;
    public bool Paused
    {
        get => paused;
        set
        {
            if (value != paused)
            {
                paused = value;
                ResolveOnPausedChanged(paused);
            }
            
        }
    }
    
    public GameWindow Window { get; private set; }
    public readonly AudioDevice AudioDevice;
    public Scene CurScene { get; private set; } = new SceneEmpty();
    
    public ScreenTexture GameTexture => gameTexture;
    #endregion
    
    #region Private Members

    private ScreenTexture gameTexture;
    private readonly ShapeCamera basicCamera = new();
    private ShapeCamera curCamera;
    
    private bool quit = false;
    private bool restart = false;
    
    private readonly List<ShapeFlash> shapeFlashes = new();
    private readonly List<DeferredInfo> deferred = new();

    /// <summary>
    /// Add functions that draw something to the screen before the game texture is drawn to the screen.
    /// Lower keys (layers) will be drawn first.
    /// </summary>
    public readonly SortedList<int, Action> DeferredDrawingBeforeGame = new();
    /// <summary>
    /// Add functions that draw something to the screen after the game texture was drawn to the screen
    /// and before the UI texture is drawn to the screen.
    /// Lower keys (layers) will be drawn first.
    /// </summary>
    public readonly SortedList<int, Action> DeferredDrawingAfterGame = new();
    /// <summary>
    /// Add functions that draw something to the screen after the UI texture was drawn to the screen.
    /// Lower keys (layers) will be drawn first.
    /// </summary>
    public readonly SortedList<int, Action> DeferredDrawingAfterUI = new();
    
    
    private float physicsAccumulator = 0f;

    private List<ScreenTexture>? customScreenTextures = null;

    
    #endregion

    public Game(GameSettings gameSettings, WindowSettings windowSettings)
    {
        CurrentGameInstance = this;
        
        #if DEBUG
        DebugMode = true;
        ReleaseMode = false;
        #endif
        
        // this.DevelopmentDimensions = gameSettings.DevelopmentDimensions;
        Window = new(windowSettings);
        Window.OnWindowSizeChanged += ResolveOnWindowSizeChanged;
        Window.OnWindowPositionChanged += ResolveOnWindowPositionChanged;
        Window.OnMonitorChanged += ResolveOnMonitorChanged;
        Window.OnMouseVisibilityChanged += ResolveOnMouseVisibilityChanged;
        Window.OnMouseEnabledChanged += ResolveOnMouseEnabledChanged;
        Window.OnMouseEnteredScreen += ResolveOnMouseEnteredScreen;
        Window.OnMouseLeftScreen += ResolveOnMouseLeftScreen;
        
        
        Window.OnWindowFocusChanged += ResolveOnWindowFocusChanged;
        Window.OnWindowFullscreenChanged += ResolveOnWindowFullscreenChanged;
        Window.OnWindowMaximizeChanged += ResolveOnWindowMaximizeChanged;
        Window.OnWindowMinimizedChanged += ResolveOnWindowMinimizedChanged;
        Window.OnWindowHiddenChanged += ResolveOnWindowHiddenChanged;
        Window.OnWindowTopmostChanged += ResolveOnWindowTopmostChanged;

        AudioDevice = new AudioDevice();

        var fixedFramerate = gameSettings.FixedFramerate;
        if (fixedFramerate <= 0)
        {
            FixedPhysicsFramerate = -1;
            FixedPhysicsTimestep = -1;
            FixedPhysicsEnabled = false;
        }
        else
        {
            if (fixedFramerate < 30) fixedFramerate = 30;
            FixedPhysicsFramerate = fixedFramerate;
            FixedPhysicsTimestep = 1f / FixedPhysicsFramerate;
            FixedPhysicsEnabled = true;
        }
        
        curCamera = basicCamera;
        curCamera.Activate();
        curCamera.SetSize(Window.CurScreenSize);
        
        var mousePosUI = Window.MousePosition;
        // var mousePosGame = Camera.ScreenToWorld(mousePosUI);
        // var cameraArea = Camera.Area;

        // GameScreenInfo = new(cameraArea, mousePosGame);


        var screenTextureMode = gameSettings.ScreenTextureMode;
        if (screenTextureMode == ScreenTextureMode.Stretch)
        {
            gameTexture = new(gameSettings.ShaderSupportType, gameSettings.TextureFilter);
        }
        else if (screenTextureMode == ScreenTextureMode.Fixed)
        {
            gameTexture = new(gameSettings.FixedDimensions, gameSettings.ShaderSupportType, gameSettings.TextureFilter);
        }
        else if (screenTextureMode == ScreenTextureMode.NearestFixed)
        {
            gameTexture = new(gameSettings.FixedDimensions, gameSettings.ShaderSupportType, gameSettings.TextureFilter, true);
        }
        else
        {
            gameTexture = new(gameSettings.PixelationFactor, gameSettings.ShaderSupportType, gameSettings.TextureFilter);
        }
        
        gameTexture.OnTextureResized += GameTextureOnTextureResized;
        gameTexture.Initialize(Window.CurScreenSize, mousePosUI, curCamera);
        gameTexture.OnDrawGame += GameTextureOnDrawGame;
        gameTexture.OnDrawUI += GameTextureOnDrawUI;
        
        GameScreenInfo = gameTexture.GameScreenInfo;
        GameUiScreenInfo = gameTexture.GameUiScreenInfo;
        UIScreenInfo = new(Window.ScreenArea, mousePosUI);

        ShapeInput.OnInputDeviceChanged += OnInputDeviceChanged;
        ShapeInput.GamepadDeviceManager.OnGamepadConnectionChanged += OnGamepadConnectionChanged;
        
        //This sets the current directory to the executable's folder, enabling double-click launches.
        //without this, the executable has to be launched from the command line
        if (IsOSX())
        {
            string exeDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(exeDir))
            {
                Directory.SetCurrentDirectory(exeDir);
            }
            else Console.WriteLine("Failed to set current directory to executable's folder in macos.");
        }
    }
    

    public ExitCode Run(params string[] launchParameters)
    {
        this.LaunchParams = launchParameters;

        quit = false;
        restart = false;
        Raylib.SetExitKey(KeyboardKey.Null);

        StartGameloop();
        RunGameloop();
        EndGameloop();
        Raylib.CloseWindow();

        return new ExitCode(restart);
    }
    
    public ScreenTexture GetGameTexture() => gameTexture;

    /// <summary>
    /// Change the game texture.
    /// </summary>
    /// <param name="newScreenTexture"> The new screen texture to use.</param>
    /// <returns>Returns the old game texture or null if the newScreenTexture is the same as the game texture.
    /// The old ScreenTexture should be unloaded and disposed of if no longer needed!</returns>
    public ScreenTexture? ChangeGameTexture(ScreenTexture newScreenTexture)
    {
        if (gameTexture == newScreenTexture) return null;

        gameTexture.OnTextureResized -= GameTextureOnTextureResized;
        gameTexture.OnDrawGame -= GameTextureOnDrawGame;
        gameTexture.OnDrawUI -= GameTextureOnDrawUI;
        
        newScreenTexture.OnTextureResized += GameTextureOnTextureResized;
        if(!newScreenTexture.Initialized) newScreenTexture.Initialize(Window.CurScreenSize, Window.MousePosition, curCamera);
        newScreenTexture.OnDrawGame += GameTextureOnDrawGame;
        newScreenTexture.OnDrawUI += GameTextureOnDrawUI;
        
        var old = gameTexture;
        gameTexture = newScreenTexture;
        
        return old;
    }
 
    #region  Gameloop
    private void StartGameloop()
    {
        ShapeInput.KeyboardDevice.OnButtonPressed += OnKeyboardButtonPressed;
        ShapeInput.KeyboardDevice.OnButtonReleased += OnKeyboardButtonReleased;
        ShapeInput.MouseDevice.OnButtonPressed += OnMouseButtonPressed;
        ShapeInput.MouseDevice.OnButtonReleased += OnMouseButtonReleased;
        ShapeInput.GamepadDeviceManager.OnGamepadButtonPressed += OnGamepadButtonPressed;
        ShapeInput.GamepadDeviceManager.OnGamepadButtonReleased += OnGamepadButtonReleased;
        
        LoadContent();
        BeginRun();
    }

    
    private void RunGameloop()
    {
        while (!quit)
        {
            if (Raylib.WindowShouldClose())
            {
                Quit();
                continue;
            }
            
            var dt = Raylib.GetFrameTime();
            Time = Time.TickF(dt);
            
            Window.Update(dt);
            AudioDevice.Update(dt, curCamera);
            ShapeInput.Update();
            
            if (Window.MouseOnScreen)
            {
                if (ShapeInput.CurrentInputDeviceType is InputDeviceType.Keyboard or InputDeviceType.Gamepad)
                {
                    Window.MoveMouse(ChangeMousePos(dt, Window.MousePosition, Window.ScreenArea));
                }
            }
            
            var mousePosUI = Window.MousePosition; 
            gameTexture.Update(dt, Window.CurScreenSize, mousePosUI, Paused);

            if (customScreenTextures is { Count: > 0 })
            {
                for (var i = 0; i < customScreenTextures.Count; i++)
                {
                    customScreenTextures[i].Update(dt, Window.CurScreenSize, mousePosUI, Paused);
                }
            }
            
            GameScreenInfo = gameTexture.GameScreenInfo;
            GameUiScreenInfo = gameTexture.GameUiScreenInfo;
            UIScreenInfo = new(Window.ScreenArea, mousePosUI);
            
            if (!Paused)
            {
                UpdateFlashes(dt);
            }

            UpdateCursor(dt, GameScreenInfo, GameUiScreenInfo, UIScreenInfo);
            
            if (FixedPhysicsEnabled)
            {
                ResolvePreFixedUpdate();
                AdvanceFixedUpdate(dt);
            }
            else ResolveUpdate();
            
            DrawToScreen();

            ResolveDeferred();
        }
    }
    private void DrawToScreen()
    {
        gameTexture.DrawOnTexture();
        if (customScreenTextures is { Count: > 0 })
        {
            for (var i = 0; i < customScreenTextures.Count; i++)
            {
                customScreenTextures[i].DrawOnTexture();
            }
        }
        
        Raylib.BeginDrawing();
        Raylib.ClearBackground(BackgroundColorRgba.ToRayColor());

        if (DeferredDrawingBeforeGame.Count > 0)
        {
            foreach (var action in DeferredDrawingBeforeGame.Values)
            {
                action.Invoke();
            }
        }
        
        //split custom screen textures into textures to draw to the screen before the game texture
        //and textures to draw to the screen after the game texture
        List<ScreenTexture>? drawBefore = null;
        List<ScreenTexture>? drawAfter = null;
        if (customScreenTextures is { Count: > 0 })
        {
            for (var i = 0; i < customScreenTextures.Count; i++)
            {
                //negative draw order means to draw it to screen before the game texture
                if (customScreenTextures[i].DrawToScreenOrder < 0)
                {
                    drawBefore ??= new List<ScreenTexture>();//initialize if it has not been initialized yet
                    drawBefore.Add(customScreenTextures[i]);
                }
                //otherwise it will be drawn to screen after the game texture
                else
                {
                    drawAfter ??= new List<ScreenTexture>();//initialize if it has not been initialized yet
                    drawAfter.Add(customScreenTextures[i]);
                }
            }
        }

        //draw screen textures to screen before the game texture
        if (drawBefore is { Count: > 0 })
        {
            drawBefore.Sort(
                (a, b) =>
                {
                    if (a.DrawToScreenOrder < b.DrawToScreenOrder) return -1;
                    if (a.DrawToScreenOrder > b.DrawToScreenOrder) return 1;
                    return 0;
                }
            );
            for (var i = 0; i < drawBefore.Count; i++)
            {
                drawBefore[i].DrawToScreen();
            }
        }
        
        //draw game texture to screen
        gameTexture.DrawToScreen();
        
        if (DeferredDrawingAfterGame.Count > 0)
        {
            foreach (var action in DeferredDrawingAfterGame.Values)
            {
                action.Invoke();
            }
        }
        
        //draw screen textures to screen after the game texture
        if (drawAfter is { Count: > 0 })
        {
            drawAfter.Sort(
                (a, b) =>
                {
                    if (a.DrawToScreenOrder < b.DrawToScreenOrder) return -1;
                    if (a.DrawToScreenOrder > b.DrawToScreenOrder) return 1;
                    return 0;
                }
            );
            for (var i = 0; i < drawAfter.Count; i++)
            {
                drawAfter[i].DrawToScreen();
            }
        }
        
        ResolveDrawUI(UIScreenInfo);
        
        if (DeferredDrawingAfterUI.Count > 0)
        {
            foreach (var action in DeferredDrawingAfterUI.Values)
            {
                action.Invoke();
            }
        }
        
        if (Window.MouseOnScreen) DrawCursorUi(UIScreenInfo);
        
        Raylib.EndDrawing();
    }
    
    private void EndGameloop()
    {
        EndRun();
        UnloadContent();
        Window.Close();
        gameTexture.Unload();
    }
    private void GameTextureOnDrawGame(ScreenInfo gameScreenInfo, ScreenTexture texture)
    {
        ResolveDrawGame(gameScreenInfo);
        if (Window.MouseOnScreen) DrawCursorGame(gameScreenInfo);
    }
    private void GameTextureOnDrawUI(ScreenInfo gameUiScreenInfo, ScreenTexture texture)
    {
        ResolveDrawGameUI(gameUiScreenInfo);
        if (Window.MouseOnScreen) DrawCursorGameUi(gameUiScreenInfo);
    }
    private void GameTextureOnTextureResized(int w, int h)
    {
        ResolveOnGameTextureResized(w, h);
    }
    // private void GameTextureOnOnClearBackground()
    // {
    //     ResolveOnGameTextureClearBackground();
    // }
    private void AdvanceFixedUpdate(float dt)
    {
        const float maxFrameTime = 1f / 30f;
        float frameTime = dt;
        // var t = 0.0f;

        if ( frameTime > maxFrameTime ) frameTime = maxFrameTime;
        
        physicsAccumulator += frameTime;
        while ( physicsAccumulator >= FixedPhysicsTimestep )
        {
            FixedTime = FixedTime.TickF(FixedPhysicsFramerate);
            ResolveFixedUpdate();
            // t += FixedPhysicsTimestep;
            physicsAccumulator -= FixedPhysicsTimestep;
        }

        float alpha = physicsAccumulator / FixedPhysicsTimestep;
        ResolveInterpolateFixedUpdate(alpha);
    }
    
    #endregion

    #region Custom Screen Textures

    public bool AddScreenTexture(ScreenTexture texture)
    {
        if (customScreenTextures == null)
        {
            customScreenTextures = new(2) { texture };
            return true;
        }

        if (customScreenTextures.Contains(texture)) return false;

        customScreenTextures.Add(texture);
        return true;

    }
    public bool HasScreenTexture(ScreenTexture texture) => customScreenTextures?.Contains(texture) ?? false;
    public bool RemoveScreenTexture(ScreenTexture texture)
    {
        if (customScreenTextures == null) return false;
        return customScreenTextures.Remove(texture);
    }
    public int ClearScreenTextures()
    {
        if (customScreenTextures == null) return 0;

        int count = customScreenTextures.Count;
        customScreenTextures.Clear();
        return count;
    }
    
    #endregion

    #region Cursor

    // private void ResolveUpdateCursor(float dt, ScreenInfo gameInfo, ScreenInfo gameUiInfo, ScreenInfo uiInfo)
    // {
    //     UpdateCursor(dt, gameInfo, gameUiInfo, uiInfo);
    // }
    // private void ResolveDrawCursorGame(ScreenInfo gameInfo)
    // {
    //     DrawCursorGame(gameInfo);
    // }
    // private void ResolveDrawCursorGameUi(ScreenInfo gameUiInfo)
    // {
    //     DrawCursorGameUi(gameUiInfo);
    // }
    // private void ResolveDrawCursorUi(ScreenInfo uiInfo)
    // {
    //     DrawCursorUi(uiInfo);   
    // }
    protected virtual void UpdateCursor(float dt, ScreenInfo gameInfo, ScreenInfo gameUiInfo, ScreenInfo uiInfo)
    {
        
    }
    protected virtual void DrawCursorGame(ScreenInfo gameInfo)
    {
        
    }
    protected virtual void DrawCursorGameUi(ScreenInfo gameUiInfo)
    {
        
    }
    protected virtual void DrawCursorUi(ScreenInfo uiInfo)
    {
        
    }

    #endregion
    
    #region Public
    public void Restart()
    {
        restart = true;
        quit = true;
    }
    public void Quit()
    {
        restart = false;
        quit = true;
    }

    /// <summary>
    /// Switches to the new scene. Deactivate is called on the old scene and then Activate is called on the new scene.
    /// </summary>
    /// <param name="newScene"></param>
    public void GoToScene(Scene newScene)
    {
        if (newScene == CurScene) return;
        
        CurScene.ResolveDeactivate();
        CurScene.SetGameReference(null);
        
        newScene.SetGameReference(this);
        newScene.ResolveActivate(CurScene);
       
        CurScene = newScene;
    }

    public void CallDeferred(Action action, int afterFrames = 0)
    {
        deferred.Add(new(action, afterFrames));
    }
    private void ResolveDeferred()
    {
        for (int i = deferred.Count - 1; i >= 0; i--)
        {
            var info = deferred[i];
            if (info.Call()) deferred.RemoveAt(i);
        }
    }

    public void Flash(float duration, ColorRgba startColorRgba, ColorRgba endColorRgba)
    {
        if (duration <= 0.0f) return;
        if (ScreenEffectIntensity <= 0f) return;
        startColorRgba = startColorRgba.SetAlpha((byte)(startColorRgba.A * ScreenEffectIntensity));
        endColorRgba = endColorRgba.SetAlpha((byte)(endColorRgba.A * ScreenEffectIntensity));
        // byte startColorAlpha = (byte)(startColor.A * ScreenEffectIntensity);
        // startColor.A = startColorAlpha;
        // byte endColorAlpha = (byte)(endColor.A * ScreenEffectIntensity);
        // endColor.A = endColorAlpha;

        ShapeFlash flash = new(duration, startColorRgba, endColorRgba);
        shapeFlashes.Add(flash);
    }

    public void ClearFlashes() => shapeFlashes.Clear();
    
    
    public void ResetCamera() => Camera = basicCamera;

    
    #endregion
    
    #region Virtual

    /// <summary>
    /// Called first after starting the gameloop.
    /// </summary>
    protected virtual void LoadContent() { }
    /// <summary>
    /// Called after LoadContent but before the main loop has started.
    /// </summary>
    protected virtual void BeginRun() { }

    /// <summary>
    /// Called when fixed framerate is disabled
    /// </summary>
    /// <param name="time"></param>
    /// <param name="game"></param>
    /// <param name="gameUi"></param>
    /// <param name="ui"></param>
    protected virtual void Update(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui) { }
    
    
    /// <summary>
    /// This functions is called every frame before fixed update. Only called when fixed framerate is enabled.
    /// </summary>
    /// <param name="time"></param>
    /// <param name="game"></param>
    /// <param name="gameUi"></param>
    /// <param name="ui"></param>
    protected virtual void PreFixedUpdate(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui) { }
    
    /// <summary>
    /// Only called when fixed framerate is enabled. This function will be called in fixed interval.
    /// </summary>
    /// <param name="fixedTime"></param>
    /// <param name="game"></param>
    /// <param name="gameUi"></param>
    /// <param name="ui"></param>
    protected virtual void FixedUpdate(GameTime fixedTime, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui) { }
    
    /// <summary>
    /// Only called when fixed framerate is enabled. This function will be called every frame.
    /// </summary>
    /// <param name="time"></param>
    /// <param name="game"></param>
    /// <param name="gameUi"></param>
    /// <param name="ui"></param>
    /// <param name="f"></param>
    protected virtual void InterpolateFixedUpdate(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui, float f) { }
    
    protected virtual void DrawGame(ScreenInfo game) { }
    protected virtual void DrawGameUI(ScreenInfo gameUi) { }
    protected virtual void DrawUI(ScreenInfo ui) { }

    /// <summary>
    /// Called before UnloadContent is called after the main gameloop has been exited.
    /// </summary>
    protected virtual void EndRun() { }
    /// <summary>
    /// Called after EndRun before the application terminates.
    /// </summary>
    protected virtual void UnloadContent() { }
    protected virtual void OnGameTextureResized(int w, int h) { }
    
    // protected virtual void OnGameTextureClearBackground() { }
    protected virtual void OnWindowSizeChanged(DimensionConversionFactors conversion) { }
    protected virtual void OnWindowPositionChanged(Vector2 oldPos, Vector2 newPos) { }
    protected virtual void OnMonitorChanged(MonitorInfo newMonitor) { }
    protected virtual void OnPausedChanged(bool newPaused) { }
    protected virtual void OnInputDeviceChanged(InputDeviceType prevDeviceType, InputDeviceType newDeviceType) { }
    protected virtual void OnGamepadConnected(ShapeGamepadDevice gamepad) { }
    protected virtual void OnGamepadDisconnected(ShapeGamepadDevice gamepad) { }
    protected virtual void OnMouseEnteredScreen() { }
    protected virtual void OnMouseLeftScreen() { }
    protected virtual void OnMouseVisibilityChanged(bool visible) { }
    protected virtual void OnMouseEnabledChanged(bool enabled) { }
    protected virtual void OnWindowFocusChanged(bool focused) { }
    protected virtual void OnWindowFullscreenChanged(bool fullscreen) { }
    protected virtual void OnWindowMaximizeChanged(bool maximized) { }
    protected virtual void OnWindowMinimizedChanged(bool minimized) { }
    protected virtual void OnWindowHiddenChanged(bool hidden) { }
    protected virtual void OnWindowTopmostChanged(bool topmost) { }
    protected virtual Vector2 ChangeMousePos(float dt, Vector2 mousePos, Rect screenArea) => mousePos;

    protected virtual void OnButtonPressed(InputEvent e)
    {
        
    }
    protected virtual void OnButtonReleased(InputEvent e)
    {
        
    }
    #endregion

    #region Resolve
    private void UpdateFlashes(float dt)
    {
        for (int i = shapeFlashes.Count() - 1; i >= 0; i--)
        {
            var flash = shapeFlashes[i];
            flash.Update(dt);
            if (flash.IsFinished()) { shapeFlashes.RemoveAt(i); }
        }
    }

    private void OnGamepadButtonReleased(ShapeGamepadDevice gamepad, ShapeGamepadButton button) => ResolveOnButtonReleased(new(gamepad, button));
    private void OnGamepadButtonPressed(ShapeGamepadDevice gamepad, ShapeGamepadButton button) => ResolveOnButtonPressed(new(gamepad, button));
    private void OnMouseButtonReleased(ShapeMouseButton button) => ResolveOnButtonReleased(new(button));
    private void OnMouseButtonPressed(ShapeMouseButton button) => ResolveOnButtonPressed(new(button));
    private void OnKeyboardButtonReleased(ShapeKeyboardButton button) => ResolveOnButtonReleased(new(button));
    private void OnKeyboardButtonPressed(ShapeKeyboardButton button) => ResolveOnButtonPressed(new(button));
    private void ResolveOnButtonPressed(InputEvent e)
    {
        OnButtonPressed(e);
        CurScene.ResolveOnButtonPressed(e);
    }
    private void ResolveOnButtonReleased(InputEvent e)
    {
        OnButtonReleased(e);
        CurScene.ResolveOnButtonReleased(e);
    }
    private void ResolveUpdate()
    {
        Update(Time, GameScreenInfo, GameUiScreenInfo, UIScreenInfo);
        CurScene.ResolveUpdate(Time, GameScreenInfo, GameUiScreenInfo, UIScreenInfo);
    }
    private void ResolvePreFixedUpdate()
    {
        PreFixedUpdate(Time, GameScreenInfo, GameUiScreenInfo, UIScreenInfo);
        CurScene.ResolvePreFixedUpdate(Time, GameScreenInfo, GameUiScreenInfo, UIScreenInfo);
    }
    private void ResolveFixedUpdate()
    {
        FixedUpdate(FixedTime, GameScreenInfo, GameUiScreenInfo, UIScreenInfo);
        CurScene.ResolveFixedUpdate(FixedTime, GameScreenInfo, GameUiScreenInfo, UIScreenInfo);
    }
    private void ResolveInterpolateFixedUpdate(float f)
    {
        InterpolateFixedUpdate(Time, GameScreenInfo, GameUiScreenInfo, UIScreenInfo, f);
        CurScene.ResolveInterpolateFixedUpdate(Time, GameScreenInfo, GameUiScreenInfo, UIScreenInfo, f);
    }
    private void ResolveOnGameTextureResized(int w, int h)
    {
        OnGameTextureResized(w, h);
        CurScene.ResolveGameTextureResized(w, h);
    }

    // private void ResolveOnGameTextureClearBackground()
    // {
    //     OnGameTextureClearBackground();
    //     CurScene.ResolveOnGameTextureClearBackground();
    // }
    private void ResolveDrawGame(ScreenInfo game)
    {
        DrawGame(game);
        CurScene.ResolveDrawGame(game);
    }
    private void ResolveDrawGameUI(ScreenInfo gameUi)
    {
        DrawGameUI(gameUi);
        CurScene.ResolveDrawGameUI(gameUi);
    }
    private void ResolveDrawUI(ScreenInfo ui)
    {
        DrawUI(ui);
        CurScene.ResolveDrawUI(ui);
    }
    private void ResolveOnWindowSizeChanged(DimensionConversionFactors conversion)
    {
        OnWindowSizeChanged(conversion);
        CurScene.ResolveOnWindowSizeChanged(conversion);
    }
    private void ResolveOnWindowPositionChanged(Vector2 oldPos, Vector2 newPos)
    {
        //Console.WriteLine($"Window Pos: {Raylib.GetWindowPosition()}");
        OnWindowPositionChanged(oldPos, newPos);
        CurScene.ResolveOnWindowPositionChanged(oldPos, newPos);
    }
    private void ResolveOnMonitorChanged(MonitorInfo newMonitor)
    {
        OnMonitorChanged(newMonitor);
        CurScene.ResolveOnMonitorChanged(newMonitor);
    }
    private void ResolveOnPausedChanged(bool newPaused)
    {
        OnPausedChanged(newPaused);
        CurScene.ResolveOnPausedChanged(newPaused);
    }
    private void ResolveOnMouseEnteredScreen()
    {
        OnMouseEnteredScreen();
        CurScene.ResolveOnMouseEnteredScreen();
    }
    private void ResolveOnMouseLeftScreen()
    {
        OnMouseLeftScreen();
        CurScene.ResolveOnMouseLeftScreen();
    }
    private void ResolveOnMouseVisibilityChanged(bool visible)
    {
        OnMouseVisibilityChanged(visible);
        CurScene.ResolveOnMouseVisibilityChanged(visible);
    }
    private void ResolveOnMouseEnabledChanged(bool enabled)
    {
        OnMouseEnabledChanged(enabled);
        CurScene.ResolveOnMouseEnabledChanged(enabled);
    }
    private void ResolveOnWindowFocusChanged(bool focused)
    {
        OnWindowFocusChanged(focused);
        CurScene.ResolveOnWindowFocusChanged(focused);
    }
    private void ResolveOnWindowFullscreenChanged(bool fullscreen)
    {
        OnWindowFullscreenChanged(fullscreen);
        CurScene.ResolveOnWindowFullscreenChanged(fullscreen);
    }
    private void ResolveOnWindowMaximizeChanged(bool maximized)
    {
        OnWindowMaximizeChanged(maximized);
        CurScene.ResolveOnWindowMaximizeChanged(maximized);
        
    }
    private void ResolveOnWindowMinimizedChanged(bool minimized)
    {
       OnWindowMinimizedChanged(minimized);
       CurScene.ResolveOnWindowMinimizedChanged(minimized);
    }
    private void ResolveOnWindowHiddenChanged(bool hidden)
    {
        OnWindowHiddenChanged(hidden);
        CurScene.ResolveOnWindowHiddenChanged(hidden);
    }
    private void ResolveOnWindowTopmostChanged(bool topmost)
    {
        OnWindowTopmostChanged(topmost);
        CurScene.ResolveOnWindowTopmostChanged(topmost);
    }

    #endregion
    
    private void OnGamepadConnectionChanged(ShapeGamepadDevice gamepad, bool connected)
    {
        if (connected)
        {
            OnGamepadConnected(gamepad);
            CurScene.ResolveOnGamepadConnected(gamepad);
        }
        else
        {
            OnGamepadDisconnected(gamepad);
            CurScene.ResolveOnGamepadDisconnected(gamepad);
        }
    }
    private void OnInputInputDeviceChanged(InputDeviceType prevDeviceType, InputDeviceType newDeviceType)
    {
        OnInputDeviceChanged(prevDeviceType, newDeviceType);
        CurScene.ResolveOnInputDeviceChanged(prevDeviceType, newDeviceType);
    }

    
    /// <summary>
    /// Use the writeAction to write to the text file.
    /// </summary>
    /// <param name="path">The path were the file should be. A new one is created if it does not exist.</param>
    /// <param name="fileName">The name of the file. Needs a valid extension.</param>
    /// <param name="writeAction">The function that is called with the active StreamWriter. Use Write/ WriteLine functions to write.</param>
    /// <exception cref="ArgumentException">Filename has no valid extension.</exception>
    public static void WriteToFile(string path, string fileName, Action<StreamWriter> writeAction)
    {
        if (!Path.HasExtension(fileName))
        {
            throw new ArgumentException("File name must have a valid extension.");
        }
        
        try
        {
            var fullPath = Path.Combine(path, fileName);
            using (var writer = new StreamWriter(fullPath))
            {
                writeAction(writer);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("The file could not be read:");
            Console.WriteLine(e.Message);
        }
    }

    /// <summary>
    /// Use the readAction to read from the file.
    /// </summary>
    /// <param name="path">The path were the file should be. A new one is created if it does not exist.</param>
    /// <param name="fileName">The name of the file. Needs a valid extension.</param>
    /// <param name="readAction">The function that is called with the active StreamReader. Use Read/ ReadLine functions to read.</param>
    /// <exception cref="ArgumentException">Filename has no valid extension.</exception>
    public static void ReadFromFile(string path, string fileName, Action<StreamReader> readAction)
    {
        
        if (!Path.HasExtension(fileName))
        {
            throw new ArgumentException("File name must have a valid extension.");
        }
        
        var fullPath = Path.Combine(path, fileName);
        try
        {
            // Open the text file using a StreamReader.
            using (StreamReader sr = new StreamReader(fullPath))
            {
                readAction(sr);
            }
        }
        catch (Exception e)
        {
            // Print any errors to the console.
            Console.WriteLine("The file could not be read:");
            Console.WriteLine(e.Message);
        }
    }
    
    public static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct
    {
        if (typeof(TEnum).IsEnum)
        {
            if (Enum.TryParse(value, true, out TEnum parsedValue))
            {
                result = parsedValue;
                return true;
            }
        }

        result = default(TEnum);
        return false;
    }
    
}




