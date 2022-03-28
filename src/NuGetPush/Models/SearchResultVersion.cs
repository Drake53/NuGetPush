using Newtonsoft.Json;

namespace NuGetPush.Models
{
    /// <summary>
    /// A single version from a <see cref="SearchResult"/>.
    ///
    /// See https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#search-result
    /// </summary>
    public class SearchResultVersion
    {
        /// <summary>
        /// The package's full NuGet version after normalization, including any SemVer 2.0.0 build metadata.
        /// </summary>
        [JsonProperty("version")]
        public string Version { get; set; }

        /// <summary>
        /// The downloads for this single version of the matched package.
        /// </summary>
        [JsonProperty("downloads")]
        public long Downloads { get; set; }
    }
}