﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.CustomVaultExamples.CustomLockedResources;
using DotNetVault.Interfaces;
using DotNetVault.LockedResources;
using DotNetVault.Logging;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace DotNetVault.CustomVaultExamples.CustomVaults
{
    /// <summary>
    /// This is an example of how to make a custom vault with a custom locked resource type:
    /// the primary advantage to using this rather than a plain <see cref="MutableResourceMonitorVault{T}"/> is because
    /// you can provide an API similar to the protected resource's API and not have to always rely on sometimes awkward
    /// delegates.  Consult this and <see cref="LockedMonitorStringBuilder"/> (it's custom locked resource type) to see how this is
    /// done.  <see cref="StringBuilder"/> was chosen because it is paradigmatic of non-vault-safe types:
    /// reference types that have mutable state and has a well-known public interface which may be preferable to use
    /// of delegates and closures for query and mutation actions performed on it while protected.
    /// </summary>
    /// <remarks>This vault uses <see cref="Monitor"/> and sync object based syncro;
    /// for atomic-based syncro, <see cref="StringBuilderVault"/></remarks>
    /// <remarks> 
    /// The spin lock methods are provided in case you wish to switch back and forth between
    /// a <see cref="StringBuilderVault"/> and a <see cref="StringBuilderMonitorVault"/> and use spin-lock when
    /// configured with <see cref="StringBuilderVault"/>.  THIS VAULT DOES NOT SUPPORT SPINNING / BUSY WAITING.
    /// The SpinLock methods in THIS implementation are EXACTLY equivalent to the Lock methods.
    /// </remarks>
    public sealed class StringBuilderMonitorVault : IVault
    {
        #region Public Properties
        //always delegate these to the implementation object.  
        //if you wish to change the value of SleepInterval, DisposeTimeout, or DefaultTimeout
        //do so via an overload in the implementation class, not here.
        /// <inheritdoc />
        public TimeSpan DisposeTimeout => _impl.DisposeTimeout;
        /// <inheritdoc />
        public TimeSpan SleepInterval => _impl.SleepInterval;
        /// <inheritdoc />
        public TimeSpan DefaultTimeout => _impl.DefaultTimeout;
        /// <inheritdoc />
        public bool DisposeInProgress => _impl.DisposeInProgress;
        /// <inheritdoc />
        public bool IsDisposed => _impl.IsDisposed;
        #endregion

        #region CTORS
        /// <summary>
        /// CTOR.  Creates a vault.
        /// </summary>
        /// <param name="defaultTimeout">the default amount of time that should be waited when calling parameterless
        /// <see cref="Lock()"/> and <see cref="SpinLock()"/> methods</param>
        /// <param name="stringBuilderGen">A function that creates a <see cref="StringBuilder"/> that will be protected
        /// by the vault.  This delegate should create a NEW string builder and simply return it ... not storing
        /// any references to it anywhere, otherwise the vault cannot effectively guard it.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stringBuilderGen"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/> was not positive.</exception>
        public StringBuilderMonitorVault(TimeSpan defaultTimeout, [NotNull] Func<StringBuilder> stringBuilderGen)
            =>
                _impl = StringBuilderVaultImpl.CreateStringBuilderVaultImpl(defaultTimeout,
                    stringBuilderGen ?? throw new ArgumentNullException(nameof(stringBuilderGen)));
        /// <summary>
        /// CTOR.  Creates a vault with a string builder that contains the contents of the string specified.
        /// </summary>
        /// <param name="fromMe">starting contents of protected <see cref="StringBuilder"/></param>
        /// <param name="defaultTimeOut">the default amount of time that should be waited when calling parameterless
        /// <see cref="Lock()"/> and <see cref="SpinLock()"/> methods</param>
        /// <exception cref="ArgumentNullException"><paramref name="fromMe"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeOut"/> was not positive.</exception>
        public StringBuilderMonitorVault([NotNull] string fromMe, TimeSpan defaultTimeOut) : this(defaultTimeOut,
            FromString(fromMe))
        { }
        /// <summary>
        /// CTOR.  Creates a vault from the supplied collection of characters.
        /// </summary>
        /// <param name="fromUs">a collection of characters which should be copied into the new vault's protected resource,
        /// as its starting value</param>
        /// <param name="defaultTimeout">the default amount of time that should be waited when calling parameterless
        /// <see cref="Lock()"/> and <see cref="SpinLock()"/> methods</param>
        /// <exception cref="ArgumentNullException"><paramref name="fromUs"/> was null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="defaultTimeout"/> was null</exception>
        public StringBuilderMonitorVault([NotNull] IEnumerable<char> fromUs, TimeSpan defaultTimeout) : this(defaultTimeout,
            FromCharEnumerable(fromUs))
        { }
        /// <summary>
        /// CTOR Creates a vault.
        /// </summary>
        /// <param name="defaultTimeout">the default amount of time that should be waited when calling parameterless
        /// <see cref="Lock()"/> and <see cref="SpinLock()"/> methods</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <remarks>Protected <see cref="StringBuilder"/> will be default constructed</remarks>
        public StringBuilderMonitorVault(TimeSpan defaultTimeout)
            : this(defaultTimeout, () => new StringBuilder()) { }
        #endregion

        #region Public Resource Accessor Methods
        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedMonitorStringBuilder) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMonitorMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMonitorMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedMonitorStringBuilder.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedMonitorStringBuilder.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedMonitorStringBuilder"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <param name="timeout">How long you want to wait to acquire the resource before throwing a <see cref="TimeoutException"/></param>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedMonitorStringBuilder"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread trying to obtain the lock already holds it.</exception>
        [return: UsingMandatory]
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        public LockedMonitorStringBuilder Lock(TimeSpan timeout)
        {
            LockedMonVaultMutableResource<MutableResourceMonitorVault<StringBuilder>, StringBuilder> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(timeout, CancellationToken.None);
                return LockedMonitorStringBuilder.CreateLockedResource(temp);
            }
            catch (ArgumentNullException e)
            {
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                DebugLog.Log(e);
                throw;
            }
            catch (ArgumentException inner)
            {
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                DebugLog.Log(inner);
                throw new InvalidOperationException("The vault is disposed or currently being disposed.");
            }
            catch (Exception e)
            {
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                DebugLog.Log(e);
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedMonitorStringBuilder) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMonitorMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMonitorMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedMonitorStringBuilder.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedMonitorStringBuilder.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedMonitorStringBuilder"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedMonitorStringBuilder"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread trying to obtain the lock already holds it.</exception>
        [return: UsingMandatory]
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        public LockedMonitorStringBuilder Lock()
        {
            LockedMonVaultMutableResource<MutableResourceMonitorVault<StringBuilder>, StringBuilder> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(DefaultTimeout, CancellationToken.None);
                return LockedMonitorStringBuilder.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                DebugLog.Log(e);
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedMonitorStringBuilder) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMonitorMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMonitorMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedMonitorStringBuilder.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedMonitorStringBuilder.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedMonitorStringBuilder"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <param name="token">a cancellation token whereby the attempt to obtain the resource may be cancelled.</param>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedMonitorStringBuilder"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread trying to obtain the lock already holds it.</exception>
        [return: UsingMandatory]
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        public LockedMonitorStringBuilder Lock(CancellationToken token)
        {
            LockedMonVaultMutableResource<MutableResourceMonitorVault<StringBuilder>, StringBuilder> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(null, token);
                return LockedMonitorStringBuilder.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                DebugLog.Log(e);
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedMonitorStringBuilder) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMonitorMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMonitorMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedMonitorStringBuilder.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedMonitorStringBuilder.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedMonitorStringBuilder"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <param name="token">a cancellation token whereby cancellation of the attempt to obtain the lock can be cancelled.</param>
        /// <param name="timeout">the maximum amount of time to wait to obtain the resource before throwing <see cref="TimeoutException"/></param>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedMonitorStringBuilder"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        ///  <exception cref="LockAlreadyHeldThreadException">the thread trying to obtain the lock already holds it.</exception>
        /// <remarks>Waits until the resource is obtained, the <paramref name="token"/> <see cref="CancellationTokenSource"/>
        /// requests the operation be cancelled or the time specified by <paramref name="timeout"/> parameter is exceeded.</remarks>
        [return: UsingMandatory]
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        public LockedMonitorStringBuilder Lock(CancellationToken token, TimeSpan timeout)
        {
            LockedMonVaultMutableResource<MutableResourceMonitorVault<StringBuilder>, StringBuilder> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(timeout, token);
                return LockedMonitorStringBuilder.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                DebugLog.Log(e);
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedMonitorStringBuilder) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMonitorMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMonitorMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedMonitorStringBuilder.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedMonitorStringBuilder.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedMonitorStringBuilder"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedMonitorStringBuilder"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="LockAlreadyHeldThreadException">the thread trying to obtain the lock already holds it.</exception>
        /// <remarks>This method may potentially deadlock as it acquires the sync object without any timeout or cancellation token.</remarks>
        [return: UsingMandatory]
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        public LockedMonitorStringBuilder LockBlockUntilAcquired()
        {
            LockedMonVaultMutableResource<MutableResourceMonitorVault<StringBuilder>, StringBuilder> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBlockTilAcquiredBase();
                return LockedMonitorStringBuilder.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                DebugLog.Log(e);
                throw;
            }
        }

        #endregion

        #region Spin lock methods for maintaining compilability across underlying sync mechanism
        /// <summary>
        /// Monitor based vaults do not support spin/busy wait.  This call is exactly identical to <see cref="SpinLock(System.TimeSpan)"/>
        /// </summary>
        [return: UsingMandatory]
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        public LockedMonitorStringBuilder SpinLock(TimeSpan timeout)
        {

            LockedMonVaultMutableResource<MutableResourceMonitorVault<StringBuilder>, StringBuilder> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(timeout, CancellationToken.None);
                return LockedMonitorStringBuilder.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                DebugLog.Log(e);
                throw;
            }
        }

        /// <summary>
        /// Monitor based vaults do not support spin/busy wait.  This call is exactly identical to <see cref="Lock()"/>
        /// </summary>
        [return: UsingMandatory]
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        public LockedMonitorStringBuilder SpinLock()
        {
            LockedMonVaultMutableResource<MutableResourceMonitorVault<StringBuilder>, StringBuilder> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(DefaultTimeout, CancellationToken.None);
                return LockedMonitorStringBuilder.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                DebugLog.Log(e);
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                throw;
            }
        }

        /// <summary>
        /// Monitor based vaults do not support spin/busy wait.  This call is exactly identical to <see cref="Lock(CancellationToken)"/>
        /// </summary>
        [return: UsingMandatory]
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        public LockedMonitorStringBuilder SpinLock(CancellationToken token)
        {
            LockedMonVaultMutableResource<MutableResourceMonitorVault<StringBuilder>, StringBuilder> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(null, token);
                return LockedMonitorStringBuilder.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                DebugLog.Log(e);
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                throw;
            }
        }

        /// <summary>
        /// Monitor based vaults do not support spin/busy wait.  This call is exactly identical to <see cref="Lock(CancellationToken, TimeSpan)"/>
        /// </summary>
        [return: UsingMandatory]
        [EarlyReleaseJustification(EarlyReleaseReason.DisposingOnError)]
        public LockedMonitorStringBuilder SpinLock(CancellationToken token, TimeSpan timeout)
        {
            LockedMonVaultMutableResource<MutableResourceMonitorVault<StringBuilder>, StringBuilder> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(timeout, token);
                return LockedMonitorStringBuilder.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                DebugLog.Log(e);
                temp.ErrorCaseReleaseOrCustomWrapperDispose();
                throw;
            }
        } 
        #endregion

        #region IDisposable

        /// <inheritdoc />
        public void Dispose() => Dispose(true); //call Dispose(bool)

        /// <inheritdoc />
        public bool TryDispose(TimeSpan timeout) => _impl.TryDispose(timeout);  //delegate

        private void Dispose(bool disposing)
        {
            if (disposing) //delegate if disposing is true
            {
                _impl.Dispose();
            }
        }
        #endregion

        #region Private ancillary methods
        private static Func<StringBuilder> FromString(string s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            return () => new StringBuilder(s);
        }

        private static Func<StringBuilder> FromCharEnumerable([NotNull] IEnumerable<char> characters)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            return () =>
            {
                var ret = new StringBuilder();
                foreach (var c in characters)
                {
                    ret.Append(c);
                }
                return ret;
            };
        }
        #endregion

        #region Nested class 
        /// <summary>
        /// Make a nested class (private or protected) that inherits from the abstract class <see cref="CustomizableMonitorMutableResourceVault{T}"/>
        /// Calls in <see cref="StringBuilderMonitorVault"/> (or your custom vault) are delegated to this implementation class.
        /// </summary>
        private sealed class StringBuilderVaultImpl : CustomizableMonitorMutableResourceVault<StringBuilder>
        {
            /// <summary>
            /// Add the following factory method to the nested class
            /// </summary>
            /// <param name="defaultTimeOut">the default time-out</param>
            /// <param name="resourceGen">a function to generate the initial value of the protected resource</param>
            /// <returns>An implementation of <see cref="CustomizableMonitorMutableResourceVault{T}"/> that your custom class
            /// (here <see cref="StringBuilderVault"/>) will delegate to.</returns>
            internal static StringBuilderVaultImpl CreateStringBuilderVaultImpl(TimeSpan defaultTimeOut,
                Func<StringBuilder> resourceGen)
            {
                IMutableResourceVaultFactory<StringBuilderVaultImpl, StringBuilder> factory =
                    StringBuilderVaultImplFactory.CreateSbImplFactory();
                return factory.CreateMutableResourceVault(
                    resourceGen ?? throw new ArgumentNullException(nameof(resourceGen)), defaultTimeOut,
                    () => new StringBuilderVaultImpl(defaultTimeOut));
            }

            /// <summary>
            /// Create whatever ctors necessary to call base impl class.
            /// </summary>
            /// <param name="defaultTimeout">the timeout</param>
            private StringBuilderVaultImpl(TimeSpan defaultTimeout) : base(defaultTimeout)
            {
            }

            /// <summary>
            /// Derive from the factory if needed to be used in static factory method shown <see cref="CreateStringBuilderVaultImpl"/>
            /// </summary>
            private sealed class
                StringBuilderVaultImplFactory : MutableResourceVaultFactory<StringBuilderVaultImpl>
            {

                /// <summary>
                /// Creates factory instance
                /// </summary>
                /// <returns>a factory object to be used in the static factory method <see cref="StringBuilderVaultImpl.CreateStringBuilderVaultImpl"/> above.
                /// </returns>
                public static IMutableResourceVaultFactory<StringBuilderVaultImpl, StringBuilder>
                    CreateSbImplFactory() => new StringBuilderVaultImplFactory();
                private StringBuilderVaultImplFactory()
                {
                }
            }


        }
        #endregion

        #region Privates
        [NotNull] private readonly StringBuilderVaultImpl _impl;
        #endregion
    }
}