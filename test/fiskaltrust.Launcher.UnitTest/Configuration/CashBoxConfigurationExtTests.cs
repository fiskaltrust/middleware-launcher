using fiskaltrust.Launcher.Configuration;
using fiskaltrust.storage.serialization.V0;
using FluentAssertions;

namespace fiskaltrust.Launcher.UnitTest.Configuration
{
  public class CashBoxConfigurationExtTests
  {
    [Fact]
    public void Serialize_Deserialize_Roundtrip_ShouldPreserveData()
    {
      var configuration = new ftCashBoxConfiguration
      {
        ftCashBoxId = Guid.NewGuid(),
        ftQueues = new PackageConfiguration[]
                {
                    new PackageConfiguration
                    {
                        Id = Guid.NewGuid(),
                        Package = "fiskaltrust.Middleware.Queue.SQLite",
                        Version = "1.3.46",
                        Url = new[] { "grpc://localhost:1400" },
                        Configuration = new Dictionary<string, object>
                        {
                            { "connectionstring", "test-connection" }
                        }
                    }
                },
        ftSignaturCreationDevices = new PackageConfiguration[]
                {
                    new PackageConfiguration
                    {
                        Id = Guid.NewGuid(),
                        Package = "fiskaltrust.Middleware.SCU.IT.Epson",
                        Version = "1.3.46",
                        Url = new[] { "grpc://localhost:1401" },
                        Configuration = new Dictionary<string, object>
                        {
                            { "DeviceUrl", "http://localhost/" }
                        }
                    }
                },
        helpers = new PackageConfiguration[]
                {
                    new PackageConfiguration
                    {
                        Id = Guid.NewGuid(),
                        Package = "fiskaltrust.Middleware.Helper.Helipad",
                        Version = "1.3.46",
                        Url = Array.Empty<string>(),
                        Configuration = new Dictionary<string, object>
                        {
                            { "useoffline", false }
                        }
                    }
                }
      };

      var serialized = configuration.Serialize();
      var deserialized = CashBoxConfigurationExt.Deserialize(serialized);

      deserialized.ftCashBoxId.Should().Be(configuration.ftCashBoxId);
      deserialized.ftQueues.Should().HaveCount(1);
      deserialized.ftQueues[0].Id.Should().Be(configuration.ftQueues[0].Id);
      deserialized.ftQueues[0].Package.Should().Be(configuration.ftQueues[0].Package);
      deserialized.ftSignaturCreationDevices.Should().HaveCount(1);
      deserialized.ftSignaturCreationDevices[0].Id.Should().Be(configuration.ftSignaturCreationDevices[0].Id);
      deserialized.helpers.Should().HaveCount(1);
      deserialized.helpers[0].Id.Should().Be(configuration.helpers[0].Id);
    }

    [Fact]
    public void Deserialize_Serialize_Roundtrip_ShouldProduceEquivalentJson()
    {
      var json = @"{
                ""ftCashBoxId"": ""5a1ff5d7-0e99-42da-859a-cb4cf054f256"",
                ""ftQueues"": [
                    {
                        ""Id"": ""ac8fc649-79a6-46b4-9010-ce7c209b0aaa"",
                        ""Package"": ""fiskaltrust.Middleware.Queue.SQLite"",
                        ""Version"": ""1.3.46"",
                        ""Url"": [""grpc://localhost:1400""],
                        ""Configuration"": { ""connectionstring"": ""test"" }
                    }
                ],
                ""ftSignaturCreationDevices"": [],
                ""helpers"": []
            }";

      var deserialized = CashBoxConfigurationExt.Deserialize(json);
      var serialized = deserialized.Serialize();
      var redeserialized = CashBoxConfigurationExt.Deserialize(serialized);

      redeserialized.ftCashBoxId.Should().Be(deserialized.ftCashBoxId);
      redeserialized.ftQueues.Should().HaveCount(deserialized.ftQueues.Length);
      redeserialized.ftQueues[0].Id.Should().Be(deserialized.ftQueues[0].Id);
      redeserialized.ftQueues[0].Package.Should().Be(deserialized.ftQueues[0].Package);
      redeserialized.ftQueues[0].Configuration["connectionstring"]?.ToString().Should().Be("test");
    }

