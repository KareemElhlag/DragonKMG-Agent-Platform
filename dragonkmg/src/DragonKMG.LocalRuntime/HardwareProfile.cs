using System.Runtime.InteropServices;

namespace DragonKMG.LocalRuntime;

public sealed record HardwareProfile(string OperatingSystem, string Architecture, int LogicalProcessors, long AvailableMemoryBytes, bool GpuDetected);

public static class HardwareProfiler
{
    public static HardwareProfile Probe()
    {
        var memory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return new HardwareProfile(RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture.ToString(), Environment.ProcessorCount, memory > 0 ? memory : 0, false);
    }
}
