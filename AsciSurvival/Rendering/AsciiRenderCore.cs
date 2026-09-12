using System;
using System.Numerics;
using System.Collections.Generic;
using Raylib_cs;

namespace AsciSurvival.Rendering
{
    /// <summary>
    /// Тип примитива сцены
    /// </summary>
    public enum PrimitiveType
    {
        Box,      // AABB коробка
        Sphere,   // Сфера
        Plane     // Плоскость (пол)
    }

    /// <summary>
    /// Примитив сцены: геометрия + материал
    /// </summary>
    public struct Primitive
    {
        public PrimitiveType Type;
        public Vector3 Position;    // Для бокса/сферы - центр, для плоскости - точка на плоскости
        public float Size;          // Для бокса - половина размера, для сферы - радиус
        public Color Color;         // Базовый цвет поверхности
        public float Brightness;    // Базовая яркость (множитель)

        public static Primitive CreateBox(Vector3 center, float halfSize, Color color, float brightness = 1.0f) =>
            new Primitive { Type = PrimitiveType.Box, Position = center, Size = halfSize, Color = color, Brightness = brightness };

        public static Primitive CreateSphere(Vector3 center, float radius, Color color, float brightness = 1.0f) =>
            new Primitive { Type = PrimitiveType.Sphere, Position = center, Size = radius, Color = color, Brightness = brightness };

        public static Primitive CreatePlane(float y, Color color, float brightness = 1.0f) =>
            new Primitive { Type = PrimitiveType.Plane, Position = new Vector3(0, y, 0), Size = 0, Color = color, Brightness = brightness };
    }

    /// <summary>
    /// Ядро ASCII-рендерера: чистая математика без зависимостей от окна/GPU.
    /// Проекция 3D точек на сетку символов, Z-буфер, выбор символа и цвета.
    /// </summary>
    public class AsciiRenderCore
    {
        /// Символьная рампа от тёмного к светлому (10 символов, проверена по плотности)
        private const string SymbolRamp = " .:-=+*#%@";
        
        /// Публичный доступ к рампе для headless-дампа
        public static string SymbolRampPublic => SymbolRamp;
        
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
        private uint[] _colorGrid;  // 32-бит ARGB8888
        private float[] _brightnessBuffer;  // Буфер яркости для сглаживания 3x3
        
