using System;
using System.Text;
using System.Threading;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;

namespace VaultUnitTests
{
    public class TestAtomicMrv : IClassFixture<VaultFactoryFixture>
    {
        [NotNull] public ITestOutputHelper Helper { get; }
        [NotNull] public VaultFactoryFixture Fixture { get; }

        public TestAtomicMrv([NotNull] ITestOutputHelper helper, [NotNull] VaultFactoryFixture fixture)
        {
            Helper = helper ?? throw new ArgumentNullException(nameof(helper));
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }
        
        [Fact]
        public void TestMrvAcquireRelease()
        {
            string result;
            using (var vault = Fixture.CreateMrv(() => new StringBuilder()))
            {
                for (int i = 0; i < 3; ++i)
                {
                    {
                        int count = i + 1;
                        using var lck = vault.SpinLock();
                        lck.ExecuteAction(((ref StringBuilder res) => res.AppendLine($"Hi mom # {count.ToString()}")));
                    }
                }

                using var finalLck = vault.SpinLock();
                result = finalLck.ExecuteQuery((in StringBuilder res) => res.ToString());
            }
            Helper.WriteLine(result);
        }

        [Fact]
        public void TestBvAcquireRelease()
        {
            string finalResult;
            using (var vault = Fixture.CreateBasicVault<string>())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), string.Empty);

                for (int i = 0; i < 3; ++i)
                {
                    using var lck = vault.SpinLock();
                    lck.Value += $"Hi mom # {(i + 1).ToString()}{Environment.NewLine}";
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
        public void TestBMonVAcquireRelease()
        {
            string finalResult;
            using (var vault = Fixture.CreateBasicMonitorVault<string>())
            {
                vault.SetCurrentValue(TimeSpan.FromMilliseconds(100), string.Empty);

                for (int i = 0; i < 3; ++i)
                {
                    using var lck = vault.SpinLock();
                    lck.Value += $"Hi mom # {(i + 1).ToString()}{Environment.NewLine}";
                }

                {
                    using var finalLck = vault.SpinLock();
                    finalResult = finalLck.Value;
                }
                Assert.Equal(finalResult, vault.CopyCurrentValue(TimeSpan.FromMilliseconds(10)));
            }
            Helper.WriteLine(finalResult);
        }
   
    }

    sealed class ExceptionReceptor
    {
        [CanBeNull]
        public Exception SuppliedException
        {
            get
            {
                Exception e = _ex;
                return e;
            }
        }

        public bool IsBadException => SuppliedException == BadException;

        [CanBeNull] public Type ExceptionType => SuppliedException?.GetType();

        public DateTime Ts => _ts;

        public void SupplyExceptionOrThrow([NotNull] Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            var shouldBeNull = Interlocked.CompareExchange(ref _ex, ex, null);
            if (shouldBeNull == null)
            {
                _ts = HpTimeStamps.TimeStampSource.Now;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public void SetBadException()
        {
            var shouldBeNull = Interlocked.CompareExchange(ref _ex, BadException, null);
            if (shouldBeNull == null)
            {
                _ts = HpTimeStamps.TimeStampSource.Now;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        private volatile Exception _ex;
        private DateTime _ts;
        public static readonly Exception BadException = new Exception();
    }

    sealed class StartToken
    {
        public bool IsSet
        {
            get
            {
                int val = _value;
                return val != Clear;
            }
        }

        public bool TrySet()
        {
            const int wantToBe = Set;
            const int needToBeNow = Clear;
            return Interlocked.CompareExchange(ref _value, wantToBe, needToBeNow) == needToBeNow;
        }

        public DateTime SetOrThrow()
        {
            HpTimeStamps.TimeStampSource.Calibrate();
            if (TrySet())
            {
                return HpTimeStamps.TimeStampSource.Now;
            }
            throw new InvalidOperationException();
        }

        private volatile int _value;
        private const int Clear = 0;
        private const int Set = 1;
    }
}
