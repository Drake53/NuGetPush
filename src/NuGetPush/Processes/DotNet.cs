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

        public static void SetMsBuildExePath(string msBuildPath)
        {
            Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", msBuildPath);
        }

        public static async Task SetMsBuildExePathAsync(CancellationToken cancellationToken)
        {
            SetMsBuildExePath(await GetMsBuildExePathAsync(cancellationToken));
        }

        // https://blog.rsuter.com/missing-sdk-when-using-the-microsoft-build-package-in-net-core/
        public static async Task<string> GetMsBuildExePathAsync(CancellationToken cancellationToken)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                FileName = ProcessName,
                Arguments = "--list-sdks",
            };

            using var dotnetListSdksProcess = Process.Start(processStartInfo);
            await dotnetListSdksProcess.WaitForExitAsync(cancellationToken);

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

            return sdks.OrderByDescending(sdk => sdk.SdkVersion).First().MSBuildPath;
        }

        public static async Task<bool> PackAsync(ClassLibrary project, CancellationToken cancellationToken)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                FileName = ProcessName,
                Arguments = $"pack \"{project.ProjectPath}\" -nologo -c Release -verbosity:quiet /p:IsPublishBuild=true /p:GeneratePackageOnBuild=false",
            };

            using var dotnetPackProcess = Process.Start(processStartInfo);

            while (true)
            {
                var line = await dotnetPackProcess.StandardOutput.ReadLineAsync().WaitAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                project.Diagnostics.Add(line);
            }

            await dotnetPackProcess.WaitForExitAsync(cancellationToken);

            return dotnetPackProcess.ExitCode == 0;
        }

        public static async Task<bool> TestAsync(TestProject project, CancellationToken cancellationToken)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                FileName = ProcessName,
                Arguments = $"test \"{project.ProjectPath}\" -nologo -c Release -verbosity:quiet",
            };

            using var dotnetTestProcess = Process.Start(processStartInfo);
            await dotnetTestProcess.WaitForExitAsync(cancellationToken);

            return dotnetTestProcess.ExitCode == 0;
        }

        // https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package
        public static async Task<bool> PushAsync(string fileName, string nuGetApiKey, string nuGetSource, Func<string, Task<bool>> deviceLoginCallback, CancellationToken cancellationToken)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                FileName = ProcessName,
                Arguments = $"nuget push \"{fileName}\" --api-key \"{nuGetApiKey}\" --source \"{nuGetSource}\" --interactive",
                RedirectStandardOutput = true,
            };

            using var dotnetPushProcess = Process.Start(processStartInfo);

            var deviceLoginLine = await dotnetPushProcess.StandardOutput.TryReadDeviceLoginAsync(cancellationToken);
            if (!string.IsNullOrEmpty(deviceLoginLine))
            {
                await deviceLoginCallback.Invoke(deviceLoginLine);
            }

            await dotnetPushProcess.WaitForExitAsync(cancellationToken);

            return dotnetPushProcess.ExitCode == 0;
        }
    }
}