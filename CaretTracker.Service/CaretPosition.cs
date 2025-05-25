using System;
using System.Text.Json.Serialization;

namespace CaretTracker.Service
{
    /// <summary>
    /// Represents the current caret position and related information
    /// </summary>
    public class CaretPosition
    {
        [JsonPropertyName("caret_x")]
        public int X { get; set; }

        [JsonPropertyName("caret_y")]
        public int Y { get; set; }

        [JsonPropertyName("caret_timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("caret_window_title")]
        public string? WindowTitle { get; set; }

        [JsonPropertyName("caret_process_name")]
        public string? ProcessName { get; set; }
    }
} 