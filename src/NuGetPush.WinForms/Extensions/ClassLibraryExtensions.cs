// ------------------------------------------------------------------------------
// <copyright file="ClassLibraryExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using NuGetPush.Models;
using NuGetPush.WinForms.Enums;

namespace NuGetPush.WinForms.Extensions
{
    public static class ClassLibraryExtensions
    {
        public static bool CanPack(this ClassLibrary project, HashSet<string> uncommittedChanges, bool force)
        {
            if (project.RecalculateStatus().CanPack(force))
            {
                if (uncommittedChanges.Any(uncommittedChange => uncommittedChange.Contains("Directory.Build.props", StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                var uncommittedFilePaths = uncommittedChanges.Where(path => path.StartsWith(project.RelativeProjectPath, StringComparison.OrdinalIgnoreCase));

                return !uncommittedFilePaths.Any();
            }

            return false;
        }

        public static bool CanPush(this ClassLibrary project, bool force)
        {
            return force
                ? project.PackageVersion is not null && (project.KnownLatestNuGetVersion is null || project.PackageVersion > project.KnownLatestNuGetVersion)
                : project.PackageVersion is not null && project.KnownLatestNuGetVersion is not null && project.PackageVersion > project.KnownLatestNuGetVersion;
        }

        public static ProjectStatus RecalculateStatus(this ClassLibrary project)
        {
            if (project.PackageVersion is null)
            {
                return ProjectStatus.NotReady;
            }

            if (project.Dependencies is null)
            {
                return ProjectStatus.DependencyError;
            }

            if (project.PackageVersion < project.KnownLatestLocalVersion || project.PackageVersion < project.KnownLatestNuGetVersion)
            {
                return ProjectStatus.Outdated;
            }

            if (project.MisconfiguredTestProjects.Any())
            {
                return ProjectStatus.Misconfigured;
            }

            if (project.PackageVersion == project.KnownLatestLocalVersion && project.PackageVersion == project.KnownLatestNuGetVersion)
            {
                return ProjectStatus.UpToDate;
            }

            if (project.PackageVersion > project.KnownLatestLocalVersion && project.PackageVersion > project.KnownLatestNuGetVersion)
            {
                return ProjectStatus.Pending;
            }

            if (project.PackageVersion > project.KnownLatestNuGetVersion)
            {
                return ProjectStatus.ReadyToPush;
            }

            return ProjectStatus.ReadyToPack;
        }
    }
}