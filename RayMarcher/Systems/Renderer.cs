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

    private readonly Color[] _textures = [Color.Blank, Color.Brown, Color.Green, Color.SkyBlue];

    [SetupMethod(Phase = SetupPhase.Late)]
    public void Setup()
    {
        _pixelBuffer = new Color[state.RenderWidth * state.RenderHeight];
        var blankImage = Raylib.GenImageColor(state.RenderWidth, state.RenderHeight, Color.Black);
        _frameTexture = Raylib.LoadTextureFromImage(blankImage);
        Raylib.SetTextureFilter(_frameTexture, TextureFilter.Bilinear);
        Raylib.UnloadImage(blankImage);
    }

    [TickMethod]
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