using ScuroGuardiano.MdLive.Commands;

namespace ScuroGuardiano.MdLive.Tests;

public class ServeArgumentsTests
{
    [Theory]
    [InlineData(new[] { "--AllowedHosts=example.test" }, "--AllowedHosts=example.test")]
    [InlineData(new[] { "--AllowedHosts", "example.test" }, "--AllowedHosts=example.test")]
    [InlineData(new[] { "--allowedhosts=*" }, "--allowedhosts=*")]
    [InlineData(new[] { "--Logging:LogLevel:Default", "Debug" }, "--Logging:LogLevel:Default=Debug")]
    [InlineData(new[] { "--logging:loglevel:Microsoft=Warning" }, "--logging:loglevel:Microsoft=Warning")]
    public void KnownConfigurationOptions_AreNormalizedToKeyValue(string[] args, string expected)
    {
        var parsed = ServeArguments.Parse(args, out var errors);

        Assert.Empty(errors);
        Assert.Equal([expected], parsed.ConfigurationArgs);
    }

    [Theory]
    [InlineData("--urls=http://0.0.0.0:80", "unknown option '--urls'")]
    [InlineData("--Kestrel:Endpoints:Pwn:Url=http://0.0.0.0:1", "unknown option '--Kestrel:Endpoints:Pwn:Url'")]
    [InlineData("--trusted=/tmp", "unknown option '--trusted'")]
    [InlineData("--Logging:", "unknown option '--Logging:'")]
    [InlineData("--Logging=x", "unknown option '--Logging'")]
    [InlineData("/AllowedHosts=*", "unexpected argument '/AllowedHosts=*'")]
    [InlineData("AllowedHosts=*", "unexpected argument 'AllowedHosts=*'")]
    public void EverythingElse_IsRejected(string arg, string expected)
    {
        var parsed = ServeArguments.Parse([arg], out var errors);

        Assert.Equal([expected], errors);
        Assert.Empty(parsed.ConfigurationArgs);
    }

    [Theory]
    [InlineData(new[] { "--AllowedHosts" }, "--AllowedHosts needs a value")]
    [InlineData(new[] { "--AllowedHosts", "--trust", "/" }, "--AllowedHosts needs a value")]
    [InlineData(new[] { "--trust", "--AllowedHosts=x" }, "--trust needs a directory")]
    public void MissingValues_AreReported_WithoutEatingTheNextOption(string[] args, string expected)
    {
        ServeArguments.Parse(args, out var errors);

        Assert.Contains(expected, errors);
    }

    [Fact]
    public void AllErrorsAreReported()
    {
        ServeArguments.Parse(["--foo", "bar", "--trust", "/definitely/not/here"], out var errors);

        Assert.Equal(3, errors.Count);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help(string arg)
    {
        Assert.True(ServeArguments.Parse([arg], out var errors).ShowHelp);
        Assert.Empty(errors);
    }
}
