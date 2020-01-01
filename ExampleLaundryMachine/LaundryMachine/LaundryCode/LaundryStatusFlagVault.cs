using System;
using System.Threading;
using DotNetVault.Attributes;
using DotNetVault.CustomVaultExamples.CustomVaults;
using DotNetVault.Interfaces;
using DotNetVault.LockedResources;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public interface IVault<T> : IVault
    {
        
    }


    public sealed class LaundryStatusFlagVault : IVault<LaundryStatusFlags>
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

        #region CTOR
        public LaundryStatusFlagVault(TimeSpan defaultTimeout, [NotNull] Func<LaundryStatusFlags> stringBuilderGen)
           =>
               _impl = LsfvImpl.CreateLsvImpl(defaultTimeout,
                   stringBuilderGen ?? throw new ArgumentNullException(nameof(stringBuilderGen)));
        public LaundryStatusFlagVault(TimeSpan defaultTimeout) : this(defaultTimeout,
            () => new LaundryStatusFlags()) { } 
        #endregion

        #region Public Resource Accessor Methods
        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedLsf) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedLsf.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedLsf.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedLsf"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <param name="timeout">How long you want to wait to acquire the resource before throwing a <see cref="TimeoutException"/></param>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedLsf"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        [return: UsingMandatory]
        public LockedLsf Lock(TimeSpan timeout)
        {
            LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, LaundryStatusFlags> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(timeout, CancellationToken.None, false);
                return LockedLsf.CreateLockedResource(temp);
            }
            catch (ArgumentNullException e)
            {
                temp.Dispose();
                Console.Error.WriteLineAsync(e.ToString());
                throw;
            }
            catch (ArgumentException inner)
            {
                temp.Dispose();
                Console.Error.WriteLineAsync(inner.ToString());
                throw new InvalidOperationException("The vault is disposed or currently being disposed.");
            }
            catch (Exception e)
            {
                temp.Dispose();
                Console.Error.WriteLineAsync(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedLsf) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedLsf.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedLsf.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedLsf"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedLsf"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        /// <remarks>Waits for the time specified by <see cref="Vault{T}.DefaultTimeout"/></remarks>
        /// <remarks>NOTE this method and its <see cref="Lock(System.TimeSpan)"/> overload sleep for brief periods
        /// in between attempts to obtain the lock.  If you want a busy wait, call <seealso cref="SpinLock(System.TimeSpan)"/> or <seealso cref="SpinLock()"/> </remarks>
        [return: UsingMandatory]
        public LockedLsf Lock()
        {
            LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, LaundryStatusFlags> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(DefaultTimeout, CancellationToken.None, false);
                return LockedLsf.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                temp.Dispose();
                Console.Error.WriteLineAsync(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedLsf) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedLsf.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedLsf.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedLsf"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <param name="token">a cancellation token whereby the attempt to obtain the resource may be cancelled.</param>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedLsf"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        /// <remarks>Busy-waits until the resource is obtained or the <paramref name="token"/>'s <see cref="CancellationTokenSource"/>
        /// requests the operation be cancelled.</remarks>
        /// <remarks>NOTE this method and its <see cref="SpinLock()"/> overload busy-wait, keeping the thread active throughout the wait.
        /// Although it MAY result in quicker performance, it will be at the cost of CPU time and power expenditure -- also, if this thread stays active too long in
        /// a busy-wait, the operating System may pre-empt the thread, causing a longer wait than if you had yielded control to the OS periodically on failure.
        /// If you want to periodically sleep, call <seealso cref="Lock(System.TimeSpan)"/> or <seealso cref="Lock()"/> instead</remarks>
        [return: UsingMandatory]
        public LockedLsf Lock(CancellationToken token)
        {
            LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, LaundryStatusFlags> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(null, token, false);
                return LockedLsf.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                temp.Dispose();
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedLsf) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedLsf.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedLsf.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedLsf"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <param name="token">a cancellation token whereby cancellation of the attempt to obtain the lock can be cancelled.</param>
        /// <param name="timeout">the maximum amount of time to wait to obtain the resource before throwing <see cref="TimeoutException"/></param>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedLsf"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        /// <remarks>Waits until the resource is obtained, the <paramref name="token"/> <see cref="CancellationTokenSource"/>
        /// requests the operation be cancelled or the time specified by <paramref name="timeout"/> parameter is exceeded.</remarks>
        /// <remarks>NOTE this method and its <see cref="SpinLock()"/> overload busy-wait, keeping the thread active throughout the wait.
        /// Although it MAY result in quicker performance, it will be at the cost of CPU time and power expenditure -- also, if this thread stays active too long in
        /// a busy-wait, the operating System may pre-empt the thread, causing a longer wait than if you had yielded control to the OS periodically on failure.
        /// If you want to periodically sleep, call <seealso cref="Lock(System.TimeSpan)"/> or <seealso cref="Lock()"/> instead</remarks>
        [return: UsingMandatory]
        public LockedLsf Lock(CancellationToken token, TimeSpan timeout)
        {
            LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, LaundryStatusFlags> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(timeout, token, false);
                return LockedLsf.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                temp.Dispose();
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedLsf) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedLsf.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedLsf.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedLsf"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <param name="timeout">How long you wish to wait to obtain the lock before throwing an <see cref="TimeoutException"/>.</param>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedLsf"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        /// <remarks>Waits for the time specified by <see cref="Vault{T}.DefaultTimeout"/></remarks>
        /// <remarks>NOTE this method and its <see cref="SpinLock()"/> overload busy-wait, keeping the thread active throughout the wait.
        /// Although it MAY result in quicker performance, it will be at the cost of CPU time and power expenditure -- also, if this thread stays active too long in
        /// a busy-wait, the operating System may pre-empt the thread, causing a longer wait than if you had yielded control to the OS periodically on failure.
        /// If you want to periodically sleep, call <seealso cref="Lock(System.TimeSpan)"/> or <seealso cref="Lock()"/> instead</remarks>
        [return: UsingMandatory]
        public LockedLsf SpinLock(TimeSpan timeout)
        {

            LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, LaundryStatusFlags> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(timeout, CancellationToken.None, true);
                return LockedLsf.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                temp.Dispose();
                Console.Error.WriteLineAsync(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedLsf) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedLsf.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedLsf.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedLsf"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedLsf"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        /// <remarks>Waits for the time specified by <see cref="Vault{T}.DefaultTimeout"/></remarks>
        /// <remarks>NOTE this method and its <see cref="SpinLock()"/> overload busy-wait, keeping the thread active throughout the wait.
        /// Although it MAY result in quicker performance, it will be at the cost of CPU time and power expenditure -- also, if this thread stays active too long in
        /// a busy-wait, the operating System may pre-empt the thread, causing a longer wait than if you had yielded control to the OS periodically on failure.
        /// If you want to periodically sleep, call <seealso cref="Lock(System.TimeSpan)"/> or <seealso cref="Lock()"/> instead</remarks>
        [return: UsingMandatory]
        public LockedLsf SpinLock()
        {
            LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, LaundryStatusFlags> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(DefaultTimeout, CancellationToken.None, true);
                return LockedLsf.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                temp.Dispose();
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedLsf) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedLsf.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedLsf.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedLsf"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedLsf"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        /// <remarks>Busy-waits until the resource is obtained or the <paramref name="token"/>'s <see cref="CancellationTokenSource"/>
        /// requests the operation be cancelled.</remarks>
        /// <remarks>NOTE this method and its <see cref="SpinLock()"/> overload busy-wait, keeping the thread active throughout the wait.
        /// Although it MAY result in quicker performance, it will be at the cost of CPU time and power expenditure -- also, if this thread stays active too long in
        /// a busy-wait, the operating System may pre-empt the thread, causing a longer wait than if you had yielded control to the OS periodically on failure.
        /// If you want to periodically sleep, call <seealso cref="Lock(System.TimeSpan)"/> or <seealso cref="Lock()"/> instead</remarks>
        [return: UsingMandatory]
        public LockedLsf SpinLock(CancellationToken token)
        {
            LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, LaundryStatusFlags> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(null, token, true);
                return LockedLsf.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                temp.Dispose();
                throw;
            }
        }

        /// <summary>
        /// All public methods that return a CustomLockedResource (here a LockedLsf) should be have their return values
        /// annotated with the <see cref="UsingMandatoryAttribute"/> attribute.  The methods should call the protected method
        /// <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/>.  This method is protected (not for public consumption
        /// except as herein described) because it does not require the <see cref="UsingMandatoryAttribute"/>, which requires the IMMEDIATE callee
        /// to dispose of it.  You should guard your call to <see cref="CustomizableMutableResourceVault{T}.GetLockedResourceBase"/> with a try catch
        /// and be sure to dispose it on any path that does not lead to it successfully calling <see cref="LockedLsf.CreateLockedResource"/>,
        /// passing the <see cref="LockedVaultMutableResource{TVault,TResource}"/> to that <see cref="LockedLsf.CreateLockedResource"/> method
        /// and returning the result to the user.  You should NOT dispose the <see cref="LockedVaultMutableResource{TVault,TResource}"/> in the success path.
        /// The <see cref="LockedLsf"/> object returned will dispose it when it is disposed and the <see cref="UsingMandatoryAttribute"/> guarantees that the immediate
        /// caller MUST guard it with a using statement, guaranteeing its disposal before it goes out of scope.
        /// </summary>
        /// <param name="token">a cancellation token whereby cancellation of the attempt to obtain the lock can be cancelled.</param>
        /// <param name="timeout">the maximum amount of time to wait to obtain the resource before throwing <see cref="TimeoutException"/></param>
        /// <returns>A custom LockedResource object (in this case) <see cref="LockedLsf"/> which is guarded by the <see cref="UsingMandatoryAttribute"/>, which
        /// requires the caller to guard it with a using statement on pain of compiler error./</returns>
        /// <exception cref="ArgumentOutOfRangeException">Timespan must be greater than <see cref="TimeSpan.Zero"/></exception>
        /// <exception cref="InvalidOperationException">The vault is disposed or being disposed.</exception>
        /// <exception cref="TimeoutException">The resource was not acquired within the time specified.  Is it already acquired on the same thread?
        /// If you are obtaining locks on multiple vaults, do you always do it in the same order?  Is resource contention higher than you anticipated?
        /// </exception>
        /// <exception cref="OperationCanceledException">the operation was canceled</exception>
        /// <remarks>Busy-waits until the resource is obtained, the <paramref name="token"/>'s <see cref="CancellationTokenSource"/>
        /// requests the operation be cancelled or the time specified by <paramref name="timeout"/> parameter is exceeded.</remarks>
        /// <remarks>NOTE this method and its <see cref="SpinLock()"/> overload busy-wait, keeping the thread active throughout the wait.
        /// Although it MAY result in quicker performance, it will be at the cost of CPU time and power expenditure -- also, if this thread stays active too long in
        /// a busy-wait, the operating System may pre-empt the thread, causing a longer wait than if you had yielded control to the OS periodically on failure.
        /// If you want to periodically sleep, call <seealso cref="Lock(System.TimeSpan)"/> or <seealso cref="Lock()"/> instead</remarks>
        [return: UsingMandatory]
        public LockedLsf SpinLock(CancellationToken token, TimeSpan timeout)
        {
            LockedVaultMutableResource<MutableResourceVault<LaundryStatusFlags>, LaundryStatusFlags> temp = default;
            try
            {
                temp =
                    _impl.GetLockedResourceBase(timeout, token, true);
                return LockedLsf.CreateLockedResource(temp);
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
                temp.Dispose();
                throw;
            }
        }

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

        #endregion

        #region Nested
        private sealed class LsfvImpl : CustomizableMutableResourceVault<LaundryStatusFlags>
        {
            /// <summary>
            /// Add the following factory method to the nested class
            /// </summary>
            /// <param name="defaultTimeOut">the default time-out</param>
            /// <param name="resourceGen">a function to generate the initial value of the protected resource</param>
            /// <returns>An implementation of <see cref="CustomizableMutableResourceVault{T}"/> that your custom class
            /// (here <see cref="StringBuilderVault"/>) will delegate to.</returns>
            internal static LsfvImpl CreateLsvImpl(TimeSpan defaultTimeOut,
                [NotNull] Func<LaundryStatusFlags> resourceGen)
            {
                MutableResourceVaultFactory<LsfvImpl> factory =
                    LsvImplFactory.CreateLsfFactory();
                return factory.CreateMutableResourceVault(
                    resourceGen ?? throw new ArgumentNullException(nameof(resourceGen)), defaultTimeOut,
                    () => new LsfvImpl(defaultTimeOut));
            }

            /// <summary>
            /// Create whatever ctors necessary to call base impl class.
            /// </summary>
            /// <param name="defaultTimeout">the timeout</param>
            private LsfvImpl(TimeSpan defaultTimeout) : base(defaultTimeout)
            {
            }

            private sealed class
                LsvImplFactory : MutableResourceVaultFactory<LsfvImpl>
            {


                public static MutableResourceVaultFactory<LsfvImpl>
                    CreateLsfFactory() => new LsvImplFactory();
                private LsvImplFactory()
                {
                }
            }
        }
        #endregion

        #region Privates
        [NotNull] private readonly LsfvImpl _impl; 
        #endregion
    }
}