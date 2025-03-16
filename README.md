## NuGet Push

Windows Forms tool to package projects and upload them to a NuGet package source.

### How to use

This tool requires that you have a local package source defined in your nuget.config file.
The local package source will contain your .nupkg files, and allows you to pack projects with dependencies without first having to upload those dependencies.
More information about the nuget.config file: https://learn.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior

When you open a solution, you must first choose a local and optionally a remote package source.
This choice will be remembered per solution in the packagesources.json file.

When opening the solution, you will see a list of class library projects, each showing up to three versions:
- Version: the version in the .csproj file.
- Local version: the latest version found in the .nupkg files in your local package source.
- Remote version: the latest version found in the remote package source.

If the remote package source requires authentication, a popup will be displayed where you must input your username and API key.

Sometimes a project will show the status 'NotReady', this is usually caused by the .csproj containing project references, or lacking a version property.
It is recommended to use the following pattern in your projects:
```xml
<Project>
  <ItemGroup Condition="'$(IsPublishBuild)'!='true'">
    <ProjectReference Include="" />
  </ItemGroup>
  <ItemGroup Condition="'$(IsPublishBuild)'=='true'">
    <PackageReference Include="" Version="" />
  </ItemGroup>
</Project>
```
The IsPublishBuild parameter will be defined as 'true' when parsing the .csproj file, and when running 'dotnet pack'.

The project list supports multiple selection and has options to pack and/or push one or more projects.
The pack option will build the project and save the resulting .nupkg (and .snupkg) file(s) in your local package source.
The push option will upload the latest version in your local package source to the remote package source.
To prevent accidents, the push option will not work for projects with no (known) version in the remote package source if multiple projects are selected.

This tool uses MSBuild to parse the .sln and .csproj files, which requires you to have the .NET SDK installed.
Starting with .NET 8.0, .sln and .slnx files are parsed with Microsoft.VisualStudio.SolutionPersistence.

If the remote package source is an azure artifacts feed, you must install the following NuGet plugin: https://github.com/microsoft/artifacts-credprovider
