using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.CodeExamples;
using DotNetVault.DeadBeefCafeBabeGame;
using DotNetVault.Exceptions;
using DotNetVault.LockedResources;
using DotNetVault.RefReturningCollections;
using DotNetVault.Vaults;
using DotNetVault.VsWrappers;
using JetBrains.Annotations;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using RoLckRes = DotNetVault.LockedResources.RoValListLockedResource<DotNetVault.Vaults.ReadWriteValueListVault<ulong>, ulong>;
using RwLckRes = DotNetVault.LockedResources.ValListLockedResource<DotNetVault.Vaults.ReadWriteValueListVault<ulong>, ulong>;
namespace VaultUnitTests
{
  

    public sealed class ValueListTests : TestBase<ValueListTestFixture>
    {
        public ValueListTests([NotNull] ITestOutputHelper helper, [NotNull] ValueListTestFixture fixture) : base(helper,
            fixture)
        {
        }

        [Fact]
        public void TestGeneration()
        {
            var (tsControl, tsTest) = Fixture.GenerateRandomTsList(TimeSpan.FromMilliseconds(250), 100);
            var (dtControl, dtTest) = Fixture.GenerateRandomDtList(TimeSpan.FromMilliseconds(250), 100);
            Assert.NotNull(tsControl);
            Assert.NotNull(tsTest);
            Assert.NotNull(dtControl);
            Assert.NotNull(dtTest);
            Assert.Equal(tsControl.Count, tsTest.GetCurrentCount());
            Assert.Equal(dtControl.Count, dtTest.GetCurrentCount());

            var dumpedTsVals = tsTest.DumpContentsToArrayAndClear(null, true);
            var dumptedDtVals = dtTest.DumpContentsToArrayAndClear(null, true);

            Assert.True(dumptedDtVals.SequenceEqual(dtControl));
            Assert.True(dumpedTsVals.SequenceEqual(tsControl));
        }

        [Fact]
        public void TestIndexBasedEnumeration()
        {
            var (dtControl, dtTest)
                = Fixture.GenerateRandomDtList(TimeSpan.FromMilliseconds(250), 10000);
            Assert.NotNull(dtControl);
            Assert.NotNull(dtTest);
            Assert.Equal(dtControl.Count, dtTest.GetCurrentCount());

            using var lck = dtTest.RoLock();
            for (int i = 0; i < lck.Count; ++i)
            {
                ref readonly var testVal = ref lck[i];
                var control = dtControl[i];
                Assert.True(testVal == control);
            }
        }

        [Fact]
        public void TestForeachEnumeration()
        {
            var (dtControl, dtTest)
                = Fixture.GenerateRandomDtList(TimeSpan.FromMilliseconds(250), 10000);
            Assert.NotNull(dtControl);
            Assert.NotNull(dtTest);
            Assert.Equal(dtControl.Count, dtTest.GetCurrentCount());

            using var lck = dtTest.RoLock();
            int idx = 0;

            foreach (ref readonly var item in lck)
            {
                var control = dtControl[idx++];
                Assert.True(control == item);
            }
        }

        [Fact]
        public void TestSort()
        {
            var (control, test) = Fixture.CreateSortedDayOfWeekTest();
            using var lck = test.Lock();
            lck.Sort<CustomDateTimeComparer>();
            control.Sort();
            Assert.Equal(lck.Count, control.Count);
            int idx = 0;
            foreach (var itm in lck)
            {
                Assert.True(itm == control[idx++]);
            }

            try
            {
                Assert.False(lck.Single() == default);
                throw new Exception();
            }
            catch (InvalidOperationException)
            {
                //ignore
            }

            Assert.True(lck.First() == control.First() && lck.Last() == control.Last());
            Assert.True(lck.FirstOrDefault() == control.FirstOrDefault() && lck.LastOrDefault() == control.LastOrDefault());
            lck.RemoveRange(1, lck.Count -1);
            control.RemoveRange(1, control.Count - 1);
            Assert.True(lck.Single() == control.Single());
            Assert.True(lck.SingleOrDefault() == control.SingleOrDefault() && lck.SingleOrDefault() != default);

            DateTime now = DateTime.Now;
            DateTime aLilLater = now + TimeSpan.FromDays(5);
            DateTime yesterDay = now - TimeSpan.FromDays(1);
            lck.Clear();
            control.Clear();
            Assert.True(lck.Count == 0 && control.Count == 0);

            lck.Add(now);
            control.Add(now);
            lck.Add(aLilLater);
            control.Add(aLilLater);
            Assert.True(lck.Single((in DateTime val) => val > now) == control.Single(val => val > now));
            var ctrlDef = control.SingleOrDefault(val => val <= yesterDay);
            Assert.True(ctrlDef == default);

            Assert.True(lck.SingleOrDefault((in DateTime val) => val <= yesterDay) ==
                       ctrlDef);

            try
            {
                var tooMany = lck.SingleOrDefault((in DateTime dt) => dt > yesterDay);
                Helper.WriteLine($"Should not see me: [{tooMany:O}].");
                throw new Exception();
            }
            catch (InvalidOperationException)
            {
                //ignore
            }

        }

        [Fact]
        public void BigLifeRecordListCreationTests()
        {
            
            const int numItems = 10_000;
            var (controlList, testListVault) = Fixture.GetRandomBigLifeRecordTestSet(numItems);
            Assert.NotNull(controlList);
            Assert.NotNull(testListVault);
            Assert.Equal(controlList.Count, testListVault.GetCurrentCount());
            
            var comparer = new BigLifeRecordComparer();
            using var lck = testListVault.RoLock();
            ref readonly var firstTest = ref lck.First();
            var firstControl = controlList.First();
            Helper.WriteLine("First test value: " + firstTest);
            Helper.WriteLine("First control value: " + firstControl);
            Assert.True(firstTest == firstControl && comparer.Equals(in firstTest, in firstControl) &&
                        comparer.Compare(in firstTest, in firstControl) == 0 && comparer.GetHashCode(in firstTest) ==
                        comparer.GetHashCode(in firstControl));
            Helper.WriteLine("First test and control are equal.");

            ref readonly var lastTest = ref lck.Last();
            var lastControl = controlList.Last();
            Helper.WriteLine("Last test value: " + lastTest);
            Helper.WriteLine("Last control value: " + lastControl);
            Assert.True(lastTest == lastControl && comparer.Equals(in lastTest, in lastControl) &&
                        comparer.Compare(in lastTest, in lastControl) == 0 && comparer.GetHashCode(in lastTest) ==
                        comparer.GetHashCode(in lastControl));
            Helper.WriteLine("last test and control are equal.");
        }

        [Fact]
        public void TestWriterAndArbiter()
        {
            bool success;
            ImmutableArray<UInt256> result;
            TimeSpan maxDelay = TimeSpan.FromSeconds(2);
            using (var vault = new ReadWriteValueListVault<UInt256>(10_000))
            {
                {
                    using var arbiterThread = TestArbiterThread.CreateArbiterThread(vault);
                    {
                        using var writerThread = TestWriterThread.CreateWriterThread(vault);
                        success = arbiterThread.Join(maxDelay);
                    }
                }
                if (success)
                {
                    Helper.WriteLine("Arbiter thread ended naturally.");
                    result = vault.DumpContentsToArrayAndClear();
                }
                else
                {
                    Helper.WriteLine("Arbiter thread did not end naturally.");
                    result = ImmutableArray<UInt256>.Empty;
                    Assert.True(false);
                }
            }
            Assert.True(success && result.Length > 0);
            int idx = result.IndexOf(TestArbiterThread.CafeBabeVal);
            Assert.True(idx > -1 && idx < result.Length);
            Assert.True(TestArbiterThread.CafeBabeVal == result.ItemRef(idx));

        }

        [Fact]
        public void WriterThreadTest()
        {
            ImmutableArray<UInt256> result;
            using (var vault = new ReadWriteValueListVault<UInt256>(10_000))
            {
                
                using (var writerThread = TestWriterThread.CreateWriterThread(vault))
                {
                    DateTime quitAfter = DateTime.Now + TimeSpan.FromMilliseconds(950);
                    while (DateTime.Now <= quitAfter){Thread.Sleep(TimeSpan.FromMilliseconds(100));}
                }
                result = vault.DumpContentsToArrayAndClear();
            }
            Assert.NotEmpty(result);
            Helper.WriteLine("There were {0} values in the vault at the end.", result.Length);
        }

