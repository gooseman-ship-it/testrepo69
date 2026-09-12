using System;
using System.Numerics;

namespace AsciSurvival.Rendering
{
    /// <summary>
    /// Класс для трассировки лучей: построение лучей и пересечения с примитивами.
    /// Чистая математика, без зависимостей от состояния рендера.
    /// </summary>
    public static class RayTracing
    {
        /// <summary>
        /// Результат пересечения луча с поверхностью
        /// </summary>
        public struct HitResult
        {
            public bool Hit;           // Было ли пересечение
            public float T;            // Дистанция вдоль луча до точки попадания
            public Vector3 HitPoint;   // Точка попадания в мировых координатах
            public Vector3 Normal;     // Нормаль поверхности в точке попадания

            public static HitResult Miss => new HitResult { Hit = false };
        }

        /// <summary>
        /// Построение луча из камеры через клетку (x, y) сетки
        /// </summary>
        /// <param name="x">Координата клетки по X (0..GridWidth-1)</param>
        /// <param name="y">Координата клетки по Y (0..GridHeight-1)</param>
        /// <param name="gridWidth">Ширина сетки символов</param>
        /// <param name="gridHeight">Высота сетки символов</param>
        /// <param name="fovy">Вертикальный угол обзора в градусах</param>
        /// <param name="camera">Камера с позицией, направлением и вверх</param>
        /// <returns>Луч: (origin, direction)</returns>
        public static (Vector3 origin, Vector3 direction) BuildRayThroughCell(
            int x, int y, int gridWidth, int gridHeight, float fovy, Camera3D camera)
        {
            Vector3 camPos = camera.Position;
            Vector3 camTarget = camera.Target;
            Vector3 camUp = camera.Up;

            Vector3 forward = Vector3.Normalize(camTarget - camPos);
            
            // Устойчивое вычисление right/up через углы (не через cross)
            float yaw = MathF.Atan2(forward.X, forward.Z);
            float pitch = MathF.Asin(forward.Y);
            Vector3 right = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
            Vector3 up = new Vector3(
                -MathF.Sin(pitch) * MathF.Sin(yaw),
                MathF.Cos(pitch),
                -MathF.Sin(pitch) * MathF.Cos(yaw)
            );

            float nx = (x + 0.5f) / gridWidth * 2f - 1f;
            float ny = 1f - (y + 0.5f) / gridHeight * 2f;

            float aspect = (float)gridWidth / gridHeight;
            float tanFovY2 = MathF.Tan(fovy * 0.5f * MathF.PI / 180f);

            Vector3 dir = forward + nx * right * aspect * tanFovY2 + ny * up * tanFovY2;
            dir = Vector3.Normalize(dir);
            return (camPos, dir);
        }

        /// <summary>
        /// Пересечение луча с AABB (алгоритм slab)
        /// </summary>
        /// <param name="rayOrigin">Начало луча</param>
        /// <param name="rayDir">Направление луча (нормализовано)</param>
        /// <param name="center">Центр коробки</param>
        /// <param name="halfSize">Половина размера коробки (extent)</param>
        /// <returns>Результат пересечения</returns>
        public static HitResult RayAABB(Vector3 rayOrigin, Vector3 rayDir, Vector3 center, float halfSize)
        {
            Vector3 min = center - new Vector3(halfSize);
            Vector3 max = center + new Vector3(halfSize);

            float tmin = float.MinValue;
            float tmax = float.MaxValue;

            // Slab по X
            if (MathF.Abs(rayDir.X) < 1e-8f)
            {
                // Луч параллелен плоскости X
                if (rayOrigin.X < min.X || rayOrigin.X > max.X)
                    return HitResult.Miss;
            }
            else
            {
                float tx1 = (min.X - rayOrigin.X) / rayDir.X;
                float tx2 = (max.X - rayOrigin.X) / rayDir.X;
                tmin = MathF.Max(tmin, MathF.Min(tx1, tx2));
                tmax = MathF.Min(tmax, MathF.Max(tx1, tx2));
            }

            // Slab по Y
            if (MathF.Abs(rayDir.Y) < 1e-8f)
            {
                if (rayOrigin.Y < min.Y || rayOrigin.Y > max.Y)
                    return HitResult.Miss;
            }
            else
            {
                float ty1 = (min.Y - rayOrigin.Y) / rayDir.Y;
                float ty2 = (max.Y - rayOrigin.Y) / rayDir.Y;
                tmin = MathF.Max(tmin, MathF.Min(ty1, ty2));
                tmax = MathF.Min(tmax, MathF.Max(ty1, ty2));
            }

            // Slab по Z
            if (MathF.Abs(rayDir.Z) < 1e-8f)
            {
                if (rayOrigin.Z < min.Z || rayOrigin.Z > max.Z)
                    return HitResult.Miss;
            }
            else
            {
                float tz1 = (min.Z - rayOrigin.Z) / rayDir.Z;
                float tz2 = (max.Z - rayOrigin.Z) / rayDir.Z;
                tmin = MathF.Max(tmin, MathF.Min(tz1, tz2));
                tmax = MathF.Min(tmax, MathF.Max(tz1, tz2));
            }

            // Проверка: есть ли пересечение
            if (tmax < tmin || tmax < 0f)
                return HitResult.Miss;

            // Берём ближайшее пересечение (tmin может быть отрицательным если камера внутри)
            float t = tmin >= 0f ? tmin : tmax;
            if (t < 0f)
                return HitResult.Miss;

            Vector3 hitPoint = rayOrigin + rayDir * t;

            // Вычисление нормали: определяем какую грань пересекли
            Vector3 rel = hitPoint - center;
            float absX = MathF.Abs(rel.X);
            float absY = MathF.Abs(rel.Y);
            float absZ = MathF.Abs(rel.Z);

            Vector3 normal;
            if (absX > absY && absX > absZ)
                normal = new Vector3(MathF.Sign(rel.X), 0, 0);
            else if (absY > absX && absY > absZ)
                normal = new Vector3(0, MathF.Sign(rel.Y), 0);
            else
                normal = new Vector3(0, 0, MathF.Sign(rel.Z));

            return new HitResult
            {
                Hit = true,
                T = t,
                HitPoint = hitPoint,
                Normal = normal
            };
        }

