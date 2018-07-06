#module "Cake.Longpath.Module"

#addin "Cake.FileHelpers"
#addin "Cake.Powershell"

using System;
using System.Linq;
using System.Text.RegularExpressions;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

//////////////////////////////////////////////////////////////////////
// VERSIONS
//////////////////////////////////////////////////////////////////////

var gitVersioningVersion = "2.1.23";
var signClientVersion = "0.9.0";
var inheritDocVersion = "1.1.1.1";

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var baseDir = MakeAbsolute(Directory("../")).ToString();
var buildDir = baseDir + "/build";
var Solution = baseDir + "/Windows Community Toolkit.sln";
var win32Solution = baseDir + "/Microsoft.Toolkit.Win32/Microsoft.Toolkit.Win32.sln";
var toolsDir = buildDir + "/tools";

var binDir = baseDir + "/bin";
var nupkgDir = binDir + "/nupkg";

var signClientSettings = MakeAbsolute(File("SignClientSettings.json")).ToString();
var signClientSecret = EnvironmentVariable("SignClientSecret");
var signClientUser = EnvironmentVariable("SignClientUser");
var signClientAppPath = toolsDir + "/SignClient/Tools/netcoreapp2.0/SignClient.dll";

var styler = toolsDir + "/XamlStyler.Console/tools/xstyler.exe";
var stylerFile = baseDir + "/settings.xamlstyler";

var versionClient = toolsDir + "/nerdbank.gitversioning/tools/Get-Version.ps1";
string Version = null;

var inheritDoc = toolsDir + "/InheritDoc/tools/InheritDoc.exe";
var inheritDocExclude = "Foo.*";

var name = "Windows Community Toolkit";
var address = "https://developer.microsoft.com/en-us/windows/uwp-community-toolkit";

//////////////////////////////////////////////////////////////////////
// METHODS
//////////////////////////////////////////////////////////////////////

void VerifyHeaders(bool Replace)
{
    var header = FileReadText("header.txt") + "\r\n";
    bool hasMissing = false;

    Func<IFileSystemInfo, bool> exclude_objDir =
        fileSystemInfo => !fileSystemInfo.Path.Segments.Contains("obj");

    var files = GetFiles(baseDir + "/**/*.cs", exclude_objDir).Where(file =>
    {
        var path = file.ToString();
        return !(path.EndsWith(".g.cs") || path.EndsWith(".i.cs") || System.IO.Path.GetFileName(path).Contains("TemporaryGeneratedFile"));
    });

    Information("\nChecking " + files.Count() + " file header(s)");
    foreach(var file in files)
    {
        var oldContent = FileReadText(file);
		if(oldContent.Contains("// <auto-generated>"))
		{
		   continue;
		}
        var rgx = new Regex("^(//.*\r?\n)*\r?\n");
        var newContent = header + rgx.Replace(oldContent, "");

        if(!newContent.Equals(oldContent, StringComparison.Ordinal))
        {
            if(Replace)
            {
                Information("\nUpdating " + file + " header...");
                FileWriteText(file, newContent);
            }
            else
            {
                Error("\nWrong/missing header on " + file);
                hasMissing = true;
            }
        }
    }

    if(!Replace && hasMissing)
    {
        throw new Exception("Please run UpdateHeaders.bat or '.\\build.ps1 -target=UpdateHeaders' and commit the changes.");
    }
}

//////////////////////////////////////////////////////////////////////
// DEFAULT TASK
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Clean the output folder")
    .Does(() =>
{
    if(DirectoryExists(binDir))
    {
        Information("\nCleaning Working Directory");
        CleanDirectory(binDir);
    }
    else
    {
        CreateDirectory(binDir);
    }
});

Task("Verify")
    .Description("Run pre-build verifications")
    .IsDependentOn("Clean")
    .Does(() =>
{
    // VerifyHeaders(false);
});

Task("Version")
    .Description("Updates the version information in all Projects")
    .IsDependentOn("Verify")
    .Does(() =>
{
    Information("\nDownloading NerdBank GitVersioning...");
    var installSettings = new NuGetInstallSettings {
        ExcludeVersion  = true,
        Version = gitVersioningVersion,
        OutputDirectory = toolsDir
    };

    NuGetInstall(new []{"nerdbank.gitversioning"}, installSettings);

    Information("\nRetrieving version...");
    var results = StartPowershellFile(versionClient);
    Version = results[1].Properties["NuGetPackageVersion"].Value.ToString();
    Information("\nBuild Version: " + Version);
});

Task("Build")
    .Description("Build all projects and get the assemblies")
    .IsDependentOn("Version")
    .Does(() =>
{
    Information("\nBuilding Solution");
    var buildSettings = new MSBuildSettings
    {
        MaxCpuCount = 0,
		MSBuildPlatform = MSBuildPlatform.x86
    }
    .SetConfiguration("Release")
    .WithTarget("Restore");

    MSBuild(Solution, buildSettings);
    MSBuild(win32Solution, buildSettings);

    EnsureDirectoryExists(nupkgDir);

	// Build once with normal dependency ordering
    buildSettings = new MSBuildSettings
    {
        MaxCpuCount = 0,
		MSBuildPlatform = MSBuildPlatform.x86
    }
    .SetConfiguration("Release")
    .WithTarget("Build")
    .WithProperty("GenerateLibraryLayout", "true");

    MSBuild(win32Solution, buildSettings);
	MSBuild(Solution, buildSettings);
});

