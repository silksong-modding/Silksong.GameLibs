using _build;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using System.Collections.Generic;

class Build : NukeBuild
{
    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    public static int Main() => Execute<Build>(x => x.Package);

    [Parameter("Target Silksong versions", ValueProviderType = typeof(SilksongVersionInfo), ValueProviderMember = nameof(SilksongVersionInfo.AllVersionStrings))]
    readonly SilksongVersionInfo[] TargetSilksongVersions;

    readonly AbsolutePath BinDir = RootDirectory / "bin";
    readonly AbsolutePath ObjDir = RootDirectory / "obj";

    Target Clean => _ => _
        .Before(Package)
        .Executes(() =>
        {
            BinDir.CreateOrCleanDirectory();
            ObjDir.CreateOrCleanDirectory();
        });

    Target Package => _ => _
        .Executes(() =>
        {
            IEnumerable<SilksongVersionInfo> targetVersions = TargetSilksongVersions != null && TargetSilksongVersions.Length > 0
                ? TargetSilksongVersions
                : SilksongVersionInfo.AllVersions;
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

}
