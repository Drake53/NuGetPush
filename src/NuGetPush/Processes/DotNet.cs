// ------------------------------------------------------------------------------
// <copyright file="DotNet.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using NuGetPush.Extensions;
using NuGetPush.Models;

namespace NuGetPush.Processes
{
    public static class DotNet
    {
        private const string ProcessName = "dotnet";

        static DotNet()
        {
            Environment.SetEnvironmentVariable(@"DOTNET_CLI_TELEMETRY_OPTOUT", "true");
        }

        // https://blog.rsuter.com/missing-sdk-when-using-the-microsoft-build-package-in-net-core/
        public static async Task SetMsBuildExePathAsync()
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                FileName = ProcessName,
                Arguments = "--list-sdks",
            };

            using var dotnetListSdksProcess = Process.Start(processStartInfo);
            await dotnetListSdksProcess.WaitForExitAsync();

            var output = await dotnetListSdksProcess.StandardOutput.ReadToEndAsync();
            var sdkPaths = Regex.Matches(output, "([0-9]+.[0-9]+.[0-9]+) \\[(.*)\\]")
                .OfType<Match>()
                .Select(match => Path.Combine(match.Groups[2].Value, match.Groups[1].Value, "MSBuild.dll"));

            var sdkPath = sdkPaths.Last();
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", sdkPath);
        }

        public static async Task<bool> PackAsync(ClassLibrary project)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                FileName = ProcessName,
                Arguments = $"pack \"{project.ProjectPath}\" -nologo -c Release -verbosity:quiet /p:IsPublishBuild=true /p:GeneratePackageOnBuild=false",
            };

            using var dotnetPackProcess = Process.Start(processStartInfo);
            await dotnetPackProcess.WaitForExitAsync();

            return dotnetPackProcess.ExitCode == 0;
        }

        public static async Task<bool> TestAsync(TestProject project)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                FileName = ProcessName,
                Arguments = $"test \"{project.ProjectPath}\" -nologo -c Release -verbosity:quiet",
            };

            using var dotnetTestProcess = Process.Start(processStartInfo);
            await dotnetTestProcess.WaitForExitAsync();

            return dotnetTestProcess.ExitCode == 0;
        }

        // https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package
        public static async Task<bool> PushAsync(string fileName, string nuGetApiKey, string nuGetSource, Action<string> deviceLoginCallback, CancellationToken cancellationToken = default)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                FileName = ProcessName,
                Arguments = $"nuget push \"{fileName}\" --api-key \"{nuGetApiKey}\" --source \"{nuGetSource}\" --interactive",
                RedirectStandardOutput = true,
            };

            using var dotnetPushProcess = Process.Start(processStartInfo);

            if (dotnetPushProcess.StandardOutput.TryReadDeviceLogin(out var deviceLoginLine))
            {
                deviceLoginCallback.Invoke(deviceLoginLine);
            }

            await dotnetPushProcess.WaitForExitAsync(cancellationToken);

            return dotnetPushProcess.ExitCode == 0;
        }
    }
}