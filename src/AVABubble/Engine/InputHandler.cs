using Avalonia.Input;

namespace AVABubble.Engine;

/// <summary>
/// Keyboard and gamepad boiled down to the controls: left or right, jump, and blow.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly ControllerManager _pad = new(0);

    public void OnKeyDown(Key key)
    {
        _held.Remove(key);
        _held.Add(key);
    }

    public void OnKeyUp(Key key) => _held.Remove(key);
    public bool Has(Key key) => _held.Contains(key);

    public void Update() => _pad.Update();

    public Controls Controls
    {
        get
        {
            double dx = 0;
            for (int i = _held.Count - 1; i >= 0 && dx == 0; i--)
                dx = _held[i] is Key.Left or Key.A or Key.NumPad4 ? -1 : _held[i] is Key.Right or Key.D or Key.NumPad6 ? 1 : 0;
            if (dx == 0) dx = _pad.DPadLeft ? -1 : _pad.DPadRight ? 1 : Math.Abs(_pad.LeftStickX) > 0.3f ? Math.Sign(_pad.LeftStickX) : 0;
            bool jump = Has(Key.X) || Has(Key.Up) || Has(Key.W) || Has(Key.Space) || _pad.ButtonA;
            bool blow = Has(Key.Z) || Has(Key.LeftCtrl) || Has(Key.RightCtrl) || _pad.ButtonB || _pad.ButtonX;
            return new Controls(dx, jump, blow);
        }
    }

    public bool Start => Has(Key.Enter) || Has(Key.Space) || Has(Key.D1) || _pad.ButtonStart || _pad.ButtonA;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
