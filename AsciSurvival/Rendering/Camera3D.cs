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
        
        // Высота глаз: стоя и в приседе
        public const float EyeHeightStanding = 2.0f;
        public const float EyeHeightCrouching = 1.2f;
        
        private float _pitch = 0.0f;
        private float _yaw = 0.0f;
        private float _verticalVelocity = 0.0f;
        private bool _isGrounded = false;
        private bool _isCrouching = false;
        private float _currentEyeHeight = EyeHeightStanding;

        // Константа для перевода градусов в радианы (совместима с .NET 8)
        private const float Deg2Rad = MathF.PI / 180.0f;

        public Camera3D()
        {
            Position = new Vector3(0, EyeHeightStanding, 10);
            Target = new Vector3(0, EyeHeightStanding, 9);
            Up = new Vector3(0, 1, 0);
            Fovy = 60.0f;
            Projection = CameraProjection.CAMERA_PERSPECTIVE;
            
            Data = new Raylib_cs.Camera3D
            {
                position = Position,
                target = Target,
                up = Up,
                fovy = Fovy,
                projection = Projection
            };
        }

        public void Update(float deltaTime, Vector3 playerPos)
        {
            Vector2 mouseDelta = InputManager.GetMouseDelta(); // читать всегда
            bool focused = Raylib.IsWindowFocused() && Raylib.IsCursorHidden();
            if (focused && mouseDelta.Length() <= 150f)
            {
                _yaw -= mouseDelta.X * MouseSensitivity;
                _pitch = Clamp(_pitch - mouseDelta.Y * MouseSensitivity, -89f, 89f);
            }
            // Клавиши движения применять только при focused.

            Vector2 inputDir = Vector2.Zero;
            
            KeyboardKey forwardKey = InputManager.GetKey("MoveForward");
            KeyboardKey backwardKey = InputManager.GetKey("MoveBackward");
            KeyboardKey leftKey = InputManager.GetKey("MoveLeft");
            KeyboardKey rightKey = InputManager.GetKey("MoveRight");
            
            // Клавиши движения применяются только при фокусе
            if (focused)
            {
                if (forwardKey != KeyboardKey.KEY_NULL && InputManager.IsKeyDown(forwardKey)) inputDir.Y = -1;
                if (backwardKey != KeyboardKey.KEY_NULL && InputManager.IsKeyDown(backwardKey)) inputDir.Y = 1;
                if (leftKey != KeyboardKey.KEY_NULL && InputManager.IsKeyDown(leftKey)) inputDir.X = -1;
                if (rightKey != KeyboardKey.KEY_NULL && InputManager.IsKeyDown(rightKey)) inputDir.X = 1;
            }

            float speed = MoveSpeed;
            
            KeyboardKey sprintKey = InputManager.GetKey("Sprint");
            KeyboardKey crouchKey = InputManager.GetKey("Crouch");
            
            if (sprintKey != KeyboardKey.KEY_NULL && InputManager.IsKeyDown(sprintKey)) 
                speed *= SprintMultiplier;
            
            // Обработка приседания: сглаженное переключение высоты глаз и замедление
            bool wantCrouch = crouchKey != KeyboardKey.KEY_NULL && InputManager.IsKeyDown(crouchKey);
            if (wantCrouch != _isCrouching)
            {
                _isCrouching = wantCrouch;
            }
            
            // Плавное изменение высоты глаз (линейная интерполяция)
            float targetEyeHeight = _isCrouching ? EyeHeightCrouching : EyeHeightStanding;
            _currentEyeHeight = _currentEyeHeight + (targetEyeHeight - _currentEyeHeight) * deltaTime * 10f;
            
            if (_isCrouching)
                speed *= 0.5f;

            Vector3 forward = new Vector3(MathF.Sin(_yaw * Deg2Rad), 0, MathF.Cos(_yaw * Deg2Rad));
            Vector3 right = new Vector3(MathF.Sin((_yaw - 90) * Deg2Rad), 0, MathF.Cos((_yaw - 90) * Deg2Rad));

            Vector3 moveVector = (forward * -inputDir.Y + right * inputDir.X);
            if (moveVector.Length() > 0) moveVector = Vector3.Normalize(moveVector);

            Position += moveVector * speed * deltaTime;

            KeyboardKey jumpKey = InputManager.GetKey("Jump");
            if (jumpKey != KeyboardKey.KEY_NULL && InputManager.IsKeyPressed(jumpKey) && _isGrounded)
            {
                _verticalVelocity = 7.0f;
                _isGrounded = false;
            }

            _verticalVelocity -= 20.0f * deltaTime;
            Position = new Vector3(Position.X, Position.Y + _verticalVelocity * deltaTime, Position.Z);

            // Напольный кламп использует текущую высоту глаз (с учётом приседа)
            if (Position.Y < _currentEyeHeight) 
            {
                Position = new Vector3(Position.X, _currentEyeHeight, Position.Z);
                _verticalVelocity = 0;
                _isGrounded = true;
            }

            Target = new Vector3(
                Position.X + MathF.Sin(_yaw * Deg2Rad) * MathF.Cos(_pitch * Deg2Rad),
                Position.Y + MathF.Sin(_pitch * Deg2Rad),
                Position.Z + MathF.Cos(_yaw * Deg2Rad) * MathF.Cos(_pitch * Deg2Rad)
            );

            Data = new Raylib_cs.Camera3D {
                position = Position,
                target = Target,
                up = Up,
                fovy = Fovy,
                projection = Projection
            };
        }
        
        private static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
    }
}
