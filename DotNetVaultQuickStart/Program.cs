using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using DotNetVault.Vaults;
using DotNetVault.Attributes;
using HpTimesStamps;
using JetBrains.Annotations;

namespace DotNetVaultQuickStart
{
    class Program
    {
        static void Main()
        {
            string tsMsg = TimeStampSource.IsHighPrecision
                ? "This demonstration has HIGH PRECISION timestamps."
                : "This demonstration has LOW PRECISION timestamps.";
            Console.WriteLine("Beginning quick start demo.  " + tsMsg);
            TimeStampSource.Calibrate();
            DateTime demoStart = TimeStampSource.Now;
            DemonstrateBasicVault();
            Console.WriteLine();
            Console.WriteLine();
            DemonstrateMutableResourceVault();
            DateTime demoEnd = TimeStampSource.Now;
            Console.WriteLine("Both demos completed ok.");
            Console.WriteLine($"Elapsed milliseconds: [{(demoEnd - demoStart).TotalMilliseconds:F3}].");

        }

        /// <summary>
        /// The basic vault is the easiest vault to use, but it is limited
        /// in that it can only store vault-safe types.  Vault-safe types do not pose
        /// the danger of leaking state outside the vault providing the possibility
        /// of inadvertent un-synchronized access.  Thus, the basic vault is straight forward.
        /// Read the Project Description for what constitutes a vault safe type.
        ///
        ///    1- Unmanaged (no reference types at any level of recursion in the type) types are automatically
        ///       deemed vault-safe: if you copy the resource out of the vault, you are working with a copy, not
        ///       the value stored in the vault
        ///     2- sealed immutable reference types annotated with the <see cref="VaultSafeAttribute"/>:
        ///         because the values of these objects never change once created, there is no chance
        ///         of any race condition.  
        ///     3- structs annotated with the vault safe attribute and that contain only other types
        ///        valid under 1, 2 or 3.        
        /// </summary>
        static void DemonstrateBasicVault()
        {

            Console.WriteLine("Starting basic vault demonstration.");

            //It isn't strictly necessary to dispose a vault, UNLESS the protected resource is IDisposable
            //in this case the vault will dispose it.
            //Regardless, if you are going to dispose a vault, make sure that all threads that might be accessing 
            //it are finished ... or use the bool TryDispose(TimeSpan) method if you can't be sure... since we are joining
            //all threads before finishing, it doesn't matter here, so we will just use using on the vault.
            using var actionVault = new BasicVault<DogActionRecord>();
            List<Dog> dogs = new List<Dog>
            {
                new BasicVaultDemoDog("Fido", 2, actionVault),
                new BasicVaultDemoDog("Muffie", 3, actionVault),
                new BasicVaultDemoDog("Rex", 4, actionVault)
            };
            foreach (Dog d in dogs)
            {
                d.DoDogActions(); //each dog spawns a thread that performs a number of doggie actions 
                                  //and stores their action in the actionVault
            }
            Thread.Sleep(TimeSpan.FromMilliseconds(250));
            foreach (Dog d in dogs)
            {
                d.DoDogActions(); //do some more doggie actions
            }
            Thread.Sleep(TimeSpan.FromMilliseconds(250));
            
            //Make sure all the doggies are done.
            dogs.Reverse();
            foreach (Dog d in dogs)
            {
                d.Join(); //Make sure all the doggies are done
            }

            //You cannot obtain access to the protected resource without obtaining a lock.
            //The static analyzer will refuse to compile the code if you do not declare the lock
            //inline and as part of a using statement -- you will not inadvertently hold a lock for 
            //any longer than its scope.

            //SpinLock is a busy wait; Lock sleeps.
            //You will not deadlock -- If you cannot obtain the resource within the specified time period,
            //an exception of type TimeoutException may be thrown.  The parameterless Lock and SpinLock 
            //methods use a default timeout period.  You may also specify a positive timespan as your own 
            //timeout period.  Alternatively, you may supply a cancellationtoken, either alone or in conjunction
            //with the timeout period.  If just the cancellation token is supplied, attempts to obtain the lock
            //will continue until it is obtained or cancellation request is propagated to token.  If token and timespan
            //are supplied attempts will continue until the earlier of: 
            // 1- resource is obtained
            // 2- cancel request propagated to token
            // 3- timeout period expires
            using var lck = actionVault.SpinLock();
            Console.WriteLine($"Final dog action was: [{lck.Value.ToString()}].");
            Console.WriteLine("Ending basic vault demonstration.");
        } //The final lock is released here -- at the end of its scope.