        [Fact]
        public void TestFirstIndexOf()
        {
            (var emptyList, var oneItemList, var manyItemList, var oneIndices, var nineIndices, ulong notInMany) =
                CreateTestingLists();

            //test empty list
            {
                const int oneIndex = -1;
                const int nineIndex = -1;
                const int notInManyIndex = -1;
                using var lck = emptyList.RoLock();
                
                //test via index of
                int firstIdxOfOneViaIndexOf = lck.IndexOf<UInt64ByRefComparer>(1);
                int firstIdxOfNineViaIndexOf = lck.IndexOf<UInt64ByRefComparer>(9);
                int firstIndexOfNotInListViaIndexOf = lck.IndexOf<UInt64ByRefComparer>(notInMany);
                Assert.Equal(oneIndex, firstIdxOfOneViaIndexOf);
                Assert.Equal(nineIndex, firstIdxOfNineViaIndexOf);
                Assert.Equal(notInManyIndex, firstIndexOfNotInListViaIndexOf);

                //test via find 
                int firstIdxOfOneViaFind = lck.FindIndex((in ulong itm) => itm == 1);
                int firstIdxOfNineViaFind = lck.FindIndex( (in ulong itm) => itm == 9);
                int firstIndexOfNotInListViaFind = lck.FindIndex((in ulong itm) => itm == notInMany);
                Assert.Equal(oneIndex, firstIdxOfOneViaFind);
                Assert.Equal(nineIndex, firstIdxOfNineViaFind);
                Assert.Equal(notInManyIndex, firstIndexOfNotInListViaFind);
            }

            //test one item list
            {
                const int oneIndex = 0;
                const int nineIndex = -1;
                const int notInManyIndex = -1;
                using var lck = oneItemList.RoLock();

                //test via index of
                int firstIdxOfOneViaIndexOf = lck.IndexOf<UInt64ByRefComparer>(1);
                int firstIdxOfNineViaIndexOf = lck.IndexOf<UInt64ByRefComparer>(9);
                int firstIndexOfNotInListViaIndexOf = lck.IndexOf<UInt64ByRefComparer>(notInMany);
                Assert.Equal(oneIndex, firstIdxOfOneViaIndexOf);
                Assert.Equal(nineIndex, firstIdxOfNineViaIndexOf);
                Assert.Equal(notInManyIndex, firstIndexOfNotInListViaIndexOf);

                //test via find 
                int firstIdxOfOneViaFind = lck.FindIndex((in ulong itm) => itm == 1);
                int firstIdxOfNineViaFind = lck.FindIndex((in ulong itm) => itm == 9);
                int firstIndexOfNotInListViaFind = lck.FindIndex((in ulong itm) => itm == notInMany);
                Assert.Equal(oneIndex, firstIdxOfOneViaFind);
                Assert.Equal(nineIndex, firstIdxOfNineViaFind);
                Assert.Equal(notInManyIndex, firstIndexOfNotInListViaFind);
            }

            //test many item list
            {
                int oneIndex = oneIndices.First();
                int nineIndex = nineIndices.First();
                const int notInManyIndex = -1;
                using var lck = manyItemList.RoLock();

                //test via index of
                int firstIdxOfOneViaIndexOf = lck.IndexOf<UInt64ByRefComparer>(1);
                int firstIdxOfNineViaIndexOf = lck.IndexOf<UInt64ByRefComparer>(9);
                int firstIndexOfNotInListViaIndexOf = lck.IndexOf<UInt64ByRefComparer>(notInMany);
                Assert.Equal(oneIndex, firstIdxOfOneViaIndexOf);
                Assert.Equal(nineIndex, firstIdxOfNineViaIndexOf);
                Assert.Equal(notInManyIndex, firstIndexOfNotInListViaIndexOf);

                //test via find 
                int firstIdxOfOneViaFind = lck.FindIndex((in ulong itm) => itm == 1);
                int firstIdxOfNineViaFind = lck.FindIndex((in ulong itm) => itm == 9);
                int firstIndexOfNotInListViaFind = lck.FindIndex((in ulong itm) => itm == notInMany);
                Assert.Equal(oneIndex, firstIdxOfOneViaFind);
                Assert.Equal(nineIndex, firstIdxOfNineViaFind);
                Assert.Equal(notInManyIndex, firstIndexOfNotInListViaFind);
            }

        }

        [Fact]
        public void TestLastIndexOf()
        {
            (var emptyList, var oneItemList, var manyItemList, var oneIndices, var nineIndices, ulong notInMany) =
                CreateTestingLists();

            //test empty list
            {
                const int oneIndex = -1;
                const int nineIndex = -1;
                const int notInManyIndex = -1;
                using var lck = emptyList.RoLock();

                //test via index of
                int lastIdxOfOneViaIndexOf = lck.LastIndexOf<UInt64ByRefComparer>(1);
                int lastIdxOfNineViaIndexOf = lck.LastIndexOf<UInt64ByRefComparer>(9);
                int lastIndexOfNotInListViaIndexOf = lck.LastIndexOf<UInt64ByRefComparer>(notInMany);
                Assert.Equal(oneIndex, lastIdxOfOneViaIndexOf);
                Assert.Equal(nineIndex, lastIdxOfNineViaIndexOf);
                Assert.Equal(notInManyIndex, lastIndexOfNotInListViaIndexOf);

                //test via find 
                int lastIdxOfOneViaFind = lck.FindLastIndex((in ulong itm) => itm == 1);
                int lastIdxOfNineViaFind = lck.FindLastIndex((in ulong itm) => itm == 9);
                int lastIndexOfNotInListViaFind = lck.FindLastIndex((in ulong itm) => itm == notInMany);
                Assert.Equal(oneIndex, lastIdxOfOneViaFind);
                Assert.Equal(nineIndex, lastIdxOfNineViaFind);
                Assert.Equal(notInManyIndex, lastIndexOfNotInListViaFind);
            }

            //test one item list
            {
                const int oneIndex = 0;
                const int nineIndex = -1;
                const int notInManyIndex = -1;
                using var lck = oneItemList.RoLock();

                //test via index of
                int lastIdxOfOneViaIndexOf = lck.LastIndexOf<UInt64ByRefComparer>(1);
                int lastIdxOfNineViaIndexOf = lck.LastIndexOf<UInt64ByRefComparer>(9);
                int lastIndexOfNotInListViaIndexOf = lck.LastIndexOf<UInt64ByRefComparer>(notInMany);
                Assert.Equal(oneIndex, lastIdxOfOneViaIndexOf);
                Assert.Equal(nineIndex, lastIdxOfNineViaIndexOf);
                Assert.Equal(notInManyIndex, lastIndexOfNotInListViaIndexOf);

                //test via find 
                int lastIdxOfOneViaFind = lck.FindLastIndex((in ulong itm) => itm == 1);
                int lastIdxOfNineViaFind = lck.FindLastIndex((in ulong itm) => itm == 9);
                int lastIndexOfNotInListViaFind = lck.FindLastIndex((in ulong itm) => itm == notInMany);
                Assert.Equal(oneIndex, lastIdxOfOneViaFind);
                Assert.Equal(nineIndex, lastIdxOfNineViaFind);
                Assert.Equal(notInManyIndex, lastIndexOfNotInListViaFind);
            }

            //test many item list
            {
                int oneIndex = oneIndices.Last();
                int nineIndex = nineIndices.Last();
                const int notInManyIndex = -1;
                using var lck = manyItemList.RoLock();

                //test via index of
                int lastIdxOfOneViaIndexOf = lck.LastIndexOf<UInt64ByRefComparer>(1);
                int lastIdxOfNineViaIndexOf = lck.LastIndexOf<UInt64ByRefComparer>(9);
                int lastIndexOfNotInListViaIndexOf = lck.LastIndexOf<UInt64ByRefComparer>(notInMany);
                Assert.Equal(oneIndex, lastIdxOfOneViaIndexOf);
                Assert.Equal(nineIndex, lastIdxOfNineViaIndexOf);
                Assert.Equal(notInManyIndex, lastIndexOfNotInListViaIndexOf);

                //test via find 
                int lastIdxOfOneViaFind = lck.FindLastIndex((in ulong itm) => itm == 1);
                int lastIdxOfNineViaFind = lck.FindLastIndex((in ulong itm) => itm == 9);
                int lastIndexOfNotInListViaFind = lck.FindLastIndex((in ulong itm) => itm == notInMany);
                Assert.Equal(oneIndex, lastIdxOfOneViaFind);
                Assert.Equal(nineIndex, lastIdxOfNineViaFind);
                Assert.Equal(notInManyIndex, lastIndexOfNotInListViaFind);
            }

        }

        private struct UInt64ByRefComparer : IByRefCompleteComparer<UInt64>
        {
            public bool Equals(ulong x, ulong y) => Equals(in x, in y);

            public int GetHashCode(ulong obj) => GetHashCode(in obj);

            public int Compare(ulong x, ulong y) => Compare(in x, in y);

            public bool IsValid => true;

            public bool WorksCorrectlyWhenDefaultConstructed => true;

            public bool Equals(in ulong lhs, in ulong rhs) => lhs == rhs;

            public int GetHashCode(in ulong obj) => obj.GetHashCode();

            public int Compare(in ulong lhs, in ulong rhs) => lhs == rhs ? 0 : (lhs > rhs ? 1 : -1);
        }

