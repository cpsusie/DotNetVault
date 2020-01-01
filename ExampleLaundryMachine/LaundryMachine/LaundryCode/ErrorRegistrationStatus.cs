using System;
using System.ComponentModel;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe]
    public readonly struct ErrorRegistrationStatus : IEquatable<ErrorRegistrationStatus>,
        IComparable<ErrorRegistrationStatus>
    {

        #region Static Initial Value
        public static ErrorRegistrationStatus NilStatus { get; } = default; 
        #endregion
        #region Public Data
        public readonly Guid ErrorIdentifier;
        public readonly ErrorRegistrationStatusCode StatusCode;
        public readonly DateTime? RegisteredTimeStamp;
        public readonly bool IsLogicError;
        [NotNull] public string Explanation => _errorExplanation ?? string.Empty; 
        #endregion

        #region Public Methods
        [Pure]
        public ErrorRegistrationStatus AsRegistered(DateTime registeredAt, [NotNull] string explanation, bool isLogicError)
        {
            Guid id = Guid.NewGuid();
            return new ErrorRegistrationStatus(ErrorRegistrationStatusCode.Registered, id, registeredAt,
                explanation ?? throw new ArgumentNullException(nameof(explanation)), isLogicError);
        }

        [Pure]
        public ErrorRegistrationStatus AsProcessed(DateTime processed, [NotNull] string processInfo)
        {
            if (processInfo == null) throw new ArgumentNullException(nameof(processInfo));
            if (StatusCode != ErrorRegistrationStatusCode.Registered)
                throw new InvalidOperationException("Cannot process an item not in registered state.");

            return new ErrorRegistrationStatus(ErrorRegistrationStatusCode.Processed, ErrorIdentifier,
                RegisteredTimeStamp,
                string.Concat($"Reg Info: [{Explanation}]{Environment.NewLine}",
                    $"Proc Info: [{processInfo}]{Environment.NewLine}"), IsLogicError);
        }

        [Pure]
        public ErrorRegistrationStatus AsCleared(DateTime processed, [NotNull] string clearInfo)
        {
            if (clearInfo == null) throw new ArgumentNullException(nameof(clearInfo));
            if (StatusCode != ErrorRegistrationStatusCode.Processed)
                throw new InvalidOperationException("Cannot clear an item not in processed state.");
            return new ErrorRegistrationStatus(ErrorRegistrationStatusCode.Cleared, ErrorIdentifier,
                RegisteredTimeStamp, string.Concat(Explanation, Environment.NewLine, "CLEARED: " + clearInfo), IsLogicError);
        }

        [Pure]
        public ErrorRegistrationStatus Reset() => default;
        public override string ToString() => StatusCode == ErrorRegistrationStatusCode.Nil
            ? "NIL ERROR STATUS"
            : $"Error registered at [{RegisteredTimeStamp?.ToString("O") ?? "UNKNOWN"}], Identifier: [{ErrorIdentifier}], Info: [{Explanation}]";
        #endregion
        
        #region Equatable / Comparble Methods and Operators
        public static bool operator ==(in ErrorRegistrationStatus lhs, in ErrorRegistrationStatus rhs) =>
            lhs.ErrorIdentifier == rhs.ErrorIdentifier && TheEnumComparer.Equals(lhs.StatusCode, rhs.StatusCode) && lhs.IsLogicError == rhs.IsLogicError &&
            lhs.RegisteredTimeStamp == rhs.RegisteredTimeStamp;
        public static bool operator !=(in ErrorRegistrationStatus lhs, in ErrorRegistrationStatus rhs) => !(lhs == rhs);
        public static bool operator >(in ErrorRegistrationStatus lhs, in ErrorRegistrationStatus rhs) =>
            Compare(in lhs, in rhs) > 0;
        public static bool operator <(in ErrorRegistrationStatus lhs, in ErrorRegistrationStatus rhs) =>
            Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in ErrorRegistrationStatus lhs, in ErrorRegistrationStatus rhs) =>
            !(lhs < rhs);
        public static bool operator <=(in ErrorRegistrationStatus lhs, in ErrorRegistrationStatus rhs) =>
            !(lhs > rhs);
        public override bool Equals(object obj) => (obj as ErrorRegistrationStatus?) == this;
        public bool Equals(ErrorRegistrationStatus other) => other == this;
        public override int GetHashCode() => ErrorIdentifier.GetHashCode();
        public int CompareTo(ErrorRegistrationStatus other) => Compare(in this, in other);
        #endregion


        #region Private CTOR and Methods
        private ErrorRegistrationStatus(ErrorRegistrationStatusCode code, Guid errorIdentifier, DateTime? regTs,
            string explanation, bool isLogicError)
        {
            StatusCode = code.IsValueDefined()
                ? code
                : throw new InvalidEnumArgumentException(nameof(code), (int) code, typeof(ErrorRegistrationStatusCode));
            RegisteredTimeStamp = regTs;
            _errorExplanation = explanation ?? string.Empty;
            ErrorIdentifier = errorIdentifier;
            IsLogicError = isLogicError;
        }

        private static int Compare(in ErrorRegistrationStatus lhs, in ErrorRegistrationStatus rhs)
        {
            int ret;
            int tsComp = NullableTsHelper.CompareNullableTimeStamps(lhs.RegisteredTimeStamp, rhs.RegisteredTimeStamp);
            if (tsComp == 0)
            {
                int guidComp = lhs.ErrorIdentifier.CompareTo(rhs.ErrorIdentifier);
                if (guidComp == 0)
                {
                    int logicComp = lhs.IsLogicError.CompareTo(rhs.IsLogicError);
                    ret= logicComp == 0 ? TheEnumComparer.Compare(lhs.StatusCode, rhs.StatusCode) : logicComp;
                }
                else
                {
                    ret = guidComp;
                }
            }
            else //tsComp != 0
            {
                ret = tsComp;
            }

            return ret;
        }
        #endregion

        #region Private Data
        private readonly string _errorExplanation;
        private static readonly EnumComparer<ErrorRegistrationStatusCode>
            TheEnumComparer = new EnumComparer<ErrorRegistrationStatusCode>();
        #endregion
    }
}