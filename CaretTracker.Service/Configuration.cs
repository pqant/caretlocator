using System;
using System.IO;
using System.Text.Json;

namespace CaretTracker.Service
{
    public class Configuration
    {
        public int UpdateIntervalMs { get; set; } = 100;
        public string OutputPath { get; set; } = "%AppData%\\dev-coder-v1\\caret_position.json";

        public static Configuration Load(string configPath = "caret_config.json")
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<Configuration>(json) ?? new Configuration();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
            }

            var config = new Configuration();
            config.Save(configPath);
            return config;
        }

        public void Save(string configPath = "caret_config.json")
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
            }
        }

        public string GetExpandedOutputPath()
        {
            return Environment.ExpandEnvironmentVariables(OutputPath);
        }
    }
} 