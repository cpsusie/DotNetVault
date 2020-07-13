using System;
using System.Threading;
using System.Diagnostics;
using DotNetVault.ClortonGame;
using DotNetVault.Vaults;
using JetBrains.Annotations;
using UpgrLockedRes = DotNetVault.LockedResources.UpgradableRoValListLockedResource<DotNetVault.Vaults.ReadWriteValueListVault<DotNetVault.DeadBeefCafeBabeGame.UInt256>, DotNetVault.DeadBeefCafeBabeGame.UInt256>;
namespace DotNetVault.DeadBeefCafeBabeGame
{
    /// <summary>
    /// Arbiter thread that participates in the <see cref="DeadBeefCafeBabeGameBase"/>.
    /// </summary>
    public sealed class ArbiterThread : CafeBabeGameThread
    {
        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="vault">the vault</param>
        /// <param name="helper">the output helper</param>
        /// <exception cref="ArgumentNullException"><paramref name="vault"/> or <paramref name="helper"/> was null.</exception>
        public ArbiterThread([NotNull] ReadWriteValueListVault<UInt256> vault, [NotNull] IOutputHelper helper) : base(
            vault, helper) { }

        /// <inheritdoc />
        protected override void ExecuteJob(CancellationToken token)
        {
            while (true)
            {
                try
                {
                    //gets an upgradable read only lock
                    using (var lck = _valueList.UpgradableRoLock(token))
                    {
                        (bool terminationConditionFound, string message) =
                            DoesTerminationApply(in lck);
                        if (terminationConditionFound)
                        {
                            int insertionIndex = _rGen.Next(0, lck.Count);
                            //use the lock like you would a vault (i.e. call .Lock() ) to upgrade to an exclusive
                            //write lock
                            using var writeLock = lck.Lock(token);
                            writeLock.Insert(insertionIndex, in LookFor);
                            _helper.WriteLine("At {0} Arbiter wrote {1} at index {2}.", CgTimeStampSource.Now.ToString("O") ,LookFor.ToString(),
                                insertionIndex.ToString());
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

        /// <inheritdoc /> //does nothing special
        protected override void PerformFinishingActions() { }

        /// <inheritdoc />
        protected override Thread InitThread() => new Thread(ThreadLoop) { IsBackground = true, Name = "ArbiterThread" };

        private (bool Applies, string Message) DoesTerminationApply(in UpgrLockedRes lck)
        {
            bool ret;
            string message;

            (int firstCharCount, int secondCharCount) = GetFrequencyHistogram(in lck);
            int comparisonResult = firstCharCount.CompareTo(secondCharCount);
            
            if (comparisonResult == 0)
            {
                ret = false; //doesn't apply ... zero doesn't count
                message = "Char histogram shows equality; condition does not apply.";
            }
            else if (comparisonResult < 0) //second is greater
            {
                int difference = secondCharCount - firstCharCount;
                ret = difference % 13 == 0;
                string baseMessage =
                    $"There were {firstCharCount} instances of {GameConstants.XNumber.ToString()} and {secondCharCount} instances of {GameConstants.ONumber.ToString()}.";
                message = baseMessage + (ret ?
                    $"  The condition DOES apply: their difference {difference.ToString()} is evenly divisible by 13."
                    : $"  The condition DOES NOT apply: their difference {difference.ToString()} IS NOT divisible by 13");
            }
            else //first greater
            {
                int difference = firstCharCount - secondCharCount;
                ret = difference % 13 == 0;
                string baseMessage =
                    $"There were {firstCharCount} instances of {GameConstants.XNumber.ToString()} and {secondCharCount} instances of {GameConstants.ONumber.ToString()}.";
                message = baseMessage + (ret ?
                    $"  The condition DOES apply: their difference {difference.ToString()} is evenly divisible by 13."
                    : $"  The condition DOES NOT apply: their difference {difference.ToString()} IS NOT divisible by 13");
            }

            return (ret, message);

        }

        private (int FirstNumberCount, int SecondNumberCount) GetFrequencyHistogram(in UpgrLockedRes lck)
        {
            int firstCount = 0;
            int secondCount = 0;
            ref readonly var xNumber = ref GameConstants.XNumber;
            ref readonly var oNumber = ref GameConstants.ONumber;
            foreach (ref readonly var itm in lck)
            {
                if (itm == xNumber) ++firstCount;
                else if (itm == oNumber) ++secondCount;
            }
            Debug.Assert(firstCount + secondCount == lck.Count, "firstCount + secondCount == lck.Count");
            return (firstCount, secondCount);
        }

        [NotNull] private readonly Random _rGen = new Random();
    }
}
