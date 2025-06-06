using System.Numerics;
using ShapeEngine.Core.CollisionSystem;
using ShapeEngine.Core.Shapes;
using ShapeEngine.Core.Structs;
using ShapeEngine.Input;
using ShapeEngine.Pathfinding;
using ShapeEngine.Screen;

namespace ShapeEngine.Core;

public abstract class Scene //: IUpdateable, IDrawable
{

    #region Members
    
    public bool Active { get; private set; } = false;
    /// <summary>
    /// If the scene is Active Game will be set.
    /// </summary>
    public Game? Game { get; private set; } = null;
    
    public SpawnArea? SpawnArea { get; private set; } = null;
    public CollisionHandler? CollisionHandler { get; private set; } = null;
    public Pathfinder? Pathfinder { get; private set; } = null;

    #endregion

    #region Init Handlers
    
    protected bool InitSpawnArea(Rect bounds)
    {
        if (SpawnArea != null) return false;

        SpawnArea = new(bounds);
            
        return true;
    }
    protected bool InitSpawnArea(float x, float y, float w, float h)
    {
        if (SpawnArea != null) return false;

        SpawnArea = new(x, y, w, h);
            
        return true;
    }
    protected bool InitSpawnArea(SpawnArea area)
    {
        if (SpawnArea != null) return false;
        if (SpawnArea == area) return false;

        SpawnArea = area;
            
        return true;
    }
    private bool RemoveSpawnArea()
    {
        if (SpawnArea == null) return false;

        SpawnArea.Close();
        SpawnArea = null;
            
        return true;
    }
        
    protected bool InitCollisionHandler(Rect bounds, int rows, int cols)
    {
        if (CollisionHandler != null) return false;
        CollisionHandler = new(bounds, rows, cols);
        return true;
    }
    protected bool InitCollisionHandler(CollisionHandler collisionHandler)
    {
        if (CollisionHandler != null) return false;
        if (CollisionHandler == collisionHandler) return false;
        CollisionHandler = collisionHandler; // new(Bounds, rows, cols);
        return true;
    }
    private bool RemoveCollisionHandler()
    {
        if (CollisionHandler == null) return false;
        CollisionHandler.Close();
        CollisionHandler = null;
        return true;
    }
    
    protected bool InitPathfinder(Rect bounds, int rows, int cols)
    {
        if (Pathfinder != null) return false;
        Pathfinder = new(bounds, rows, cols);
        return true;
    }
    protected bool InitPathfinder(Pathfinder pathfinder)
    {
        if (Pathfinder != null) return false;
        if (Pathfinder == pathfinder) return false;
        Pathfinder = pathfinder;
        return true;
    }
    private bool RemovePathfinder()
    {
        if (Pathfinder == null) return false;
        Pathfinder.Clear();
        Pathfinder = null;
        return true;
    }
    #endregion
    
    #region Internal

    internal void SetGameReference(Game? game) => Game = game;

    internal void ResolveActivate(Scene oldScene)
    {
        Active = true;
        OnActivate(oldScene);
    }
    internal void ResolveDeactivate()
    {
        Active = false;
        OnDeactivate();
    }

    /// <summary>
    /// Used for cleanup. Should be called once right before the scene gets deleted.
    /// </summary>
    public void Close()
    {
        RemoveSpawnArea();
        RemoveCollisionHandler();
        RemovePathfinder();
        OnClose();
    }
    internal void ResolveUpdate(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui)
    {
        SpawnArea?.Update(time, game, gameUi, ui);
        CollisionHandler?.Update(time.Delta);
        Pathfinder?.Update(time.Delta);
        
        OnUpdate(time, game, gameUi, ui);
    }
    internal void ResolvePreFixedUpdate(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui)
    {
        SpawnArea?.PreFixedUpdate(time, game, gameUi, ui);
        OnUpdate(time, game, gameUi, ui);
    }
    internal void ResolveFixedUpdate(GameTime fixedTime, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui)
    {
        SpawnArea?.FixedUpdate(fixedTime, game, gameUi, ui);
        CollisionHandler?.Update(fixedTime.Delta);
        Pathfinder?.Update(fixedTime.Delta);
        OnFixedUpdate(fixedTime, game, gameUi, ui);
    }
    internal void ResolveInterpolateFixedUpdate(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui, float f)
    {
        SpawnArea?.InterpolateFixedUpdate(time, game, gameUi, ui, f);
        OnInterpolateFixedUpdate(time, game, gameUi, ui, f);
    }
    internal void ResolveGameTextureResized(int w, int h)
    {
        OnGameTextureResized(w, h);
    }

