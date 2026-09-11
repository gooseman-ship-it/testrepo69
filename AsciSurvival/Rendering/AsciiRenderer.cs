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
        /// Использует per-cell raycasting для сплошных поверхностей
        /// </summary>
        public static void RenderTestScene(AsciiRenderCore core, Camera3D camera)
        {
            // Создаём список примитивов для raycasting-рендера
            var primitives = new List<Primitive>();

            // Пол (плоскость y=0, бесконечный)
            primitives.Add(Primitive.CreatePlane(0f, Color.GREEN, 1.0f));

            // 3 бокса (AABB) с позицией, размером (half-size), цветом
            // Расположены перед камерой (камера на Z=15, смотрит на Z=0)
            primitives.Add(Primitive.CreateBox(new Vector3(-5, 2, 5), 1.5f, Color.RED, 1.0f));
            primitives.Add(Primitive.CreateBox(new Vector3(5, 3, 7), 2f, Color.BLUE, 1.0f));
            primitives.Add(Primitive.CreateBox(new Vector3(0, 1, 2), 1f, Color.YELLOW, 1.0f));

            // 2 сферы с центром, радиусом, цветом
            primitives.Add(Primitive.CreateSphere(new Vector3(-8, 2, 8), 2f, Color.PURPLE, 1.0f));
            primitives.Add(Primitive.CreateSphere(new Vector3(8, 3, 5), 2.5f, Color.ORANGE, 1.0f));

            // Рендерим сцену через per-cell raycasting
            core.RenderScene(primitives, camera);
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
