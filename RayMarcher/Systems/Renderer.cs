using System.Numerics;
using Raylib_cs;
using RayMarcher.Framework;
using RayMarcher.Systems.States;
using RayMarcher.Utils;

namespace RayMarcher.Systems;

[FrameSystem(Phase.Render, 0)]
public class Renderer(SharedApplicationState state, WorldGrid world, ICameraStateReader stateReader)
{
    private Texture2D _frameTexture;
    private Color[] _pixelBuffer = null!;
    private Shader _shader;
    private Texture2D _voxelsTexture;
    private Texture2D _textureAtlas;
    private int textureAtlasLoc;
    private int voxelsLoc;
    private int camPosLoc;
    private int camForwardLoc;
    private int camUpLoc;
    private int camRightLoc;
    private int resolutionLoc;
    private int dimensionsLoc;
    private byte[] paddedVoxels;

    private readonly Color[] _textures = [Color.Blank, Color.Brown, Color.Green, Color.SkyBlue];

    [SetupMethod(Phase = SetupPhase.Late)]
    public void Setup()
    {
        var totalVoxels = state.MapWidth * state.MapHeight * state.MapDepth;
        var rows = (int)Math.Ceiling((double)totalVoxels / 4096);
        paddedVoxels = new byte[rows * 4096];
        world.Voxels.CopyTo(paddedVoxels);
        
        unsafe
        {
            fixed (byte* ptr = paddedVoxels)
            {
                var img = new Image
                {
                    Format = PixelFormat.UncompressedGrayscale,
                    Width = 4096,
                    Height = rows,
                    Mipmaps = 1,
                    Data = ptr
                };
                _voxelsTexture = Raylib.LoadTextureFromImage(img);
                Raylib.SetTextureFilter(_voxelsTexture, TextureFilter.Point);
            }
        }

        var atlas = Raylib.LoadImage("Shaders/atlas16x16.png");
        _textureAtlas = Raylib.LoadTextureFromImage(atlas);
        Raylib.SetTextureFilter(_textureAtlas, TextureFilter.Point);
        Raylib.UnloadImage(atlas);
        
        
        _shader = Raylib.LoadShader(null, "Shaders/raymarch.fs");
        voxelsLoc = Raylib.GetShaderLocation(_shader, "voxels");
        camPosLoc = Raylib.GetShaderLocation(_shader, "camPos");
        camForwardLoc = Raylib.GetShaderLocation(_shader, "camForward");
        camUpLoc = Raylib.GetShaderLocation(_shader, "camUp");
        camRightLoc = Raylib.GetShaderLocation(_shader, "camRight");
        resolutionLoc = Raylib.GetShaderLocation(_shader, "resolution");
        dimensionsLoc = Raylib.GetShaderLocation(_shader, "dimensions");
        textureAtlasLoc = Raylib.GetShaderLocation(_shader, "textureAtlas");
    }

    public void Destroy()
    {
        Raylib.UnloadTexture(_voxelsTexture);
        Raylib.UnloadTexture(_textureAtlas);
        Raylib.UnloadShader(_shader);
    }

