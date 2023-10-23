using AutoBogus;
using Bogus;
using Bogus.Extensions;
using fiskaltrust.Launcher.Common.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace fiskaltrust.Launcher.UnitTest.Configuration
{
    public class ConfigurationTests
    {
        [Fact]
        public void EmptyConfiguration_SerializaAndDeserialize_ShouldPreserveNull()
        {
            var configuration = new LauncherConfiguration { };

            var serialized = configuration.Serialize();

            var deserialized = LauncherConfiguration.Deserialize(serialized);

            deserialized.Should().BeEquivalentTo(configuration);
        }

        [Fact]
        public void RandomConfiguration_SerializaAndDeserialize_ShouldPreserveNull()
        {
            var faker = new Faker();
            for (var i = 0; i < 100; i++)
            {
                var configuration = new AutoFaker<LauncherConfiguration>()
                    .Configure(builder => builder.WithSkip<SemanticVersioning.Range>())
                    .RuleFor(c => c.CashboxId, f => Guid.NewGuid())
                    .RuleFor(c => c.AccessToken, f => Convert.ToBase64String(f.Random.Bytes(33)))
                    .RuleForType(typeof(int?), f => f.Random.Int().OrNull(f))
                    .RuleForType(typeof(bool?), f => f.Random.Bool().OrNull(f))
                    .RuleForType(typeof(string), f => f.Random.Word().OrNull(f))
                    .RuleForType(typeof(Guid?), f => f.Random.Guid().OrNull(f))
                    .RuleForType(typeof(LogLevel?), f => f.Random.Enum<LogLevel>().OrNull(f))
                    .RuleForType(typeof(Uri), f => new Uri(f.Internet.Url()).OrNull(f))
                    .Generate();

                var serialized = configuration.Serialize();

                var deserialized = LauncherConfiguration.Deserialize(serialized);

                deserialized.Raw(d => configuration.Raw(c => d.Should().BeEquivalentTo(c, "")));
                deserialized.Should().BeEquivalentTo(deserialized);
            }
        }
        
        [Fact]
        public void DifferentCaseInKeys_Deserialize_ShouldPreserveProperties()
        {
            var json = @"{
                ""loglevel"": ""Information"",
                ""LOGLEVEL"": ""Error"",
                ""LogLevel"": ""Warning""
            }";
        
            var deserialized = LauncherConfiguration.Deserialize(json);
        
            deserialized.LogLevel.Should().Be(LogLevel.Warning);
        }
        
        [Fact]
        public void LowerCaseKeys_Deserialize_ShouldPreserveProperties()
        {
            var json = @"{
                ""loglevel"": ""Information""
            }";
        
            var deserialized = LauncherConfiguration.Deserialize(json);
        
            deserialized.LogLevel.Should().Be(LogLevel.Information);
        }
        
        [Fact]
        public void UpperCaseKeys_Deserialize_ShouldPreserveProperties()
        {
            var json = @"{
                ""LOGLEVEL"": ""Error""
            }";
        
            var deserialized = LauncherConfiguration.Deserialize(json);
        
            deserialized.LogLevel.Should().Be(LogLevel.Error);
        }
        
        [Fact]
        public void MixedCaseKeys_Deserialize_ShouldPreserveProperties()
        {
            var json = @"{
                ""logLevel"": ""Warning""
            }";
        
            var deserialized = LauncherConfiguration.Deserialize(json);
        
            deserialized.LogLevel.Should().Be(LogLevel.Warning);
        }        
    }
}