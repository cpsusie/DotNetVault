using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using DotNetVault.CodeExamples;
using DotNetVault.Exceptions;
using DotNetVault.Vaults;
using DotNetVault.VsWrappers;
using Xunit;

namespace VaultUnitTests
{
    public sealed class ValueListTestFixture
    {
        public (List<BigLifeRecord> Control, ReadWriteValueListVault<BigLifeRecord> Test,
            ImmutableArray<BigLifeRecord> Present, ImmutableArray<BigLifeRecord> NotPresent) CreateSortAndSetTestData(
                int tstControlCount, int numPresentSamples, int numNotPresent)
        {
            if (tstControlCount < 1) throw new ArgumentNotPositiveException<int>(nameof(tstControlCount), tstControlCount);
            if (numPresentSamples >= tstControlCount || numPresentSamples < 1)
                throw new ArgumentOutOfRangeException(nameof(numPresentSamples), numPresentSamples,
                    @$"Parameter must be positive and less than {nameof(tstControlCount)}.");
            if (numNotPresent >= tstControlCount || numNotPresent < 1)
                throw new ArgumentOutOfRangeException(nameof(numNotPresent), numNotPresent,
                    @$"Parameter must be positive and less than {nameof(tstControlCount)}.");
            SortedSet<BigLifeRecord> bigLifeRecords = new SortedSet<BigLifeRecord>(GetRandomRecords(tstControlCount *2));
            
            while (bigLifeRecords.Count < tstControlCount *2)
            {
                bigLifeRecords.Add(_rgen.GetRandomBigLifeRecordData());
            }
            List<BigLifeRecord> control = new List<BigLifeRecord>(bigLifeRecords.Take(tstControlCount));
            _rgen.Shuffle<List<BigLifeRecord>, BigLifeRecord>(control);
            var notPresent = bigLifeRecords.Skip(tstControlCount).Take(numNotPresent).ToList();
            List<BigLifeRecord> present = new List<BigLifeRecord>(control.Take(numPresentSamples));
            _rgen.Shuffle<List<BigLifeRecord>, BigLifeRecord>(notPresent);

            bigLifeRecords.Clear();
            HashSet<BigLifeRecord> controlSet = new HashSet<BigLifeRecord>(control);

            bool allInPresentInControl = controlSet.IsProperSupersetOf(present);
            bool noneInControl = !controlSet.Overlaps(notPresent) &&
                                 !(new HashSet<BigLifeRecord>(notPresent)).Overlaps(present);
            controlSet.Clear();
            if (!allInPresentInControl || !noneInControl) throw new Exception("Logic error in setting up test data!");
            var vault = new ReadWriteValueListVault<BigLifeRecord>(VsEnumerableWrapper<BigLifeRecord>.FromIEnumerable(control));
            using var lck = vault.RoLock();
            
            if (lck.Count != control.Count) throw new Exception("vault count != control count.");

            int ctrlCount = 0;
            foreach (ref readonly var record in lck)
            {
                if (record != control[ctrlCount++]) throw new Exception($"itm at idx {ctrlCount - 1} not equal.");
            }

            return (control, vault, present.ToImmutableArray(), notPresent.ToImmutableArray());

        }

        public (List<BigLifeRecord> ControlList, ReadWriteValueListVault<BigLifeRecord> Test)
            GetRandomBigLifeRecordTestSet(int count) => _rgen.CreateRandomBigLifeRecords(count);

        public (List<DateTime> DateTimesControl, ReadWriteValueListVault<DateTime>
            DateTimesTest) CreateSortedDayOfWeekTest()
        {
            DateTime today = DateTime.Today;


            var control = new List<DateTime>(new[]
            {

                today, today + TimeSpan.FromDays(1), today + TimeSpan.FromDays(2), today + TimeSpan.FromDays(3),
                today + TimeSpan.FromDays(4), today + TimeSpan.FromDays(5), today + TimeSpan.FromDays(6)
            });
        
            _rgen.Shuffle<List<DateTime>, DateTime>(control);
            var vault = new ReadWriteValueListVault<DateTime>(VsEnumerableWrapper<DateTime>.FromIEnumerable(control));
            Assert.Equal(vault.GetCurrentCount(), control.Count);
            using var lck = vault.RoLock();
            for (int i = 0; i < lck.Count; ++i)
            {
                Assert.True(lck[i] == control[i]);
            }

            return (control, vault);
        }


        public IEnumerable<BigLifeRecord> GetRandomRecords(int count)
        {
            int soFar = 0;
            while (soFar++ < count)
            {
                yield return _rgen.GetRandomBigLifeRecordData();
            }
        }

        public (List<DateTime> DateTimesControl, ReadWriteValueListVault<DateTime>
            DateTimesTest)
            GenerateRandomDtList(TimeSpan vaultTimeout, int dateTimeCount)
        {
            var control = new List<DateTime>(_rgen.GetRandomDateTimes(dateTimeCount));
            Assert.True(control.Count == dateTimeCount);
            var vault = new ReadWriteValueListVault<DateTime>(VsEnumerableWrapper<DateTime>.FromIEnumerable(control), vaultTimeout);
            using var lck = vault.RoLock();
            Assert.Equal(lck.Count, control.Count);
            using var controlEnumerator = control.GetEnumerator();
            var testEnumerator = lck.GetEnumerator();
            bool doNext;
            do
            {
                bool nextControl = controlEnumerator.MoveNext();
                bool nextTest = testEnumerator.MoveNext();
                Assert.Equal(nextTest, nextControl);
                doNext = nextTest;
                if (doNext)
                {
                    var controlVal = controlEnumerator.Current;
                    ref readonly var testVal = ref testEnumerator.Current;
                    Assert.True(controlVal == testVal);
                }
            } while (doNext);

            return (control, vault);
        }

        public (List<TimeSpan> TimeSpansControl, ReadWriteValueListVault<TimeSpan>
            TimeSpansTest)
            GenerateRandomTsList(TimeSpan vaultTimeout, int tsCount)
        {
            var control = new List<TimeSpan>(_rgen.GetRandomTimeSpans(tsCount));
            Assert.True(control.Count == tsCount);
            var vault = new ReadWriteValueListVault<TimeSpan>(VsEnumerableWrapper<TimeSpan>.FromIEnumerable(control), vaultTimeout);
            using var lck = vault.RoLock();
            Assert.Equal(lck.Count, control.Count);
            using var controlEnumerator = control.GetEnumerator();
            var testEnumerator = lck.GetEnumerator();
            bool doNext;
            do
            {
                bool nextControl = controlEnumerator.MoveNext();
                bool nextTest = testEnumerator.MoveNext();
                Assert.Equal(nextTest, nextControl);
                doNext = nextTest;
                if (doNext)
                {
                    var controlVal = controlEnumerator.Current;
                    ref readonly var testVal = ref testEnumerator.Current;
                    Assert.True(controlVal == testVal);
                }
            } while (doNext);

            return (control, vault);
        }

        private readonly RandomUtil _rgen = new RandomUtil();
    }
}