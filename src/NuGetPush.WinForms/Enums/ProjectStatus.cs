// ------------------------------------------------------------------------------
// <copyright file="ProjectStatus.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

namespace NuGetPush.WinForms.Enums
{
    public enum ProjectStatus
    {
        /// <summary>
        /// The project is up-to-date locally and on NuGet.
        /// </summary>
        UpToDate,

        /// <summary>
        /// The project has one or more test projects that reference a different package version than the project's version.
        /// </summary>
        Misconfigured,

        /// <summary>
        /// The project file's version is lower than the known latest version.
        /// </summary>
        Outdated,

        /// <summary>
        /// The project is not ready for packaging, because it has one or more project references.
        /// </summary>
        NotReady,

        /// <summary>
        /// The project has been packed, but it has not been pushed yet.
        /// </summary>
        ReadyToPush,

        /// <summary>
        /// The project is up-to-date on NuGet, but this version does not yet exist locally.
        /// </summary>
        ReadyToPack,

        /// <summary>
        /// The project has been updated, but it has not been packed and pushed yet.
        /// </summary>
        Pending,

        /// <summary>
        /// The project has been packed, but it has not been pushed yet.
        /// </summary>
        Packed,

        /// <summary>
        /// The project has been pushed.
        /// </summary>
        Pushed,

        /// <summary>
        /// One or more tests failed in this project's test project(s).
        /// </summary>
        TestFailed,

        /// <summary>
        /// One or more dependencies for this project have an error (during build) or an exception occured during <see cref="Models.ClassLibrary.FindDependencies(Models.Solution)"/>.
        /// </summary>
        DependencyError,

        /// <summary>
        /// An exception occured when pushing the project.
        /// </summary>
        PushError,

        /// <summary>
        /// An exception occured when packing the project.
        /// </summary>
        PackError,

        /// <summary>
        /// An exception occured when parsing the project.
        /// </summary>
        ParseError,

        /// <summary>
        /// The project is waiting for a dependency to be built.
        /// </summary>
        Idle,

        /// <summary>
        /// The project is being built, tested, and/or pushed.
        /// </summary>
        Working,
    }
}