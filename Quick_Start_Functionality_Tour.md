# DotNetVault Quick Start Functionality Tour – JetBrains Rider 2019.3.1+ (Amazon Linux)  
  
1.  This tutorial gives a brief tour of DotNetVault's functionality using JetBrains Rider on Amazon Linux.  If you have not installed DotNetVault yet on Rider, see the [Installation Quick Start guide](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Installation%20Guide%20%E2%80%93%20JetBrains%20Rider%20(Tested%20on%20Amazon%20Linux).md#dotnetvault-quick-start-installation-guide--jetbrains-rider-201931-tested-on-amazon-linux) first. After you have completed the installation process and ensured that Roslyn analyzers are enabled and working in Rider, return here. If you are using Visual Studio, use the [Windows Installation Quick Start Guide here](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Installation%20Guide%20Visual%20Studio%202019%20(Windows%2010).md#dotnetvault-quick-start-installation-guide-visual-studio-2019-windows-10), but simply open the solution as shown below in Visual Studio rather than Rider. 
  
2. Go to this projects source repository on GitHub and download the source code for DotNetVault as shown (you will not need the source code to use DotNetVault, but it contains many useful test and demonstration projects and will be needed for this tour):  
  
    ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_1.png?raw=true)  
      
    * Extract the contents of the zip file to a convenient location  
    
        ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_2.png?raw=true)    
          
    * Go into the folder structure and open the file “DotNetVaultQuickStart.sln” using Rider 2019.3.1+:  
    
        ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_3.png?raw=true)    
          
    * Build the solution:  
    
      ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_4.png?raw=true)    
        
    * Run the solution:  
      
      ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_5.png?raw=true)  
      
3.  This quick start projects demonstrates the use of Vaults and LockedResources.  A vault is an object that guards an object for thread synchronization purposes.  It prevents you from accessing the guarded object except for brief periods where you "obtain a lock" on the object.  You "obtain a lock" by calling one of the Lock, Spinlock and specialized locking method overloads.  If you successfully obtain the lock, you are presented with a **Locked Resource** which allows you to temporarily access the guarded object.  This **locked resource** object is a ref struct that can only be present in a single stack frame (cannot be copied to the heap (via boxing or otherwise) or to static  memory. Static analyzer rules are activated to prevent you from using the **locked resource** in a way that would allow the lock to remain open for longer than its scope or to copy its mutable contents (if any) to an unprotected context:  
  
    *  You must guard the **LockedResource** with a using statement or declaration
    *  You must declare the **locked resource** object and assign to it inline to prevent
    retained access beyond its scope
    *  You cannot dispose it early (to prevent use after dispose)
    *  You cannot copy it by value or by non-readonly reference
    * You may not by-ref-alias the object it grants access to (to prevent access after the end of the locked resource objects scope)  
    * Other rules as set forth in the ProjectDescription.pdf
    
4. All the above rules are enforced by the emission of compilation errors by the static analyzer.  The goal is to make it as close to impossible as possible to gain unsynchronized access to the guarded object.

5.  There are several general types of vaults provided by this project and a few specialized vaults.  The general vaults can be categorized on two axes:  
    
    * by the underlying synchronization mechanism they use and  
    * by the characteristics of the object they protect.

6. The underlying synchronization mechanisms available include Monitor.Enter and its overloads (like using lock with a synchronization object), lock free atomic operations, and ReaderWriterLockSlim.  The other axis is separated into vaults that guard **vault-safe** objects and those that guard objects that contain **reference** types with **mutable state**. 

7. Among vaults that guard the same type of object, it is easy to switch between underlying synchronization methods at compile time because their Lock method overloads have the compatible signatures.  This (along with compiler-enforced limitation of access to the **locked resource**) provides a significant advantage over using the underlying mechanisms directly: it requires minimal refactoring to switch between lock free atomic synchronization and mutex-based synchronization.  You may even switch from monitor/atomic to read-write lock, but, if you use readonly locks, it will of necessity prove difficult to switch back to atomic or lock + synchronization object based synchronization.  

8.  BasicVaults protected only **vault-safe** resources and may be monitor-lock based or based on lock-free atomics.  The basic vault shown in this demonstration protects the **vault-safe** resource DogActionRecord and is configured, by default to be monitor-lock-based.  As shown, it is easy to re-configure it to be based on lock-free atomics by editing the type aliases defined at the top of Program.cs and Dog.cs:

    ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_6.png?raw=true)  
    
