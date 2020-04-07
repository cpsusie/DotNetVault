using System;
using System.Text;
using System.Threading;
using DotNetVault;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace VaultUnitTests
{
    using ResourceType = String;
    using VaultType = ReadWriteStringBufferVault;
    public class SbVaultRwAcqTests
    {
        [NotNull] public ITestOutputHelper Helper { get; }

        public SbVaultRwAcqTests([NotNull] ITestOutputHelper helper) =>
            Helper = helper ?? throw new ArgumentNullException(nameof(helper));
        
        [Fact]
        public void TestThrowsAlready()
        {
            ResourceType finalResult;
            using (var vault = _vaultGen())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), ResourceType.Empty);

                for (int i = 0; i < 3; ++i)
                {
                    using var lck = vault.Lock();
                    lck.AppendLine($"Hi mom # {(i + 1)}");
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
                    using var lck = vault.Lock();
                    lck.AppendLine($"Hi mom # {(i + 1)}");
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
                    finalResult = finalLck.ToString();
                }
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
        }
        [Fact]
        public void TestThrowsOperationCancelled()
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
            using (var vault = _vaultGen())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(10), string.Empty);

                {
                    using var lck = vault.LockBlockUntilAcquired();
                    lck.Append("Hello, ");
                }
                {
                    using var lck = vault.LockBlockUntilAcquired();
                    lck.Append("World!\n");
                }
                Assert.Throws<RwLockAlreadyHeldThreadException>(() =>
                {
                    using var lckOne = vault.LockBlockUntilAcquired();
                    using var lckTwo = vault.LockBlockUntilAcquired();
                    lckTwo.Append("This won't appear.");
                });

                {
                    using var lck = vault.LockBlockUntilAcquired();
                    finalResult = lck.ToString();
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
            using (var vault = _vaultGen())
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
            using (var vault = _vaultGen())
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


        private const string StartingText = "Hello, world!";
        private readonly Func<VaultType> _vaultGen = () =>
            new VaultType(TimeSpan.FromMilliseconds(250), () => new StringBuilder(StartingText));
    }
}
