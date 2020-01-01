using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.Interfaces;
using JetBrains.Annotations;
using TTwoStepDisposeFlag = DotNetVault.DisposeFlag.TwoStepDisposeFlag;
using TToggleFlag = DotNetVault.ToggleFlags.ToggleFlag;
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
        /// Passed in delegate to the locked resource object returned so it knows how to
        /// return the object to the vault when it goes out of scope
        /// </summary>
        /// <param name="v">the vault to which the object should be returned</param>
        /// <param name="b">the box that should be returned</param>
        /// <returns>should return null always</returns>
        protected internal static Box ReleaseResourceMethod([NotNull] Vault<T> v, [NotNull] Box b)
        {
            Debug.Assert(b != null && v != null && v._resourcePtr == null);
            Box shouldBeNull = Interlocked.Exchange(ref v._resourcePtr, b);
            Debug.Assert(shouldBeNull == null);
            return shouldBeNull;
        }
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
        /// Amount of time to obtain lock during disposal.  Should be significantly longer than
        /// </summary>
        public virtual TimeSpan DisposeTimeout => TimeSpan.FromSeconds(5);
        /// <summary>
        /// How long should we sleep for between failed attempt to obtain a lock.
        /// </summary>
        public virtual TimeSpan SleepInterval => TimeSpan.FromMilliseconds(10);
        /// <summary>
        /// The concrete type of the vault
        /// </summary>
        protected Type ConcreteType => _concreteType ??= GetType();
        /// <summary>
        /// The name of the concrete type of the vault
        /// </summary>
        protected string ConcreteTypeName => ConcreteType.Name;

        /// <summary>
        /// If you are unsure whether any thread might hold a lock when you want to dispose,
        /// you should call this method to dispose the vault rather than the normal <see cref="IDisposable.Dispose"/> method
        /// </summary>
        /// <param name="timeout">how long should we wait to get the resource back?</param>
        /// <returns>true if disposal successful, false if resource could not be obtained in limit
        /// specified by <paramref name="timeout"/></returns>
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
        protected Vault(TimeSpan defaultTimeout) =>
            _defaultTimeout = (defaultTimeout > TimeSpan.Zero)
                ? defaultTimeout
                : throw new ArgumentOutOfRangeException(nameof(defaultTimeout), defaultTimeout, @"Must be positive.");

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
        protected virtual void Dispose(bool disposing, TimeSpan? timeout = null)
        {
            if (disposing)
            {
                TimeSpan disposeTimeout = timeout ?? DisposeTimeout;
                if (disposeTimeout <= TimeSpan.Zero)
                {
                    disposeTimeout = DisposeTimeout;
                }
                Debug.Assert(disposeTimeout > TimeSpan.Zero);
                if (_disposeFlag.SignalDisposeBegin())
                {
                    try
                    {
                        using (InternalLockedResource l = GetInternalLockedResourceDuringDispose(disposeTimeout, true))
                        {
                            l.Destroy();
                            _lockedResource = null;
                            // ReSharper disable once RedundantAssignment
                            bool finishedDispose = _disposeFlag.SignalDisposed();
                            Debug.Assert(finishedDispose);
                        }
                    }
                    catch (TimeoutException)
                    {
                        _disposeFlag.SignalDisposeCancelled();
                        throw;
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e);
                        _disposeFlag.SignalDisposeCancelled();
                        throw new TimeoutException($"Unable to obtain lock within {DisposeTimeout.TotalMilliseconds:F3} milliseconds.", e);
                    }
                }
            }
        }

        /// <summary>
        /// Get the internal locked resource
        /// </summary>
        /// <param name="timeout">how long should we wait for</param>
        /// <returns>an internal locked resource.  Before you release the resource to the ultimate caller, you are responsible
        /// for proper disposal in exceptional/failure cases where needed.</returns>
        /// <exception cref="TimeoutException">could not obtain the resource in time</exception>
        /// <exception cref="ArgumentOutOfRangeException"> non-positive <paramref name="timeout"/> value</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected InternalLockedResource GetInternalLockedResource(TimeSpan timeout) =>
            ExecuteGetInternalLockedResource(timeout, false);
        /// <summary>
        /// get the lock for the amount of time specified by <see cref="DefaultTimeout"/>
        /// </summary>
        /// <param name="spin">true for spinlock (i.e. busy wait), false to yield control for at least <see cref="SleepInterval"/>
        /// after failed attempts</param>
        /// <returns>the locked resource</returns>
        /// <exception cref="TimeoutException">resource not obtained within <see cref="DefaultTimeout"/> time period</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected InternalLockedResource GetInternalLockedResource(bool spin) =>
            ExecuteGetInternalLockedResource(DefaultTimeout, spin);
        /// <summary>
        /// get the lock for the amount of time specified by <see cref="DefaultTimeout"/>
        /// Yields control for <see cref="SleepInterval"/> on failure so not a busy wait
        /// </summary>
        /// <returns>the locked resource</returns>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected InternalLockedResource GetInternalLockedResource() => ExecuteGetInternalLockedResource(DefaultTimeout, false);
        /// <summary>
        /// Try for up to <paramref name="timeOut"/> time to obtain the lock.  
        /// </summary>
        /// <param name="timeOut">how long to wait?</param>
        /// <param name="spin">busy wait?</param>
        /// <returns>the locked resource</returns>
        /// <exception cref="ArgumentOutOfRangeException">non-positive <paramref name="timeOut"/> argument</exception>
        /// <exception cref="TimeoutException">didn't obtain it within time specified by <paramref name="timeOut"/></exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected virtual InternalLockedResource GetInternalLockedResource(TimeSpan timeOut, bool spin) =>
            ExecuteGetInternalLockedResource(timeOut, spin);
        /// <summary>
        /// Used during disposal to get the locked resource.  DO NOT CALL UNLESS DISPOSING
        /// </summary>
        /// <param name="timeOut">the timeout</param>
        /// <param name="spin">the spin</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException">non-positive <paramref name="timeOut"/> argument</exception>
        /// <exception cref="TimeoutException">didn't obtain it within time specified by <paramref name="timeOut"/></exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected virtual InternalLockedResource GetInternalLockedResourceDuringDispose(TimeSpan timeOut, bool spin) =>
            ExecuteGetInternalLockedResourceDuringDispose(timeOut, spin);
        /// <summary>
        /// Try to get the resource until earliest of following happens
        ///     1- get it successfully,
        ///     2- cancellation requested via <paramref name="token"/>
        ///     3- time specified by <paramref name="timeout"/> exceeded
        /// </summary>
        /// <param name="timeout">how long should we wait?</param>
        /// <param name="token">token by which another thread can cancel the attempt to obtain resource</param>
        /// <param name="spin">busy wait? or yield control between failures?</param>
        /// <returns>the resource</returns>
        /// <exception cref="ArgumentOutOfRangeException">non-positive <paramref name="timeout"/> argument</exception>
        /// <exception cref="TimeoutException">didn't obtain it within time specified by <paramref name="timeout"/></exception>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected virtual InternalLockedResource GetInternalLockedResource(TimeSpan timeout, CancellationToken token,
            bool spin) =>
            ExecuteGetInternalLockedResource(timeout, spin, token);
        /// <summary>
        /// Get the locked resource.  
        /// </summary>
        /// <param name="token">a token by which the operation can be terminated prematurely</param>
        /// <param name="spin">busy wait?  or yield control (false)</param>
        /// <returns>the resource</returns>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <exception cref="OperationCanceledException">operation was cancelled</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected InternalLockedResource GetInternalLockedResource(CancellationToken token,
            bool spin) =>
            ExecuteGetInternalLockedResource(null, spin, token);

        private InternalLockedResource ExecuteGetInternalLockedResource(TimeSpan timeOut, bool spin)
        {
            ThrowIfDisposingOrDisposed();
            if (timeOut <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeOut), timeOut, @"Must be positive.");
            return InternalLockedResource.CreateInternalLockedResource(this, timeOut, spin);
        }

        private InternalLockedResource ExecuteGetInternalLockedResource(TimeSpan? timeout, bool spin,
            CancellationToken token)
        {
            ThrowIfDisposingOrDisposed();
            //todo we are currently going to allow the infinite wait if desired ... not sure if this is a good idea but for now i'll leave it.
            //if (timeout == null && token == CancellationToken.None)
            //{
            //    throw new ArgumentException("If no timeout is specified, the cancellation token must not be none.");
            //}
            if (timeout.HasValue && timeout.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"Must be positive.");
            }

            return timeout == null
                ? InternalLockedResource.CreateInternalLockedResource(this, token, spin)
                : InternalLockedResource.CreateInternalLockedResource(this, timeout.Value, token, spin);
        }

        private InternalLockedResource ExecuteGetInternalLockedResourceDuringDispose(TimeSpan timeOut, bool spin)
        {
            ThrowIfNotDisposing();
            if (timeOut <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeOut), timeOut, @"Must be positive.");
            return InternalLockedResource.CreateInternalLockedResource(this, timeOut, spin, true);
        }

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
        protected void Init(T value)
        {
            _lockedResource = Box.CreateBox();
            ref T temp = ref _lockedResource.Value;
            temp = value;
            _resourcePtr = _lockedResource;
        }

        /// <summary>
        /// intermediate locked resource object storing protected resource after extracted from vault but
        /// before final delivery to user behind a method with the <see cref="UsingMandatoryAttribute"/>
        /// </summary>
        protected ref struct InternalLockedResource
        {
            internal static bool CreateInternalLockedResourceNowOrGiveUp([NotNull] Vault<T> owner, out InternalLockedResource res)
            {
                bool ret;
                res = default;
                var boxRes = AcquireBoxPointer(owner, null, true, true, CancellationToken.None);
                ret = boxRes.acquiredBox != null;
                return ret;
            }

            internal static InternalLockedResource CreateInternalLockedResource([NotNull] Vault<T> owner,
                TimeSpan timeout, bool spin, bool vaultDisposing = false)
            {
                if (timeout <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"Must be positive.");
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                if (vaultDisposing && !owner.DisposeInProgress)
                {
                    throw new InvalidOperationException($"The {nameof(vaultDisposing)} parameter indicates this call is part of a vault disposal routine." + 
                                                        "  The vault, however, is not performing such a routine.");
                }
                if (!vaultDisposing && owner.IsDisposed)
                {
                    throw new ArgumentException(@"The vault is disposed.", nameof(owner));
                }

                var boxRes = AcquireBoxPointer(owner, timeout, spin, false, CancellationToken.None);
                Debug.Assert(!boxRes.cancelled); // we didn't pass a token, so not possible
                if (boxRes.acquiredBox != null)
                {
                    return new InternalLockedResource(owner, boxRes.acquiredBox);
                }

                throw new TimeoutException(
                    $"Unable to obtain the lock in {timeout.TotalMilliseconds:F3} milliseconds.");
            }

            internal static InternalLockedResource CreateInternalLockedResource([NotNull] Vault<T> owner,
                TimeSpan timeout, CancellationToken token, bool spin, bool vaultDisposing = false)
            {
                if (timeout <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"Must be positive.");
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                if (vaultDisposing && !owner.DisposeInProgress)
                {
                    throw new InvalidOperationException($"The {nameof(vaultDisposing)} parameter indicates this call is part of a vault disposal routine." +
                                                        "  The vault, however, is not performing such a routine.");
                }
                if (!vaultDisposing && owner.IsDisposed)
                {
                    throw new ArgumentException(@"The vault is disposed.", nameof(owner));
                }

                var boxRes = AcquireBoxPointer(owner, timeout, spin, false, token);
                if (boxRes.acquiredBox != null)
                {
                    return new InternalLockedResource(owner, boxRes.acquiredBox);
                }
                if (boxRes.cancelled)
                {
                    throw new OperationCanceledException(token);
                }
                throw new TimeoutException(
                    $"Unable to obtain the lock in {timeout.TotalMilliseconds:F3} milliseconds.");
            }

            internal static InternalLockedResource CreateInternalLockedResource([NotNull] Vault<T> owner,
                CancellationToken token, bool spin, bool vaultDisposing = false)
            {
                if (token == CancellationToken.None)
                    throw new ArgumentException("Cancellation token may not be none if no timeout is specified.");
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                if (vaultDisposing && !owner.DisposeInProgress)
                {
                    throw new InvalidOperationException($"The {nameof(vaultDisposing)} parameter indicates this call is part of a vault disposal routine." +
                                                        "  The vault, however, is not performing such a routine.");
                }
                if (!vaultDisposing && owner.IsDisposed)
                {
                    throw new ArgumentException(@"The vault is disposed.", nameof(owner));
                }

                var boxRes = AcquireBoxPointer(owner, null, spin, false, token);
                Debug.Assert(!boxRes.timedOut, "Timeout shouldn't be possible -- no timeout specified.");
                if (boxRes.acquiredBox != null)
                {
                    return new InternalLockedResource(owner, boxRes.acquiredBox);
                }
                throw new OperationCanceledException(token);
               
            }
            
            internal static InternalLockedResource TryCreateInternalLockedResource([NotNull] Vault<T> owner,
                TimeSpan timeout, bool spin, bool vaultDisposing = false)
            {
                if (timeout <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"Must be positive.");
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                if (vaultDisposing && !owner.DisposeInProgress)
                {
                    throw new InvalidOperationException($"The {nameof(vaultDisposing)} parameter indicates this call is part of a vault disposal routine." +
                                                        "  The vault, however, is not performing such a routine.");
                }
                if (!vaultDisposing && owner.IsDisposed)
                {
                    throw new ArgumentException(@"The vault is disposed.", nameof(owner));
                }

                var boxRes = AcquireBoxPointer(owner, timeout, spin, false, CancellationToken.None);
                Debug.Assert(!boxRes.cancelled); // we didn't pass a token, so not possible
                return boxRes.acquiredBox != null ? new InternalLockedResource(owner, boxRes.acquiredBox) : new InternalLockedResource(owner);
            }

            internal static InternalLockedResource TryCreateInternalLockedResource([NotNull] Vault<T> owner,
                TimeSpan timeout, CancellationToken token, bool spin, bool vaultDisposing = false)
            {
                if (timeout <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"Must be positive.");
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                if (vaultDisposing && !owner.DisposeInProgress)
                {
                    throw new InvalidOperationException($"The {nameof(vaultDisposing)} parameter indicates this call is part of a vault disposal routine." +
                                                        "  The vault, however, is not performing such a routine.");
                }
                if (!vaultDisposing && owner.IsDisposed)
                {
                    throw new ArgumentException(@"The vault is disposed.", nameof(owner));
                }

                var boxRes = AcquireBoxPointer(owner, timeout, spin, false, token);
                return boxRes.acquiredBox != null ? new InternalLockedResource(owner, boxRes.acquiredBox) : new InternalLockedResource(owner);
            }

            internal static InternalLockedResource TryCreateInternalLockedResource([NotNull] Vault<T> owner,
                CancellationToken token, bool spin, bool vaultDisposing = false)
            {
                if (token == CancellationToken.None)
                    throw new ArgumentException("Cancellation token may not be none if no timeout is specified.");
                if (owner == null) throw new ArgumentNullException(nameof(owner));
                if (vaultDisposing && !owner.DisposeInProgress)
                {
                    throw new InvalidOperationException($"The {nameof(vaultDisposing)} parameter indicates this call is part of a vault disposal routine." +
                                                        "  The vault, however, is not performing such a routine.");
                }
                if (!vaultDisposing && owner.IsDisposed)
                {
                    throw new ArgumentException(@"The vault is disposed.", nameof(owner));
                }

                var boxRes = AcquireBoxPointer(owner, null, spin, false, token);
                Debug.Assert(!boxRes.timedOut, "Timeout shouldn't be possible -- no timeout specified.");
                return boxRes.acquiredBox != null ? new InternalLockedResource(owner, boxRes.acquiredBox) : new InternalLockedResource(owner);
            }

            /// <summary>
            /// This holds a valid resource
            /// </summary>
            public bool IsGood => _isGood;

            /// <summary>
            /// a reference to the protected resource
            /// </summary>
            public ref T Value
            {
                get => ref _b.Value;
            }
            /// <summary>
            /// returns the resource called only during vault disposal
            /// </summary>
            public void Destroy()
            {
                Debug.Assert(_b != null && _owner._resourcePtr == null);
                _b.Dispose();
            }
            /// <summary>
            /// release the box to the next container on its way to the user
            /// </summary>
            /// <returns>the box</returns>
            /// <exception cref="InvalidOperationException">already released</exception>
            internal Box Release()
            {
                if (_releaseFlag.SetFlag())
                {
                    Box b = Interlocked.Exchange(ref _b, null);
                    Debug.Assert(b != null);
                    return b;
                }
                throw new InvalidOperationException("The box has already been released.");
            }
            /// <summary>
            /// return the resource to the vault
            /// </summary>
            public void Dispose()
            {
                if (_releaseFlag.SetFlag())
                {
                    if (!_b.IsDisposed && IsGood)
                    {
                        // ReSharper disable once RedundantAssignment
                        Box nullB = Interlocked.Exchange(ref _owner._resourcePtr, _b);
                        Debug.Assert(nullB == null);
                    }
                }

                _b = null;
            }

            private InternalLockedResource([NotNull] Vault<T> owner, [NotNull] Box b)
            {
                _b = b ?? throw new ArgumentNullException(nameof(b));
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _releaseFlag = new TToggleFlag(false);
                _isGood = true;
            }

            //Make a bad one
            private InternalLockedResource([NotNull] Vault<T> owner)
            {
                _releaseFlag = new TToggleFlag(false);
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _b = null;
                _isGood = false;
            }

            private static (Box acquiredBox, bool cancelled, bool timedOut) AcquireBoxPointer(Vault<T> owner,
               TimeSpan? timeout, bool spin, bool justOnce, CancellationToken token)
            {
                Box acquiredPtr;
                bool cancel = false;
                bool timedOut;
                Debug.Assert(timeout != null || token != CancellationToken.None || justOnce);

                DateTime? quitAfter = DateTime.Now + timeout;
                TimeSpan ownerSleepInterval = owner.SleepInterval;
                TimeSpan sleepFor = ownerSleepInterval > TimeSpan.Zero && ownerSleepInterval < timeout ? ownerSleepInterval : FallbackSleepInterval;
                do
                {
                    acquiredPtr = Interlocked.Exchange(ref owner._resourcePtr, null);
                    if (acquiredPtr == null)
                    {
                        cancel = token.IsCancellationRequested;
                        if (!spin && !cancel && !justOnce)
                        {
                            Thread.Sleep(sleepFor);
                        }
                        cancel = token.IsCancellationRequested;
                    }

                } while (!justOnce && !cancel && acquiredPtr == null && (quitAfter == null || DateTime.Now <= quitAfter));

                if (acquiredPtr != null)
                {
                    timedOut = false;
                }
                else
                {
                    timedOut = !cancel || justOnce;
                    cancel = !timedOut;
                }

                Debug.Assert(DoPostConditionCheck(acquiredPtr, cancel, timedOut));
                return (acquiredPtr, cancel, timedOut);

                bool DoPostConditionCheck(Box b, bool c, bool to)
                {
                    bool gotTheBox = b != null;
                    bool weCancelled = c;
                    bool weTimedOut = to;
                    return gotTheBox ?
                        !weCancelled && !weTimedOut //since we got it, we didn't cancel and there wasn't a timeout
                        : weCancelled != weTimedOut; //we didn't get it, we cancelled xor we timed out (not both)
                }
            }
            
            private readonly bool _isGood;
            private readonly TToggleFlag _releaseFlag;
            private Box _b;
            [NotNull] private readonly Vault<T> _owner;
            private static TimeSpan FallbackSleepInterval => TimeSpan.FromMilliseconds(1);
            
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

        private readonly TimeSpan _defaultTimeout;
        [NotNull] private readonly TTwoStepDisposeFlag _disposeFlag = new TTwoStepDisposeFlag();
        [CanBeNull] private volatile Box _resourcePtr;
        [CanBeNull] private Box _lockedResource;
        [CanBeNull] private Type _concreteType;
    }
}