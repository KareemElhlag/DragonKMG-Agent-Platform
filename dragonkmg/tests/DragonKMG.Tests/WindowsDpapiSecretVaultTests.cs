using DragonKMG.Infrastructure;

namespace DragonKMG.Tests;

public sealed class WindowsDpapiSecretVaultTests
{
    [Fact]
    public void StoresAndReadsASecretForTheCurrentWindowsUser()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), $"dragonkmg-vault-{Guid.NewGuid():N}");
        try
        {
            var vault = new WindowsDpapiSecretVault(directory);
            vault.Store("provider_token", "test-only-value");
            Assert.Equal("test-only-value", vault.Read("provider_token"));
            Assert.Null(vault.Read("missing"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
