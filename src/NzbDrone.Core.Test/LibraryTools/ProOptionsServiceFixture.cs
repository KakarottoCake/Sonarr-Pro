using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.LibraryTools;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.LibraryTools
{
    [TestFixture]
    public class ProOptionsServiceFixture : CoreTest<ProOptionsService>
    {
        private const string Token = "spr_test_token";

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>().SetupProperty(c => c.ProAccessKeysJson, "[]");
            Mocker.GetMock<IConfigService>().SetupProperty(c => c.ProOptionsJson, "{}");
            Subject.SaveKeys(new List<ProAccessKey>
            {
                new ProAccessKey { Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Token))), Permission = "read-library" }
            });
        }

        [TestCase("GET", "/api/v5/series", true)]
        [TestCase("GET", "/api/v3/episodefile/7", true)]
        [TestCase("GET", "/api/v5/series/1/compression", false)]
        [TestCase("GET", "/api/v5/config/host", false)]
        [TestCase("GET", "/api/v5/librarytools/keys", false)]
        [TestCase("GET", "/api/v5/command", false)]
        [TestCase("GET", "/api/v5/log", false)]
        [TestCase("POST", "/api/v5/series", false)]
        [TestCase("DELETE", "/api/v5/series/1", false)]
        [TestCase("PUT", "/api/v5/series/1", false)]
        [TestCase("GET", "/api/v5/series/0", false)]
        public void read_keys_use_an_explicit_allowlist(string method, string path, bool expected)
        {
            (Subject.Authenticate(Token, method, path) != null).Should().Be(expected);
        }

        [Test]
        public void add_key_can_add_but_cannot_modify_or_delete_existing_series()
        {
            var keys = Subject.Keys();
            keys[0].Permission = "add-series";
            Subject.SaveKeys(keys);
            Subject.Authenticate(Token, "POST", "/api/v5/series").Should().NotBeNull();
            Subject.Authenticate(Token, "PUT", "/api/v5/series/1").Should().BeNull();
            Subject.Authenticate(Token, "DELETE", "/api/v5/series/1").Should().BeNull();
        }

        [Test]
        public void expired_revoked_and_malformed_keys_are_rejected()
        {
            var keys = Subject.Keys();
            keys[0].Expires = DateTime.UtcNow.AddSeconds(-1);
            Subject.SaveKeys(keys);
            Subject.Authenticate(Token, "GET", "/api/v5/series").Should().BeNull();
            Subject.SaveKeys(new List<ProAccessKey>());
            Subject.Authenticate(Token, "GET", "/api/v5/series").Should().BeNull();
            Mocker.GetMock<IConfigService>().Object.ProAccessKeysJson = "broken";
            Subject.Authenticate(Token, "GET", "/api/v5/series").Should().BeNull();
        }

        [Test]
        public void editing_a_snapshot_does_not_change_unsaved_settings()
        {
            var options = Subject.Read();
            options.AutomationPaused = true;
            options.Series[1] = new ProSeriesOptions { KeepVersions = true };
            Subject.Read().AutomationPaused.Should().BeFalse();
            Subject.ForSeries(1).KeepVersions.Should().BeFalse();
            Subject.Save(options);
            Subject.Read().AutomationPaused.Should().BeTrue();
            Subject.ForSeries(1).KeepVersions.Should().BeTrue();
        }
    }
}