        [Fact]
        public void SearchAndSortTest()
        {
            const int sampleSize = 100_000;
            const int presentSamples = 5_000;
            const int notPresentSamples = 5_000;

            var (controlList, testVault, present, notPresent) =
                Fixture.CreateSortAndSetTestData(sampleSize, presentSamples, notPresentSamples);
            Assert.NotNull(controlList);
            Assert.NotNull(testVault);
            Assert.False(notPresent.IsDefault);
            Assert.False(present.IsDefault);
            Assert.Equal(controlList.Count, sampleSize);
            Assert.Equal(testVault.GetCurrentCount(), controlList.Count);
            Assert.Equal(present.Length, presentSamples);
            Assert.Equal(notPresent.Length, notPresentSamples);
            
            controlList.Sort(new BigLifeRecordComparer());
            using var lck = testVault.Lock();
            lck.Sort<BigLifeRecordComparer>();
            using var lstEnumerator = controlList.GetEnumerator();
            foreach (ref readonly var itm in lck)
            {
                Assert.True(lstEnumerator.MoveNext());
                Assert.True(lstEnumerator.Current == itm);
            }

            var (presentTestIndices, presentControlIndices, absentControlIndices, absentTestIndices) =
                DoShit(in lck, controlList, present, notPresent);
            Assert.True(presentControlIndices.SequenceEqual(presentTestIndices) && absentTestIndices.SequenceEqual(absentControlIndices));
            Assert.True(presentControlIndices.All(itm => itm > -1));
            Assert.True(absentControlIndices.All(itm => itm < 0 ));

            foreach (var idx in presentTestIndices)
            {
                Assert.True(lck[idx] == controlList[idx]);
                Assert.True(lck[idx].GetHashCode() == controlList[idx].GetHashCode());
            }


        }
        [Fact]
        public void TestGetRangeOnEmptyList()
        {
            (var empty, _, _, _, _, _) = CreateTestingLists();

            ImmutableArray<ulong> emptyContents = empty.CopyContentsToArray();
            Assert.Empty(emptyContents);

            {
                using var lck = empty.RoLock();
                var test = lck.GetRange(0, 0);
                Assert.Empty(test.ToArray());
            }
            Assert.Throws<ArgumentNegativeException<int>>(delegate
            {
                using var lck = empty.RoLock();
                var badRange = lck.GetRange(-1, 0);
                Helper.WriteLine("You should never see this: [" + badRange.Count + "].");
            });
            Assert.Throws<ArgumentNegativeException<int>>(delegate
            {
                using var lck = empty.RoLock();
                var badRange = lck.GetRange(0, -1);
                Helper.WriteLine("You should never see this: [" + badRange.Count + "].");
            });
            {
                
                try
                {
                    using var lck = empty.RoLock();
                    var badRange = lck.GetRange(0, 1);
                    Helper.WriteLine("You should never see this: [" + badRange.Count + "].");
                    throw new ThrowsException(typeof(ArgumentException));
                }
                catch (ArgumentException ex)
                {
                    Assert.True(typeof(ArgumentException) == ex.GetType());
                    Helper.WriteLine("Yep .... it threw the right exception.  Contents:  [{0}].", ex);
                }
                catch (ThrowsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Helper.WriteLine("Threw wrong type of exception.  Actual type: [{0}]; Expected type: [{1}]; Contents: [{2}].", ex.GetType().Name, typeof(ArgumentException).Name, ex);
                    throw new ThrowsException(typeof(ArgumentException), ex);
                }
            }
            {

                try
                {
                    using var lck = empty.RoLock();
                    var badRange = lck.GetRange(1, 0);
                    Helper.WriteLine("You should never see this: [" + badRange.Count + "].");
                    throw new ThrowsException(typeof(ArgumentException));
                }
                catch (ArgumentException ex)
                {
                    Assert.True(typeof(ArgumentException) == ex.GetType());
                    Helper.WriteLine("Yep .... it threw the right exception.  Contents:  [{0}].", ex);
                }
                catch (ThrowsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Helper.WriteLine("Threw wrong type of exception.  Actual type: [{0}]; Expected type: [{1}]; Contents: [{2}].", ex.GetType().Name, typeof(ArgumentException).Name, ex);
                    throw new ThrowsException(typeof(ArgumentException), ex);
                }
            }
        }

        [Fact]
        public void TestGetRangeOnOneItemList()
        {
            (_, var oneItem, _, _, _, _) = CreateTestingLists();
            ImmutableArray<ulong> oneItemContents = oneItem.CopyContentsToArray();
            Assert.Single(oneItemContents);

            {
                using var lck = oneItem.RoLock();
                var test = lck.GetRange(0, 0);
                Assert.Empty(test.ToArray());
            }
            Assert.Throws<ArgumentNegativeException<int>>(delegate
            {
                using var lck = oneItem.RoLock();
                var badRange = lck.GetRange(-1, 0);
                Helper.WriteLine("You should never see this: [" + badRange.Count + "].");
            });
            Assert.Throws<ArgumentNegativeException<int>>(delegate
            {
                using var lck = oneItem.RoLock();
                var badRange = lck.GetRange(0, -1);
                Helper.WriteLine("You should never see this: [" + badRange.Count + "].");
            });

            {
                using var lck = oneItem.RoLock();
                var okRange = lck.GetRange(0, 1);
                Assert.Single(okRange.ToArray());
                Assert.True(oneItemContents.SequenceEqual(okRange.ToArray()));
            }
            List<ulong> tryMe = new List<ulong>(oneItemContents);

            {
                 var standardListRange = tryMe.GetRange(1, 0);
                 Helper.WriteLine("You should never see this: [" + standardListRange.Count + "].");
                 //standard list<T> does not throw when index: 1, count: 0 even when list.Count == 1 
            }


            {
                //mine imitates theirs even though it would make more sense to always throw if index out of range for zero count (or even never throw)
                //standard list<T> DOES throw when index > 1 for count zero on one item list.
                try
                {
                    var standardListRange = tryMe.GetRange(2, 0);
                    Helper.WriteLine("You should never see this: [" + standardListRange.Count + "].");
                    throw new ThrowsException(typeof(ArgumentException));
                }
                catch (ArgumentException ex)
                {
                    Assert.True(typeof(ArgumentException) == ex.GetType());
                    Helper.WriteLine("Yep .... it threw the right exception.  Contents:  [{0}].", ex);
                }
                catch (ThrowsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Helper.WriteLine("Threw wrong type of exception.  Actual type: [{0}]; Expected type: [{1}]; Contents: [{2}].", ex.GetType().Name, typeof(ArgumentException).Name, ex);
                    throw new ThrowsException(typeof(ArgumentException), ex);
                }

            }
            {
                using var lck = oneItem.RoLock();
                var strangelyOkRangeButSameAsRegListT = lck.GetRange(1, 0);
                Helper.WriteLine("Yep, for whatever reason, doesn't throw == " + strangelyOkRangeButSameAsRegListT.Count);
            }
            {

                try
                {
                    using var lck = oneItem.RoLock();
                    var badRange = lck.GetRange(2, 0);
                    Helper.WriteLine("You should never see this: [" + badRange.Count + "].");
                    throw new ThrowsException(typeof(ArgumentException));
                }
                catch (ArgumentException ex)
                {
                    Assert.True(typeof(ArgumentException) == ex.GetType());
                    Helper.WriteLine("Yep .... it threw the right exception.  Contents:  [{0}].", ex);
                }
                catch (ThrowsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Helper.WriteLine("Threw wrong type of exception.  Actual type: [{0}]; Expected type: [{1}]; Contents: [{2}].", ex.GetType().Name, typeof(ArgumentException).Name, ex);
                    throw new ThrowsException(typeof(ArgumentException), ex);
                }
            }
            {

                try
                {
                    using var lck = oneItem.RoLock();
                    var badRange = lck.GetRange(1, 1);
                    Helper.WriteLine("You should never see this: [" + badRange.Count + "].");
                    throw new ThrowsException(typeof(ArgumentException));
                }
                catch (ArgumentException ex)
                {
                    Assert.True(typeof(ArgumentException) == ex.GetType());
                    Helper.WriteLine("Yep .... it threw the right exception.  Contents:  [{0}].", ex);
                }
                catch (ThrowsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Helper.WriteLine("Threw wrong type of exception.  Actual type: [{0}]; Expected type: [{1}]; Contents: [{2}].", ex.GetType().Name, typeof(ArgumentException).Name, ex);
                    throw new ThrowsException(typeof(ArgumentException), ex);
                }
            }
            {

                try
                {
                    using var lck = oneItem.RoLock();
                    var badRange = lck.GetRange(0, 2);
                    Helper.WriteLine("You should never see this: [" + badRange.Count + "].");
                    throw new ThrowsException(typeof(ArgumentException));
                }
                catch (ArgumentException ex)
                {
                    Assert.True(typeof(ArgumentException) == ex.GetType());
                    Helper.WriteLine("Yep .... it threw the right exception.  Contents:  [{0}].", ex);
                }
                catch (ThrowsException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Helper.WriteLine("Threw wrong type of exception.  Actual type: [{0}]; Expected type: [{1}]; Contents: [{2}].", ex.GetType().Name, typeof(ArgumentException).Name, ex);
                    throw new ThrowsException(typeof(ArgumentException), ex);
                }
            }
        }

