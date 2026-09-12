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
            
            int width = ConfigManager.Graphics.Width;
            int height = ConfigManager.Graphics.Height;
            
            // Флаг рамки без украшения — строго ДО создания окна
            if (ConfigManager.Graphics.WindowMode == "borderless")
            {
                Raylib_cs.Raylib.SetConfigFlags(ConfigFlags.FLAG_WINDOW_UNDECORATED);
            }
            
            // Создание окна — единственный вызов во всей программе
            Raylib_cs.Raylib.InitWindow(width, height, "ASCII Survival: Frozen Peaks");
            if (!Raylib_cs.Raylib.IsWindowReady())
            {
                Console.WriteLine("Окно не инициализировано");
                return;
            }
            
            // Центрирование ПОСЛЕ создания, с охраной невалидного монитора
            if (ConfigManager.Graphics.WindowMode == "borderless")
            {
                int mon = Raylib_cs.Raylib.GetCurrentMonitor();
                int mw = Raylib_cs.Raylib.GetMonitorWidth(mon);
                int mh = Raylib_cs.Raylib.GetMonitorHeight(mon);
                if (mw > 0 && mh > 0)
                {
                    Raylib_cs.Raylib.SetWindowPosition(
                        Math.Max(0, (mw - width) / 2),
                        Math.Max(0, (mh - height) / 2));
                }
            }
            
            Raylib_cs.Raylib.SetTargetFPS(ConfigManager.Graphics.FpsLimit);
            
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
            
            // Зонд щели (только под test-frame)
            renderer.RunGapProbe();
            
            renderer.RenderTestScene();
            Console.WriteLine("Headless test complete.");
        }
    }
}
