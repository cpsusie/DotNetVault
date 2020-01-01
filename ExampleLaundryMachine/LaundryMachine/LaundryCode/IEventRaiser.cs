using System;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public interface IEventRaiser  : IDisposable
    {
        event EventHandler<DelegateExceptionEventArgs> HandlerThrew;
        bool IsDisposed { get; }
        bool ThreadActive { get; }
        void AddAction([NotNull] Action item);
    }
    
}