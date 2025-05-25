using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CaretTracker.Service
{
    internal class Program
    {
        // Windows API imports will be added here
        // TODO: Add GetGUIThreadInfo and ClientToScreen imports

        static async Task Main(string[] args)
        {
            Console.WriteLine("Caret Tracker Service Starting...");

            // Load configuration
            var config = Configuration.Load();
            Console.WriteLine($"Update interval: {config.UpdateIntervalMs} ms");
            Console.WriteLine($"Output path: {config.OutputPath}");

            try
            {
                // Pass configuration to service loop
                await RunServiceAsync(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static async Task RunServiceAsync(Configuration config)
        {
            // Service implementation will be added here
            // Use configured update interval
            while (true)
            {
                await Task.Delay(config.UpdateIntervalMs);
            }
        }
    }
}
