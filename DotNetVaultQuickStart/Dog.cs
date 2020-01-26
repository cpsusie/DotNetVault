using System;
using System.Collections.Generic;
using System.Threading;
using DotNetVault.Vaults;
using HpTimesStamps;
using JetBrains.Annotations;

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
        [CanBeNull] protected volatile Thread _t;
        protected readonly int _numActions;
        [NotNull] private readonly string _name;
    }

    public sealed class BasicVaultDemoDog : Dog
    {

        public BasicVaultDemoDog([NotNull] string name, int numActions,
            [NotNull] BasicVault<DogActionRecord> actionVault) : base(name, numActions)
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
                    using var lck = _recordVault.SpinLock();
                    lck.Value = new DogActionRecord($"Dog named {Name} performed an action.");
                }
                Thread.Sleep(TimeSpan.FromMilliseconds(1));
            }
        }
        [NotNull] private readonly BasicVault<DogActionRecord> _recordVault;
    }

    public sealed class MutableResourceVaultDemoDog : Dog
    {
        public MutableResourceVaultDemoDog([NotNull] string name, int numActions,
            [NotNull] MutableResourceVault<SortedSet<DogActionRecord>> vault) : base(name, numActions) =>
            _vault = vault ?? throw new ArgumentNullException(nameof(vault));


        protected override void AddDogActionsToVault()
        {
            int numActions = _numActions;
            int spinDelay = _rgen.Value.Next(1, 2500);
            TimeStampSource.Calibrate();
            Thread.SpinWait(spinDelay);
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
                Thread.SpinWait(_rgen.Value.Next(1, 2500));
                Thread.Sleep(TimeSpan.FromMilliseconds(1));
            }
        }


        [NotNull] private readonly MutableResourceVault<SortedSet<DogActionRecord>> _vault;

        
    }
}