    // internal void ResolveOnGameTextureClearBackground()
    // {
    //     OnGameTextureClearBackground();
    // }
    internal void ResolveDrawGame(ScreenInfo game)
    {
        OnPreDrawGame(game);
        SpawnArea?.DrawGame(game);
        OnDrawGame(game);
    }
    internal void ResolveDrawGameUI(ScreenInfo gameUi)
    {
        OnPreDrawGameUI(gameUi);
        SpawnArea?.DrawGameUI(gameUi);
        OnDrawGameUI(gameUi);
    }
    internal void ResolveDrawUI(ScreenInfo ui)
    {
        OnDrawUI(ui);
    }
    internal void ResolveOnWindowSizeChanged(DimensionConversionFactors conversionFactors)
    {
        OnWindowSizeChanged(conversionFactors);
    }
    internal void ResolveOnWindowPositionChanged(Vector2 oldPos, Vector2 newPos)
    {
        OnWindowPositionChanged(oldPos, newPos);
    }
    internal void ResolveOnMonitorChanged(MonitorInfo newMonitor)
    {
        OnMonitorChanged(newMonitor);   
    }
    internal void ResolveOnGamepadConnected(ShapeGamepadDevice gamepad)
    {
        OnGamepadConnected(gamepad);
    }
    internal void ResolveOnGamepadDisconnected(ShapeGamepadDevice gamepad)
    {
        OnGamepadDisconnected(gamepad);
    }
    internal void ResolveOnInputDeviceChanged(InputDeviceType prevDeviceType, InputDeviceType curDeviceType)
    {
        OnInputDeviceChanged(prevDeviceType, curDeviceType);
    }
    internal void ResolveOnPausedChanged(bool newPaused)
    {
        OnPausedChanged(newPaused);
    }
    internal void ResolveOnMouseEnteredScreen()
    {
        OnMouseEnteredScreen();
    }
    internal void ResolveOnMouseLeftScreen()
    {
        OnMouseLeftScreen();
    }
    internal void ResolveOnMouseVisibilityChanged(bool visible)
    {
        OnMouseVisibilityChanged(visible);
    }
    internal void ResolveOnMouseEnabledChanged(bool enabled)
    {
        OnMouseEnabledChanged(enabled);
    }
    internal void ResolveOnWindowFocusChanged(bool focused)
    {
        OnWindowFocusChanged(focused);
    }
    internal void ResolveOnWindowFullscreenChanged(bool fullscreen)
    {
        OnWindowFullscreenChanged(fullscreen);
    }
    internal void ResolveOnWindowMaximizeChanged(bool maximized)
    {
        OnWindowMaximizeChanged(maximized);
    }

    internal void ResolveOnWindowMinimizedChanged(bool minimized)
    {
        OnWindowMinimizedChanged(minimized);
    }

    internal void ResolveOnWindowHiddenChanged(bool hidden)
    {
        OnWindowHiddenChanged(hidden);
    }

    internal void ResolveOnWindowTopmostChanged(bool topmost)
    {
        OnWindowTopmostChanged(topmost);
    }
    public void ResolveOnButtonPressed(InputEvent e)
    {
        OnButtonPressed(e);
    }
    public void ResolveOnButtonReleased(InputEvent e)
    {
        OnButtonReleased(e);
    }
    #endregion
    
