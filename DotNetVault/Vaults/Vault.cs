using System;
using System.Runtime.CompilerServices;
using DotNetVault.Interfaces;
using DotNetVault.Logging;
using JetBrains.Annotations;
using SetOnceValFlag = DotNetVault.ToggleFlags.SetOnceValFlag;
using TTwoStepDisposeFlag = DotNetVault.DisposeFlag.TwoStepDisposeFlag;
using TSimpleDisposeFlag = DotNetVault.DisposeFlag.DisposeFlag;

namespace DotNetVault.Vaults
{
    /// <summary>
    /// The base class for all vault objects.  Vault objects isolate
    /// the protected resource (of type <typeparamref name="T"/>) and prevent access
    /// to them when not "checked-out" to a locked resource object.  When the locked resource object goes out
    /// of scope, the resource is returned to the vault automatically for use by other threads.
    /// </summary>
    /// <typeparam name="T">the protected resource type</typeparam>
    public abstract class Vault<T> : IVault
    {
        /// <summary>
        /// True if the dispose operation is in progress at moment of call, false otherwise
        /// </summary>
        public bool DisposeInProgress => _disposeFlag.IsDisposing;
        /// <summary>
        /// True if disposing or disposed, false otherwise.
        /// </summary>
        public bool IsDisposed => !_disposeFlag.IsClear;

        /// <summary>
        /// The default amount of time to wait while attempting to acquire a lock
        /// before throwing an <see cref="TimeoutException"/>
        /// </summary>
        public TimeSpan DefaultTimeout => _defaultTimeout;
        /// <summary>
        /// The Box/Ptr to resource the vault protects
        /// </summary>
        protected Box BoxPtr => _resourcePtr;
        /// <summary>
        /// True if the init function has been called, false otherwise
        /// </summary>
        protected bool CalledInit => _initCalled.IsSet;
        /// <summary>
        /// Amount of time to obtain lock during disposal.  Should be significantly longer than
        /// </summary>
        public virtual TimeSpan DisposeTimeout => TimeSpan.FromSeconds(5);
        
        /// <summary>
        /// On atomic vaults -- How long we should sleep for between failed attempt to obtain a lock.
        /// You may need to fine tune this value if you use sleeping waits with atomics for your use-case
        /// and performance requirements.
        ///  
        /// In Monitor and other mutex/lock primitive type vaults (as opposed to atomic vaults),
        /// represents how long thread should block when trying to acquire mutex before checking
        /// whether a cancellation request has been propagated to the cancellation token.  In
        /// such cases, when the resource has not been acquired, no cancellation request has been
        /// propagated and there is still time remaining to acquire the lock, it will resume blocking
        /// for another period as specified herein, and the process repeats until lock is required,
        /// timeout happens or a cancellation request is propagated.  Keep this value small if 
        /// you want the cancellation requests to be prompt (at the expense of needing to wake
        /// the thread up prematurely).  If no valid cancellation token is supplied, this value is ignored
        /// and the thread will block until 
        /// </summary>
        public virtual TimeSpan SleepInterval => TimeSpan.FromMilliseconds(10);

        /// <summary>
        /// The concrete type of the vault
        /// </summary>
        [NotNull]
        protected Type ConcreteType => _concreteType.Value;
        /// <summary>
        /// The name of the concrete type of the vault
        /// </summary>
        [NotNull]
        protected string ConcreteTypeName => ConcreteType.Name;
        
