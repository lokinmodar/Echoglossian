namespace DalaMock.Core.Imgui;

public unsafe class RawSdl2Window : ISdl2Window
{
    private readonly Sdl2Window sdl2Window;

    /// <summary>
    /// Initializes a new instance of the <see cref="RawSdl2Window"/> class.
    /// </summary>
    /// <param name="sdl2Window"></param>
    public RawSdl2Window(Sdl2Window sdl2Window)
    {
        this.sdl2Window = sdl2Window;
    }

    /// <inheritdoc/>
    public bool LimitPollRate
    {
        get => this.sdl2Window.LimitPollRate;
        set => this.sdl2Window.LimitPollRate = value;
    }

    /// <inheritdoc/>
    public float PollIntervalInMs
    {
        get => this.sdl2Window.PollIntervalInMs;
        set => this.sdl2Window.PollIntervalInMs = value;
    }

    /// <inheritdoc/>
    public int X
    {
        get => this.sdl2Window.X;
        set => this.sdl2Window.X = value;
    }

    /// <inheritdoc/>
    public int Y
    {
        get => this.sdl2Window.Y;
        set => this.sdl2Window.Y = value;
    }

    /// <inheritdoc/>
    public int Width
    {
        get => this.sdl2Window.Width;
        set => this.sdl2Window.Width = value;
    }

    /// <inheritdoc/>
    public int Height
    {
        get => this.sdl2Window.Height;
        set => this.sdl2Window.Height = value;
    }

    /// <inheritdoc/>
    public IntPtr Handle => this.sdl2Window.Handle;

    /// <inheritdoc/>
    public string Title
    {
        get => this.sdl2Window.Title;
        set => this.sdl2Window.Title = value;
    }

    /// <inheritdoc/>
    public WindowState WindowState
    {
        get => this.sdl2Window.WindowState;
        set => this.sdl2Window.WindowState = value;
    }

    /// <inheritdoc/>
    public bool Exists => this.sdl2Window.Exists;

    /// <inheritdoc/>
    public bool Visible
    {
        get => this.sdl2Window.Visible;
        set => this.sdl2Window.Visible = value;
    }

    /// <inheritdoc/>
    public Vector2 ScaleFactor => this.sdl2Window.ScaleFactor;

    /// <inheritdoc/>
    public Rectangle Bounds => this.sdl2Window.Bounds;

    /// <inheritdoc/>
    public bool CursorVisible
    {
        get => this.sdl2Window.CursorVisible;
        set => this.sdl2Window.CursorVisible = value;
    }

    /// <inheritdoc/>
    public float Opacity
    {
        get => this.sdl2Window.Opacity;
        set => this.sdl2Window.Opacity = value;
    }

    /// <inheritdoc/>
    public bool Focused => this.sdl2Window.Focused;

    /// <inheritdoc/>
    public bool Resizable
    {
        get => this.sdl2Window.Resizable;
        set => this.sdl2Window.Resizable = value;
    }

    /// <inheritdoc/>
    public bool BorderVisible
    {
        get => this.sdl2Window.BorderVisible;
        set => this.sdl2Window.BorderVisible = value;
    }

    /// <inheritdoc/>
    public IntPtr SdlWindowHandle => this.sdl2Window.SdlWindowHandle;

    /// <inheritdoc/>
    public Vector2 MouseDelta => this.sdl2Window.MouseDelta;

    /// <inheritdoc/>
    public event SysAction? Resized
    {
        add => this.sdl2Window.Resized += value;
        remove => this.sdl2Window.Resized -= value;
    }

    /// <inheritdoc/>
    public event SysAction? Closing
    {
        add => this.sdl2Window.Closing += value;
        remove => this.sdl2Window.Closing -= value;
    }

    /// <inheritdoc/>
    public event SysAction? Closed
    {
        add => this.sdl2Window.Closed += value;
        remove => this.sdl2Window.Closed -= value;
    }

    /// <inheritdoc/>
    public event SysAction? FocusLost
    {
        add => this.sdl2Window.FocusLost += value;
        remove => this.sdl2Window.FocusLost -= value;
    }

