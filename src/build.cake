var target = Argument("target", "Build");
var configuration = Argument("configuration", "Release");
var msbuildVerbosity = Argument("msbuild-verbosity", "Minimal");
var coverage = Argument("coverage", "false");

void SetMSBuildConfiguration(MSBuildSettings settings){
  settings.Configuration = configuration;
  settings.Verbosity = (Verbosity)Enum.Parse(typeof(Verbosity), msbuildVerbosity);
}

Task("Nuget-Restore")
  .Does(() =>
{
  NuGetRestore(".");
  DotNetCoreRestore();
});
Task("Build")
  .IsDependentOn("Nuget-Restore")
  .Does(() =>
{
  MSBuild(".", SetMSBuildConfiguration);
});

Task("Test")
  .IsDependentOn("Nuget-Restore")
  .Does(() =>
{
  var settings = new DotNetCoreTestSettings
     {
         Configuration = configuration,
         NoRestore = true,
     };
      var projectFiles = GetFiles("./**/*Test*.csproj");
      foreach (var file in projectFiles)
      {
        DotNetCoreTest(file.FullPath, settings);          
      }
});

RunTarget(target);