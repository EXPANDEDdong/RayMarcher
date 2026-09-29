using System.Numerics;
using Raylib_cs;

const int mapWidth = 32 * 2;
const int mapHeight = 16 * 2;
const int mapDepth = 32 * 2;

const int chunkSize = 4;

const int chunkMapWidth = mapWidth / chunkSize;
const int chunkMapHeight = mapHeight / chunkSize;
const int chunkMapDepth = mapDepth / chunkSize;


byte[] voxels = new byte[mapWidth * mapHeight * mapDepth];
bool[] chunks = new bool[chunkMapWidth * chunkMapHeight * chunkMapDepth];

int Index(int x, int y, int z) => x + y * mapWidth + z * mapWidth * mapHeight;
int ChunkIndex(int x, int y, int z) => x + y * chunkMapWidth + z * chunkMapWidth * chunkMapHeight;

bool InBounds(int x, int y, int z) =>
    x >= 0 && x < mapWidth &&
    y >= 0 && y < mapHeight &&
    z >= 0 && z < mapDepth;

bool IsSolid(int x, int y, int z)
{
    if (!InBounds(x, y, z)) return false;
    return voxels[Index(x, y, z)] != 0;
}

void GenerateTestMap()
{
    for (int z = 0; z < mapDepth; z++)
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                bool solid = false;

                if (y == 0) solid = true;

                // if (y == mapHeight - 1) solid = true;

                if (x == 0 || x == mapWidth - 1 || z == 0 || z == mapDepth - 1) solid = true;

                if (x == 10 && z == 10 && y < 5) solid = true;

                if (solid)
                {
                    voxels[Index(x, y, z)] = 1;
                    chunks[ChunkIndex(x / chunkSize, y / chunkSize, z / chunkSize)] = true;
                }
            }
        }
    }
}


Vector3 playerPos = new Vector3(mapWidth / 2f, 12f, mapDepth / 2f);
float yaw = 0f;
float pitch = 0f;
var maxPitch = MathF.PI / 2f - 0.01f;


const float moveSpeed = 4f;
const float lookSpeed = 2f;


const int screenWidth = 640 * 2;
const int screenHeight = 360 * 2;
const int renderWidth = 640;
const int renderHeight = 360;


Color[] pixelBuffer = new Color[renderWidth * renderHeight];

GenerateTestMap();

Raylib.InitWindow(screenWidth, screenHeight, "RayMarcher");
Raylib.DisableCursor();
Raylib.SetTargetFPS(60);


Image blankImage = Raylib.GenImageColor(renderWidth, renderHeight, Color.Black);
Texture2D frameTexture = Raylib.LoadTextureFromImage(blankImage);
Raylib.SetTextureFilter(frameTexture, TextureFilter.Point);
Raylib.UnloadImage(blankImage);