9.  DogActionRecord is an example of a **vault-safe** type.  These types can be stored in BasicVaults and ReadWriteVaults and are easier to work with than **locked resources** that protect reference types with mutable state.  A type is vault-safe if:  
  
    * It is an unmanaged type (i.e. a value type that does not contain any references type anywhere in its object graph at any level of nesting)
    * A sealed immutable reference type (no state change possible) that is annotated with the VaultSafe attribute
    * A value type that is annotated with the VaultSafe attribute and contains only types that are also vault-safe
    
10.  DogActionRecord is shown below with commentary embedded in the image:  
    
        ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_7.png?raw=true)    
          
11. Although DogActionRecord is a value type, it is not an unmanaged value type.  For it to be VaultSafe, it must not contain any reference types with mutable state and it must be annotated with the VaultSafe attribute.  Since it is annotated with VaultSafe attribute and since its only unmanaged field is a string, which is a sealed class with no mutable state, it is VaultSafe.  When you apply the VaultSafe attribute to a type, the static analyzer verifies **vault-safety** and refuses to compile if it cannot prove at compile time that the field is VaultSafe.  For example, if you were to change the type of the field "_action"
to a StringBuilder, it would cause compilation errors:

      ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_8.png?raw=true)  
      
12.  As a **vault-safe** type, DogActionRecord can be stored in a BasicVault.  The call to DemonstrateBasicVault() in Program.cs's Main method shows how this works.    
  
     ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_9.png?raw=true)  
       
13. This demonstration is rather straight forward.  Dogs are created which obtain a name, a number of actions to perform when called upon and a reference to the vault guarding a dog action record.  When they are told to perform their actions, they spawn a thread and for each of their number of actions to perform, obtain a lock and update the guarded resource to contain a record of their action.  They then release the lock, wait a bit, and repeat for any remaining actions depending on how many they are configured to perform.  In this demo, the dogs are called upon to do their actions twice, causing them to contend for access multiple times to the vault.  The main thread then waits for them all to get done then itself obtains the lock and prints out the dog action record of the last dog to obtain a lock:  

     ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_10.png?raw=true)      
       
14. The second demonstration is similar to the first one except the protected resource is a SortedSet of DogActionRecords rather than a singular DogActionRecord.  Since a SortedSet is a mutable reference type, a mutable resource vault, rather than a Basic Vault needs to be used.  As shown below, in this demonstration, the Dogs add their actions to the SortedSet rather than overwriting a single DogActionRecord:  
  
    ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_11.png?raw=true)  
      
15. The output of both demonstrations is found below:  
> Beginning quick start demo.  This demonstration has HIGH PRECISION timestamps.  
> Starting basic vault demonstration.  
Final dog action was: [At [2020-09-20T10:42:47.4776143-04:00], the following DogAction occured: [Dog named Rex performed an action.]].  
Ending basic vault demonstration.
> 
> Starting MutableResourceVault Demo  
Will print results from MutableResourceVault Demo.  
>
>  
> Printing 18 dog action results:  
        DAR#    1:      [At [2020-09-20T10:42:47.7451546-04:00], the following DogAction occured: [Dog named Fido performed an action.]]  
        DAR#    2:      [At [2020-09-20T10:42:47.7467689-04:00], the following DogAction occured: [Dog named Muffie performed an action.]]  
        DAR#    3:      [At [2020-09-20T10:42:47.7472108-04:00], the following DogAction occured: [Dog named Rex performed an action.]]  
        DAR#    4:      [At [2020-09-20T10:42:47.7484366-04:00], the following DogAction occured: [Dog named Muffie performed an action.]]  
        DAR#    5:      [At [2020-09-20T10:42:47.7485437-04:00], the following DogAction occured: [Dog named Fido performed an action.]]  
        DAR#    6:      [At [2020-09-20T10:42:47.7495119-04:00], the following DogAction occured: [Dog named Rex performed an action.]]  
        DAR#    7:      [At [2020-09-20T10:42:47.7505092-04:00], the following DogAction occured: [Dog named Muffie performed an action.]]  
        DAR#    8:      [At [2020-09-20T10:42:47.7514178-04:00], the following DogAction occured: [Dog named Rex performed an action.]]  
        DAR#    9:      [At [2020-09-20T10:42:47.7534545-04:00], the following DogAction occured: [Dog named Rex performed an action.]]  
        DAR#    10:     [At [2020-09-20T10:42:47.9909874-04:00], the following DogAction occured: [Dog named Fido performed an action.]]  
        DAR#    11:     [At [2020-09-20T10:42:47.9911216-04:00], the following DogAction occured: [Dog named Muffie performed an action.]]  
        DAR#    12:     [At [2020-09-20T10:42:47.9914118-04:00], the following DogAction occured: [Dog named Rex performed an action.]]  
        DAR#    13:     [At [2020-09-20T10:42:47.9923904-04:00], the following DogAction occured: [Dog named Fido performed an action.]]  
        DAR#    14:     [At [2020-09-20T10:42:47.9924403-04:00], the following DogAction occured: [Dog named Muffie performed an action.]]  
        DAR#    15:     [At [2020-09-20T10:42:47.9933630-04:00], the following DogAction occured: [Dog named Rex performed an action.]]  
        DAR#    16:     [At [2020-09-20T10:42:47.9943917-04:00], the following DogAction occured: [Dog named Muffie performed an action.]]  
        DAR#    17:     [At [2020-09-20T10:42:47.9954311-04:00], the following DogAction occured: [Dog named Rex performed an action.]]  
        DAR#    18:     [At [2020-09-20T10:42:47.9974086-04:00], the following DogAction occured: [Dog named Rex performed an action.]]  