    #region Protected Virtual
    protected abstract void OnActivate(Scene oldScene);
    protected abstract void OnDeactivate();

    /// <summary>
    /// Used for cleanup. Should be called once right before the scene gets deleted.
    /// </summary>
    protected virtual void OnClose()
    {
        
    }
    protected virtual void OnGameTextureResized(int w, int h) { }
   
    // protected virtual void OnGameTextureClearBackground() { }
    
    /// <summary>
    /// Called every frame. Called before FixedUpdate if fixed framerate is enabled.
    /// </summary>
    /// <param name="time"></param>
    /// <param name="game"></param>
    /// <param name="gameUi"></param>
    /// <param name="ui"></param>
    protected virtual void OnUpdate(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui) { }
    
    // /// <summary>
    // /// Only called when fixed framerate is enabled. Called every frame before fixed update.
    // /// </summary>
    // /// <param name="time"></param>
    // /// <param name="game"></param>
    // /// <param name="gameUi"></param>
    // /// <param name="ui"></param>
    // protected virtual void OnHandleInput(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui) { }
    
    /// <summary>
    /// Only called when fixed framerate is enabled. Called in fixed interval.
    /// </summary>
    /// <param name="fixedTime"></param>
    /// <param name="game"></param>
    /// <param name="gameUi"></param>
    /// <param name="ui"></param>
    protected virtual void OnFixedUpdate(GameTime fixedTime, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui) { }
    
    /// <summary>
    /// Only called when fixed framerate is enabled. Called every frame after all fixed update calls.
    /// </summary>
    /// <param name="time"></param>
    /// <param name="game"></param>
    /// <param name="gameUi"></param>
    /// <param name="ui"></param>
    /// <param name="f"></param>
    protected virtual void OnInterpolateFixedUpdate(GameTime time, ScreenInfo game, ScreenInfo gameUi, ScreenInfo ui, float f) { }
    /// <summary>
    /// Called before SpawnArea DrawGame is called.
    /// </summary>
    /// <param name="game"></param>
    protected virtual void OnPreDrawGame(ScreenInfo game) { }
    /// <summary>
    /// Draw your game. Is affected by screen shaders and the camera.
    /// </summary>
    /// <param name="game"></param>
    protected virtual void OnDrawGame(ScreenInfo game) { }
        
    /// <summary>
    /// Called before SpawnArea DrawGameUI is called
    /// </summary>
    /// <param name="ui"></param>
    protected virtual void OnPreDrawGameUI(ScreenInfo ui) { }
    /// <summary>
    /// Draw your game ui. Is not affected by screen shaders and not by the camera but uses the same ScreenTextureMode as DrawGame.
    /// </summary>
    /// <param name="gameUi"></param>
    protected virtual void OnDrawGameUI(ScreenInfo gameUi) { }
        
    /// <summary>
    /// Draw your main ui. Is NOT affected by screen shaders and NOT affected by the camera.
    /// </summary>
    protected virtual void OnDrawUI(ScreenInfo ui) { }
    protected virtual void OnWindowSizeChanged(DimensionConversionFactors conversionFactors) { }
    protected virtual void OnWindowPositionChanged(Vector2 oldPos, Vector2 newPos){}
    protected virtual void OnMonitorChanged(MonitorInfo newMonitor){}
    protected virtual void OnGamepadConnected(ShapeGamepadDevice gamepad){}
    protected virtual void OnGamepadDisconnected(ShapeGamepadDevice gamepad){}
    protected virtual void OnInputDeviceChanged(InputDeviceType prevDeviceType, InputDeviceType curDeviceType){}
    protected virtual void OnPausedChanged(bool newPaused) { }
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
    protected virtual void OnButtonPressed(InputEvent e) { }
    protected virtual void OnButtonReleased(InputEvent e) { }
    
    #endregion
    
}