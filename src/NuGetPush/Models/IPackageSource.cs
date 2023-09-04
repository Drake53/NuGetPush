// ------------------------------------------------------------------------------
// <copyright file="IPackageSource.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Configuration;
using NuGet.Versioning;

namespace NuGetPush.Models
{
    [Obsolete("Use PackageSourceExtensions")]
    public interface IPackageSource
    {
        PackageSource PackageSource { get; }

        Task<NuGetVersion?> GetLatestNuGetVersionAsync(CancellationToken cancellationToken = default);

        Task<bool> UploadPackageAsync(CancellationToken cancellationToken = default);
    }
}