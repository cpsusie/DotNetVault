# DotNetVault

## Synchronization Library and Static Analyzer for C\# 8.0+  
**MAJOR NEW RELEASE (version 0.2.5.x)**

DotNetVault takes its inspiration from the synchronization mechanisms provided
by Rust language and the Facebook Folly C++ synchronization library. These
synchronization mechanisms observe that the mutex should own the data they
protect. You literally cannot access the protected data without first obtaining
the lock. RAII destroys the lock when it goes out of scope – even if an
exception is thrown or early return taken. DotNetVault provides mechanisms for 
RAII-based thread synchronization and uses Roslyn analyzer to *add new rules to the
C\# language* to actively prevent resources protected by its vaults from being used 
in a thread-unsafe or non-synchronized way. You need not rely on convention or programmer
discipline to ensure compliance: DotNetVault enforces compliance **at compile-time**.

### **Requirements**
  
* Environment targeting DotNet Framework 4.8+, DotNet Standard 2.0+ or DotNet Core 3.1+.  (For framework 4.8 and DotNet Standard 2.0, any projects using this library or its analyzer must be manually set to use C# 8.0.  This is unnecessary for DotNetCore 3.1+).  
* A build environment that supports Roslyn Analyzers and capable of emitting compiler errors as prompted by analyzers. (Both Visual Studio Community 2019+ on Windows and Jetbrains Rider 2019.3.1+ on Amazon Linux have been extensively tested).  Visual Studio Code has also been tested but not extensively.  From testing, Visual Studio Code, with Roslyn Analyzers enabled, will emit compilation errors at build-time, but Intellisense identification of the errors was markedly inferior to the Intellisense available in Visual Studio and Rider.    
* Installation of this library and its dependencies via NuGet.

### **Advantages:**

#### **Easy to Change Underlying Synchronization Mechanisms**  
DotNetVault uses a variety of different underlying synchronization mechanisms:

  * **Monitor + Sync object**  This is the most widely used thread synchronization mechanism
  used in C\# code.  It is the mechanism used when you use the *lock* keyword such as 
  
    ```csharp
    lock (_syncObj)
    {
        //Access protected resource
    }
    ```
  * **Atomics**  You can also base your synchronization on lock-free interlocked exchanges using 
  DotNetVault.
  
  * **ReaderWriterLockSlim** This synchronization mechanism allows for the possibility of read-only locks
  which multiple threads can obtain concurrently as well as read-write locks which are exclusive.
   (Upgradable read-only locks are also available.)  DotNetVault provides Vaults that use this as its 
   underlying synchronization mechanism as well.  
   
All of the above synchronization mechanisms use different syntax to obtain and release locks.  If you use them
directly, and decide to switch to (or try) a different mechanism, it will require extensive refactoring which may
be prohibitively difficult to do correctly: and be equally difficult to switch back.  **DotNetVault simplifies the 
required refactoring.**  All vaults expose a **common *compile-time* API.**  Simply change the type of vault protecting
your resource (and perhaps update the constructor that instantiates the vault) and the underlying mechanism used 
changes accordingly. (Of course, read-only locks are only available with vaults that use ReaderWriterLockSlim, 
but if you are using read-only locks, the other mechanisms are inappropriate.)  
   
#### **Deadlock Avoidance**  
  
Deadlocks occur most frequently when you acquire successive locks in differing orders on different threads.  In large projects,
it can be very difficult to ensure that locks are always obtained in the same order.  Errors typically manifest in your application 
*silently* freezing up.  Moreover, it can be **very** difficult to reproduce certain deadlocks and the act of attaching a debugger
with break points may change the behavior your customer is observing.  In short, these are very expensive problems to debug.

The way that DotNetVault helps you avoid deadlocks is by making all lock acquisitions timed by default.  You can, of course, do timed acquisition with the underlying primitives directly, but this is syntactically difficult (compare, for example, the untimed,  scope-based, release-guaranteed lock statement with its timed alternative using Monitor.TryEnter and a timeout).  The syntactic difficulties and lack of RAII, scope based acquisition and release easily leads to errors such as:  

* by forgetting to free it (perhaps due to early return or exception), 
* not realizing it wasn't obtained successfully and, under this delusion, proceed to access the resource without synchronization
* deciding not to use a timed acquisition because of the foregoing difficulties and thus running into the foregoing dreaded *deadlocks*.

Using DotNetVault, ***all* access is subject to RAII, scoped-based lock acquisition and release**.  Failure to obtain a lock throws an exception --- there can be no mistake as to whether it is obtained.  When a locks scope ends, it is released.  By default, **all** lock acquisitions are timed -- you must explicitly and clearly use the differently and ominously named untimed acquisition methods if you wish to avoid the slight overhead imposed by timed acquisition. (Typically after using and heavily testing using the standard timed acquisition methods, ensuring there are no deadlocks, profiling and discovering a bottleneck caused by timed acquisition, and then switching to the untimed acquisition method in those identified bottlenecks.  It is **hard** to deadlock accidentally in DotNetVault.

#### **RAII (Scope-based) Lock Acquisition and Release:**  

Locks are stack-only objects (ref structs) and the integrated Roslyn analyzer forces you to declare the lock inline in a using statement or declaration, or it will cause a compilation error.  

 * There is no danger of accidentally holding the lock open longer than its scope **even in the presence of an exception or early return.**
 * There is no danger of being able to access the protected resource if the lock is not obtained.
 * There is no danger of being able to access the protected resource after release.  
 
#### **Enforcement of Read-Only Access When Lock is Read-Only**  

  None of the synchronization primitives, when used directly, *prevents* access to the protected resource when it is not obtained. DotNetVault, as mentioned in numerous places, *actively prevents such unsynchronized access at **compile-time**.* ReaderWriterLockSlim is unique among the synchronization primitives employed by DotNetVault in allowing for multiple threads to hold *read-only* locks at the same time.  As the primitive cannot prevent access to the resource generally, it also cannot *validate that user code does not **mutate** the protected resource while holding a **read-only lock**.* DotNetVault not only prevents access to the underlying resource while the correct lock is not held, it also enforces that, while a **read-only lock** is held, the protected object **cannot be mutated**.  This is also enforced statically, **at compile-time**.

#### **Isolation of Protected Resources**

The need for programmer discipline is reduced:  
1. programmers do not need to remember which mutexes protect which resources,  
2. once a protected resource is in a vault, no reference to any mutable state of any object in the resources object graph can be accessed except when a stack-frame limited lock has been obtained: the static analyzer prevents it **at compile-time**.  
3. programmers cannot access the protected resource before they obtain the lock and cannot access any mutable state from the protected resource after releasing the lock, and     
4. static analysis rules prevent mutable state not protected by the vault from becoming part of the state of the protected resource.

#### **Summary of Advantages**

The ubiquity of shared mutable state in Garbage Collected languages like C\# can work at cross purposes to thread-safety. One approach to thread-safety in such languages is to eliminate the use of mutable state. Because this is not always possible or even desireable, the synchronization mechanisms employed in C\# typically rely on programmer knowledge and discipline. DotNetVault uses Disposable Ref Structs together with custom language rules enforced by an integrated Roslyn analyzer to prevent unsynchronized sharing of protected resources. Locks cannot be held longer than their scope and, by default, will timeout. This enables deadlock-avoidance.

Try DotNetVault. There is a learning curve because it is restrictive about sharing protected resources. There are plenty of documents and example projects provided with the source code of this project that can ease you into that learning curve and demonstrate DotNetVault's suitability for use in highly complex concurrent code. Armed with the resources that DotNetVault provides, you
will be able to approach concurrent programming, including use of shared mutable state, with a high degree of confidence.

### **Resources For Learning To Use DotNetVault**

#### **Quick Start Guides**

1. [A quick-start installation guide for installing the DotNetVault library and analyzer for use in Visual Studio 2019+ on Windows.](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Installation%20Guide%20Visual%20Studio%202019%20(Windows%2010).md#dotnetvault-quick-start-installation-guide-visual-studio-2019-windows-10)
2. [A quick start installation guide for installing the DotNetVault library and analyzer for use in JetBrains Rider 2019.3.1+](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Installation%20Guide%20%E2%80%93%20JetBrains%20Rider%20(Tested%20on%20Amazon%20Linux).md#dotnetvault-quick-start-installation-guide--jetbrains-rider-201931-tested-on-amazon-linux) (created on an Amazon Linux environment, presumably applicable to any platform supporting JetBrains Rider 2019.3.1+).
3. [A guided overview of the functionality of DotNetVault](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Functionality%20Tour%20%E2%80%93%20JetBrains%20Rider%20(Amazon%20Linux).md#dotnetvault-quick-start-functionality-tour--jetbrains-rider-201931-amazon-linux) along with a [test project](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/DotNetVaultQuickStart) available on Github in both source and compiled code.  
  
#### **Detailed Project Description**

  An extensive document called "[Project Description](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVault%20Description.pdf)" covers:

* vaults, 
* locked resource objects, 
* static analysis rules,
* attributes that activate the static analyzer rules
* rationale for the static analyzer rules
* concept of vault-safety
* effective design of easy-to-use and easy-to-isolate objects

#### **Demo projects and Stress Test Projects**  

* An *[Example Code Playground](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/ExampleCodePlayground)*: allows user to get used to the validation rules, provides sample code.  Also contains commented-out code (with explanations) that, if uncommented, will trigger various static-analyzer-based compilation errors.  
* *["Laundry Machine" Stress Test](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/ExampleLaundryMachine):* This multi-project (Windows-Only, because of WPF) test demonstrates multi-threaded state machines using vaults to protect their mutable state flags.  There are three laundry state machines, each with their own threads.  Four robots (two loader and two unloader) also have their own threads and contend for access to the laundry machine.  The loaders grab dirty clothes, contend (vs other robots and the laundry machine thread itself) for access to the laundry machines in turn, when access is obtained they check if it is empty, if so, they load laundry, start the machine and then release access.  The unloaders (vs the loaders, the other unloader and the laundry machine threads itself) contend for access to the same laundry machines.  They search for a full laundry machines with clean clothes: once found, they unload the laundry and place it in the clean bin.  The stress test ends when all the laundry articles are in the clean bin.  This shows multi-threaded state machine interaction using synchronized mutable state.
* *[Clorton Game](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/Clorton%20Game/ClortonGameDemo):* A console-based stress test (can run on any platform supporting .NET Core 3.1+). Clorton game is played by a set of read-only threads that scan a character buffer for the text “CLORTON”: the first reader thread to find this text wins the game. Non-competitive threads also participate. There is an ‘x’ writer and ‘o’ writer thread that obtain writeable locks on the resource and write a random number of ‘x’es or ‘o’es then release their lock and repeat. Finally, there is an arbiter thread that obtains an upgradable readonly lock. When it obtains this lock, it scans the buffer to determine whether the absolute value of the difference between the number of x’es and the number of o’es is a positive integer evenly divisible by thirteen: if so, it upgrades its lock to a writable lock and appends “CLORTON” to it. This enables one of the reader threads to find the text it seeks and thereby win the game.  This game has two modes: one uses a [BasicReadWriteVault](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVault/Vaults/BasicReadWriteVault.cs) protecting a *string*; the other a specialized, [customized ReadWriteVault](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVault/Vaults/ReadWriteStringBufferVault.cs) that allows access to a mutable string buffer (similar to a *StringBuilder*).
* *[Cáfe Babe Game](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/CafeBabeGame/CafeBabeGame)*:  this game (targeting DotNetCore 3.1 and in a console application format) is played in a conceptually similar way to the *Clorton Game* and serves a similar purpose as a stress test and demonstration.  Instead of a string or string buffer, the protected resource is a [customized read-write vault](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVault/Vaults/ReadWriteValueListVault.cs) that provides synchronized access to a [protected resource](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVault/RefReturningCollections/BigValueList.cs) that is, in many respects, like [`List<T>`](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=netcore-3.1) but instead is optimized for efficient storage and retrieval of large immutable value types.  In this example, a single-purpose 256-bit unsigned integer value type serves as the content that goes into the collection (rather than characters into a *string* or mutable string buffer).  This value type certainly qualifies as a large immutable value type and was chosen for that reason.  The same threads are used for this game as for the *Clorton Game* (i.e. Competitor reader threads, two non-competitive writer threads and one non-competitive arbiter thread that obtains upgradable read-only locks.)  Unlike *Clorton Game* the writer threads each have their own 256-bit integer 0xface_c0ca_f00d_bad0_face_c0ca_f00d_bad0_face_c0ca_f00d_bad0_face_c0ca_f00d_bad0 and 0xc0de_d00d_fea2_b00b_c0de_d00d_fea2_b00b_c0de_d00d_fea2_b00b_c0de_d00d_fea2_b00b) which they write a random number of times into the protected collection each time they obtain their locks.  The arbiter thread obtains an upgradable read-only lock and counts how many of each of those two values are present in the collection.  If the absolute value of the difference of those counts is non-zero and evenly divisible by thirteen,
it upgrades to a writable lock and writes the value 0xDEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE into the collection.  If that condition (absVCountDiff != 0 && ( (absVCountDiff % 13) == 0) it will not upgrade its lock but will instead release it and check again later.  The first reader thread that finds 0xDEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE_DEAD_BEEF_CAFE_BABE in the collection wins.  
* *[Console Stress Test](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/ConsoleStressTest)* This stress-test and demonstration console application is simple and straightforward.  Although not as interesting as the *Clorton Game* and *Cáfe Babe Game*, it may be more accessible to gain a very basic overview of how vaults can be used.
* *Unit Testing Projects*- This project includes two unit testing projects.  The [first contains unit tests](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/DotNetVault.Test) that focus on testing the operation of this project's static analyzer and may be useful if you are interested in learning how Roslyn Analyzers work.  The analyzer code itself is found [here](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVault/DotNetVaultAnalyzer.cs) and may also be useful for learning purposes.  The [other unit tests](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/VaultUnitTests) validate the correctness of the library functionality (i.e. the vaults and locked resource objects) and perform some stress testing as well.
* Explaining how to debug the analyzer itself is beyond the scope of this document.  Essentially, one must use the *[vsix](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/DotNetVault.Vsix)* project to launch a second special instance of visual studio that is running a debug build of the analyzers and has appropriate breakpoints set.  Then in the other instance, you can load the project that causes the issue you wish to debug.  This, obviously, is not for the faint-of-heart.  An important thing to note is that you **should not use the vsix extension project to install DotNetVault for normal use: analyzers imported via a vsix extension are incapable of causing compilation errors and this project depends heavily on compile-time analysis to ensure code is correct**.  To install DotNetVault for normal use, **use the nuget package**.    
  
#### Guide To Using Large Value Types  
In older versions of C\# we were cautioned against using large value types and value types with mutable state.  Recent changes to the C# language greatly mitigate concerns about the use of large and/or mutable value types.  The ability to pass or return structs by reference and the  *readonly* specifier (as applied to things that hithertofore did not permit this specifier, shown below) work together to nearly eliminate prior problems.  The readonly specifier now can be applied:
* To a struct itself to indicate the value is immutable and prevent defensive copying
* To struct readonly properties (to indicate that the get accessor will not cause any mutations to the state of the struct as a side effect)
* To struct read-write properties' get accessor (to indicate that the get accessor will not cause any mutations to the state of the struct as a side effect)
* To struct methods, to indicate that the method will not cause side-effects that mutate the value of the struct.  

Essentially, when we have an immutable or **mutable** value type that we accessing **through a readonly reference**, we can now force the compiler to omit defensive copying.  This is especially helpful when passing a mutable struct by readonly reference.  This facilitates both isolating shared mutable state and controlling read-only vs read-write accessibility.  Happily this ability works together with the *ReadWriteVault nearly **perfectly***. For that reason, this project provides a [guide document](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/Advantages%20of%20Using%20Large%20Mutable%20Structs.pdf) for how to design large immutable value types in general as well as how to design large mutable value types as a shared state repository for usage with a ReadWrite vault. If you have trouble reading the pdf with your browser or otherwise desire, the document can be downloaded [here](https://github.com/cpsusie/DotNetVault/raw/v0.2.5.x/Advantages%20of%20Using%20Large%20Mutable%20Structs.pdf).

### **Development Roadmap** 

#### *Release History*

The last official release was version [0.1.5.4](https://github.com/cpsusie/DotNetVault/releases/tag/v0.1.5.4), available as a Nuget package [here](https://www.nuget.org/packages/DotNetVault/0.1.5.4).  Since then, **many** features have been added to DotNetVault.  All of these features were included in the feature-complete beta version  [0.2.2.12-beta](https://github.com/cpsusie/DotNetVault/releases/tag/v0.2.2.12-beta), available as a Nuget package [here](https://www.nuget.org/packages/DotNetVault/0.2.2.12-beta).   The following list is a non-exhaustive summary of these new features:  
* Upgrading to new versions of Roslyn libraries, immutable collections and other minor dependency upgrades
* Changing some of the formatting of analyzer diagnostics to comply with Roslyn authors' recommendations
* Adding Monitor Vaults (using Monitor.Enter + sync object) as the synchronization mechanism
* Adding ReadWrite Vaults (using ReaderWriterLockSlim) as their synchronization mechanism
* Fixing flawed static analyzer rules
* Adding new analyzer rules to close encountered loopholes in the ruleset that potentially allowed unsynchronized access to protected resource objects
* Unit tests as appropriate for new functionality
* Creation of quick start installation guides with test projects
* Not including project pdfs in the released package but instead providing an md document and a txt document with links to those documents in the github repository
* Significant updates to the formatting and content of project markdown documents
* Adding Source Link and releasing a symbol package along with the nuget package for this project
* Writing many test projects and demonstration projects to verify functionality, stress test and profile performance of the vaults
* Adding a document serving as a guide to using large mutable value types generally and as a repository for shared mutable state  

#### *Version / Branch 0.2.5.x*

No major new features will be added to version 0.2.5.  Development will remain open in the [0.2.5 branch](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x) primarily for refinements, bug fixes and documentation updates.  Versions 0.2.5+ will continue to support .NET Framework 4.8, .NET Standard 2.0+ and .NET Core 3.1+ but will not make use of any features from the upcoming Version 5 of the unified DotNet framework.  If you are not upgrading your projects to .NET 5, continue to use releases numbered 0.2 but make no upgrade to any package versioned 0.3+.  The first official release will likely be quickly followed up by minor releases fixing aesthetic problems, documentation, update of links etc.  Making upgrades between the first *official* 0.2.5+ release and the first *stable official* release thereafter will not (to the extent possible) affect behavior of the library.  Analyzer behavior will be updated only to close any encountered loopholes (or minor textual or formatting changes).

#### Future Features

All (non-trivial) future feature development will occur in the [master branch](https://github.com/cpsusie/DotNetVault/tree/master) of this project.  They will be released starting at version 0.3+.  It is likely that the next version of DotNetVault will be targetting the upcoming unified framework version 5.0+ and not support prior versions of DotNet.  The primary focus of development will be the code generation capabilities of the Roslyn platform planned for release with .NET version 5.0+.  It is hoped to allow development and (to some extent) automated generation of customized vaults and their locked resource objects for users of this library.


See **[DotNetVault Description.pdf](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVault%20Description.pdf)** which serves as the most complete design document for this project.