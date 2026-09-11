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

        public static Primitive CreatePlane(float y, Color color, float brightness = 1.0f, float limitX = 0f, float limitZ = 0f) =>
            new Primitive { Type = PrimitiveType.Plane, Position = new Vector3(0, y, 0), Size = 0, Color = color, Brightness = limitX, _limitZ = limitZ };

        private float _limitZ;  // Для плоскости: ограничение по Z
        public float LimitX => Type == PrimitiveType.Plane ? Brightness : 0f;  // Для плоскости: limitX хранится в Brightness
        public float LimitZ => _limitZ;
    }

    /// <summary>
    /// Ядро ASCII-рендерера: чистая математика без зависимостей от окна/GPU.
    /// Проекция 3D точек на сетку символов, Z-буфер, выбор символа и цвета.
    /// </summary>
    public class AsciiRenderCore
    {
        /// Символьная рампа от тёмного к светлому (только ASCII, коды 32..126, не менее 24 символов)
        private const string SymbolRamp = " .'-:;~=+*<>!?|()[]{}#%@$&";
        
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
        
        public AsciiRenderCore()
        {
            int size = GridWidth * GridHeight;
            _zBuffer = new float[size];
            _symbolGrid = new char[size];
            _colorGrid = new uint[size];
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
        /// Рендеринг сцены с per-cell raycasting (сплошные поверхности)
        /// Для каждой клетки строится луч, находится ближайшее пересечение,
        /// вычисляется нормаль и shading по нормали.
        /// </summary>
        public void RenderScene(List<Primitive> primitives, Camera3D camera)
        {
            // Диагностический зонд для клеток (80, 89) и (80, 60)
            bool diagnosticDone = false;

            // Для каждой клетки сетки
            for (int y = 0; y < GridHeight; y++)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    // Построить луч из камеры через центр клетки
                    var (rayOrigin, rayDir) = RayTracing.BuildRayThroughCell(
                        x, y, GridWidth, GridHeight, Fovy, camera);

                    // Диагностический вывод для клетки (80, 89) - низ центра
                    if (!diagnosticDone && x == 80 && y == 89)
                    {
                        Console.WriteLine($"DIAGNOSTIC cell ({x},{y}):");
                        Console.WriteLine($"  rayDir = {rayDir}");
                        
                        // Проверка пересечения с полом
                        var planeHit = RayTracing.RayPlane(rayOrigin, rayDir, 0f, 0f, 0f);
                        Console.WriteLine($"  RayPlane: hit={planeHit.Hit}, t={planeHit.T}, hitPoint={planeHit.HitPoint}, normal={planeHit.Normal}");
                        
                        // Проверка dot с LightDir
                        if (planeHit.Hit)
                        {
                            Vector3 normal = planeHit.Normal;
                            if (Vector3.Dot(normal, rayDir) > 0f)
                            {
                                normal = -normal;
                            }
                            float dot = Vector3.Dot(normal, LightDir);
                            Console.WriteLine($"  normal(after flip) = {normal}");
                            Console.WriteLine($"  dot(normal, LightDir) = {dot}");
                        }
                        
                        diagnosticDone = true;
                    }

                    // Найти ближайшее пересечение со всеми примитивами
                    float minT = FarPlane;
                    RayTracing.HitResult closestHit = RayTracing.HitResult.Miss;
                    Color hitColor = Color.WHITE;
                    float hitBrightness = 1.0f;

                    foreach (var prim in primitives)
                    {
                        RayTracing.HitResult hit = prim.Type switch
                        {
                            PrimitiveType.Box => RayTracing.RayAABB(rayOrigin, rayDir, prim.Position, prim.Size),
                            PrimitiveType.Sphere => RayTracing.RaySphere(rayOrigin, rayDir, prim.Position, prim.Size),
                            PrimitiveType.Plane => RayTracing.RayPlane(rayOrigin, rayDir, prim.Position.Y, prim.LimitX, prim.LimitZ),
                            _ => RayTracing.HitResult.Miss
                        };

                        if (hit.Hit && hit.T > NearPlane && hit.T < minT)
                        {
                            minT = hit.T;
                            closestHit = hit;
                            hitColor = prim.Color;
                            hitBrightness = prim.Brightness;
                        }
                    }

                    // Если есть пересечение — записать в буфер
                    if (closestHit.Hit)
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

                        // Shading по нормали: brightness = max(0, dot(normal, lightDir))
                        float shade = MathF.Max(0f, Vector3.Dot(normal, LightDir));
                        float finalBrightness = hitBrightness * shade * BrightnessMultiplier;

                        _symbolGrid[index] = GetSymbol(depth, finalBrightness);
                        _colorGrid[index] = GetCellColor(depth, hitColor, finalBrightness);
                        _zBuffer[index] = depth;
                    }
                    // Если нет пересечения — клетка остаётся пробелом (уже очищена)
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
                    _symbolGrid[index] = GetSymbol(depth, brightness);
                    _colorGrid[index] = GetCellColor(depth, color, brightness);
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
