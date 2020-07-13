using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DotNetVault.Exceptions;
using DotNetVault.Vaults;
using DotNetVault.VsWrappers;
using JetBrains.Annotations;

namespace DotNetVault.CodeExamples
{
    /// <summary>
    /// Util used for random generation in code examples
    /// </summary>
    public readonly struct RandomUtil
    {
        /// <summary>
        /// Get random date time
        /// </summary>
        public DateTime RandomDateTime => new DateTime(RandomTimeSpan.Ticks, DateTimeKind.Utc);
        
        /// <summary>
        /// Get random timespan
        /// </summary>
        public TimeSpan RandomTimeSpan
        {
            get
            {
                long randomLong = GetPositiveRandomLong(DateTime.MaxValue.Ticks);
                return TimeSpan.FromTicks(randomLong);
            }
        }


        /// <summary>
        /// Create a control and test list of the specified number of big life records
        /// </summary>
        /// <param name="count">number of records</param>
        /// <returns>control and test list</returns>
        /// <exception cref="ArgumentNegativeException"><paramref name="count"/> was null.</exception>
        public (List<BigLifeRecord> Control, ReadWriteValueListVault<BigLifeRecord> Test)
            CreateRandomBigLifeRecords(int count)
        {
            if (count < 0) throw new ArgumentNegativeException<int>(nameof(count), count);
            List<BigLifeRecord> blr = new List<BigLifeRecord>(count);
            while (blr.Count < count)
            {
                blr.Add(GetRandomBigLifeRecordData());
            }

            VsEnumerableWrapper<BigLifeRecord> ctrl =
                VsEnumerableWrapper<BigLifeRecord>.FromIEnumerable(blr);
            ReadWriteValueListVault<BigLifeRecord> tst = new ReadWriteValueListVault<BigLifeRecord>(ctrl);
            Validate(blr, tst);
            return (blr, tst);

            static void Validate(List<BigLifeRecord> control, ReadWriteValueListVault<BigLifeRecord> test)
            {
                if (control.Count != test.GetCurrentCount()) throw new LogicErrorException("Collections do not have same number of items.");

                using var lck = test.RoLock();
                for (int i = 0; i < lck.Count; ++i)
                {
                    BigLifeRecord ctrl = control[i];
                    if (lck[i] != ctrl)
                        throw new LogicErrorException(
                            $"Elements at idx {i} are not equal via == - control: [{ctrl}]; test: [{lck[i]}]");
                    if (BigLifeRecord.Compare(in lck[i], in ctrl) != 0) throw new LogicErrorException(
                        $"Elements at idx {i} are not equal via compare - control: [{ctrl}]; test: [{lck[i]}]");
                    if (lck[i].GetHashCode() != ctrl.GetHashCode()) throw new LogicErrorException(
                        $"Elements at idx {i} do not return same hash code - control: [{ctrl}]; test: [{lck[i]}]");
                }
            }
        }

        /// <summary>
        /// Create a random big life record value
        /// </summary>
        /// <returns>a random big life record value</returns>
        public BigLifeRecord GetRandomBigLifeRecordData()
        {
            while (true)
            {
                try
                {
                    
                    DateTime randomBirthDay = RandomDateTime;
                    bool aliveStill = RGen.Next(0, 2) == 1;
                    decimal balance = GetRandomPositiveDecimal();
                    if (!aliveStill)
                    {
                        int yearsOfLife = RGen.Next(1, 101);
                        TimeSpan lifeSpan = TimeSpan.FromDays(yearsOfLife * 365);
                        DateTime dateOfDeath = randomBirthDay + lifeSpan;

                        return BigLifeRecord.CreateNewDeadRecord(in randomBirthDay, in dateOfDeath, in balance);
                    }
                    return BigLifeRecord.CreateNewLiveRecord(in randomBirthDay, in balance);
                    
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
            }
        }

        /// <summary>
        /// Get many random timespans
        /// </summary>
        /// <param name="count">number of timespans</param>
        /// <returns>A bunch of random timespans</returns>
        public IEnumerable<TimeSpan> GetRandomTimeSpans(int count)
        {
            if (count < 0) throw new ArgumentNullException(nameof(count));
            int actual = 0;
            while (actual++ < count)
            {
                yield return RandomTimeSpan;
            }
        }

        /// <summary>
        /// Get a bunch of random date times
        /// </summary>
        /// <param name="count">number you want</param>
        /// <returns>a bunch of random date times</returns>
        public IEnumerable<DateTime> GetRandomDateTimes(int count)
        {
            if (count < 0) throw new ArgumentNullException(nameof(count));
            int actual = 0;
            while (actual++ < count)
            {
                yield return RandomDateTime;
            }
        }

        /// <summary>
        /// Get a positive random decimal from 1 to a million
        /// </summary>
        /// <returns>a positive random decimal from one to a million.</returns>
        public decimal GetRandomPositiveDecimal() => (decimal) RGen.NextDouble() * 1_000_001 + 1;

        /// <summary>
        /// Get a random long
        /// </summary>
        /// <returns>a random long</returns>
        public long GetRandomLong()
        {
            unchecked
            {
                return (long) GetRandomULong();
            }
        }

        /// <summary>
        /// Shuffle a list of comparable and equatable value types
        /// </summary>
        /// <typeparam name="TList">the list type</typeparam>
        /// <typeparam name="TValue">the value type</typeparam>
        /// <param name="list">the list to shuffle</param>
        /// <exception cref="ArgumentNullException"><paramref name="list"/> was null.</exception>
        /// <exception cref="ArgumentException"><paramref name="list"/> was readonly.</exception>
        public void Shuffle<TList, TValue>([NotNull] TList list) where TList : IList<TValue>
            where TValue : struct, IEquatable<TValue>, IComparable<TValue>
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (list.IsReadOnly) throw new ArgumentException(@"Supplied list is readonly.", nameof(list));
            if (!list.Any()) return;

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = RGen.Next(n + 1);
                TValue value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// Get a positive long integer less than or equal to <paramref name="max"/>.
        /// </summary>
        /// <param name="max">biggest inclusive random you want -- should be a very large number this
        /// is not optimized for producing values that are not very very large</param>
        /// <returns>a long integer</returns>
        /// <remarks>I'm serious -- any max needs to be pretty damn close to max val -- otherwise could
        /// take a VERY long time -- an infinite amount technically</remarks>
        public long GetPositiveRandomLong(long max)
        {
            if (max <= 0) throw new ArgumentNotPositiveException<long>(nameof(max), max);
            unchecked
            {
                long temp;
                bool good;
                bool notMin, lessThanEqMax;
                do
                {
                    temp = (long)GetRandomULong();
                    notMin = temp != long.MinValue;
                    if (notMin)
                    {
                        temp = temp < 0 ? -temp : temp;
                    }
                    lessThanEqMax = notMin && temp <= max;
                    good = notMin && lessThanEqMax;
                } while (!good);
                return temp < 0 ? -temp : temp;
            }
        }
        /// <summary>
        /// Get a random unsigned long integer
        /// </summary>
        /// <returns>a random unsigned long integer</returns>
        public ulong GetRandomULong()
        {
            byte[] eightBytes = new byte[8];
            RGen.NextBytes(eightBytes);
            return BitConverter.ToUInt64(eightBytes, 0);
        }

        private Random RGen => TheRGen.Value;

        private static readonly ThreadLocal<Random> TheRGen = new ThreadLocal<Random>(() => new Random());
    }
}