END dog action result printout.  
FINISHED MutableResourceVault Demo  
  
16. The final demonstration shows usage of a **ReadWriteVault**.  ReadWrite vaults require **vault-safe** protected resources.  Note that **vault-safety** does not imply *immutability* for **value types**.  In fact, large mutable structs work *very* well with **ReadWriteVaults**.  Please note that the Project Description PDF covers customized read-write vaults for protecting List<T> like collections of large value types and StringBuilders.  These specialized vaults are not covered in this tutorial.   

17. **ReadWriteVaults**  use *ReaderWriterLockSlim* as their underlying synchronization mechanism.  They allow several different *modes* of locking.  There are:  
  
  * Read-only locks via *RoLock* overloads
  * Read-write locks via *Lock* and *SpinLock* overloads (which, as with Monitor Vaults, do the same thing) 
  * Upgradable read-only locks via *UpgradableRoLock* overloads

18. Read-only locks can be **concurrently held by many threads** at once.  ReadWriteVaults shine here. All vaults are superior to the direct usage of the underlying synchronization mechanism by preventing via compiler errors unsynchronized access to the protected resource. The ReadWriteVault does this and more: it also prevents (via compiler error) mutating the protected resource while holding a read-only lock.  Direct use of ReadWriteLockSlim requires adherence to convention for both access to the protected resource and whether read or read-write access is permissible.  

19.  Writable locks are exclusive.  While a write lock is held no other lock of any type may be held 
concurrently (except, see below, an *upgradable* read-only lock can be converted into a writable lock without needing to release the read-only lock first).

20. Of any number of read-only locks held concurrently, **exactly one or exactly zero** of them may be an *upgradable* read-only lock.  An *upgradable* read-only lock may be used to obtain an exclusive writable lock without needing to release its read-lock first.  
  
