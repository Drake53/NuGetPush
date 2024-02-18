// ------------------------------------------------------------------------------
// <copyright file="Git.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetPush.Processes
{
    public static class Git
    {
        private const string ProcessName = "git";

        public static async Task<string?> GetRepositoryRootAsync(string solutionDirectory, CancellationToken cancellationToken)
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

            var result = await gitStatusProcess.StandardOutput.ReadLineAsync().WaitAsync(cancellationToken);

            await gitStatusProcess.StandardOutput.ReadToEndAsync().WaitAsync(cancellationToken);
            await gitStatusProcess.WaitForExitAsync(cancellationToken);

            return gitStatusProcess.ExitCode == 0 ? result : null;
        }

        // https://git-scm.com/docs/git-status
        public static async Task<HashSet<string>> CheckUncommittedChangesAsync(string? repositoryRoot, CancellationToken cancellationToken)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (repositoryRoot is null)
            {
                return result;
            }

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

            while (true)
            {
                var line = await gitStatusProcess.StandardOutput.ReadLineAsync().WaitAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                result.Add(line[3..]);
            }

            await gitStatusProcess.WaitForExitAsync(cancellationToken);

            return result;
        }
    }
}