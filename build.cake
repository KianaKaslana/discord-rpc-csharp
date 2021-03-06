// Addins
#addin nuget:?package=Cake.FileHelpers
#addin nuget:?package=Cake.Git
#addin nuget:?package=Cake.VersionReader

// Adjustable Variables
var projectName = "Discord RPC";

// Arguments
var target = Argument ("target", "Default");
var buildType = Argument<string>("buildType", "Release");
var buildCounter = Argument<int>("buildCounter", 0);

// Project Variables
var sln = string.Format("./{0}/{0}.sln", projectName);
var releaseFolder = string.Format("./{0}/bin/{1}", projectName, buildType);
var releaseDll = string.Format("/{0}.dll", projectName);
var nuspecFile = string.Format("./{0}/{0}.nuspec", projectName);

// Execution Variables
var version = "0.0.0";
var ciVersion = "0.0.0-CI00000";
var runningOnTeamCity = false;
var runningOnAppVeyor = false;

// Find out if we are running on a Build Server
Task ("DiscoverBuildDetails")
	.Does (() =>
	{
		runningOnTeamCity = TeamCity.IsRunningOnTeamCity;
		Information("Running on TeamCity: " + runningOnTeamCity);
		runningOnAppVeyor = AppVeyor.IsRunningOnAppVeyor;
		Information("Running on AppVeyor: " + runningOnAppVeyor);
	});

// Outputs Argument values so they are visible in the build log
Task ("OutputVariables")
	.Does (() =>
	{
		Information("BuildType: " + buildType);
		Information("BuildCounter: " + buildCounter);
	});

// Builds the code
Task ("Build")
	.Does (() => {
		MSBuild (sln, new MSBuildSettings 
						{
							Verbosity = Verbosity.Quiet,
							Configuration = buildType
						});
		var file = MakeAbsolute(Directory(releaseFolder)) + releaseDll;
		version = GetVersionNumber(file);
		ciVersion = GetVersionNumberWithContinuesIntegrationNumberAppended(file, buildCounter);
		Information("Version: " + version);
		Information("CI Version: " + ciVersion);
		PushVersion(ciVersion);
	});

// Create Nuget Package
Task ("Nuget")
    .WithCriteria (buildType == "Release")
	.Does (() => {
		CreateDirectory ("./nupkg/");
		ReplaceRegexInFiles(nuspecFile, "0.0.0", version);
		
		NuGetPack (nuspecFile, new NuGetPackSettings { 
			Verbosity = NuGetVerbosity.Detailed,
			OutputDirectory = "./nupkg/"
		});	
	});

// Restore Nuget packages
Task ("NugetRestore")
	.Does (() => {
		var blockText = "Nuget Restore";
		StartBlock(blockText);
		NuGetRestore(sln);
		EndBlock(blockText);
	});

//Push to Nuget
Task ("Push")
	.WithCriteria (buildType == "Release")
	.Does (() => {
		// Get the newest (by last write time) to publish
		var newestNupkg = GetFiles ("nupkg/*.nupkg")
			.OrderBy (f => new System.IO.FileInfo (f.FullPath).LastWriteTimeUtc)
			.LastOrDefault();
		var apiKey = EnvironmentVariable("NugetKey");
		NuGetPush (newestNupkg, new NuGetPushSettings { 
			Verbosity = NuGetVerbosity.Detailed,
			Source = "https://www.nuget.org/api/v2/package/",
			ApiKey = apiKey
		});
	});

Task ("Default")
	.IsDependentOn ("OutputVariables")
	.IsDependentOn ("DiscoverBuildDetails")
	.IsDependentOn ("NugetRestore")
	.IsDependentOn ("Build");
Task ("NugetBuild")
	.IsDependentOn ("OutputVariables")
	.IsDependentOn ("DiscoverBuildDetails")
	.IsDependentOn ("NugetRestore")
	.IsDependentOn ("Build")
    .IsDependentOn ("Nuget")
    .IsDependentOn ("Push");
    
RunTarget (target);

// Code to start a TeamCity log block
public void StartBlock(string blockName)
{
		if(runningOnTeamCity)
		{
			TeamCity.WriteStartBlock(blockName);
		}
}

// Code to start a TeamCity build block
public void StartBuildBlock(string blockName)
{
	if(runningOnTeamCity)
	{
		TeamCity.WriteStartBuildBlock(blockName);
	}
}

// Code to end a TeamCity log block
public void EndBlock(string blockName)
{
	if(runningOnTeamCity)
	{
		TeamCity.WriteEndBlock(blockName);
	}
}

// Code to end a TeamCity build block
public void EndBuildBlock(string blockName)
{
	if(runningOnTeamCity)
	{
		TeamCity.WriteEndBuildBlock(blockName);
	}
}

// Code to push the Version number to the build system
public void PushVersion(string version)
{
	if(runningOnTeamCity)
	{
		TeamCity.SetBuildNumber(version);
	}
	if(runningOnAppVeyor)
	{
		Information("Pushing version to AppVeyor: " + version);
		AppVeyor.UpdateBuildVersion(version);
	}
}

// Code to push the Test Results to AppVeyor for display purposess
public void PushTestResults(string filePath)
{
	var file = MakeAbsolute(File(filePath));
	if(runningOnAppVeyor)
	{
		AppVeyor.UploadTestResults(file, AppVeyorTestResultsType.NUnit3);
	}
}