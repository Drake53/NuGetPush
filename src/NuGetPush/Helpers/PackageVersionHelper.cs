﻿// ------------------------------------------------------------------------------
// <copyright file="PackageVersionHelper.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Evaluation;

using NuGet.Versioning;

namespace NuGetPush.Helpers
{
    internal static class PackageVersionHelper
    {
        public static Dictionary<string, NuGetVersion>? GetCentrallyManagedPackageVersions(Project project)
        {
            if (project.GetProperty("ManagePackageVersionsCentrally")?.EvaluatedValue != "true")
            {
                return null;
            }

            var result = new Dictionary<string, NuGetVersion>(StringComparer.Ordinal);
            foreach (var packageVersion in project.GetItems("PackageVersion"))
            {
                var version = packageVersion.GetMetadata("Version");
                if (version is null)
                {
                    throw new InvalidDataException($"Package version is missing. (Project = {Path.GetFileNameWithoutExtension(project.FullPath)}, Package = {packageVersion.EvaluatedInclude})");
                }

                if (!NuGetVersion.TryParse(version.EvaluatedValue, out var nuGetVersion))
                {
                    throw new InvalidDataException($"Package version '{version.EvaluatedValue}' ({version.UnevaluatedValue}) is invalid. (Project = {Path.GetFileNameWithoutExtension(project.FullPath)}, Package = {packageVersion.EvaluatedInclude})");
                }

                result.Add(packageVersion.EvaluatedInclude, nuGetVersion);
            }

            return result;
        }

        public static NuGetVersion GetNuGetVersionFromPackageReference(
            ProjectItem packageReference,
            Dictionary<string, NuGetVersion>? centralPackageVersions)
        {
            var packageName = packageReference.EvaluatedInclude;

            if (centralPackageVersions is not null)
            {
                var version = packageReference.GetMetadata("Version");
                if (version is not null)
                {
                    throw new InvalidDataException($"Package version should not be defined on a PackageReference when central package management is enabled. (Package = {packageName})");
                }

                if (!centralPackageVersions.TryGetValue(packageName, out var result))
                {
                    throw new InvalidDataException($"Package is missing from central package management. (Package = {packageName})");
                }

                return result;
            }
            else
            {
                var version = packageReference.GetMetadata("Version");
                if (version is null)
                {
                    throw new InvalidDataException($"Package version is missing. (Package = {packageName})");
                }

                if (!NuGetVersion.TryParse(version.EvaluatedValue, out var result))
                {
                    throw new InvalidDataException($"Package version '{version.EvaluatedValue}' ({version.UnevaluatedValue}) is invalid. (Package = {packageName})");
                }

                return result;
            }
        }
    }
}