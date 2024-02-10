// ------------------------------------------------------------------------------
// <copyright file="RemotePackageVersionRequestState.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

namespace NuGetPush.Enums
{
    public enum RemotePackageVersionRequestState
    {
        Offline,
        Loading,
        Loaded,
        Unauthorized,
        Error,
    }
}