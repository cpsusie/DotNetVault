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

    public delegate VaultType RwVaultCreationMethod(TimeSpan? timeout);
    public sealed class RwVaultWriteAcqBehaviorTests : VaultAcqBehaviorTest
    {
        public RwVaultWriteAcqBehaviorTests([NotNull] ITestOutputHelper helper, [NotNull] VaultFactoryFixture fixture) :
            base(helper, fixture)
        {
            _meth = (ts) => Fixture.CreateBasicReadWriteVault<ResourceType>(ts);
        }


        [Fact]
        public void TestThrowsAlready()
        {
            ResourceType finalResult;
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), ResourceType.Empty);

                for (int i = 0; i < 3; ++i)
                {
                    using var lck = vault.Lock();
                    lck.Value += $"Hi mom # {(i + 1).ToString()}{Environment.NewLine}";
                }

                {
                    using var lck = vault.Lock();
                    try
                    {
                        // ReSharper disable once UnusedVariable
                        ResourceType shouldntWork = vault.CopyCurrentValue(TimeSpan.FromMilliseconds(100));
                        throw new ThrowsException(typeof(LockAlreadyHeldThreadException));
                    }
                    catch (LockAlreadyHeldThreadException)
                    {

                    }
                    catch (ThrowsException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new ThrowsException(typeof(LockAlreadyHeldThreadException), ex);
                    }
                }
                {
                    using var finalLck = vault.SpinLock();
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
                    using var lck = vault.Lock();
                    lck.Value += $"Hi mom # {(i + 1).ToString()}{Environment.NewLine}";
                }

                {
                    Assert.Throws<RwLockAlreadyHeldThreadException>(() =>
                    {
                        using var lck = vault.Lock();
                        using var lck2 = vault.Lock();
                    });
                }
                {
                    using var finalLck = vault.SpinLock();
                    finalResult = finalLck.Value;
                }
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
        }

        [Fact]
        public void TestThrowsTimeout()
        {
            StartToken token = new StartToken();
            ResourceType finalResult;
            ExceptionReceptor receptor = new ExceptionReceptor();
            DateTime startedAt;
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), ResourceType.Empty);

                // ReSharper disable AccessToDisposedClosure
                Thread firstThread = new Thread(() => DoThreadOne(vault, token));
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
            static void DoThreadOne(VaultType bmv, StartToken tkn)
            {
                HpTimesStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }

                using var lck = bmv.Lock();
                Thread.Sleep(TimeSpan.FromMilliseconds(750));
                lck.Value += "Hi mom!";
            }

            static void DoThreadTwo(VaultType bv, StartToken tkn, ExceptionReceptor receptor)
            {
                HpTimesStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }
                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                try
                {
                    using var lck = bv.Lock(TimeSpan.FromMilliseconds(10));
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
                HpTimesStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }
                try
                {
                    using var lck = bv.Lock(token);
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
            string finalResult;
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(10), string.Empty);

                {
                    using var lck = vault.LockBlockUntilAcquired();
                    lck.Value += "Hello, ";
                }
                {
                    using var lck = vault.LockBlockUntilAcquired();
                    lck.Value += "World!\n";
                }
                Assert.Throws<RwLockAlreadyHeldThreadException>(() =>
                {
                    using var lckOne = vault.LockBlockUntilAcquired();
                    using var lckTwo = vault.LockBlockUntilAcquired();
                    lckTwo.Value += "This won't appear.";
                });

                {
                    using var lck = vault.LockBlockUntilAcquired();
                    finalResult = lck.Value;
                }
                Assert.True(finalResult == vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
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
                HpTimesStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }
                try
                {
                    using var lck = bv.Lock(TimeSpan.FromMinutes(3600), token);
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
                    using var lck = vault.Lock();
                    thread.Start();
                    Thread.SpinWait(100_000);
                    startedAt = token.SetOrThrow();
                    Thread.Sleep(TimeSpan.FromMilliseconds(550));
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
                Assert.True(typeof(TimeoutException) == receptor.ExceptionType);
                Assert.True(receptor.Ts > startedAt);
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
            Helper.WriteLine($"Cancellation thrown after {(receptor.Ts - startedAt).TotalMilliseconds:F3} milliseconds.");

            static void DoThreadTwo(VaultType bv, StartToken tkn, ExceptionReceptor receptor, CancellationToken token)
            {
                HpTimesStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }
                try
                {
                    using var lck = bv.Lock(TimeSpan.FromMilliseconds(100), token);
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
