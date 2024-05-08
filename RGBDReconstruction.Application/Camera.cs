using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace RGBDReconstruction.Application;

public class Camera
{
    public Vector3 _position;
    private Vector3 _target;
    
    private Vector3 _front;
    private Vector3 _up;
    private Vector3 _right;

    private Matrix4 _lookAt;

    private float _speed;

    // Y
    public float _yaw = -90f;
    
    // X
    public float _pitch = 0f;
    
    // Z
    public float _roll = 0f;

    private float _fov = MathHelper.DegreesToRadians(39.6f*9/16f);
    private float _aspectRatio;

    public Camera()
    {
        _position = new Vector3(0.0f, 0.0f, 0.0f);
        _target = new Vector3();
        _front = new Vector3(0.0f, 0.0f, -1.0f);
        _up = new Vector3(0.0f, 1.0f, 0.0f);
        _right = new Vector3(1.0f, 0.0f, 0.0f);
        _speed = -1.5f;
    }

    public Matrix4 LookAt => Matrix4.LookAt(_position, _position + _front, _up);

    public Matrix4 CameraViewMatrix => Matrix4.Mult(LookAt, Matrix4.CreateTranslation(_position));

    public Vector3 Position
    {
        get => _position;
        set => _position = value;
    }

    public float Fov
    {
        get => _fov;
        set => _fov = float.Clamp(value, (1.0f/180f)*Single.Pi, (45.0f/180f)*Single.Pi);
    }

    public Matrix4 CameraProjectionMatrix => Matrix4.CreatePerspectiveFieldOfView(_fov, _aspectRatio, 0.1f, 100f);

    public float AspectRatio
    {
        get => _aspectRatio;
        set => _aspectRatio = value;
    }
    
    public void HandleInput(KeyboardState input, Vector2 deltaMousePos, float sensitivity, double deltaTime)
    {
        // Mouse input for looking around
        _yaw += deltaMousePos.X * sensitivity;
        _pitch -= deltaMousePos.Y * sensitivity;
        if (Single.Abs(_pitch) > 89.0f)
        {
            _pitch = 89.0f * Single.Abs(_pitch) / _pitch;
        }
        
        _front.X = (float)Math.Cos(MathHelper.DegreesToRadians(_pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(_yaw));
        _front.Y = (float)Math.Sin(MathHelper.DegreesToRadians(_pitch));
        _front.Z = (float)Math.Cos(MathHelper.DegreesToRadians(_pitch)) * (float)Math.Sin(MathHelper.DegreesToRadians(_yaw));
        _front = Vector3.Normalize(_front);
        
        // Keyboard input for moving around
        if (input.IsKeyDown(Keys.W))
        {
            _position += _front * _speed * (float)deltaTime; //Forward 
        }

        if (input.IsKeyDown(Keys.S))
        {
            _position -= _front * _speed * (float)deltaTime; //Backwards
        }

        if (input.IsKeyDown(Keys.A))
        {
            _position -= Vector3.Normalize(Vector3.Cross(_front, _up)) * _speed * (float)deltaTime; //Left
        }

        if (input.IsKeyDown(Keys.D))
        {
            _position += Vector3.Normalize(Vector3.Cross(_front, _up)) * _speed * (float)deltaTime; //Right
        }

        if (input.IsKeyDown(Keys.Space))
        {
            _position += _up * _speed * (float)deltaTime; //Up 
        }

        if (input.IsKeyDown(Keys.LeftShift))
        {
            _position -= _up * _speed * (float)deltaTime; //Down
        }
    }
}