Task("InheritDoc")
	.Description("Updates <inheritdoc /> tags from base classes, interfaces, and similar methods")
	.IsDependentOn("Build")
	.Does(() =>
{
	Information("\nDownloading InheritDoc...");
	var installSettings = new NuGetInstallSettings {
		ExcludeVersion = true,
        Version = inheritDocVersion,
		OutputDirectory = toolsDir
	};

	NuGetInstall(new []{"InheritDoc"}, installSettings);
    
    var args = new ProcessArgumentBuilder()
                .AppendSwitchQuoted("-b", baseDir)
                .AppendSwitch("-o", "")
                .AppendSwitchQuoted("-x", inheritDocExclude);

    var result = StartProcess(inheritDoc, new ProcessSettings { Arguments = args });
    
    if (result != 0)
    {
        throw new InvalidOperationException("InheritDoc failed!");
    }

    Information("\nFinished generating documentation with InheritDoc");
});

Task("Package")
	.Description("Pack the NuPkg")
	.IsDependentOn("InheritDoc")
	.Does(() =>
{
	// Invoke the pack target in the end
    var buildSettings = new MSBuildSettings {
        MaxCpuCount = 0,
		MSBuildPlatform = MSBuildPlatform.x86
    }
    .SetConfiguration("Release")
    .WithTarget("Pack")
    .WithProperty("GenerateLibraryLayout", "true")
	.WithProperty("PackageOutputPath", nupkgDir);

    MSBuild(Solution, buildSettings);
    MSBuild(win32Solution, buildSettings);

    // Build and pack C++ packages
    buildSettings = new MSBuildSettings
    {
        MaxCpuCount = 0,
		MSBuildPlatform = MSBuildPlatform.x86
    }
    .SetConfiguration("Native");

    buildSettings.SetPlatformTarget(PlatformTarget.ARM);
    MSBuild(Solution, buildSettings);

    buildSettings.SetPlatformTarget(PlatformTarget.x64);
    MSBuild(Solution, buildSettings);

    buildSettings.SetPlatformTarget(PlatformTarget.x86);
    MSBuild(Solution, buildSettings);

    var nuGetPackSettings = new NuGetPackSettings
	{
		OutputDirectory = nupkgDir,
        Version = Version
	};

    var nuspecs = GetFiles("./*.nuspec");
    foreach (var nuspec in nuspecs)
    {
        NuGetPack(nuspec, nuGetPackSettings);
    }
});

Task("SignNuGet")
    .Description("Sign the NuGet packages with the Code Signing service")
    .IsDependentOn("Package")
    .Does(() =>
{
    if(!string.IsNullOrWhiteSpace(signClientSecret))
    {
        Information("\nDownloading Sign Client...");
        var installSettings = new NuGetInstallSettings {
            ExcludeVersion  = true,
            OutputDirectory = toolsDir,
            Version = signClientVersion
        };
        NuGetInstall(new []{"SignClient"}, installSettings);

        var packages = GetFiles(nupkgDir + "/*.nupkg");
        Information("\n Signing " + packages.Count() + " Packages");
        foreach(var package in packages)
        {
            Information("\nSubmitting " + package + " for signing...");
            var arguments = new ProcessArgumentBuilder()
                .AppendQuoted(signClientAppPath)
                .Append("sign")
                .AppendSwitchQuotedSecret("-s", signClientSecret)
                .AppendSwitchQuotedSecret("-r", signClientUser)
                .AppendSwitchQuoted("-c", signClientSettings)
                .AppendSwitchQuoted("-i", MakeAbsolute(package).FullPath)
                .AppendSwitchQuoted("-n", name)
                .AppendSwitchQuoted("-d", name)
                .AppendSwitchQuoted("-u", address);

            // Execute Signing
            var result = StartProcess("dotnet", new ProcessSettings {  Arguments = arguments });
            if(result != 0)
            {
                throw new InvalidOperationException("Signing failed!");
            }

            Information("\nFinished signing " + package);
        }
    }
    else
    {
        Warning("\nClient Secret not found, not signing packages...");
    }
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Package");

Task("UpdateHeaders")
    .Description("Updates the headers in *.cs files")
    .Does(() =>
{
    VerifyHeaders(true);
});

Task("StyleXaml")
    .Description("Ensures XAML Formatting is Clean")
    .Does(() =>
{
    Information("\nDownloading XamlStyler...");
    var installSettings = new NuGetInstallSettings {
        ExcludeVersion  = true,
        OutputDirectory = toolsDir
    };

    NuGetInstall(new []{"xamlstyler.console"}, installSettings);

    Func<IFileSystemInfo, bool> exclude_objDir =
        fileSystemInfo => !fileSystemInfo.Path.Segments.Contains("obj");

    var files = GetFiles(baseDir + "/**/*.xaml", exclude_objDir);
    Information("\nChecking " + files.Count() + " file(s) for XAML Structure");
    foreach(var file in files)
    {
        StartProcess(styler, "-f \"" + file + "\" -c \"" + stylerFile + "\"");
    }
});



//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
