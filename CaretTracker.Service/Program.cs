using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Text.Json;

namespace CaretTracker.Service
{
    /// <summary>
    /// Main program class for the Caret Tracker Service
    /// </summary>
    public class Program
    {
        // Windows API structures for caret tracking
        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // Windows API declarations
        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // Windows API declarations for console window visibility
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
        private const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
        private const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
        private const uint EVENT_SYSTEM_SWITCHSTART = 0x0014;
        private const uint EVENT_SYSTEM_SWITCHEND = 0x0015;
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private const uint EVENT_OBJECT_FOCUS = 0x8005;
        private const uint EVENT_OBJECT_VALUECHANGE = 0x800E;
        private const uint EVENT_OBJECT_SELECTION = 0x8006;
        private const uint EVENT_OBJECT_CONTENTSCROLLED = 0x8015;
        private const uint EVENT_OBJECT_SHOW = 0x8002;
        private const uint EVENT_OBJECT_HIDE = 0x8003;
        private const uint EVENT_OBJECT_CREATE = 0x8000;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        private const int HWND_TOPMOST = -1;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint OBJID_CARET = 0xFFFFFFF8;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_CONTROL = 0x11;
        private const int VK_C = 0x43;

        private static EventLog? eventLog;
        private static CancellationTokenSource? cancellationTokenSource;
        private const string EventLogSource = "CaretTracker";
        private const string EventLogName = "Application";
        private static Configuration? configuration;
        private static CaretPosition? lastPosition = null;
        private static IntPtr? winEventHook;
        private static StreamWriter? debugLogWriter;
        private static readonly object logLock = new object();
        private static System.Threading.Timer? fallbackTimer;

        /// <summary>
        /// Writes a log entry to both EventLog and debug log file
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void WriteLog(string message, EventLogEntryType entryType = EventLogEntryType.Information)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] [{entryType}] {message}";

            // Write to EventLog if on Windows
            if (OperatingSystem.IsWindows())
            {
                eventLog?.WriteEntry(message, entryType);
            }

