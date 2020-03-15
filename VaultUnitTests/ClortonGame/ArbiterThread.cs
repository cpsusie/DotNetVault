using System;
using System.Threading;
using DotNetVault.Vaults;
using HpTimesStamps;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace VaultUnitTests.ClortonGame
{
    sealed class ArbiterThread : ClortonGameThread
    {
        public ArbiterThread([NotNull] BasicReadWriteVault<string> vault, [NotNull] ITestOutputHelper helper) : base(vault, helper)
        {
        }

        protected override void ExecuteJob(CancellationToken token)
        {
            while (true)
            {
                try
                {
                    DateTime writeTs;
                    using (var lck = _vault.UpgradableRoLock(token))
                    {
                        (bool terminationConditionFound, string message) =
                            DoesTerminationConditionApply(lck.Value, 'x', 'o');
                        if (terminationConditionFound)
                        {
                            using var writeLock = lck.Lock(token);
                            writeTs = TimeStampSource.Now;
                            writeLock.Value += LookFor;
                            _helper.WriteLine("Arbiter wrote {0} at [{1:O}].", LookFor, writeTs);
                            _helper.WriteLine(message);
                            return;
                        }
                    }
                }
                catch (TimeoutException ex)
                {
                    DateTime ts = TimeStampSource.Now;
                    _helper.WriteLine("At [{0:O}], arbiter thread timed out with ex: [{1}].",
                        ts, ex);
                }
                catch (OperationCanceledException)
                {
                    DateTime ts = TimeStampSource.Now;
                    _helper.WriteLine("At [{0:O}], arbiter thread cancelled.", ts);
                    throw;
                }
                catch (Exception ex)
                {
                    DateTime ts = TimeStampSource.Now;
                    _helper.WriteLine("At [{0:O}], arbiter faulted with ex: [{1}]", ts, ex);
                    throw;
                }
            }
        }

        protected override void PerformFinishingActions()
        {
            
        }

        protected override Thread InitThread() => new Thread(ThreadLoop){IsBackground = true, Name = "ArbiterThread"};

        private static (bool Applies, string Message) DoesTerminationConditionApply([NotNull] string s, char firstChar, char secondChar)
        {
            DateTime ts = TimeStampSource.Now;
            bool ret;
            string message;
            (int firstCharCount, int secondCharCount) = GetHistogram(s, firstChar, secondChar);
            int comparisonResults = firstCharCount.CompareTo(secondCharCount);


            if (comparisonResults == 0)
            {
                ret = false;
                message = $"At [{ts:O}], Char histogram shows equality; condition does not apply.";
            }
            else if (comparisonResults < 0)
            {
                int difference = secondCharCount - firstCharCount;
                ret = difference % 13 == 0;
                string baseMessage =
                    $"At[{ts:O}], there were {firstCharCount.ToString()} instances of {firstChar.ToString()} and {secondCharCount.ToString()} instances of {secondChar.ToString()}.";
                message = baseMessage + (ret ? 
                    $"  The condition DOES apply: their difference {difference.ToString()} is evenly divisible by 13."
                    : $"  The condition DOES NOT apply: their difference {difference.ToString()} IS NOT divisible by 13");
            }
            else
            {
                int difference =  firstCharCount - secondCharCount;
                ret = difference % 13 == 0;
                string baseMessage =
                    $"At[{ts:O}], there were {firstCharCount.ToString()} instances of {firstChar.ToString()} and {secondCharCount.ToString()} instances of {secondChar.ToString()}.";
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

        
    }
}