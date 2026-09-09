using System;
using System.Numerics;
using Raylib_cs;
using AsciSurvival.Core;

namespace AsciSurvival.Rendering
{
    public class Camera3D
    {
        public Raylib_cs.Camera3D Data { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Target { get; set; }
        public Vector3 Up { get; set; }
        public float Fovy { get; set; }
        public CameraProjection Projection { get; set; }
        
        public float MouseSensitivity = 0.1f;
        public float MoveSpeed = 5.0f;
        public float SprintMultiplier = 1.8f;
        
        private float _pitch = 0.0f;
        private float _yaw = 0.0f;
        private float _verticalVelocity = 0.0f;
        private bool _isGrounded = false;

        // Константа для перевода градусов в радианы (совместима с .NET 8)
        private const float Deg2Rad = MathF.PI / 180.0f;

        public Camera3D()
        {
            Position = new Vector3(0, 2, 10);
            Target = new Vector3(0, 2, 9);
            Up = new Vector3(0, 1, 0);
            Fovy = 60.0f;
            Projection = CameraProjection.Perspective;
            
            Data = new Raylib_cs.Camera3D
            {
                Position = Position,
                Target = Target,
                Up = Up,
                Fovy = Fovy,
                Projection = Projection
            };
        }

        public void Update(float deltaTime, Vector3 playerPos)
        {
            if (!InputManager.IsMenuOpen && Raylib.IsCursorHidden())
            {
                Vector2 mouseDelta = InputManager.GetMouseDelta();
                
                _yaw -= mouseDelta.X * MouseSensitivity;
                _pitch -= mouseDelta.Y * MouseSensitivity;

                _pitch = Clamp(_pitch, -89.0f, 89.0f);
            }

            Vector2 inputDir = Vector2.Zero;
            
            KeyboardKey forwardKey = InputManager.GetKey("MoveForward");
            KeyboardKey backwardKey = InputManager.GetKey("MoveBackward");
            KeyboardKey leftKey = InputManager.GetKey("MoveLeft");
            KeyboardKey rightKey = InputManager.GetKey("MoveRight");
            
            if (forwardKey != KeyboardKey.None && InputManager.IsKeyDown(forwardKey)) inputDir.Y = -1;
            if (backwardKey != KeyboardKey.None && InputManager.IsKeyDown(backwardKey)) inputDir.Y = 1;
            if (leftKey != KeyboardKey.None && InputManager.IsKeyDown(leftKey)) inputDir.X = -1;
            if (rightKey != KeyboardKey.None && InputManager.IsKeyDown(rightKey)) inputDir.X = 1;

            float speed = MoveSpeed;
            
            KeyboardKey sprintKey = InputManager.GetKey("Sprint");
            KeyboardKey crouchKey = InputManager.GetKey("Crouch");
            
            if (sprintKey != KeyboardKey.None && InputManager.IsKeyDown(sprintKey)) 
                speed *= SprintMultiplier;
            if (crouchKey != KeyboardKey.None && InputManager.IsKeyDown(crouchKey)) 
                speed *= 0.5f;

            Vector3 forward = new Vector3(MathF.Sin(_yaw * Deg2Rad), 0, MathF.Cos(_yaw * Deg2Rad));
            Vector3 right = new Vector3(MathF.Sin((_yaw - 90) * Deg2Rad), 0, MathF.Cos((_yaw - 90) * Deg2Rad));

            Vector3 moveVector = (forward * -inputDir.Y + right * inputDir.X);
            if (moveVector.Length() > 0) moveVector = Vector3.Normalize(moveVector);

            Position += moveVector * speed * deltaTime;

            KeyboardKey jumpKey = InputManager.GetKey("Jump");
            if (jumpKey != KeyboardKey.None && InputManager.IsKeyPressed(jumpKey) && _isGrounded)
            {
                _verticalVelocity = 7.0f;
                _isGrounded = false;
            }

            _verticalVelocity -= 20.0f * deltaTime;
            Position = new Vector3(Position.X, Position.Y + _verticalVelocity * deltaTime, Position.Z);

            if (Position.Y < 2.0f) 
            {
                Position = new Vector3(Position.X, 2.0f, Position.Z);
                _verticalVelocity = 0;
                _isGrounded = true;
            }

            Target = new Vector3(
                Position.X + MathF.Sin(_yaw * Deg2Rad) * MathF.Cos(_pitch * Deg2Rad),
                Position.Y + MathF.Sin(_pitch * Deg2Rad),
                Position.Z + MathF.Cos(_yaw * Deg2Rad) * MathF.Cos(_pitch * Deg2Rad)
            );

            Data.Position = Position;
            Data.Target = Target;
            Data.Up = Up;
            Data.Fovy = Fovy;
            Data.Projection = Projection;
        }
        
        private static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
    }
}
