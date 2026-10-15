using DragonKMG.Cli;

namespace DragonKMG.Tests;

public sealed class TerminalTextLayoutTests
{
    [Fact] public void WindowsRuntimeLoadsSystemIcu() { if (OperatingSystem.IsWindows()) Assert.True(TerminalTextLayout.IsAvailable); }
    [Fact] public void EmojiVariationSelectorAndFormatWidthsMatchSurface() { var heart = TerminalTextLayout.Render("❤️"); Assert.Equal(2, heart.CaretColumns[^1]); Assert.Equal(TerminalSurface.DisplayWidth(heart.Text), heart.CaretColumns[^1]); var format = TerminalTextLayout.Render("\u200c"); Assert.Equal(0, format.CaretColumns[^1]); }
    [Fact] public void FirstStrongLatinHeaderKeepsLeftToRightParagraph() { if (!TerminalTextLayout.IsAvailable) return; Assert.False(TerminalTextLayout.Render("model: مرحبا 123").IsRightToLeft); }
    [Fact] public void LatinFastPathPreservesTextAndCaret() { var line = TerminalTextLayout.Render("model-123"); Assert.Equal("model-123", line.Text); Assert.False(line.IsRightToLeft); Assert.Equal(Enumerable.Range(0, 10), line.CaretColumns); }
    [Theory]
    [InlineData("تم", "\ufee2\ufe97")]
    [InlineData("مرحبا", "\ufe8e\ufe92\ufea3\ufeae\ufee3")]
    public void ArabicUsesContextualFormsAndVisualOrder(string logical, string visual) { if (!TerminalTextLayout.IsAvailable) return; var line = TerminalTextLayout.Render(logical); Assert.Equal(visual, line.Text); Assert.True(line.IsRightToLeft); Assert.Equal(logical.Length + 1, line.CaretColumns.Length); Assert.Equal(0, line.CaretColumns[^1]); Assert.Equal(logical.Length, line.CaretColumns[0]); }
    [Fact] public void MixedIdentifierNumbersAndEmojiRemainIntact() { if (!TerminalTextLayout.IsAvailable) return; var logical = "مرحبا model-id/123 👩‍💻"; var line = TerminalTextLayout.Render(logical); Assert.Contains("model-id/123", line.Text); Assert.Contains("👩‍💻", line.Text); Assert.Equal(logical.Length + 1, line.CaretColumns.Length); Assert.All(line.CaretColumns, column => Assert.InRange(column, 0, 100)); Assert.Equal("مرحبا model-id/123 👩‍💻", logical); }
    [Fact] public void LamAlefPreservesTwoCursorSlots() { if (!TerminalTextLayout.IsAvailable) return; var line = TerminalTextLayout.Render("لا"); Assert.Equal(2, line.Text.Length); Assert.Equal(3, line.CaretColumns.Length); Assert.DoesNotContain(line.Text, c => c is >= '\ufef5' and <= '\ufefc'); }
    [Fact] public void CombiningMarksStayWithTheirBaseAndZwnjBreaksJoining() { if (!TerminalTextLayout.IsAvailable) return; var marked = TerminalTextLayout.Render("تَم"); Assert.Contains("\ufe97\u064e", marked.Text); Assert.Equal(4, marked.CaretColumns.Length); var separated = TerminalTextLayout.Render("ت\u200cم"); Assert.Contains('\ufe95', separated.Text); Assert.Contains('\ufee1', separated.Text); }
}
