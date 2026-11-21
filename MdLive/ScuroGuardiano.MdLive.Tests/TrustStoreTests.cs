using ScuroGuardiano.MdLive.Scripting;

namespace ScuroGuardiano.MdLive.Tests;

public class TrustStoreTests
{
    [Fact]
    public void NewConfig_IsUntrusted()
    {
        using var project = new TestProject();
        project.Write("config.csx", "");

        Assert.Equal(TrustStatus.Untrusted, project.TrustStore.GetState(project.ReadRequired()).Status);
    }

    [Fact]
    public void TrustedConfig_IsTrustedUntilAnythingChanges()
    {
        using var project = new TestProject();
        project.Write("config.csx", "#load \"a.csx\"\n");
        project.Write("a.csx", "// a");
        project.TrustCurrent();

        Assert.Equal(TrustStatus.Trusted, project.TrustStore.GetState(project.ReadRequired()).Status);

        project.Write("a.csx", "// changed");

        Assert.Equal(TrustStatus.Changed, project.TrustStore.GetState(project.ReadRequired()).Status);
    }

    [Fact]
    public void Revoke_RemovesTrust()
    {
        using var project = new TestProject();
        project.Write("config.csx", "");
        project.TrustCurrent();

        Assert.True(project.TrustStore.Revoke(project.ReadRequired().ConfigDirectory));
        Assert.Equal(TrustStatus.Untrusted, project.TrustStore.GetState(project.ReadRequired()).Status);
    }

    [Fact]
    public void TrustIsBoundToDirectory_SameContentElsewhereIsUntrusted()
    {
        using var trusted = new TestProject();
        trusted.Write("config.csx", "// same");
        trusted.TrustCurrent();

        // Kopia tego samego configu w innym katalogu (np. sklonowane obce repo) nie dziedziczy zaufania
        using var copy = new TestProject();
        copy.Write("config.csx", "// same");

        Assert.Equal(trusted.ReadRequired().Hash, copy.ReadRequired().Hash);
        Assert.Equal(TrustStatus.Untrusted, trusted.TrustStore.GetState(copy.ReadRequired()).Status);
    }

    [Fact]
    public void StoreFile_IsPrivate()
    {
        if (OperatingSystem.IsWindows()) return; // uprawnienia Unixowe

        using var project = new TestProject();
        project.Write("config.csx", "");
        project.TrustCurrent();

        var file = project.TrustStore.FilePath;
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(Path.GetDirectoryName(file)!));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(file)!, "*.tmp"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StoreWritableByOthers_IsIgnored(bool directory)
    {
        if (OperatingSystem.IsWindows()) return; // uprawnienia Unixowe

        using var project = new TestProject();
        project.Write("config.csx", "");
        project.TrustCurrent();

        var path = directory ? Path.GetDirectoryName(project.TrustStore.FilePath)! : project.TrustStore.FilePath;
        File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.GroupWrite);

        var state = project.TrustStore.GetState(project.ReadRequired());

        Assert.Equal(TrustStatus.StoreRejected, state.Status);
        Assert.Contains("writable by group or others", state.Problem);
        Assert.Throws<InvalidOperationException>(project.TrustCurrent);
    }

    [Fact]
    public void CorruptedStore_TrustsNothing()
    {
        using var project = new TestProject();
        project.Write("config.csx", "");
        project.TrustCurrent();
        File.WriteAllText(project.TrustStore.FilePath, "{ not json");

        Assert.Equal(TrustStatus.StoreRejected, project.TrustStore.GetState(project.ReadRequired()).Status);
    }
}
