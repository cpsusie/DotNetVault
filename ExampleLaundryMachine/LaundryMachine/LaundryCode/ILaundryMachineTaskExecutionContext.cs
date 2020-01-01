using System;
using DotNetVault.Attributes;

namespace LaundryMachine.LaundryCode
{
    public interface ILaundryMachineTaskExecutionContext<[VaultSafeTypeParam] TResult> : IDisposable where TResult : struct, IEquatable<TResult>
    {
        event EventHandler Disposed;
        event EventHandler Faulted;
        event EventHandler Terminated;
        bool IsTaskBeingProcessedNow { get; }
        bool IsFaulted { get; }
        bool IsActive { get; }
        bool IsTerminated { get; }
        bool IsDisposed { get; }
        void ExecuteTask(in TaskFunctionControlBlock<TaskResult> executeMe);
    }
}