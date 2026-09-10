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
