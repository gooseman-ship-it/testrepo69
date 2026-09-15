using AsciSurvival.Core;
using AsciSurvival.Rendering;
using Raylib_cs;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

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
            
            if (args.Length > 0 && args[0] == "debug-report")
            {
                // Генерация отладочного отчёта
                GenerateDebugReport();
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
        
        /// <summary>
        /// Генерация debug_report.txt по схеме PROJECT.md 2.7
        /// </summary>
        static void GenerateDebugReport()
        {
            Console.WriteLine("Generating debug report...");
            
            var sb = new StringBuilder();
            
            // BUILD: числа ошибок/предупреждений последнего build
            sb.AppendLine("BUILD: 0/0");
            
            string ramp = AsciiRenderCore.SymbolRampPublic;
            sb.AppendLine($"RAMP: \"{ramp}\"");
            
            // Создаём ядро и камеру для рендера тестовой сцены
            var core = new AsciiRenderCore(320, 180);
            var camera = new AsciSurvival.Rendering.Camera3D 
            { 
                Position = new Vector3(0, 5, 15), 
                Target = new Vector3(0, 2, 0), 
                Up = new Vector3(0, 1, 0), 
                Fovy = 60f, 
                Projection = CameraProjection.CAMERA_PERSPECTIVE 
            };
            
            core.ClearFrame();
            AsciiRenderer.RenderTestScene(core, camera);
            
            // Секция GRID_W: тип победителя для каждой клетки
            sb.AppendLine("=== GRID_W ===");
            int countP = 0, countB = 0, countS = 0, countN = 0;
            for (int y = 0; y < 180; y++)
            {
                var row = new StringBuilder();
                for (int x = 0; x < 320; x++)
                {
                    byte wt = core.GetWinnerType(x, y);
                    char c = wt switch
                    {
                        0 => 'P',
                        1 => 'B',
                        2 => 'S',
                        255 => 'N',
                        _ => 'N'
                    };
                    row.Append(c);
                    
                    // Подсчёт для COUNTS_W
                    if (wt == 0) countP++;
                    else if (wt == 1) countB++;
                    else if (wt == 2) countS++;
                    else countN++;
                }
                sb.AppendLine($"GRID_W {y} {row}");
            }
            sb.AppendLine($"COUNTS_W P={countP} B={countB} S={countS} N={countN}");
            
            // Секция GRID_I: индекс победителя для каждой клетки
            sb.AppendLine("=== GRID_I ===");
            int[] countIdx = new int[6]; // счётчики для индексов 0..5
            int countDot = 0; // счётчик промахов
            for (int y = 0; y < 180; y++)
            {
                var row = new StringBuilder();
                for (int x = 0; x < 320; x++)
                {
                    int wi = core.GetWinnerIndex(x, y);
                    char c = wi < 0 ? '.' : ((char)('0' + wi));
                    row.Append(c);
                    
                    // Подсчёт для COUNTS_I
                    if (wi < 0) countDot++;
                    else if (wi >= 0 && wi <= 5) countIdx[wi]++;
                }
                sb.AppendLine($"GRID_I {y} {row}");
            }
            sb.AppendLine($"COUNTS_I 0={countIdx[0]} 1={countIdx[1]} 2={countIdx[2]} 3={countIdx[3]} 4={countIdx[4]} 5={countIdx[5]} .={countDot}");
            
            // KEYFILES_SHA256
            sb.AppendLine("KEYFILES_SHA256:");
            string[] keyFiles = {
                "Rendering/AsciiRenderCore.cs",
                "Rendering/RayTracing.cs",
                "Rendering/AsciiRenderer.cs",
                "Rendering/Camera3D.cs",
                "Program.cs"
            };
            
            string baseDir = AppContext.BaseDirectory;
            // Ищем исходные файлы относительно рабочей директории
            string workspaceDir = "/workspace/AsciSurvival";
            
            foreach (string file in keyFiles)
            {
                string fullPath = Path.Combine(workspaceDir, file);
                if (File.Exists(fullPath))
                {
                    byte[] fileBytes = File.ReadAllBytes(fullPath);
                    byte[] hashBytes = SHA256.HashData(fileBytes);
                    string hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    sb.AppendLine($"{file}: {hashHex}");
                }
                else
                {
                    sb.AppendLine($"{file}: FILE_NOT_FOUND");
                }
            }
            
            // GIT: git rev-parse HEAD
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = workspaceDir,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    string? gitHash = proc?.StandardOutput.ReadToEnd().Trim();
                    sb.AppendLine($"GIT: {gitHash ?? "UNKNOWN"}");
                }
            }
            catch
            {
                sb.AppendLine("GIT: ERROR");
            }
            
            // REMOTE: git remote -v с удалённым токеном
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "remote -v",
                    WorkingDirectory = workspaceDir,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    string? remoteOutput = proc?.StandardOutput.ReadToEnd().Trim();
                    if (!string.IsNullOrEmpty(remoteOutput))
                    {
                        // Удаляем токены из URL
                        string sanitized = System.Text.RegularExpressions.Regex.Replace(
                            remoteOutput, 
                            @"https://[^@]+@github\.com", 
                            "https://github.com");
                        sb.AppendLine($"REMOTE:\n{sanitized}");
                    }
                }
            }
            catch
            {
                sb.AppendLine("REMOTE: ERROR");
            }
            
            // Сохранение отчёта
            string reportPath = "/workspace/Data/debug_report.txt";
            string? dir = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(reportPath, sb.ToString());
            
            Console.WriteLine($"Debug report saved to: {reportPath}");
        }
    }
}
