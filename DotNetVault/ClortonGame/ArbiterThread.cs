using System;
using System.Threading;
using DotNetVault.CustomVaultExamples.CustomLockedResources;
using DotNetVault.Interfaces;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using TimeStampSource = DotNetVault.ClortonGame.CgTimeStampSource;
namespace DotNetVault.ClortonGame
{
    /// <summary>
    /// An arbiter thread configured for use in the clorton game with the <see cref="ReadWriteStringBufferVault"/>.
    /// </summary>
    public sealed class StringBufferVaultArbiterThread : ArbiterThread<ReadWriteStringBufferVault>
    {
        /// <inheritdoc />
        public StringBufferVaultArbiterThread([NotNull] ReadWriteStringBufferVault vault, [NotNull] IOutputHelper helper) : base(vault, helper)
        {
        }

        /// <inheritdoc />
        protected override void ExecuteJob(CancellationToken token)
        {
            while (true)
            {
                try
                {
                    //gets an upgradable read only lock
                    using (var lck = _vault.UpgradableRoLock(token))
                    {
                        (bool terminationConditionFound, string message) =
                            DoesTerminationConditionApply(in lck, 'x', 'o');
                        if (terminationConditionFound)
                        {
                            //use the lock like you would a vault (i.e. call .Lock() ) to upgrade to an exclusive
                            //write lock
                            using var writeLock = lck.Lock(token);
                            writeLock.Append(LookFor);
                            _helper.WriteLine("Arbiter wrote {0}.", LookFor);
                            _helper.WriteLine(message);
                            return; //the write lock is released, then the upgradable read lock.
                        }
                    }
                }
                catch (TimeoutException ex) //log timeout exception, but it isn't a fault (I've not observed it yet here)
                {
                    _helper.WriteLine("Arbiter thread timed out with ex: [{0}].",
                        ex);
                }
                catch (OperationCanceledException) //cancel the thread (not a fault, but terminates) 
                {
                    _helper.WriteLine("Arbiter thread cancelled.");
                    throw;
                }
                catch (Exception ex) //faulting exception
                {
                    _helper.WriteLine("Arbiter faulted with ex: [{0}]", ex);
                    throw;
                }
            }
        }

        private (bool, string) DoesTerminationConditionApply(in StringBuilderUpgradableRoLockedResource lck, char firstChar, char secondChar)
        {
            bool ret;
            string message;

            //examine the string for counts of first and second and determine the <, >, == of them
            (int firstCharCount, int secondCharCount) = GetHistogram(in lck, firstChar, secondChar);
            int comparisonResults = firstCharCount.CompareTo(secondCharCount);

            if (comparisonResults == 0)
            {
                ret = false; //doesn't apply ... zero doesn't count
                message = "Char histogram shows equality; condition does not apply.";
            }
            else if (comparisonResults < 0) //second is greater
            {
                int difference = secondCharCount - firstCharCount;
                ret = difference % 13 == 0;
                string baseMessage =
                    $"There were {firstCharCount.ToString()} instances of {firstChar.ToString()} and {secondCharCount.ToString()} instances of {secondChar.ToString()}.";
                message = baseMessage + (ret ?
                    $"  The condition DOES apply: their difference {difference.ToString()} is evenly divisible by 13."
                    : $"  The condition DOES NOT apply: their difference {difference.ToString()} IS NOT divisible by 13");
            }
            else //first greater
            {
                int difference = firstCharCount - secondCharCount;
                ret = difference % 13 == 0;
                string baseMessage =
                    $"There were {firstCharCount.ToString()} instances of {firstChar.ToString()} and {secondCharCount.ToString()} instances of {secondChar.ToString()}.";
                message = baseMessage + (ret ?
                    $"  The condition DOES apply: their difference {difference.ToString()} is evenly divisible by 13."
                    : $"  The condition DOES NOT apply: their difference {difference.ToString()} IS NOT divisible by 13");
            }
            return (ret, message);
        }

        private static (int FirstCharCount, int SecondCharCount) GetHistogram(in StringBuilderUpgradableRoLockedResource lck, char firstChar, char secondChar)
        {
            int firstCharCount = 0;
            int secondCharCount = 0;
            for (int i = 0; i < lck.Length; ++i)
            {
                char test = lck[i];
                if (test == firstChar)
                    ++firstCharCount;
                else if (test == secondChar)
                    ++secondCharCount;
            }

            return (firstCharCount, secondCharCount);
        }
    }

    /// <summary>
    /// An arbiter thread configured for use in the clorton game with the <see cref="BasicReadWriteVault{T}"/>
    /// </summary>
    public sealed class BasicVaultArbiterThread : ArbiterThread<BasicReadWriteVault<string>>
    {
        /// <inheritdoc />
        public BasicVaultArbiterThread([NotNull] BasicReadWriteVault<string> vault, [NotNull] IOutputHelper helper) : base(vault, helper)
        {
        }

