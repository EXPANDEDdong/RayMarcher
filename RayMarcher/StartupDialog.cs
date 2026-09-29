using System.Numerics;
using Raylib_cs;

namespace RayMarcher;


public sealed record GameSettings(
    int WindowWidth,
    int WindowHeight,
    int MapWidth,
    int MapHeight,
    int MapDepth,
    int Seed);

/// <summary>
/// AI slopped together shit to get the boring stuff out of the way
/// </summary>
public static class StartupDialog
{
    private const int DialogWidth = 460;
    private const int DialogHeight = 460;

    public static GameSettings? Show(GameSettings? defaults = null)
    {
        defaults ??= new GameSettings(1280, 720, 128, 128, 128, Random.Shared.Next());

        Raylib.InitWindow(DialogWidth, DialogHeight, "New game");
        Raylib.SetTargetFPS(60);

        const float labelX = 30;
        const float boxX = 200;
        const float boxW = 220;
        const float boxH = 34;
        const float rowStart = 60;
        const float rowGap = 52;

        var fields = new[]
        {
            new IntField("Window width",  defaults.WindowWidth,  640, 7680, Row(0)),
            new IntField("Window height", defaults.WindowHeight, 360, 4320, Row(1)),
            new IntField("Map width",     defaults.MapWidth,     8,   4096, Row(2)),
            new IntField("Map height",    defaults.MapHeight,    8,   4096, Row(3)),
            new IntField("Map depth",     defaults.MapDepth,     8,   4096, Row(4)),
            new IntField("Seed",          defaults.Seed,         0, int.MaxValue, Row(5)),
        };

        // Seed field is narrower to make room for the "Random" button.
        var seedField = fields[5];
        seedField.Bounds = new Rectangle(boxX, Row(5).Y, boxW - 80, boxH);
        var randomButton = new Rectangle(boxX + boxW - 72, Row(5).Y, 72, boxH);
        var startButton = new Rectangle(DialogWidth - 30 - 130, DialogHeight - 30 - 40, 130, 40);

        int focus = 0;
        bool accepted = false;

        while (!Raylib.WindowShouldClose()) // Esc or window close button = cancel
        {
            Vector2 mouse = Raylib.GetMousePosition();
            bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

            // --- input ---
            if (clicked)
            {
                for (int i = 0; i < fields.Length; i++)
                    if (Raylib.CheckCollisionPointRec(mouse, fields[i].Bounds))
                        focus = i;

                if (Raylib.CheckCollisionPointRec(mouse, randomButton))
                    seedField.Text = Random.Shared.Next().ToString();

                if (Raylib.CheckCollisionPointRec(mouse, startButton))
                    accepted = true;
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Tab))
            {
                bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
                focus = (focus + (shift ? fields.Length - 1 : 1)) % fields.Length;
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
                accepted = true;

            // Always drain the char queue so stray characters never pile up.
            int ch;
            while ((ch = Raylib.GetCharPressed()) != 0)
                if (ch is >= '0' and <= '9')
                    fields[focus].Append((char)ch);

            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace))
                fields[focus].Backspace();

            if (accepted && fields.All(f => f.IsValid))
                break;
            accepted = false; // invalid input: stay open, invalid fields are drawn red

            // --- draw ---
            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.RayWhite);
            Raylib.DrawText("Game setup", (int)labelX, 16, 26, Color.DarkGray);

            for (int i = 0; i < fields.Length; i++)
            {
                IntField f = fields[i];
                bool isFocused = i == focus;

                Raylib.DrawText(f.Label, (int)labelX, (int)(f.Bounds.Y + 8), 20, Color.DarkGray);
                Raylib.DrawRectangleRec(f.Bounds, Color.White);

                Color border = !f.IsValid ? Color.Red : isFocused ? Color.Blue : Color.Gray;
                Raylib.DrawRectangleLinesEx(f.Bounds, isFocused ? 2 : 1, border);

                Raylib.DrawText(f.Text, (int)f.Bounds.X + 8, (int)f.Bounds.Y + 8, 20, Color.Black);

                if (isFocused && (int)(Raylib.GetTime() * 2) % 2 == 0)
                {
                    int textWidth = Raylib.MeasureText(f.Text, 20);
                    Raylib.DrawRectangle((int)f.Bounds.X + 10 + textWidth, (int)f.Bounds.Y + 7, 2, 22, Color.Black);
                }

                // Range hint under the box, only while invalid
                if (!f.IsValid)
                    Raylib.DrawText($"{f.Min} - {f.Max}", (int)f.Bounds.X, (int)(f.Bounds.Y + f.Bounds.Height + 2), 12, Color.Red);
            }

            DrawButton(randomButton, "Random", mouse);
            DrawButton(startButton, "Start", mouse);
            Raylib.EndDrawing();
        }

        GameSettings? result = accepted
            ? new GameSettings(
                fields[0].Value, fields[1].Value,
                fields[2].Value, fields[3].Value,
                fields[4].Value, fields[5].Value)
            : null;

        Raylib.CloseWindow();
        return result;

        Rectangle Row(int index) => new(boxX, rowStart + index * rowGap, boxW, boxH);
    }

    private static void DrawButton(Rectangle bounds, string text, Vector2 mouse)
    {
        bool hover = Raylib.CheckCollisionPointRec(mouse, bounds);
        Raylib.DrawRectangleRec(bounds, hover ? Color.SkyBlue : Color.LightGray);
        Raylib.DrawRectangleLinesEx(bounds, 1, Color.Gray);
        int textWidth = Raylib.MeasureText(text, 20);
        Raylib.DrawText(text,
            (int)(bounds.X + (bounds.Width - textWidth) / 2),
            (int)(bounds.Y + (bounds.Height - 20) / 2),
            20, Color.Black);
    }

    private sealed class IntField
    {
        private const int MaxLength = 10;

        public string Label { get; }
        public int Min { get; }
        public int Max { get; }
        public string Text { get; set; }
        public Rectangle Bounds { get; set; }

        public IntField(string label, int initial, int min, int max, Rectangle bounds)
        {
            Label = label;
            Min = min;
            Max = max;
            Text = initial.ToString();
            Bounds = bounds;
        }

        public void Append(char c)
        {
            if (Text.Length < MaxLength)
                Text += c;
        }

        public void Backspace()
        {
            if (Text.Length > 0)
                Text = Text[..^1];
        }

        public bool IsValid => long.TryParse(Text, out long v) && v >= Min && v <= Max;

        public int Value => (int)long.Parse(Text);
    }
}