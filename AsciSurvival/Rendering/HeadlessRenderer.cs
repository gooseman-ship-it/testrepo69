using System;
using System.IO;
using System.Numerics;
using Raylib_cs;

namespace AsciSurvival.Rendering
{
    /// <summary>
    /// Headless-рендерер для автотестов без GPU.
    /// Рендерит тестовую сцену в текстовый файл.
    /// </summary>
    public class HeadlessRenderer
    {
        private readonly AsciiRenderCore _core;
        private readonly Camera3D _camera;
        
        public HeadlessRenderer()
        {
            _core = new AsciiRenderCore(320, 180);
            _camera = new Camera3D
            {
                Position = new Vector3(0, 5, 15),
                Target = new Vector3(0, 2, 0),
                Up = new Vector3(0, 1, 0),
                Fovy = 60f,
                Projection = CameraProjection.CAMERA_PERSPECTIVE
            };
        }
        
        /// <summary>
        /// Рендер тестовой сцены: боксы, сферы, сетка-пол
        /// </summary>
        public void RenderTestScene()
        {
            // Очистка всех буферов кадра (символы, цвета, Z-буфер)
            _core.ClearFrame();
            
            // Используем общий метод рендерера для тестовой сцены
            AsciiRenderer.RenderTestScene(_core, _camera);
            
            // Сохранение дампа
            SaveFrameDump("Data/test_frame.txt");
        }
        
