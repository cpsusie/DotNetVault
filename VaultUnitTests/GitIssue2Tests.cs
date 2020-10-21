using System;
using System.Text;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;

namespace VaultUnitTests
{
    using SbVault = ReadWriteStringBufferVault;
    using GuidVault = ReadWriteValueListVault<Guid>;
    using RwStrVault = BasicReadWriteVault<string>;
    using MutResMonV = MutableResourceMonitorVault<StringBuilder>;
    using BscMonV = BasicMonitorVault<string>;

    public sealed class GitIssue2Tests  : OutputHelperAndFixtureHavingTests<GitIssueTwoTestFixture>
    {
        public GitIssue2Tests([NotNull] ITestOutputHelper helper, [NotNull] GitIssueTwoTestFixture fixture)
            : base(helper, fixture) { }

        [Fact]
        public void RunFixtureTest()
        {
            Fixture.AppendToVault("Hello, world!");
            Fixture.AppendToVault("  How are you today?");
            Helper.WriteLine($"Current count: {Fixture.GetCount()}!");
            Fixture.AppendToVault($"{Environment.NewLine} FIZZ BUZZ BAM BOOZLE!");
            (bool extracted, string contents) = Fixture.ExtractContentsAndClearIfCountGreaterThanThen(9);
            Helper.WriteLine(extracted ? "extracted and cleared" : "did not extract or clear");
            if (extracted)
            {
                Helper.WriteLine($"Extracted contents: [{contents}]");
            }
            Helper.WriteLine($"Count post potential extraction: {Fixture.GetCount()}");
            Helper.WriteLine("Done!");
        }

        [Fact]
        public void TestRwSbVaultNormalForever()
        {
            const string text = "Foobar";
            string output;
            SbVault vault = new SbVault(TimeSpan.FromMilliseconds(250), () => new StringBuilder());
            {
                //using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                using var roLck = vault.UpgradableRoLock();
                if (roLck.Length == 0)
                {
                    using var writeLock = roLck.LockBlockUntilAcquired();
                    //using var writeLock = roLck.Lock();
                    writeLock.Append("Foobar");
                }
                output = roLck.ToString();
            }
            Assert.True(text == output);
        }

        [Fact]
        public void TestRwSbVaultForeverNormal()
        {
            const string text = "Foobar";
            string output;
            SbVault vault = new SbVault(TimeSpan.FromMilliseconds(250), () => new StringBuilder());
            {
                using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                //using var roLck = vault.UpgradableRoLock();
                if (roLck.Length == 0)
                {
                    //using var writeLock = roLck.LockBlockUntilAcquired();
                    using var writeLock = roLck.Lock();
                    writeLock.Append("Foobar");
                }
                output = roLck.ToString();
            }
            Assert.True(text == output);
        }
        [Fact]
        public void TestRwSbVaultForeverForever()
        {
            const string text = "Foobar";
            string output;
            SbVault vault = new SbVault(TimeSpan.FromMilliseconds(250), () => new StringBuilder());
            {
                using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                //using var roLck = vault.UpgradableRoLock();
                if (roLck.Length == 0)
                {
                    using var writeLock = roLck.LockBlockUntilAcquired();
                    //using var writeLock = roLck.Lock();
                    writeLock.Append("Foobar");
                }
                output = roLck.ToString();
            }
            Assert.True(text == output);
        }
        [Fact]
        public void TestRwSbVaultNormalNormal()
        {
            const string text = "Foobar";
            string output;
            SbVault vault = new SbVault(TimeSpan.FromMilliseconds(250), () => new StringBuilder());
            {
                //using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                using var roLck = vault.UpgradableRoLock();
                if (roLck.Length == 0)
                {
                    //using var writeLock = roLck.LockBlockUntilAcquired();
                    using var writeLock = roLck.Lock();
                    writeLock.Append("Foobar");
                }
                output = roLck.ToString();
            }
            Assert.True(text == output);
        }

        [Fact]
        public void TestGuidVaultNormalNormal()
        {
            
            Guid expected = Guid.NewGuid();
            Guid output;
            GuidVault vault = new GuidVault();
            {
                //using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                using var roLck = vault.UpgradableRoLock();
                if (roLck.Count == 0)
                {
                    //using var writeLock = roLck.LockWaitForever();
                    using var writeLock = roLck.Lock();
                    writeLock.Add(in expected);
                }

                output = roLck[0];
            }
            Assert.True(expected == output);
        }

