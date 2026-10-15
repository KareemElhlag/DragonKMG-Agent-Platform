using System.Globalization;
using System.Text;

namespace DragonKMG.Cli;

/// <summary>Bounded terminal viewport; no provider content is interpreted as ANSI control sequences.</summary>
public sealed class TerminalSurface : IDisposable
{
    private readonly List<(string Kind, string Text)> entries = [];
    private readonly List<string> history = [];
    private int scrollOffset;
    private int cachedWidth;
    private List<(string Kind, string Text)>? cachedRows;
    private bool? originalControlC;
    public TerminalSurface()
    {
        if (Interactive) { originalControlC = Console.TreatControlCAsInput; Console.TreatControlCAsInput = true; }
    }
    public string SessionName { get; set; } = "";
    public string Context { get; set; } = "";
    public string Resources { get; set; } = "";
    // Windows console capability is not described by inherited POSIX TERM.
    // Desktop launchers may set TERM=dumb even for a real Windows Terminal window.
    private bool Interactive => CanUseInteractive(Console.IsInputRedirected, Console.IsOutputRedirected, OperatingSystem.IsWindows(), Environment.GetEnvironmentVariable("TERM"));

    public static bool CanUseInteractive(bool inputRedirected, bool outputRedirected, bool windows, string? term) =>
        !inputRedirected && !outputRedirected && (windows || !string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase));

    public void Add(string kind, string text)
    {
        text = SafeText(text);
        if (text.Length > 32000) text = text[..32000] + "\n[Display shortened; full conversation is retained in the session report.]";
        entries.Add((kind, text));
        cachedRows = null;
        if (entries.Count > 200) entries.RemoveAt(0);
        scrollOffset = 0;
        if (!Interactive) Console.WriteLine("[" + kind + "] " + SafeText(text));
    }

    public static string SafeText(string text) => new(text.Replace("\r\n", "\n").Replace('\r', '\n')
        .Where(character => character == '\n' || (!char.IsControl(character) && character != '\u007f')).ToArray());

    public static IReadOnlyList<string> Wrap(string text, int width)
    {
        width = Math.Max(2, width);
        var lines = new List<string>();
        foreach (var line in SafeText(text).Split('\n'))
        {
            var row = new StringBuilder();
            var count = 0;
            foreach (System.Text.RegularExpressions.Match token in System.Text.RegularExpressions.Regex.Matches(line, @"\s+|\S+"))
            {
                var tokenWidth = DisplayWidth(token.Value);
                if (tokenWidth <= width)
                {
                    if (count > 0 && count + tokenWidth > width) { lines.Add(row.ToString()); row.Clear(); count = 0; }
                    row.Append(token.Value); count += tokenWidth;
                }
                else
                {
                    var elements = StringInfo.GetTextElementEnumerator(token.Value);
                    while (elements.MoveNext())
                    {
                        var element = elements.GetTextElement(); var cells = CellWidth(element);
                        if (count > 0 && count + cells > width) { lines.Add(row.ToString()); row.Clear(); count = 0; }
                        row.Append(element); count += cells;
                    }
                }
            }
            lines.Add(row.ToString());
        }
        return lines;
    }

    private static string Color(string kind) => kind switch
    {
        "you" => "\u001b[38;5;117m",
        "assistant" => "\u001b[38;5;158m",
        "error" => "\u001b[38;5;210m",
        "process" => "\u001b[38;5;215m",
        _ => "\u001b[38;5;145m"
    };

    public void Draw(string draft = "", string status = "Ready", int? caret = null)
    {
        if (!Interactive) return;
        try { Console.Write(RenderFrame(Console.WindowWidth, Console.WindowHeight, draft, status, caret)); }
        catch (IOException) { /* Terminal detached or resized during a frame. */ }
        catch (ArgumentOutOfRangeException) { /* Retry dimensions on the next frame. */ }
    }

    public string RenderFrame(int columns, int consoleRows, string draft = "", string status = "Ready", int? caret = null)
    {
        var width = Math.Max(10, columns - 1);
        var height = Math.Max(8, consoleRows);
        var contentHeight = Math.Max(1, height - 8);
        if (cachedRows is null || cachedWidth != width)
        {
            cachedRows = entries.SelectMany(entry => Wrap(entry.Text, Math.Max(1, width - 3))
                .Select((line, index) => (entry.Kind, Text: (index == 0 ? "› " : "  ") + line))).ToList();
            cachedWidth = width;
        }
        var rows = cachedRows;
        scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, rows.Count - 1));
        var end = Math.Max(0, rows.Count - scrollOffset);
        var start = Math.Max(0, end - contentHeight);
        var output = new StringBuilder("\u001b[?25l\u001b[H");
        void Line(string text, string color = "\u001b[38;5;145m", bool visual = false)
        {
            text = Clip(text.Replace('\n', ' '), width);
            if (!visual)
            {
                try
                {
                    var layout = TerminalTextLayout.Render(text);
                    text = layout.Text;
                    if (layout.IsRightToLeft) text = new string(' ', Math.Max(0, width - DisplayWidth(text))) + text;
                }
                catch (PlatformNotSupportedException) { text = "[Arabic terminal layout unavailable: install system ICU.]"; }
            }
            output.Append(color).Append(text).Append("\u001b[0m\u001b[K\n");
        }
        Line(" DragonKMG  /  " + SafeText(SessionName), "\u001b[1;38;5;117m");
        Line(SafeText(Context), "\u001b[38;5;158m");
        Line(SafeText(Resources));
        Line(new string('─', width));
        for (var i = 0; i < contentHeight; i++)
        {
            var index = start + i;
            Line(index < end ? rows[index].Text : "", index < end ? Color(rows[index].Kind) : "");
        }
        Line(new string('─', width), "\u001b[38;5;67m");
        var position = Math.Clamp(caret ?? draft.Length, 0, draft.Length);
        var boundaries = StringInfo.ParseCombiningCharacters(draft);
        position = position == draft.Length ? position : boundaries.LastOrDefault(offset => offset <= position);
        var prompt = LayoutPrompt(draft, position, width - 4);
        Line("❯ " + prompt.Text, "\u001b[38;5;255m", visual: true);
        Line(SafeText(status) + (scrollOffset > 0 ? " • history" : "") + "  /help • Enter send • PgUp/PgDn history", "\u001b[38;5;145m");
        output.Append("\u001b[J\u001b[").Append(height - 2).Append(';')
            .Append(Math.Min(width, 3 + prompt.CaretColumn)).Append("H\u001b[?25h");

        return output.ToString();
    }

    public static (string Text, int CaretColumn) LayoutPrompt(string draft, int caret, int availableColumns)
    {
        availableColumns = Math.Max(2, availableColumns);
        TerminalVisualLine layout;
        try { layout = TerminalTextLayout.Render(draft); }
        catch (PlatformNotSupportedException) { return ("[Arabic requires system ICU]", 0); }
        caret = Math.Clamp(caret, 0, draft.Length);
        var cursorColumn = layout.CaretColumns[caret];
        var requestedStart = Math.Max(0, cursorColumn - availableColumns + 1);
        var elements = StringInfo.GetTextElementEnumerator(layout.Text);
        var start = 0; var used = 0; var visible = new StringBuilder();
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement(); var cells = CellWidth(element);
            if (start + cells <= requestedStart && visible.Length == 0) { start += cells; continue; }
            if (used + cells > availableColumns) break;
            visible.Append(element); used += cells;
        }
        var padding = layout.IsRightToLeft && DisplayWidth(layout.Text) <= availableColumns
            ? availableColumns - used : 0;
        return (new string(' ', padding) + visible, Math.Clamp(padding + cursorColumn - start, 0, availableColumns));
    }

    public static int MoveVisualCaret(string text, int caret, bool left)
    {
        TerminalVisualLine layout;
        try { layout = TerminalTextLayout.Render(text); }
        catch (PlatformNotSupportedException) { return left ? PreviousBoundary(text, caret) : NextBoundary(text, caret); }
        caret = Math.Clamp(caret, 0, text.Length);
        var current = layout.CaretColumns[caret];
        var candidates = StringInfo.ParseCombiningCharacters(text).Append(text.Length)
            .Where(index => left ? layout.CaretColumns[index] < current : layout.CaretColumns[index] > current)
            .OrderBy(index => Math.Abs(layout.CaretColumns[index] - current)).ThenBy(index => Math.Abs(index - caret));
        return candidates.FirstOrDefault(caret);
    }

    private static string Clip(string text, int width)
    {
        var result = new StringBuilder();
        var used = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement(); var cells = CellWidth(element);
            if (used + cells > width) break;
            result.Append(element); used += cells;
        }
        return result.ToString();
    }

    public static int CellWidth(string element)
    {
        if (element.EnumerateRunes().All(rune => Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format or UnicodeCategory.Control)) return 0;
        var wide = element.EnumerateRunes().Any(rune => rune.Value is >= 0x1100 and <= 0x115f or 0x2329 or 0x232a
            or >= 0x2e80 and <= 0xa4cf or >= 0xac00 and <= 0xd7a3 or >= 0xf900 and <= 0xfaff
            or >= 0xfe10 and <= 0xfe19 or >= 0xfe30 and <= 0xfe6f or >= 0xff01 and <= 0xff60
            or >= 0xffe0 and <= 0xffe6 or >= 0x1f000 and <= 0x1faff or >= 0x20000 and <= 0x3ffff or 0xfe0f);
        return wide ? 2 : 1;
    }

    public static int DisplayWidth(string text)
    {
        var elements = StringInfo.GetTextElementEnumerator(text);
        var width = 0;
        while (elements.MoveNext()) width += CellWidth(elements.GetTextElement());
        return width;
    }

    private static int PreviousBoundary(string text, int caret) => StringInfo.ParseCombiningCharacters(text).LastOrDefault(offset => offset < caret);
    private static int NextBoundary(string text, int caret) => StringInfo.ParseCombiningCharacters(text).FirstOrDefault(offset => offset > caret, text.Length);

    public async Task<string?> ReadAsync(CancellationToken ct)
    {
        if (!Interactive) { Console.Write("DragonKMG > "); return Console.ReadLine(); }
        var draft = new StringBuilder();
        var caret = 0;
        var historyIndex = history.Count;
        var dimensions = (Console.WindowWidth, Console.WindowHeight);
        Draw();
        while (!ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(40, ct);
                var current = (Console.WindowWidth, Console.WindowHeight);
                if (dimensions != current) { dimensions = current; Draw(draft.ToString(), caret: caret); }
                continue;
            }
            var key = Console.ReadKey(true);
            if (draft.Length == 0 && key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key is ConsoleKey.D or ConsoleKey.Z) return null;
            if (key.Key == ConsoleKey.Enter)
            {
                var text = draft.ToString().Trim();
                if (text.Length > 0) { history.Add(text); if (history.Count > 100) history.RemoveAt(0); }
                return text;
            }
            if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control)) { draft.Clear(); caret = 0; }
            else if (key.Key == ConsoleKey.PageUp) scrollOffset += Math.Max(1, Console.WindowHeight - 10);
            else if (key.Key == ConsoleKey.PageDown) scrollOffset = Math.Max(0, scrollOffset - Math.Max(1, Console.WindowHeight - 10));
            else if (key.Key == ConsoleKey.LeftArrow) caret = MoveVisualCaret(draft.ToString(), caret, left: true);
            else if (key.Key == ConsoleKey.RightArrow) caret = MoveVisualCaret(draft.ToString(), caret, left: false);
            else if (key.Key == ConsoleKey.Home) caret = 0;
            else if (key.Key == ConsoleKey.End) caret = draft.Length;
            else if (key.Key == ConsoleKey.Backspace && caret > 0) { var previous = PreviousBoundary(draft.ToString(), caret); draft.Remove(previous, caret - previous); caret = previous; }
            else if (key.Key == ConsoleKey.Delete && caret < draft.Length) draft.Remove(caret, NextBoundary(draft.ToString(), caret) - caret);
            else if (key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow)
            {
                historyIndex = Math.Clamp(historyIndex + (key.Key == ConsoleKey.UpArrow ? -1 : 1), 0, history.Count);
                draft.Clear(); if (historyIndex < history.Count) draft.Append(history[historyIndex]); caret = draft.Length;
            }
            else if (!char.IsControl(key.KeyChar) && draft.Length < 32000) { draft.Insert(caret++, key.KeyChar); scrollOffset = 0; }
            Draw(draft.ToString(), status: TerminalTextLayout.IsAvailable ? "Ready · Arabic shaping + bidi" : "Ready · ICU unavailable", caret: caret);
        }
        return null;
    }

    public async Task<T> TrackAsync<T>(Task<T> operation, CancellationTokenSource cancellation)
    {
        if (!Interactive) { Console.WriteLine("[process] Waiting for provider response…"); return await operation; }
        var started = DateTimeOffset.UtcNow;
        while (!operation.IsCompleted)
        {
            Draw(status: $"Running • {(DateTimeOffset.UtcNow - started).TotalSeconds:F0}s • Ctrl+C cancel");
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control)) cancellation.Cancel();
            }
            await Task.WhenAny(operation, Task.Delay(250));
        }
        return await operation;
    }

    public void Clear() { entries.Clear(); cachedRows = null; }
    public void Dispose()
    {
        if (originalControlC is bool original) Console.TreatControlCAsInput = original;
        if (Interactive) Console.Write("\u001b[0m\u001b[?25h\n");
    }
}
