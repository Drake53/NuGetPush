// ------------------------------------------------------------------------------
// <copyright file="ClassLibraryExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

using NuGetPush.Enums;
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

                var uncommittedFilePaths = uncommittedChanges.Where(path => path.StartsWith(project.ProjectDirectory, StringComparison.OrdinalIgnoreCase));

                return !uncommittedFilePaths.Any();
            }

            return false;
        }

        public static bool CanPush(this ClassLibrary project, bool force)
        {
            if (!project.IsRemotePackageSourceLoaded())
            {
                return false;
            }

            return force
                ? project.PackageVersion is not null && (project.KnownLatestRemoteVersion is null || project.PackageVersion > project.KnownLatestRemoteVersion)
                : project.PackageVersion is not null && project.KnownLatestRemoteVersion is not null && project.PackageVersion > project.KnownLatestRemoteVersion;
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

            if (project.PackageVersion < project.KnownLatestLocalVersion || project.PackageVersion < project.KnownLatestRemoteVersion)
            {
                return ProjectStatus.Outdated;
            }

            if (project.MisconfiguredTestProjects.Any())
            {
                return ProjectStatus.Misconfigured;
            }

            if (!project.IsRemotePackageSourceLoaded())
            {
                if (project.PackageVersion == project.KnownLatestLocalVersion)
                {
                    return ProjectStatus.UpToDate;
                }

                return ProjectStatus.Pending;
            }
            else
            {
                if (project.PackageVersion == project.KnownLatestLocalVersion && project.PackageVersion == project.KnownLatestRemoteVersion)
                {
                    return ProjectStatus.UpToDate;
                }

                if (project.PackageVersion > project.KnownLatestLocalVersion && project.PackageVersion > project.KnownLatestRemoteVersion)
                {
                    return ProjectStatus.Pending;
                }

                if (project.PackageVersion > project.KnownLatestRemoteVersion)
                {
                    return ProjectStatus.ReadyToPush;
                }

                return ProjectStatus.ReadyToPack;
            }
        }

        public static string GetRemotePackageString(this ClassLibrary project)
        {
            if (project.PackageVersion is null)
            {
                return string.Empty;
            }

            return project.KnownLatestRemoteVersionState != RemotePackageVersionRequestState.Loaded
                ? project.KnownLatestRemoteVersionState.ToString()
                : project.KnownLatestRemoteVersion?.ToNormalizedString() ?? string.Empty;
        }
    }
}