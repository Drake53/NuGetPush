// ------------------------------------------------------------------------------
// <copyright file="DotNet.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

            var sdks = new List<DotNetSdk>();
            var prefix = $"{Environment.Version.ToString(2)}.";

            while (true)
            {
                var line = await dotnetListSdksProcess.StandardOutput.ReadLineAsync();
                if (line is null)
                {
                    break;
                }

                if (line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    sdks.Add(DotNetSdk.Parse(line));
                }
            }

            if (sdks.Count == 0)
            {
                throw new InvalidOperationException($"No .NET SDK found which matches the current runtime version '{Environment.Version}'.");
            }

            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", sdks.OrderByDescending(sdk => sdk.SdkVersion).First().MSBuildPath);
        }

        public static async Task<bool> PackAsync(ClassLibrary project)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                FileName = ProcessName,
                Arguments = $"pack \"{project.ProjectPath}\" -nologo -c Release -verbosity:quiet /p:IsPublishBuild=true /p:GeneratePackageOnBuild=false",
            };

            using var dotnetPackProcess = Process.Start(processStartInfo);
            await dotnetPackProcess.WaitForExitAsync();

            project.Diagnostics.Add(await dotnetPackProcess.StandardOutput.ReadToEndAsync());

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