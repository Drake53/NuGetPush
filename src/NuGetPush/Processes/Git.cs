// ------------------------------------------------------------------------------
// <copyright file="Git.cs" company="Drake53">
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

namespace NuGetPush.Processes
{
    public static class Git
    {
        private const string ProcessName = "git";

        public static async Task<string> GetRepositoryRootAsync(string solutionDirectory, CancellationToken cancellationToken)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = solutionDirectory,
                FileName = ProcessName,
                Arguments = "rev-parse --show-toplevel",
            };

            using var gitStatusProcess = Process.Start(processStartInfo);
            await gitStatusProcess.WaitForExitAsync(cancellationToken);

            var output = await gitStatusProcess.StandardOutput.ReadToEndAsync();

            return output.TrimEnd('\n');
        }

        // https://git-scm.com/docs/git-status
        public static async Task<HashSet<string>> CheckUncommittedChangesAsync(string repositoryRoot, CancellationToken cancellationToken)
        {
            var processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = repositoryRoot,
                FileName = ProcessName,
                Arguments = "status --porcelain -uno",
            };

            using var gitStatusProcess = Process.Start(processStartInfo);
            await gitStatusProcess.WaitForExitAsync(cancellationToken);

            var output = await gitStatusProcess.StandardOutput.ReadToEndAsync();

            return output.Split("\n", StringSplitOptions.RemoveEmptyEntries).Select(line => line[3..]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}