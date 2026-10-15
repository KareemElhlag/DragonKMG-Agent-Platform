using System.Security.Cryptography;
using System.Text;
using DragonKMG.Application;

namespace DragonKMG.Infrastructure;

public sealed class WindowsDpapiSecretVault(string vaultDirectory)
{
    private readonly string _vaultDirectory = Path.GetFullPath(vaultDirectory);

    public void Store(string name, string value)
    {
        ValidateName(name);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows DPAPI vault is only available on Windows.");
        Directory.CreateDirectory(_vaultDirectory);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(Path.Combine(_vaultDirectory, $"{name}.dpapi"), encrypted);
    }

    public string? Read(string name)
    {
        ValidateName(name);
        if (!OperatingSystem.IsWindows()) return null;
        var path = Path.Combine(_vaultDirectory, $"{name}.dpapi");
        if (!File.Exists(path)) return null;
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser));
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_')) throw new ArgumentException("Vault names may contain letters, digits, hyphens, and underscores only.", nameof(name));
    }
}

public sealed class CompositeSecretResolver(WindowsDpapiSecretVault vault) : ISecretResolver
{
    public string? Resolve(string secretReference) => secretReference.StartsWith("vault:", StringComparison.OrdinalIgnoreCase)
        ? vault.Read(secretReference[6..])
        : secretReference.StartsWith("env:", StringComparison.OrdinalIgnoreCase) ? Environment.GetEnvironmentVariable(secretReference[4..]) : null;
}
