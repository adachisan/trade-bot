using System.Diagnostics;
using System.Runtime.InteropServices;

#pragma warning disable CA1416

/// <summary>Custom functions for console development by adachisan.</summary>
struct Menu
{
    /// <summary>Gets or sets console title.
    /// <para>Will not set if value is equal to title.</para>
    /// <para>Only works on windows platform.</para>
    /// </summary>
    public static string Title
    {
        get => Console.Title;
        set
        {
            if (Console.Title != value)
                Console.Title = value;
        }
    }

    /// <summary>Clears line on specific X and Y axis.</summary>
    static void ClearLine(int X, int Y, int size)
    {
        Console.SetCursorPosition(X, Y);
        Console.Write(new string(' ', size));
        Console.SetCursorPosition(X, Y);
    }

    /// <summary>Reads console line on specific Y axis.</summary>
    public static string Read(int Y = 1)
    {
        ClearLine(0, Y, Console.WindowWidth);
        return Console.ReadLine();
    }

    /// <summary>Gets or sets console window size.
    /// <para>Only works on windows platform.</para>
    /// </summary>
    public static (int X, int Y) Size
    {
        get => (Console.WindowWidth, Console.WindowHeight);
        set => Console.SetWindowSize(value.X, value.Y);
    }

    /// <summary>Writes on specific X and Y axis.</summary>
    public static void Write(string text, int x = 0, int y = 0, ConsoleColor color = ConsoleColor.White)
    {
        Console.CursorVisible = false;
        (int X, int Y) cursor = (Console.CursorLeft, Console.CursorTop);
        Console.ForegroundColor = color;
        ClearLine(x, y, text.Length + 1);
        Console.Write(text);
        Console.ForegroundColor = ConsoleColor.White;
        Console.SetCursorPosition(cursor.X, cursor.Y);
        Console.CursorVisible = true;
    }

    /// <summary>Sets an action before console exits.</summary>
    public static void onExit(Action a)
    {
        AppDomain.CurrentDomain.ProcessExit += (s, e) => { a(); Thread.Sleep(1000); };
    }

    /// <summary>Clears everything on console.</summary>
    public static void Clear() => Console.Clear();

    /// <summary>Prints a table on console.</summary>
    public static void Table(string[][] items, int minWidth = 3, ConsoleColor[] colors = default)
    {
        int rows = items.Length;
        int columns = items[0].Length;
        int[] maxWidth = new int[columns];
        int[] position = new int[columns];

        for (int i = 0; i < rows; i++)
            for (int n = 0; n < columns; n++)
                if (items[i][n].Length > maxWidth[n])
                    maxWidth[n] = items[i][n].Length;

        for (int i = 1; i < position.Length; i++)
            position[i] = maxWidth[i - 1] + position[i - 1] + minWidth;

        var checkColor = (int i) => colors == null || colors[i] == default ? ConsoleColor.White : colors[i];

        for (int i = 0; i < rows; i++)
            for (int n = 0; n < columns; n++)
                Menu.Write(items[i][n], position[n], i, checkColor(i));
    }

    /// <summary>Prints a table on console.</summary>
    public static void Table(string[,] items, int minWidth = 3, ConsoleColor[] colors = default)
    {
        int rows = items.GetLength(0);
        int columns = items.GetLength(1);
        int[] maxWidth = new int[columns];
        int[] position = new int[columns];

        for (int i = 0; i < rows; i++)
            for (int n = 0; n < columns; n++)
                if (items[i, n].Length > maxWidth[n])
                    maxWidth[n] = items[i, n].Length;

        for (int i = 1; i < position.Length; i++)
            position[i] = maxWidth[i - 1] + position[i - 1] + minWidth;

        var checkColor = (int i) => colors == null || colors[i] == default ? ConsoleColor.White : colors[i];

        for (int i = 0; i < rows; i++)
            for (int n = 0; n < columns; n++)
                Menu.Write(items[i, n], position[n], i, checkColor(i));
    }

    /// <summary>Hides or shows window function from 'user32.dll'.</summary>
    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hwnd, int viewState);

    /// <summary>Stores the current process.
    /// <para>Needs to store because cannot get current process after hidden.</para>
    /// </summary>
    static Process _process { get; set; } = null;

    /// <summary>Stores if current window is visible or not.</summary>
    static bool _visible { get; set; } = true;

    /// <summary>Gets or sets view state of current window.
    /// <para>Only works on windows since needs 'user32.dll'.</para>
    /// </summary>
    public static bool Visible
    {
        get => _visible;
        set
        {
            if (!value)
                _process = Process.GetCurrentProcess();
            ShowWindow(_process.MainWindowHandle, value ? 1 : 0);
            _visible = value;
        }
    }

    /// <summary>Shows message box function from 'user32.dll'.
    /// <para>Only works on windows since needs 'user32.dll'.</para>
    /// </summary>
    [DllImport("user32.dll")]
    public static extern MsgBox MessageBox(int hWnd, string message, string title, MsgType type = MsgType.Ok);

    /// <summary>Possible returns from message box function.</summary>
    public enum MsgBox { Ok = 1, Cancel = 2, Abort = 3, Retry = 4, Ignore = 5, Yes = 6, No = 7, Try = 10, Continue = 11 }

    /// <summary>Types of message box.</summary>
    public enum MsgType { Ok = 0, OkCancel = 1, Ignore = 2, YesNoCancel = 3, YesNo = 4, Repeat = 5, Try = 6 }

    /// <summary>Gets key state function from 'user32.dll'.</summary>
    [DllImport("user32.dll")]
    static extern short GetKeyState(ConsoleKey key);

    /// <summary>Returns if specific key is pressed.
    /// <para>Only works on windows since needs 'user32.dll'.</para>
    /// </summary>
    public static bool IsKeyDown(ConsoleKey key) => (GetKeyState(key) & 0x8000) != 0;
}