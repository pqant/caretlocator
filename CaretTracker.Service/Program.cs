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

        private static EventLog? eventLog;
        private static CancellationTokenSource? cancellationTokenSource;
        private const string EventLogSource = "CaretTracker";
        private const string EventLogName = "Application";
        private const string OutputDirectory = "caret_data";

        /// <summary>
        /// Main entry point of the application
        /// </summary>
        public static async Task Main(string[] args)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    InitializeEventLog();
                }

                cancellationTokenSource = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cancellationTokenSource.Cancel();
                };

                await RunServiceAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                if (OperatingSystem.IsWindows())
                {
                    eventLog?.WriteEntry($"Fatal error: {ex.Message}", EventLogEntryType.Error);
                }
                Console.WriteLine($"Fatal error: {ex.Message}");
                Environment.Exit(1);
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

                eventLog.WriteEntry("Caret Tracker Service started", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize event log: {ex.Message}");
                eventLog?.WriteEntry($"Failed to initialize event log: {ex.Message}", EventLogEntryType.Error);
            }
        }

        /// <summary>
        /// Main service loop
        /// </summary>
        private static async Task RunServiceAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Caret Tracker Service is running. Press Ctrl+C to stop.");

            if (OperatingSystem.IsWindows())
            {
                eventLog?.WriteEntry("Service started successfully", EventLogEntryType.Information);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
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

                            await WritePositionToFileAsync(position);
                        }
                    }

                    await Task.Delay(100, cancellationToken);
                }
                catch (Exception ex)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        eventLog?.WriteEntry($"Error tracking caret: {ex.Message}", EventLogEntryType.Error);
                    }
                    Console.WriteLine($"Error tracking caret: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }

            if (OperatingSystem.IsWindows())
            {
                eventLog?.WriteEntry("Service stopped", EventLogEntryType.Information);
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
                eventLog?.WriteEntry("Edge case: No active window found", EventLogEntryType.Warning);
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
                eventLog?.WriteEntry($"Edge case: Failed to get thread information for window {foregroundWindow}", 
                    EventLogEntryType.Warning);
                return null;  // Failed to get thread information (e.g., insufficient permissions or thread not found)
            }

            // Check if caret exists
            if (threadInfo.hwndCaret == IntPtr.Zero)
            {
                eventLog?.WriteEntry($"Edge case: No caret present in window {foregroundWindow}", 
                    EventLogEntryType.Warning);
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
                eventLog?.WriteEntry($"Edge case: Failed to convert coordinates for window {foregroundWindow}", 
                    EventLogEntryType.Warning);
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
        private static async Task WritePositionToFileAsync(CaretPosition position)
        {
            try
            {
                if (!Directory.Exists(OutputDirectory))
                {
                    Directory.CreateDirectory(OutputDirectory);
                }

                string fileName = Path.Combine(OutputDirectory, $"caret_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                string json = JsonSerializer.Serialize(position, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(fileName, json);
            }
            catch (Exception ex)
            {
                if (OperatingSystem.IsWindows())
                {
                    eventLog?.WriteEntry($"Error writing position to file: {ex.Message}", EventLogEntryType.Error);
                }
                Console.WriteLine($"Error writing position to file: {ex.Message}");
            }
        }
    }
}
