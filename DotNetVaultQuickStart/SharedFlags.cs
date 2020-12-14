using System;
using HpTimeStamps;

namespace DotNetVaultQuickStart
{
    /// <summary>
    /// This large mutable struct is used in ReadWriteVault demo as its protected resource.
    /// </summary>
    /// <remarks>All properties could be auto-implemented but are not to demonstrate how to use
    /// readonly specifier.</remarks>
    public struct SharedFlags : IEquatable<SharedFlags>, IComparable<SharedFlags>
    {
        /// <summary>
        /// Factory method
        /// </summary>
        /// <returns>Shared flags with a unique guid, timestamp reflecting creation time,
        /// activation action of none and item count of zero</returns>
        public static SharedFlags CreateSharedFlags() =>
            new SharedFlags(Guid.NewGuid(), TimeStampSource.Now, ActiveAction.None, 0);

        /// <summary>
        /// Get the current item count (accessible with readonly and writeable lock)
        /// </summary>
        /// <remarks>Note readonly specification</remarks>
        public readonly ulong ItemCount => _itemCount;
        /// <summary>
        /// Get the timestamp of the last mutation (or construction) of this flag (accessible with readonly and writable lock)
        /// </summary>
        /// <remarks>Note readonly specification on getter.  If your not-auto-implemented
        /// property has a setter, the getter must be marked readonly to prevent defensive deep copy.</remarks>
        public DateTime LastUpdateAt
        {
            readonly get => _lastUpdateTimestamp;
            private set => _lastUpdateTimestamp = value;
        }

        /// <summary>
        /// Get the current active action of the flag (accessible with readonly and writable lock)
        /// </summary>
        public readonly ActiveAction CurrentAction => _currentAction;
        /// <summary>
        /// Get unique id of the flag (accessible with readonly and writable lock)
        /// </summary>
        /// <remarks>Note that auto-implemented (get) accessor is automatically readonly: specifier not needed.</remarks>
        public Guid FlagId { get; }

        #region Non mutating public methods and operators -- all these accessible from readonly and write lock
        /// <summary>
        /// Check whether two flags objects have same value (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if equal, false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator ==(in SharedFlags lhs, in SharedFlags rhs) => lhs.FlagId == rhs.FlagId &&
           lhs.LastUpdateAt == rhs.LastUpdateAt && lhs._itemCount == rhs._itemCount &&
           lhs._currentAction == rhs._currentAction;
        /// <summary>
        /// Check whether two flags objects have distinct values (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if values are distinct, false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator !=(in SharedFlags lhs, in SharedFlags rhs) => !(lhs == rhs);
        /// <summary>
        /// Check whether left hand operand is considered greater than right hand operand (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if left hand operand greater than right hand operand; false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator >(in SharedFlags lhs, in SharedFlags rhs) => Compare(in lhs, in rhs) > 0;
        /// <summary>
        /// Check whether left hand operand is considered less than right hand operand (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if left hand operand less than right hand operand; false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator <(in SharedFlags lhs, in SharedFlags rhs) => Compare(in lhs, in rhs) < 0;
        /// <summary>
        /// Check whether left hand operand is considered greater than or equal to right hand operand (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if left hand operand greater than or equal to right hand operand; false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator >=(in SharedFlags lhs, in SharedFlags rhs) => !(lhs < rhs);
        /// <summary>
        /// Check whether left hand operand is considered less than or equal to right hand operand (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if left hand operand less than or equal to right hand operand; false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator <=(in SharedFlags lhs, in SharedFlags rhs) => !(lhs > rhs);
        /// <summary>
        /// Get hash code (callable from readonly or write lock)
        /// </summary>
        /// <returns>a hash code</returns>
        /// <remarks>Note explicit readonly specifier allowing access from readonly lock ... otherwise,
        /// would cause defensive copy</remarks>
        public override readonly int GetHashCode() => FlagId.GetHashCode();
        /// <summary>
        /// Check to see if this value is the same value as some other object. (callable from readonly or write lock)
        /// </summary>
        /// <param name="other">the other object</param>
        /// <returns>true if same value, false otherwise</returns>
        /// <remarks>Note explicit readonly specifier allowing access from readonly lock ... otherwise,
        /// would cause defensive copy.  Avoid.... requires boxing ... a deep copy if the other object is a SharedFlag</remarks>
        public override readonly bool Equals(object other) => other is SharedFlags sf && sf == this;
        /// <summary>
        /// Check to see if this value is the same value as some other object (callable from readonly or write lock)
        /// </summary>
        /// <param name="other">the other object ... is passed by value as required (sadly) by interface ... avoid</param>
        /// <returns>true if same value, false otherwise</returns>
        /// <remarks>Note explicit readonly specifier allowing access from readonly lock ... otherwise,
        /// would cause defensive copy.  Avoid calling .... interface requires pass by value, resulting in deep copy</remarks>
        public readonly bool Equals(SharedFlags other) => other == this;
        /// <summary>
        /// Compare this value to another of the same type (callable from readonly or write lock)
        /// </summary>
        /// <param name="other">the other value (sadly, interface requires pass by value,
        /// resulting in deep copy).</param>
        /// <returns>
        /// a negative number if this value is less than <paramref name="other"/>
        /// a positive number if this value is greater than <paramref name="other"/>
        /// zero if this value equals <paramref name="other"/>
        /// </returns>
        /// <remarks>Not readonly specification in signature .... needed to prevent the defensive deep copying of this object.
        /// Avoid calling .... interface sadly requires pass by value resulting in deep copy of <paramref name="other"/> parameter.</remarks>
        public readonly int CompareTo(SharedFlags other) => Compare(in this, in other);

