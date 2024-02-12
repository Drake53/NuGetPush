// ------------------------------------------------------------------------------
// <copyright file="AuthenticationManager.cs" company="Drake53">
// Licensed under the MIT license.
// See the LICENSE file in the project root for more information.
// </copyright>
// ------------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using NuGet.Configuration;

using NuGetPush.WinForms.Forms;

namespace NuGetPush.WinForms.Helpers
{
    public static class AuthenticationManager
    {
        private static readonly SemaphoreSlim _asyncLock = new(1, 1);
        private static bool _canceledAuthentication;

        public static async Task<bool> HandleAuthenticationAsync(PackageSource packageSource, CancellationToken cancellationToken)
        {
            await _asyncLock.WaitAsync(cancellationToken);
            try
            {
                if (_canceledAuthentication)
                {
                    return false;
                }

                using var credentialsForm = new CredentialsForm(packageSource);

                var dialogResult = credentialsForm.ShowDialog();
                if (dialogResult == DialogResult.OK)
                {
                    packageSource.Credentials = new PackageSourceCredential(
                        packageSource.Source,
                        credentialsForm.UserName,
                        credentialsForm.AccessToken,
                        true,
                        null);

                    return true;
                }

                _canceledAuthentication = true;

                return false;
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        public static void Reset()
        {
            _canceledAuthentication = false;
        }
    }
}