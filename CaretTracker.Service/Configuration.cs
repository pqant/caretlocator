using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics; // Required for EventLog if we decide to log here

namespace CaretTracker.Service
{
    /// <summary>
    /// Represents the application's configuration settings.
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// Gets or sets the update interval in milliseconds for caret position tracking.
        /// </summary>
        [JsonPropertyName("update_interval_ms")]
        public int UpdateIntervalMs { get; set; } = 100; // Default value

        /// <summary>
        /// Gets or sets the output path for storing caret position data.
        /// Supports environment variables like %USERPROFILE%.
        /// </summary>
        [JsonPropertyName("output_path")]
        public string OutputPath { get; set; } = "caret_data"; // Default value, will be treated as a directory

        private const string DefaultConfigFileName = "caret_config.json";

        /// <summary>
        /// Loads configuration from the specified JSON file or returns default configuration if the file doesn't exist or is invalid.
        /// </summary>
        /// <param name="configFilePath">The path to the configuration file.</param>
        /// <param name="eventLog">Optional EventLog instance for logging warnings.</param>
        /// <returns>An instance of the Configuration class.</returns>
        public static Configuration Load(string configFilePath = DefaultConfigFileName, EventLog? eventLog = null)
        {
            try
            {
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    var config = JsonSerializer.Deserialize<Configuration>(json);
                    if (config != null)
                    {
                        // Basic validation
                        if (config.UpdateIntervalMs <= 0)
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                eventLog?.WriteEntry($"Warning: UpdateIntervalMs in '{configFilePath}' is invalid ({config.UpdateIntervalMs}). Using default value (100ms).", EventLogEntryType.Warning);
                            }
                            config.UpdateIntervalMs = 100;
                        }
                        if (string.IsNullOrWhiteSpace(config.OutputPath))
                        {
                            if (OperatingSystem.IsWindows())
                            {
                                eventLog?.WriteEntry($"Warning: OutputPath in '{configFilePath}' is empty. Using default value ('caret_data').", EventLogEntryType.Warning);
                            }
                            config.OutputPath = "caret_data";
                        }
                        if (OperatingSystem.IsWindows())
                        {
                            eventLog?.WriteEntry($"Configuration loaded successfully from '{configFilePath}'. Update Interval: {config.UpdateIntervalMs}ms, Output Path: {config.OutputPath}", EventLogEntryType.Information);
                        }
                        return config;
                    }
                }
                else
                {
                    if (OperatingSystem.IsWindows())
                    {
                        eventLog?.WriteEntry($"Configuration file '{configFilePath}' not found. Using default configuration. Update Interval: 100ms, Output Path: 'caret_data'", EventLogEntryType.Warning);
                    }
                }
            }
            catch (JsonException ex)
            {
                if (OperatingSystem.IsWindows())
                {
                    eventLog?.WriteEntry($"Error deserializing configuration file '{configFilePath}': {ex.Message}. Using default configuration.", EventLogEntryType.Warning);
                }
            }
            catch (Exception ex)
            {
                if (OperatingSystem.IsWindows())
                {
                    eventLog?.WriteEntry($"Error loading configuration file '{configFilePath}': {ex.Message}. Using default configuration.", EventLogEntryType.Error);
                }
            }
            
            // Return default configuration if file not found, is empty, or error occurs
            return new Configuration();
        }

        /// <summary>
        /// Gets the fully resolved output directory path, expanding environment variables.
        /// </summary>
        /// <returns>The expanded output directory path.</returns>
        public string GetExpandedOutputDirectory()
        {
            // Ensure OutputPath is treated as a directory.
            // If OutputPath was intended to be a full file path, this logic would need adjustment.
            // For now, assuming OutputPath specifies a directory.
            return Environment.ExpandEnvironmentVariables(OutputPath);
        }
    }
} 