        /// <summary>
        /// Get string representation. (callable from readonly or write lock)
        /// </summary>
        /// <returns>a string representation of the value.</returns>
        /// <remarks>Note readonly specifier on method ... necessary to prevent defensive deep copy
        /// of value.</remarks>
        public override readonly string ToString() => "Current action: [" + _currentAction + "]; Last update: [" +
                                             LastUpdateAt.ToString("O") + "]; Item count: [" + _itemCount +
                                             "].";
        /// <summary>
        /// Compare to values of this type. (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>
        /// a negative number if <paramref name="lhs"/> is less than <paramref name="rhs"/>
        /// a positive number if <paramref name="lhs"/> is greater than <paramref name="rhs"/>
        /// zero if <paramref name="lhs"/> equals <paramref name="rhs"/>
        /// </returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static int Compare(in SharedFlags lhs, in SharedFlags rhs)
        {
            int ret;
            int idCompare = lhs.FlagId.CompareTo(rhs.FlagId);
            if (idCompare == 0)
            {
                int tsCompare = lhs.LastUpdateAt.CompareTo(rhs.LastUpdateAt);
                if (tsCompare == 0)
                {
                    int itemCtComp = lhs._itemCount.CompareTo(rhs._itemCount);
                    ret = itemCtComp == 0 ? CompareActions(lhs._currentAction, rhs._currentAction) : itemCtComp;
                }
                else
                {
                    ret = tsCompare;
                }
            }
            else
            {
                ret = idCompare;
            }
            return ret;

            static int CompareActions(ActiveAction la, ActiveAction ra) => ((ulong)la).CompareTo((ulong)ra);
        }
        #endregion

        #region Mutator MEthods -- callable with effect only from write lock .... calling from read lock will in defensive deep copy

        /// <summary>
        /// Start Frobnicating
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="CurrentAction"/> is not equal to <see cref="ActiveAction.None"/></exception>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public void Frobnicate()
        {
            if (_currentAction != ActiveAction.None)
            {
                throw new InvalidOperationException("Can only frobnicate when current action is none.");
            }

            _currentAction = ActiveAction.Frobnicating;
            LastUpdateAt = TimeStampSource.Now;
        }

        /// <summary>
        /// Start Prognosticating
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="CurrentAction"/> is not equal to <see cref="ActiveAction.Frobnicating"/></exception>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public void Prognosticate()
        {
            if (_currentAction != ActiveAction.Frobnicating)
            {
                throw new InvalidOperationException("Can only prognosticate when current action is frobnicate.");
            }

            _currentAction = ActiveAction.Prognosticating;
            LastUpdateAt = TimeStampSource.Now;
        }

        /// <summary>
        /// Start Procrastinating
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="CurrentAction"/> is not equal to <see cref="ActiveAction.Prognosticating"/></exception>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public void Procrastinate()
        {
            if (_currentAction != ActiveAction.Prognosticating)
            {
                throw new InvalidOperationException("Can only procrastinate when current action is prognosticate.");
            }

            _currentAction = ActiveAction.Procrastinating;
            LastUpdateAt = TimeStampSource.Now;
        }

        /// <summary>
        /// Start Dithering
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="CurrentAction"/> is not equal to <see cref="ActiveAction.Procrastinating"/></exception>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public void Dither()
        {
            if (_currentAction != ActiveAction.Procrastinating)
            {
                throw new InvalidOperationException("Can only dither when current action is procrastinate.");
            }

            _currentAction = ActiveAction.Dithering;
            LastUpdateAt = TimeStampSource.Now;
        }

        /// <summary>
        /// Set to Done
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="CurrentAction"/> is not equal to <see cref="ActiveAction.Dithering"/></exception>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public void Finish()
        {
            if (_currentAction != ActiveAction.Dithering)
            {
                throw new InvalidOperationException("Can only finish when current action is dithering.");
            }

            _currentAction = ActiveAction.Done;
            LastUpdateAt = TimeStampSource.Now;
        }

        /// <summary>
        /// Increment <see cref="ItemCount"/> by <paramref name="count"/>
        /// </summary>
        /// <param name="count">amount by which <see cref="ItemCount"/> should be incremented.</param>
        /// <returns>true if incremented (including by zero), false if not incremented (incrementing by zero considered increment for
        /// these purposes).  Will return false if <see cref="CurrentAction"/> is equal to <see cref="ActiveAction.Done"/>.</returns>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public bool Increment(ulong count)
        {
            if (_currentAction == ActiveAction.Done) return false;
            _itemCount += count;
            LastUpdateAt = TimeStampSource.Now;
            return true;
        }
        #endregion

        #region Private CTOR
        private SharedFlags(Guid id, DateTime ts, ActiveAction action, ulong count)
        {
            _itemCount = count;
            FlagId = id;
            _currentAction = action;
            _lastUpdateTimestamp = ts;
        }
        #endregion

        #region Private data
        private ulong _itemCount;
        private DateTime _lastUpdateTimestamp;
        private ActiveAction _currentAction;
        #endregion
    }

    public enum ActiveAction : ulong
    {
        None,
        Frobnicating,
        Prognosticating,
        Procrastinating,
        Dithering,
        Done
    }
}
