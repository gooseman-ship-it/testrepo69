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
            _core = new AsciiRenderCore();
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
        /// Зонд для отладки щели: камера pos=<0,2,10>, pitch=-80, строка 85, колонки 70..90
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
            
            Console.WriteLine("=== GAP PROBE: camera pos=<0,2,10>, pitch=-80, row=85, cols=70..90 ===");
            Console.WriteLine("Columns: rayDir.X,Y,Z | winner | t | symbol(code) | color(hex)");
            
            int probeRow = 85;
            for (int x = 70; x <= 90; x++)
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
                    float viewDot = MathF.Max(0f, Vector3.Dot(normal, -rayDir));
                    float shade = lightDot * viewDot;
                    
                    // Получаем символ и цвет через core
                    int index = probeRow * _core.GridWidth + x;
                    // Для зонда используем упрощённую логику
                    symbol = _core.GetSymbolForProbe(t, shade, x, probeRow);
                    color = _core.GetColorForProbe(t, closestHit.Hit ? primitives.Find(p => p.Type == hitType).Color : Color.WHITE, shade);
                }
                
                Console.WriteLine($"{x}: rayDir({rayDir.X:F3},{rayDir.Y:F3},{rayDir.Z:F3}) | {winner,-6} | t={t:F2} | '{symbol}'({(int)symbol}) | {color:X8}");
            }
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
                writer.WriteLine(new string('-', _core.GridWidth));
                
                // Построчный вывод символов через общий метод BuildLine
                for (int y = 0; y < _core.GridHeight; y++)
                {
                    string line = _core.BuildLine(y, out _);
                    writer.WriteLine(line);
                }
                
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
