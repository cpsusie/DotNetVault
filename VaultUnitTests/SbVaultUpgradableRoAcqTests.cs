using System;
using System.Text;
using System.Threading;
using DotNetVault;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using ResourceType = System.String;
namespace VaultUnitTests
{
    using VaultType = ReadWriteStringBufferVault;
    public class SbVaultUpgradableRoAcqTests
    {
        [NotNull] public ITestOutputHelper Helper { get; }

        public SbVaultUpgradableRoAcqTests([NotNull] ITestOutputHelper helper) =>
            Helper = helper ?? throw new ArgumentNullException(nameof(helper));

        [Fact]
        public void TestThrowsAlready()
        {
            ResourceType text = "Hello, world!";
            ResourceType finalResult;
            using (var vault = _vaultGen())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), text);

                for (int i = 0; i < 3; ++i)
                {
                    using var lck = vault.UpgradableRoLockBlockUntilAcquired();
                    {
                        using var writeLock = lck.LockBlockUntilAcquired();
                        writeLock.Append(Environment.NewLine + "Addition number: " + (i + 1));
                    }
                }

                {
                    using var lck = vault.UpgradableRoLock();
                    try
                    {
                        // ReSharper disable once UnusedVariable
                        ResourceType shouldWork = vault.CopyCurrentValue(TimeSpan.FromMilliseconds(100));
                        ResourceType alsoShouldWork = vault.CopyCurrentValue(TimeSpan.FromMilliseconds(100));
                        Assert.Equal(shouldWork, alsoShouldWork);

                        //should work
                        using var writeLck1 = lck.Lock();
                        //should throw (copy current value releases before returns; here we hold open)
                        using var writeLck2 = lck.Lock();
                        throw new ThrowsException(typeof(RwLockAlreadyHeldThreadException));
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
                    finalResult = finalLck.ToString();
                }
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
        }

