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

Task("Nuget-pack")
  .IsDependentOn("Nuget-Restore")
  .Does(()=>{
    var version = "0.1.0";
    if(HasEnvironmentVariable("TAG_NAME")){
      version = EnvironmentVariable("TAG_NAME");
    }
    var settings = new DotNetCorePackSettings
     {
        Configuration = "Release",
        OutputDirectory = "./nupkgs/",
        MSBuildSettings = new DotNetCoreMSBuildSettings().WithProperty("PackageVersion", version),

     };

     DotNetCorePack("./", settings);
  });

Task("Nuget-push")
.IsDependentOn("Nuget-pack")
.Does(()=>{
  if(!HasEnvironmentVariable("TAG_NAME")){
    return;
  }
   // Get the paths to the packages.
 var packages = GetFiles("./nupkgs/*.nupkg");

 // Push the package.
 NuGetPush(packages, new NuGetPushSettings {
     Source = "https://api.nuget.org/v3/index.json",
     ApiKey =  EnvironmentVariable("NugetAPIKey")
 });
});

RunTarget(target);