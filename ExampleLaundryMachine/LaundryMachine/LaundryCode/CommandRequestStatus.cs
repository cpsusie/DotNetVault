using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe]
    public readonly struct CommandRequestStatus : 
        IEquatable<CommandRequestStatus>, IComparable<CommandRequestStatus>
    {
        
        #region Public Data
        [NotNull] public string FinalStatusExplanation => _finalStatusExplanation ?? string.Empty;
        public readonly CommandIds CommandId;
        public readonly CommandRequestStatusCode StatusCode;
        public readonly DateTime? RequestedTimeStamp;
        public readonly DateTime? PendingOrRefusedTimeStamp;
        public readonly DateTime? CancellationRequestedTimeStamp;
        public readonly DateTime? CompletedFaultingOrCancelledTimeStamp;
        #endregion

        #region CTORs
        public CommandRequestStatus(CommandIds whichCommand)
        {
            CommandId = whichCommand.IsValueDefined()
                ? whichCommand
                : throw new ArgumentOutOfRangeException(nameof(whichCommand), whichCommand,
                    $"The supplied value [{whichCommand}] is not a defined value of the [{typeof(CommandIds).Name}] enumeration.");
            StatusCode = CommandRequestStatusCode.Nil;
            RequestedTimeStamp = null;
            PendingOrRefusedTimeStamp = null;
            CompletedFaultingOrCancelledTimeStamp = null;
            CancellationRequestedTimeStamp = null;
            _finalStatusExplanation = string.Empty;
        }

        private CommandRequestStatus(CommandIds commandId, CommandRequestStatusCode statusCode,
            DateTime? requestedTimeStamp, DateTime? pendingOrRefusedTimeStamp, DateTime? cancellationRequestedTimeStamp,
            DateTime? completedFaultingOrCancelledTimeStamp, string finalStatusExplanation)
        {
            CommandId = commandId;
            StatusCode = statusCode;
            RequestedTimeStamp = requestedTimeStamp;
            PendingOrRefusedTimeStamp = pendingOrRefusedTimeStamp;
            CancellationRequestedTimeStamp = cancellationRequestedTimeStamp;
            CompletedFaultingOrCancelledTimeStamp = completedFaultingOrCancelledTimeStamp;
            _finalStatusExplanation = finalStatusExplanation ?? string.Empty;
        }
        #endregion

        #region public methods
        public override string ToString() => $"Command: [{CommandId}], Status: [{StatusCode}]";

        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus AsRequested(DateTime requestTime)
        {
            if (StatusCode != CommandRequestStatusCode.Nil)
                throw new InvalidOperationException(
                    $"The command is currently in a [{StatusCode}] state " +
                    "and not eligible for the requested state.");
            return new CommandRequestStatus(CommandId, CommandRequestStatusCode.Requested, requestTime, 
                null, null, null, string.Empty);
        }

        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus AsRejected(DateTime rejectTime)
        {
            if (StatusCode != CommandRequestStatusCode.Requested)
                throw new InvalidOperationException(
                    $"The command is currently in a [{StatusCode}] state " +
                    "and not eligible for the refused state.");
            return new CommandRequestStatus(CommandId, CommandRequestStatusCode.Refused, RequestedTimeStamp, rejectTime,
                null, null,string.Empty);
        }

        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus AsPending(DateTime acceptTime)
        {
            if (StatusCode != CommandRequestStatusCode.Requested)
                throw new InvalidOperationException(
                    $"The command is currently in a [{StatusCode}] state " +
                    "and not eligible for the pending state.");
            return new CommandRequestStatus(CommandId, CommandRequestStatusCode.Pending, RequestedTimeStamp, acceptTime,
                null, null, string.Empty);
        }
        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus AsRequestedCancel(DateTime cancelRequestTime)
        {
            if (StatusCode != CommandRequestStatusCode.Pending && StatusCode != CommandRequestStatusCode.Requested)
                throw new InvalidOperationException(
                    $"The command is currently in a [{StatusCode}] state " +
                    "and not eligible for the cancellation requested state.");
            return StatusCode == CommandRequestStatusCode.Requested ? new CommandRequestStatus(CommandId, CommandRequestStatusCode.Cancelled, RequestedTimeStamp, PendingOrRefusedTimeStamp, cancelRequestTime, cancelRequestTime, ""): new CommandRequestStatus(CommandId, CommandRequestStatusCode.CancellationRequested,
                RequestedTimeStamp, PendingOrRefusedTimeStamp, cancelRequestTime, null, string.Empty);
        }
        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus AsCancelled(DateTime cancelled, [NotNull] string cancellationReason)
        {
            if (cancellationReason == null) throw new ArgumentNullException(nameof(cancellationReason));
            if (StatusCode != CommandRequestStatusCode.Pending && StatusCode != CommandRequestStatusCode.CancellationRequested)
                throw new InvalidOperationException(
                    $"The command is currently in a [{StatusCode}] state " +
                    "and not eligible for the cancelled state.");
            return new CommandRequestStatus(CommandId, CommandRequestStatusCode.Cancelled, RequestedTimeStamp,
                PendingOrRefusedTimeStamp,
                CancellationRequestedTimeStamp, cancelled, string.Format(CancelFormatStr, cancellationReason));
        }
        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus AsFaultedNoException(DateTime faulted, [NotNull] string faultingExplanation)
        {
            if (faultingExplanation == null) throw  new ArgumentNullException(nameof(faultingExplanation));
            return PerformAsFaultedNoException(faulted, faultingExplanation, null);
        }
        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus AsFaultedBcException(DateTime faulted, [NotNull] string faultingExplanation,
            [NotNull] Exception faultingEx)
        {
            if (faultingExplanation == null) throw new ArgumentNullException(nameof(faultingExplanation));
            if (faultingEx == null) throw new ArgumentNullException(nameof(faultingEx));
            return PerformAsFaultedNoException(faulted, faultingExplanation, null);
        }
        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus AsRanToCompletion(DateTime done)
        {
            if (StatusCode != CommandRequestStatusCode.Pending)
                throw new InvalidOperationException(
                    $"The command is currently in a [{StatusCode}] state " +
                    "and not eligible for the completed state.");
            return new CommandRequestStatus(CommandId, CommandRequestStatusCode.Completed, RequestedTimeStamp,
                PendingOrRefusedTimeStamp, CancellationRequestedTimeStamp, done, SuccessNoExplStr);
        }
        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus AsRanToCompletion(DateTime done, [NotNull] string extraInfo)
        {
            if (extraInfo == null) throw new ArgumentNullException(nameof(extraInfo));
            if (StatusCode != CommandRequestStatusCode.Pending)
                throw new InvalidOperationException(
                    $"The command is currently in a [{StatusCode}] state " +
                    "and not eligible for the completed state.");
            return new CommandRequestStatus(CommandId, CommandRequestStatusCode.Completed, RequestedTimeStamp,
                PendingOrRefusedTimeStamp, CancellationRequestedTimeStamp, done,
                string.Format(SuccessExplFormatStr, extraInfo));
        }
        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus AsReset() => DoReset(false);
        [System.Diagnostics.Contracts.Pure]
        public CommandRequestStatus ForceReset() => DoReset(true);
        #endregion

        #region Equatable and Comparable Methods and Operators
        public static bool operator ==(in CommandRequestStatus lhs, in CommandRequestStatus rhs) =>
            lhs.CommandId == rhs.CommandId && lhs.StatusCode == rhs.StatusCode &&
            lhs.RequestedTimeStamp == rhs.RequestedTimeStamp &&
            lhs.PendingOrRefusedTimeStamp == rhs.PendingOrRefusedTimeStamp &&
            lhs.CancellationRequestedTimeStamp == rhs.CancellationRequestedTimeStamp &&
            lhs.CompletedFaultingOrCancelledTimeStamp == rhs.CompletedFaultingOrCancelledTimeStamp;
        public static bool operator !=(in CommandRequestStatus lhs, in CommandRequestStatus rhs) => !(lhs == rhs);
        public static bool operator >(in CommandRequestStatus lhs, in CommandRequestStatus rhs) =>
            Compare(in lhs, in rhs) > 0;
        public static bool operator <(in CommandRequestStatus lhs, in CommandRequestStatus rhs) =>
            Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in CommandRequestStatus lhs, in CommandRequestStatus rhs) =>
            !(lhs < rhs);
        public static bool operator <=(in CommandRequestStatus lhs, in CommandRequestStatus rhs) =>
            !(lhs > rhs);
        public override bool Equals(object other) => (other as CommandRequestStatus?) == this;
        public bool Equals(CommandRequestStatus other) => other == this;
        public int CompareTo(CommandRequestStatus other) => Compare(in this, in other);
        
        public override int GetHashCode()
        {
            int hash = CommandIdComparer.GetHashCode(CommandId);
            unchecked
            {
                hash = (hash * 397) ^ (StatusComparer.GetHashCode(StatusCode));
                hash = (hash * 397) ^ RequestedTimeStamp.GetHashCode();
            }
            return hash;
        }
        #endregion

        #region Private Methods

        [System.Diagnostics.Contracts.Pure]
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        private CommandRequestStatus DoReset(bool force)
        {
            if (!force && StatusCode != CommandRequestStatusCode.Completed &&
                StatusCode != CommandRequestStatusCode.Cancelled && StatusCode != CommandRequestStatusCode.Refused &&
                StatusCode != CommandRequestStatusCode.Faulted)
            {
                throw new InvalidOperationException(
                    $"The command is currently in a [{StatusCode}] state, force was not specified:  " +
                    "not eligible for reset.");
            }
            
            return new CommandRequestStatus(CommandId, CommandRequestStatusCode.Nil, null, null, null, null,
                string.Empty);
        }
        [System.Diagnostics.Contracts.Pure]
        private CommandRequestStatus PerformAsFaultedNoException(DateTime faulted, [NotNull] string faultingExplanation,
            [CanBeNull] Exception faultingEx)
        {
            CommandRequestStatus ret;
            switch (StatusCode)
            {
                case CommandRequestStatusCode.Requested:
                    ret = new CommandRequestStatus(CommandId, CommandRequestStatusCode.Faulted, RequestedTimeStamp,
                        PendingOrRefusedTimeStamp, CancellationRequestedTimeStamp, faulted,
                        faultingEx != null
                            ? string.Format(FaultDuringStartupWithExString, faultingExplanation, faultingExplanation)
                            : string.Format(FaultDuringStartupNoExString, faultingExplanation));
                    break;
                case CommandRequestStatusCode.CancellationRequested:
                case CommandRequestStatusCode.Pending:
                    ret = new CommandRequestStatus(CommandId, CommandRequestStatusCode.Faulted, RequestedTimeStamp,
                        PendingOrRefusedTimeStamp, CancellationRequestedTimeStamp, faulted, faultingEx != null
                            ? string.Format(FaultWithExFormatStr, faultingExplanation, faultingExplanation)
                            : string.Format(FaultNoExFormatStr, faultingExplanation));
                    break;
                default:
                    throw new InvalidOperationException($"The command is currently in a [{StatusCode}] state " +
                                                        "and not eligible for the faulted state.");
            }

            return ret;
        }

        private static int Compare(in CommandRequestStatus lhs, in CommandRequestStatus rhs)
        {
            int ret;
            int commandIdComparison = CommandIdComparer.Compare(lhs.CommandId, rhs.CommandId);
            if (commandIdComparison == 0)
            {
                int requestTsComparison = NullableTsHelper.CompareNullableTimeStamps(lhs.RequestedTimeStamp, rhs.RequestedTimeStamp);
                if (requestTsComparison == 0)
                {
                    int statusCodeComparison = StatusComparer.Compare(lhs.StatusCode, rhs.StatusCode);
                    if (statusCodeComparison == 0)
                    {
                        int accRejTsComparison = NullableTsHelper.CompareNullableTimeStamps(lhs.PendingOrRefusedTimeStamp,
                            rhs.PendingOrRefusedTimeStamp);
                        if (accRejTsComparison == 0)
                        {
                            int cancelReqTsComparison = NullableTsHelper.CompareNullableTimeStamps(lhs.CancellationRequestedTimeStamp,
                                rhs.CancellationRequestedTimeStamp);
                            ret= cancelReqTsComparison == 0
                                ? NullableTsHelper.CompareNullableTimeStamps(lhs.CompletedFaultingOrCancelledTimeStamp,
                                    rhs.CompletedFaultingOrCancelledTimeStamp)
                                : cancelReqTsComparison;
                        }
                        else //accRejTsComparison != 0
                        {
                            ret = accRejTsComparison;
                        }
                    }
                    else //statusCodeComparison != 0
                    {
                        ret = statusCodeComparison;
                    }
                }
                else //requestTsComparison != 0
                {
                    ret = requestTsComparison;
                }
            }
            else //commandIdComparison != 0
            {
                ret = commandIdComparison;
            }

            return ret;


        }
        #endregion
        
        #region Private Data
        private const string FaultWithExFormatStr =
            "The task faulted for the following reason: [{0}].  The following exception caused the fault: [{1}]";
        private const string FaultNoExFormatStr = "The task faulted for the following reason: [{0}]";
        private const string CancelFormatStr = "The task was cancelled for the following reason: [{0}].";
        private const string FaultDuringStartupNoExString =
            "The task faulted during startup for the following reason: [{0}].";
        private const string FaultDuringStartupWithExString =
            "The task faulted during startup for the following reason: [{0}].  The following exception caused the fault: [{1}].";
        private const string SuccessNoExplStr = "The task ran to completion.";
        private const string SuccessExplFormatStr = "The task ran to completion.  Additional information: [{0}]";
        private readonly string _finalStatusExplanation;
        private static readonly EnumCompleteComparer<CommandIds, Int32> CommandIdComparer =
            new EnumCompleteComparer<CommandIds, Int32>();
        private static readonly EnumCompleteComparer<CommandRequestStatusCode, Int32>
            StatusComparer = new EnumCompleteComparer<CommandRequestStatusCode, Int32>();

       

        #endregion
    }
}