namespace DalaMock.Core.Extensions;

public static class KeyEventExtension
{
    public static VirtualKey ToKeyState(this KeyEvent keyEvent)
    {
        var keyState = default(VirtualKey);
        switch (keyEvent.Key)
        {
            case Key.A:
                keyState = VirtualKey.A;
                break;
            case Key.B:
                keyState = VirtualKey.B;
                break;
            case Key.C:
                keyState = VirtualKey.C;
                break;
            case Key.D:
                keyState = VirtualKey.D;
                break;
            case Key.E:
                keyState = VirtualKey.E;
                break;
            case Key.F:
                keyState = VirtualKey.F;
                break;
            case Key.G:
                keyState = VirtualKey.G;
                break;
            case Key.H:
                keyState = VirtualKey.H;
                break;
            case Key.I:
                keyState = VirtualKey.I;
                break;
            case Key.K:
                keyState = VirtualKey.K;
                break;
            case Key.L:
                keyState = VirtualKey.L;
                break;
            case Key.M:
                keyState = VirtualKey.M;
                break;
            case Key.N:
                keyState = VirtualKey.N;
                break;
            case Key.O:
                keyState = VirtualKey.O;
                break;
            case Key.P:
                keyState = VirtualKey.P;
                break;
            case Key.Q:
                keyState = VirtualKey.Q;
                break;
            case Key.R:
                keyState = VirtualKey.R;
                break;
            case Key.S:
                keyState = VirtualKey.S;
                break;
            case Key.T:
                keyState = VirtualKey.T;
                break;
            case Key.U:
                keyState = VirtualKey.U;
                break;
            case Key.V:
                keyState = VirtualKey.V;
                break;
            case Key.W:
                keyState = VirtualKey.W;
                break;
            case Key.X:
                keyState = VirtualKey.X;
                break;
            case Key.Y:
                keyState = VirtualKey.Z;
                break;
            case Key.ShiftLeft:
                keyState = VirtualKey.SHIFT;
                break;
            case Key.ControlLeft:
                keyState = VirtualKey.CONTROL;
                break;
            case Key.AltLeft:
                keyState = VirtualKey.MENU;
                break;
            case Key.ShiftRight:
                keyState = VirtualKey.SHIFT;
                break;
            case Key.ControlRight:
                keyState = VirtualKey.CONTROL;
                break;
            case Key.AltRight:
                keyState = VirtualKey.MENU;
                break;
        }

        return keyState;
    }
}
