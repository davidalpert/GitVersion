using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using GitVersion;
using GitVersionCore.Tests;
using NUnit.Framework;

[TestFixture]
public class NugetCompatibilityTests
{
    [TestCase("2.1.0", VersioningMode.ContinuousDelivery)]
    [TestCase("2.1.0", VersioningMode.ContinuousDeployment)]
    [TestCase("2.8.2", VersioningMode.ContinuousDelivery)]
    [TestCase("2.8.2", VersioningMode.ContinuousDeployment)]
    public void NuGetVersionV2_is_compatible_with_NuGet_version_(string nugetProductVersion, VersioningMode versioningMode)
    {
        var semVer = new SemanticVersion
        {
            Major = 1,
            Minor = 2,
            Patch = 3,
            PreReleaseTag = "pr",
            BuildMetaData = "5.Branch.develop"
        };
        semVer.BuildMetaData.Branch = "pull/2/merge";
        semVer.BuildMetaData.Sha = "commitSha";
        semVer.BuildMetaData.CommitDate = DateTimeOffset.Parse("2014-03-06 23:59:59Z");

        var config = new TestEffectiveConfiguration(versioningMode: versioningMode, tagNumberPattern: @"[/-](?<number>\d+)[-/]");
        var vars = VariableProvider.GetVariablesFor(semVer, config, false);

        var thisAssemblyFileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
        var compatibilityDirectoryInfo = new DirectoryInfo(Path.Combine(thisAssemblyFileInfo.DirectoryName, "NugetCompatibility"));
        var nugetExeFileInfo = new FileInfo(Path.Combine(compatibilityDirectoryInfo.FullName, nugetProductVersion, "nuget.exe"));

        var samplePackageNuspecFileInfo = new FileInfo(Path.Combine(compatibilityDirectoryInfo.FullName, "SamplePackage.nuspec"));

        var expectedNugetPackageFileInfo = new FileInfo(Path.Combine(compatibilityDirectoryInfo.FullName, "SamplePackage." + vars.NuGetVersionV2 + ".nupkg"));
        RemoveFileIfNeeded(expectedNugetPackageFileInfo);

        var p = RunNugetPack(nugetExeFileInfo, samplePackageNuspecFileInfo, vars.NuGetVersionV2, compatibilityDirectoryInfo);
        Assert.AreEqual(0, p.ExitCode);

        expectedNugetPackageFileInfo.Refresh(); // apparently this is needed to make sure Exists is accurate to the current state
        Assert.True(expectedNugetPackageFileInfo.Exists, "cannot find: {0}", expectedNugetPackageFileInfo.FullName);

        RemoveFileIfNeeded(expectedNugetPackageFileInfo);
    }

    private static Process RunNugetPack(FileInfo nugetExeFileInfo, FileInfo samplePackageNuspecFileInfo, string packageVersion, DirectoryInfo compatibilityDirectoryInfo)
    {
        Assert.True(nugetExeFileInfo.Exists, "Did not find expected file at: {0}", nugetExeFileInfo.FullName);
        var psi = new ProcessStartInfo(nugetExeFileInfo.FullName)
        {
            Arguments = string.Format(@"pack ""{0}"" -version {1} -outputdirectory ""{2}""", samplePackageNuspecFileInfo.FullName, packageVersion, compatibilityDirectoryInfo.FullName)
        };

        Console.WriteLine(@"""{0}"" {1}", nugetExeFileInfo.FullName, psi.Arguments);

        var p = Process.Start(psi);

        p.WaitForExit();

        return p;
    }

    private static void RemoveFileIfNeeded(FileSystemInfo fileToRemove)
    {
        if (fileToRemove.Exists)
        {
            fileToRemove.Delete();
            fileToRemove.Refresh(); // apparently this is needed to make sure Exists is accurate to the current state
        }
        Assert.IsFalse(fileToRemove.Exists, "Should have removed: {0}", fileToRemove.FullName);
    }
}