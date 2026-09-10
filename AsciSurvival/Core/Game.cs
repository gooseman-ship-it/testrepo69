using Raylib_cs;
using AsciSurvival.Rendering;
using AsciSurvival.Gameplay;

namespace AsciSurvival.Core
{
    public class Game : System.IDisposable
    {
        private Rendering.Camera3D? _camera;
        private Player? _player;
        private AsciiRenderer? _renderer;
        private bool _isDisposed = false;

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

            InputManager.Update();

            // Проверка выхода из игры
            if (Raylib.IsKeyPressed(KeyboardKey.KEY_ESCAPE))
            {
                Raylib.CloseWindow();
                return;
            }

            if (!InputManager.IsMenuOpen && _player != null && _camera != null)
            {
                _player.Update(deltaTime, _camera);
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
            Raylib.DrawText($"FPS: {Raylib.GetFPS()}", Raylib.GetScreenWidth() - 60, 10, fontSize, Color.GRAY);
            
            if (InputManager.IsMenuOpen)
            {
                Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, 180));
                Raylib.DrawText("MENU (TAB to close)", Raylib.GetScreenWidth() / 2 - 100, Raylib.GetScreenHeight() / 2, 30, Color.WHITE);
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