        [Fact]
        public void TestGetRangeManyItemList()
        {
            (_, _, var manyItems, _, _, _) = CreateTestingLists();
            List<ulong> manyItemsContents = manyItems.CopyContentsToArray().ToList();
            Assert.True(manyItemsContents.Count > 1);

            using RoLckRes lck = manyItems.RoLock();
            Assert.Equal(lck.Count, manyItemsContents.Count);
            
            const int indexMinimum = -2;
            int indexMaximum = lck.Count + 3;

            for (int index = indexMinimum; index <= indexMaximum; ++index)
            {
                for (int count = indexMinimum; count <= indexMaximum; ++count)
                {
                    VerifySameResults(in lck, manyItemsContents, index, count, Helper);
                }
            }

            static void VerifySameResults(in RoLckRes lck, List<ulong> identicalList, int index, int count, ITestOutputHelper helper)
            {
                try
                {
                    BigValueList<ulong> bvlRes;
                    Exception bvlException;

                    List<ulong> identRes;
                    Exception identicalResException;

                    try
                    {
                        bvlRes = lck.GetRange(index, count);
                        bvlException = null;
                    }
                    catch (Exception ex)
                    {
                        bvlRes = null;
                        bvlException = ex;
                    }

                    try
                    {
                        identRes = identicalList.GetRange(index, count);
                        identicalResException = null;
                    }
                    catch (Exception ex)
                    {
                        identRes = null;
                        identicalResException = ex;
                    }

                    Assert.True((identRes == null) == (bvlRes == null));
                    Assert.True((identicalResException == null) == (bvlException == null));
                    Assert.True((identRes == null) != (identicalResException == null));
                    Assert.True((bvlException == null) != (bvlRes == null));

                    if (bvlException != null)
                    {
                        switch (identicalResException)
                        {
                            case null:
                                Assert.False(true);
                                break;
                            case ArgumentOutOfRangeException _:
                                Assert.True(bvlException is ArgumentNegativeException<int> || bvlException.GetType() == typeof(ArgumentOutOfRangeException));
                                break;
                            case ArgumentException aex:
                                Assert.True(aex.GetType() == bvlException.GetType());
                                break;
                            default:
                                Assert.True(identicalResException.GetType() == bvlException.GetType());
                                break;
                        }
                    }

                    if (bvlRes != null)
                    {
                        Assert.Equal(bvlRes.Count, identRes.Count);
                        Assert.True(bvlRes.ToArray().SequenceEqual(identRes));
                        Assert.True(identRes.SequenceEqual(bvlRes.ToArray()));
                    }
                    helper.WriteLine("IDX: {0}, COUNT: {1} -- PASSED.", index, count);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Test failed-- idx: {index}, count: {count}.", ex);
                }
            }
            
        }
        [Fact]
        public void TestAllAnySingleFirstLastOnEmptyList()
        {
            (var empty, _, _, _, _, _) = CreateTestingLists();
            var controlList = empty.CopyContentsToArray().ToList();

            DualOperationProviderFactory factory = default;
            IDualQueryOperationProvider operationProvider = factory.CreateDualOperationProvider(controlList);
            {
                using var lck = empty.RoLock();

                DualQueryOperationResult<bool> allNineResult =
                    operationProvider.Execute(in lck, AllNineTestQuery, AllNineControlQuery, 9, 9);
                DualQueryOperationResult<bool> anyNineResult =
                    operationProvider.Execute(in lck, AnyNineTestQuery, AnyNineControlQuery, 9, 9);
                Assert.True(allNineResult.ResultsMatch && allNineResult.TestResult.QueryResult == true);
                Assert.True(anyNineResult.ResultsMatch && anyNineResult.TestResult.QueryResult == false);
            }
            {
                using var lck = empty.RoLock();
                DualQueryOperationResult<ulong> singleNoPredResult =
                    operationProvider.Execute(in lck, TestSingleNoPredQuery, ControlSingleNoPredQuery, 9, 9);
                Assert.True(singleNoPredResult.ResultsMatch &&
                            singleNoPredResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> singleOrDefaultNoPredResult =
                    operationProvider.Execute(in lck, TestSingleOrDefaultNoPredQuery, ControlSingleOrDefaultNoPredQuery,
                        9, 9);
                Assert.True(singleOrDefaultNoPredResult.ResultsMatch &&
                            singleOrDefaultNoPredResult.TestResult.QueryResult == default(ulong));
                DualQueryOperationResult<ulong> singleIsNineResult =
                    operationProvider.Execute(in lck, TestSingleIsNineQuery, ControlSingleIsNineQuery, 9, 9);
                Assert.True(singleIsNineResult.ResultsMatch &&
                            singleIsNineResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> singleOrDefaultIsNineResult =
                    operationProvider.Execute(in lck, TestSingleOrDefaultIsNineQuery, ControlSingleOrDefaultIsNineQuery,
                        9, 9);
                Assert.True(singleOrDefaultIsNineResult.ResultsMatch &&
                            singleOrDefaultIsNineResult.TestResult.QueryResult == default(ulong));
            }
            {
                using var lck = empty.RoLock();
                DualQueryOperationResult<ulong> firstNoPredResult =
                    operationProvider.Execute(in lck, TestFirstNoPredQuery, ControlFirstNoPredQuery, 9, 9);
                Assert.True(firstNoPredResult.ResultsMatch &&
                            firstNoPredResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> firstOrDefaultNoPredResult =
                    operationProvider.Execute(in lck, TestFirstOrDefaultNoPredQuery, ControlFirstOrDefaultNoPredQuery,
                        9, 9);
                Assert.True(firstOrDefaultNoPredResult.ResultsMatch &&
                            firstOrDefaultNoPredResult.TestResult.QueryResult == default(ulong));
                DualQueryOperationResult<ulong> firstIsNineResult =
                    operationProvider.Execute(in lck, TestFirstIsNineQuery, ControlFirstIsNineQuery, 9, 9);
                Assert.True(firstIsNineResult.ResultsMatch &&
                            firstIsNineResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> firstOrDefaultIsNineResult =
                    operationProvider.Execute(in lck, TestFirstOrDefaultIsNineQuery, ControlFirstOrDefaultIsNineQuery,
                        9, 9);
                Assert.True(firstOrDefaultIsNineResult.ResultsMatch &&
                            firstOrDefaultIsNineResult.TestResult.QueryResult == default(ulong));
            }
            {
                using var lck = empty.RoLock();
                DualQueryOperationResult<ulong> lastNoPredResult =
                    operationProvider.Execute(in lck, TestLastNoPredQuery, ControlLastNoPredQuery, 9, 9);
                Assert.True(lastNoPredResult.ResultsMatch &&
                            lastNoPredResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> lastOrDefaultNoPredResult =
                    operationProvider.Execute(in lck, TestLastOrDefaultNoPredQuery, ControlLastOrDefaultNoPredQuery,
                        9, 9);
                Assert.True(lastOrDefaultNoPredResult.ResultsMatch &&
                            lastOrDefaultNoPredResult.TestResult.QueryResult == default(ulong));
                DualQueryOperationResult<ulong> lastIsNineResult =
                    operationProvider.Execute(in lck, TestLastIsNineQuery, ControlLastIsNineQuery, 9, 9);
                Assert.True(lastIsNineResult.ResultsMatch &&
                            lastIsNineResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> lastOrDefaultIsNineResult =
                    operationProvider.Execute(in lck, TestLastOrDefaultIsNineQuery, ControlLastOrDefaultIsNineQuery,
                        9, 9);
                Assert.True(lastOrDefaultIsNineResult.ResultsMatch &&
                            lastOrDefaultIsNineResult.TestResult.QueryResult == default(ulong));
            }
        }

        [Fact]
        public void TestAllAnySingleFirstLastOnSingletonList()
        { 
            (_, var singleton, _, _, _, _) = CreateTestingLists();
            var controlList = singleton.CopyContentsToArray().ToList();

            QueryOperation<ulong> queryOp = TestValueEnumeration;
            FilteredQueryOperation<ulong> filteredQueryOp = TestFilteredEnumeration;
            TransformingQueryOperation<ulong, TimeSpan> transformingQueryOp = TestTransformingEnumeration;//(in RoLckRes res, RefFunc<ulong, TimeSpan> transformation) => TestTransformingEnumeration<TimeSpan>(in res, transformation);
            FilteredTransformingQueryOperation<ulong, TimeSpan> filteredTransformingQueryOp =
                TestFilteredTransformingEnumeration;

            DualOperationProviderFactory factory = default;
            IDualQueryOperationProvider operationProvider = factory.CreateDualOperationProvider(controlList);
            {
                using var lck = singleton.RoLock();

                DualQueryOperationResult<bool> allNineResult =
                    operationProvider.Execute(in lck, AllNineTestQuery, AllNineControlQuery, 9, 9);
                DualQueryOperationResult<bool> anyNineResult =
                    operationProvider.Execute(in lck, AnyNineTestQuery, AnyNineControlQuery, 9, 9);
                Assert.True(allNineResult.ResultsMatch && allNineResult.TestResult.QueryResult == false);
                Assert.True(anyNineResult.ResultsMatch && anyNineResult.TestResult.QueryResult == false);
            }
            {
                using var lck = singleton.RoLock();
                DualQueryOperationResult<ulong> singleNoPredResult =
                    operationProvider.Execute(in lck, TestSingleNoPredQuery, ControlSingleNoPredQuery, 9, 9);
                Assert.True(singleNoPredResult.ResultsMatch &&
                            singleNoPredResult.TestResult.QueryResult ==
                            1);
                DualQueryOperationResult<ulong> singleOrDefaultNoPredResult =
                    operationProvider.Execute(in lck, TestSingleOrDefaultNoPredQuery, ControlSingleOrDefaultNoPredQuery,
                        9, 9);
                Assert.True(singleOrDefaultNoPredResult.ResultsMatch &&
                            singleOrDefaultNoPredResult.TestResult.QueryResult == 1);
                DualQueryOperationResult<ulong> singleIsNineResult =
                    operationProvider.Execute(in lck, TestSingleIsNineQuery, ControlSingleIsNineQuery, 9, 9);
                Assert.True(singleIsNineResult.ResultsMatch &&
                            singleIsNineResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> singleOrDefaultIsNineResult =
                    operationProvider.Execute(in lck, TestSingleOrDefaultIsNineQuery, ControlSingleOrDefaultIsNineQuery,
                        9, 9);
                Assert.True(singleOrDefaultIsNineResult.ResultsMatch &&
                            singleOrDefaultIsNineResult.TestResult.QueryResult == default(ulong));
            }
            {
                using var lck = singleton.RoLock();
                DualQueryOperationResult<ulong> firstNoPredResult =
                    operationProvider.Execute(in lck, TestFirstNoPredQuery, ControlFirstNoPredQuery, 9, 9);
                Assert.True(firstNoPredResult.ResultsMatch &&
                            firstNoPredResult.TestResult.QueryResult == 1, firstNoPredResult.ToString());
                DualQueryOperationResult<ulong> firstOrDefaultNoPredResult =
                    operationProvider.Execute(in lck, TestFirstOrDefaultNoPredQuery, ControlFirstOrDefaultNoPredQuery,
                        9, 9);
                Assert.True(firstOrDefaultNoPredResult.ResultsMatch &&
                            firstOrDefaultNoPredResult.TestResult.QueryResult == 1);
                DualQueryOperationResult<ulong> firstIsNineResult =
                    operationProvider.Execute(in lck, TestFirstIsNineQuery, ControlFirstIsNineQuery, 9, 9);
                Assert.True(firstIsNineResult.ResultsMatch &&
                            firstIsNineResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> firstOrDefaultIsNineResult =
                    operationProvider.Execute(in lck, TestFirstOrDefaultIsNineQuery, ControlFirstOrDefaultIsNineQuery,
                        9, 9);
                Assert.True(firstOrDefaultIsNineResult.ResultsMatch &&
                            firstOrDefaultIsNineResult.TestResult.QueryResult == default(ulong));
            }
            {
                using var lck = singleton.RoLock();
                DualQueryOperationResult<ulong> lastNoPredResult =
                    operationProvider.Execute(in lck, TestLastNoPredQuery, ControlLastNoPredQuery, 9, 9);
                Assert.True(lastNoPredResult.ResultsMatch &&
                            lastNoPredResult.TestResult.QueryResult == 1);
                DualQueryOperationResult<ulong> lastOrDefaultNoPredResult =
                    operationProvider.Execute(in lck, TestLastOrDefaultNoPredQuery, ControlLastOrDefaultNoPredQuery,
                        9, 9);
                Assert.True(lastOrDefaultNoPredResult.ResultsMatch &&
                            lastOrDefaultNoPredResult.TestResult.QueryResult == 1);
                DualQueryOperationResult<ulong> lastIsNineResult =
                    operationProvider.Execute(in lck, TestLastIsNineQuery, ControlLastIsNineQuery, 9, 9);
                Assert.True(lastIsNineResult.ResultsMatch &&
                            lastIsNineResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> lastOrDefaultIsNineResult =
                    operationProvider.Execute(in lck, TestLastOrDefaultIsNineQuery, ControlLastOrDefaultIsNineQuery,
                        9, 9);
                Assert.True(lastOrDefaultIsNineResult.ResultsMatch &&
                            lastOrDefaultIsNineResult.TestResult.QueryResult == default(ulong));
            }
        }

        

        [Fact]
        public void TestAllAnySingleFirstLastOnPopulatedList()
        {
            (_, _, ReadWriteValueListVault<ulong> populated, _, _, _) = CreateTestingLists();
            var controlList = populated.CopyContentsToArray().ToList();

            DualOperationProviderFactory factory = default;
            IDualQueryOperationProvider operationProvider = factory.CreateDualOperationProvider(controlList);
            {
                using var lck = populated.RoLock();

                DualQueryOperationResult<bool> allNineResult =
                    operationProvider.Execute(in lck, AllNineTestQuery, AllNineControlQuery, 9, 9);
                DualQueryOperationResult<bool> anyNineResult =
                    operationProvider.Execute(in lck, AnyNineTestQuery, AnyNineControlQuery, 9, 9);
                Assert.True(allNineResult.ResultsMatch && allNineResult.TestResult.QueryResult == false);
                Assert.True(anyNineResult.ResultsMatch && anyNineResult.TestResult.QueryResult == true);
            }
            {
                using var lck = populated.RoLock();
                DualQueryOperationResult<ulong> singleNoPredResult =
                    operationProvider.Execute(in lck, TestSingleNoPredQuery, ControlSingleNoPredQuery, 9, 9);
                Assert.True(singleNoPredResult.ResultsMatch &&
                            singleNoPredResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> singleOrDefaultNoPredResult =
                    operationProvider.Execute(in lck, TestSingleOrDefaultNoPredQuery, ControlSingleOrDefaultNoPredQuery,
                        9, 9);
                Assert.True(singleOrDefaultNoPredResult.ResultsMatch &&
                            singleOrDefaultNoPredResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> singleIsNineResult =
                    operationProvider.Execute(in lck, TestSingleIsNineQuery, ControlSingleIsNineQuery, 9, 9);
                Assert.True(singleIsNineResult.ResultsMatch &&
                            singleIsNineResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
                DualQueryOperationResult<ulong> singleOrDefaultIsNineResult =
                    operationProvider.Execute(in lck, TestSingleOrDefaultIsNineQuery, ControlSingleOrDefaultIsNineQuery,
                        9, 9);
                Assert.True(singleOrDefaultIsNineResult.ResultsMatch &&
                            singleOrDefaultIsNineResult.TestResult.FaultingException?.GetType() ==
                            typeof(InvalidOperationException));
            }
            {
                using var lck = populated.RoLock();
                DualQueryOperationResult<ulong> firstNoPredResult =
                    operationProvider.Execute(in lck, TestFirstNoPredQuery, ControlFirstNoPredQuery, 9, 9);
                Assert.True(firstNoPredResult.ResultsMatch &&
                            firstNoPredResult.TestResult.QueryResult == 1, firstNoPredResult.ToString());
                DualQueryOperationResult<ulong> firstOrDefaultNoPredResult =
                    operationProvider.Execute(in lck, TestFirstOrDefaultNoPredQuery, ControlFirstOrDefaultNoPredQuery,
                        9, 9);
                Assert.True(firstOrDefaultNoPredResult.ResultsMatch &&
                            firstOrDefaultNoPredResult.TestResult.QueryResult == 1);
                DualQueryOperationResult<ulong> firstIsNineResult =
                    operationProvider.Execute(in lck, TestFirstIsNineQuery, ControlFirstIsNineQuery, 9, 9);
                Assert.True(firstIsNineResult.ResultsMatch &&
                            firstIsNineResult.TestResult.QueryResult == 9ul);
                DualQueryOperationResult<ulong> firstOrDefaultIsNineResult =
                    operationProvider.Execute(in lck, TestFirstOrDefaultIsNineQuery, ControlFirstOrDefaultIsNineQuery,
                        9, 9);
                Assert.True(firstOrDefaultIsNineResult.ResultsMatch &&
                            firstOrDefaultIsNineResult.TestResult.QueryResult == 9);
            }
            {
                using var lck = populated.RoLock();
                DualQueryOperationResult<ulong> lastNoPredResult =
                    operationProvider.Execute(in lck, TestLastNoPredQuery, ControlLastNoPredQuery, 9, 9);
                Assert.True(lastNoPredResult.ResultsMatch &&
                            lastNoPredResult.TestResult.QueryResult == 9ul);
                DualQueryOperationResult<ulong> lastOrDefaultNoPredResult =
                    operationProvider.Execute(in lck, TestLastOrDefaultNoPredQuery, ControlLastOrDefaultNoPredQuery,
                        9, 9);
                Assert.True(lastOrDefaultNoPredResult.ResultsMatch &&
                            lastOrDefaultNoPredResult.TestResult.QueryResult == 9ul);
                DualQueryOperationResult<ulong> lastIsNineResult =
                    operationProvider.Execute(in lck, TestLastIsNineQuery, ControlLastIsNineQuery, 9, 9);
                Assert.True(lastIsNineResult.ResultsMatch &&
                            lastIsNineResult.TestResult.QueryResult == 9ul);
                DualQueryOperationResult<ulong> lastOrDefaultIsNineResult =
                    operationProvider.Execute(in lck, TestLastOrDefaultIsNineQuery, ControlLastOrDefaultIsNineQuery,
                        9, 9);
                Assert.True(lastOrDefaultIsNineResult.ResultsMatch &&
                            lastOrDefaultIsNineResult.TestResult.QueryResult == 9ul);
            }
        }

        [Fact]
        public void TestFindIndexAndFindLastIndex()
        {
            (_, _, var populated, _, _, _) = CreateTestingLists();
            var controlList = populated.CopyContentsToArray().ToList();

            DualOperationProviderFactory factory = default;
            IDualQueryOperationProvider operationProvider = factory.CreateDualOperationProvider(controlList);
            {
                using var lck = populated.RoLock();
                DualQueryOperationResult<int> res = operationProvider.Execute(in lck, FindGreaterThan3BetweenIdxOneAndTwoQuery,
                    FindControlGreaterThan3BetweenIdxOneAndTwoQuery, 3, 4);
                Assert.True(res.ResultsMatch && res.TestResult.QueryResult == 3);
                res = operationProvider.Execute(in lck, FindLastGreaterThanBetweenIdxOneAndTwoQuery,
                    FindControlLastGreaterThanBetweenIdxOneAndTwoQuery, 3, 4);
                Assert.True(res.ResultsMatch && res.TestResult.FaultingException == null && res.ControlResult.FaultingException == null);
            }
        }

        [Fact]
        public void TestIndexOfAndLastIndexOf()
        {
           HashSet<ulong> include = new HashSet<ulong>( new [] {0xdead_beef_cafe_babe, 0xcafe_beef_dead_babe, 0xbabe_dead_cafe_beef});
           HashSet<ulong> exclude = new HashSet<ulong>( new ulong [] {0x1234_5678_9ABC_DEF0, 0x0FED_CBA9_8765_4321, 0x1111_1111_1111_111});
           const int arrSize = 10_000;
           List<ulong> control = CreateRandomArray(include, exclude, arrSize).ToList();
           using ReadWriteValueListVault<ulong> test =
               new ReadWriteValueListVault<ulong>(VsEnumerableWrapper<ulong>.FromIEnumerable(control),
                   TimeSpan.FromMilliseconds(500));
           UInt64ByRefComparer comparer = default;
           {
               foreach (var item in include)
               {
                   int firstControlIdx = control.IndexOf(item);
                   int lastControlIdx = control.LastIndexOf(item);
                   int firstTestIdx;
                   int lastTestIdx;
                   ulong firstTestVal, lastTestVal;
                   {
                       using var lck = test.RoLock();
                       firstTestIdx = lck.IndexOf<UInt64ByRefComparer>(item);
                       lastTestIdx = lck.LastIndexOf<UInt64ByRefComparer>(item);
                       Assert.True(firstTestIdx > -1 && firstTestIdx < lck.Count);
                       Assert.True(lastTestIdx > -1 && lastTestIdx < lck.Count);
                       firstTestVal = lck[firstTestIdx];
                       lastTestVal = lck[lastTestIdx];
                   }
                   Assert.Equal(firstControlIdx, firstTestIdx);
                   Assert.Equal(lastControlIdx, lastTestIdx);
                   Assert.Equal(item, firstTestVal);
                   Assert.Equal(item, lastTestVal);
                   Assert.Equal(item, control[firstControlIdx]);
                   Assert.Equal(item, control[lastControlIdx]);
               }

               foreach (var item in exclude)
               {
                   int firstControlIdx = control.IndexOf(item);
                   int lastControlIdx = control.LastIndexOf(item);
                   int firstTestIdx;
                   int lastTestIdx;
                   {
                       using var lck = test.RoLock();
                       firstTestIdx = lck.IndexOf<UInt64ByRefComparer>(item, 250, 500);
                       lastTestIdx = lck.LastIndexOf<UInt64ByRefComparer>(item, 750, 500);
                   }
                   Assert.True(firstControlIdx < 0);
                   Assert.True(lastControlIdx < 0);
                   Assert.True(firstTestIdx < 0);
                   Assert.True(lastTestIdx < 0);
                   Assert.Equal(firstControlIdx, firstTestIdx);
                   Assert.Equal(lastControlIdx, lastTestIdx);
               }

               foreach (var item in include)
               {
                   int firstControlIdx = control.IndexOf(item, 250, 500);
                   int lastControlIdx = control.LastIndexOf(item, 750, 500);
                   int firstTestIdx;
                   int lastTestIdx;
                   {
                       using var lck = test.RoLock();
                       firstTestIdx = lck.IndexOf<UInt64ByRefComparer>(item, 250, 500);
                       lastTestIdx = lck.LastIndexOf<UInt64ByRefComparer>(item, 750, 500);
                   }
                   Assert.Equal(firstControlIdx, firstTestIdx);
                   Assert.Equal(lastControlIdx, lastTestIdx);

               }

               foreach (var item in exclude)
               {
                   int firstControlIdx = control.IndexOf(item, 250, 500);
                   int lastControlIdx = control.LastIndexOf(item, 750, 500);
                   int firstTestIdx;
                   int lastTestIdx;
                   {
                       using var lck = test.RoLock();
                       firstTestIdx = lck.IndexOf<UInt64ByRefComparer>(item, 250, 500);
                       lastTestIdx = lck.LastIndexOf<UInt64ByRefComparer>(item, 750, 500);
                   }
                   Assert.True(firstControlIdx < 0);
                   Assert.True(lastControlIdx < 0);
                   Assert.True(firstTestIdx < 0);
                   Assert.True(lastTestIdx < 0);
                   Assert.Equal(firstControlIdx, firstTestIdx);
                   Assert.Equal(lastControlIdx, lastTestIdx);
               }

               //////
               foreach (var item in include)
               {
                   int firstControlIdx = control.IndexOf(item);
                   int lastControlIdx = control.LastIndexOf(item);
                   int firstTestIdx;
                   int lastTestIdx;
                   ulong firstTestVal, lastTestVal;
                   {
                       using var lck = test.RoLock();
                       firstTestIdx = lck.IndexOf(item, in comparer);
                       lastTestIdx = lck.LastIndexOf(item, in comparer);
                       Assert.True(firstTestIdx > -1 && firstTestIdx < lck.Count);
                       Assert.True(lastTestIdx > -1 && lastTestIdx < lck.Count);
                       firstTestVal = lck[firstTestIdx];
                       lastTestVal = lck[lastTestIdx];
                   }
                   Assert.Equal(firstControlIdx, firstTestIdx);
                   Assert.Equal(lastControlIdx, lastTestIdx);
                   Assert.Equal(item, firstTestVal);
                   Assert.Equal(item, lastTestVal);
                   Assert.Equal(item, control[firstControlIdx]);
                   Assert.Equal(item, control[lastControlIdx]);
               }

               foreach (var item in exclude)
               {
                   int firstControlIdx = control.IndexOf(item);
                   int lastControlIdx = control.LastIndexOf(item);
                   int firstTestIdx;
                   int lastTestIdx;
                   {
                       using var lck = test.RoLock();
                       firstTestIdx = lck.IndexOf(item, in comparer);
                       lastTestIdx = lck.LastIndexOf(item, in comparer);
                   }
                   Assert.True(firstControlIdx < 0);
                   Assert.True(lastControlIdx < 0);
                   Assert.True(firstTestIdx < 0);
                   Assert.True(lastTestIdx < 0);
                   Assert.Equal(firstControlIdx, firstTestIdx);
                   Assert.Equal(lastControlIdx, lastTestIdx);
               }

               foreach (var item in include)
               {
                   int firstControlIdx = control.IndexOf(item, 250, 500);
                   int lastControlIdx = control.LastIndexOf(item, 750, 500);
                   int firstTestIdx;
                   int lastTestIdx;
                   {
                       using var lck = test.RoLock();
                       firstTestIdx = lck.IndexOf(item, 250, 500, in comparer);
                       lastTestIdx = lck.LastIndexOf(item, 750, 500, in comparer);
                   }
                   Assert.Equal(firstControlIdx, firstTestIdx);
                   Assert.Equal(lastControlIdx, lastTestIdx);

               }

               foreach (var item in exclude)
               {
                   int firstControlIdx = control.IndexOf(item, 250, 500);
                   int lastControlIdx = control.LastIndexOf(item, 750, 500);
                   int firstTestIdx;
                   int lastTestIdx;
                   {
                       using var lck = test.RoLock();
                       firstTestIdx = lck.IndexOf(item, 250, 500, in comparer);
                       lastTestIdx = lck.LastIndexOf(item, 750, 500, in comparer);
                   }
                   Assert.True(firstControlIdx < 0);
                   Assert.True(lastControlIdx < 0);
                   Assert.True(firstTestIdx < 0);
                   Assert.True(lastTestIdx < 0);
                   Assert.Equal(firstControlIdx, firstTestIdx);
                   Assert.Equal(lastControlIdx, lastTestIdx);
               }
           }

        }

        [Fact]
        public void TestAddRemoveEtc()
        {
            HashSet<ulong> include = new HashSet<ulong>(new[] { 0xdead_beef_cafe_babe, 0xcafe_beef_dead_babe, 0xbabe_dead_cafe_beef });
            HashSet<ulong> exclude = new HashSet<ulong>(new ulong[] { 0x1234_5678_9ABC_DEF0, 0x0FED_CBA9_8765_4321, 0x1111_1111_1111_1111 });
            const ulong singularAdd = 0xdead_babe_beef_cafe;
            const int arrSize = 10_000;
            List<ulong> control = CreateRandomArray(include, exclude, arrSize).ToList();
            HashSet<ulong> empty = new HashSet<ulong>();
            ImmutableArray<ulong> insertionRange1 =
                CreateRandomArray(empty, empty, 321, 1);
            Assert.Equal(321, insertionRange1.Length);

            ImmutableArray<ulong> insertionRange2 = CreateRandomArray(empty, empty, 41, 1);
            Assert.Equal(41, insertionRange2.Length);

            using ReadWriteValueListVault<ulong> test =
                new ReadWriteValueListVault<ulong>(VsEnumerableWrapper<ulong>.FromIEnumerable(control),
                    TimeSpan.FromMilliseconds(500));
           // UInt64ByRefComparer comparer = default;
            {
                control.Reverse();
                {
                    using var lck = test.Lock();
                    lck.Reverse();
                }
                AssertSame(test, control);
            }

            {
                control.Reverse(500, 250);
                {
                    using var lck = test.Lock();
                    lck.Reverse(500, 250);
                }
                AssertSame(test, control);
            }

            {
                control.RemoveAt(9);
                {
                    using var lck = test.Lock();
                    lck.RemoveAt(9);
                }
                AssertSame(test, control);
            }

            {
                control.InsertRange(99, exclude);
                {
                    using var lck = test.Lock();
                    lck.InsertRange(99, VsEnumerableWrapper<ulong>.FromIEnumerable(exclude));
                }
                AssertSame(test, control);
            }

            {
                control.Insert(21, singularAdd);
                {
                    using var lck = test.Lock();
                    lck.Insert(21, singularAdd);
                }
                AssertSame(test, control);
            }

            {
                control.AddRange(insertionRange1);
                {
                    using var lck = test.Lock();
                    lck.AddRange((VsEnumerableWrapper<ulong>.FromIEnumerable(insertionRange1)));
                }
                AssertSame(test, control);
            }

            {
                control.InsertRange(732, insertionRange2);
                {
                    using var lck = test.Lock();
                    lck.InsertRange(732, (VsEnumerableWrapper<ulong>.FromIEnumerable(insertionRange2)));
                }

                AssertSame(test, control);
            }

            {
                bool removedFromControl = control.Remove(singularAdd);
                bool removedFromTest;
                {
                    using var lck = test.Lock();
                    removedFromTest = lck.Remove(singularAdd);
                }
                Assert.Equal(removedFromControl, removedFromTest);
                Assert.True(removedFromControl);
                AssertSame(test, control);
            }

            {
                int removedFromControlCount = control.RemoveAll(val => val > 0xdead_beef_cafe_babe);
                int removedFromTestCount;
                {
                    using var lck = test.Lock();
                    removedFromTestCount = lck.RemoveAll((in ulong val) => val > 0xdead_beef_cafe_babe);
                }
                Assert.Equal(removedFromControlCount, removedFromTestCount);
                AssertSame(test, control);
            }

            {
                for (int i = 0; i < control.Count; ++i)
                {
                    control[i] = ~control[i];
                }

                {
                    using var lck = test.Lock();
                    lck.ForEach((ref ulong l) => l = ~l);
                }
                AssertSame(test, control);

            }

            {
                const ulong greaterThanMe = 0xbabe_dead_cafe_beef;
                ImmutableArray<ulong> controlGreaterThan = control.Where(itm => itm > greaterThanMe).ToImmutableArray();
                ImmutableArray<ulong> testGreaterThan;
                {
                    var bldr = ImmutableArray.CreateBuilder<ulong>(controlGreaterThan.Length);
                    using var lck = test.Lock();
                    var enumerator = lck.GetFilteredEnumerator((in ulong val) => val > greaterThanMe);
                    while (enumerator.MoveNext())
                    {
                        bldr.Add(enumerator.Current);
                    }
                    Assert.Equal(controlGreaterThan.Length, bldr.Count);
                    testGreaterThan = bldr.MoveToImmutable();
                }
                Assert.True(controlGreaterThan.SequenceEqual(testGreaterThan));
                AssertSame(test, control);

                ImmutableArray<ulong> bitwiseInverseWhereControlGreaterThan =
                    control.Where(itm => itm > greaterThanMe).Select(itm => ~itm).ToImmutableArray();
                ImmutableArray<ulong> bitwiseInverseWhereTestGreaterThan;
                {
                    var bldr = ImmutableArray.CreateBuilder<ulong>(bitwiseInverseWhereControlGreaterThan.Length);
                    using var lck = test.Lock();
                    var enumerator = lck.GetFilteredTransformingEnumerator((in ulong val) => val > greaterThanMe,
                        (in ulong val) => ~val);
                    while (enumerator.MoveNext())
                    {
                        bldr.Add(enumerator.Current);
                    }
                    Assert.Equal(bitwiseInverseWhereControlGreaterThan.Length, bldr.Count);
                    bitwiseInverseWhereTestGreaterThan = bldr.MoveToImmutable();
                }
                
                Assert.True(bitwiseInverseWhereTestGreaterThan.SequenceEqual(bitwiseInverseWhereControlGreaterThan));
                AssertSame(test, control);
            }

            void AssertSame(ReadWriteValueListVault<ulong> tst, List<ulong> ctrl)
            {
                ImmutableArray<ulong> testContents;
                {
                    using var lck = tst.RoLock();
                    testContents = lck.ToArray();
                }
                Assert.True(ctrl.SequenceEqual(testContents));
            }
        }

        private (ReadWriteValueListVault<ulong> EmptyList, ReadWriteValueListVault<ulong> OneItemList,
            ReadWriteValueListVault<ulong> ManyItemsList, ImmutableArray<int> OneIndices, ImmutableArray<int> NineIndices, ulong notInList) CreateTestingLists()
        {
            var emptyList = new ReadWriteValueListVault<ulong>();
            var oneItemList = new ReadWriteValueListVault<ulong>(VsEnumerableWrapper<ulong>.FromIEnumerable(new []{1ul}));
            var manyItemList = new ReadWriteValueListVault<ulong>(VsEnumerableWrapper<ulong>.FromIEnumerable(new[] { 1ul, 9ul, 3ul, 5ul, 9ul, 1ul, 9ul }));
            var oneIndices = new[] {0, 5}.ToImmutableArray();
            var nineIndices = new[] {1, 4, 6}.ToImmutableArray();
            ulong notInList = 69;
            return (emptyList, oneItemList, manyItemList, oneIndices, nineIndices, notInList);
        }

        private ImmutableArray<ulong> TestValueEnumeration(in RoLckRes res)
        {
            var builder = ImmutableArray.CreateBuilder<ulong>();
            var enumerator = res.GetEnumerator();
            while (enumerator.MoveNext())
            {
                builder.Add(enumerator.Current);
            }
            return builder.ToImmutable();
        }

        private ImmutableArray<ulong> TestFilteredEnumeration(in RoLckRes res, [NotNull] RefPredicate<ulong> filter)
        {
            var builder = ImmutableArray.CreateBuilder<ulong>();
            var enumerator = res.GetFilteredEnumerator(filter);
            while (enumerator.MoveNext())
            {
                builder.Add(enumerator.Current);
            }
            return builder.ToImmutable();
        }

        private ImmutableArray<TTransformTo> TestTransformingEnumeration<[VaultSafeTypeParam] TTransformTo>(in RoLckRes res,
            [NotNull] RefFunc<ulong, TTransformTo> transformation) where TTransformTo : unmanaged, IEquatable<TimeSpan>, IComparable<TimeSpan>
        {
            var builder = ImmutableArray.CreateBuilder<TTransformTo>();
            var enumerator = res.GetTransformingEnumerator(transformation);
            while (enumerator.MoveNext())
            {
                builder.Add(enumerator.Current);
            }
            return builder.ToImmutable();
        }

        private ImmutableArray<TimeSpan> TestFilteredTransformingEnumeration(in RoLckRes res,
            [NotNull] RefPredicate<ulong> filter, [NotNull] RefFunc<ulong, TimeSpan> transformation)
        {
            var builder = ImmutableArray.CreateBuilder<TimeSpan>();
            var enumerator = res.GetFilteredTransformingEnumerator(filter, transformation);
            while (enumerator.MoveNext())
            {
                builder.Add(enumerator.Current);
            }
            return builder.ToImmutable();
        }

        private int FindGreaterThan3BetweenIdxOneAndTwoQuery(in RoLckRes res, in int start, in int count) =>
            res.FindIndex(start, count, (in ulong val) => val > 3);
        private int FindLastGreaterThanBetweenIdxOneAndTwoQuery(in RoLckRes res, in int start, in int count) =>
            res.FindLastIndex(start, count, (in ulong val) => val > 3);

        private int FindControlGreaterThan3BetweenIdxOneAndTwoQuery(List<ulong> l) =>
            l.FindIndex(3, 4, val => val > 3);
        private int FindControlLastGreaterThanBetweenIdxOneAndTwoQuery(List<ulong> l) =>
            l.FindLastIndex(3, 4, val => val > 3);


        private bool AllNineTestQuery(in RoLckRes res, in int x, in int y) =>
            res.All((in ulong val) => val == 9);
        private bool AllNineControlQuery(List<ulong> list) => list.All(val => val == 9);
        private bool AnyNineTestQuery(in RoLckRes res, in int x, in int y) =>
            res.Any((in ulong val) => val == 9);
        private bool AnyNineControlQuery(List<ulong> list) => list.Any(val => val == 9);
        private ulong TestSingleNoPredQuery(in RoLckRes res, in int x, in int y) =>
            res.Single();
        private ulong ControlSingleNoPredQuery(List<ulong> list) =>
            list.Single();
        private ulong TestSingleOrDefaultNoPredQuery(in RoLckRes res, in int x, in int y) =>
            res.SingleOrDefault();
        private ulong ControlSingleOrDefaultNoPredQuery(List<ulong> list) =>
            list.SingleOrDefault();
        private ulong TestSingleIsNineQuery(in RoLckRes res, in int x, in int y) =>
            res.Single( (in ulong val) => val == 9);
        private ulong ControlSingleIsNineQuery(List<ulong> list) =>
            list.Single(val => val == 9);
        private ulong TestSingleOrDefaultIsNineQuery(in RoLckRes res, in int x, in int y) =>
            res.SingleOrDefault( (in ulong val) => val == 9);
        private ulong ControlSingleOrDefaultIsNineQuery(List<ulong> list) =>
            list.SingleOrDefault(val => val == 9);
        
        private ulong TestFirstNoPredQuery(in RoLckRes res, in int x, in int y) =>
            res.First();
        private ulong ControlFirstNoPredQuery(List<ulong> list) =>
            list.First();
        private ulong TestFirstOrDefaultNoPredQuery(in RoLckRes res, in int x, in int y) =>
            res.FirstOrDefault();
        private ulong ControlFirstOrDefaultNoPredQuery(List<ulong> list) =>
            list.FirstOrDefault();
        private ulong TestFirstIsNineQuery(in RoLckRes res, in int x, in int y) =>
            res.First((in ulong val) => val == 9);
        private ulong ControlFirstIsNineQuery(List<ulong> list) =>
            list.First(val => val == 9);
        private ulong TestFirstOrDefaultIsNineQuery(in RoLckRes res, in int x, in int y) =>
            res.FirstOrDefault((in ulong val) => val == 9);
        private ulong ControlFirstOrDefaultIsNineQuery(List<ulong> list) =>
            list.FirstOrDefault(val => val == 9);

        private ulong TestLastNoPredQuery(in RoLckRes res, in int x, in int y) =>
            res.Last();
        private ulong ControlLastNoPredQuery(List<ulong> list) =>
            list.Last();
        private ulong TestLastOrDefaultNoPredQuery(in RoLckRes res, in int x, in int y) =>
            res.LastOrDefault();
        private ulong ControlLastOrDefaultNoPredQuery(List<ulong> list) =>
            list.LastOrDefault();
        private ulong TestLastIsNineQuery(in RoLckRes res, in int x, in int y) =>
            res.Last((in ulong val) => val == 9);
        private ulong ControlLastIsNineQuery(List<ulong> list) =>
            list.Last(val => val == 9);
        private ulong TestLastOrDefaultIsNineQuery(in RoLckRes res, in int x, in int y) =>
            res.LastOrDefault((in ulong val) => val == 9);
        private ulong ControlLastOrDefaultIsNineQuery(List<ulong> list) =>
            list.LastOrDefault(val => val == 9);

        private ImmutableArray<ulong> CreateRandomArray(HashSet<ulong> include, HashSet<ulong> exclude, int count, int includeCount = 2)
        {
            Assert.True(include.Count <= count);
            Assert.False(include.Overlaps(exclude));
            List<ulong> arr = new List<ulong>(count);
            foreach (var item in include)
            {
                int timesAdded = 0;
                while (timesAdded++ < includeCount)
                    arr.Add(item);
            }

            while (arr.Count < count)
            {
                ulong random = NextULong(exclude);
                arr.Add(random);
            }
            Shuffle(arr);
            return arr.ToImmutableArray();
        }

        private static void Shuffle<T>(List<T> list)
        {
            Random rgen = TheRgen.Value;
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rgen.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private (ImmutableArray<int> PresentTestIndices, ImmutableArray<int> PresentControlIndices, 
            ImmutableArray<int> AbsentTestIndices, ImmutableArray<int> AbsentControlIndices)
            DoShit(in ValListLockedResource<ReadWriteValueListVault<BigLifeRecord>, BigLifeRecord> test, 
                    List<BigLifeRecord> control, ImmutableArray<BigLifeRecord> present, ImmutableArray<BigLifeRecord> notPresent)
        {
            var presentTestIndices = ImmutableArray.CreateBuilder<int>(present.Length);
            var presentControlIndices = ImmutableArray.CreateBuilder<int>(present.Length);
            var absentTestIndices = ImmutableArray.CreateBuilder<int>(notPresent.Length);
            var absentControlIndices = ImmutableArray.CreateBuilder<int>(notPresent.Length);
            
            Assert.Equal(test.Count, control.Count);
            
            BigLifeRecordComparer comparer = default;
            for (int i = 0; i < present.Length; ++i)
            {
                ref readonly var findMe = ref present.ItemRef(i);
                int testIdx = test.BinarySearch(in findMe, in comparer);
                int ctrlIdx = control.BinarySearch(findMe);
                Assert.True(ctrlIdx == testIdx && ctrlIdx > -1);
                presentTestIndices.Add(testIdx);
                presentControlIndices.Add(ctrlIdx);
            }

            for (int i = 0; i < notPresent.Length; ++i)
            {
                ref readonly var findMe = ref notPresent.ItemRef(i);
                int testIdx = test.BinarySearch(in findMe, in comparer);
                int ctrlIdx = control.BinarySearch(findMe);
                Assert.True(ctrlIdx == testIdx && ctrlIdx < 0);
                absentTestIndices.Add(testIdx);
                absentControlIndices.Add(ctrlIdx);
            }

            Assert.Equal(presentTestIndices.Count, presentTestIndices.Capacity);
            Assert.Equal(presentTestIndices.Count, present.Length);
            Assert.Equal(absentTestIndices.Count, absentTestIndices.Capacity);
            Assert.Equal(absentTestIndices.Count, notPresent.Length);

            Assert.Equal(presentControlIndices.Count, presentControlIndices.Capacity);
            Assert.Equal(presentControlIndices.Count, present.Length);
            Assert.Equal(absentControlIndices.Count, absentControlIndices.Capacity);
            Assert.Equal(absentControlIndices.Count, notPresent.Length);

            return (presentTestIndices.MoveToImmutable(), presentControlIndices.MoveToImmutable(),
                absentTestIndices.MoveToImmutable(), absentControlIndices.MoveToImmutable());
        }

        private ulong NextULong(HashSet<ulong> exclude)
        {
            Span<byte> b = stackalloc byte[8];
            ulong ret;
            do
            {
                ret = 0;
                Random rgen = TheRgen.Value;
               
                rgen.NextBytes(b);
                unchecked
                {
                    for (byte i = 0; i < b.Length; ++i)
                    {
                        ulong asUlong = b[i];
                        ret |= (( asUlong <<  (i * 8)));
                    }
                }
            } while (exclude.Contains(ret));
            return ret;
        }

        [NotNull] private static readonly ThreadLocal<Random> TheRgen = new ThreadLocal<Random>(() => new Random());
    }

    sealed class TestWriterThread : IDisposable
    {
        internal static TestWriterThread CreateWriterThread([NotNull] ReadWriteValueListVault<UInt256> vault) => new TestWriterThread(vault);

        public event EventHandler Faulted;

        private TestWriterThread([NotNull] ReadWriteValueListVault<UInt256> vault)
        {
            _vlv = vault ?? throw new ArgumentNullException(nameof(vault));
            _thread = new Thread(ThreadLoop);
            _startRequested.SetOrThrow();
            _thread.Start(_cts.Token);
            DateTime quitAfter = DateTime.Now + TimeSpan.FromSeconds(1);
            while (!_everStarted.IsSet && DateTime.Now <= quitAfter)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }

            if (!_everStarted.IsSet)
            {
                _cts.Cancel();
                _thread.Join();
                throw new InvalidOperationException("Unable to start thread.");
            }
        }

        public void Dispose() => Dispose(true);

        private void Dispose(bool disposing)
        {
            if (_disposed.TrySet() && disposing)
            {
                if (_everStarted.IsSet && !_terminated.IsSet)
                {
                    _cts.Cancel();
                }
                _thread.Join();
                _cts.Dispose();
                Faulted = null;
            }
        }

        private int GetNextCount() => RGen.Next(Min, Max + 1);

        private void ThreadLoop(object cancTknObj)
        {
            try
            {
                if (_startRequested.IsSet && _everStarted.TrySet())
                {
                    if (cancTknObj is CancellationToken token)
                    {
                        while (true)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                using var lck = _vlv.Lock(token);
                                int numTimesToWrite = GetNextCount();
                                int timesWritten = 0;
                                while (timesWritten++ < numTimesToWrite)
                                {
                                    lck.Add(in WriteMeVal);
                                }
                            }
                            catch (TimeoutException)
                            {
                                Console.Error.WriteLineAsync("Timed out on writer thread.");
                            }
                        }
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"Thread expected {nameof(cancTknObj)} to be of type {typeof(CancellationToken)}.  Actual type: {(cancTknObj?.GetType().Name ?? "NULL REFERENCE")}");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Already started, or start never requested.");
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                OnFaulted();
                throw;
            }
            finally
            {
                _terminated.TrySet();
            }
        }

        private void OnFaulted() => Faulted?.Invoke(this, EventArgs.Empty);
        

        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private Random RGen => TheRgen.Value;
        [NotNull] private readonly ReadWriteValueListVault<UInt256> _vlv;
        [NotNull] private readonly Thread _thread;
        private LocklessSetOnly _disposed = default;
        private LocklessSetOnly _terminated = default;
        private LocklessSetOnly _startRequested = default;
        private LocklessSetOnly _everStarted=default;
        private static readonly UInt256 WriteMeVal = new UInt256(0xb00b_b00b_b00b_b00b, 0xb00b_b00b_b00b_b00b, 0xb00b_b00b_b00b_b00b, 0xb00b_b00b_b00b_b00b);
        [NotNull] private static readonly ThreadLocal<Random> TheRgen = new ThreadLocal<Random>(() => new Random());
        private const int Min = 0;
        private const int Max = 6;
    }

    sealed class TestArbiterThread : IDisposable
    {
        internal static TestArbiterThread CreateArbiterThread([NotNull] ReadWriteValueListVault<UInt256> vault) => new TestArbiterThread(vault);
        public static ref readonly UInt256 CafeBabeVal => ref TheCafeBabeVal;
        public event EventHandler Faulted;

        public event EventHandler Done;
        
        private TestArbiterThread([NotNull] ReadWriteValueListVault<UInt256> vault)
        {
            _vlv = vault ?? throw new ArgumentNullException(nameof(vault));
            _thread = new Thread(ThreadLoop);
            _startRequested.SetOrThrow();
            _thread.Start(_cts.Token);
            DateTime quitAfter = DateTime.Now + TimeSpan.FromSeconds(1);
            while (!_everStarted.IsSet && DateTime.Now <= quitAfter)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }

            if (!_everStarted.IsSet)
            {
                _cts.Cancel();
                _thread.Join();
                throw new InvalidOperationException("Unable to start thread.");
            }
        }

        public bool Join(TimeSpan wait) => _thread.Join(wait);

        public void Dispose() => Dispose(true);

        private void Dispose(bool disposing)
        {
            if (_disposed.TrySet() && disposing)
            {
                if (_everStarted.IsSet && !_terminated.IsSet)
                {
                    _cts.Cancel();
                }
                _thread.Join();
                _cts.Dispose();
                Faulted = null;
            }
        }

        private void ThreadLoop(object cancTknObj)
        {
            try
            {
                if (_startRequested.IsSet && _everStarted.TrySet())
                {
                    if (cancTknObj is CancellationToken token)
                    {
                        while (true)
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                using var lck = _vlv.UpgradableRoLock(token);
                                {
                                    if (lck.Count % 13 == 0 && lck.Count > 0)
                                    {
                                        {
                                            using var writeLock = lck.Lock(token);
                                            int idx = RGen.Next(0, lck.Count);
                                            writeLock.Insert(idx, in TheCafeBabeVal);
                                        }
                                        OnDone();
                                        return;
                                    }
                                }
                            }
                            catch (TimeoutException)
                            {
                                Console.Error.WriteLineAsync("Timed out on arbiter thread.");
                            }
                        }
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"Thread expected {nameof(cancTknObj)} to be of type {typeof(CancellationToken)}.  Actual type: {(cancTknObj?.GetType().Name ?? "NULL REFERENCE")}");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Already started, or start never requested.");
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                OnFaulted();
                throw;
            }
            finally
            {
                _terminated.TrySet();
            }
        }

        private void OnFaulted() => Faulted?.Invoke(this, EventArgs.Empty);

        private void OnDone() => Done?.Invoke(this, EventArgs.Empty);

        [NotNull] private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        [NotNull] private Random RGen => TheRgen.Value;
        [NotNull] private readonly ReadWriteValueListVault<UInt256> _vlv;
        [NotNull] private readonly Thread _thread;
        private LocklessSetOnly _disposed = default;
        private LocklessSetOnly _terminated = default;
        private LocklessSetOnly _startRequested = default;
        private LocklessSetOnly _everStarted = default;
        private static readonly UInt256 TheCafeBabeVal = new UInt256(0xcafe_babe_cafe_babe, 0xcafe_babe_cafe_babe, 0xcafe_babe_cafe_babe, 0xcafe_babe_cafe_babe);
        [NotNull] private static readonly ThreadLocal<Random> TheRgen = new ThreadLocal<Random>(() => new Random());
    }
    
    public struct LocklessSetOnly
    {
        public bool IsSet
        {
            get
            {
                int val = _value;
                return val != NotSet;
            }
        }

        public bool TrySet() => Interlocked.CompareExchange(ref _value, Set, NotSet) == NotSet;

        public void SetOrThrow()
        {
            if (!TrySet()) throw new InvalidOperationException("Already set.");
        }

        private const int NotSet = 0;
        private const int Set = 1;
        private volatile int _value;
    }
}
