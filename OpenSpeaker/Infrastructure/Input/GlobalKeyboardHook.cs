using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using OpenSpeaker.Infrastructure.Logging;
namespace OpenSpeaker.Input;

public readonly record struct KeyEvent(int VirtualKey, bool IsDown);

public sealed class GlobalKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint WM_QUIT = 0x0012;
    private const int MaxQueuedEvents = 4096;
    private const long RetryIntervalMs = 5000;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private readonly ConcurrentQueue<KeyEvent> _events = new();
    private readonly LowLevelKeyboardProc _proc;
    private readonly IAppLogger? _logger;
    private readonly object _lifecycle = new();
    private readonly ManualResetEventSlim _started = new(false);

    private Thread? _thread;
    private uint _threadId;
    private volatile bool _alive;
    private volatile bool _disposed;
    private long _nextRetryAt;

    public GlobalKeyboardHook(IAppLogger? logger = null)
    {
        _logger = logger;
        _proc = HookCallback;
    }

    public bool IsInstalled => _alive;

    public event Action? Reinstalled;

    public void Install()
    {
        lock (_lifecycle)
        {
            if (_disposed || _alive) return;
            if (_thread is { IsAlive: true }) return;

            _thread = new Thread(HookThreadMain)
            {
                IsBackground = true,
                Name = "OpenSpeaker.KeyboardHook",
                Priority = ThreadPriority.AboveNormal
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _started.Reset();
            _thread.Start();
            _started.Wait(TimeSpan.FromSeconds(2));
        }
    }

    public void EnsureInstalled()
    {
        if (_disposed || _alive) return;
        var now = Environment.TickCount64;
        if (now < _nextRetryAt) return;
        _nextRetryAt = now + RetryIntervalMs;

        lock (_lifecycle)
        {
            if (_disposed || _alive) return;
            if (_thread is { IsAlive: true }) return;
            _thread = null;
        }
        Install();
        if (_alive) Reinstalled?.Invoke();
    }

    public KeyEvent[] DrainEvents()
    {
        if (_events.IsEmpty) return Array.Empty<KeyEvent>();
        var drained = new List<KeyEvent>();
        while (_events.TryDequeue(out var e)) drained.Add(e);
        return drained.ToArray();
    }

    public static bool IsPhysicallyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private void HookThreadMain()
    {
        _threadId = GetCurrentThreadId();
        var hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero)
        {
            _logger?.Error($"[Keybind] Failed to install keyboard hook, error {Marshal.GetLastWin32Error()}");
            _started.Set();
            return;
        }

        _alive = true;
        _logger?.Info("[Keybind] Global keyboard hook installed (keys are captured while OpenSpeaker is unfocused; " +
                      "input sent to elevated windows stays invisible unless OpenSpeaker also runs elevated)");
        _started.Set();

        try
        {
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        finally
        {
            _alive = false;
            UnhookWindowsHookEx(hook);
            _logger?.Info("[Keybind] Global keyboard hook removed");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam;
            var vk = Marshal.ReadInt32(lParam);
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) Enqueue(new KeyEvent(vk, true));
            else if (msg == WM_KEYUP || msg == WM_SYSKEYUP) Enqueue(new KeyEvent(vk, false));
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void Enqueue(KeyEvent e)
    {
        while (_events.Count >= MaxQueuedEvents) _events.TryDequeue(out _);
        _events.Enqueue(e);
    }

    public void Dispose()
    {
        Thread? thread;
        lock (_lifecycle)
        {
            if (_disposed) return;
            _disposed = true;
            thread = _thread;
            _thread = null;
        }

        if (thread is { IsAlive: true })
        {
            PostThreadMessage(_threadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
            if (!thread.Join(TimeSpan.FromSeconds(2)))
                _logger?.Warn("[Keybind] Keyboard hook thread did not exit in time");
        }

        _events.Clear();
        _started.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
