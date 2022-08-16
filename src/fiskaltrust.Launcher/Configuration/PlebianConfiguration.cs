using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Constants;

namespace fiskaltrust.Launcher.Configuration
{
    public record PlebianConfiguration
    {
        public PackageType PackageType { get; set; }
        public Guid PackageId { get; set; }
    }
}