        /// <summary>
        /// Зонд для отладки щели: камера pos=<0,2,10>, pitch=-80, строка 170, колонки 140..180
        /// Выводит таблицу: rayDir, победитель (тип или NONE), t, код символа, цвет hex
        /// </summary>
        public void RunGapProbe()
        {
            // Камера для зонда: pos=<0,2,10>, pitch=-80 (взгляд вниз)
            var probeCamera = new Camera3D
            {
                Position = new Vector3(0, 2, 10),
                Target = new Vector3(0, 2 + MathF.Sin(-80f * MathF.PI / 180f), 10 + MathF.Cos(-80f * MathF.PI / 180f)),
                Up = new Vector3(0, 1, 0),
                Fovy = 60f,
                Projection = CameraProjection.CAMERA_PERSPECTIVE
            };
            
            // Создаём тестовую сцену
            var primitives = new List<Primitive>();
            primitives.Add(Primitive.CreatePlane(0f, Color.GREEN, 1.0f));
            primitives.Add(Primitive.CreateBox(new Vector3(-5, 2, 5), 1.5f, Color.RED, 1.0f));
            primitives.Add(Primitive.CreateBox(new Vector3(5, 3, 7), 2f, Color.BLUE, 1.0f));
            primitives.Add(Primitive.CreateBox(new Vector3(0, 1, 2), 1f, Color.YELLOW, 1.0f));
            primitives.Add(Primitive.CreateSphere(new Vector3(-8, 2, 8), 2f, Color.PURPLE, 1.0f));
            primitives.Add(Primitive.CreateSphere(new Vector3(8, 3, 5), 2.5f, Color.ORANGE, 1.0f));
            
            Console.WriteLine("=== GAP PROBE: camera pos=<0,2,10>, pitch=-80, row=170, cols=140..180 ===");
            Console.WriteLine("Columns: rayDir.X,Y,Z | winner | t | symbol(code) | color(hex)");
            
            int probeRow = 170;
            for (int x = 140; x <= 180; x++)
            {
                var (rayOrigin, rayDir) = RayTracing.BuildRayThroughCell(
                    x, probeRow, _core.GridWidth, _core.GridHeight, probeCamera.Fovy, probeCamera);
                
                float minT = _core.FarPlane;
                RayTracing.HitResult closestHit = RayTracing.HitResult.Miss;
                PrimitiveType hitType = PrimitiveType.Plane;
                
                foreach (var prim in primitives)
                {
                    RayTracing.HitResult hit = prim.Type switch
                    {
                        PrimitiveType.Box => RayTracing.RayAABB(rayOrigin, rayDir, prim.Position, prim.Size),
                        PrimitiveType.Sphere => RayTracing.RaySphere(rayOrigin, rayDir, prim.Position, prim.Size),
                        PrimitiveType.Plane => RayTracing.RayPlane(rayOrigin, rayDir, prim.Position.Y),
                        _ => RayTracing.HitResult.Miss
                    };
                    
                    if (hit.Hit && hit.T > _core.NearPlane && hit.T < minT)
                    {
                        minT = hit.T;
                        closestHit = hit;
                        hitType = prim.Type;
                    }
                }
                
                string winner = closestHit.Hit ? hitType.ToString() : "NONE";
                float t = closestHit.Hit ? minT : -1f;
                
                // Симуляция GetSymbolWithDither для этой клетки
                char symbol = ' ';
                uint color = 0;
                if (closestHit.Hit)
                {
                    Vector3 normal = closestHit.Normal;
                    if (Vector3.Dot(normal, rayDir) > 0f)
                        normal = -normal;
                    
                    float lightDot = MathF.Max(0f, Vector3.Dot(normal, AsciiRenderCore.LightDirPublic));
                    float shade = lightDot; // Только освещение от источника, без углового члена
                    
                    // Получаем символ и цвет через core
                    int index = probeRow * _core.GridWidth + x;
                    // Для зонда используем упрощённую логику
                    symbol = _core.GetSymbolForProbe(t, shade, x, probeRow);
                    color = _core.GetColorForProbe(t, closestHit.Hit ? primitives.Find(p => p.Type == hitType).Color : Color.WHITE, shade);
                }
                
                Console.WriteLine($"{x}: rayDir({rayDir.X:F3},{rayDir.Y:F3},{rayDir.Z:F3}) | {winner,-6} | t={t:F2} | '{symbol}'({(int)symbol}) | {color:X8}");
            }
            
            // Таблица зонда из 20 клеток для игровой камеры (pos=<0,2,10>, pitch=-80)
            Console.WriteLine("");
            Console.WriteLine("=== ZOND TABLE: 20 cells for camera pos=<0,2,10>, pitch=-80 ===");
            Console.WriteLine("Format: (x,y) | hit | t | normal(X,Y,Z) | lightDot | finalBrightness");
            
            int[] probeXs = { 140, 150, 160, 170, 180 };
            int[] probeYs = { 80, 90, 100, 110 };
            
            foreach (int py in probeYs)
            {
                foreach (int px in probeXs)
                {
                    var (rayOrigin, rayDir) = RayTracing.BuildRayThroughCell(
                        px, py, _core.GridWidth, _core.GridHeight, probeCamera.Fovy, probeCamera);
                    
                    float minT = _core.FarPlane;
                    RayTracing.HitResult closestHit = RayTracing.HitResult.Miss;
                    PrimitiveType hitType = PrimitiveType.Plane;
                    
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
                            closestHit = hitResult;
                            hitType = prim.Type;
                        }
                    }
                    
                    bool hit = closestHit.Hit;
                    float tVal = hit ? minT : -1f;
                    Vector3 normal = hit ? closestHit.Normal : new Vector3(0, 0, 0);
                    float lightDot = 0f;
                    float finalBrightness = 0f;
                    
                    if (hit)
                    {
                        // Переворот нормали навстречу лучу
                        if (Vector3.Dot(normal, rayDir) > 0f)
                            normal = -normal;
                        
                        lightDot = MathF.Max(0f, Vector3.Dot(normal, AsciiRenderCore.LightDirPublic));
                        float shade = lightDot;
                        float hitBrightness = hitType == PrimitiveType.Plane ? 1.0f : primitives.Find(p => p.Type == hitType).Brightness;
                        finalBrightness = hitBrightness * shade * _core.BrightnessMultiplier;
                    }
                    
                    Console.WriteLine($"({px},{py}) | hit={hit,-5} | t={tVal,-6:F2} | normal=({normal.X:F2},{normal.Y:F2},{normal.Z:F2}) | lightDot={lightDot:F2} | finalBrightness={finalBrightness:F2}");
                }
            }
            
            // Самотест согласованности базисов: проверка лучей клеток (240,90) и (80,90) при yaw=0
            Console.WriteLine("");
            Console.WriteLine("=== BASIS SELF-TEST ===");
            // При yaw=0 стрейф вправо = (-cos(0), 0, sin(0)) = (-1, 0, 0)
            Vector3 strafeRight = new Vector3(-1f, 0f, 0f);
            
            var (rayOrigin120, rayDir120) = RayTracing.BuildRayThroughCell(
                240, 90, _core.GridWidth, _core.GridHeight, probeCamera.Fovy, probeCamera);
            var (rayOrigin40, rayDir40) = RayTracing.BuildRayThroughCell(
                80, 90, _core.GridWidth, _core.GridHeight, probeCamera.Fovy, probeCamera);
            
