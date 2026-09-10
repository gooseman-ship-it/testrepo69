using System;
using System.Numerics;
using Raylib_cs;

namespace AsciSurvival.Rendering
{
    /// <summary>
    /// Ядро ASCII-рендерера: чистая математика без зависимостей от окна/GPU.
    /// Проекция 3D точек на сетку символов, Z-буфер, выбор символа и цвета.
    /// </summary>
    public class AsciiRenderCore
    {
        // Символьная рампа от тёмного к светлому
        private const string SymbolRamp = " .:-=+*#%@";
        
        // Разрешение сетки символов (фиксированное количество клеток)
        public int GridWidth { get; } = 160;
        public int GridHeight { get; } = 90;
        
        // Параметры камеры
        public float Fovy { get; set; } = 60f;
        public float NearPlane { get; set; } = 0.1f;
        public float FarPlane { get; set; } = 100f;
        
        // Внешний множитель яркости (точка расширения для освещения)
        public float BrightnessMultiplier { get; set; } = 1.0f;
        
        // Буферы
        private float[] _zBuffer;
        private char[] _symbolGrid;
        private ushort[] _colorGrid;
        
        public AsciiRenderCore()
        {
            int size = GridWidth * GridHeight;
            _zBuffer = new float[size];
            _symbolGrid = new char[size];
            _colorGrid = new ushort[size];
        }
        
        /// <summary>
        /// Очистка всех буферов кадра: Z-буфер, символы (пробелы), цвета
        /// Вызывается КАЖДЫЙ кадр перед рендерингом
        /// </summary>
        public void ClearFrame()
        {
            for (int i = 0; i < _zBuffer.Length; i++)
            {
                _zBuffer[i] = float.MaxValue;
                _symbolGrid[i] = ' ';
                _colorGrid[i] = 0;
            }
        }
        
        /// <summary>
        /// Устаревший метод, оставлен для совместимости
        /// </summary>
        [Obsolete("Используйте ClearFrame() вместо ClearZBuffer()")]
        public void ClearZBuffer()
        {
            for (int i = 0; i < _zBuffer.Length; i++)
                _zBuffer[i] = float.MaxValue;
        }
        
        /// <summary>
        /// Преобразование世界овых координат в экранные (сетка символов)
        /// Возвращает false если точка за камерой или вне экрана
        /// </summary>
        public bool ProjectWorldToGrid(Vector3 worldPoint, Camera3D camera, 
            out int gridX, out int gridY, out float depth)
        {
            gridX = -1;
            gridY = -1;
            depth = 0f;
            
            // Перевод точки в пространство камеры
            Vector3 camPos = camera.Position;
            Vector3 camTarget = camera.Target;
            Vector3 camUp = camera.Up;
            
            // Вектор взгляда
            Vector3 forward = Vector3.Normalize(camTarget - camPos);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, camUp));
            Vector3 up = Vector3.Cross(right, forward);
            
            // Вектор от камеры до точки
            Vector3 toPoint = worldPoint - camPos;
            
            // Проверка: точка за камерой
            float distAlongView = Vector3.Dot(toPoint, forward);
            if (distAlongView <= NearPlane)
                return false;
            
            // Проецирование на плоскость камеры
            float halfFovyRad = (Fovy * 0.5f) * MathF.PI / 180f;
            float aspectRatio = (float)GridWidth / GridHeight;
            
            float projDist = distAlongView * MathF.Tan(halfFovyRad);
            float halfWidth = projDist * aspectRatio;
            float halfHeight = projDist;
            
            // Координаты в пространстве камеры
            float xCam = Vector3.Dot(toPoint, right);
            float yCam = Vector3.Dot(toPoint, up);
            
            // Нормализованные координаты [-1, 1]
            float nx = xCam / halfWidth;
            float ny = yCam / halfHeight;
            
            // Проверка границ экрана
            if (nx < -1f || nx > 1f || ny < -1f || ny > 1f)
                return false;
            
            // Преобразование в координаты сетки
            gridX = (int)((nx + 1f) * 0.5f * GridWidth);
            gridY = (int)((1f - ny) * 0.5f * GridHeight);
            
            // Глубина (для Z-буфера используем дистанцию вдоль луча зрения)
            depth = distAlongView;
            
