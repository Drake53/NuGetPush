// ------------------------------------------------------------------------------
// <copyright file="ProjectStatusExtensions.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using NuGetPush.WinForms.Enums;

namespace NuGetPush.WinForms.Extensions
{
    public static class ProjectStatusExtensions
    {
        public static bool CanPack(this ProjectStatus projectStatus, bool force)
        {
            return projectStatus switch
            {
                ProjectStatus.UpToDate => force,
                ProjectStatus.Misconfigured => false,
                ProjectStatus.Outdated => false,
                ProjectStatus.NotReady => false,
                ProjectStatus.ReadyToPush => force,
                ProjectStatus.ReadyToPack => true,
                ProjectStatus.Pending => true,
                ProjectStatus.Packed => force,
                ProjectStatus.Pushed => force,
                ProjectStatus.TestFailed => force,
                ProjectStatus.DependencyError => false,
                ProjectStatus.PushError => force,
                ProjectStatus.PackError => force,
                ProjectStatus.ParseError => false,
                ProjectStatus.Idle => false,
                ProjectStatus.Working => false,
            };
        }
    }
}