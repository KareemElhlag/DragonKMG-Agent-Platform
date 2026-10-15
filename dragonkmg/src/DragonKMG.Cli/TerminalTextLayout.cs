using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace DragonKMG.Cli;

public sealed record TerminalVisualLine(string Text, bool IsRightToLeft, int[] CaretColumns);

/// <summary>Presentation only: logical prompts and persisted/provider text are never mutated.</summary>
public static class TerminalTextLayout
{
    private static readonly NativeIcuBidi? Icu = NativeIcuBidi.Load();
    public static bool IsAvailable => Icu is not null;

    public static TerminalVisualLine Render(string logical)
    {
        ArgumentNullException.ThrowIfNull(logical);
        if (logical.Length == 0) return new("", false, [0]);
        if (!logical.Any(c => c is >= '\u0590' and <= '\u08ff' or >= '\ufb1d' and <= '\ufeff' or >= '\u202a' and <= '\u202e' or >= '\u2066' and <= '\u2069'))
            return Compose(logical, logical, Enumerable.Range(0, logical.Length).ToArray(), new byte[logical.Length], false);
        var icu = Icu ?? throw new PlatformNotSupportedException("Native ICU is required for correct bidirectional terminal text. Install the operating system ICU runtime.");
        var bidi = icu.Create();
        if (bidi == IntPtr.Zero) throw new OutOfMemoryException("ICU bidi allocation failed.");
        var buffer = Marshal.StringToHGlobalUni(logical);
        try
        {
            var error = 0;
            icu.Set(bidi, buffer, logical.Length, 0xfe, IntPtr.Zero, ref error);
            Check(error);
            var map = new int[logical.Length]; icu.Map(bidi, map, ref error); Check(error);
            var levels = Enumerable.Range(0, logical.Length).Select(i => icu.GetLevel(bidi, i)).ToArray();
            return Compose(logical, Shape(logical, icu), map, levels, (icu.GetParaLevel(bidi) & 1) != 0);
        }
        finally { icu.Dispose(bidi); Marshal.FreeHGlobal(buffer); }
    }

    private static string Shape(string logical, NativeIcuBidi icu)
    {
        // ICU contextual shaping, logical order, fixed size. A joining control between
        // lam and alef avoids the optional two-to-one ligature, retaining cursor indices.
        var expanded = new StringBuilder(); var inserted = new HashSet<int>();
        for (var i = 0; i < logical.Length; i++)
        {
            expanded.Append(logical[i]);
            if (logical[i] != '\u0644') continue;
            var next = i + 1;
            while (next < logical.Length && CharUnicodeInfo.GetUnicodeCategory(logical, next) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark) next++;
            if (next < logical.Length && logical[next] is '\u0622' or '\u0623' or '\u0625' or '\u0627') { inserted.Add(expanded.Length); expanded.Append('\u200d'); }
        }
        var input = expanded.ToString();
        var source = Marshal.StringToHGlobalUni(input); var destination = Marshal.AllocHGlobal((input.Length + 1) * 2);
        try
        {
            var error = 0; var length = icu.ShapeArabic(source, input.Length, destination, input.Length + 1, 8u | 1u, ref error); Check(error);
            if (length != input.Length) throw new InvalidOperationException("ICU Arabic shaping changed the fixed-length text.");
            var shaped = Marshal.PtrToStringUni(destination, length)!; var result = new StringBuilder(logical.Length);
            for (var i = 0; i < shaped.Length; i++) if (!inserted.Contains(i)) result.Append(CharUnicodeInfo.GetUnicodeCategory(input, i) is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark ? input[i] : shaped[i]);
            return result.ToString();
        }
        finally { Marshal.FreeHGlobal(destination); Marshal.FreeHGlobal(source); }
    }

    private static TerminalVisualLine Compose(string logical, string shaped, int[] map, byte[] levels, bool rtl)
    {
        var starts = StringInfo.ParseCombiningCharacters(logical);
        var clusters = starts.Select((start, index) => (Start: start, Length: (index + 1 < starts.Length ? starts[index + 1] : logical.Length) - start))
            .OrderBy(cluster => map.Skip(cluster.Start).Take(cluster.Length).Min()).ToArray();
        var output = new StringBuilder(); var carets = new int[logical.Length + 1]; var column = 0;
        foreach (var cluster in clusters)
        {
            var text = shaped.Substring(cluster.Start, cluster.Length);
            var odd = (levels[cluster.Start] & 1) != 0;
            if (odd && Icu is not null) text = string.Concat(text.EnumerateRunes().Select(rune => new Rune(Icu.GetMirror(rune.Value)).ToString()));
            var width = TerminalSurface.CellWidth(logical.Substring(cluster.Start, cluster.Length));
            output.Append(text);
            for (var i = cluster.Start; i < cluster.Start + cluster.Length; i++) carets[i] = odd ? column + width : column;
            carets[cluster.Start + cluster.Length] = odd ? column : column + width;
            column += width;
        }
        return new(output.ToString(), rtl, carets);
    }

    private static void Check(int error) { if (error > 0) throw new InvalidOperationException($"ICU text layout failed ({error})."); }
}
