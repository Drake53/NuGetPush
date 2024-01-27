namespace NuGetPush.WinForms.Models
{
    public sealed class NoPackageSource
    {
        private static readonly NoPackageSource _instance = new NoPackageSource();

        private NoPackageSource()
        {
        }

        public static NoPackageSource Instance => _instance;

        public string Description => "Offline mode";

        public override string ToString() => "None";
    }
}