        /// <inheritdoc />
        protected override void ExecuteJob(CancellationToken token)
        {
            while (true)
            {
                try
                {
                    //gets an upgradable read only lock
                    using (var lck = _vault.UpgradableRoLock(token))
                    {
                        (bool terminationConditionFound, string message) =
                            DoesTerminationConditionApply(lck.Value, 'x', 'o');
                        if (terminationConditionFound)
                        {
                            //use the lock like you would a vault (i.e. call .Lock() ) to upgrade to an exclusive
                            //write lock
                            using var writeLock = lck.Lock(token);
                            writeLock.Value += LookFor;
                            _helper.WriteLine("Arbiter wrote {0}.", LookFor);
                            _helper.WriteLine(message);
                            return; //the write lock is released, then the upgradable read lock.
                        }
                    }
                }
                catch (TimeoutException ex) //log timeout exception, but it isn't a fault (I've not observed it yet here)
                {
                    _helper.WriteLine("Arbiter thread timed out with ex: [{0}].",
                        ex);
                }
                catch (OperationCanceledException) //cancel the thread (not a fault, but terminates) 
                {
                    _helper.WriteLine("Arbiter thread cancelled.");
                    throw;
                }
                catch (Exception ex) //faulting exception
                {
                    _helper.WriteLine("Arbiter faulted with ex: [{0}]", ex);
                    throw;
                }
            }
        }

        #region Private Methods
        private static (bool Applies, string Message) DoesTerminationConditionApply([NotNull] string s, char firstChar, char secondChar)
        {
            bool ret;
            string message;

            //examine the string for counts of first and second and determine the <, >, == of them
            (int firstCharCount, int secondCharCount) = GetHistogram(s, firstChar, secondChar);
            int comparisonResults = firstCharCount.CompareTo(secondCharCount);

            if (comparisonResults == 0)
            {
                ret = false; //doesn't apply ... zero doesn't count
                message = "Char histogram shows equality; condition does not apply.";
            }
            else if (comparisonResults < 0) //second is greater
            {
                int difference = secondCharCount - firstCharCount;
                ret = difference % 13 == 0;
                string baseMessage =
                    $"There were {firstCharCount.ToString()} instances of {firstChar.ToString()} and {secondCharCount.ToString()} instances of {secondChar.ToString()}.";
                message = baseMessage + (ret ?
                    $"  The condition DOES apply: their difference {difference.ToString()} is evenly divisible by 13."
                    : $"  The condition DOES NOT apply: their difference {difference.ToString()} IS NOT divisible by 13");
            }
            else //first greater
            {
                int difference = firstCharCount - secondCharCount;
                ret = difference % 13 == 0;
                string baseMessage =
                    $"There were {firstCharCount.ToString()} instances of {firstChar.ToString()} and {secondCharCount.ToString()} instances of {secondChar.ToString()}.";
                message = baseMessage + (ret ?
                    $"  The condition DOES apply: their difference {difference.ToString()} is evenly divisible by 13."
                    : $"  The condition DOES NOT apply: their difference {difference.ToString()} IS NOT divisible by 13");
            }
            return (ret, message);
        }

        private static (int FirstCharCount, int SecondCharCount) GetHistogram([NotNull] string s, char firstChar, char secondChar)
        {
            int firstCharCount = 0;
            int secondCharCount = 0;
            for (int i = 0; i < s.Length; ++i)
            {
                char test = s[i];
                if (test == firstChar)
                    ++firstCharCount;
                else if (test == secondChar)
                    ++secondCharCount;
            }

            return (firstCharCount, secondCharCount);
        }
        #endregion
    }

    /// <summary>
    /// This thread demonstrates usage of the upgradable read-only lock.
    /// It
    ///     1- obtains an upgradable readonly lock
    ///     2- checks for the termination condition:
    ///      (the absolute value of the difference between the 'x' count and 'o' count being non-zero
    ///      and evenly divisible by 13)
    ///     3- if the termination condition DOES apply, it:
    ///             -upgrades its lock to a write lock.
    ///             -writes "CLORTON" into the buffer
    ///             -releases its locks (releases write lock, then releases upgradable read lock)
    ///         otherwise it simply releases its upgradable read lock.
    /// </summary>
    public abstract class ArbiterThread<TVault> : ClortonGameThread<TVault> where TVault : IBasicVault<string>
    {
        #region CTOR
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="vault">A string vault</param>
        /// <param name="helper">Output helper for logging</param>
        protected ArbiterThread([NotNull] TVault vault, [NotNull] IOutputHelper helper)
            : base(vault, helper) { } 
        #endregion

        #region Methods
        /// <inheritdoc />
        protected sealed override void PerformFinishingActions() { }

        /// <inheritdoc />
        protected sealed override Thread InitThread() =>
            new Thread(ThreadLoop) { IsBackground = true, Name = "ArbiterThread" }; 
        #endregion

    }
}