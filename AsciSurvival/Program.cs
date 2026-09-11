using AsciSurvival.Core;
using AsciSurvival.Rendering;

namespace AsciSurvival
{
    class Program
    {
        static void Main(string[] args)
        {
            // Обработка аргументов командной строки
            if (args.Length > 0 && args[0] == "test-frame")
            {
                // Headless-тест: рендер кадра без инициализации окна
                RunHeadlessTest();
                return;
            }
            
            // Загружаем конфигурацию
            ConfigManager.Load("Data/config.json");
            
            int width = ConfigManager.Graphics.Width;
            int height = ConfigManager.Graphics.Height;
            
            // Инициализация окна с поддержкой полноэкранного режима
            Raylib_cs.Raylib.InitWindow(width, height, "ASCII Survival: Frozen Peaks");
            Raylib_cs.Raylib.SetTargetFPS(ConfigManager.Graphics.FpsLimit);

            // Применяем настройки окна (включая полноэкранный режим)
            ConfigManager.ApplyWindowSettings();

            InputManager.Init();
            
            using (var game = new Game())
            {
                game.Initialize();
                
                while (!Raylib_cs.Raylib.WindowShouldClose())
                {
                    float deltaTime = Raylib_cs.Raylib.GetFrameTime();
                    
                    game.Update(deltaTime);
                    game.Draw();
                }
            }

            ConfigManager.Save("Data/config.json");
            Raylib_cs.Raylib.CloseWindow();
        }
        
        /// <summary>
        /// Headless-тест: рендер тестовой сцены в файл без GPU
        /// </summary>
        static void RunHeadlessTest()
        {
            Console.WriteLine("Running headless frame test...");
            var renderer = new HeadlessRenderer();
            renderer.RenderTestScene();
            Console.WriteLine("Headless test complete.");
        }
    }
}
