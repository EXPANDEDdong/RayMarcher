using System.Numerics;
using Raylib_cs;
using RayMarcher.Framework;
using RayMarcher.Systems.States;

namespace RayMarcher.Systems;

[FrameSystem(Phase.Input, -10)]
public sealed class PlayerController(
    SharedApplicationState state,
    WorldGrid world,
    ICameraStateReader cameraStateReader,
    ICameraStateWriter cameraStateWriter)
{
    private const float MoveSpeed = 12f;
    private const float LookSpeed = 6f;
    private readonly float _maxPitch = MathF.PI / 2f - 0.01f;
    private float _pitch, _yaw;
    private Vector3 _position;

    [SetupMethod(Phase = SetupPhase.Late)]
    public void Setup()
    {
        var spawnX = world.MapWidth / 2;
        var spawnZ = world.MapDepth / 2;
        var spawnY = world.MapHeight - 10;

        _position = new Vector3(spawnX, spawnY, spawnZ);

        var fX = (float)(Math.Cos(_pitch) * Math.Sin(_yaw));
        var fY = (float)Math.Sin(_pitch);
        var fZ = (float)(Math.Cos(_pitch) * Math.Cos(_yaw));
        var forward = new Vector3(fX, fY, fZ);
        var right = Vector3.Cross(forward, Vector3.UnitY);
        var up = Vector3.Cross(right, forward);

        cameraStateWriter.PushOrthonormal(new OrthonormalBasis(up, right, forward));
        cameraStateWriter.PushCameraPosition(new CameraPosition(_position, _pitch, _yaw));
    }

    [TickMethod(Phase = Phase.Input, Order = 0)]
    public void CalculateOrthonormal()
    {
        var fX = (float)(Math.Cos(_pitch) * Math.Sin(_yaw));
        var fY = (float)Math.Sin(_pitch);
        var fZ = (float)(Math.Cos(_pitch) * Math.Cos(_yaw));
        var forward = new Vector3(fX, fY, fZ);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));

        cameraStateWriter.PushOrthonormal(new OrthonormalBasis(up, right, forward));
    }

    [TickMethod(Phase = Phase.Input, Order = 10)]
    public void HandleInput(float dt)
    {
        var (orthonormal, cameraPosition) = cameraStateReader.GetCameraState();

        var forward = orthonormal.Forward;
        var right = orthonormal.Right;
        var mouseDelta = Raylib.GetMouseDelta();
        _yaw -= mouseDelta.X * 0.01f;
        _pitch -= mouseDelta.Y * 0.01f;
        _pitch = Math.Clamp(_pitch, -_maxPitch, _maxPitch);

        if (Raylib.IsKeyDown(KeyboardKey.W))
        {
            var next = _position + forward * (MoveSpeed * dt);
            if (!world.IsSolid((int)next.X, (int)_position.Y, (int)_position.Z)) _position.X = next.X;
            if (!world.IsSolid((int)_position.X, (int)next.Y, (int)_position.Z)) _position.Y = next.Y;
            if (!world.IsSolid((int)_position.X, (int)_position.Y, (int)next.Z)) _position.Z = next.Z;
        }

        if (Raylib.IsKeyDown(KeyboardKey.S))
        {
            var next = _position - forward * (MoveSpeed * dt);
            if (!world.IsSolid((int)next.X, (int)_position.Y, (int)_position.Z)) _position.X = next.X;
            if (!world.IsSolid((int)_position.X, (int)next.Y, (int)_position.Z)) _position.Y = next.Y;
            if (!world.IsSolid((int)_position.X, (int)_position.Y, (int)next.Z)) _position.Z = next.Z;
        }

        if (Raylib.IsKeyDown(KeyboardKey.A))
        {
            var next = _position - right * (MoveSpeed * dt);
            if (!world.IsSolid((int)next.X, (int)_position.Y, (int)_position.Z)) _position.X = next.X;
            if (!world.IsSolid((int)_position.X, (int)next.Y, (int)_position.Z)) _position.Y = next.Y;
            if (!world.IsSolid((int)_position.X, (int)_position.Y, (int)next.Z)) _position.Z = next.Z;
        }

        if (Raylib.IsKeyDown(KeyboardKey.D))
        {
            var next = _position + right * (MoveSpeed * dt);
            if (!world.IsSolid((int)next.X, (int)_position.Y, (int)_position.Z)) _position.X = next.X;
            if (!world.IsSolid((int)_position.X, (int)next.Y, (int)_position.Z)) _position.Y = next.Y;
            if (!world.IsSolid((int)_position.X, (int)_position.Y, (int)next.Z)) _position.Z = next.Z;
        }

        if (Raylib.IsKeyDown(KeyboardKey.Right)) _yaw -= LookSpeed * dt;

        if (Raylib.IsKeyDown(KeyboardKey.Left)) _yaw += LookSpeed * dt;

        if (Raylib.IsKeyDown(KeyboardKey.Up))
        {
            _pitch += LookSpeed * dt;
            _pitch = Math.Clamp(_pitch, -_maxPitch, _maxPitch);
        }

        if (Raylib.IsKeyDown(KeyboardKey.Down))
        {
            _pitch -= LookSpeed * dt;
            _pitch = Math.Clamp(_pitch, -_maxPitch, _maxPitch);
        }

        cameraStateWriter.PushCameraPosition(new CameraPosition(_position, _pitch, _yaw));
    }
}