    [Fact]
    public void Deserialize_ValidJson_ShouldReturnCorrectObject()
    {
      var json = @"{
                ""ftCashBoxId"": ""5a1ff5d7-0e99-42da-859a-cb4cf054f256"",
                ""ftQueues"": [],
                ""ftSignaturCreationDevices"": [],
                ""helpers"": []
            }";

      var result = CashBoxConfigurationExt.Deserialize(json);

      result.Should().NotBeNull();
      result.ftCashBoxId.Should().Be(Guid.Parse("5a1ff5d7-0e99-42da-859a-cb4cf054f256"));
    }

    [Fact]
    public void Deserialize_InvalidJson_ShouldThrow()
    {
      var invalidJson = "not a json string";

      var act = () => CashBoxConfigurationExt.Deserialize(invalidJson);

      act.Should().Throw<Exception>();
    }

    [Fact]
    public void Serialize_EmptyConfiguration_ShouldProduceValidJson()
    {
      var configuration = new ftCashBoxConfiguration();

      var serialized = configuration.Serialize();

      serialized.Should().NotBeNullOrWhiteSpace();
      var deserialized = CashBoxConfigurationExt.Deserialize(serialized);
      deserialized.Should().NotBeNull();
    }

    [Fact]
    public void Serialize_UsesNewtonsoftJson_ConsistentWithDeserialize()
    {
      // This test verifies that Serialize and Deserialize use the same serializer (Newtonsoft.Json),
      // which was the fix in the latest commit. Previously Serialize used System.Text.Json which
      // could produce different output than Newtonsoft.Json, causing roundtrip issues.
      var configuration = new ftCashBoxConfiguration
      {
        ftCashBoxId = Guid.NewGuid(),
        ftQueues = new PackageConfiguration[]
                {
                    new PackageConfiguration
                    {
                        Id = Guid.NewGuid(),
                        Package = "test.package",
                        Version = "1.0.0",
                        Url = new[] { "grpc://localhost:1400" },
                        Configuration = new Dictionary<string, object>
                        {
                            { "key", "value" },
                            { "nested", new Dictionary<string, object> { { "inner", "data" } } }
                        }
                    }
                },
        ftSignaturCreationDevices = Array.Empty<PackageConfiguration>(),
        helpers = Array.Empty<PackageConfiguration>()
      };

      var serialized = configuration.Serialize();

      // Verify Newtonsoft can deserialize it (same serializer)
      var newtonsoftDeserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<ftCashBoxConfiguration>(serialized);
      newtonsoftDeserialized.Should().NotBeNull();
      newtonsoftDeserialized!.ftQueues[0].Id.Should().Be(configuration.ftQueues[0].Id);
      newtonsoftDeserialized.ftQueues[0].Configuration["key"]?.ToString().Should().Be("value");
    }

    [Fact]
    public void Serialize_ConfigurationWithSpecialCharacters_ShouldRoundtrip()
    {
      var configuration = new ftCashBoxConfiguration
      {
        ftCashBoxId = Guid.NewGuid(),
        ftQueues = new PackageConfiguration[]
                {
                    new PackageConfiguration
                    {
                        Id = Guid.NewGuid(),
                        Package = "test.package",
                        Version = "1.0.0",
                        Url = Array.Empty<string>(),
                        Configuration = new Dictionary<string, object>
                        {
                            { "path", "C:\\Program Files\\fiskaltrust" },
                            { "unicode", "äöü ß €" },
                            { "quotes", "value with \"quotes\"" }
                        }
                    }
                },
        ftSignaturCreationDevices = Array.Empty<PackageConfiguration>(),
        helpers = Array.Empty<PackageConfiguration>()
      };

      var serialized = configuration.Serialize();
      var deserialized = CashBoxConfigurationExt.Deserialize(serialized);

      deserialized.ftQueues[0].Configuration["path"]?.ToString().Should().Be("C:\\Program Files\\fiskaltrust");
      deserialized.ftQueues[0].Configuration["unicode"]?.ToString().Should().Be("äöü ß €");
      deserialized.ftQueues[0].Configuration["quotes"]?.ToString().Should().Be("value with \"quotes\"");
    }
  }
}
