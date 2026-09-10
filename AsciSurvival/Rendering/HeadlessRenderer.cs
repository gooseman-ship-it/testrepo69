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
            // Очистка буферов
            _core.ClearZBuffer();
            
            // Сетка пола (20x20, 20 делений)
            _core.RenderGrid(20f, 20, _camera, Color.GREEN, 0.8f);
            
            // Несколько коробок разных цветов и размеров
            _core.RenderBox(new Vector3(-5, 2, -5), 3f, _camera, Color.RED, 1.0f);
            _core.RenderBox(new Vector3(5, 3, -3), 4f, _camera, Color.BLUE, 1.0f);
            _core.RenderBox(new Vector3(0, 1, -8), 2f, _camera, Color.YELLOW, 1.0f);
            
            // Сферы
            _core.RenderSphere(new Vector3(-8, 2, 3), 2f, _camera, Color.PURPLE, 1.0f);
            _core.RenderSphere(new Vector3(8, 3, 5), 2.5f, _camera, Color.ORANGE, 1.0f);
            
            // Сохранение дампа
            SaveFrameDump("Data/test_frame.txt");
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
                
                // Построчный вывод символов
                for (int y = 0; y < _core.GridHeight; y++)
                {
                    var lineChars = new char[_core.GridWidth];
                    for (int x = 0; x < _core.GridWidth; x++)
                    {
                        lineChars[x] = _core.GetGridSymbol(x, y);
                    }
                    writer.WriteLine(new string(lineChars));
                }
                
                // Разделитель и информация о цветах (RGB565)
                writer.WriteLine(new string('-', _core.GridWidth));
                writer.WriteLine("Color data (RGB565 hex values):");
                
                // Выводим только каждый 8-й пиксель по горизонтали и вертикали для компактности
                for (int y = 0; y < _core.GridHeight; y += 8)
                {
                    var hexLine = "";
                    for (int x = 0; x < _core.GridWidth; x += 8)
                    {
                        ushort color = _core.GetGridColor(x, y);
                        hexLine += $"{color:X4} ";
                    }
                    writer.WriteLine(hexLine.TrimEnd());
                }
            }
            
            Console.WriteLine($"Frame dump saved to: {path}");
        }
    }
}