while (!Raylib.WindowShouldClose())
{
    float dt = Raylib.GetFrameTime();

    float fX = (float)(Math.Cos(pitch) * Math.Sin(yaw));
    float fY = (float)(Math.Sin(pitch));
    float fZ = (float)(Math.Cos(pitch) * Math.Cos(yaw));
    Vector3 forward = new Vector3(fX, fY, fZ);
    var right = Vector3.Cross(forward, Vector3.UnitY);
    var up = Vector3.Cross(right, forward);

    Vector2 mouseDelta = Raylib.GetMouseDelta();
    yaw -= mouseDelta.X * 0.01f;
    pitch -= mouseDelta.Y * 0.01f;
    pitch = Math.Clamp(pitch, -maxPitch, maxPitch);

    if (Raylib.IsKeyDown(KeyboardKey.W))
    {
        Vector3 next = playerPos + forward * (moveSpeed * dt);
        if (!IsSolid((int)next.X, (int)playerPos.Y, (int)playerPos.Z)) playerPos.X = next.X;
        if (!IsSolid((int)playerPos.X, (int)next.Y, (int)playerPos.Z)) playerPos.Y = next.Y;
        if (!IsSolid((int)playerPos.X, (int)playerPos.Y, (int)next.Z)) playerPos.Z = next.Z;
    }

    if (Raylib.IsKeyDown(KeyboardKey.S))
    {
        Vector3 next = playerPos - forward * (moveSpeed * dt);
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

    if (Raylib.IsKeyDown(KeyboardKey.Right))
    {
        yaw -= lookSpeed * dt;
    }

    if (Raylib.IsKeyDown(KeyboardKey.Left))
    {
        yaw += lookSpeed * dt;
    }

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

    float playerX = playerPos.X;
    float playerY = playerPos.Y;
    float playerZ = playerPos.Z;

    Parallel.For(0, renderHeight, py =>
    {
        var cameraY = 2.0f * py / renderHeight - 1f;
        for (int px = 0; px < renderWidth; px++)
        {
            var cameraX = 2.0f * px / renderWidth - 1f;

            var rayDir = forward + right * cameraX + up * -cameraY;

            int mapX = (int)playerX;
            int mapY = (int)playerY;
            int mapZ = (int)playerZ;

            int chunkMapX = mapX / chunkSize;
            int chunkMapY = mapY / chunkSize;
            int chunkMapZ = mapZ / chunkSize;

            int chunkMinX = chunkMapX * chunkSize;
            int chunkMaxX = chunkMinX + chunkSize;
            int chunkMinY = chunkMapY * chunkSize;
            int chunkMaxY = chunkMinY + chunkSize;
            int chunkMinZ = chunkMapZ * chunkSize;
            int chunkMaxZ = chunkMinZ + chunkSize;

            float deltaDistX = MathF.Abs(1f / rayDir.X);
            float deltaDistY = MathF.Abs(1f / rayDir.Y);
            float deltaDistZ = MathF.Abs(1f / rayDir.Z);

            float deltaChunkDistX = MathF.Abs((float)chunkSize / rayDir.X);
            float deltaChunkDistY = MathF.Abs((float)chunkSize / rayDir.Y);
            float deltaChunkDistZ = MathF.Abs((float)chunkSize / rayDir.Z);

            int stepX, stepY, stepZ;
            float sideDistX, sideDistY, sideDistZ;

            float sideChunkDistX, sideChunkDistY, sideChunkDistZ;

            if (rayDir.X < 0)
            {
                stepX = -1;
                sideDistX = (playerX - mapX) * deltaDistX;
                sideChunkDistX = (playerX - chunkMinX) * deltaDistX;
            }
            else
            {
                stepX = 1;
                sideDistX = (mapX + 1f - playerX) * deltaDistX;
                sideChunkDistX = (chunkMaxX - playerX) * deltaDistX;
            }

            if (rayDir.Y < 0)
            {
                stepY = -1;
                sideDistY = (playerY - mapY) * deltaDistY;
                sideChunkDistY = (playerY - chunkMinY) * deltaDistY;
            }
            else
            {
                stepY = 1;
                sideDistY = (mapY + 1f - playerY) * deltaDistY;
                sideChunkDistY = (chunkMaxY - playerY) * deltaDistY;
            }

            if (rayDir.Z < 0)
            {
                stepZ = -1;
                sideDistZ = (playerZ - mapZ) * deltaDistZ;
                sideChunkDistZ = (playerZ - chunkMinZ) * deltaDistZ;
            }
            else
            {
                stepZ = 1;
                sideDistZ = (mapZ + 1f - playerZ) * deltaDistZ;
                sideChunkDistZ = (chunkMaxZ - playerZ) * deltaDistZ;
            }

            bool chunkIsOccupied = chunks[ChunkIndex(chunkMapX, chunkMapY, chunkMapZ)];
            bool hit = false;
            int side = 0;
            int safety = 0;

            while (!hit && safety < 40)
            {
                if (chunkIsOccupied)
                {
                    safety++;
                    if (sideDistX < sideDistY)
                    {
                        if (sideDistX < sideDistZ)
                        {
                            sideDistX += deltaDistX;
                            mapX += stepX;
                            side = 0;
                        }
                        else
                        {
                            sideDistZ += deltaDistZ;
                            mapZ += stepZ;
                            side = 2;
                        }
                    }
                    else
                    {
                        if (sideDistY < sideDistZ)
                        {
                            sideDistY += deltaDistY;
                            mapY += stepY;
                            side = 1;
                        }
                        else
                        {
                            sideDistZ += deltaDistZ;
                            mapZ += stepZ;
                            side = 2;
                        }
                    }

                    if (mapX >= chunkMaxX || mapY >= chunkMaxY || mapZ >= chunkMaxZ || mapX < chunkMinX ||
                        mapY < chunkMinY || mapZ < chunkMinZ)
                    {
                        if (mapX < 0 || mapX >= mapWidth || mapY < 0 || mapY >= mapHeight || mapZ < 0 ||
                            mapZ >= mapDepth) break;
                        chunkMapX = mapX / chunkSize;
                        chunkMapY = mapY / chunkSize;
                        chunkMapZ = mapZ / chunkSize;

                        chunkMinX = chunkMapX * chunkSize;
                        chunkMaxX = chunkMinX + chunkSize;
                        chunkMinY = chunkMapY * chunkSize;
                        chunkMaxY = chunkMinY + chunkSize;
                        chunkMinZ = chunkMapZ * chunkSize;
                        chunkMaxZ = chunkMinZ + chunkSize;

                        if (stepX == -1)
                        {
                            sideChunkDistX = (playerX - chunkMinX) * deltaDistX;
                        }
                        else
                        {
                            sideChunkDistX = (chunkMaxX - playerX) * deltaDistX;
                        }

                        if (stepY == -1)
                        {
                            sideChunkDistY = (playerY - chunkMinY) * deltaDistY;
                        }
                        else
                        {
                            sideChunkDistY = (chunkMaxY - playerY) * deltaDistY;
                        }

                        if (stepZ == -1)
                        {
                            sideChunkDistZ = (playerZ - chunkMinZ) * deltaDistZ;
                        }
                        else
                        {
                            sideChunkDistZ = (chunkMaxZ - playerZ) * deltaDistZ;
                        }

                        chunkIsOccupied = chunks[ChunkIndex(chunkMapX, chunkMapY, chunkMapZ)];
                    }
                }
                else
                {
                    safety += 4;
                    if (sideChunkDistX < sideChunkDistY)
                    {
                        if (sideChunkDistX < sideChunkDistZ)
                        {
                            sideChunkDistX += deltaChunkDistX;
                            chunkMapX += stepX;
                            side = 0;
                        }
                        else
                        {
                            sideChunkDistZ += deltaChunkDistZ;
                            chunkMapZ += stepZ;
                            side = 2;
                        }
                    }
                    else
                    {
                        if (sideChunkDistY < sideChunkDistZ)
                        {
                            sideChunkDistY += deltaChunkDistY;
                            chunkMapY += stepY;
                            side = 1;
                        }
                        else
                        {
                            sideChunkDistZ += deltaChunkDistZ;
                            chunkMapZ += stepZ;
                            side = 2;
                        }
                    }

                    chunkMinX = chunkMapX * chunkSize;
                    chunkMaxX = chunkMinX + chunkSize;
                    chunkMinY = chunkMapY * chunkSize;
                    chunkMaxY = chunkMinY + chunkSize;
                    chunkMinZ = chunkMapZ * chunkSize;
                    chunkMaxZ = chunkMinZ + chunkSize;

                    if (side == 0)
                    {
                        if (stepX == 1)
                        {
                            mapX = chunkMinX;
                            sideDistX = (mapX + 1f - playerX) * deltaDistX;
                        }
                        else
                        {
                            mapX = chunkMaxX - 1;
                            sideDistX = (playerX - mapX) * deltaDistX;
                        }
                    }

                    if (side == 1)
                    {
                        if (stepY == 1)
                        {
                            mapY = chunkMinY;
                            sideDistY = (mapY + 1f - playerY) * deltaDistY;
                        }
                        else
                        {
                            mapY = chunkMaxY - 1;
                            sideDistY = (playerY - mapY) * deltaDistY;
                        }
                    }

                    if (side == 2)
                    {
                        if (stepZ == 1)
                        {
                            mapZ = chunkMinZ;
                            sideDistZ = (mapZ + 1f - playerZ) * deltaDistZ;
                        }
                        else
                        {
                            mapZ = chunkMaxZ - 1;
                            sideDistZ = (playerZ - mapZ) * deltaDistZ;
                        }
                    }

                    if (mapX < 0 || mapX >= mapWidth || mapY < 0 || mapY >= mapHeight || mapZ < 0 ||
                        mapZ >= mapDepth) break;
                    chunkIsOccupied = chunks[ChunkIndex(chunkMapX, chunkMapY, chunkMapZ)];
                }

                if (IsSolid(mapX, mapY, mapZ)) hit = true;
            }

            if (!hit)
            {
                pixelBuffer[px + py * renderWidth] = Color.Black;
                continue;
            }

            float perpWallDist = side switch
            {
                0 => (mapX - playerX + (1 - stepX) / 2f) / rayDir.X,
                1 => (mapY - playerY + (1 - stepY) / 2f) / rayDir.Y,
                2 => (mapZ - playerZ + (1 - stepZ) / 2f) / rayDir.Z,
                _ => throw new InvalidOperationException("Invalid side")
            };

            byte shade = (byte)Math.Clamp(255 - perpWallDist * 25, 40, 255);

            Color color = side switch
            {
                0 => new Color(shade, shade, shade, (byte)255),
                1 => new Color((byte)(shade * 0.7f), (byte)(shade * 0.7f), (byte)(shade * 0.7f), (byte)255),
                2 => new Color((byte)(shade * 0.3f), (byte)(shade * 0.3f), (byte)(shade * 0.3f), (byte)255),
                _ => throw new InvalidOperationException("Invalid side")
            };


            if (py == 200 && px == 300)
            {
                color = new Color(0f, shade, 0f, (byte)255);
            }

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