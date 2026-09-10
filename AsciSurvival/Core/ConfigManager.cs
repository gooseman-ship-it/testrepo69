using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Raylib_cs;

namespace AsciSurvival.Core
{
    public class ConfigData
    {
        public GraphicsSettings Graphics { get; set; } = new GraphicsSettings();
        public ControlSettings Controls { get; set; } = new ControlSettings();
        public AudioSettings Audio { get; set; } = new AudioSettings();
    }

    public class GraphicsSettings
    {
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 720;
        public bool Fullscreen { get; set; } = false;
        public int RenderDistance { get; set; } = 50;
        public float Brightness { get; set; } = 1.0f;
        public string FontPath { get; set; } = "Data/fonts/ascii_font.png";
    }

    public class ControlSettings
    {
        // Используем точные имена клавиш из энума KeyboardKey (KEY_W, KEY_SPACE и т.д.)
        public Dictionary<string, string> Bindings { get; set; } = new Dictionary<string, string>
        {
            { "MoveForward", "KEY_W" },
            { "MoveBackward", "KEY_S" },
            { "MoveLeft", "KEY_A" },
            { "MoveRight", "KEY_D" },
            { "Jump", "KEY_SPACE" },
            { "Crouch", "KEY_LEFT_CONTROL" },
            { "Sprint", "KEY_LEFT_SHIFT" },
            { "Interact", "KEY_E" },
            { "Menu", "KEY_TAB" }
        };
        
        [System.Xml.Serialization.XmlIgnore]
        public Dictionary<string, KeyboardKey> BindingsMap { get; set; } = new Dictionary<string, KeyboardKey>();
    }

    public class AudioSettings
    {
        public float MasterVolume { get; set; } = 0.5f;
        public float MusicVolume { get; set; } = 0.3f;
        public float SfxVolume { get; set; } = 0.7f;
    }

    public static class ConfigManager
    {
        private static ConfigData? _config;

        public static GraphicsSettings Graphics => _config!.Graphics;
        public static ControlSettings Controls => _config!.Controls;
        public static AudioSettings Audio => _config!.Audio;

        public static void Load(string path)
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _config = JsonSerializer.Deserialize<ConfigData>(json);
            }
            else
            {
                _config = new ConfigData();
                Save(path);
            }
            
            ApplyWindowSettings();
            ParseControls();
        }

        public static void Save(string path)
        {
            if (_config == null) return;
            
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(path, json);
        }

        public static void ApplyWindowSettings()
        {
            if (_config != null && _config.Graphics.Fullscreen)
            {
                Raylib.ToggleFullscreen();
            }
        }

        public static void SetFullscreen(bool fullscreen)
        {
            if (_config != null)
            {
                _config.Graphics.Fullscreen = fullscreen;
                if (fullscreen)
                {
                    Raylib.ToggleFullscreen();
                }
            }
        }

        private static void ParseControls()
        {
            Controls.BindingsMap.Clear();
            foreach (var bind in Controls.Bindings)
            {
                if (Enum.TryParse<KeyboardKey>(bind.Value, true, out var key))
                {
                    Controls.BindingsMap[bind.Key] = key;
                }
            }
        }
    }
}
