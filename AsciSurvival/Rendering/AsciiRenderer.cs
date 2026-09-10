using Raylib_cs;
using AsciSurvival.Core;
using System.Numerics;

namespace AsciSurvival.Rendering
{
    public class AsciiRenderer : IDisposable
    {
        private int _width;
        private int _height;
        private Font _font;
        private bool _isDisposed = false;
        private AsciiRenderCore _core;

        public AsciiRenderer(int width, int height)
        {
            _width = width;
            _height = height;
            _core = new AsciiRenderCore();
            
            // Загружаем шрифт или используем дефолтный
            string fontPath = ConfigManager.Graphics.FontPath;
            if (System.IO.File.Exists(fontPath))
            {
                _font = Raylib.LoadFont(fontPath);
            }
            else
            {
                _font = Raylib.GetFontDefault();
            }
        }

        public void Render(Camera3D camera, Vector3 playerPosition)
        {
            if (_isDisposed) return;

            // Очистка Z-буфера ядра рендерера
            _core.ClearZBuffer();

            // Рендеринг тестовой сцены: сетка пола, боксы, сферы
            // Сетка пола (20x20, 20 делений)
            _core.RenderGrid(20f, 20, camera, Color.GREEN, 0.8f);

            // Коробки разных цветов и размеров
            _core.RenderBox(new Vector3(-5, 2, -5), 3f, camera, Color.RED, 1.0f);
            _core.RenderBox(new Vector3(5, 3, -3), 4f, camera, Color.BLUE, 1.0f);
            _core.RenderBox(new Vector3(0, 1, -8), 2f, camera, Color.YELLOW, 1.0f);

            // Сферы
            _core.RenderSphere(new Vector3(-8, 2, 3), 2f, camera, Color.PURPLE, 1.0f);
            _core.RenderSphere(new Vector3(8, 3, 5), 2.5f, camera, Color.ORANGE, 1.0f);

            // Отрисовка ASCII-сетки на экране через батчинг символов
            DrawAsciiGrid();
        }

        /// <summary>
        /// Отрисовка ASCII-сетки на экране через батчинг (один DrawText на строку)
        /// </summary>
        private void DrawAsciiGrid()
        {
            // Вычисляем размер клетки в пикселях из текущего размера окна
            int screenWidth = Raylib.GetScreenWidth();
            int screenHeight = Raylib.GetScreenHeight();
            float cellWidth = (float)screenWidth / _core.GridWidth;
            float cellHeight = (float)screenHeight / _core.GridHeight;

            // Размер шрифта подбирается под высоту клетки
            int fontSize = (int)MathF.Max(8f, cellHeight);

            // Буфер для построения строки
            char[] lineBuffer = new char[_core.GridWidth];

            for (int y = 0; y < _core.GridHeight; y++)
            {
                // Формируем строку символов
                for (int x = 0; x < _core.GridWidth; x++)
                {
                    lineBuffer[x] = _core.GetGridSymbol(x, y);
                }

                string line = new string(lineBuffer);

                // Получаем цвет клетки (для простоты берём усреднённый по строке)
                // В полной реализации можно делать градиент или использовать текстуру
                ushort avgColor = 0;
                int colorCount = 0;
                for (int x = 0; x < _core.GridWidth; x++)
                {
                    ushort c = _core.GetGridColor(x, y);
                    if (c != 0)
                    {
                        avgColor = c;
                        colorCount++;
                    }
                }

                Color drawColor = colorCount > 0 
                    ? AsciiRenderCore.DequantizeFromRGB565(avgColor) 
                    : Color.WHITE;

                // Рисуем строку одним вызовом (батчинг)
                Raylib.DrawText(line, 0, (int)(y * cellHeight), fontSize, drawColor);
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                // Проверяем, что шрифт не дефолтный (по id текстуры)
                Font defaultFont = Raylib.GetFontDefault();
                if (_font.texture.id != defaultFont.texture.id)
                {
                    Raylib.UnloadFont(_font);
                }
                _isDisposed = true;
            }
        }
    }
}