            // Проекция луча на вектор стрейфа: dot(rayDir, strafeRight)
            float proj120 = Vector3.Dot(rayDir120, strafeRight);
            float proj40 = Vector3.Dot(rayDir40, strafeRight);
            
            // Луч клетки (240,90) должен иметь положительную компоненту вдоль стрейфа вправо
            // Луч клетки (80,90) должен иметь отрицательную компоненту
            bool basisOk = proj120 > 0f && proj40 < 0f;
            Console.WriteLine($"Cell (240,90): rayDir=({rayDir120.X:F3},{rayDir120.Y:F3},{rayDir120.Z:F3}), proj_on_strafe={proj120:F3}");
            Console.WriteLine($"Cell (80,90): rayDir=({rayDir40.X:F3},{rayDir40.Y:F3},{rayDir40.Z:F3}), proj_on_strafe={proj40:F3}");
            Console.WriteLine($"BASIS: {(basisOk ? "OK" : "NOK")}");
        }
        
        /// <summary>
        /// Сохранение кадра в текстовый файл
        /// </summary>
        private void SaveFrameDump(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            using (var writer = new StreamWriter(path))
            {
                // Заголовок с информацией о разрешении
                writer.WriteLine($"ASCII Frame Dump");
                writer.WriteLine($"Grid: {_core.GridWidth}x{_core.GridHeight}");
                writer.WriteLine($"Camera: pos={_camera.Position}, target={_camera.Target}");
                writer.WriteLine($"RAMP: \"{AsciiRenderCore.SymbolRampPublic}\"");
                writer.WriteLine(new string('-', _core.GridWidth));
                
                // Построчный вывод символов через общий метод BuildLine
                for (int y = 0; y < _core.GridHeight; y++)
                {
                    string line = _core.BuildLine(y, out _);
                    writer.WriteLine(line);
                }
                
                // Секция GRID_W: типы победителей (P=Plane, B=Box, S=Sphere, N=None)
                writer.WriteLine();
                writer.WriteLine("GRID_W");
                for (int y = 0; y < _core.GridHeight; y++)
                {
                    var line = "";
                    for (int x = 0; x < _core.GridWidth; x++)
                    {
                        byte type = _core.GetWinnerType(x, y);
                        char ch = type switch
                        {
                            0 => 'P',
                            1 => 'B',
                            2 => 'S',
                            _ => 'N'
                        };
                        line += ch;
                    }
                    writer.WriteLine(line);
                }
                
                // Секция GRID_I: индексы победителей (0..N-1 или . при -1)
                writer.WriteLine();
                writer.WriteLine("GRID_I");
                for (int y = 0; y < _core.GridHeight; y++)
                {
                    var line = "";
                    for (int x = 0; x < _core.GridWidth; x++)
                    {
                        int index = _core.GetWinnerIndex(x, y);
                        line += index >= 0 ? $"{index % 10}" : ".";
                    }
                    writer.WriteLine(line);
                }
                
                // Секция COUNTS: подсчёт типов по всей сетке
                writer.WriteLine();
                writer.WriteLine("COUNTS");
                int countP = 0, countB = 0, countS = 0, countN = 0;
                for (int y = 0; y < _core.GridHeight; y++)
                {
                    for (int x = 0; x < _core.GridWidth; x++)
                    {
                        byte type = _core.GetWinnerType(x, y);
                        switch (type)
                        {
                            case 0: countP++; break;
                            case 1: countB++; break;
                            case 2: countS++; break;
                            default: countN++; break;
                        }
                    }
                }
                writer.WriteLine($"P={countP}");
                writer.WriteLine($"B={countB}");
                writer.WriteLine($"S={countS}");
                writer.WriteLine($"N={countN}");
                
                // Разделитель и информация о цветах (ARGB32)
                writer.WriteLine(new string('-', _core.GridWidth));
                writer.WriteLine("Color data (ARGB32 hex values):");
                
                // Выводим только каждый 8-й пиксель по горизонтали и вертикали для компактности
                for (int y = 0; y < _core.GridHeight; y += 8)
                {
                    var hexLine = "";
                    for (int x = 0; x < _core.GridWidth; x += 8)
                    {
                        uint color = _core.GetGridColor(x, y);
                        hexLine += $"{color:X8} ";
                    }
                    writer.WriteLine(hexLine.TrimEnd());
                }
            }
            
            Console.WriteLine($"Frame dump saved to: {path}");
        }
    }
}
