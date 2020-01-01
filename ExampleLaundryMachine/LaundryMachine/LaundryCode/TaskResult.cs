using System;
using DotNetVault.Attributes;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    [VaultSafe]
    public readonly struct TaskResult : IComparable<TaskResult>, IEquatable<TaskResult>
    {
        #region Static Factory Methods
        public static TaskResult CreateNewTask(DateTime startedAt, TaskType task) =>
            new TaskResult(startedAt, task, TaskResultCode.StillPendingResult, null, string.Empty);
        public static TaskResult CreateNewTask(TaskType task) => new TaskResult(TimeStampSource.Now, task,
            TaskResultCode.StillPendingResult, null, string.Empty); 
        #endregion

        #region Public Properties
        public DateTime StartedAtTimeStamp { get; }
        public TaskType Task { get; }
        public TaskResultCode TerminationStatus { get; }
        public DateTime? TerminationTimeStamp { get; }
        [NotNull] public string Explanation => _explanation ?? string.Empty; 
        #endregion

        #region Equatable/Comparable methods and operators
        public static bool operator ==(in TaskResult lhs, in TaskResult rhs) =>
            lhs.StartedAtTimeStamp == rhs.StartedAtTimeStamp && lhs.Task == rhs.Task &&
                lhs.TerminationStatus == rhs.TerminationStatus && lhs.TerminationTimeStamp == rhs.TerminationTimeStamp;
        public static bool operator !=(in TaskResult lhs, in TaskResult rhs) => !(lhs == rhs);
        public static bool operator >(in TaskResult lhs, in TaskResult rhs) => Compare(in lhs, in rhs) > 0;
        public static bool operator <(in TaskResult lhs, in TaskResult rhs) => Compare(in lhs, in rhs) < 0;
        public static bool operator >=(in TaskResult lhs, in TaskResult rhs) => !(lhs < rhs);
        public static bool operator <=(in TaskResult lhs, in TaskResult rhs) => !(lhs > rhs);
        public override bool Equals(object other) => (other as TaskResult?) == this;
        public bool Equals(TaskResult other) => this == other;
        public int CompareTo(TaskResult other) => Compare(in this, in other);
        public override int GetHashCode()
        {
            int hash = Task.GetHashCode();
            unchecked
            {
                hash = (hash * 397) ^ StartedAtTimeStamp.GetHashCode();
            }
            return hash;
        } 
        #endregion

        #region Public Methods
        public override string ToString()
        {
            string terminationTimeStamp = TerminationTimeStamp != null
                ? $"  terminated at [{TerminationTimeStamp.Value:O}]"
                : string.Empty;
            string explanation = !string.IsNullOrWhiteSpace(Explanation)
                ? $" with explanation [{Explanation}]"
                : string.Empty;
            return
                $"Task [{Task}] started at [{StartedAtTimeStamp:O}] in state [{TerminationStatus}]{terminationTimeStamp}{explanation}.";
        }
        
        [System.Diagnostics.Contracts.Pure]
        public TaskResult WithExplanation(string explanation) => new TaskResult(StartedAtTimeStamp, Task,
           TerminationStatus, TerminationTimeStamp, explanation);
        [System.Diagnostics.Contracts.Pure]
        public TaskResult WithTerminationTaskResultType(TaskResultCode result) =>
            WithTerminationResultExplanationAndTimeStamp(TimeStampSource.Now, result, string.Empty);
        [System.Diagnostics.Contracts.Pure]
        public TaskResult WithTerminationResultAndExplanation(TaskResultCode result, [CanBeNull] string explanation) =>
            WithTerminationResultExplanationAndTimeStamp(TimeStampSource.Now, result, explanation);
        [System.Diagnostics.Contracts.Pure]
        public TaskResult WithTerminationResultExplanationAndTimeStamp(DateTime terminationTimeStamp,
            TaskResultCode result, [CanBeNull] string explanation) => new TaskResult(StartedAtTimeStamp, Task, result,
            terminationTimeStamp, explanation ?? string.Empty); 
        #endregion
        
        #region Private CTOR
        private TaskResult(DateTime startedAt, TaskType type, TaskResultCode terminationStatus,
            DateTime? terminationTimeStamp, [CanBeNull] string explanation)
        {
            StartedAtTimeStamp = startedAt;
            Task = type;
            TerminationStatus = terminationStatus;
            TerminationTimeStamp = terminationTimeStamp;
            _explanation = explanation;
        } 
        #endregion

        #region Private Methods
        private static int Compare(in TaskResult lhs, in TaskResult rhs)
        {
            int ret;
            int startAtComparison = lhs.StartedAtTimeStamp.CompareTo(rhs.StartedAtTimeStamp);
            if (startAtComparison == 0)
            {
                int taskTypeComparison = CompareTaskType(lhs.Task, rhs.Task);
                if (taskTypeComparison == 0)
                {
                    int taskResultComparison = CompareTaskResult(lhs.TerminationStatus, rhs.TerminationStatus);
                    if (taskResultComparison == 0)
                    {
                        ret = taskResultComparison == 0
                            ? CompareNullableDt(lhs.TerminationTimeStamp, rhs.TerminationTimeStamp)
                            : taskResultComparison;
                    }
                    else
                    {
                        ret = taskResultComparison;
                    }
                }
                else
                {
                    ret = taskTypeComparison;
                }
            }
            else
            {
                ret = startAtComparison;
            }

            return ret;

            static int CompareTaskType(TaskType l, TaskType r) => ((Int32)l).CompareTo((Int32)r);
            static int CompareTaskResult(TaskResultCode l, TaskResultCode r) => ((Int32)l).CompareTo((Int32)r);
        }

        private static int CompareNullableDt(DateTime? lhs, DateTime? rhs)
        {
            if (lhs == null && rhs == null) return 0;
            if (lhs == null) return -1;
            if (rhs == null) return 1;

            return lhs.Value.CompareTo(rhs.Value);
        } 
        #endregion

        #region Private Fields
        private readonly string _explanation; 
        #endregion
    }
}