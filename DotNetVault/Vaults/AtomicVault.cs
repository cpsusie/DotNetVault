using System;
using System.Diagnostics;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.ToggleFlags;
using JetBrains.Annotations;
using TTwoStepDisposeFlag = DotNetVault.DisposeFlag.TwoStepDisposeFlag;
using TToggleFlag = DotNetVault.ToggleFlags.ToggleFlag;
using TSimpleDisposeFlag = DotNetVault.DisposeFlag.DisposeFlag;
namespace DotNetVault.Vaults
{
    /// <summary>
    /// A base class for vaults that use atomics (i.e. Interlocked Exchange and Compare Exchange)
    /// as their synchronization mechanism
    /// </summary>
    /// <typeparam name="T">The type of the protected resource.</typeparam>
    public abstract class AtomicVault<T> : Vault<T>
    {
        internal static TimeSpan FallbackSleepInterval => TimeSpan.FromMilliseconds(1);
        
        /// <inheritdoc />
        protected AtomicVault(TimeSpan defaultTimeout) 
            : base(defaultTimeout) { }

        /// <summary>
        /// Passed in delegate to the locked resource object returned so it knows how to
        /// return the object to the vault when it goes out of scope
        /// </summary>
        /// <param name="v">the vault to which the object should be returned</param>
        /// <param name="b">the box that should be returned</param>
        /// <returns>should return null always</returns>
        protected internal static Box ReleaseResourceMethod<TAv>([NotNull] TAv v, [NotNull] Box b) where TAv : AtomicVault<T>
        {
            Debug.Assert(b != null && v != null && v._resourcePtr == null);
            Box shouldBeNull = Interlocked.Exchange(ref v._resourcePtr, b);
            Debug.Assert(shouldBeNull == null);
            return shouldBeNull;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing, TimeSpan? timeout = null)
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
                        using (var l = GetInternalLockedResourceDuringDispose(disposeTimeout, true))
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
                        throw new TimeoutException($"Unable to obtain lock within {disposeTimeout.TotalMilliseconds:F3} milliseconds.", e);
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
        protected AtomicLockedResource GetInternalLockedResource(TimeSpan timeout) =>
            ExecuteGetInternalLockedResource(timeout, false);
        /// <summary>
        /// get the lock for the amount of time specified by <see cref="Vault{T}.DefaultTimeout"/>
        /// </summary>
        /// <param name="spin">true for spinlock (i.e. busy wait), false to yield control for at least <see cref="Vault{T}.SleepInterval"/>
        /// after failed attempts</param>
        /// <returns>the locked resource</returns>
        /// <exception cref="TimeoutException">resource not obtained within <see cref="Vault{T}.DefaultTimeout"/> time period</exception>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected AtomicLockedResource GetInternalLockedResource(bool spin) =>
            ExecuteGetInternalLockedResource(DefaultTimeout, spin);

        /// <summary>
        /// get the lock for the amount of time specified by <see cref="Vault{T}.DefaultTimeout"/>
        /// Yields control for <see cref="Vault{T}.SleepInterval"/> on failure so not a busy wait
        /// </summary>
        /// <returns>the locked resource</returns>
        /// <exception cref="ObjectDisposedException">the object was disposed</exception>
        /// <remarks>After method returns value, you are responsible for disposal until passing to ultimate user behind a method whose return
        /// value is annotated by the <see cref="UsingMandatoryAttribute"/>.  This means you must dispose of it yourself in all failure/exceptional
        /// cases after this method returns a value but before ultimately passed to user.</remarks>
        protected AtomicLockedResource GetInternalLockedResource() =>
            ExecuteGetInternalLockedResource(DefaultTimeout, false);

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
        protected AtomicLockedResource GetInternalLockedResource(TimeSpan timeOut, bool spin) =>
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
        protected AtomicLockedResource GetInternalLockedResourceDuringDispose(TimeSpan timeOut, bool spin) =>
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
        protected AtomicLockedResource GetInternalLockedResource(TimeSpan timeout, CancellationToken token,
            bool spin) => ExecuteGetInternalLockedResource(timeout, spin, token);

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
        protected AtomicLockedResource GetInternalLockedResource(CancellationToken token,
            bool spin) => ExecuteGetInternalLockedResource(null, spin, token);

        private AtomicLockedResource ExecuteGetInternalLockedResource(TimeSpan timeOut, bool spin)
        {
            ThrowIfDisposingOrDisposed();
            if (timeOut <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeOut), timeOut, @"Must be positive.");
            return AtomicLockedResource.CreateInternalLockedResource(this, timeOut, spin);
        }

