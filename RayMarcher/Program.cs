using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Raylib_cs;
using RayMarcher;
using RayMarcher.Framework;
using RayMarcher.Systems.States;

GameSettings? settings = StartupDialog.Show();
if (settings is null) return;

var services = new ServiceCollection();
services.AddSingleton<GameSettings>(b => settings);
services.AddFrameSystems(Assembly.GetExecutingAssembly());

var cameraState = new CameraState();

services.AddSingleton<ICameraStateReader>(b => cameraState);
services.AddSingleton<ICameraStateWriter>(b => cameraState);

using var provider = services.BuildServiceProvider();
var scheduler = provider.GetRequiredService<FrameScheduler>();
scheduler.RunSetup();

while (!Raylib.WindowShouldClose())
{
    scheduler.Tick(Raylib.GetFrameTime());
}