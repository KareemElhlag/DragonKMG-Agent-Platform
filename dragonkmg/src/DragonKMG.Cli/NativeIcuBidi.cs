using System.Runtime.InteropServices;

namespace DragonKMG.Cli;

// ICU C ABI: https://unicode-org.github.io/icu-docs/apidoc/released/icu4c/ubidi_8h.html
// The library stays loaded for the process lifetime because delegates retain native addresses.
internal sealed class NativeIcuBidi
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr Open();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void Close(IntPtr bidi);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void SetPara(IntPtr bidi, IntPtr text, int length, byte level, IntPtr levels, ref int error);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LogicalMap(IntPtr bidi, [Out] int[] map, ref int error);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate byte Level(IntPtr bidi, int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate byte ParaLevel(IntPtr bidi);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Shape(IntPtr source, int length, IntPtr destination, int capacity, uint options, ref int error);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Mirror(int scalar);
    internal readonly Open Create;
    internal readonly Close Dispose;
    internal readonly SetPara Set;
    internal readonly LogicalMap Map;
    internal readonly Level GetLevel;
    internal readonly ParaLevel GetParaLevel;
    internal readonly Shape ShapeArabic;
    internal readonly Mirror GetMirror;
    private NativeIcuBidi(IntPtr handle, string suffix)
    {
        T Resolve<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(handle, name + suffix));
        Create = Resolve<Open>("ubidi_open"); Dispose = Resolve<Close>("ubidi_close"); Set = Resolve<SetPara>("ubidi_setPara");
        Map = Resolve<LogicalMap>("ubidi_getLogicalMap"); GetLevel = Resolve<Level>("ubidi_getLevelAt"); GetParaLevel = Resolve<ParaLevel>("ubidi_getParaLevel");
        ShapeArabic = Resolve<Shape>("u_shapeArabic"); GetMirror = Resolve<Mirror>("u_charMirror");
    }
    internal static NativeIcuBidi? Load()
    {
        var names = OperatingSystem.IsWindows() ? new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "icu.dll") }
            : OperatingSystem.IsMacOS() ? new[] { "/usr/lib/libicucore.A.dylib" }
            : new[] { "libicuuc.so" }.Concat(Enumerable.Range(60, 40).Reverse().Select(v => $"libicuuc.so.{v}")).ToArray();
        foreach (var name in names)
        {
            if (!NativeLibrary.TryLoad(name, out var handle)) continue;
            foreach (var suffix in new[] { "" }.Concat(Enumerable.Range(60, 40).Reverse().Select(v => $"_{v}")))
                if (NativeLibrary.TryGetExport(handle, "ubidi_open" + suffix, out _))
                {
                    try { return new NativeIcuBidi(handle, suffix); }
                    catch (EntryPointNotFoundException) { break; }
                }
            NativeLibrary.Free(handle);
        }
        return null;
    }
}
