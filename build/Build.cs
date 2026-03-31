using _build;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Serilog;
using System.Collections.Generic;
using System.Linq;

class Build : NukeBuild
{
    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    [GitRepository]
    readonly GitRepository Repository;

    public static int Main() => Execute<Build>(x => x.Package);

    [Parameter("Target Silksong versions",
        ValueProviderType = typeof(SilksongVersionInfo),
        ValueProviderMember = nameof(SilksongVersionInfo.AllVersionStrings))]
    readonly SilksongVersionInfo[] TargetSilksongVersions;

    [Parameter("Whether to treat the build as a dryrun. Actions will be logged but not performed.")]
    readonly bool DryRun;

    [Secret, Parameter("The NuGet API key to use for publishing")]
    readonly string NuGetApiKey;

    readonly AbsolutePath BinDir = RootDirectory / "bin";
    readonly AbsolutePath ObjDir = RootDirectory / "obj";

    Target Clean => _ => _
        .Before(Package, Publish)
        .Executes(() =>
        {
            BinDir.CreateOrCleanDirectory();
            ObjDir.CreateOrCleanDirectory();
        });

    Target Package => _ => _
        .Executes(() =>
        {
            IEnumerable<SilksongVersionInfo> targetVersions = GetTargetVersions();
            Log.Information("Packaging for target versions {Versions}", string.Join(", ", targetVersions));
            if (DryRun)
            {
                Log.Information("Skipping build in dryrun mode");
                return;
            }

            DotNetTasks.DotNetPack(_ => _
                .SetProject(Solution.Silksong_GameLibs.RelativePath)
                .SetConfiguration("Release")
                .CombineWith(targetVersions, (_, v) => _
                    .SetProperty("BaseIntermediateOutputPath", $"{ObjDir / v}/")
                    .SetProperty("BaseOutputPath", $"{BinDir / v}/")
                    .SetProperty("TargetSilksongVersion", v)),
                degreeOfParallelism: 8
            );
        });

    Target Publish => _ => _
        .DependsOn(Clean, Package)
        .Requires(() => NuGetApiKey)
        .Executes(() =>
        {
            Assert.True(GitTasks.GitHasCleanWorkingCopy(), "Error: You have uncommitted changes. Please commit or stash them before publishing.");
            Assert.True(Repository.IsOnMainBranch(), $"Error: You must be on the main branch to publish. Current branch: {Repository.Branch}");

            GitTasks.Git("fetch origin main");
            GitTasks.Git("diff --quiet HEAD origin/main", exitHandler: p =>
            {
                if (p.ExitCode != 0)
                {
                    Assert.Fail("Error: Local main branch is not up to date with origin/main. Please pull/rebase first.");
                }
                return null;
            });

            IEnumerable<SilksongVersionInfo> targetVersions = GetTargetVersions();
            Log.Information("On main branch, up to date with origin/main, and no uncommitted changes.");
            Log.Information("Publishing for target versions {Versions}", string.Join(", ", targetVersions));

            if (DryRun)
            {
                Log.Information("Skipping publish in dryrun mode");
                return;
            }
            IEnumerable<AbsolutePath> packages = targetVersions.Select(v => BinDir / v).SelectMany(x => x.GlobFiles("**/*.nupkg"));
            DotNetTasks.DotNetNuGetPush(_ => _
                .SetSource("https://api.nuget.org/v3/index.json")
                .SetApiKey(NuGetApiKey)
                .SetSkipDuplicate(true)
                .CombineWith(packages, (_, p) => _
                    .SetTargetPath(p)),
                degreeOfParallelism: 8
            );
        });

    private IEnumerable<SilksongVersionInfo> GetTargetVersions()
    {
        return TargetSilksongVersions != null && TargetSilksongVersions.Length > 0
            ? TargetSilksongVersions
            : SilksongVersionInfo.AllVersions;
    }
}
