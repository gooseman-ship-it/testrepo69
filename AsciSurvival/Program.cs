using AsciSurvival.Core;
using AsciSurvival.Rendering;
using Raylib_cs;

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
            
            // Настройка borderless режима ДО InitWindow
            int width = ConfigManager.Graphics.Width;
            int height = ConfigManager.Graphics.Height;
            
            if (ConfigManager.Graphics.WindowMode == "borderless")
                Raylib.SetConfigFlags(ConfigFlags.FLAG_WINDOW_UNDECORATED);
            
            InputManager.Init();
            
            using (var game = new Game())
            {
                game.Initialize();
                
                // Центрирование окна для borderless ПОСЛЕ InitWindow
                if (ConfigManager.Graphics.WindowMode == "borderless")
                {
                    int mon = Raylib.GetCurrentMonitor();
                    Raylib.SetWindowPosition(
                        (Raylib.GetMonitorWidth(mon) - width) / 2,
                        (Raylib.GetMonitorHeight(mon) - height) / 2);
                }
                
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
            
            // Зонд щели (только под test-frame)
            renderer.RunGapProbe();
            
            renderer.RenderTestScene();
            Console.WriteLine("Headless test complete.");
        }
    }
}
