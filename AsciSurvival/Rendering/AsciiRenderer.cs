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

            // Очистка всех буферов кадра (символы, цвета, Z-буфер)
            _core.ClearFrame();

            // Рендеринг тестовой сцены: сетка пола, боксы, сферы
            RenderTestScene(_core, camera);

            // Отрисовка ASCII-сетки на экране через батчинг символов
            DrawAsciiGrid();
        }

        /// <summary>
        /// Рендер тестовой сцены (общий метод для игрового рендера и headless)
        /// </summary>
        public static void RenderTestScene(AsciiRenderCore core, Camera3D camera)
        {
            // Сетка пола (20x20, 20 делений)
            core.RenderGrid(20f, 20, camera, Color.GREEN, 0.8f);

            // Коробки разных цветов и размеров
            core.RenderBox(new Vector3(-5, 2, -5), 3f, camera, Color.RED, 1.0f);
            core.RenderBox(new Vector3(5, 3, -3), 4f, camera, Color.BLUE, 1.0f);
            core.RenderBox(new Vector3(0, 1, -8), 2f, camera, Color.YELLOW, 1.0f);

            // Сферы
            core.RenderSphere(new Vector3(-8, 2, 3), 2f, camera, Color.PURPLE, 1.0f);
            core.RenderSphere(new Vector3(8, 3, 5), 2.5f, camera, Color.ORANGE, 1.0f);
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

            // Размер шрифта подбирается из метрик шрифта, а не приравнивается к cellHeight
            int fontSize = (int)MathF.Max(8f, cellHeight);

            for (int y = 0; y < _core.GridHeight; y++)
            {
                // Используем общий метод ядра для построения строки
                string line = _core.BuildLine(y, out uint[] lineColors);

                // Run-length батчинг: последовательные клетки одинакового ARGB32 рисуются одним DrawTextEx
                int x = 0;
                while (x < _core.GridWidth)
                {
                    // Пропускаем пустые клетки (пробелы без цвета)
                    if (lineColors[x] == 0)
                    {
                        x++;
                        continue;
                    }

                    // Находим конец последовательности с одинаковым цветом
                    uint currentColor = lineColors[x];
                    int runLength = 1;
                    while (x + runLength < _core.GridWidth && lineColors[x + runLength] == currentColor)
                    {
                        runLength++;
                    }

                    // Извлекаем подстроку для этого отрезка
                    string runText = line.Substring(x, runLength);

                    // Декодируем цвет из ARGB32
                    Color drawColor = AsciiRenderCore.UnpackARGB32(currentColor);

                    // Позиционирование по cellWidth явно
                    int posX = (int)(x * cellWidth);
                    int posY = (int)(y * cellHeight);

                    // Рисуем отрезок строки с точной позицией
                    Raylib.DrawText(runText, posX, posY, fontSize, drawColor);

                    x += runLength;
                }
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
