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

        public AsciiRenderer(int width, int height)
        {
            _width = width;
            _height = height;
            
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

            // Устанавливаем камеру для 3D рендеринга
            Raylib.BeginMode3D(camera.Data);

            // Здесь будет рендеринг ASCII-объектов мира
            // Для vertical slice рисуем простую сетку
            
            Raylib.DrawGrid(20, 1.0f);

            Raylib.EndMode3D();
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_font.Id != Raylib.GetFontDefault().Id)
                {
                    Raylib.UnloadFont(_font);
                }
                _isDisposed = true;
            }
        }
    }
}