21. In the demonstration of the ReadWriteVault, the protected resource is the value type SharedFlag a mutable, equatable and comparable vault-safe value type.  When designing a mutable value type to be a protected resource, especially a protected resource in a ReadWriteVault, it becomes **CRITICAL** to properly annotate all non-mutating **methods** and **readonly properties** with the *readonly* keyword to prevent defensive copying.  If the property is not auto-implemented and has a *set* accessor, you must also annotate the *get* accessor with the *readonly* keyword.  Note that the *get* accessor of an auto-implemented property is implicitly read-only.  Review the code and comments below very carefully: 
   
 ```csharp
    /// <summary>
    /// This large mutable struct is used in ReadWriteVault demo as its protected resource.
    /// </summary>
    /// <remarks>All properties could be auto-implemented but are not to demonstrate how to use
    /// readonly specifier.</remarks>
    public struct SharedFlags : IEquatable<SharedFlags>, IComparable<SharedFlags>
    {
        /// <summary>
        /// Factory method
        /// </summary>
        /// <returns>Shared flags with a unique guid, timestamp reflecting creation time,
        /// activation action of none and item count of zero</returns>
        public static SharedFlags CreateSharedFlags() =>
            new SharedFlags(Guid.NewGuid(), TimeStampSource.Now, ActiveAction.None, 0);

        /// <summary>
        /// Get the current item count (accessible with readonly and writeable lock)
        /// </summary>
        /// <remarks>Note readonly specification</remarks>
        public readonly ulong ItemCount => _itemCount;
        /// <summary>
        /// Get the timestamp of the last mutation (or construction) of this flag (accessible with readonly and writable lock)
        /// </summary>
        /// <remarks>Note readonly specification on getter.  If your not-auto-implemented
        /// property has a setter, the getter must be marked readonly to prevent defensive deep copy.</remarks>
        public DateTime LastUpdateAt
        {
            readonly get => _lastUpdateTimestamp;
            private set => _lastUpdateTimestamp = value;
        }

        /// <summary>
        /// Get the current active action of the flag (accessible with readonly and writable lock)
        /// </summary>
        public readonly ActiveAction CurrentAction => _currentAction;
        /// <summary>
        /// Get unique id of the flag (accessible with readonly and writable lock)
        /// </summary>
        /// <remarks>Note that auto-implemented (get) accessor is automatically readonly: specifier not needed.</remarks>
        public Guid FlagId { get; }

        #region Non mutating public methods and operators -- all these accessible from readonly and write lock
        /// <summary>
        /// Check whether two flags objects have same value (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if equal, false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator ==(in SharedFlags lhs, in SharedFlags rhs) => lhs.FlagId == rhs.FlagId &&
           lhs.LastUpdateAt == rhs.LastUpdateAt && lhs._itemCount == rhs._itemCount &&
           lhs._currentAction == rhs._currentAction;
        /// <summary>
        /// Check whether two flags objects have distinct values (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if values are distinct, false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator !=(in SharedFlags lhs, in SharedFlags rhs) => !(lhs == rhs);
        /// <summary>
        /// Check whether left hand operand is considered greater than right hand operand (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if left hand operand greater than right hand operand; false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator >(in SharedFlags lhs, in SharedFlags rhs) => Compare(in lhs, in rhs) > 0;
        /// <summary>
        /// Check whether left hand operand is considered less than right hand operand (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if left hand operand less than right hand operand; false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator <(in SharedFlags lhs, in SharedFlags rhs) => Compare(in lhs, in rhs) < 0;
        /// <summary>
        /// Check whether left hand operand is considered greater than or equal to right hand operand (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if left hand operand greater than or equal to right hand operand; false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator >=(in SharedFlags lhs, in SharedFlags rhs) => !(lhs < rhs);
        /// <summary>
        /// Check whether left hand operand is considered less than or equal to right hand operand (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>true if left hand operand less than or equal to right hand operand; false otherwise</returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static bool operator <=(in SharedFlags lhs, in SharedFlags rhs) => !(lhs > rhs);
        /// <summary>
        /// Get hash code (callable from readonly or write lock)
        /// </summary>
        /// <returns>a hash code</returns>
        /// <remarks>Note explicit readonly specifier allowing access from readonly lock ... otherwise,
        /// would cause defensive copy</remarks>
        public override readonly int GetHashCode() => FlagId.GetHashCode();
        /// <summary>
        /// Check to see if this value is the same value as some other object. (callable from readonly or write lock)
        /// </summary>
        /// <param name="other">the other object</param>
        /// <returns>true if same value, false otherwise</returns>
        /// <remarks>Note explicit readonly specifier allowing access from readonly lock ... otherwise,
        /// would cause defensive copy.  Avoid.... requires boxing ... a deep copy if the other object is a SharedFlag</remarks>
        public override readonly bool Equals(object other) => other is SharedFlags sf && sf == this;
        /// <summary>
        /// Check to see if this value is the same value as some other object (callable from readonly or write lock)
        /// </summary>
        /// <param name="other">the other object ... is passed by value as required (sadly) by interface ... avoid</param>
        /// <returns>true if same value, false otherwise</returns>
        /// <remarks>Note explicit readonly specifier allowing access from readonly lock ... otherwise,
        /// would cause defensive copy.  Avoid calling .... interface requires pass by value, resulting in deep copy</remarks>
        public readonly bool Equals(SharedFlags other) => other == this;
        /// <summary>
        /// Compare this value to another of the same type (callable from readonly or write lock)
        /// </summary>
        /// <param name="other">the other value (sadly, interface requires pass by value,
        /// resulting in deep copy).</param>
        /// <returns>
        /// a negative number if this value is less than <paramref name="other"/>
        /// a positive number if this value is greater than <paramref name="other"/>
        /// zero if this value equals <paramref name="other"/>
        /// </returns>
        /// <remarks>Not readonly specification in signature .... needed to prevent the defensive deep copying of this object.
        /// Avoid calling .... interface sadly requires pass by value resulting in deep copy of <paramref name="other"/> parameter.</remarks>
        public readonly int CompareTo(SharedFlags other) => Compare(in this, in other);

        /// <summary>
        /// Get string representation. (callable from readonly or write lock)
        /// </summary>
        /// <returns>a string representation of the value.</returns>
        /// <remarks>Note readonly specifier on method ... necessary to prevent defensive deep copy
        /// of value.</remarks>
        public override readonly string ToString() => "Current action: [" + _currentAction + "]; Last update: [" +
                                             LastUpdateAt.ToString("O") + "]; Item count: [" + _itemCount +
                                             "].";
        /// <summary>
        /// Compare to values of this type. (callable from readonly or write lock)
        /// </summary>
        /// <param name="lhs">left hand operand</param>
        /// <param name="rhs">right hand operand</param>
        /// <returns>
        /// a negative number if <paramref name="lhs"/> is less than <paramref name="rhs"/>
        /// a positive number if <paramref name="lhs"/> is greater than <paramref name="rhs"/>
        /// zero if <paramref name="lhs"/> equals <paramref name="rhs"/>
        /// </returns>
        /// <remarks>
        /// Defined with in to avoid copying.  This will disallow writing to fields,
        /// calling any non-readonly property or using a mutator method (mutator METHOD
        /// will compile but will make defensive copy and mutate the copy, not the original)
        /// </remarks>
        public static int Compare(in SharedFlags lhs, in SharedFlags rhs)
        {
            int ret;
            int idCompare = lhs.FlagId.CompareTo(rhs.FlagId);
            if (idCompare == 0)
            {
                int tsCompare = lhs.LastUpdateAt.CompareTo(rhs.LastUpdateAt);
                if (tsCompare == 0)
                {
                    int itemCtComp = lhs._itemCount.CompareTo(rhs._itemCount);
                    ret = itemCtComp == 0 ? CompareActions(lhs._currentAction, rhs._currentAction) : itemCtComp;
                }
                else
                {
                    ret = tsCompare;
                }
            }
            else
            {
                ret = idCompare;
            }
            return ret;

            static int CompareActions(ActiveAction la, ActiveAction ra) => ((ulong)la).CompareTo((ulong)ra);
        }
        #endregion

        #region Mutator MEthods -- callable with effect only from write lock .... calling from read lock will in defensive deep copy

        /// <summary>
        /// Start Frobnicating
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="CurrentAction"/> is not equal to <see cref="ActiveAction.None"/></exception>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public void Frobnicate()
        {
            if (_currentAction != ActiveAction.None)
            {
                throw new InvalidOperationException("Can only frobnicate when current action is none.");
            }

            _currentAction = ActiveAction.Frobnicating;
            LastUpdateAt = TimeStampSource.Now;
        }

        /// <summary>
        /// Start Prognosticating
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="CurrentAction"/> is not equal to <see cref="ActiveAction.Frobnicating"/></exception>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public void Prognosticate()
        {
            if (_currentAction != ActiveAction.Frobnicating)
            {
                throw new InvalidOperationException("Can only prognosticate when current action is frobnicate.");
            }

            _currentAction = ActiveAction.Prognosticating;
            LastUpdateAt = TimeStampSource.Now;
        }

        /// <summary>
        /// Start Procrastinating
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="CurrentAction"/> is not equal to <see cref="ActiveAction.Prognosticating"/></exception>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public void Procrastinate()
        {
            if (_currentAction != ActiveAction.Prognosticating)
            {
                throw new InvalidOperationException("Can only procrastinate when current action is prognosticate.");
            }

            _currentAction = ActiveAction.Procrastinating;
            LastUpdateAt = TimeStampSource.Now;
        }

        /// <summary>
        /// Start Dithering
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="CurrentAction"/> is not equal to <see cref="ActiveAction.Procrastinating"/></exception>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public void Dither()
        {
            if (_currentAction != ActiveAction.Procrastinating)
            {
                throw new InvalidOperationException("Can only dither when current action is procrastinate.");
            }

            _currentAction = ActiveAction.Dithering;
            LastUpdateAt = TimeStampSource.Now;
        }

        /// <summary>
        /// Set to Done
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="CurrentAction"/> is not equal to <see cref="ActiveAction.Dithering"/></exception>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public void Finish()
        {
            if (_currentAction != ActiveAction.Dithering)
            {
                throw new InvalidOperationException("Can only finish when current action is dithering.");
            }

            _currentAction = ActiveAction.Done;
            LastUpdateAt = TimeStampSource.Now;
        }

        /// <summary>
        /// Increment <see cref="ItemCount"/> by <paramref name="count"/>
        /// </summary>
        /// <param name="count">amount by which <see cref="ItemCount"/> should be incremented.</param>
        /// <returns>true if incremented (including by zero), false if not incremented (incrementing by zero considered increment for
        /// these purposes).  Will return false if <see cref="CurrentAction"/> is equal to <see cref="ActiveAction.Done"/>.</returns>
        /// <remarks>If you attempt to call from readonly vault, will issue compiler warning.  The value will be deep copied and the deep copy, rather
        /// than protected resource will be updated.</remarks>
        public bool Increment(ulong count)
        {
            if (_currentAction == ActiveAction.Done) return false;
            _itemCount += count;
            LastUpdateAt = TimeStampSource.Now;
            return true;
        }
        #endregion

        #region Private CTOR
        private SharedFlags(Guid id, DateTime ts, ActiveAction action, ulong count)
        {
            _itemCount = count;
            FlagId = id;
            _currentAction = action;
            _lastUpdateTimestamp = ts;
        }
        #endregion

        #region Private data
        private ulong _itemCount;
        private DateTime _lastUpdateTimestamp;
        private ActiveAction _currentAction;
        #endregion
    }

    public enum ActiveAction : ulong
    {
        None,
        Frobnicating,
        Prognosticating,
        Procrastinating,
        Dithering,
        Done
    }
```  
  