            // Write to debug log file
            try
            {
                lock (logLock)
                {
                    debugLogWriter ??= InitializeDebugLogWriter();
                    debugLogWriter.WriteLine(logMessage);
                }
            }
            catch (Exception ex)
            {
                // If we can't write to debug log, at least try to write to console
                Console.WriteLine($"Failed to write to debug log: {ex.Message}");
            }
        }

        /// <summary>
        /// Main entry point of the application
        /// </summary>
        public static async Task Main(string[] args)
        {
            try
            {
                // Register system event handlers
                if (OperatingSystem.IsWindows())
                {
                    AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                    Console.CancelKeyPress += OnCancelKeyPress;
                }

                if (OperatingSystem.IsWindows())
                {
                    InitializeEventLog();
                }

                if (OperatingSystem.IsWindows())
                {
                    WriteLog("Caret Tracker Service starting...");
                }

                // Load configuration
                configuration = Configuration.Load(eventLog: eventLog);
                if (OperatingSystem.IsWindows())
                {
                    WriteLog($"Configuration loaded. Update Interval: {configuration.UpdateIntervalMs}ms, Output Path: {configuration.OutputPath}, Debug Mode: {configuration.DebugMode}");
                }

                // Set console window visibility and position based on debug mode
                if (OperatingSystem.IsWindows())
                {
                    var handle = GetConsoleWindow();
                    if (configuration.DebugMode)
                    {
                        ShowWindow(handle, SW_SHOW);
                        SetWindowPos(handle, (IntPtr)HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                    }
                    else
                    {
                        ShowWindow(handle, SW_HIDE);
                    }
                }

                cancellationTokenSource = new CancellationTokenSource();
                await RunServiceAsync(cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // Service stopped by user (Ctrl+C)
                if (OperatingSystem.IsWindows())
                {
                    WriteLog("Service stopped by user (Ctrl+C).", EventLogEntryType.Information);
                }
                Console.WriteLine("Service stopped by user (Ctrl+C).");
            }
            catch (Exception ex)
            {
                if (OperatingSystem.IsWindows())
                {
                    WriteLog($"Fatal error: {ex.Message}", EventLogEntryType.Error);
                }
                Console.WriteLine($"Fatal error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent the default Ctrl+C behavior
            if (OperatingSystem.IsWindows())
            {
                WriteLog("Ctrl+C detected, initiating graceful shutdown...", EventLogEntryType.Information);
            }
            cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Handles process exit event
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void OnProcessExit(object? sender, EventArgs e)
        {
            try
            {
                if (OperatingSystem.IsWindows() && eventLog != null)
                {
                    WriteLog("Service is shutting down...");
                }

                // Cleanup resources
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }
                debugLogWriter?.Dispose();
                eventLog?.Dispose();

                // Unregister event handlers
                if (OperatingSystem.IsWindows())
                {
                    AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                    AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                    Console.CancelKeyPress -= OnCancelKeyPress;
                }
            }
            catch (Exception ex)
            {
                if (OperatingSystem.IsWindows() && eventLog != null)
                {
                    WriteLog($"Error during shutdown: {ex.Message}", EventLogEntryType.Error);
                }
            }
        }

        /// <summary>
        /// Handles unhandled exceptions
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                if (OperatingSystem.IsWindows() && eventLog != null)
                {
                    WriteLog($"Unhandled exception: {e.ExceptionObject}", EventLogEntryType.Error);
                }

                // Cleanup resources
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }
                debugLogWriter?.Dispose();
                eventLog?.Dispose();

                // Unregister event handlers
                if (OperatingSystem.IsWindows())
                {
                    AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                    AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                    Console.CancelKeyPress -= OnCancelKeyPress;
                }
            }
            catch
            {
                // Ignore errors during emergency shutdown
            }
        }

        /// <summary>
        /// Initializes the Windows Event Log
        /// </summary>
        private static void InitializeEventLog()
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                if (!EventLog.SourceExists(EventLogSource))
                {
                    EventLog.CreateEventSource(EventLogSource, EventLogName);
                }

                eventLog = new EventLog(EventLogName)
                {
                    Source = EventLogSource
                };

                if (OperatingSystem.IsWindows() && eventLog != null)
                {
                    eventLog.WriteEntry("Caret Tracker Service started", EventLogEntryType.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize event log: {ex.Message}");
                if (OperatingSystem.IsWindows() && eventLog != null)
                {
                    eventLog.WriteEntry($"Failed to initialize event log: {ex.Message}", EventLogEntryType.Error);
                }
            }
        }

        private static IntPtr? windowEventHook;
        private static IntPtr? objectEventHook;

        /// <summary>
        /// Main service loop using Windows event hook
        /// </summary>
        private static async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            if (configuration == null)
            {
                throw new InvalidOperationException("Configuration not loaded");
            }

            if (configuration.DebugMode)
            {
                Console.WriteLine("Caret Tracker Service is running in DEBUG mode. Press Ctrl+C to stop.");
            }
            else
            {
                Console.WriteLine("Caret Tracker Service is running. Press Ctrl+C to stop.");
            }

            if (OperatingSystem.IsWindows())
            {
                WriteLog("Service started successfully", EventLogEntryType.Information);

                // Set up Windows event hook for caret tracking
                var winEventProc = new WinEventDelegate((hWinEventHook, eventType, hwnd, idObject, idChild, dwEventThread, dwmsEventTime) =>
                {
                    try
                    {
                        if (configuration?.DebugMode == true)
                        {
                            Console.WriteLine($"Event received - Type: 0x{eventType:X}, Object: {idObject}, Child: {idChild}");
                        }

                        // Track caret position for all events that might affect it
                        // If we're using fallback timer, stop it temporarily
                        if (fallbackTimer != null)
                        {
                            fallbackTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        }

                        TrackCaretPositionAsync(cancellationToken).Wait();

                        // Resume fallback timer if it exists
                        if (fallbackTimer != null && configuration != null)
                        {
                            // If event hook is successful, use 2x interval
                            var interval = winEventHook != IntPtr.Zero 
                                ? TimeSpan.FromMilliseconds(configuration.UpdateIntervalMs * 2)
                                : TimeSpan.FromMilliseconds(configuration.UpdateIntervalMs);
                            
                            fallbackTimer.Change(TimeSpan.Zero, interval);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            WriteLog($"Error in event handler: {ex.Message}", EventLogEntryType.Error);
                        }
                    }
                });

                // Set up event hooks for all relevant events
                winEventHook = SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND,  // Start with foreground window changes
                    EVENT_OBJECT_CONTENTSCROLLED,  // End with content scroll
                    IntPtr.Zero,  // No DLL handle needed
                    winEventProc,
                    0,  // All processes
                    0,  // All threads
                    WINEVENT_OUTOFCONTEXT
                );

                // Set up additional event hooks for window changes
                windowEventHook = SetWinEventHook(
                    EVENT_SYSTEM_MINIMIZEEND,
                    EVENT_SYSTEM_SWITCHEND,
                    IntPtr.Zero,
                    winEventProc,
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT
                );

                objectEventHook = SetWinEventHook(
                    EVENT_OBJECT_SHOW,
                    EVENT_OBJECT_DESTROY,
                    IntPtr.Zero,
                    winEventProc,
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT
                );

                if (winEventHook == IntPtr.Zero && windowEventHook == IntPtr.Zero && objectEventHook == IntPtr.Zero)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        WriteLog("Failed to set up event hooks, falling back to timer", EventLogEntryType.Warning);
                    }
                    // Fallback to timer if event hooks fail
                    if (configuration != null)
                    {
                        fallbackTimer = new System.Threading.Timer(
                            async _ => await TrackCaretPositionAsync(cancellationToken),
                            null,
                            TimeSpan.Zero,
                            TimeSpan.FromMilliseconds(configuration.UpdateIntervalMs)
                        );
                    }
                }
                else
                {
                    if (OperatingSystem.IsWindows())
                    {
                        WriteLog("Event hooks set up successfully", EventLogEntryType.Information);
                    }
                    // Start with a fallback timer in case we miss some events
                    if (configuration != null)
                    {
                        fallbackTimer = new System.Threading.Timer(
                            async _ => await TrackCaretPositionAsync(cancellationToken),
                            null,
                            TimeSpan.FromMilliseconds(configuration.UpdateIntervalMs * 2), // Start after a delay
                            TimeSpan.FromMilliseconds(configuration.UpdateIntervalMs * 2)  // Run less frequently
                        );
                    }
                }
            }

            try
            {
                // Keep the service running until cancellation is requested
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
            finally
            {
                // Cleanup Windows event hooks
                if (OperatingSystem.IsWindows())
                {
                    if (winEventHook.HasValue && winEventHook.Value != IntPtr.Zero)
                    {
                        UnhookWinEvent(winEventHook.Value);
                    }
                    if (windowEventHook.HasValue && windowEventHook.Value != IntPtr.Zero)
                    {
                        UnhookWinEvent(windowEventHook.Value);
                    }
                    if (objectEventHook.HasValue && objectEventHook.Value != IntPtr.Zero)
                    {
                        UnhookWinEvent(objectEventHook.Value);
                    }
                    fallbackTimer?.Dispose();
                }
            }

            if (OperatingSystem.IsWindows())
            {
                WriteLog("Service stopped", EventLogEntryType.Information);
            }
        }

        /// <summary>
        /// Tracks caret position and writes to file if position has changed
        /// </summary>
        private static async Task TrackCaretPositionAsync(CancellationToken cancellationToken)
        {
            if (configuration == null) return;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var caretInfo = GetCaretPosition();
                    if (caretInfo.HasValue)
                    {
                        var foregroundWindow = GetForegroundWindow();
                        var position = new CaretPosition
                        {
                            X = caretInfo.Value.x,
                            Y = caretInfo.Value.y,
                            Timestamp = DateTime.Now,
                            WindowTitle = GetWindowTitle(foregroundWindow),
                            ProcessName = GetProcessName(foregroundWindow)
                        };

                        // Only update if position has changed
                        if (lastPosition == null || 
                            position.X != lastPosition.X || 
                            position.Y != lastPosition.Y || 
                            position.WindowTitle != lastPosition.WindowTitle || 
                            position.ProcessName != lastPosition.ProcessName)
                        {
                            lastPosition = position;
                            await WritePositionToFileAsync(position, configuration);

                            // Log position in debug mode
                            if (configuration.DebugMode)
                            {
                                Console.WriteLine($"Caret Position - X: {position.X}, Y: {position.Y}, Window: {position.WindowTitle}, Process: {position.ProcessName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (OperatingSystem.IsWindows())
                {
                    WriteLog($"Error tracking caret: {ex.Message}", EventLogEntryType.Error);
                }
                Console.WriteLine($"Error tracking caret: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current caret position from the active window.
        /// Returns null in the following cases:
        /// 1. No active window is found
        /// 2. Thread information cannot be retrieved
        /// 3. No caret is present in the active window
        /// 4. Coordinate conversion fails
        /// </summary>
        private static (int x, int y)? GetCaretPosition()
        {
            if (!OperatingSystem.IsWindows()) return null;

            // Get the foreground window handle
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                if (OperatingSystem.IsWindows() && eventLog != null)
                {
                    eventLog.WriteEntry("Edge case: No active window found", EventLogEntryType.Warning);
                }
                return null;  // No active window found (e.g., desktop or system window is active)
            }

            // Get the thread ID of the foreground window
            uint processId;
            uint threadId = GetWindowThreadProcessId(foregroundWindow, out processId);

            // Initialize thread info structure
            var threadInfo = new GUITHREADINFO();
            threadInfo.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));

            // Get GUI thread information
            if (!GetGUIThreadInfo(threadId, ref threadInfo))
            {
                if (OperatingSystem.IsWindows() && eventLog != null)
                {
                    eventLog.WriteEntry($"Edge case: Failed to get thread information for window {foregroundWindow}", 
                        EventLogEntryType.Warning);
                }
                return null;  // Failed to get thread information (e.g., insufficient permissions or thread not found)
            }

            // Check if caret exists
            if (threadInfo.hwndCaret == IntPtr.Zero)
            {
                if (OperatingSystem.IsWindows() && eventLog != null)
                {
                    eventLog.WriteEntry($"Edge case: No caret present in window {foregroundWindow}", 
                        EventLogEntryType.Warning);
                }
                return null;  // No caret present in the active window (e.g., no text input focus or unsupported application)
            }

            // Get caret coordinates
            var point = new POINT
            {
                X = threadInfo.rcCaret.left,
                Y = threadInfo.rcCaret.top
            };

            // Convert client coordinates to screen coordinates
            if (!ClientToScreen(threadInfo.hwndCaret, ref point))
            {
                if (OperatingSystem.IsWindows() && eventLog != null)
                {
                    eventLog.WriteEntry($"Edge case: Failed to convert coordinates for window {foregroundWindow}", 
                        EventLogEntryType.Warning);
                }
                return null;  // Failed to convert coordinates (e.g., window closed or invalid state)
            }

            return (point.X, point.Y);
        }

        /// <summary>
        /// Gets the title of the specified window
        /// </summary>
        private static string GetWindowTitle(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows()) return string.Empty;

            int length = GetWindowTextLength(hwnd);
            if (length == 0) return string.Empty;

            var builder = new StringBuilder(length + 1);
            GetWindowText(hwnd, builder, builder.Capacity);
            return builder.ToString();
        }

        /// <summary>
        /// Gets the process name associated with the specified window
        /// </summary>
        private static string GetProcessName(IntPtr hwnd)
        {
            if (!OperatingSystem.IsWindows()) return string.Empty;

            try
            {
                GetWindowThreadProcessId(hwnd, out uint processId);
                var process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Writes the caret position to a JSON file
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static async Task WritePositionToFileAsync(CaretPosition position, Configuration config)
        {
            try
            {
                // Use a fixed path in AppData
                string outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "dev-coder-v1"
                );

                // Ensure directory exists
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Use a fixed filename
                string fileName = Path.Combine(outputDir, "caret_position.json");
                string json = JsonSerializer.Serialize(position, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(fileName, json);

                if (OperatingSystem.IsWindows())
                {
                    WriteLog($"Position written to file: {fileName}");
                }
            }
            catch (Exception ex)
            {
                if (OperatingSystem.IsWindows())
                {
                    WriteLog($"Error writing position to file: {ex.Message}", EventLogEntryType.Error);
                }
                Console.WriteLine($"Error writing position to file: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes the debug log writer
        /// </summary>
        private static StreamWriter InitializeDebugLogWriter()
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "dev-coder-v1",
                "caret_tracker_debug.log"
            );
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            
            return new StreamWriter(logPath, true, Encoding.UTF8)
            {
                AutoFlush = true
            };
        }
    }
}