        [Fact]
        public void ThrowsDoubleLockAlready()
        {
            ResourceType finalResult;
            using (var vault = _vaultGen())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), ResourceType.Empty);

                for (int i = 0; i < 3; ++i)
                {
                    //RwLockedResource<VaultType, string> writeLock = default;
                    string appendMe = $"Hi mom # {(i + 1).ToString()}{Environment.NewLine}";
                    using var lck = vault.UpgradableRoLock();
                    {
                        using var wl = lck.Lock();
                        wl.Append(appendMe);
                    }
                    Assert.EndsWith(appendMe, lck.ToString());
                }

                {
                    Assert.Throws<RwLockAlreadyHeldThreadException>(() =>
                    {

                        using var lck = vault.UpgradableRoLockBlockUntilAcquired();
                        using var lck2 = vault.UpgradableRoLockBlockUntilAcquired();
                    });
                }
                {
                    using var finalLck = vault.RoLock();
                    finalResult = finalLck.ToString();
                }
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
        }

        [Fact]
        public void TestThrowsTimeout()
        {
            string text = "Hello, world!";
            StartToken token = new StartToken();
            ResourceType finalResult;
            ExceptionReceptor receptor = new ExceptionReceptor();
            DateTime startedAt;
            using (var vault = _vaultGen())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), text);

                // ReSharper disable AccessToDisposedClosure
                Thread firstThread = new Thread(() => DoThreadOne(vault, token, text));
                Thread secondThread = new Thread(() => DoThreadTwo(vault, token, receptor));
                // ReSharper restore AccessToDisposedClosure

                firstThread.Start();
                secondThread.Start();
                Thread.SpinWait(100_000);
                startedAt = token.SetOrThrow();
                secondThread.Join();
                firstThread.Join();

                {
                    using var lck = vault.Lock();
                    lck.Append("... it worked?!");
                    finalResult = lck.ToString();
                }

                Assert.NotNull(receptor.SuppliedException);
                Assert.False(receptor.IsBadException);
                Assert.True(typeof(TimeoutException) == receptor.ExceptionType);
                Assert.True(receptor.Ts > startedAt);
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
            Helper.WriteLine($"Timeout thrown after {(receptor.Ts - startedAt).TotalMilliseconds:F3} milliseconds.");
            static void DoThreadOne(VaultType bmv, StartToken tkn, string shouldBe)
            {
                HpTimeStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }

                using var lck = bmv.UpgradableRoLock();
                Thread.Sleep(TimeSpan.FromMilliseconds(750));
                Assert.Equal(lck.ToString(), shouldBe);
            }

            static void DoThreadTwo(VaultType bv, StartToken tkn, ExceptionReceptor receptor)
            {
                HpTimeStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                try
                {
                    using var lck = bv.UpgradableRoLock(TimeSpan.FromMilliseconds(10));
                    receptor.SetBadException();
                }
                catch (TimeoutException ex)
                {
                    receptor.SupplyExceptionOrThrow(ex);
                }
                catch (Exception rx)
                {
                    receptor.SupplyExceptionOrThrow(rx);
                }
            }
        }

        [Fact]
        public void TestThrowsOperationCancelled()
        {
            string text = "Hello, world!";
            StartToken token = new StartToken();
            ResourceType finalResult;
            ExceptionReceptor receptor = new ExceptionReceptor();
            DateTime startedAt;
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var vault = _vaultGen())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), text);

                // ReSharper disable AccessToDisposedClosure
                Thread thread = new Thread(() => DoThreadTwo(vault, token, receptor, cancellationTokenSource.Token));
                // ReSharper restore AccessToDisposedClosure

                {
                    using var lck = vault.Lock();
                    thread.Start();
                    Thread.SpinWait(100_000);
                    startedAt = token.SetOrThrow();
                    Thread.Sleep(TimeSpan.FromMilliseconds(250));
                    cancellationTokenSource.Cancel();
                    lck.Append("Hi mom!");
                    thread.Join();
                }

                {
                    using var lck = vault.Lock();
                    lck.Append("... it worked?!");
                    finalResult = lck.ToString();
                }

                Assert.NotNull(receptor.SuppliedException);
                Assert.False(receptor.IsBadException);
                Assert.True(typeof(OperationCanceledException) == receptor.ExceptionType);
                Assert.True(receptor.Ts > startedAt);
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
            Helper.WriteLine($"Cancellation thrown after {(receptor.Ts - startedAt).TotalMilliseconds:F3} milliseconds.");

            static void DoThreadTwo(VaultType bv, StartToken tkn, ExceptionReceptor receptor, CancellationToken token)
            {
                HpTimeStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }
                try
                {
                    using var lck = bv.UpgradableRoLock(token);
                    receptor.SetBadException();
                }
                catch (OperationCanceledException ex)
                {
                    receptor.SupplyExceptionOrThrow(ex);
                }
                catch (Exception rx)
                {
                    receptor.SupplyExceptionOrThrow(rx);
                }
            }
        }

        [Fact]
        public void TestSeqBlockAcqs()
        {
            const string individAppend = "\nAnd hello again!";
            const string concatenated = individAppend + individAppend;
            const string startingText = "Hello, world!";
            const string finalShouldBe = startingText + concatenated;
            string finalResult;
            using (var vault = _vaultGen())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(10), startingText);

                {
                    using var lck = vault.UpgradableRoLockBlockUntilAcquired();
                    {
                        using var wl1 = lck.LockBlockUntilAcquired();
                        wl1.Append(individAppend);
                    }
                    Assert.EndsWith(individAppend, lck.ToString());
                    {
                        using var wl2 = lck.LockBlockUntilAcquired();
                        wl2.Append(individAppend);
                    }
                    Assert.EndsWith(concatenated, lck.ToString());
                }
                {
                    using var lck = vault.RoLockBlockUntilAcquired();
                    Assert.Equal(finalShouldBe, lck.ToString());
                }
                Assert.Throws<RwLockAlreadyHeldThreadException>(() =>
                {
                    using var lckOne = vault.UpgradableRoLockBlockUntilAcquired();
                    using var lckTwo = vault.UpgradableRoLockBlockUntilAcquired();
                });

                {
                    using var lck = vault.RoLockBlockUntilAcquired();
                    finalResult = lck.ToString();
                }
                Assert.True(finalResult == vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)) && finalResult == finalShouldBe);
            }
            Helper.WriteLine(finalResult);
        }

        [Fact]
        public void TestThrowsOperationCancelledWhenCancelBeforeTimeout()
        {
            StartToken token = new StartToken();
            ResourceType finalResult;
            ExceptionReceptor receptor = new ExceptionReceptor();
            DateTime startedAt;
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var vault = _vaultGen())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), ResourceType.Empty);

                // ReSharper disable AccessToDisposedClosure
                Thread thread = new Thread(() => DoThreadTwo(vault, token, receptor, cancellationTokenSource.Token));
                // ReSharper restore AccessToDisposedClosure

                {
                    using var lck = vault.UpgradableRoLockBlockUntilAcquired();
                    thread.Start();
                    Thread.SpinWait(100_000);
                    startedAt = token.SetOrThrow();
                    Thread.Sleep(TimeSpan.FromMilliseconds(250));
                    cancellationTokenSource.Cancel();
                    using var writeLock = lck.LockBlockUntilAcquired();
                    writeLock.Append("Hi mom!");
                    thread.Join();
                }

                {
                    using var lck = vault.Lock();
                    lck.Append("... it worked?!");
                    finalResult = lck.ToString();
                }

                Assert.NotNull(receptor.SuppliedException);
                Assert.False(receptor.IsBadException);
                Assert.True(typeof(OperationCanceledException) == receptor.ExceptionType);
                Assert.True(receptor.Ts > startedAt);
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
            Helper.WriteLine($"Cancellation thrown after {(receptor.Ts - startedAt).TotalMilliseconds:F3} milliseconds.");

            static void DoThreadTwo(VaultType bv, StartToken tkn, ExceptionReceptor receptor, CancellationToken token)
            {
                HpTimeStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }
                try
                {
                    using var lck = bv.UpgradableRoLock(TimeSpan.FromMinutes(3600), token);
                    receptor.SetBadException();
                }
                catch (OperationCanceledException ex)
                {
                    receptor.SupplyExceptionOrThrow(ex);
                }
                catch (Exception rx)
                {
                    receptor.SupplyExceptionOrThrow(rx);
                }
            }
        }

        [Fact]
        public void TestThrowsTimeoutWhenTimeoutBeforeCancel()
        {
            StartToken token = new StartToken();
            ResourceType finalResult;
            ExceptionReceptor receptor = new ExceptionReceptor();
            DateTime startedAt;
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var vault = _vaultGen())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), ResourceType.Empty);

                // ReSharper disable AccessToDisposedClosure
                Thread thread = new Thread(() => DoThreadTwo(vault, token, receptor, cancellationTokenSource.Token));
                // ReSharper restore AccessToDisposedClosure

                {
                    using var lck = vault.UpgradableRoLock();
                    thread.Start();
                    Thread.SpinWait(100_000);
                    startedAt = token.SetOrThrow();
                    Thread.Sleep(TimeSpan.FromMilliseconds(550));
                    cancellationTokenSource.Cancel();
                    using var writeLock = lck.Lock();
                    writeLock.Append("Hi mom!");
                    thread.Join();
                }

                {
                    using var lck = vault.Lock();
                    lck.Append("... it worked?!");
                    finalResult = lck.ToString();
                }

                Assert.NotNull(receptor.SuppliedException);
                Assert.False(receptor.IsBadException);
                Assert.True(typeof(TimeoutException) == receptor.ExceptionType);
                Assert.True(receptor.Ts > startedAt);
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
            Helper.WriteLine($"Cancellation thrown after {(receptor.Ts - startedAt).TotalMilliseconds:F3} milliseconds.");

            static void DoThreadTwo(VaultType bv, StartToken tkn, ExceptionReceptor receptor, CancellationToken token)
            {
                HpTimeStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }
                try
                {
                    using var lck = bv.UpgradableRoLock(TimeSpan.FromMilliseconds(100), token);
                    receptor.SetBadException();
                }
                catch (OperationCanceledException ex)
                {
                    receptor.SupplyExceptionOrThrow(ex);
                }
                catch (Exception rx)
                {
                    receptor.SupplyExceptionOrThrow(rx);
                }
            }
        }

        private const string StartingText = "Hello, world!";
        private readonly Func<VaultType> _vaultGen = () =>
            new VaultType(TimeSpan.FromMilliseconds(250),
                () => new StringBuilder(StartingText));
    }
}