22. This demonstration has four threads concurrently contending for access to the SharedFlags object: two threads that obtain **read-only locks**, query for information, then release the locks and repeat; one thread that obtains a **writable lock**, mutates the flag then continues (or quit if the mutation signals termination condition); one thread that obtains an **upgradable read-only lock** and checks for certain conditions, which if they hold true, causes it to upgrade to a write lock and make a specific mutation.  All these threads continue until they detect a termination condition or a fault.

23. The ReadWriteVault demonstration spawns a master thread.  The master thread then creates the reader threads, the writer thread, and the upgradable read-only thread.  It then waits until timeout or all threads complete.  Then the main thread returns the log produced by the reader threads.  Because the log is typically large (several megabytes), it is written to file rather than displayed.  

     ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_12.png?raw=true)  


24.  The SharedFlag's CurrentAction property is mutated via one of its several mutator methods.  It may only be changed from:  
* None->Frobnicating via the *Frobnicate()* mutator
* Frobnicating->Prognosticating via the *Prognosticate()* mutator
* Prognosticating->Procrastinating via the *Procrastinate()* mutator
* Procrastinating->Dithering via the *Dither()* mutator
* Dithering->Done via the *Finish()* mutator  
  
25. Attempts to use a mutator while it is not in the correct source state for that mutator result in an InvalidOperationException being thrown.  Successful calls to the mutators update the state and timestamp accordingly.
      
