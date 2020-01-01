using System;
using System.Collections.Immutable;
using System.Diagnostics;
using DotNetVault.Exceptions;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using InternalRGenType = LaundryMachine.LaundryCode.LocklessRandomNumberGenerator;

namespace LaundryMachine.LaundryCode
{
    using RGenVaultType = BasicVault<InternalRGenType>;
    public static class RandomNumberSource
    {
        public static readonly TimeSpan DefaultTimeout;

        internal static bool TrySetAlternateRGenFactory([NotNull] Func<TimeSpan, RGenVaultType> alternate) =>
            TheRGenVaultFactory.TrySet(alternate ?? throw new ArgumentNullException(nameof(alternate)));

        internal static RGenVaultType RGenVault
        {
            get
            {
                var value = TheRGenVault.TryGetValue().Value;
                if (value == null)
                {
                    try
                    {
                        var temp = RGenVaultFactory(DefaultTimeout);
                        if (temp == null)
                        {
                            throw new DelegateReturnedNullException<Func<TimeSpan, RGenVaultType>>(RGenVaultFactory,
                                nameof(RGenVaultFactory));
                        }

                        TheRGenVault.TrySet(temp);
                        value = TheRGenVault.Value;
                    }
                    catch (DelegateException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        throw new DelegateThrewException<Func<TimeSpan, RGenVaultType>>(RGenVaultFactory,
                            nameof(RGenVaultFactory), e);
                    }
                }
                Debug.Assert(value != null);
                return value;
            }
        }

        internal static Func<TimeSpan, RGenVaultType> RGenVaultFactory
        {
            get
            {
                Func<TimeSpan, RGenVaultType> ret = TheRGenVaultFactory.TryGetValue().Value;
                if (ret == null)
                {
                    Func<TimeSpan, RGenVaultType> temp = CreateRGenVault;
                    TheRGenVaultFactory.TrySet(temp);
                    ret = TheRGenVaultFactory.Value;
                }
                Debug.Assert(ret != null);
                return ret;
            }
        }

        public static int Next() => Next(DefaultTimeout);
        public static int Next(int maxValue) => Next(maxValue, DefaultTimeout);
        public static int Next(int minValue, int maxValue) => Next(minValue, maxValue, DefaultTimeout);
        public static ImmutableArray<byte> NextBytes(int count) => NextBytes(count, DefaultTimeout);
        public static void NextBytes(byte[] bytes) => NextBytes(bytes, DefaultTimeout);
        public static double NextDouble() => NextDouble(DefaultTimeout);

        public static int Next(TimeSpan timeout)
        {
            using var lck = RGenVault.SpinLock(timeout);
            return lck.Value.Next();
        }

        public static int Next(int maxValue, TimeSpan timeout)
        {
            using var lck = RGenVault.SpinLock(timeout);
            return lck.Value.Next(maxValue);
        }

        public static int Next(int minValue, int maxValue, TimeSpan timeout)
        {
            using var lck = RGenVault.SpinLock(timeout);
            return lck.Value.Next(minValue, maxValue);
        }

        public static ImmutableArray<byte> NextBytes(int count, TimeSpan timeout)
        {
            byte[] b = new byte[count];
            using var lck = RGenVault.SpinLock(timeout);
            lck.Value.NextBytes(b);
            return b.ToImmutableArray();
        }

        public static void NextBytes(byte[] bytes, TimeSpan timeout)
        {
            using var lck = RGenVault.SpinLock(timeout);
            lck.Value.NextBytes(bytes);
        }

        public static double NextDouble(TimeSpan timeout)
        {
            using var lck = RGenVault.SpinLock(timeout);
            return lck.Value.NextDouble();
        }
        
        private static RGenVaultType CreateRGenVault(TimeSpan ts) =>
            new RGenVaultType(InternalRGenType.CreateInstance(), ts);

        static RandomNumberSource()
        {
            DefaultTimeout = TimeSpan.FromSeconds(2);
            TheRGenVaultFactory = new LocklessWriteOnce<Func<TimeSpan, RGenVaultType>>();
            TheRGenVault = new LocklessWriteOnce<RGenVaultType>();
        }

       

        private static readonly LocklessWriteOnce<Func<TimeSpan, RGenVaultType>> TheRGenVaultFactory;
        private static readonly LocklessWriteOnce<RGenVaultType>
            TheRGenVault;

    }
}
