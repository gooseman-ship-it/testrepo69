using System.Numerics;
using AsciSurvival.Core;
using AsciSurvival.Rendering;

namespace AsciSurvival.Gameplay
{
    public class Player
    {
        public Vector3 Position { get; set; }
        public float Health { get; set; } = 100.0f;
        public float Hunger { get; set; } = 100.0f;
        public float Thirst { get; set; } = 100.0f;
        public float Temperature { get; set; } = 36.6f;

        public Player()
        {
            Position = new Vector3(0, 2, 0);
        }

        public void Update(float deltaTime, Rendering.Camera3D camera)
        {
            // Синхронизируем позицию игрока с камерой
            camera.Update(deltaTime, Position);
            Position = camera.Position;

            // Простая симуляция выживания
            Hunger -= 0.5f * deltaTime;
            Thirst -= 1.0f * deltaTime;
            
            // Влияние холода
            if (Temperature < 35.0f)
            {
                Health -= 0.2f * deltaTime;
            }
            
            // Ограничения параметров
            Hunger = MathF.Max(0, Hunger);
            Thirst = MathF.Max(0, Thirst);
            Temperature = MathF.Clamp(Temperature, 30.0f, 42.0f);
        }
    }
}
