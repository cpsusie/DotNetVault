﻿using System;
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

    public sealed class GitIssue2Tests  : TestOutputHelperHaver
    {
        public GitIssue2Tests([NotNull] ITestOutputHelper helper)
            : base(helper) { }

        [Fact]
        public void TestRwSbVault()
        {
            const string text = "Foobar";
            string output;
            SbVault vault = new SbVault(TimeSpan.FromMilliseconds(250), () => new StringBuilder());
            {
                using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                if (roLck.Length == 0)
                {
                    using var writeLock = roLck.LockBlockUntilAcquired();
                    writeLock.Append("Foobar");
                }
                output = roLck.ToString();
            }
            Assert.True(text == output);
        }

        [Fact]
        public void TestGuidVault()
        {
            
            Guid expected = Guid.NewGuid();
            Guid output;
            GuidVault vault = new GuidVault();
            {
                using var roLck = vault.UpgradableRoLockBlockUntilAcquired();
                if (roLck.Count == 0)
                {
                    using var writeLock = roLck.LockWaitForever();
                    writeLock.Add(in expected);
                }

                output = roLck[0];
            }
            Assert.True(expected == output);
        }

        [Fact]
        public void TestStrVault()
        {
            const string text = "Foobar";
            string output;
            RwStrVault vault = new RwStrVault(string.Empty ,TimeSpan.FromMilliseconds(250));
            {
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
