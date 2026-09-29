using System.Numerics;
using NoiseDotNet;
using Raylib_cs;
using RayMarcher;

const int mapWidth = 32 * 2;
const int mapHeight = 16 * 2;
const int mapDepth = 32 * 2;

const int chunkSize = 4;

const int chunkMapWidth = mapWidth / chunkSize;
const int chunkMapHeight = mapHeight / chunkSize;
const int chunkMapDepth = mapDepth / chunkSize;

var voxels = new byte[mapWidth * mapHeight * mapDepth];
var chunks = new bool[chunkMapWidth * chunkMapHeight * chunkMapDepth];


int Index(int x, int y, int z)
{
    return x + y * mapWidth + z * mapWidth * mapHeight;
}

int ChunkIndex(int x, int y, int z)
{
    return x + y * chunkMapWidth + z * chunkMapWidth * chunkMapHeight;
}

bool InBounds(int x, int y, int z)
{
    return x >= 0 && x < mapWidth &&
           y >= 0 && y < mapHeight &&
           z >= 0 && z < mapDepth;
}

bool IsSolid(int x, int y, int z)
{
    if (!InBounds(x, y, z)) return false;
    return voxels[Index(x, y, z)] != 0;
}

void GenerateTestMap()
{
    // One noise sample per (x, z) column, batched the way NoiseDotNet wants: flat coordinate
    // arrays in, flat output array out, rather than calling it per-voxel.
    var columnCount = mapWidth * mapDepth;
    var xCoords = new float[columnCount];
    var zCoords = new float[columnCount];
    var noise = new float[columnCount];

    var idx = 0;
    for (var z = 0; z < mapDepth; z++)
    for (var x = 0; x < mapWidth; x++)
    {
        xCoords[idx] = x;
        zCoords[idx] = z;
        idx++;
    }

    // Lower frequency = broader, smoother hills; higher = choppier terrain.
    const float noiseFrequency = 0.05f;
    const int noiseSeed = 1337;
    var settings = new NoiseSettings(xFreq: noiseFrequency, yFreq: noiseFrequency, seed: noiseSeed);
    Noise.GradientNoise2D(xCoords, zCoords, noise, settings);

    // Ground sits around baseHeight, swinging +/- heightVariance with the noise.
    const int baseHeight = mapHeight / 4;
    const int heightVariance = mapHeight / 4;

    idx = 0;
    for (var z = 0; z < mapDepth; z++)
    for (var x = 0; x < mapWidth; x++)
    {
        var groundHeight = baseHeight + (int)MathF.Round(noise[idx] * heightVariance);
        groundHeight = Math.Clamp(groundHeight, 1, mapHeight - 2);
        idx++;

        for (var y = 0; y < mapHeight; y++)
        {
            var solid = y <= groundHeight;

            if (x == 0 || x == mapWidth - 1 || z == 0 || z == mapDepth - 1) solid = true;

            if (solid)
            {
                voxels[Index(x, y, z)] = 1;
                chunks[ChunkIndex(x / chunkSize, y / chunkSize, z / chunkSize)] = true;
            }
        }
    }
}


GenerateTestMap();

var spawnX = mapWidth / 2;
var spawnZ = mapDepth / 2;
var spawnY = mapHeight - 1;
while (spawnY > 0 && !IsSolid(spawnX, spawnY, spawnZ)) spawnY--;
var playerPos = new Vector3(spawnX, spawnY + 3f, spawnZ);
var yaw = 0f;
var pitch = 0f;
var maxPitch = MathF.PI / 2f - 0.01f;


const float moveSpeed = 4f;
const float lookSpeed = 2f;


const int screenWidth = 640 * 2;
const int screenHeight = 360 * 2;
const int renderWidth = 640 / 2;
const int renderHeight = 360 / 2;


var pixelBuffer = new Color[renderWidth * renderHeight];

var test = voxels.Count(v => v != 0);
var octree = Octree.Build(new VoxelMap(voxels, mapWidth, mapHeight, mapDepth));

Raylib.InitWindow(screenWidth, screenHeight, "Raymarcher practice");
Raylib.DisableCursor();
Raylib.SetTargetFPS(60);


var blankImage = Raylib.GenImageColor(renderWidth, renderHeight, Color.Black);
var frameTexture = Raylib.LoadTextureFromImage(blankImage);
Raylib.SetTextureFilter(frameTexture, TextureFilter.Point);
Raylib.UnloadImage(blankImage);


