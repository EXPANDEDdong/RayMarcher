using System.Numerics;

namespace RayMarcher.Systems.States;

public class CameraState : ICameraStateWriter, ICameraStateReader
{
    private OrthonormalBasis orthonormal;
    private CameraPosition cameraPosition;

    public void PushOrthonormal(OrthonormalBasis orthonormal)
    {
        this.orthonormal = orthonormal;
    }

    public void PushCameraPosition(CameraPosition cameraPosition)
    {
        this.cameraPosition = cameraPosition;
    }
    
    public CameraStateCopy GetCameraState() => new(orthonormal, cameraPosition);
}

public interface ICameraStateWriter
{
    void PushOrthonormal(OrthonormalBasis orthonormal);
    void PushCameraPosition(CameraPosition cameraPosition);
}

public interface ICameraStateReader
{
    CameraStateCopy GetCameraState();
}

public record struct CameraStateCopy(OrthonormalBasis Orthonormal, CameraPosition CameraPosition);

public readonly record struct OrthonormalBasis(Vector3 Up, Vector3 Right, Vector3 Forward);
public readonly record struct CameraPosition(Vector3 Position, float Pitch, float Yaw);