26. A call to *Increment(ulong amount)* mutator will, in any state except *Done*, increment *ItemCount* by *amount*, update the timestamp and return *true*.  If called while in *Done*, *false* is returned but nothing else happens.

27. The reader threads simply examine the state if the Shared Flags, get its string representation **(#1)** and CurrentAction property value **(#2)**.  The readers then release their read-only lock **(#3)**, log (along with their reader thread number -- 1 or 2) **(#4)** the results of the query.  Finally, they yield to another **(#5)** thread and repeat until a termination condition **(#6)** is detected. 

     ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_13.png?raw=true)  


28.  The writer continues looping until a termination condition is reached.  First, it generates random ulong between 0 and 102 **(#1)**, inclusive.  Then, it obtains a writable lock **(#2)** and calls the SharedFlags *Increment(ulong)* **(#3)** mutator method passing the generated random number as a parameter.  If *Increment(ulong)* returns false **(#4)**, it considers it a termination condition, releases its lock **(#5)** and returns.  Otherwise, it releases its lock **(#5)** and continues looping.  

     ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_14.png?raw=true)  
       
29.   
    
   The upgradable thread loops until a fault **(#1)** is encountered or the last action it read from the SharedFlags is 'Done' **(#2)**.  The thread first obtains **(#3)** an upgradable read-only lock on the shared flags.  It can obtain this lock while other threads hold read-only locks.  If any thread holds a write lock or another upgradable read-only lock, it must wait. (It is usually a good practice to only obtain an upgradable read-only lock on the same thread.)  It then stores **(#4)** the value of the SharedFlags's *CurrentAction* property in a local variable.  It then switches **(#5)** on that local variable and depending on its state performs further actions, possibly preceded by further checks:
    * If the current state is *None*, the lock is upgraded to a writable lock and the *Frobnicate* mutator is called, causing the SharedFlags to transition to the Frobnicating state.  Frobnicating is saved in the local variable *changedToAction* for later use.  The writable lock is then released (but the upgradable read-only lock remains held). **(#6)**  
    * If the current state is *Frobnicating*, check to see whether *ItemCount* meets threshold (#14) for *Prognosticating*.  If it is, upgrade to a writable lock **(#7)**, call the *Prognosticate* mutator, record that we changed to *Prognosticating* this iteration and release the writable lock.
    * If the current state is *Prognosticating*, check to see whether *ItemCount* meets threshold (#14) for *Procrastinating*.  If it is, upgrade to a writable lock **(#8)**, call the *Procrastinate* mutator, record that we changed to *Procrastinating* this iteration and release the writable lock.
    * If the current state is *Procrastinating*, check to see whether *ItemCount* meets threshold (#14) for *Dithering*.  If it is, upgrade to a writable lock **(#9)**, call the *Dither* mutator, record that we changed to *Dithering* this iteration and release the writable lock.
    * If the current state is *Dithering*, check to see whether *ItemCount* meets threshold (#14) for *Done*.  If it is, upgrade to a writable lock **(#10)**, call the *Finish* mutator, record that we changed to *Done* this iteration and release the writable lock.    
      
    After exiting the switch, release **(#12)** the upgradable read-only lock then:  
    * Check whether we changed to a new state this iteration and, if we have, log it to the Console **(#11)**.  
    * If we have not changed this iteration and we are not in the 'Done' state (and thus about to end the loop), yield to another thread if the Operating System thinks that it is a good idea **(#13)**.  
      
     ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_15.png?raw=true)  
       
30. Executing the code shows the Upgradable Read Only thread logging state changes to the console (#3).
Because the log file is too large to display in the console as is done for the prior two demonstrations, the ReadWriteVault Demo outputs the ReadOnly threads to a text file as shown (##1-2).  
  
![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_func_tour_pics/pic_16.png?raw=true)  
  
31. An excerpt from the output log file is included below.  The first timestamp is the time the message reached the logger.  The second timestamp is the time of the last update / change that happened to the SharedFlags as of the time the ReadOnly thread made that query.  Notice that the state changes occur shortly after ItemCount reaches the appropriate threshold.  "..." denotes skipped entries.
    
> At [2020-09-23T08:36:33.5309401-04:00]:		 From reader #1: Current action: [None]; Last update: [2020-09-23T08:36:33.4941321-04:00]; Item count: [0].  
At [2020-09-23T08:36:33.5309504-04:00]:		 From reader #2: Current action: [None]; Last update: [2020-09-23T08:36:33.4941321-04:00]; Item count: [0].  

>At [2020-09-23T08:36:33.5333478-04:00]:		 From reader #1: Current action: [Frobnicating]; Last update: [2020-09-23T08:36:33.5333186-04:00]; Item count: [16306].  
At [2020-09-23T08:36:33.5333784-04:00]:		 From reader #1: Current action: [Frobnicating]; Last update: [2020-09-23T08:36:33.5333186-04:00]; Item count: [16306].  
...  
At [2020-09-23T08:36:33.5692732-04:00]:		 From reader #2: Current action: [Frobnicating]; Last update: [2020-09-23T08:36:33.5692679-04:00]; Item count: [145038].  
At [2020-09-23T08:36:33.5692768-04:00]:		 From reader #1: Current action: [Frobnicating]; Last update: [2020-09-23T08:36:33.5692679-04:00]; Item count: [145038].  
  
>At [2020-09-23T08:36:33.5695669-04:00]:		 From reader #1: Current action: [Prognosticating]; Last update: [2020-09-23T08:36:33.5695543-04:00]; Item count: [145240].  
...  
At [2020-09-23T08:36:33.5861594-04:00]:		 From reader #2: Current action: [Prognosticating]; Last update: [2020-09-23T08:36:33.5861537-04:00]; Item count: [250056].  
 

>At [2020-09-23T08:36:33.5865868-04:00]:		 From reader #2: Current action: [Procrastinating]; Last update: [2020-09-23T08:36:33.5865741-04:00]; Item count: [251152].  
At [2020-09-23T08:36:33.5865973-04:00]:		 From reader #1: Current action: [Procrastinating]; Last update: [2020-09-23T08:36:33.5865918-04:00]; Item count: [251250].  
...  
At [2020-09-23T08:36:33.6050584-04:00]:		 From reader #1: Current action: [Procrastinating]; Last update: [2020-09-23T08:36:33.6050521-04:00]; Item count: [374971].  
At [2020-09-23T08:36:33.6050684-04:00]:		 From reader #2: Current action: [Procrastinating]; Last update: [2020-09-23T08:36:33.6050598-04:00]; Item count: [375007].  

>At [2020-09-23T08:36:33.6054020-04:00]:		 From reader #2: Current action: [Dithering]; Last update: [2020-09-23T08:36:33.6053195-04:00]; Item count: [375149].  
At [2020-09-23T08:36:33.6054188-04:00]:		 From reader #1: Current action: [Dithering]; Last update: [2020-09-23T08:36:33.6054081-04:00]; Item count: [375231].  
...  
At [2020-09-23T08:36:33.6597486-04:00]:		 From reader #2: Current action: [Dithering]; Last update: [2020-09-23T08:36:33.6597416-04:00]; Item count: [523193].  
At [2020-09-23T08:36:33.6597550-04:00]:		 From reader #1: Current action: [Dithering]; Last update: [2020-09-23T08:36:33.6597416-04:00]; Item count: [523193].  
At [2020-09-23T08:36:33.6597620-04:00]:		 From reader #1: Current action: [Dithering]; Last update: [2020-09-23T08:36:33.6597416-04:00]; Item count: [523193].  

>At [2020-09-23T08:36:33.6600566-04:00]:		 From reader #1: Current action: [Done]; Last update: [2020-09-23T08:36:33.6599964-04:00]; Item count: [523193].  
At [2020-09-23T08:36:33.6600732-04:00]:		 From reader #2: Current action: [Done]; Last update: [2020-09-23T08:36:33.6599964-04:00]; Item count: [523193].  
  
32. This is the end of the Quick Start tutorial.  There are far more resources available to help you make the best possible use of DotNetVault:

    * **["Project Description.pdf"](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVault%20Description.pdf)** is a large PDF document with a detailed table of contents and table of figures.  You should be able to find most, if not all, of the information you need there

    * **"[Example Code Playground](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/ExampleCodePlayground)"** project is a project with plenty of code to allow you to learn about DotNetVault and its static analyzer in a hands-on fashion.  There is plenty of commented-out code that will trigger the analyzer's compilation errors along with notes as to why it is an error along with the rationale.
    
    * **"[Clorton Game](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/Clorton%20Game/ClortonGameDemo)"** is both a test project and unit test that shows the customized StringBuilder read-write vault being used in an environment of very high thread-contention.
    
    * **"[Cafe Babe Game](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/CafeBabeGame/CafeBabeGame)"** is both a test project and unit test that shows the customized ReadWrite ValueListVault being used in an environment of very high thread-contention.  The ValueList vault guards a `List<T>`-like collection optimized for the efficient storage and retrieval of large value-types.
    
    * **"[Laundry Stress Test](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/ExampleLaundryMachine)"** is a WPF (and thus, unfortunately, a Windows-only) demonstration and stress test of a laundry service.  There are multiple laundry machines (each with with its own state machine and threads) and loader and unloader robot-agents (also each with its own thread) who contend with the laundry machines' threads and with each other for access to the laundry and laundry machines. 