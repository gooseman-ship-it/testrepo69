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
            _core = new AsciiRenderCore(320, 180);
            
            // Загружаем шрифт или используем дефолтный
            string fontPath = ConfigManager.Graphics.FontPath;
            if (System.IO.File.Exists(fontPath) && fontPath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            {
                int targetSize = Math.Max(4, Raylib.GetScreenHeight() / _core.GridHeight);
                _font = Raylib.LoadFontEx(fontPath, targetSize, null, 0);
                Raylib.SetTextureFilter(_font.texture, TextureFilter.TEXTURE_FILTER_POINT);
            }
            else if (System.IO.File.Exists(fontPath))
            {
                _font = Raylib.LoadFont(fontPath);
                Raylib.SetTextureFilter(_font.texture, TextureFilter.TEXTURE_FILTER_POINT);
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
        /// Отрисовка ASCII-сетки на экране посимвольно с явной позицией клетки
        /// </summary>
        private void DrawAsciiGrid()
        {
            // Вычисляем размер клетки в пикселях из текущего размера окна
            int screenWidth = Raylib.GetScreenWidth();
            int screenHeight = Raylib.GetScreenHeight();
            float cellWidth = (float)screenWidth / _core.GridWidth;
            float cellHeight = (float)screenHeight / _core.GridHeight;

            // Размер шрифта подбирается из метрик шрифта, а не приравнивается к cellHeight
            int fontSize = _font.baseSize;

            // Посимвольно: каждый символ рисуется точно в клетке (x*cellWidth, y*cellHeight).
            // Дефолтный шрифт Raylib не моноширинный, поэтому группировать нельзя —
            // накопление реальных ширин glyph ломает выравнивание и даёт чёрные зазоры.
            for (int y = 0; y < _core.GridHeight; y++)
            {
                for (int x = 0; x < _core.GridWidth; x++)
                {
                    uint argb = _core.GetGridColor(x, y);
                    if (argb == 0) continue;

                    char sym = _core.GetGridSymbol(x, y);
                    if (sym == ' ') continue;

                    Color drawColor = AsciiRenderCore.UnpackARGB32(argb);
                    int codepoint = (int)sym;
                    Raylib.DrawTextCodepoint(
                        _font,
                        codepoint,
                        new Vector2((float)(x * cellWidth), (float)(y * cellHeight)),
                        fontSize,
                        drawColor);
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
