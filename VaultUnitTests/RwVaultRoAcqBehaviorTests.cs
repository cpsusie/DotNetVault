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


    public sealed class RwVaultRoAcqBehaviorTests : VaultAcqBehaviorTest
    {
        public RwVaultRoAcqBehaviorTests([NotNull] ITestOutputHelper helper, [NotNull] VaultFactoryFixture fixture) :
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
                    using var lck = vault.RoLock();
                    Assert.Equal(text, lck.Value);
                }

                {
                    using var lck = vault.RoLock();
                    try
                    {
                        // ReSharper disable once UnusedVariable
                        ResourceType shouldntWork = vault.CopyCurrentValue(TimeSpan.FromMilliseconds(100));
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
                    finalResult = finalLck.Value;
                }
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
                Assert.Equal(finalResult, text);
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
                        using var lck = vault.RoLock();
                        using var lck2 = vault.RoLock();
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
                HpTimesStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }

                using var lck = bmv.RoLock();
                Thread.Sleep(TimeSpan.FromMilliseconds(750));
                Assert.Equal(lck.Value, shouldBe);
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
                HpTimesStamps.TimeStampSource.Calibrate();
                while (!tkn.IsSet) { }
                try
                {
                    using var lck = bv.RoLock(token);
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
            string text = "Hello, world!";
            string finalResult;
            using (var vault = _meth(TimeSpan.FromMilliseconds(250)))
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(10), text);

                {
                    using var lck = vault.RoLockBlockUntilAcquired();
                    Assert.Equal(lck.Value, text);
                }
                {
                    using var lck = vault.RoLockBlockUntilAcquired();
                    Assert.Equal(lck.Value, text);
                }
                Assert.Throws<RwLockAlreadyHeldThreadException>(() =>
                {
                    using var lckOne = vault.RoLockBlockUntilAcquired();
                    using var lckTwo = vault.RoLockBlockUntilAcquired();
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
                    using var lck = bv.RoLock(TimeSpan.FromMinutes(3600), token);
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
                    using var lck = bv.RoLock(TimeSpan.FromMilliseconds(100), token);
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
