using System;
using System.Collections.Generic;
using System.Threading;
using HpTimeStamps;
using JetBrains.Annotations;
//SpinLock methods:
//    ATOMIC VAULTS: BUSY WAIT
//    MONITOR AND RW Vaults: SAME AS LOCK
//Lock methods:
//    ATOMIC VAULTS: Sleep briefly between failed attempts to obtain resource
//    MONITOR AND RW Vaults:
//         Uses Monitor.Enter (monitor) with a (private) sync object to synchronize 
//         Uses readwritelock slim to obtain a writeable lock
//If you want to switch between monitor and atomic vaults during performance profiling (and, generally, using atomics you envision a busy wait
//but short thread contention period, SpinLock methods should be preferred.
//
//You can switch from atomic vaults to monitor vaults by switching the alias in this and Dog.cs, you will also have to edit the factory method
//used for the monitor vault (use of CTOR is identical for basic vaults).
using BasicDogVault = DotNetVault.Vaults.BasicMonitorVault<DotNetVaultQuickStart.DogActionRecord>;
//COMMENT OUT PRIOR LINE AND UNCOMMENT SUBSEQUENT LINE HERE AND IN DOG.CS TO USE AN ATOMIC BASIC VAULT (also in DOG.cs)
//using BasicDogVault = DotNetVault.Vaults.BasicVault<DotNetVaultQuickStart.DogActionRecord>;
//using MutableResDogVault = DotNetVault.Vaults.MutableResourceVault<System.Collections.Generic.SortedSet<DotNetVaultQuickStart.DogActionRecord>>;
//UNCOMMENT OUT PRIOR LINE AND COMMENT SUBSEQUENT LINE TO USE ATOMIC MUTABLE RESOURCE VAULT
using MutableResDogVault = DotNetVault.Vaults.MutableResourceMonitorVault<System.Collections.Generic.SortedSet<DotNetVaultQuickStart.DogActionRecord>>;
namespace DotNetVaultQuickStart
{
    public abstract class Dog
    {
        [NotNull] public string Name => _name;

        protected Dog([NotNull] string name, int numActions)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Parameter may not be empty or whitespace.", nameof(name));
            if (numActions <= 0) throw new ArgumentOutOfRangeException(nameof(numActions), numActions, "Parameter must be positive");
            _numActions = numActions;
            _name = name;
        }

        public void DoDogActions()
        {
            Thread newThread = new Thread(AddDogActionsToVault);
            var old = Interlocked.Exchange(ref _t, newThread);
            old?.Join();
            newThread.Start();
        }

        public void Join()
        {
            var thread = Interlocked.Exchange(ref _t, null);
            thread?.Join();
        }

        protected abstract void AddDogActionsToVault();

