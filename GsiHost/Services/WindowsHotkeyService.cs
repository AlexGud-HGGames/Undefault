using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Core.Models;
using GsiHost.Configuration;
using GsiHost.Dtos;
using Microsoft.Extensions.Options;

namespace GsiHost.Services;

public sealed class WindowsHotkeyService : IHostedService
{
    private const int WmHotkey = 0x0312;
    private const int WmQuit = 0x0012;

    private readonly KeybindOptions _options;
    private readonly UserActionService _userActions;
    private readonly ILogger<WindowsHotkeyService> _logger;
    private readonly ConcurrentDictionary<int, KeybindBindingOptions> _bindingsById = new();
    private Thread? _messageThread;
    private uint _messageThreadId;
    private int _nextHotkeyId = 100;

    public WindowsHotkeyService(
        IOptions<KeybindOptions> options,
        UserActionService userActions,
        ILogger<WindowsHotkeyService> logger)
    {
        _options = options.Value;
        _userActions = userActions;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.CompletedTask;
        }

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("Keybind capture is enabled, but global hotkeys are only supported on Windows.");
            return Task.CompletedTask;
        }

        var parsedBindings = _options.Bindings
            .Select(binding => new { Binding = binding, Hotkey = HotkeyDefinition.TryParse(binding.Key) })
            .Where(item => item.Hotkey is not null && !string.IsNullOrWhiteSpace(item.Binding.EventKey))
            .ToList();

        if (parsedBindings.Count == 0)
        {
            _logger.LogWarning("Keybind capture is enabled, but no valid keybinds are configured.");
            return Task.CompletedTask;
        }

        _messageThread = new Thread(() => RunMessageLoop(parsedBindings.Select(item => (item.Binding, item.Hotkey!.Value)).ToList()))
        {
            IsBackground = true,
            Name = "UndefaultIt Hotkeys"
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_messageThread is null)
        {
            return Task.CompletedTask;
        }

        if (_messageThreadId != 0)
        {
            PostThreadMessage(_messageThreadId, WmQuit, UIntPtr.Zero, IntPtr.Zero);
        }

        if (!_messageThread.Join(TimeSpan.FromSeconds(2)))
        {
            _logger.LogWarning("Timed out waiting for hotkey message loop to stop.");
        }

        return Task.CompletedTask;
    }

    private void RunMessageLoop(IReadOnlyList<(KeybindBindingOptions Binding, HotkeyDefinition Hotkey)> bindings)
    {
        _messageThreadId = GetCurrentThreadId();
        try
        {
            RegisterBindings(bindings);

            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                if (message.message != WmHotkey)
                {
                    continue;
                }

                var id = unchecked((int)message.wParam.ToUInt32());
                if (_bindingsById.TryGetValue(id, out var binding))
                {
                    HandleHotkey(binding);
                }
            }
        }
        finally
        {
            foreach (var id in _bindingsById.Keys)
            {
                UnregisterHotKey(IntPtr.Zero, id);
            }

            _bindingsById.Clear();
            _messageThreadId = 0;
        }
    }

    private void RegisterBindings(IReadOnlyList<(KeybindBindingOptions Binding, HotkeyDefinition Hotkey)> bindings)
    {
        foreach (var (binding, hotkey) in bindings)
        {
            var id = Interlocked.Increment(ref _nextHotkeyId);
            if (!RegisterHotKey(IntPtr.Zero, id, hotkey.Modifiers, hotkey.VirtualKey))
            {
                _logger.LogWarning("Failed to register hotkey {Key} for {EventKey}.", binding.Key, binding.EventKey);
                continue;
            }

            _bindingsById[id] = binding;
            _logger.LogInformation("Registered hotkey {Key} for {EventKey}.", binding.Key, EventKeys.Normalize(binding.EventKey));
        }
    }

    private void HandleHotkey(KeybindBindingOptions binding)
    {
        try
        {
            var request = new UserActionRequest(
                binding.EventKey ?? string.Empty,
                binding.Action,
                binding.Detail ?? $"hotkey:{binding.Key}");

            _userActions.RecordAsync(request).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hotkey action failed for {EventKey}.", binding.EventKey);
        }
    }

    private readonly record struct HotkeyDefinition(uint Modifiers, uint VirtualKey)
    {
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;

        public static HotkeyDefinition? TryParse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var modifiers = 0u;
            uint? key = null;
            foreach (var rawPart in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var part = rawPart.Trim();
                if (part.Equals("ctrl", StringComparison.OrdinalIgnoreCase)
                    || part.Equals("control", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModControl;
                    continue;
                }

                if (part.Equals("alt", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModAlt;
                    continue;
                }

                if (part.Equals("shift", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModShift;
                    continue;
                }

                if (part.Equals("win", StringComparison.OrdinalIgnoreCase)
                    || part.Equals("windows", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= ModWin;
                    continue;
                }

                key = ParseVirtualKey(part);
            }

            return key.HasValue
                ? new HotkeyDefinition(modifiers, key.Value)
                : null;
        }

        private static uint? ParseVirtualKey(string value)
        {
            if (value.Length == 1)
            {
                var c = char.ToUpperInvariant(value[0]);
                if (c is >= 'A' and <= 'Z')
                {
                    return c;
                }

                if (c is >= '0' and <= '9')
                {
                    return c;
                }
            }

            if (value.StartsWith('F') && int.TryParse(value[1..], out var functionKey) && functionKey is >= 1 and <= 24)
            {
                return (uint)(0x70 + functionKey - 1);
            }

            return value.ToLowerInvariant() switch
            {
                "space" => 0x20,
                "escape" or "esc" => 0x1B,
                "pause" => 0x13,
                "insert" or "ins" => 0x2D,
                "delete" or "del" => 0x2E,
                "home" => 0x24,
                "end" => 0x23,
                "pageup" => 0x21,
                "pagedown" => 0x22,
                "up" => 0x26,
                "down" => 0x28,
                "left" => 0x25,
                "right" => 0x27,
                _ => null
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
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
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint idThread, int msg, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