        public AsciiRenderCore()
        {
            int size = GridWidth * GridHeight;
            _zBuffer = new float[size];
            _symbolGrid = new char[size];
            _colorGrid = new uint[size];
            _brightnessBuffer = new float[size];
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
        /// Выбор символа из рампы на основе яркости с байеровским дизерингом 4x4
        /// </summary>
        public char GetSymbolWithDither(float depth, float brightness, int x, int y)
        {
            // Яркость уже сглажена и содержит все необходимые множители (shade, BrightnessMultiplier)
            // Применяем только мягкое глубинное затухание для согласованности с цветом
            float normalizedDepth = Clamp((depth - NearPlane) / (FarPlane - NearPlane), 0f, 1f);
            float depthFade = 1f - normalizedDepth * 0.3f;
            float intensity = Clamp(brightness * depthFade, 0f, 1f);

            // Байеровский дизеринг 4x4 для сглаживания переходов между символами
            int[,] BayerMatrix4x4 = {
                { 0,  8,  2, 10},
                {12,  4, 14,  6},
                { 3, 11,  1,  9},
                {15,  7, 13,  5}
            };
            
            float ditherOffset = (BayerMatrix4x4[x & 3, y & 3] + 0.5f) / 16f - 0.5f;
            float rampIndexFloat = intensity * (SymbolRamp.Length - 1) + ditherOffset;
            int rampIndex = (int)MathF.Round(rampIndexFloat);
            rampIndex = Clamp(rampIndex, 0, SymbolRamp.Length - 1);
            
            return SymbolRamp[rampIndex];
        }
        
        /// <summary>
        /// Вычисление цвета с мягким глубинным затуханием (без изолиний)
        /// </summary>
        public uint GetCellColorWithFade(float depth, Color baseColor, float brightness)
        {
            // Мягкое затухание с глубиной (линейное, без квантования)
            float normalizedDepth = Clamp((depth - NearPlane) / (FarPlane - NearPlane), 0f, 1f);
            float depthFade = 1f - normalizedDepth * 0.3f;  // 30% затухание на дальней дистанции
            
            // Применяем яркость с BrightnessMultiplier
            float intensity = brightness * BrightnessMultiplier * depthFade;
            intensity = Clamp(intensity, 0f, 1f);
            
            // Модулируем каждый канал с клампом 0..255
            int r = Clamp((int)(baseColor.r * intensity), 0, 255);
            int g = Clamp((int)(baseColor.g * intensity), 0, 255);
            int b = Clamp((int)(baseColor.b * intensity), 0, 255);
            byte a = 255;  // Полностью непрозрачный
            
            return PackARGB32(a, (byte)r, (byte)g, (byte)b);
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
        /// Упаковка цвета в 32-бит ARGB8888
        /// Формат: AAAAAAAA RRRRRRRR GGGGGGGG BBBBBBBB
        /// </summary>
        public static uint PackARGB32(byte a, byte r, byte g, byte b)
        {
            return (uint)((a << 24) | (r << 16) | (g << 8) | b);
        }
        
        /// <summary>
        /// Распаковка 32-бит ARGB8888 в Color
        /// </summary>
        public static Color UnpackARGB32(uint argb)
        {
            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            byte g = (byte)((argb >> 8) & 0xFF);
            byte b = (byte)(argb & 0xFF);
            return new Color((int)r, (int)g, (int)b, (int)a);
        }
        
        /// <summary>
        /// Вычисление цвета на основе глубины и базового цвета объекта
        /// Возвращает упакованный 32-бит ARGB8888
        /// </summary>
        public uint GetCellColor(float depth, Color baseColor, float brightness = 1.0f)
        {
            // Затухание с глубиной
            float depthFade = Clamp(1f - (depth - NearPlane) / (FarPlane - NearPlane) * 0.5f, 0.3f, 1f);
            
            // Применяем яркость с BrightnessMultiplier
            float intensity = brightness * BrightnessMultiplier * depthFade;
            intensity = Clamp(intensity, 0f, 1f);
            
            // Модулируем каждый канал с клампом 0..255
            int r = Clamp((int)(baseColor.r * intensity), 0, 255);
            int g = Clamp((int)(baseColor.g * intensity), 0, 255);
            int b = Clamp((int)(baseColor.b * intensity), 0, 255);
            byte a = 255;  // Полностью непрозрачный
            
            return PackARGB32(a, (byte)r, (byte)g, (byte)b);
        }

        /// <summary>
        /// Направленный свет для shading (заглушка).
        /// Конвенция: LightDir — направление ОТ поверхности К источнику света.
        /// Свет слева-сверху-спереди от камеры для освещения верхних и фронтальных граней.
        /// </summary>
        private static readonly Vector3 LightDir = Vector3.Normalize(new Vector3(-0.4f, 0.8f, 0.5f));

        /// <summary>
        /// Публичный доступ к LightDir для зондов
        /// </summary>
        public static Vector3 LightDirPublic => LightDir;

        /// <summary>
        /// Метод для зонда: получение символа по t и shade
        /// </summary>
        public char GetSymbolForProbe(float t, float shade, int x, int y)
        {
            return GetSymbolWithDither(t, shade, x, y);
        }

        /// <summary>
        /// Метод для зонда: получение цвета по t и baseColor/shade
        /// </summary>
        public uint GetColorForProbe(float t, Color baseColor, float shade)
        {
            return GetCellColorWithFade(t, baseColor, shade);
        }

        /// <summary>
        /// Рендеринг сцены с per-cell raycasting (сплошные поверхности)
        /// Для каждой клетки строится луч, находится ближайшее пересечение,
        /// вычисляется нормаль и shading по нормали.
        /// </summary>
        public void RenderScene(List<Primitive> primitives, Camera3D camera)
        {
            // Первый проход: трассировка лучей, заполнение _brightnessBuffer и _zBuffer
            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    // Построить луч из камеры через центр клетки
                    var (rayOrigin, rayDir) = RayTracing.BuildRayThroughCell(
                        x, y, GridWidth, GridHeight, Fovy, camera);

                    // Инициализировать minT = float.MaxValue для устранения клина горизонта
                    float minT = float.MaxValue;
                    RayTracing.HitResult closestHit = RayTracing.HitResult.Miss;
                    Color hitColor = Color.WHITE;
                    float hitBrightness = 1.0f;
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

                        if (hit.Hit && hit.T > NearPlane && hit.T < minT)
                        {
                            minT = hit.T;
                            closestHit = hit;
                            hitColor = prim.Color;
                            hitBrightness = prim.Brightness;
                            hitType = prim.Type;
                        }
                    }

                    // Отсечение по дали применяем ОДНИМ условием после выбора победителя
                    if (closestHit.Hit && minT <= FarPlane)
                    {
                        int index = y * GridWidth + x;
                        float depth = minT;

                        // Нормаль должна смотреть НАВСТРЕЧУ лучу (в сторону камеры)
                        // Если dot(normal, rayDir) > 0, значит нормаль смотрит в ту же сторону что и луч — переворачиваем
                        Vector3 normal = closestHit.Normal;
                        if (Vector3.Dot(normal, rayDir) > 0f)
                        {
                            normal = -normal;
                        }

                        // Угловой шейдинг: только освещение от источника, без углового члена
                        float lightDot = MathF.Max(0f, Vector3.Dot(normal, LightDir));
                        float shade = lightDot; // Только освещение от источника, без углового члена
                        float finalBrightness = hitBrightness * shade * BrightnessMultiplier;

                        // Сохраняем яркость в буфер для последующего сглаживания 3×3
                        _brightnessBuffer[index] = finalBrightness;
                        _zBuffer[index] = depth;
                        
                        // Вычисляем и сохраняем цвет с затуханием по глубине
                        uint cellColor = GetCellColorWithFade(minT, hitColor, hitBrightness * (1f - Clamp((minT - NearPlane) / (FarPlane - NearPlane), 0f, 1f) * 0.15f));
                        _colorGrid[index] = cellColor;
                    }
                    else
                    {
                        // Нет пересечения или t > FarPlane — клетка остаётся пробелом
                        int index = y * GridWidth + x;
                        _brightnessBuffer[index] = 0f;
                        _zBuffer[index] = float.MaxValue;
                    }
                }
            }