        [NotNull] protected readonly ThreadLocal<Random> _rgen = new ThreadLocal<Random>(() => new Random());
        [CanBeNull] private volatile Thread _t;
        protected readonly int _numActions;
        [NotNull] private readonly string _name;
    }

    public sealed class BasicVaultDemoDog : Dog
    {

        public BasicVaultDemoDog([NotNull] string name, int numActions,
            [NotNull] BasicDogVault actionVault) : base(name, numActions)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Parameter may not be empty or whitespace.", nameof(name));
            if (numActions <= 0)
                throw new ArgumentOutOfRangeException(nameof(numActions), numActions, "Parameter must be positive");
            _recordVault = actionVault ?? throw new ArgumentNullException(nameof(name));
        }
        
        protected override void AddDogActionsToVault()
        {
            int numActions = _numActions;
            int spinDelay = _rgen.Value.Next(1, 2500);
            TimeStampSource.Calibrate();
            Thread.SpinWait(spinDelay);
            while (numActions-- > 0)
            {
                {
                    //using the basicvault's locked resource object is easy: get or set. 
                    //we don't have to worry about copying the value out of the vault -- because it is vault-safe,
                    //the value is effectively a deep copy (and the string member is immutable and sealed).
                    //The copy cannot be used to affect the value stored in the vault after the lock is released.
                    
                    //Remember, if you are using an ATOMIC vault, SpinLock is a busy wait
                    //If you are using a BASIC vault, SpinLock is the same as Lock
                    //Regardless, will throw TimeoutException if lock not obtained within vault's default timeout.
                    //you can also use an explicit positive timespan, a cancellation token, or both via overloads.
                    using var lck = _recordVault.SpinLock();
                    lck.Value = new DogActionRecord($"Dog named {Name} performed an action.");
                }
                //give another thread a chance.
                //Thread.Yield() also works.
                //You can also not include this, but, if this is an atomic vault
                //with a busy wait, you probably want to do something to yield here 
                //or you may timeout threads competing for access on a system without a lot 
                //of available execution cores.  Unnecessary, though perhaps sometimes desirable, if
                //using read-write vault or monitor vault .... though in those cases, Thread.Yield() is 
                //probably the better option if you want to make sure give other threads get an event
                //shot of having a go at the lock.
                Thread.Sleep(TimeSpan.FromMilliseconds(1)); 
            }
        }
        [NotNull] private readonly BasicDogVault _recordVault;
    }

    public sealed class MutableResourceVaultDemoDog : Dog
    {
        public MutableResourceVaultDemoDog([NotNull] string name, int numActions,
            [NotNull] MutableResDogVault vault) : base(name, numActions) =>
            _vault = vault ?? throw new ArgumentNullException(nameof(vault));


        protected override void AddDogActionsToVault()
        {
            int numActions = _numActions;
            int spinDelay = _rgen.Value.Next(1, 2500);
            TimeStampSource.Calibrate();
            Thread.SpinWait(spinDelay); //keeps it staggered start if using atomic spin wait 
            while (numActions-- > 0)
            {
                {
                    //using the mutable resource vault is a little more tricky.  All inputs to and outputs from the non-vault-safe
                    //resource must THEMSELVES be vault-safe even though the protected resource is not.  Thus, access 
                    //to the protected mutable resource is mediated by delegates with special attributes that are meaningful
                    //to the integrated static analyzer.  Using non-vault safe parameters or capturing or referencing non-vault
                    //safe values (other than the protected resource itself) is detected by the analyzer, which will refuse 
                    //to compile the code until you use a vault-safe alternative.  If use of shared mutable state were allowed
                    //the analyzer would have no reasonable way to prevent shared mutable state from escaping the vault or 
                    //shared mutable state accessible from outside the vault from changing values inside of it.
                    
                    //The same considerations apply here in the SpinLock vs Lock overload choice as apply to the Basic (and readwrite)
                    //vaults.  See the BasicVault override of this method for comments.  The difference between Basic and Mutable resource 
                    //vault is the vault-safety of the resource they guard;  The difference between Monitor and Atomic (and readwrite) vaults
                    //lies in their underlying synchronization method
                    using var lck = _vault.SpinLock();
                    bool addedOk= lck.ExecuteMixedOperation(
                        (ref SortedSet<DogActionRecord> res, in DogActionRecord record) => res.Add(record),
                        new DogActionRecord($"Dog named {Name} performed an action."));
                    //See the Project Description Pdf for a full description of the delegates used to update protected mutable resources
                    //and for how to use extension methods and/or custom vault objects to make this less cumbersome if used very
                    //frequently
                    if (!addedOk)
                    {
                        Console.Error.WriteLineAsync(
                            $"At {TimeStampSource.Now:O}, a dog action record could not be added to the sorted set.");
                    }
                }
                //Help keep the output randomized not dominated by same dog
                //See comments above in BasicVault override regarding why we do this:
                //probably unnecessary for a monitor vault.
                Thread.Sleep(TimeSpan.FromMilliseconds(1));
            }
        }

        [NotNull] private readonly MutableResDogVault _vault;

        
    }
}