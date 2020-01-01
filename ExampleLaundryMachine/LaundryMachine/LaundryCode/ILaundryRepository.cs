using System;
using System.Collections.Immutable;
using System.Threading;

namespace LaundryMachine.LaundryCode
{
    public interface ILaundryRepository : IDisposable
    {
        event EventHandler<LaundryRepositoryEventArgs> ContentsChanged;
        int Count { get; }
        Guid Id { get; }
        bool ForDirtyLaundry { get; }
        bool IsDisposed { get; }
        string Description { get; }
        void Add(in LaundryItems li);
        (bool RemovedOk, LaundryItems Item) Remove(CancellationToken token);
        (bool RemovedOk, LaundryItems Item) Remove(TimeSpan ts, CancellationToken token);
        ImmutableList<LaundryItems> Dump();
    }
}