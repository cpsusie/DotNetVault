using System;
using System.Text;
using DotNetVault;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VaultUnitTests
{
    using VaultType = ReadWriteStringBufferVault;

    public class SbVaultRoAcqTests
    {
        [NotNull] public ITestOutputHelper Helper { get; }

        [Fact]
        public void TestThrowsAlready()
        {
            string finalResult;
            using (var vault = _vaultGen())
            {
                string originalValue = vault.CopyCurrentValue(TimeSpan.FromMilliseconds(50));
                Assert.True(originalValue == StartingText);
                for (int i = 0; i < 3; ++i)
                {
                    using var lck = vault.RoLock();
                    Assert.Equal(StartingText, lck.ToString());
                }

                {
                    using var lck = vault.RoLock();
                    try
                    {
                        string shouldntWork = vault.CopyCurrentValue(TimeSpan.FromMilliseconds(100));
                        Helper.WriteLine("You shouldn't ever see me: {0}.", shouldntWork);
                        throw new ThrowsException(typeof(LockAlreadyHeldThreadException));
                    }
                    catch (RwLockAlreadyHeldThreadException)
                    {

                    }
                    catch (ThrowsException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new ThrowsException(typeof(RwLockAlreadyHeldThreadException), ex);
                    }
                }
                {
                    using var finalLck = vault.RoLock();
                    Assert.True(finalLck.Contains("Hello"));
                    Assert.True(finalLck.IndexOf("world") == StartingText.IndexOf("world", StringComparison.Ordinal));
                    Assert.True(finalLck.IndexOf("Clorton") < 0);
                    Assert.False(finalLck.Contains("Clorton"));
                    Assert.True(finalLck.Length == StartingText.Length);
                    for (int i = 0; i < finalLck.Length; ++i)
                    {
                        Assert.Equal(finalLck[i], StartingText[i]);
                    }

                    try
                    {
                        char x = finalLck[finalLck.Length];
                        Helper.WriteLine("Never see me: {0}", x);
                        throw new ThrowsException(typeof(IndexOutOfRangeException));
                    }
                    catch (IndexOutOfRangeException)
                    {

                    }
                    

                    finalResult = finalLck.ToString();
                }
                Assert.Equal(finalResult, originalValue);
            }

            Assert.Equal(finalResult, StartingText);
            Helper.WriteLine(finalResult);
        }

        public SbVaultRoAcqTests([NotNull] ITestOutputHelper helper) =>
            Helper = helper ?? throw new ArgumentNullException(nameof(helper));

        private const string StartingText = "Hello, world!";
        private readonly Func<VaultType> _vaultGen = () =>
            new VaultType(TimeSpan.FromMilliseconds(250), () => new StringBuilder(StartingText));
    }
}
