using DragonKMG.Cli;

namespace DragonKMG.Tests;

public sealed class TerminalSurfaceTests
{
    [Fact]
    public void ProcessUpdatesDoNotDragTerminalHistoryToLatest()
    {
        using var surface = new TerminalSurface();
        surface.Add("assistant", string.Join("\n", Enumerable.Range(0, 60).Select(index => "history-row-" + index)));
        surface.RenderFrame(80, 24);
        surface.ScrollHistory(25);
        var before = surface.RenderFrame(80, 24);
        surface.Add("process", "new execution stage");
        var after = surface.RenderFrame(80, 24);
        Assert.Equal(before, after);
        surface.ScrollHistory(-1000);
        Assert.Contains("new execution stage", surface.RenderFrame(80, 24));
    }
    [Fact]
    public void WrappingKeepsMixedModelIdentifierWholeWhenItFitsOneLine()
    {
        const string text = "مرحبا بالعالم model-id 123 نص عربي";
        var lines = TerminalSurface.Wrap(text, 16);
        Assert.Equal(text, string.Concat(lines));
        Assert.Contains(lines, line => line.Contains("model-id"));
        Assert.All(lines, line => Assert.True(TerminalSurface.DisplayWidth(line) <= 16));
    }

    [Fact]
    public void ArabicPromptUsesVisualCaretAndPreservesLogicalInput()
    {
        const string input = "تم";
        var result = TerminalSurface.LayoutPrompt(input, input.Length, 20);
        Assert.Equal(new string(' ', 18) + "\ufee2\ufe97", result.Text);
        Assert.Equal(18, result.CaretColumn);
        Assert.Equal(1, TerminalSurface.MoveVisualCaret(input, input.Length, left: false));
        Assert.Equal(1, TerminalSurface.MoveVisualCaret(input, 0, left: true));
        Assert.Equal("تم", input);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(25)]
    public void LongMixedPromptKeepsCaretInsideViewport(int width)
    {
        const string input = "مرحبا model-id 123 بالعالم ❤️ للاختبار";
        foreach (var index in System.Globalization.StringInfo.ParseCombiningCharacters(input).Append(input.Length))
        {
            var prompt = TerminalSurface.LayoutPrompt(input, index, width);
            Assert.InRange(prompt.CaretColumn, 0, width);
            Assert.True(TerminalSurface.DisplayWidth(prompt.Text) <= width);
        }
    }

    [Theory]
    [InlineData(false, false, true, "dumb", true)]
    [InlineData(false, false, false, "dumb", false)]
    [InlineData(false, false, false, "xterm-256color", true)]
    [InlineData(true, false, true, "dumb", false)]
    [InlineData(false, true, true, "xterm-256color", false)]
    public void NativeWindowsConsoleDoesNotInheritPosixFallback(bool input, bool output, bool windows, string term, bool expected) =>
        Assert.Equal(expected, TerminalSurface.CanUseInteractive(input, output, windows, term));

    [Theory]
    [InlineData(40, 15)]
    [InlineData(80, 24)]
    [InlineData(120, 35)]
    public void PromptStaysAtBottomAfterTranscriptAndResize(int columns, int rows)
    {
        using var surface = new TerminalSurface { SessionName = "test", Context = "agent · model", Resources = "4 skills" };
        surface.Add("assistant", string.Join("\n", Enumerable.Repeat("中🐉 response", 60)));
        var frame = surface.RenderFrame(columns, rows, "draft 中🐉", caret: 10);
        Assert.Contains("❯ draft 中🐉", frame);
        Assert.Contains($"\u001b[{rows - 2};", frame);
        Assert.Equal(rows - 1, frame.Count(character => character == '\n'));
        surface.Add("process", "Completed");
        Assert.Contains("Completed", surface.RenderFrame(columns, rows));
    }

    [Theory]
    [InlineData("exit criteria should include tests")]
    [InlineData("model the transfer workflow")]
    [InlineData("open the implementation plan")]
    public void NaturalPromptsCannotTriggerControlCommands(string text)
    {
        Assert.Equal(("__prompt", text), TerminalInput.Parse(text));
    }

    [Fact]
    public void SlashCommandsAreExplicit()
    {
        Assert.Equal(("use", "Execution Agent"), TerminalInput.Parse("/use Execution Agent"));
        Assert.Equal(("exit", ""), TerminalInput.Parse("/exit"));
    }

    [Fact]
    public void ProviderOutputCannotInjectTerminalEscapeSequences()
    {
        var text = TerminalSurface.SafeText("hello\u001b[2J\u0007\r\nworld\t!");
        Assert.DoesNotContain('\u001b', text);
        Assert.DoesNotContain('\u0007', text);
        Assert.Equal("hello[2J\nworld!", text);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(76)]
    public void WrapRetainsUnicodeGraphemesAndContent(int width)
    {
        const string value = "Dragon 🐉 العربية e\u0301";
        var lines = TerminalSurface.Wrap(value, width);
        Assert.Equal(value, string.Concat(lines));
        Assert.DoesNotContain(lines, line => line.EndsWith('\ud83d'));
        Assert.DoesNotContain(lines, line => line.StartsWith('\u0301'));
    }

    [Fact]
    public void WideTextFitsActualTerminalCells()
    {
        Assert.Equal(2, TerminalSurface.DisplayWidth("🐉"));
        Assert.Equal(2, TerminalSurface.DisplayWidth("中"));
        Assert.Equal(1, TerminalSurface.DisplayWidth("e\u0301"));
        Assert.All(TerminalSurface.Wrap("中🐉中🐉text", 6), line => Assert.True(TerminalSurface.DisplayWidth(line) <= 6));
    }

    [Fact]
    public void WrapPreservesBlankLinesAndHandlesTinyWidths()
    {
        Assert.Equal(new[] { "a", "", "b" }, TerminalSurface.Wrap("a\n\nb", 0));
    }
}
