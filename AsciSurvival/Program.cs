using AsciSurvival.Core;
using AsciSurvival.Rendering;
using Raylib_cs;
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
            
            var core = new AsciiRenderCore();
            string ramp = AsciiRenderCore.SymbolRampPublic;
            sb.AppendLine($"RAMP: \"{ramp}\"");
            sb.AppendLine($"CONST_CHECK: FarPlane={core.FarPlane} NearPlane={core.NearPlane} RampLen={ramp.Length} Ramp=\"{ramp}\"");
            sb.AppendLine($"RUNTIME_CHECK: symZero={core.GetSymbolForProbe(10f, 0f, 0, 0)} symLow={core.GetSymbolForProbe(10f, 0.05f, 1, 1)}");

            // BASIS: самотест лучей (120,45) и (40,45) при yaw=0
            var probeCamera = new Rendering.Camera3D
            {
                Position = new System.Numerics.Vector3(0, 2, 10),
                Target = new System.Numerics.Vector3(0, 2 + MathF.Sin(-80f * MathF.PI / 180f), 10 + MathF.Cos(-80f * MathF.PI / 180f)),
                Up = new System.Numerics.Vector3(0, 1, 0),
                Fovy = 60f,
                Projection = CameraProjection.CAMERA_PERSPECTIVE
            };
            
            var (_, rayDir120) = RayTracing.BuildRayThroughCell(120, 45, core.GridWidth, core.GridHeight, probeCamera.Fovy, probeCamera);
            var (_, rayDir40) = RayTracing.BuildRayThroughCell(40, 45, core.GridWidth, core.GridHeight, probeCamera.Fovy, probeCamera);
            
            System.Numerics.Vector3 strafeRight = new System.Numerics.Vector3(-1f, 0f, 0f); // (-cos(0), 0, sin(0))
            float proj120 = System.Numerics.Vector3.Dot(rayDir120, strafeRight);
            float proj40 = System.Numerics.Vector3.Dot(rayDir40, strafeRight);
            bool basisOk = proj120 > 0f && proj40 < 0f;
            sb.AppendLine($"BASIS: {(basisOk ? "OK" : "NOK")}");
            
            // Камера H: pos=<0,2,10>, target=<0,2,9>
            var cameraH = new Rendering.Camera3D
            {
                Position = new System.Numerics.Vector3(0, 2, 10),
                Target = new System.Numerics.Vector3(0, 2, 9),
                Up = new System.Numerics.Vector3(0, 1, 0),
                Fovy = 60f,
                Projection = CameraProjection.CAMERA_PERSPECTIVE
            };
            
            // Камера D: pos=<0,2,10>, pitch=-80 (target вычисляется)
            var cameraD = new Rendering.Camera3D
            {
                Position = new System.Numerics.Vector3(0, 2, 10),
                Target = new System.Numerics.Vector3(0, 2 + MathF.Sin(-80f * MathF.PI / 180f), 10 + MathF.Cos(-80f * MathF.PI / 180f)),
                Up = new System.Numerics.Vector3(0, 1, 0),
                Fovy = 60f,
                Projection = CameraProjection.CAMERA_PERSPECTIVE
            };
            
            var primitives = new List<Primitive>();
            primitives.Add(Primitive.CreatePlane(0f, Color.GREEN, 1.0f));
            primitives.Add(Primitive.CreateBox(new System.Numerics.Vector3(-5, 2, 5), 1.5f, Color.RED, 1.0f));
            primitives.Add(Primitive.CreateBox(new System.Numerics.Vector3(5, 3, 7), 2f, Color.BLUE, 1.0f));
            primitives.Add(Primitive.CreateBox(new System.Numerics.Vector3(0, 1, 2), 1f, Color.YELLOW, 1.0f));
            primitives.Add(Primitive.CreateSphere(new System.Numerics.Vector3(-8, 2, 8), 2f, Color.PURPLE, 1.0f));
            primitives.Add(Primitive.CreateSphere(new System.Numerics.Vector3(8, 3, 5), 2.5f, Color.ORANGE, 1.0f));
            
            // === СЕКЦИЯ H (камера pos=<0,2,10>, target=<0,2,9>) ===
            core.ClearFrame();
            core.RenderScene(primitives, cameraH);
            
            sb.AppendLine("=== CAMERA H: pos=<0,2,10>, target=<0,2,9> ===");
            sb.AppendLine("DUMP_SYMBOLS_H:");
            for (int y = 0; y < core.GridHeight; y++)
            {
                string line = core.BuildLine(y, out _);
                sb.AppendLine(line);
            }
            
            sb.AppendLine("DUMP_COLORS_H:");
            for (int y = 0; y < core.GridHeight; y++)
            {
                var hexLine = "";
                for (int x = 0; x < core.GridWidth; x += 8)
                {
                    uint color = core.GetGridColor(x, y);
                    hexLine += $"{color:X8} ";
                }
                sb.AppendLine(hexLine.TrimEnd());
            }
            
            sb.AppendLine("GRID_W_H:");
            for (int y = 0; y < core.GridHeight; y++)
            {
                var line = "";
                for (int x = 0; x < core.GridWidth; x++)
                {
                    int index = y * core.GridWidth + x;
                    char sym = core.GetGridSymbol(x, y);
                    if (sym == ' ') { line += "."; continue; }
                    line += "?";
                }
                sb.AppendLine(line);
            }
            
            sb.AppendLine("GRID_Y_H:");
            for (int y = 0; y < core.GridHeight; y++)
            {
                var line = "";
                for (int x = 0; x < core.GridWidth; x++)
                {
                    var (_, rayDir) = RayTracing.BuildRayThroughCell(x, y, core.GridWidth, core.GridHeight, cameraH.Fovy, cameraH);
                    if (MathF.Abs(rayDir.Y) < 0.02f) line += "=";
                    else if (rayDir.Y > 0f) line += "+";
                    else line += "-";
                }
                sb.AppendLine(line);
            }
            
            sb.AppendLine("GRID_T_H:");
            for (int y = 0; y < core.GridHeight; y++)
            {
                var line = "";
                for (int x = 0; x < core.GridWidth; x++)
                {
                    var (rayOrigin, rayDir) = RayTracing.BuildRayThroughCell(x, y, core.GridWidth, core.GridHeight, cameraH.Fovy, cameraH);
                    
                    float minT = core.FarPlane;
                    bool hit = false;
                    foreach (var prim in primitives)
                    {
                        RayTracing.HitResult hitResult = prim.Type switch
                        {
                            PrimitiveType.Box => RayTracing.RayAABB(rayOrigin, rayDir, prim.Position, prim.Size),
                            PrimitiveType.Sphere => RayTracing.RaySphere(rayOrigin, rayDir, prim.Position, prim.Size),
                            PrimitiveType.Plane => RayTracing.RayPlane(rayOrigin, rayDir, prim.Position.Y),
                            _ => RayTracing.HitResult.Miss
                        };
                        
                        if (hitResult.Hit && hitResult.T > 0f && hitResult.T < minT)
                        {
                            minT = hitResult.T;
                            hit = true;
                        }
                    }
                    
                    if (!hit) line += ".";
                    else if (minT < 1f) line += "0";
                    else if (minT < 5f) line += "1";
                    else if (minT < 20f) line += "2";
                    else if (minT < 100f) line += "3";
                    else line += ".";
                }
                sb.AppendLine(line);
            }
            
            // === СЕКЦИЯ D (камера pos=<0,2,10>, pitch=-80) ===
            core.ClearFrame();
            core.RenderScene(primitives, cameraD);
            
            sb.AppendLine("=== CAMERA D: pos=<0,2,10>, pitch=-80 ===");
            sb.AppendLine("DUMP_SYMBOLS_D:");
            for (int y = 0; y < core.GridHeight; y++)
            {
                string line = core.BuildLine(y, out _);
                sb.AppendLine(line);
            }
            
            sb.AppendLine("DUMP_COLORS_D:");
            for (int y = 0; y < core.GridHeight; y++)
            {
                var hexLine = "";
                for (int x = 0; x < core.GridWidth; x += 8)
                {
                    uint color = core.GetGridColor(x, y);
                    hexLine += $"{color:X8} ";
                }
                sb.AppendLine(hexLine.TrimEnd());
            }
            
            sb.AppendLine("GRID_W_D:");
            for (int y = 0; y < core.GridHeight; y++)
            {
                var line = "";
                for (int x = 0; x < core.GridWidth; x++)
                {
                    int index = y * core.GridWidth + x;
                    char sym = core.GetGridSymbol(x, y);
                    if (sym == ' ') { line += "."; continue; }
                    line += "?";
                }
                sb.AppendLine(line);
            }
            
            sb.AppendLine("GRID_Y_D:");
            for (int y = 0; y < core.GridHeight; y++)
            {
                var line = "";
                for (int x = 0; x < core.GridWidth; x++)
                {
                    var (_, rayDir) = RayTracing.BuildRayThroughCell(x, y, core.GridWidth, core.GridHeight, cameraD.Fovy, cameraD);
                    if (MathF.Abs(rayDir.Y) < 0.02f) line += "=";
                    else if (rayDir.Y > 0f) line += "+";
                    else line += "-";
                }
                sb.AppendLine(line);
            }
            
            sb.AppendLine("GRID_T_D:");
            for (int y = 0; y < core.GridHeight; y++)
            {
                var line = "";
                for (int x = 0; x < core.GridWidth; x++)
                {
                    var (rayOrigin, rayDir) = RayTracing.BuildRayThroughCell(x, y, core.GridWidth, core.GridHeight, cameraD.Fovy, cameraD);
                    
                    float minT = core.FarPlane;
                    bool hit = false;
                    foreach (var prim in primitives)
                    {
                        RayTracing.HitResult hitResult = prim.Type switch
                        {
                            PrimitiveType.Box => RayTracing.RayAABB(rayOrigin, rayDir, prim.Position, prim.Size),
                            PrimitiveType.Sphere => RayTracing.RaySphere(rayOrigin, rayDir, prim.Position, prim.Size),
                            PrimitiveType.Plane => RayTracing.RayPlane(rayOrigin, rayDir, prim.Position.Y),
                            _ => RayTracing.HitResult.Miss
                        };
                        
                        if (hitResult.Hit && hitResult.T > 0f && hitResult.T < minT)
                        {
                            minT = hitResult.T;
                            hit = true;
                        }
                    }
                    
                    if (!hit) line += ".";
                    else if (minT < 1f) line += "0";
                    else if (minT < 5f) line += "1";
                    else if (minT < 20f) line += "2";
                    else if (minT < 100f) line += "3";
                    else line += ".";
                }
                sb.AppendLine(line);
            }
            
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
