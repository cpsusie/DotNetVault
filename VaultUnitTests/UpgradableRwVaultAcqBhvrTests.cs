using System;
using System.Threading;
using DotNetVault;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using ResourceType = System.String;
namespace VaultUnitTests
{
    using VaultType = DotNetVault.Vaults.BasicReadWriteVault<ResourceType>;

    public sealed class ReadOnlyUpgradableRwLockedResource : VaultAcqBehaviorTest
    {
        public ReadOnlyUpgradableRwLockedResource([NotNull] ITestOutputHelper helper, [NotNull] VaultFactoryFixture fixture) :
            base(helper, fixture)
        {
            _meth = (ts) => Fixture.CreateBasicReadWriteVault<ResourceType>(ts);
        }


        [Fact]
        public void TestThrowsAlready()
        {
            ResourceType text = "Hello, world!";
            ResourceType finalResult;
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), text);

                for (int i = 0; i < 3; ++i)
                {
                    using var lck = vault.UpgradableRoLockBlockUntilAcquired();
                    {
                        using var writeLock = lck.LockWaitForever();
                        writeLock.Value += (Environment.NewLine) + "Addition number: " + (i + 1);
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
                    finalResult = finalLck.Value;
                }
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
        }

        [Fact]
        public void ThrowsDoubleLockAlready()
        {
            ResourceType finalResult;
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), ResourceType.Empty);

                for (int i = 0; i < 3; ++i)
                {
                    //RwLockedResource<VaultType, string> writeLock = default;
                    string appendMe = $"Hi mom # {(i + 1).ToString()}{Environment.NewLine}";
                    using var lck = vault.UpgradableRoLock();
                    {
                        //using (writeLock = lck.Lock()) // correct should not work
                        //{
                        //    writeLock.Value += appendMe;
                        //}
                        using var wl = lck.Lock();
                        wl.Value += appendMe;
                    }
                    Assert.EndsWith(appendMe, lck.Value);
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
                    finalResult = finalLck.Value;
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
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
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
                    lck.Value += "... it worked?!";
                    finalResult = lck.Value;
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
                Assert.Equal(lck.Value, shouldBe);
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
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
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
                    lck.Value += "Hi mom!";
                    thread.Join();
                }

                {
                    using var lck = vault.Lock();
                    lck.Value += "... it worked?!";
                    finalResult = lck.Value;
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
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(10), startingText);

                {
                    using var lck = vault.UpgradableRoLockBlockUntilAcquired();
                    {
                        using var wl1 = lck.LockWaitForever();
                        wl1.Value += individAppend;
                    }
                    Assert.EndsWith(individAppend, lck.Value);
                    {
                        using var wl2 = lck.LockWaitForever();
                        wl2.Value += individAppend;
                    }
                    Assert.EndsWith(concatenated, lck.Value);
                }
                {
                    using var lck = vault.RoLockBlockUntilAcquired();
                    Assert.Equal(finalShouldBe, lck.Value);
                }
                Assert.Throws<RwLockAlreadyHeldThreadException>(() =>
                {
                    using var lckOne = vault.UpgradableRoLockBlockUntilAcquired();
                    using var lckTwo = vault.UpgradableRoLockBlockUntilAcquired();
                });

                {
                    using var lck = vault.RoLockBlockUntilAcquired();
                    finalResult = lck.Value;
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
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
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
                    using var writeLock = lck.LockWaitForever();
                    writeLock.Value += "Hi mom!";
                    thread.Join();
                }

                {
                    using var lck = vault.Lock();
                    lck.Value += "... it worked?!";
                    finalResult = lck.Value;
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
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
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
                    writeLock.Value += "Hi mom!";
                    thread.Join();
                }

                {
                    using var lck = vault.Lock();
                    lck.Value += "... it worked?!";
                    finalResult = lck.Value;
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
                    using var lck = bv.UpgradableRoLock( TimeSpan.FromMilliseconds(100), token);
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

        [NotNull] private readonly RwVaultCreationMethod _meth;
    }
}
