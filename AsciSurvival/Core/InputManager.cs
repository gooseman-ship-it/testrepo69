using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace AsciSurvival.Core
{
    public static class InputManager
    {
        private static Dictionary<string, KeyboardKey> _keyBindings = null!;
        public static bool IsMenuOpen { get; private set; } = false;

        public static void Init()
        {
            _keyBindings = ConfigManager.Controls.BindingsMap; 
            ToggleMouseCapture(true);
        }

        public static void Update(ref GameState gameState)
        {
            // В состоянии игры обрабатываем переключение состояний через Game.cs
            // Здесь только обновление состояния клавиш
        }

        public static bool IsActionPressed(string action)
        {
            KeyboardKey key = GetKey(action);
            return key != KeyboardKey.KEY_NULL && Raylib.IsKeyPressed(key);
        }

        public static bool IsKeyDown(KeyboardKey key) => Raylib.IsKeyDown(key);
        public static bool IsKeyPressed(KeyboardKey key) => Raylib.IsKeyPressed(key);
        
        public static Vector2 GetMouseDelta()
        {
            var mouse = Raylib.GetMouseDelta();
            return new Vector2(mouse.X, mouse.Y);
        }

        public static void ToggleMouseCapture(bool capture)
        {
            if (capture)
            {
                Raylib.DisableCursor();
                Raylib.SetMousePosition(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2);
            }
            else
            {
                Raylib.EnableCursor();
            }
        }

        public static KeyboardKey GetKey(string action)
        {
            return _keyBindings.TryGetValue(action, out var key) ? key : KeyboardKey.KEY_NULL;
        }
    }
}
