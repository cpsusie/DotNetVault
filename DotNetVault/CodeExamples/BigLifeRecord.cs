using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using DotNetVault.RefReturningCollections;

namespace DotNetVault.CodeExamples
{
    /// <summary>
    /// A large comparable and equatable struct used in demonstrating big value list vault
    /// </summary>
    public readonly struct BigLifeRecord : IEquatable<BigLifeRecord>, IComparable<BigLifeRecord>
    {
        #region Default Value
        /// <summary>
        /// Invalid default
        /// </summary>
        public static ref readonly BigLifeRecord InvalidDefault => ref TheDefault;
        #endregion

        #region Factories
        /// <summary>
        /// Create a new record of an alive person.
        /// </summary>
        /// <param name="dob">dob</param>
        /// <param name="balance">balance</param>
        /// <returns>record</returns>
        public static BigLifeRecord CreateNewLiveRecord(in DateTime dob, in decimal balance)
        {
            var universalDob = dob.ToUniversalTime();
            Guid id = Guid.NewGuid();
            return new BigLifeRecord(in id, in universalDob, null, in balance);
        }

        /// <summary>
        /// Create new record of a dead person.  
        /// </summary>
        /// <param name="dob">date of birth</param>
        /// <param name="dod">date of death</param>
        /// <param name="balance">balance</param>
        /// <returns>Record</returns>
        /// <exception cref="ArgumentException">Dob greater than dod.</exception>
        public static BigLifeRecord CreateNewDeadRecord(in DateTime dob, in DateTime dod, in decimal balance)
        {

            var universalDob = dob.ToUniversalTime();
            DateTime? universalDod = dod.ToUniversalTime();
            if (universalDob > universalDod) throw new ArgumentException("Date of birth cannot be greater than date of death.");
            Guid id = Guid.NewGuid();
            return new BigLifeRecord(in id, in universalDob, in universalDod, in balance);
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// True if this value has not been initialized and is thus invalid
        /// </summary>
        public bool IsInvalidDefault => this == InvalidDefault;
        /// <summary>
        /// unique identifier for this record
        /// </summary>
        public Guid RecordId => _recordId;
        /// <summary>
        /// Birth date of person to whom this record refers
        /// </summary>
        public DateTime DateOfBirth => _dateOfBirth;
        /// <summary>
        /// If the person is dead, the date of their death; otherwise, null.
        /// </summary>
        public DateTime? DateOfDeath => _dateOfDeath;
        /// <summary>
        /// Current account balance
        /// </summary>
        public decimal AccountBalance => _accountBalance;
        /// <summary>
        /// True if the person is still alive, false otherwise
        /// </summary>
        public bool IsAlive => _dateOfDeath == null;
        /// <summary>
        /// Persons current age if alive, otherwise age at time of death.
        /// </summary>
        public TimeSpan Age => _dateOfDeath == null ? (DateTime.UtcNow - _dateOfBirth) : (_dateOfDeath.Value - _dateOfBirth);
        /// <summary>
        /// Amount of time since death if person dead otherwise null.
        /// </summary>
        public TimeSpan? TimeSinceDeath =>
            _dateOfDeath != null ? (TimeSpan?)(DateTime.UtcNow - _dateOfDeath.Value) : null;
        #endregion
       
        #region CTOR
        private BigLifeRecord(in Guid id, in DateTime dob, in DateTime? dod, in decimal balance)
        {
            _recordId = id;
            _dateOfBirth = dob;
            _dateOfDeath = dod;
            _accountBalance = balance;
        }
        #endregion

        #region Public Methods and Operators
        /// <summary>
        /// Get a value based hashcode 
        /// </summary>
        /// <returns>a hash code</returns>
        public override int GetHashCode() => _recordId.GetHashCode();
        /// <summary>
        /// Test two values to see if the first equals the second
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if <paramref name="lhs"/> equals <paramref name="rhs"/>, false otherwise</returns>
        public static bool operator ==(in BigLifeRecord lhs, in BigLifeRecord rhs) =>
            lhs._recordId == rhs._recordId && lhs._accountBalance == rhs._accountBalance &&
            lhs._dateOfBirth == rhs._dateOfBirth && lhs._dateOfDeath == rhs._dateOfDeath;
        /// <summary>
        /// Test two values to see if the first does not equal the second
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if <paramref name="lhs"/> does not equal <paramref name="rhs"/>, false otherwise</returns>
        public static bool operator !=(in BigLifeRecord lhs, in BigLifeRecord rhs) => !(lhs == rhs);
        /// <summary>
        /// Test two values to see if the first is greater than the second
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if <paramref name="lhs"/> is greater than <paramref name="rhs"/>, false otherwise</returns>
        public static bool operator >(in BigLifeRecord lhs, in BigLifeRecord rhs) => Compare(in lhs, in rhs) > 0;
        /// <summary>
        /// Test two values to see if the first is less than the second
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if <paramref name="lhs"/> is less than <paramref name="rhs"/>, false otherwise</returns>
        public static bool operator <(in BigLifeRecord lhs, in BigLifeRecord rhs) => Compare(in lhs, in rhs) < 0;
        /// <summary>
        /// Test two values to see if the first is greater than or equal to the second
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if <paramref name="lhs"/> is greater than or equal to <paramref name="rhs"/>, false otherwise</returns>
        public static bool operator >=(in BigLifeRecord lhs, in BigLifeRecord rhs) => !(lhs < rhs);
        /// <summary>
        /// Test two values to see if the first is less than or equal to the second
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if <paramref name="lhs"/> is less than or equal to <paramref name="rhs"/>, false otherwise</returns>
        public static bool operator <=(in BigLifeRecord lhs, in BigLifeRecord rhs) => !(lhs > rhs);

        /// <inheritdoc />
        public bool Equals(BigLifeRecord other) => other == this;

        /// <inheritdoc />
        public override bool Equals(object other) => other is BigLifeRecord blf && blf == this;

        /// <inheritdoc />
        public int CompareTo(BigLifeRecord other) => Compare(in this, in other);

        /// <inheritdoc />
        public override string ToString() => "Record id: [" + _recordId + "]; Status: [" +
                                             (IsAlive ? "ALIVE" : "DEAD") + "]; Age: [" + (Age.TotalDays / 365) +
                                             " years]; " +
                                             (TimeSinceDeath != null
                                                 ? ("Time since death: [" + TimeSinceDeath.Value.TotalDays / 365 +
                                                    " years]; ")
                                                 : string.Empty) + "Account balance: [" +
                                             _accountBalance.ToString("C") + "].";

        /// <summary>
        /// Compare two values
        /// </summary>
        /// <param name="lhs">the first value</param>
        /// <param name="rhs">the second value</param>
        /// <returns>A negative number if <paramref name="lhs"/> is less than
        /// <paramref name="rhs"/>;
        /// 0 if <paramref name="lhs"/> equals <paramref name="rhs"/>;
        /// a positive number if <paramref name="lhs"/> is greater than <paramref name="rhs"/>
        /// </returns>
        /// <remarks>Comparison priority - birth date, death date, balance, guid.</remarks>
        public static int Compare(in BigLifeRecord lhs, in BigLifeRecord rhs)
        {
            int ret;
            int birthComparison = lhs._dateOfBirth.CompareTo(rhs._dateOfBirth);
            if (birthComparison == 0)
            {
                int deathComparison = CompareDeathDates(in lhs._dateOfDeath, in rhs._dateOfDeath);
                if (deathComparison == 0)
                {
                    int balanceComparison = lhs._accountBalance.CompareTo(rhs._accountBalance);
                    ret = balanceComparison == 0 ? lhs._recordId.CompareTo(rhs._recordId) : balanceComparison;
                }
                else
                {
                    ret = deathComparison;
                }
            }
            else
            {
                ret = birthComparison;
            }

            return ret;

            static int CompareDeathDates(in DateTime? lhs, in DateTime? rhs)
            {
                if (lhs == rhs) return 0;
                if (lhs == null) return -1;
                if (rhs == null) return 1;
                return lhs.Value.CompareTo(rhs.Value);
            }
        } 
        #endregion

        #region Update Methods
        /// <summary>
        /// Get a record based on this one with an updated balance
        /// </summary>
        /// <param name="newBalance">new balance</param>
        /// <returns>a record with updated balance</returns>
        [Pure]
        public BigLifeRecord UpdateAccountBalance(in decimal newBalance)
            => new BigLifeRecord(in _recordId, in _dateOfBirth, in _dateOfDeath, in newBalance);

        /// <summary>
        /// Get updated record with today as date of death
        /// </summary>
        /// <returns>A new record with today as death date</returns>
        /// <exception cref="InvalidOperationException">Already dead.</exception>
        [Pure]
        public BigLifeRecord UpdateTodayDateOfDeath() => UpdateWithDateOfDeath(DateTime.UtcNow);

        /// <summary>
        /// Submit a death date
        /// </summary>
        /// <param name="dateOfDeath">date of death</param>
        /// <returns>A new record with today as death date</returns>
        /// <exception cref="InvalidOperationException">Already dead.</exception>
        /// <exception cref="ArgumentException"><paramref name="dateOfDeath"/> less than dob</exception>
        [Pure]
        public BigLifeRecord UpdateWithDateOfDeath(in DateTime dateOfDeath)
        {
            if (!IsAlive) throw new InvalidOperationException("Record is already recorded as dead.");
            DateTime? universalDeath = dateOfDeath.ToUniversalTime();
            if (universalDeath.Value <= _dateOfBirth) throw new ArgumentException("Date of death must be greater than date of birth.");
            return new BigLifeRecord(in _recordId, in _dateOfBirth, in universalDeath, in _accountBalance);
        }
        #endregion

        #region Privates
        private readonly Guid _recordId;
        private readonly DateTime _dateOfBirth;
        private readonly DateTime? _dateOfDeath;
        private readonly decimal _accountBalance;
        private static readonly BigLifeRecord TheDefault = default;
        #endregion
    }

    /// <inheritdoc />
    public readonly struct BigLifeRecordComparer : IByRefCompleteComparer<BigLifeRecord>
    {
        /// <inheritdoc />
        public bool IsValid => true;
        /// <inheritdoc />
        public bool WorksCorrectlyWhenDefaultConstructed => true;
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(in BigLifeRecord lhs, in BigLifeRecord rhs) => BigLifeRecord.Compare(in lhs, in rhs);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(in BigLifeRecord lhs, in BigLifeRecord rhs) => lhs == rhs;
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(in BigLifeRecord obj) => obj.GetHashCode();
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(BigLifeRecord x, BigLifeRecord y) => Equals(in x, in y);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(BigLifeRecord obj) => GetHashCode(in obj);
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(BigLifeRecord x, BigLifeRecord y) => Compare(in x, in y);

    }

 
}