        /// <summary>
        /// If you are unsure whether any thread might hold a lock when you want to dispose,
        /// you should call this method to dispose the vault rather than the normal <see cref="IDisposable.Dispose"/> method
        /// </summary>
        /// <param name="timeout">how long should we wait to get the resource back?</param>
        /// <returns>true if disposal successful, false if resource could not be obtained in limit
        /// specified by <paramref name="timeout"/></returns>
        /// <exception cref="Exception">Depending on implementation, dispose method failed for reasons other than timeout.</exception>
        public bool TryDispose(TimeSpan timeout)
        {
            timeout = timeout >= TimeSpan.Zero ? timeout : DisposeTimeout;
            try
            {
                Dispose(true, timeout);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="defaultTimeout">the default timeout</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/> was not positive.</exception>
        protected Vault(TimeSpan defaultTimeout)
        {
            _defaultTimeout = (defaultTimeout > TimeSpan.Zero)
                ? defaultTimeout
                : throw new ArgumentOutOfRangeException(nameof(defaultTimeout), defaultTimeout, @"Must be positive.");
            _concreteType = new LocklessConcreteType(this);
        }

        /// <summary>
        /// Dispose the vault, preventing further use
        /// </summary>
        /// <remarks>If you are unsure whether it is possible that any other thread holds the lock
        /// (or will hold the lock at any time during this call), you should use <seealso cref="TryDispose"/> instead.
        /// An exception will be thrown with this method if the resource cannot be obtained, but unpredicatable results
        /// may ensue as a consequence.  You should consider the program to be in a corrupted state if this throws.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Finalizer in case derived uses unmanaged resources
        /// </summary>
        ~Vault() => Dispose(false);

        /// <summary>
        /// The dispose method
        /// </summary>
        /// <param name="disposing">true if called by program, false if called by garbage collector
        /// during finalization.</param>
        /// <param name="timeout">how long should we wait?  <seealso cref="IDisposable.Dispose"/> passes null and therefore
        /// uses the <see cref="DisposeTimeout"/> value for how long to wait.  The <see cref="TryDispose"/> method passes
        /// the time supplied to it hereto.</param>
        protected abstract void Dispose(bool disposing, TimeSpan? timeout = null);
  

        /// <summary>
        /// Throws if NOT disposing
        /// </summary>
        /// <param name="caller">calling member name</param>
        protected void ThrowIfNotDisposing([CallerMemberName] string caller = "")
        {
            if (!_disposeFlag.IsDisposing)
            {
                throw new InvalidOperationException($"Illegal call to {ConcreteTypeName} object's {caller} member: " +
                                                    $"this call is only valid when the vault is in a disposing state.");
            }
        }

        /// <summary>
        /// Throw if currently disposing or already disposed
        /// </summary>
        /// <param name="caller">caller name</param>
        protected void ThrowIfDisposingOrDisposed([CallerMemberName] string caller = "")
        {
            if (!_disposeFlag.IsClear)
            {
                throw new ObjectDisposedException(
                    $"Illegal call to {ConcreteTypeName} object's {caller ?? "NULL"} member: the object is disposed or being disposed.");
            }
        }

        /// <summary>
        /// used during initialization to create box and store resource therein
        /// </summary>
        /// <param name="value">initial value to store in the resource</param>
        /// <exception cref="InvalidOperationException">Init was already called (may
        /// only call once).</exception>
        protected void Init(T value)
        {
            _initCalled.SetOrThrow();
            _lockedResource = Box.CreateBox();
            ref T temp = ref _lockedResource.Value;
            temp = value;
            _resourcePtr = _lockedResource;
        }


      
        /// <summary>
        /// A box.  Serves as a strongly-typed pointer to the locked resource
        /// </summary>
        public sealed class Box : IBox<T>
        {
            [NotNull]
            internal static Box CreateBox() => new Box();
            /// <summary>
            /// Was the box disposed
            /// </summary>
            public bool IsDisposed => _flag.IsDisposed;
            /// <summary>
            /// Get a reference to the value stored herein
            /// </summary>
            public ref T Value => ref _value;

            /// <summary>
            /// Get a readonly reference to the value stored herein
            /// </summary>
            public ref readonly T RoValue => ref _value;

            /// <summary>
            /// return the resource to the vault
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
            }

            private Box() { }
            
            private void Dispose(bool disposing)
            {
                if (disposing && _flag.SignalDisposed())
                {
                    IDisposable disposable = _value as IDisposable;
                    try
                    {
                        disposable?.Dispose();
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e);
                    }
                    _value = default;
                }
            }

            private readonly TSimpleDisposeFlag _flag = new TSimpleDisposeFlag();
            [CanBeNull] private T _value;
        }


        private SetOnceValFlag _initCalled = default;
        private readonly TimeSpan _defaultTimeout;
        [NotNull] private protected readonly TTwoStepDisposeFlag _disposeFlag = new TTwoStepDisposeFlag();
        [CanBeNull] private protected volatile Box _resourcePtr;
        [CanBeNull] private protected Box _lockedResource;
        [NotNull] private readonly LocklessConcreteType _concreteType;

    }
}