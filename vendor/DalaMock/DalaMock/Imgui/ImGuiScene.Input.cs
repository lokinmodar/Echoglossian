namespace DalaMock.Core.Imgui;

/// <summary>
/// This file contains everything related to handling imgui input.
/// </summary>
public partial class ImGuiScene
{
    private bool altDown;
    private bool controlDown;
    private ImGuiMouseCursor? mouseCursor;
    private bool shiftDown;
    private bool winKeyDown;

    private void UpdateImGuiInput(InputSnapshot snapshot)
    {
        var io = ImGui.GetIO();

        var mousePosition = snapshot.MousePosition;

        // Determine if any of the mouse buttons were pressed during this snapshot period, even if they are no longer held.
        var leftPressed = false;
        var middlePressed = false;
        var rightPressed = false;
        foreach (var me in snapshot.MouseEvents)
        {
            if (me.Down)
            {
                switch (me.MouseButton)
                {
                    case MouseButton.Left:
                        leftPressed = true;
                        break;
                    case MouseButton.Middle:
                        middlePressed = true;
                        break;
                    case MouseButton.Right:
                        rightPressed = true;
                        break;
                }
            }
        }

        io.MouseDown[0] = leftPressed || snapshot.IsMouseDown(MouseButton.Left);
        io.MouseDown[1] = rightPressed || snapshot.IsMouseDown(MouseButton.Right);
        io.MouseDown[2] = middlePressed || snapshot.IsMouseDown(MouseButton.Middle);
        io.MousePos = mousePosition;
        io.MouseWheel = snapshot.WheelDelta;

        if (this.mouseCursor != ImGui.GetMouseCursor())
        {
            this.mouseCursor = ImGui.GetMouseCursor();
            var nativeCursor = Sdl2Native.SDL_CreateSystemCursor(this.ToSdlCursor(this.mouseCursor.Value));
            Sdl2Native.SDL_SetCursor(nativeCursor);
        }

        var keyCharPresses = snapshot.KeyCharPresses;
        for (var i = 0; i < keyCharPresses.Count; i++)
        {
            var c = keyCharPresses[i];
            io.AddInputCharacter(c);
        }

        var keyEvents = snapshot.KeyEvents;
        for (var i = 0; i < keyEvents.Count; i++)
        {
            var keyEvent = keyEvents[i];
            io.KeysDown[(int)keyEvent.Key] = keyEvent.Down;
            if (keyEvent.Key == Key.ControlLeft)
            {
                this.controlDown = keyEvent.Down;
            }

            if (keyEvent.Key == Key.ShiftLeft)
            {
                this.shiftDown = keyEvent.Down;
            }

            if (keyEvent.Key == Key.AltLeft)
            {
                this.altDown = keyEvent.Down;
            }

            if (keyEvent.Key == Key.WinLeft)
            {
                this.winKeyDown = keyEvent.Down;
            }
        }

        io.KeyCtrl = this.controlDown;
        io.KeyAlt = this.altDown;
        io.KeyShift = this.shiftDown;
        io.KeySuper = this.winKeyDown;
    }

    private void SetKeyMappings()
    {
        var io = ImGui.GetIO();
        io.KeyMap[(int)ImGuiKey.Tab] = (int)Key.Tab;
        io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)Key.Left;
        io.KeyMap[(int)ImGuiKey.RightArrow] = (int)Key.Right;
        io.KeyMap[(int)ImGuiKey.UpArrow] = (int)Key.Up;
        io.KeyMap[(int)ImGuiKey.DownArrow] = (int)Key.Down;
        io.KeyMap[(int)ImGuiKey.PageUp] = (int)Key.PageUp;
        io.KeyMap[(int)ImGuiKey.PageDown] = (int)Key.PageDown;
        io.KeyMap[(int)ImGuiKey.Home] = (int)Key.Home;
        io.KeyMap[(int)ImGuiKey.End] = (int)Key.End;
        io.KeyMap[(int)ImGuiKey.Delete] = (int)Key.Delete;
        io.KeyMap[(int)ImGuiKey.Backspace] = (int)Key.BackSpace;
        io.KeyMap[(int)ImGuiKey.Enter] = (int)Key.Enter;
        io.KeyMap[(int)ImGuiKey.Escape] = (int)Key.Escape;
        io.KeyMap[(int)ImGuiKey.Space] = (int)Key.Space;
        io.KeyMap[(int)ImGuiKey.A] = (int)Key.A;
        io.KeyMap[(int)ImGuiKey.C] = (int)Key.C;
        io.KeyMap[(int)ImGuiKey.V] = (int)Key.V;
        io.KeyMap[(int)ImGuiKey.X] = (int)Key.X;
        io.KeyMap[(int)ImGuiKey.Y] = (int)Key.Y;
        io.KeyMap[(int)ImGuiKey.Z] = (int)Key.Z;
    }

    private static IntPtr clipboardTextPtr = IntPtr.Zero;

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GetClipboardText();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe int SDL_SetClipboardText(byte* text);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_free(IntPtr mem);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe byte* GetClipboardTextImpl(void* userData)
    {
        if (clipboardTextPtr != IntPtr.Zero)
        {
            SDL_free(clipboardTextPtr);
            clipboardTextPtr = IntPtr.Zero;
        }

        clipboardTextPtr = SDL_GetClipboardText();
        return (byte*)clipboardTextPtr;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void SetClipboardTextImpl(void* userData, byte* text)
    {
        SDL_SetClipboardText(text);
    }

    private unsafe void SetClipboardFunctions()
    {
        var io = ImGui.GetIO();
        io.GetClipboardTextFn = (void*)(delegate* unmanaged[Cdecl]<void*, byte*>)&GetClipboardTextImpl;
        io.SetClipboardTextFn = (void*)(delegate* unmanaged[Cdecl]<void*, byte*, void>)&SetClipboardTextImpl;
        io.ClipboardUserData = null;
    }

    private SDL_SystemCursor ToSdlCursor(ImGuiMouseCursor cursor)
    {
        switch (cursor)
        {
            case ImGuiMouseCursor.Arrow:
                return SDL_SystemCursor.Arrow;
            case ImGuiMouseCursor.Hand:
                return SDL_SystemCursor.Hand;
            case ImGuiMouseCursor.NotAllowed:
                return SDL_SystemCursor.No;
            case ImGuiMouseCursor.ResizeAll:
                return SDL_SystemCursor.SizeAll;
            case ImGuiMouseCursor.TextInput:
                return SDL_SystemCursor.IBeam;
            case ImGuiMouseCursor.ResizeEw:
                return SDL_SystemCursor.SizeWE;
            case ImGuiMouseCursor.ResizeNs:
                return SDL_SystemCursor.SizeNS;
            case ImGuiMouseCursor.ResizeNesw:
                return SDL_SystemCursor.SizeNESW;
            case ImGuiMouseCursor.ResizeNwse:
                return SDL_SystemCursor.SizeNWSE;
        }

        return SDL_SystemCursor.WaitArrow;
    }
}