    [TickMethod]
    public void RenderShader()
    {
        var (orthonormal, cameraState) = stateReader.GetCameraState();
        Raylib.SetShaderValue(_shader, camPosLoc, cameraState.Position, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(_shader, camForwardLoc, orthonormal.Forward, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(_shader, camUpLoc, orthonormal.Up, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(_shader, camRightLoc, orthonormal.Right, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(_shader, resolutionLoc, new Vector2(state.WindowWidth, state.WindowHeight), ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(_shader, dimensionsLoc, new int[3]{ state.MapWidth, state.MapHeight, state.MapDepth }, ShaderUniformDataType.IVec3);
        Raylib.SetShaderValueTexture(_shader, textureAtlasLoc, _textureAtlas);
        
        Raylib.BeginDrawing();
            Raylib.BeginShaderMode(_shader);
            Raylib.SetShaderValueTexture(_shader, textureAtlasLoc, _textureAtlas);
            Raylib.SetShaderValueTexture(_shader, voxelsLoc, _voxelsTexture);
                Raylib.DrawRectangle(0, 0, state.WindowWidth, state.WindowHeight, Color.White);
            Raylib.EndShaderMode();
            Raylib.DrawFPS(10, 10);
        Raylib.EndDrawing();
    }

    /*[TickMethod] Not in use, remains for reference. Will likely be removed in the future. */
    public void Render()
    {
        var (orthonormal, cameraState) = stateReader.GetCameraState();
        Parallel.For((long)0, state.RenderHeight, py =>
        {
            var cameraY = 2.0f * py / state.RenderHeight - 1f;
            Span<TreeBranch> branchBuffer = stackalloc TreeBranch[world.Octree.MaxDepth + 1];
            branchBuffer[0] = TreeBranch.Create(0, 0, 0, 0, 0);
            var climber = new TreeClimber(branchBuffer, world.Octree.OneSideLength);
            var currentRayPos = cameraState.Position;
            var rayOrigin = cameraState.Position;
            for (var px = 0; px < state.RenderWidth; px++)
            {
                var cameraX = 2.0f * px / state.RenderWidth - 1f;

                var rayDir = orthonormal.Forward + orthonormal.Right * cameraX + orthonormal.Up * -cameraY;

                currentRayPos = cameraState.Position;
                rayOrigin = cameraState.Position;

                var rayDirSignX = rayDir.X < 0 ? -1 : 1;
                var rayDirSignY = rayDir.Y < 0 ? -1 : 1;
                var rayDirSignZ = rayDir.Z < 0 ? -1 : 1;

                var hit = false;
                var side = 0;
                var safety = 0;
                const float epsilon = 0.0001f;

                var trueRayPos = currentRayPos;
                Node? hitNode = null;

                while (!hit && safety++ < 100)
                {
                    if (Vector3.Distance(new Vector3(currentRayPos.X, 0f, currentRayPos.Z),
                            new Vector3(rayOrigin.X, 0f, rayOrigin.Z)) > 50f) break;
                    if (!world.InBounds((int)currentRayPos.X, (int)currentRayPos.Y, (int)currentRayPos.Z)) break;
                    //climber.Climb(currentRayPos);
                    var (currentNode, nodeBranch) = climber.Descend(world.Octree, currentRayPos);
                    var sideLength = (int)world.Octree.OneSideLength >> nodeBranch.Depth;

                    if (currentNode.State == NodeState.OutOfBounds) break;

                    if (currentNode.State == NodeState.Solid)
                    {
                        hit = true;
                        hitNode = currentNode;
                        continue;
                    }

                    if (currentNode.State == NodeState.Empty)
                    {
                        (int X, int Y, int Z) max = (nodeBranch.X + sideLength, nodeBranch.Y + sideLength,
                            nodeBranch.Z + sideLength);
                        var boundaryX = (rayDirSignX == 1 ? max : (nodeBranch.X, nodeBranch.Y, nodeBranch.Z)).X;
                        var boundaryY = (rayDirSignY == 1 ? max : (nodeBranch.X, nodeBranch.Y, nodeBranch.Z)).Y;
                        var boundaryZ = (rayDirSignZ == 1 ? max : (nodeBranch.X, nodeBranch.Y, nodeBranch.Z)).Z;

                        var sideDistX = (boundaryX - currentRayPos.X) / rayDir.X;
                        var sideDistY = (boundaryY - currentRayPos.Y) / rayDir.Y;
                        var sideDistZ = (boundaryZ - currentRayPos.Z) / rayDir.Z;

                        if (sideDistX < sideDistY)
                        {
                            if (sideDistX < sideDistZ)
                            {
                                currentRayPos += rayDir * sideDistX;
                                side = 0;
                            }
                            else
                            {
                                currentRayPos += rayDir * sideDistZ;
                                side = 2;
                            }
                        }
                        else
                        {
                            if (sideDistY < sideDistZ)
                            {
                                currentRayPos += rayDir * sideDistY;
                                side = 1;
                            }
                            else
                            {
                                currentRayPos += rayDir * sideDistZ;
                                side = 2;
                            }
                        }

                        trueRayPos = currentRayPos;

                        switch (side)
                        {
                            case 0: currentRayPos.X += rayDirSignX * epsilon; break;
                            case 1: currentRayPos.Y += rayDirSignY * epsilon; break;
                            case 2: currentRayPos.Z += rayDirSignZ * epsilon; break;
                        }
                    }
                }

                if (!hit || !hitNode.HasValue || hitNode == null)
                {
                    _pixelBuffer[px + py * state.RenderWidth] = Color.SkyBlue;
                    continue;
                }

                var perpWallDist = side switch
                {
                    0 => (trueRayPos.X - rayOrigin.X) / rayDir.X,
                    1 => (trueRayPos.Y - rayOrigin.Y) / rayDir.Y,
                    2 => (trueRayPos.Z - rayOrigin.Z) / rayDir.Z,
                    _ => throw new InvalidOperationException("Invalid side")
                };

                var shade = (byte)Math.Clamp(255 - perpWallDist * 25, 40, 255);

                var hitColor = _textures[hitNode.Value.MaterialId];

                if (hitColor != Color.SkyBlue)
                    switch (side)
                    {
                        case 0:
                            hitColor.A = 255;
                            break;
                        case 1:
                            hitColor.A = 200;
                            break;
                        case 2:
                            hitColor.A = 155;
                            break;
                    }

                _pixelBuffer[px + py * state.RenderWidth] = hitColor;
            }
        });

        unsafe
        {
            fixed (Color* bufferPtr = _pixelBuffer)
            {
                Raylib.UpdateTexture(_frameTexture, bufferPtr);
            }
        }

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        Raylib.DrawTexturePro(_frameTexture, new Rectangle(0, 0, state.RenderWidth, state.RenderHeight),
            new Rectangle(0, 0, state.WindowWidth, state.WindowHeight), new Vector2(0, 0), 0.0f, Color.White);
        Raylib.DrawFPS(10, 10);
        Raylib.EndDrawing();
    }
}