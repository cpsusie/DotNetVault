using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    internal sealed class LaundryRepository : ILaundryRepository
    {
        public static ILaundryRepository CreateRepository([NotNull] string description, bool isForDirty) => new LaundryRepository(description, isForDirty);

        public event EventHandler<LaundryRepositoryEventArgs> ContentsChanged;
        public Guid Id { get; } = Guid.NewGuid();
        public int Count => _collection.Count;
        public bool ForDirtyLaundry {get; }

        public bool IsDisposed => _disposed.IsSet;
        [NotNull] public string Description { get; }

        private LaundryRepository([NotNull] string description, bool forDirty)
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
            _eventThread = EventRaisingThread.CreateEventRaiser(description);
            ForDirtyLaundry = forDirty;
        }

        public void Dispose() => Dispose(true);

        public void Add(in LaundryItems li)
        {
            ThrowIfDisposed(nameof(Add));
            _collection.Add(li);
            OnRepositoryChanged(new LaundryRepositoryEventArgs(this, li, true));
        }

        public (bool RemovedOk, LaundryItems Item) Remove(CancellationToken token) => Remove(_defaultWait, token);

        public (bool RemovedOk, LaundryItems Item) Remove(TimeSpan ts, CancellationToken token)
        {
            ThrowIfDisposed(nameof(Remove));
            LaundryItems item;
            bool gotIt = _collection.TryTake(out item, (int) ts.TotalMilliseconds, token);
            if (gotIt)
            {
                LaundryRepositoryEventArgs args = new LaundryRepositoryEventArgs(this, item, false);
                OnRepositoryChanged(args);
            }
            return (gotIt, item);
        }

        public ImmutableList<LaundryItems> Dump() =>
            _finalContents.IsSet ? _finalContents.Value : ImmutableList<LaundryItems>.Empty;

        private void Dispose(bool disposing)
        {
            if (disposing && _disposed.TrySet())
            {
                _collection.CompleteAdding();
                _finalContents.SetOrThrow(ImmutableList.Create(_collection.ToArray()));
                _collection.Dispose();
                _eventThread.Dispose();
                ContentsChanged = null;
            }
            _disposed.TrySet();
        }

        private void ThrowIfDisposed([NotNull] string name)
        {
            if (_disposed.IsSet)
                throw new ObjectDisposedException(nameof(LaundryRepository),
                    $"Illegal call to repository [id: {Id}]'s {name} method: the object has been disposed.");
        }

        private void OnRepositoryChanged([NotNull] LaundryRepositoryEventArgs e)
        {
            if (!_eventThread.IsDisposed && _eventThread.ThreadActive)
            {
                try
                {
                    _eventThread.AddAction(() => ContentsChanged?.Invoke(this, e));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLineAsync(ex.ToString());
                }
            }
            else
            {
                ContentsChanged?.Invoke(this, e);
            }
        }

        private readonly LocklessWriteOnce<ImmutableList<LaundryItems>> _finalContents = new LocklessWriteOnce<ImmutableList<LaundryItems>>();
        private LocklessSetOnceFlagVal _disposed = new LocklessSetOnceFlagVal();
        private readonly TimeSpan _defaultWait = TimeSpan.FromMilliseconds(250);
        [NotNull] private readonly IEventRaiser _eventThread;
        [NotNull] private readonly BlockingCollection<LaundryItems> _collection = new BlockingCollection<LaundryItems>(new ConcurrentQueue<LaundryItems>());
    }
}
