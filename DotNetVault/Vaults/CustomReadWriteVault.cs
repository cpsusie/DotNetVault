using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using JetBrains.Annotations;
[assembly: InternalsVisibleTo("VaultUnitTests")]
namespace DotNetVault.Vaults
{
    internal abstract partial class CustomReadWriteVaultBase<TResource> : ReadWriteVault<TResource>
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="defaultTimeout"></param>
        /// <param name="func"></param>
        /// <exception cref="ArgumentNullException"></exception>
        protected CustomReadWriteVaultBase(TimeSpan defaultTimeout, [NotNull] Func<TResource> func)
            : base(defaultTimeout)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            Init(func());
        }

        private protected CustomReadWriteVaultBase(TimeSpan defaultTimeout, [NotNull] Func<TResource> resCtor, [NotNull] Func<ReaderWriterLockSlim> lockCtor) : base(defaultTimeout, lockCtor)
        {
            if (resCtor == null) throw new ArgumentNullException(nameof(resCtor));
            Init(resCtor());
        }

        

        /// <inheritdoc />
        protected sealed override void ExecuteDispose(bool disposing, TimeSpan? timeout = null)
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
                        using (var l = ExecuteGetInternalLockedResourceDuringDispose(disposeTimeout))
                        {
                            l.Destroy();
                            _lockedResource = null;
                            // ReSharper disable once RedundantAssignment
                            bool finishedDispose =
                                _disposeFlag.SignalDisposed();
                            Debug.Assert(finishedDispose);
                        }
                    }
                    catch (TimeoutException)
                    {
                        _disposeFlag.SignalDisposeCancelled();
                        throw;
                    }
                    catch (LockRecursionException e)
                    {
                        _disposeFlag.SignalDisposeCancelled();
                        Console.Error.WriteLine(e);
                        throw new RwLockAlreadyHeldThreadException(Thread.CurrentThread.ManagedThreadId, e);
                    }
                    catch (Exception e)
                    {
                        _disposeFlag.SignalDisposeCancelled();
                        Console.Error.WriteLine(e);
                        throw new TimeoutException($"Unable to obtain lock within {disposeTimeout.TotalMilliseconds:F3} milliseconds.", e);
                    }
                }
            }
        }
    }

    partial class CustomReadWriteVaultBase<TResource>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TVault"></typeparam>
        internal readonly ref struct InternalCustomRwLockedResource<TVault>
            where TVault : CustomReadWriteVaultBase<TResource>
        {

            internal static InternalCustomRwLockedResource<TVault> CreateWritableLockedResource([NotNull] Box b,
                [NotNull] TVault v)
            {
                if (b == null) throw new ArgumentNullException(nameof(b));
                if (v == null) throw new ArgumentNullException(nameof(v));
                Func<ReadWriteVault<TResource>, Box, AcquisitionMode, Box> releaseMethod =
                    ReadWriteVault<TResource>.ReleaseResourceMethod;
                return new InternalCustomRwLockedResource<TVault>(v, b, releaseMethod);

            }
            internal static InternalCustomRwLockedResource<TVault> CreateWritableLockedResource(ref RwVaultInternalLockedResource ilr,
                [NotNull] TVault v)
            {
                if (v == null) throw new ArgumentNullException(nameof(v));
                Func<ReadWriteVault<TResource>, Box, AcquisitionMode, Box> releaseMethod =
                    ReadWriteVault<TResource>.ReleaseResourceMethod;
                return new InternalCustomRwLockedResource<TVault>(v, ilr.Release(), releaseMethod);
            }

            /// <summary>
            /// Access to the protected value by reference
            /// </summary>
            [BasicVaultProtectedResource]
            public ref TResource Value => ref _box.Value;

            internal InternalCustomRwLockedResource([NotNull] TVault v, [NotNull] Box b,
                [NotNull] Func<TVault, Box, AcquisitionMode, Box> disposeMethod)
            {
                Debug.Assert(b != null && v != null && disposeMethod != null);
                _box = b;
                _disposeMethod = disposeMethod;
                _flag = new LockedResources.DisposeFlag();
                _vault = v;
            }

            /// <summary>
            /// release the lock and return the protected resource to vault for use by others
            /// </summary>
            [NoDirectInvoke]
            public void Dispose()
            {
                if (_flag?.TrySet() == true)
                {
                    Box b = _box;
                    // ReSharper disable once RedundantAssignment DEBUG vs RELEASE
                    var temp = _disposeMethod(_vault, b, Mode);
                    Debug.Assert(temp == null);
                }
            }

            private readonly LockedResources.DisposeFlag _flag;
            private readonly Func<TVault, Box,
                AcquisitionMode, Box> _disposeMethod;
            private readonly Box _box;
            private readonly TVault _vault;
            private const AcquisitionMode Mode = AcquisitionMode.ReadWrite;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TVault"></typeparam>
        internal readonly ref struct InternalCustomRoLockedResource<TVault>
            where TVault : CustomReadWriteVaultBase<TResource>
        {

            //internal static InternalCustomRoLockedResource<TVault> CreateRoLockedResource([NotNull] TVault v,
            //    [NotNull] Box b)
            //{
            //    Func<ReadWriteVault<TResource>, Box, AcquisitionMode, Box> releaseMethod =
            //        ReadWriteVault<TResource>.ReleaseResourceMethod;
            //    return new InternalCustomRoLockedResource<TVault>(v, b, releaseMethod);
            //}

            internal static InternalCustomRoLockedResource<TVault> CreateRoLockedResource(ref RwVaultInternalLockedResource internalLock, [NotNull] TVault v)
            {
                if (!internalLock.IsGood)
                {
                    throw new ArgumentException(@"Locked resource received is not good.", nameof(internalLock));
                }
                Func<ReadWriteVault<TResource>, Box, AcquisitionMode, Box> releaseMethod =
                    ReadWriteVault<TResource>.ReleaseResourceMethod;
                return new InternalCustomRoLockedResource<TVault>(v, internalLock.Release(), releaseMethod);
            }

            /// <summary>
            /// Access to the protected value by reference
            /// </summary>
            [BasicVaultProtectedResource]
            public ref TResource Value => ref _box.Value;

            private InternalCustomRoLockedResource([NotNull] TVault v, [NotNull] Box b,
                [NotNull] Func<TVault, Box, AcquisitionMode, Box> disposeMethod)
            {
                Debug.Assert(b != null && v != null && disposeMethod != null);
                _box = b;
                _disposeMethod = disposeMethod;
                _flag = new LockedResources.DisposeFlag();
                _vault = v;
            }

            /// <summary>
            /// release the lock and return the protected resource to vault for use by others
            /// </summary>
            [NoDirectInvoke]
            public readonly void Dispose()
            {
                if (_flag?.TrySet() == true)
                {
                    Box b = _box;
                    // ReSharper disable once RedundantAssignment DEBUG vs RELEASE
                    var temp = _disposeMethod(_vault, b, Mode);
                    Debug.Assert(temp == null);
                }
            }

            private readonly LockedResources.DisposeFlag _flag;
            private readonly Func<TVault, Box,
                AcquisitionMode, Box> _disposeMethod;
            private readonly Box _box;
            private readonly TVault _vault;
            private const AcquisitionMode Mode = AcquisitionMode.ReadOnly;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TVault"></typeparam>
        internal readonly ref struct InternalCustomUpgradableRoLockedResource<TVault>
            where TVault : CustomReadWriteVaultBase<TResource>
        {

            internal static InternalCustomUpgradableRoLockedResource<TVault> CreateUpgradableRoLockedResource([NotNull] TVault v,
                [NotNull] Box b, [NotNull] Action<TimeSpan?, CancellationToken> upgradeAction, [NotNull] Action upgradeForeverAction)
            {
                Func<ReadWriteVault<TResource>, Box, AcquisitionMode, Box> releaseMethod =
                    ReadWriteVault<TResource>.ReleaseResourceMethod;
                return new InternalCustomUpgradableRoLockedResource<TVault>(
                    v ?? throw new ArgumentNullException(nameof(v)), b ?? throw new ArgumentNullException(nameof(b)),
                    releaseMethod, upgradeAction ?? throw new ArgumentNullException(nameof(upgradeAction)),
                    upgradeForeverAction ?? throw new ArgumentNullException(nameof(upgradeForeverAction)));
            }

            /// <summary>
            /// Access to the protected value by reference
            /// </summary>
            [BasicVaultProtectedResource]
            public ref TResource Value => ref _box.Value;

            /// <summary>
            /// The underlying vault's default timeout
            /// </summary>
            public TimeSpan DefaultTimeout => _vault?.DefaultTimeout ?? TimeSpan.Zero;

            private InternalCustomUpgradableRoLockedResource([NotNull] TVault v, [NotNull] Box b,
                [NotNull] Func<TVault, Box, AcquisitionMode, Box> disposeMethod,
                [NotNull] Action<TimeSpan?, CancellationToken> upgradeAction, [NotNull] Action upgradeForeverAction)
            {
                Debug.Assert(b != null && v != null && disposeMethod != null &&
                             upgradeAction != null && upgradeForeverAction != null);
                _box = b;
                _disposeMethod = disposeMethod;
                _flag = new LockedResources.DisposeFlag();
                _vault = v;
                _upgradeWithWait = upgradeAction;
                _upgradeForever = upgradeForeverAction;
            }

            /// <summary>
            /// release the lock and return the protected resource to vault for use by others
            /// </summary>
            [NoDirectInvoke]
            public void Dispose()
            {
                if (_flag?.TrySet() == true)
                {
                    Box b = _box;
                    // ReSharper disable once RedundantAssignment DEBUG vs RELEASE
                    var temp = _disposeMethod(_vault, b, Mode);
                    Debug.Assert(temp == null);
                }
            }

            /// <summary>
            ///  Obtain a writable locked resource.  Keep attempting until
            ///  sooner of following occurs:
            ///     1- time period specified by <paramref name="timeout"/> expires or
            ///     2- cancellation is requested via <paramref name="token"/>'s <see cref="CancellationTokenSource"/>
            /// </summary>
            /// <param name="timeout">the max time to wait for</param>
            /// <param name="token">a cancellation token</param>
            /// <returns>the resource</returns>
            /// <exception cref="InvalidOperationException">This locked resource object has not been initialized validly</exception>
            /// <exception cref="ArgumentOutOfRangeException">Non-positive <paramref name="timeout"/></exception>
            /// <exception cref="TimeoutException">Could not obtain write lock in time specified by <paramref name="timeout"/></exception>
            /// <exception cref="OperationCanceledException">A cancellation request was propagated to the <paramref name="token"/></exception>
            /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
            [return: UsingMandatory]
            internal InternalCustomRwLockedResource<TVault> Lock(TimeSpan timeout, CancellationToken token)
            {
                if (_box == null) throw new InvalidOperationException("This object is invalid.");
                if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"");
                return UpgradeAction(timeout, token);
            }

            /// <summary>
            /// Get a writable lock.
            /// </summary>
            /// <param name="timeout">how long to keep attempting before throwing <see cref="TimeoutException"/></param>
            /// <returns>the resource</returns>
            /// <exception cref="InvalidOperationException">This locked resource object has not been initialized validly</exception>
            /// <exception cref="ArgumentOutOfRangeException">Non-positive <paramref name="timeout"/></exception>
            /// <exception cref="TimeoutException">Could not obtain write lock in time specified by <paramref name="timeout"/></exception>
            /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
            [return: UsingMandatory]
            internal InternalCustomRwLockedResource<TVault> Lock(TimeSpan timeout)
            {
                if (_box == null) throw new InvalidOperationException("This object is invalid.");
                if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), timeout, @"");
                return UpgradeAction(timeout, CancellationToken.None);
            }


            /// <summary>
            ///  Obtain a writable locked resource. 
            /// </summary>
            /// <param name="token">a token to which cancellation requests can be propagated.</param>
            /// <returns>the resource</returns>
            /// <exception cref="InvalidOperationException">This locked resource object has not been initialized validly</exception>
            /// <exception cref="OperationCanceledException">A cancellation request was propagated to the <paramref name="token"/></exception>
            /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
            [return: UsingMandatory]
            internal InternalCustomRwLockedResource<TVault> Lock(CancellationToken token)
            {
                if (_box == null) throw new InvalidOperationException("This object is invalid.");
                return UpgradeAction(null, token);
            }

            /// <summary>
            /// Obtain the writable locked resource
            /// </summary>
            /// <returns>the locked resource</returns>
            /// <exception cref="InvalidOperationException">This locked resource object has not been initialized
            /// validly</exception>
            /// <exception cref="TimeoutException">Unable to obtain locked resource in the underlying vault's
            /// <see cref="DefaultTimeout"/>.</exception>
            /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock</exception>
            [return: UsingMandatory]
            internal InternalCustomRwLockedResource<TVault> Lock()
            {
                if (_box == null) throw new InvalidOperationException("This object is invalid.");
                return UpgradeAction(_vault.DefaultTimeout, CancellationToken.None);
            }

            /// <summary>
            /// Wait to obtain the WriteLock potentially forever
            /// </summary>
            /// <returns></returns>
            /// <exception cref="InvalidOperationException">This locked resource object has not been </exception>
            /// <exception cref="RwLockAlreadyHeldThreadException">This thread already holds a write lock.</exception>
            [return: UsingMandatory]
            internal InternalCustomRwLockedResource<TVault> LockWaitForever()
            {
                if (_box == null) throw new InvalidOperationException("This object is invalid.");
                return UpgradeWaitForever();
            }

            private InternalCustomRwLockedResource<TVault> UpgradeAction(TimeSpan? ts, CancellationToken token)
            {
                if (_box == null) throw new InvalidOperationException("This object is invalid.");
                if (ts.HasValue && ts <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ts), ts, @"Not null timespan must have positive value.");
                _upgradeWithWait(ts, token);
                return InternalCustomRwLockedResource<TVault>.CreateWritableLockedResource(_box, _vault);
            }

            private InternalCustomRwLockedResource<TVault> UpgradeWaitForever()
            {
                if (_box == null) throw new InvalidOperationException("This object is invalid.");
                _upgradeForever();
                return InternalCustomRwLockedResource<TVault>.CreateWritableLockedResource(_box, _vault);
            }

            private readonly LockedResources.DisposeFlag _flag;
            private readonly Func<TVault, Box,
                AcquisitionMode, Box> _disposeMethod;
            private readonly Action<TimeSpan?, CancellationToken> _upgradeWithWait;
            private readonly Action _upgradeForever;
            private readonly Box _box;
            private readonly TVault _vault;
            private const AcquisitionMode Mode = AcquisitionMode.UpgradableReadOnly;
        }
    }

    internal sealed class StringBufferReadWriteVault : ReadWriteVault<StringBuilder>
    {
        public StringBufferReadWriteVault(TimeSpan defaultTimeout) 
            : this(defaultTimeout, () => new StringBuilder()) { }
        

        public StringBufferReadWriteVault(TimeSpan defaultTimeout, [NotNull] Func<StringBuilder> func) : base(defaultTimeout)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            Init(func());
        }

        internal StringBufferReadWriteVault(TimeSpan defaultTimeout, [NotNull] Func<StringBuilder> sbCtor, [NotNull] Func<ReaderWriterLockSlim> lockCtor) : base(defaultTimeout, lockCtor)
        {
            if (sbCtor == null) throw new ArgumentNullException(nameof(sbCtor));
            Init(sbCtor());
        }
        

        protected override void ExecuteDispose(bool disposing, TimeSpan? timeout = null)
        {
            throw new NotImplementedException();
        }
    }



    internal ref struct SubStringMatcher
    {
        public static int FindFirstIndexOfSubString([NotNull] StringBuilder str, [NotNull] string substr)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));
            if (substr == null) throw new ArgumentNullException(nameof(substr));

            if (substr.Length > str.Length) return -1;
            if (str.Length == 0 || substr.Length == 0) return -1;

            SubStringMatcher matcher = CreateMatcher(substr);
            int idx=-1;
            for (int i = 0; i < str.Length; ++i)
            {
                matcher.FeedChar(str[i]);
                if (matcher.IsMatch)
                {
                    idx = i - (matcher.Length - 1);
                    break;
                }
            }
            return idx;
        }

        public static SubStringMatcher CreateMatcher(string subStringToMatch)
        {
            var span = subStringToMatch.AsSpan();
            return new SubStringMatcher(in span);
        }

        public static SubStringMatcher CreateMatcher(in ReadOnlySpan<char> subStringToMatch)
            => new SubStringMatcher(in subStringToMatch);

        public int Length => _stringText.Length;
        public bool IsMatch => _isMatch;

        public void FeedChar(char c)
        {
            if (_isMatch)
            {
                _state = 0;
                _isMatch = false;
            }

            if (c == _stringText[_state])
            {
                ++_state;
                if (_state == _stringText.Length)
                {
                    _isMatch = true;
                }
            }
        }

        private SubStringMatcher(in ReadOnlySpan<char> span)
        {
            if (span.Length < 1)
                throw new ArgumentException(@"Empty span not allowed.", nameof(span));
            _stringText = span;
            _state = 0;
            _isMatch = false;
        }


        private bool _isMatch;
        private int _state;
        private readonly ReadOnlySpan<char> _stringText;
    }
}