        /// <summary>
        /// The mutable resource vault allows protection of non-vault-safe resources.  
        /// </summary>
        static void DemonstrateMutableResourceVault()
        {
            Console.WriteLine("Starting MutableResourceVault Demo");

            //You supply the ctor for the protected resource to the mutable resource vault factory,
            //that way the resource vault will create it and there is no way to get to it outside the vault
            //do not use a lambda that simply provides access to existing object: there is no reasonable
            //way for the analyzer to protect you.  Pass the ctor in as a lambda, the vault will create 
            //and guard the resource.  The provision of the timespan argument is optional.
            //If you do not provide one, a default value will be used.
            string finalResults;
            //See comments in the BasicVault demonstration regarding disposing VAULTS
            using (var mutableResourceVault =
                MutableResourceVault<SortedSet<DogActionRecord>>.CreateMutableResourceVault(
                    () => new SortedSet<DogActionRecord>(), TimeSpan.FromSeconds(1)))
            {
                List<Dog> dogs = new List<Dog>
                {
                    new MutableResourceVaultDemoDog("Fido", 2, mutableResourceVault),
                    new MutableResourceVaultDemoDog("Muffie", 3, mutableResourceVault),
                    new MutableResourceVaultDemoDog("Rex", 4, mutableResourceVault)
                };
                foreach (Dog d in dogs)
                {
                    d.DoDogActions(); //each dog spawns a thread that performs a number of doggie actions 
                    //and stores their action in the actionVault
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(250));
                foreach (Dog d in dogs)
                {
                    d.DoDogActions(); //do some more doggie actions 
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(250));

                //Make sure all the doggies are done.
                dogs.Reverse();
                foreach (Dog d in dogs)
                {
                    d.Join(); //Make sure all the doggies are done
                }

                //The resource itself is mutable -- a SortedSet, though the items it contains are VaultSafe.
                //It is not recommended to store COLLECTIONS of items that are not vault safe because there is almost
                //no way to ensure their isolation (and thus freedom from race conditions).  If the resource itself, however,
                //such a string, a DateTime, a DogActionRecord, a long, an enum, etc is VaultSafe itself, it is easy to store
                //them in a mutable collection protected by the a MutableResourceVault.  
                ImmutableSortedSet<DogActionRecord> results;
                {
                    using var lck = mutableResourceVault.SpinLock();
                    results = lck.ExecuteQuery((in SortedSet<DogActionRecord> res) => res.ToImmutableSortedSet());
                } //lock is released here
                finalResults = ProcessResults(results);
            }

            Console.WriteLine("Will print results from MutableResourceVault Demo.");
            Console.WriteLine(Environment.NewLine + finalResults + Environment.NewLine);
            Console.WriteLine("FINISHED MutableResourceVault Demo");
        }

        private static string ProcessResults([NotNull] ImmutableSortedSet<DogActionRecord> results)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Printing {results.Count} dog action results:");
            int count = 0;
            foreach (DogActionRecord dar in results)
            {
                sb.AppendLine($"\tDAR#\t{(++count).ToString()}:\t[{dar.ToString()}]");
            }
            sb.AppendLine("END dog action result printout.");
            return sb.ToString();
        }
    }
}