            return gridX >= 0 && gridX < GridWidth && gridY >= 0 && gridY < GridHeight;
        }
        
        /// <summary>
        /// Обновление Z-буфера для клетки
        /// </summary>
        public bool UpdateZBuffer(int gridX, int gridY, float depth)
        {
            int index = gridY * GridWidth + gridX;
            if (depth < _zBuffer[index])
            {
                _zBuffer[index] = depth;
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Выбор символа из рампы на основе глубины и яркости
        /// </summary>
        public char GetSymbol(float depth, float brightness = 1.0f)
        {
            // Нормализуем глубину в диапазон [0, 1] для выбора символа
            float normalizedDepth = Clamp((depth - NearPlane) / (FarPlane - NearPlane), 0f, 1f);
            
            // Применяем множитель яркости (извне для освещения)
            float adjustedBrightness = Clamp(brightness * BrightnessMultiplier, 0f, 1f);
            
            // Комбинируем глубину и яркость: дальние объекты темнее
            float intensity = adjustedBrightness * (1f - normalizedDepth * 0.5f);
            
            int rampIndex = (int)(intensity * (SymbolRamp.Length - 1));
            rampIndex = Clamp(rampIndex, 0, SymbolRamp.Length - 1);
            
            return SymbolRamp[rampIndex];
        }
        
        /// <summary>
        /// Вспомогательный метод Clamp для float
        /// </summary>
        private static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
        
        /// <summary>
        /// Вспомогательный метод Clamp для int
        /// </summary>
        private static int Clamp(int value, int min, int max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
        
        /// <summary>
        /// Квантование цвета в RGB565 (16 бит)
        /// Формат: RRRRR GGGGGG BBBBB (5-6-5 бит)
        /// </summary>
        public ushort QuantizeToRGB565(Color color)
        {
            // Квантование: R и B по 5 бит (0-31), G по 6 бит (0-63)
            byte r5 = (byte)((color.r >> 3) & 0x1F);
            byte g6 = (byte)((color.g >> 2) & 0x3F);
            byte b5 = (byte)((color.b >> 3) & 0x1F);
            
            // Упаковка в 16 бит: RRRRRGGGGGGBBBBB
            return (ushort)((r5 << 11) | (g6 << 5) | b5);
        }
        
        /// <summary>
        /// Декодирование RGB565 обратно в Color (для отладки/рендера)
        /// </summary>
        public static Color DequantizeFromRGB565(ushort rgb565)
        {
            byte r5 = (byte)((rgb565 >> 11) & 0x1F);
            byte g6 = (byte)((rgb565 >> 5) & 0x3F);
            byte b5 = (byte)(rgb565 & 0x1F);
            
            // Масштабирование обратно к 8 битам
            byte r8 = (byte)((r5 * 255) / 31);
            byte g8 = (byte)((g6 * 255) / 63);
            byte b8 = (byte)((b5 * 255) / 31);
            
            return new Color((int)r8, (int)g8, (int)b8, 255);
        }
        
        /// <summary>
        /// Вычисление цвета на основе глубины и базового цвета объекта
        /// </summary>
        public ushort GetCellColor(float depth, Color baseColor, float brightness = 1.0f)
        {
            // Затухание с глубиной
            float depthFade = Clamp(1f - (depth - NearPlane) / (FarPlane - NearPlane) * 0.5f, 0.3f, 1f);
            
            // Применяем яркость
            float intensity = brightness * BrightnessMultiplier * depthFade;
            
            Color modulated = new Color(
                (int)(baseColor.r * intensity),
                (int)(baseColor.g * intensity),
                (int)(baseColor.b * intensity),
                255
            );
            
            return QuantizeToRGB565(modulated);
        }
        
        /// <summary>
        /// Рендер примитива (точки) в сетку с Z-тестом
        /// </summary>
        public void RenderPoint(Vector3 worldPos, Camera3D camera, Color color, float brightness = 1.0f)
        {
            if (ProjectWorldToGrid(worldPos, camera, out int gridX, out int gridY, out float depth))
            {
                if (UpdateZBuffer(gridX, gridY, depth))
                {
                    int index = gridY * GridWidth + gridX;
                    _symbolGrid[index] = GetSymbol(depth, brightness);
                    _colorGrid[index] = GetCellColor(depth, color, brightness);
                }
            }
        }
        
        /// <summary>
        /// Построение строки кадра для заданной строки сетки
        /// Возвращает строку символов и массив цветов для этой строки
        /// </summary>
        public string BuildLine(int y, out ushort[] lineColors)
        {
            lineColors = new ushort[GridWidth];
            char[] lineBuffer = new char[GridWidth];
            
            for (int x = 0; x < GridWidth; x++)
            {
                int index = y * GridWidth + x;
                lineBuffer[x] = _symbolGrid[index];
                lineColors[x] = _colorGrid[index];
            }
            
            return new string(lineBuffer);
        }
        
        /// <summary>
        /// Получить символ для клетки
        /// </summary>
        public char GetGridSymbol(int x, int y)
        {
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight)
                return ' ';
            return _symbolGrid[y * GridWidth + x];
        }
        
        /// <summary>
        /// Получить цвет для клетки (RGB565)
        /// </summary>
        public ushort GetGridColor(int x, int y)
        {
            if (x < 0 || x >= GridWidth || y < 0 || y >= GridHeight)
                return 0;
            return _colorGrid[y * GridWidth + x];
        }
        
        /// <summary>
        /// Получить весь массив символов для дампа
        /// </summary>
        public char[] GetSymbolArray() => (char[])_symbolGrid.Clone();
        
        /// <summary>
        /// Получить весь массив цветов (RGB565) для анализа
        /// </summary>
        public ushort[] GetColorArray() => (ushort[])_colorGrid.Clone();
        
        /// <summary>
        /// Рендер линии между двумя точками (алгоритм Брезенхема 3D)
        /// Количество шагов адаптивное — от длины линии и дистанции до камеры
        /// </summary>
        public void RenderLine(Vector3 start, Vector3 end, Camera3D camera, Color color, float brightness = 1.0f)
        {
            // Вычисляем длину линии в мировом пространстве
            float lineLength = Vector3.Distance(start, end);
            
            // Вычисляем среднюю дистанцию до камеры для определения плотности шагов
            Vector3 midPoint = (start + end) * 0.5f;
            float distToCamera = Vector3.Distance(midPoint, camera.Position);
            
            // Мировая величина клетки на этой дистанции (примерно)
            // Чем дальше объект, тем больше его проекция на сетку
            float fovRad = Fovy * MathF.PI / 180f;
            float cellWorldSizeAtDist = (distToCamera * MathF.Tan(fovRad * 0.5f) * 2f) / GridHeight;
            
            // Шаг не крупнее половины мировой величины клетки на этой дистанции
            float maxStepSize = cellWorldSizeAtDist * 0.5f;
            
            // Адаптивное количество шагов: минимум 10, максимум исходя из длины
            int steps = Math.Max(10, (int)(lineLength / maxStepSize));
            
            // Ограничиваем максимум для производительности
            steps = Math.Min(steps, 200);
            
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 point = Vector3.Lerp(start, end, t);
                RenderPoint(point, camera, color, brightness);
            }
        }
        
        /// <summary>
        /// Рендер коробки (каркас из 12 рёбер)
        /// </summary>
        public void RenderBox(Vector3 center, float size, Camera3D camera, Color color, float brightness = 1.0f)
        {
            float h = size * 0.5f;
            Vector3 min = new Vector3(center.X - h, center.Y - h, center.Z - h);
            Vector3 max = new Vector3(center.X + h, center.Y + h, center.Z + h);
            
            // 12 рёбер коробки
            // Нижняя грань
            RenderLine(new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), camera, color, brightness);
            RenderLine(new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z), camera, color, brightness);
            RenderLine(new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z), camera, color, brightness);
            RenderLine(new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, min.Y, min.Z), camera, color, brightness);
            
            // Верхняя грань
            RenderLine(new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z), camera, color, brightness);
            RenderLine(new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z), camera, color, brightness);
            RenderLine(new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), camera, color, brightness);
            RenderLine(new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z), camera, color, brightness);
            
            // Вертикальные рёбра
            RenderLine(new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z), camera, color, brightness);
            RenderLine(new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), camera, color, brightness);
            RenderLine(new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), camera, color, brightness);
            RenderLine(new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), camera, color, brightness);
        }
        
        /// <summary>
        /// Рендер сферы (каркас из меридианов и параллелей)
        /// </summary>
        public void RenderSphere(Vector3 center, float radius, Camera3D camera, Color color, float brightness = 1.0f)
        {
            int meridians = 12;
            int parallels = 8;
            
            // Меридианы
            for (int i = 0; i < meridians; i++)
            {
                float angle1 = (float)i * MathF.PI * 2f / meridians;
                float angle2 = (float)(i + 1) * MathF.PI * 2f / meridians;
                
                Vector3 prev = new Vector3(
                    center.X + radius * MathF.Sin(angle1),
                    center.Y,
                    center.Z + radius * MathF.Cos(angle1)
                );
                
                for (int j = 0; j <= parallels; j++)
                {
                    float v = (float)j / parallels * MathF.PI;
                    Vector3 curr = new Vector3(
                        center.X + radius * MathF.Sin(v) * MathF.Sin(angle1),
                        center.Y + radius * MathF.Cos(v),
                        center.Z + radius * MathF.Sin(v) * MathF.Cos(angle1)
                    );
                    RenderLine(prev, curr, camera, color, brightness);
                    prev = curr;
                }
            }
            
            // Параллели
            for (int i = 1; i < parallels; i++)
            {
                float v = (float)i * MathF.PI / parallels;
                float y = center.Y + radius * MathF.Cos(v);
                float r = radius * MathF.Sin(v);
                
                Vector3 prev = Vector3.Zero;
                for (int j = 0; j <= meridians; j++)
                {
                    float angle = (float)j * MathF.PI * 2f / meridians;
                    Vector3 curr = new Vector3(
                        center.X + r * MathF.Sin(angle),
                        y,
                        center.Z + r * MathF.Cos(angle)
                    );
                    if (j > 0)
                        RenderLine(prev, curr, camera, color, brightness);
                    prev = curr;
                }
            }
        }
        
        /// <summary>
        /// Рендер сетки пола
        /// </summary>
        public void RenderGrid(float size, int divisions, Camera3D camera, Color color, float brightness = 1.0f)
        {
            float step = size / divisions;
            float half = size * 0.5f;
            
            // Линии по X
            for (int i = 0; i <= divisions; i++)
            {
                float z = -half + i * step;
                RenderLine(new Vector3(-half, 0, z), new Vector3(half, 0, z), camera, color, brightness);
            }
            
            // Линии по Z
            for (int i = 0; i <= divisions; i++)
            {
                float x = -half + i * step;
                RenderLine(new Vector3(x, 0, -half), new Vector3(x, 0, half), camera, color, brightness);
            }
        }
    }
}
