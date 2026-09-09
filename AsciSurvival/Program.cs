using Raylib_cs;
using AsciSurvival.Core;

namespace AsciSurvival
{
    class Program
    {
        static void Main(string[] args)
        {
            // Загружаем конфигурацию
            ConfigManager.Load("Data/config.json");
            
            int width = ConfigManager.Graphics.Width;
            int height = ConfigManager.Graphics.Height;
            
            // Инициализация окна с поддержкой полноэкранного режима
            Raylib.InitWindow(width, height, "ASCII Survival: Frozen Peaks");
            Raylib.SetTargetFPS(60);

            // Применяем настройки окна (включая полноэкранный режим)
            ConfigManager.ApplyWindowSettings();

            InputManager.Init();
            
            using (var game = new Game())
            {
                game.Initialize();
                
                while (!Raylib.WindowShouldClose())
                {
                    float deltaTime = Raylib.GetFrameTime();
                    
                    game.Update(deltaTime);
                    game.Draw();
                }
            }

            ConfigManager.Save("Data/config.json");
            Raylib.CloseWindow();
        }
    }
}
