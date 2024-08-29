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
        public static bool CanPack(this ClassLibrary project, bool force)
        {
            return project.RecalculateStatus().CanPack(force);
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
                    return project.IsDirty ? ProjectStatus.Dirty : ProjectStatus.UpToDate;
                }

                return project.IsDirty ? ProjectStatus.Dirty : ProjectStatus.Pending;
            }
            else
            {
                if (project.PackageVersion == project.KnownLatestLocalVersion && project.PackageVersion == project.KnownLatestRemoteVersion)
                {
                    return project.IsDirty ? ProjectStatus.Dirty : ProjectStatus.UpToDate;
                }

                if (project.PackageVersion > project.KnownLatestLocalVersion && project.PackageVersion > project.KnownLatestRemoteVersion)
                {
                    return project.IsDirty ? ProjectStatus.Dirty : ProjectStatus.Pending;
                }

                if (project.PackageVersion > project.KnownLatestRemoteVersion)
                {
                    return ProjectStatus.ReadyToPush;
                }

                return project.IsDirty ? ProjectStatus.Dirty : ProjectStatus.ReadyToPack;
            }
        }

        public static void CheckDirty(this ClassLibrary project, HashSet<string> uncommittedChanges)
        {
            var dirtyFiles = uncommittedChanges
                .Where(path => path.Contains("Directory.Build.props", StringComparison.OrdinalIgnoreCase)
                            || path.StartsWith(project.ProjectDirectory, StringComparison.OrdinalIgnoreCase));

            project.DirtyFiles.Clear();
            project.DirtyFiles.AddRange(dirtyFiles);
            project.IsDirty = project.DirtyFiles.Count > 0;
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