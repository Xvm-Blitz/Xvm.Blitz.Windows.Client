using Avalonia.Input;
using Avalonia.Threading;
using Xvm.Blitz.Windows.Client.Core.WindowsApis;
using Xvm.Blitz.Windows.Client.UI.Windows;

namespace Xvm.Blitz.Windows.Client.UI.HotKeys;

public static class GlobalHotkey
{
    private static Timer? _keyCheckTimer;

    private static bool _lastKeyState;

    private static KeyGesture? _currentKeyGesture;

    private static DateTime _lastTriggerTime = DateTime.MinValue;

    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(200);

    public static void StartMonitoring(
        string hotkey,
        bool withCtrl,
        bool withAlt,
        bool withShift)
    {
        var key = ParseKey(hotkey);
        var modifiers = KeyModifiers.None;

        if (withCtrl)
            modifiers |= KeyModifiers.Control;
        if (withAlt)
            modifiers |= KeyModifiers.Alt;
        if (withShift)
            modifiers |= KeyModifiers.Shift;

        var keyGesture = new KeyGesture(key, modifiers);
        StartMonitoring(keyGesture);
    }

    public static void StopMonitoring()
    {
        _keyCheckTimer?.Dispose();
        _keyCheckTimer = null;
    }

    private static void StartMonitoring(KeyGesture keyGesture)
    {
        _currentKeyGesture = keyGesture;
        _keyCheckTimer = new Timer(
            CheckHotkey,
            null,
            0,
            50);
    }

    private static void CheckHotkey(object? state)
    {
        if (_currentKeyGesture == null)
            return;

        try
        {
            var virtualKey = GetVirtualKey(_currentKeyGesture.Key);
            var keyPressed = (WindowsApi.GetAsyncKeyState((int)virtualKey) & 0x8000) != 0;
            var ctrlPressed = (WindowsApi.GetAsyncKeyState(0x11) & 0x8000) != 0;
            var altPressed = (WindowsApi.GetAsyncKeyState(0x12) & 0x8000) != 0;
            var shiftPressed = (WindowsApi.GetAsyncKeyState(0x10) & 0x8000) != 0;

            var expectedCtrl = _currentKeyGesture.KeyModifiers.HasFlag(KeyModifiers.Control);
            var expectedAlt = _currentKeyGesture.KeyModifiers.HasFlag(KeyModifiers.Alt);
            var expectedShift = _currentKeyGesture.KeyModifiers.HasFlag(KeyModifiers.Shift);

            var currentState = keyPressed &&
                               ctrlPressed == expectedCtrl &&
                               altPressed == expectedAlt &&
                               shiftPressed == expectedShift;

            if (currentState && !_lastKeyState)
            {
                var now = DateTime.UtcNow;

                if (now - _lastTriggerTime >= DebounceInterval)
                {
                    _lastTriggerTime = now;
                    Dispatcher.UIThread.InvokeAsync(() => App.MainWindow?.ViewModel.ToggleWindowsVisibility());
                }
            }

            _lastKeyState = currentState;
        }
        catch
        {
            // ignored
        }
    }

    private static Key ParseKey(string keyString)
    {
        return keyString.ToUpperInvariant() switch
        {
            "A" => Key.A, "B" => Key.B, "C" => Key.C, "D" => Key.D, "E" => Key.E,
            "F" => Key.F, "G" => Key.G, "H" => Key.H, "I" => Key.I, "J" => Key.J,
            "K" => Key.K, "L" => Key.L, "M" => Key.M, "N" => Key.N, "O" => Key.O,
            "P" => Key.P, "Q" => Key.Q, "R" => Key.R, "S" => Key.S, "T" => Key.T,
            "U" => Key.U, "V" => Key.V, "W" => Key.W, "X" => Key.X, "Y" => Key.Y,
            "Z" => Key.Z,
            "0" => Key.D0, "1" => Key.D1, "2" => Key.D2, "3" => Key.D3, "4" => Key.D4,
            "5" => Key.D5, "6" => Key.D6, "7" => Key.D7, "8" => Key.D8, "9" => Key.D9,
            "F1" => Key.F1, "F2" => Key.F2, "F3" => Key.F3, "F4" => Key.F4, "F5" => Key.F5,
            "F6" => Key.F6, "F7" => Key.F7, "F8" => Key.F8, "F9" => Key.F9, "F10" => Key.F10,
            "F11" => Key.F11, "F12" => Key.F12,
            _ => Key.H
        };
    }

    private static uint GetVirtualKey(Key key)
    {
        return key switch
        {
            Key.A => 0x41, Key.B => 0x42, Key.C => 0x43, Key.D => 0x44, Key.E => 0x45,
            Key.F => 0x46, Key.G => 0x47, Key.H => 0x48, Key.I => 0x49, Key.J => 0x4A,
            Key.K => 0x4B, Key.L => 0x4C, Key.M => 0x4D, Key.N => 0x4E, Key.O => 0x4F,
            Key.P => 0x50, Key.Q => 0x51, Key.R => 0x52, Key.S => 0x53, Key.T => 0x54,
            Key.U => 0x55, Key.V => 0x56, Key.W => 0x57, Key.X => 0x58, Key.Y => 0x59,
            Key.Z => 0x5A,
            Key.D0 => 0x30, Key.D1 => 0x31, Key.D2 => 0x32, Key.D3 => 0x33, Key.D4 => 0x34,
            Key.D5 => 0x35, Key.D6 => 0x36, Key.D7 => 0x37, Key.D8 => 0x38, Key.D9 => 0x39,
            Key.F1 => 0x70, Key.F2 => 0x71, Key.F3 => 0x72, Key.F4 => 0x73, Key.F5 => 0x74,
            Key.F6 => 0x75, Key.F7 => 0x76, Key.F8 => 0x77, Key.F9 => 0x78, Key.F10 => 0x79,
            Key.F11 => 0x7A, Key.F12 => 0x7B,
            _ => 0x48
        };
    }
}