        [Fact]
        public void TestGuidVaultNormalForever()
        {

            Guid expected = Guid.NewGuid();
            Guid output;
            GuidVault vault = new GuidVault();
            {
                //using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                using var roLck = vault.UpgradableRoLock();
                if (roLck.Count == 0)
                {
                    using var writeLock = roLck.LockWaitForever();
                    //using var writeLock = roLck.Lock();
                    writeLock.Add(in expected);
                }

                output = roLck[0];
            }
            Assert.True(expected == output);
        }

        [Fact]
        public void TestGuidVaultForeverNormal()
        {

            Guid expected = Guid.NewGuid();
            Guid output;
            GuidVault vault = new GuidVault();
            {
                using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                //using var roLck = vault.UpgradableRoLock();
                if (roLck.Count == 0)
                {
                    //using var writeLock = roLck.LockWaitForever();
                    using var writeLock = roLck.Lock();
                    writeLock.Add(in expected);
                }

                output = roLck[0];
            }
            Assert.True(expected == output);
        }

        [Fact]
        public void TestGuidVaultForeverForever()
        {

            Guid expected = Guid.NewGuid();
            Guid output;
            GuidVault vault = new GuidVault();
            {
                using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                //using var roLck = vault.UpgradableRoLock();
                if (roLck.Count == 0)
                {
                    using var writeLock = roLck.LockWaitForever();
                    //using var writeLock = roLck.Lock();
                    writeLock.Add(in expected);
                }

                output = roLck[0];
            }
            Assert.True(expected == output);
        }

        [Fact]
        public void TestStrVaultNormalForever()
        {
            const string text = "Foobar";
            string output;
            RwStrVault vault = new RwStrVault(string.Empty ,TimeSpan.FromMilliseconds(250));
            {
                using var roLck = vault.UpgradableRoLock();
                //using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                if (roLck.Value.Length == 0)
                {
                    using var writeLock = roLck.LockWaitForever();
                    writeLock.Value += text;
                }
                output = roLck.Value;
            }
            Assert.True(text == output);
        }

        [Fact]
        public void TestStrVaultForeverNormal()
        {
            const string text = "Foobar";
            string output;
            RwStrVault vault = new RwStrVault(string.Empty, TimeSpan.FromMilliseconds(250));
            {
                //using var roLck = vault.UpgradableRoLock();
                using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                if (roLck.Value.Length == 0)
                {
                    using var writeLock = roLck.Lock();
                    writeLock.Value += text;
                }
                output = roLck.Value;
            }
            Assert.True(text == output);
        }

        [Fact]
        public void TestStrVaultForeverForever()
        {
            const string text = "Foobar";
            string output;
            RwStrVault vault = new RwStrVault(string.Empty, TimeSpan.FromMilliseconds(250));
            {
                //using var roLck = vault.UpgradableRoLock();
                using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                if (roLck.Value.Length == 0)
                {
                    using var writeLock = roLck.LockWaitForever();
                    writeLock.Value += text;
                }
                output = roLck.Value;
            }
            Assert.True(text == output);
        }

        [Fact]
        public void TestStrVaultNormalNormal()
        {
            const string text = "Foobar";
            string output;
            RwStrVault vault = new RwStrVault(string.Empty, TimeSpan.FromMilliseconds(250));
            {
                using var roLck = vault.UpgradableRoLock();
                //using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                if (roLck.Value.Length == 0)
                {
                    using var writeLock = roLck.Lock();
                    writeLock.Value += text;
                }
                output = roLck.Value;
            }
            Assert.True(text == output);
        }

        [Fact]
        public void TestSbMonV()
        {
            const string text = "Foobar";
            string output;
            MutResMonV vault = MutResMonV.CreateMonitorMutableResourceVault(() => new StringBuilder(text), TimeSpan.FromMilliseconds(250));
            {
                using var lck = vault.LockBlockUntilAcquired();
                output = lck.ExecuteQuery((in StringBuilder sb) => sb.ToString());
            }
            Assert.True(text == output);
        }

        [Fact]
        public void TestBasicStringVault()
        {
            const string text = "Foobar";
            string output;
            BscMonV vault = new BscMonV(string.Empty, TimeSpan.FromMilliseconds(250));
            {
                using var lck = vault.LockBlockUntilAcquired();
                if (lck.Value.Length == 0)
                {
                    lck.Value += text;
                }
                output = lck.Value;
            }
            Assert.True(text == output);
        }

        





    }
}