        /// <summary>
        /// Пересечение луча со сферой (квадратное уравнение)
        /// </summary>
        /// <param name="rayOrigin">Начало луча</param>
        /// <param name="rayDir">Направление луча (нормализовано)</param>
        /// <param name="center">Центр сферы</param>
        /// <param name="radius">Радиус сферы</param>
        /// <returns>Результат пересечения</returns>
        public static HitResult RaySphere(Vector3 rayOrigin, Vector3 rayDir, Vector3 center, float radius)
        {
            Vector3 oc = rayOrigin - center;
            float a = Vector3.Dot(rayDir, rayDir);
            float b = 2f * Vector3.Dot(oc, rayDir);
            float c = Vector3.Dot(oc, oc) - radius * radius;

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
                return HitResult.Miss;

            float sqrtDisc = MathF.Sqrt(discriminant);
            float t0 = (-b - sqrtDisc) / (2f * a);
            float t1 = (-b + sqrtDisc) / (2f * a);

            // Берём ближайшее положительное пересечение
            float t = t0 >= 0f ? t0 : t1;
            if (t < 0f)
                return HitResult.Miss;

            Vector3 hitPoint = rayOrigin + rayDir * t;
            Vector3 normal = Vector3.Normalize(hitPoint - center);

            return new HitResult
            {
                Hit = true,
                T = t,
                HitPoint = hitPoint,
                Normal = normal
            };
        }

        /// <summary>
        /// Пересечение луча с плоскостью y = planeY (для пола).
        /// Пол бесконечный, ограничения по X/Z не применяются — дальние клетки гаснут по глубине (FarPlane).
        /// </summary>
        /// <param name="rayOrigin">Начало луча</param>
        /// <param name="rayDir">Направление луча (нормализовано)</param>
        /// <param name="planeY">Y-координата плоскости (пол на y=0)</param>
        /// <returns>Результат пересечения</returns>
        public static HitResult RayPlane(Vector3 rayOrigin, Vector3 rayDir, float planeY)
        {
            // Если луч параллелен плоскости
            if (MathF.Abs(rayDir.Y) < 1e-8f)
                return HitResult.Miss;

            float t = (planeY - rayOrigin.Y) / rayDir.Y;
            if (t < 0f)
                return HitResult.Miss;

            // FarPlane передаётся извне через AsciiRenderCore, здесь просто возвращаем пересечение
            // Отсечение по FarPlane делается в RenderScene при сравнении t < minT

            Vector3 hitPoint = rayOrigin + rayDir * t;

            return new HitResult
            {
                Hit = true,
                T = t,
                HitPoint = hitPoint,
                Normal = new Vector3(0, 1, 0)  // Нормаль пола направлена вверх
            };
        }
    }
}
