using Raylib_cs;
using AsciSurvival.Rendering;
using AsciSurvival.Gameplay;
using System.Numerics;

namespace AsciSurvival.Core
{
    public enum GameState { Playing, Paused, Inventory }

    public class Game : System.IDisposable
    {
        private Rendering.Camera3D? _camera;
        private Player? _player;
        private AsciiRenderer? _renderer;
        private bool _isDisposed = false;
        private GameState _gameState = GameState.Playing;

        public void Initialize()
        {
            _camera = new Rendering.Camera3D();
            _player = new Player();
            _renderer = new AsciiRenderer(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            
            // Отключаем стандартный выход по ESC, чтобы обрабатывать его самим
            Raylib.SetExitKey(KeyboardKey.KEY_NULL);
        }

        public void Update(float deltaTime)
        {
            if (_isDisposed) return;

            InputManager.Update(ref _gameState);

            // Обработка переключения состояний игры
            if (_gameState == GameState.Playing)
            {
                if (InputManager.IsActionPressed("Pause"))
                {
                    _gameState = GameState.Paused;
                    InputManager.ToggleMouseCapture(false);
                }
                else if (InputManager.IsActionPressed("Inventory"))
                {
                    _gameState = GameState.Inventory;
                    InputManager.ToggleMouseCapture(false);
                }
                else if (_player != null && _camera != null)
                {
                    _player.Update(deltaTime, _camera);
                }
            }
            else if (_gameState == GameState.Paused)
            {
                if (InputManager.IsActionPressed("Pause"))
                {
                    _gameState = GameState.Playing;
                    InputManager.ToggleMouseCapture(true);
                }
                else if (InputManager.IsActionPressed("Exit"))
                {
                    Raylib.CloseWindow();
                    return;
                }
            }
            else if (_gameState == GameState.Inventory)
            {
                if (InputManager.IsActionPressed("Inventory") || InputManager.IsActionPressed("Pause"))
                {
                    _gameState = GameState.Playing;
                    InputManager.ToggleMouseCapture(true);
                }
            }
        }

        public void Draw()
        {
            if (_isDisposed || _renderer == null || _camera == null || _player == null) return;

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.BLACK);

            _renderer.Render(_camera, _player.Position);

            DrawHUD();

            Raylib.EndDrawing();
        }

        private void DrawHUD()
        {
            int fontSize = 20;
            
            if (_player == null) return;
            
            Raylib.DrawText($"HP: {_player.Health:F0}", 10, 10, fontSize, Color.GREEN);
            Raylib.DrawText($"Temp: {_player.Temperature:F1}C", 10, 35, fontSize, Color.SKYBLUE);
            Raylib.DrawText($"Hunger: {_player.Hunger:F0}", 10, 60, fontSize, Color.ORANGE);
            Raylib.DrawText($"Thirst: {_player.Thirst:F0}", 10, 85, fontSize, Color.BLUE);
            Raylib.DrawText(
                $"cam=({_camera.Position.X:F1},{_camera.Position.Y:F1},{_camera.Position.Z:F1}) yaw={_camera.Yaw:F0} pitch={_camera.Pitch:F0}",
                10, 110, fontSize, Color.WHITE);
            
            // FPS с отступом через MeasureText, чтобы не выходил за правый край
            string fpsText = $"FPS: {Raylib.GetFPS()}";
            Vector2 fpsSize = Raylib.MeasureTextEx(Raylib.GetFontDefault(), fpsText, fontSize, 1.0f);
            int fpsX = Raylib.GetScreenWidth() - (int)fpsSize.X - 10;
            Raylib.DrawText(fpsText, fpsX, 10, fontSize, Color.GRAY);
            
            // Отрисовка оверлеев для состояний Paused и Inventory
            if (_gameState == GameState.Paused)
            {
                Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, 180));
                Raylib.DrawText("ПАУЗА", Raylib.GetScreenWidth() / 2 - 80, Raylib.GetScreenHeight() / 2 - 60, 40, Color.WHITE);
                Raylib.DrawText("Continue (Esc)", Raylib.GetScreenWidth() / 2 - 90, Raylib.GetScreenHeight() / 2, 30, Color.LIGHTGRAY);
                Raylib.DrawText("Settings (заглушка)", Raylib.GetScreenWidth() / 2 - 120, Raylib.GetScreenHeight() / 2 + 40, 30, Color.LIGHTGRAY);
                Raylib.DrawText("Exit (Ctrl+Q)", Raylib.GetScreenWidth() / 2 - 85, Raylib.GetScreenHeight() / 2 + 80, 30, Color.LIGHTGRAY);
            }
            else if (_gameState == GameState.Inventory)
            {
                Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, 180));
                Raylib.DrawText("ИНВЕНТАРЬ", Raylib.GetScreenWidth() / 2 - 100, Raylib.GetScreenHeight() / 2 - 60, 40, Color.WHITE);
                Raylib.DrawText("Инвентарь пуст", Raylib.GetScreenWidth() / 2 - 85, Raylib.GetScreenHeight() / 2, 30, Color.LIGHTGRAY);
                Raylib.DrawText("Нажмите Tab или Esc для выхода", Raylib.GetScreenWidth() / 2 - 150, Raylib.GetScreenHeight() / 2 + 60, 25, Color.GRAY);
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _renderer?.Dispose();
                _isDisposed = true;
            }
        }
    }
}