        private AtomicLockedResource ExecuteGetInternalLockedResource(TimeSpan? timeout, bool spin,
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
                ? AtomicLockedResource.CreateInternalLockedResource(this, token, spin)
                : AtomicLockedResource.CreateInternalLockedResource(this, timeout.Value, token, spin);
        }

        private AtomicLockedResource ExecuteGetInternalLockedResourceDuringDispose(TimeSpan timeOut, bool spin)
        {
            ThrowIfNotDisposing();
            if (timeOut <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeOut), timeOut, @"Must be positive.");
            return AtomicLockedResource.CreateInternalLockedResource(this, timeOut, spin, true);
        }
        /// <summary>
        /// intermediate locked resource object storing protected resource after extracted from vault but
        /// before final delivery to user behind a method with the <see cref="UsingMandatoryAttribute"/>
        /// </summary>
        protected internal ref struct AtomicLockedResource 
        {
            internal static bool CreateInternalLockedResourceNowOrGiveUp([NotNull] AtomicVault<T> owner, out AtomicLockedResource res)
            {
                bool ret;
                res = default;
                var boxRes = AcquireBoxPointer(owner, null, true, true, CancellationToken.None);
                ret = boxRes.acquiredBox != null;
                return ret;
            }

            internal static AtomicLockedResource CreateInternalLockedResource([NotNull] AtomicVault<T> owner,
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
                    return new AtomicLockedResource(owner, boxRes.acquiredBox);
                }

                throw new TimeoutException(
                    $"Unable to obtain the lock in {timeout.TotalMilliseconds:F3} milliseconds.");
            }

            internal static AtomicLockedResource CreateInternalLockedResource([NotNull] AtomicVault<T> owner,
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
                    return new AtomicLockedResource(owner, boxRes.acquiredBox);
                }
                if (boxRes.cancelled)
                {
                    throw new OperationCanceledException(token);
                }
                throw new TimeoutException(
                    $"Unable to obtain the lock in {timeout.TotalMilliseconds:F3} milliseconds.");
            }

            internal static AtomicLockedResource CreateInternalLockedResource([NotNull] AtomicVault<T> owner,
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
                    return new AtomicLockedResource(owner, boxRes.acquiredBox);
                }
                throw new OperationCanceledException(token);

            }

            internal static AtomicLockedResource TryCreateInternalLockedResource([NotNull] AtomicVault<T> owner,
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
                return boxRes.acquiredBox != null
                    ? new AtomicLockedResource(owner, boxRes.acquiredBox)
                    : new AtomicLockedResource(owner);
            }

            internal static AtomicLockedResource TryCreateInternalLockedResource([NotNull] AtomicVault<T> owner,
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
                return boxRes.acquiredBox != null
                    ? new AtomicLockedResource(owner, boxRes.acquiredBox)
                    : new AtomicLockedResource(owner);
            }

            internal static AtomicLockedResource TryCreateInternalLockedResource([NotNull] AtomicVault<T> owner,
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
                return boxRes.acquiredBox != null
                    ? new AtomicLockedResource(owner, boxRes.acquiredBox)
                    : new AtomicLockedResource(owner);
            }

            /// <summary>
            /// This holds a valid resource
            /// </summary>
            public bool IsGood => _isGood;

            /// <summary>
            /// a reference to the protected resource
            /// </summary>
            public ref T Value => ref _b.Value;

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
            ///  return it to the vault
            /// </summary>
            public void Dispose() => Dispose(null);

            /// <summary>
            /// return the resource to the vault
            /// </summary>
            internal void Dispose(object[] extraParams)
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


            
            private AtomicLockedResource([NotNull] AtomicVault<T> owner, [NotNull] Box b)
            {
                _b = b ?? throw new ArgumentNullException(nameof(b));
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _releaseFlag = new ToggleFlag(false);
                _isGood = true;
            }

            //Make a bad one
            private AtomicLockedResource([NotNull] AtomicVault<T> owner)
            {
                _releaseFlag = new ToggleFlag(false);
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _b = null;
                _isGood = false;
            }



            private static (Box acquiredBox, bool cancelled, bool timedOut) AcquireBoxPointer(AtomicVault<T> owner,
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

                static bool DoPostConditionCheck(Box b, bool c, bool to)
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
            [NotNull] private readonly AtomicVault<T> _owner;
        }

    }
}