using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Exceptions;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    /// <summary>
    /// Exemplifies the use-case of marking something as vault safe that is not vault safe because 1- it uses its own vault-safety mechanisms for
    /// its privates and 2- it's public members only expose and operate on vault safe values.  If stored in a vault, there is no (other than Reflection
    /// or something else that reeks equally) reasonable way for the user  to gain access to the non-vault safe "Random" class and leak it out and as
    /// far as the setting of its own internal fields go, it provides
    /// it's own synchro mechanism.  The downside to this approach is that if a maintainer somehow exposes some new shared mutable state (currently Random)
    /// the vault will be unable to fully protect it.  Also, if a maintainer breaks the internal synchronization mechanisms, the vault will not be
    /// able to protect that.  You should carefully inspect any changes made to types that are marked with the <see cref="VaultSafeAttribute"/> with its
    /// first parameter set to true.  It disables analysis of the actual Vault-Safety of the type and while it may currently (to the best of my knowledge
    /// and without considering reflection or perhaps unsafe code) be vault-safe, there is no compile-time way to check that it has remained so.
    /// </summary>
    /// <remarks>
    /// A future feature might give more options than just "on faith" level of analysis.  Open to suggestions.
    /// </remarks>
    [VaultSafe(true)]
    internal sealed class LocklessRandomNumberGenerator
    {
        internal static LocklessRandomNumberGenerator CreateInstance() =>
            new LocklessRandomNumberGenerator(() => new Random());
        internal static LocklessRandomNumberGenerator CreateInstance(int seed) => new LocklessRandomNumberGenerator(() => new Random(seed));

        public int Next() => Value.Next();

        public int Next(int maxValue) => Value.Next(maxValue);

        public int Next(int minValue, int maxValue) => Value.Next(minValue, maxValue);

        public void NextBytes(byte[] bytes) => Value.NextBytes(bytes);

        public double NextDouble() => Value.NextDouble();
        
        private Random Value
        {
            get
            {
                Random ret = _rgen;
                if (ret == null)
                {
                    Random temp;
                    temp = InitNewRet();
                    Debug.Assert(temp != null);
                    Interlocked.CompareExchange(ref _rgen, temp, null);
                    ret = _rgen;
                }
                Debug.Assert(ret != null);
                return ret;
            }
        }

        private Random InitNewRet()
        {
            try
            {
                Random newR = _randomNumberGenerator();
                if (newR == null)
                    throw new DelegateReturnedNullException<Func<Random>>(_randomNumberGenerator,
                        nameof(_randomNumberGenerator));
                return newR;
            }
            catch (DelegateException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new DelegateThrewException<Func<Random>>(_randomNumberGenerator,
                    nameof(_randomNumberGenerator), e);
            }
        }

        private LocklessRandomNumberGenerator([NotNull] Func<Random> rgen) =>
            _randomNumberGenerator = rgen ?? throw new ArgumentNullException(nameof(rgen));

        private volatile Random _rgen;
        private readonly Func<Random> _randomNumberGenerator;
    }
}