while (!Raylib.WindowShouldClose())
{
    var dt = Raylib.GetFrameTime();

    var fX = (float)(Math.Cos(pitch) * Math.Sin(yaw));
    var fY = (float)Math.Sin(pitch);
    var fZ = (float)(Math.Cos(pitch) * Math.Cos(yaw));
    var forward = new Vector3(fX, fY, fZ);
    var right = Vector3.Cross(forward, Vector3.UnitY);
    var up = Vector3.Cross(right, forward);

    var mouseDelta = Raylib.GetMouseDelta();
    yaw -= mouseDelta.X * 0.01f;
    pitch -= mouseDelta.Y * 0.01f;
    pitch = Math.Clamp(pitch, -maxPitch, maxPitch);

    if (Raylib.IsKeyDown(KeyboardKey.W))
    {
        var next = playerPos + forward * (moveSpeed * dt);
        if (!IsSolid((int)next.X, (int)playerPos.Y, (int)playerPos.Z)) playerPos.X = next.X;
        if (!IsSolid((int)playerPos.X, (int)next.Y, (int)playerPos.Z)) playerPos.Y = next.Y;
        if (!IsSolid((int)playerPos.X, (int)playerPos.Y, (int)next.Z)) playerPos.Z = next.Z;
    }

    if (Raylib.IsKeyDown(KeyboardKey.S))
    {
        var next = playerPos - forward * (moveSpeed * dt);
        if (!IsSolid((int)next.X, (int)playerPos.Y, (int)playerPos.Z)) playerPos.X = next.X;
        if (!IsSolid((int)playerPos.X, (int)next.Y, (int)playerPos.Z)) playerPos.Y = next.Y;
        if (!IsSolid((int)playerPos.X, (int)playerPos.Y, (int)next.Z)) playerPos.Z = next.Z;
    }

    if (Raylib.IsKeyDown(KeyboardKey.A))
    {
        // TODO: strafe left
    }

    if (Raylib.IsKeyDown(KeyboardKey.D))
    {
        // TODO: strafe right
    }

    if (Raylib.IsKeyDown(KeyboardKey.Right)) yaw -= lookSpeed * dt;

    if (Raylib.IsKeyDown(KeyboardKey.Left)) yaw += lookSpeed * dt;

    if (Raylib.IsKeyDown(KeyboardKey.Up))
    {
        pitch += lookSpeed * dt;
        pitch = Math.Clamp(pitch, -maxPitch, maxPitch);
    }

    if (Raylib.IsKeyDown(KeyboardKey.Down))
    {
        pitch -= lookSpeed * dt;
        pitch = Math.Clamp(pitch, -maxPitch, maxPitch);
    }

    var playerX = playerPos.X;
    var playerY = playerPos.Y;
    var playerZ = playerPos.Z;

    Parallel.For((long)0, renderHeight, py =>
    {
        var cameraY = 2.0f * py / renderHeight - 1f;
        var nodeBuffer = new NodeBuffer(octree.MaxDepth);
        nodeBuffer.AppendNode(octree.Root);
        for (var px = 0; px < renderWidth; px++)
        {
            var cameraX = 2.0f * px / renderWidth - 1f;

            var rayDir = forward + right * cameraX + up * -cameraY;

            var currentRayPos = playerPos;
            var rayOrigin = playerPos;

            int rayDirSignX = rayDir.X < 0 ? -1 : 1;
            int rayDirSignY = rayDir.Y < 0 ? -1 : 1;
            int rayDirSignZ = rayDir.Z < 0 ? -1 : 1;

            var hit = false;
            var side = 0;
            var safety = 0;
            var epsilon = 0.0001f;

            Vector3 trueRayPos = currentRayPos;

            while (!hit && safety++ < 100)
            {
                if (octree.IsOutOfBounds(currentRayPos))
                {
                    break;
                }

                Node.Climb(ref nodeBuffer, currentRayPos);
                var currentNode = Node.Descend(ref nodeBuffer, currentRayPos);

                if (currentNode.State == NodeState.OutOfBounds) break;

                if (currentNode.State == NodeState.Solid)
                {
                    hit = true;
                    continue;
                }

                if (currentNode.State == NodeState.Empty)
                {
                    var boundaryX = (rayDirSignX == 1 ? currentNode.Max : currentNode.Min).X;
                    var boundaryY = (rayDirSignY == 1 ? currentNode.Max : currentNode.Min).Y;
                    var boundaryZ = (rayDirSignZ == 1 ? currentNode.Max : currentNode.Min).Z;

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

            nodeBuffer.Reset();

            if (!hit)
            {
                pixelBuffer[px + py * renderWidth] = Color.Black;
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

            var color = side switch
            {
                0 => new Color(shade, shade, shade, (byte)255),
                1 => new Color((byte)(shade * 0.7f), (byte)(shade * 0.7f), (byte)(shade * 0.7f), (byte)255),
                2 => new Color((byte)(shade * 0.3f), (byte)(shade * 0.3f), (byte)(shade * 0.3f), (byte)255),
                _ => throw new InvalidOperationException("Invalid side")
            };


            if (py == 200 && px == 300) color = new Color(0f, shade, 0f, 255);

            pixelBuffer[px + py * renderWidth] = color;
        }
    });

    unsafe
    {
        fixed (Color* bufferPtr = pixelBuffer)
        {
            Raylib.UpdateTexture(frameTexture, bufferPtr);
        }
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);
    Raylib.DrawTexturePro(frameTexture, new Rectangle(0, 0, renderWidth, renderHeight),
        new Rectangle(0, 0, screenWidth, screenHeight), new Vector2(0, 0), 0.0f, Color.White);
    Raylib.DrawFPS(10, 10);
    Raylib.EndDrawing();
}

Raylib.UnloadTexture(frameTexture);
Raylib.CloseWindow();