    /// <inheritdoc/>
    public event SysAction? FocusGained
    {
        add => this.sdl2Window.FocusGained += value;
        remove => this.sdl2Window.FocusGained -= value;
    }

    /// <inheritdoc/>
    public event SysAction? Shown
    {
        add => this.sdl2Window.Shown += value;
        remove => this.sdl2Window.Shown -= value;
    }

    /// <inheritdoc/>
    public event SysAction? Hidden
    {
        add => this.sdl2Window.Hidden += value;
        remove => this.sdl2Window.Hidden -= value;
    }

    /// <inheritdoc/>
    public event SysAction? MouseEntered
    {
        add => this.sdl2Window.MouseEntered += value;
        remove => this.sdl2Window.MouseEntered -= value;
    }

    /// <inheritdoc/>
    public event SysAction? MouseLeft
    {
        add => this.sdl2Window.MouseLeft += value;
        remove => this.sdl2Window.MouseLeft -= value;
    }

    /// <inheritdoc/>
    public event SysAction? Exposed
    {
        add => this.sdl2Window.Exposed += value;
        remove => this.sdl2Window.Exposed -= value;
    }

    /// <inheritdoc/>
    public event Action<Point>? Moved
    {
        add => this.sdl2Window.Moved += value;
        remove => this.sdl2Window.Moved -= value;
    }

    /// <inheritdoc/>
    public event Action<MouseWheelEventArgs>? MouseWheel
    {
        add => this.sdl2Window.MouseWheel += value;
        remove => this.sdl2Window.MouseWheel -= value;
    }

    /// <inheritdoc/>
    public event Action<MouseMoveEventArgs>? MouseMove
    {
        add => this.sdl2Window.MouseMove += value;
        remove => this.sdl2Window.MouseMove -= value;
    }

    /// <inheritdoc/>
    public event Action<MouseEvent>? MouseDown
    {
        add => this.sdl2Window.MouseDown += value;
        remove => this.sdl2Window.MouseDown -= value;
    }

    /// <inheritdoc/>
    public event Action<MouseEvent>? MouseUp
    {
        add => this.sdl2Window.MouseUp += value;
        remove => this.sdl2Window.MouseUp -= value;
    }

    /// <inheritdoc/>
    public event Action<KeyEvent>? KeyDown
    {
        add => this.sdl2Window.KeyDown += value;
        remove => this.sdl2Window.KeyDown -= value;
    }

    /// <inheritdoc/>
    public event Action<KeyEvent>? KeyUp
    {
        add => this.sdl2Window.KeyUp += value;
        remove => this.sdl2Window.KeyUp -= value;
    }

    /// <inheritdoc/>
    public event Action<DragDropEvent>? DragDrop
    {
        add => this.sdl2Window.DragDrop += value;
        remove => this.sdl2Window.DragDrop -= value;
    }

    /// <inheritdoc/>
    public Point ClientToScreen(Point p)
    {
        return this.sdl2Window.ClientToScreen(p);
    }

    /// <inheritdoc/>
    public void SetMousePosition(Vector2 position)
    {
        this.sdl2Window.SetMousePosition(position);
    }

    /// <inheritdoc/>
    public void SetMousePosition(int x, int y)
    {
        this.sdl2Window.SetMousePosition(x, y);
    }

    /// <inheritdoc/>
    public void SetCloseRequestedHandler(Func<bool> handler)
    {
        this.sdl2Window.SetCloseRequestedHandler(handler);
    }

    /// <inheritdoc/>
    public void Close()
    {
        this.sdl2Window.Close();
    }

    /// <inheritdoc/>
    public InputSnapshot PumpEvents()
    {
        return this.sdl2Window.PumpEvents();
    }

    /// <inheritdoc/>
    public void PumpEvents(SDLEventHandler eventHandler)
    {
        this.sdl2Window.PumpEvents(eventHandler);
    }

    /// <inheritdoc/>
    public Point ScreenToClient(Point p)
    {
        return this.sdl2Window.ScreenToClient(p);
    }
}