            // Второй проход: сглаживание яркости окном 3×3 и запись символов
            ApplyBrightnessSmoothingAndFillSymbols();
        }

        /// <summary>
        /// Сглаживание яркости окном 3×3 с весами (центр 4, стороны 2, углы 1)
        /// и заполнение _symbolGrid сглаженными значениями
        /// </summary>
        private void ApplyBrightnessSmoothingAndFillSymbols()
        {
            float[] smoothedBuffer = new float[_brightnessBuffer.Length];
            
            // Веса окна 3×3: центр 4, соседи по сторонам 2, углы 1
            // Сумма весов = 4 + 4*2 + 4*1 = 4 + 8 + 4 = 16

            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    float weightedSum = 0f;
                    float currentWeightSum = 0f;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;

                            // Края без заворота — пропускаем выходящие за границы
                            if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight)
                                continue;

                            int nIndex = ny * GridWidth + nx;
                            
                            // Определяем вес в зависимости от позиции
                            float weight;
                            if (dx == 0 && dy == 0)
                                weight = 4f; // центр
                            else if (dx == 0 || dy == 0)
                                weight = 2f; // соседи по сторонам
                            else
                                weight = 1f; // углы

                            weightedSum += _brightnessBuffer[nIndex] * weight;
                            currentWeightSum += weight;
                        }
                    }

                    smoothedBuffer[y * GridWidth + x] = weightedSum / currentWeightSum;
                }
            }

            // Заполняем _symbolGrid используя сглаженные значения яркости
            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    int index = y * GridWidth + x;
                    float depth = _zBuffer[index];
                    
                    if (depth < float.MaxValue && depth <= FarPlane)
                    {
                        float smoothedBrightness = smoothedBuffer[index];
                        _symbolGrid[index] = GetSymbolWithDither(depth, smoothedBrightness, x, y);
                    }
                    else
                    {
                        _symbolGrid[index] = ' ';
                    }
                }
            }
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
                    _symbolGrid[index] = GetSymbolWithDither(depth, brightness, gridX, gridY);
                    _colorGrid[index] = GetCellColorWithFade(depth, color, brightness);
                }
            }
        }
        
        /// <summary>
        /// Построение строки кадра для заданной строки сетки
        /// Возвращает строку символов и массив цветов (ARGB32) для этой строки
        /// </summary>
        public string BuildLine(int y, out uint[] lineColors)
        {
            lineColors = new uint[GridWidth];
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
        /// Получить цвет для клетки (ARGB32)
        /// </summary>
        public uint GetGridColor(int x, int y)
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
        /// Получить весь массив цветов (ARGB32) для анализа
        /// </summary>
        public uint[] GetColorArray() => (uint[])_colorGrid.Clone();
        
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
            
            // Шаг не крупнее трети мировой величины клетки на этой дистанции для гарантии сплошности
            float maxStepSize = cellWorldSizeAtDist * 0.3f;
            
            // Адаптивное количество шагов: минимум 10, максимум исходя из длины
            int steps = Math.Max(10, (int)(lineLength / maxStepSize));
            
            // Ограничиваем максимум для производительности (4000 шагов достаточно для длинных линий)
            steps = Math.Min(steps, 4000);
            
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
