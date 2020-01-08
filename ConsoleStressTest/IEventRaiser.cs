using System;
using JetBrains.Annotations;

namespace ConsoleStressTest
{
    public interface IEventRaiser : IDisposable
    {
        event EventHandler<DelegateExceptionEventArgs> HandlerThrew;
        bool IsDisposed { get; }
        bool ThreadActive { get; }
        void AddAction([NotNull] Action item);
    }
}