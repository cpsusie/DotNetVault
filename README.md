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

1. A quick-start installation guide for installing the DotNetVault library and analyzer for use in Visual Studio 2019+ on Windows.
2. A quick start installation guide for installing the DotNetVault library and analyzer for use in JetBrains Rider 2019.3.1+ (created on an Amazon Linux environment, presumably applicable to any platform supporting JetBrains Rider 2019.3.1+).
3. A guided overview of the functionality of DotNetVault along with a test project available on Github in both source and compiled code.  
  
#### **Detailed Project Description**

  An extensive document called "Project Description.pdf" *insert link* covers:  

* vaults, 
* locked resource objects, 
* static analysis rules,
* attributes that activate the static analyzer rules
* rationale for the static analyzer rules
* concept of vault-safety
* effective design of easy-to-use and easy-to-isolate objects

#### **Demo projects and Stress Test Projects**  
(All are available on Github in both source code and compiled libraries / executables.)
  
* *An example code playground*: allows user to get used to the validation rules, provides sample code.  Also contains commented-out code (with explanations) that, if uncommented, will trigger various static-analyzer-based compilation errors.  
* *"Laundry Machine" Stress Test:* This test (Windows-Only, because of WPF) demonstrates multi-threaded state machines using vaults to protected their mutable state flags.  There are three laundry state machines, each with their own threads.  Four robots (two loader and two unloader) also have their own threads and contend for access to the laundry machine.  The loaders grab dirty clothes, contend (vs other robots and the laundry machine thread itself) for access to the laundry machines in turn, when access is obtained they check if it is empty, if so, they load laundry, start the machine and then release access.  The unloaders (vs the loaders, the other unloader and the laundry machine threads itself) contend for access to the same laundry machines.  They search for a full laundry machines with clean clothes: once found, they unload the laundry and place it in the clean bin.  The stress test ends when all the laundry articles are in the clean bin.  This shows multi-threaded state machine interaction using synchronized mutable state.
* *Clorton Game:* A console-based stress test (can run on any platform supporting .NET Core 3.1+) demonstrating 

**Development Roadmap**: 
	As of version 0.2.2.12-beta, Version 0.2 is feature complete 
and with luck will be released in its first non-beta version soon. Any further
releases in version two will hopefully be limited to documentation content updates, cleanup
of test code and demonstration code.  Bug fixes may also be released in Version 2 
but no new features (except as needed to fix bugs) should be expected.  

	Future development in Version 0.2 after it is released in non-beta form will
be limited to the correction of bugs and other flaws and perhaps refactoring to 
the extent it does not materially change behavior.

	After Version 0.2, new features will be developed under 0.3.  These
features currently center on taking advantage of Roslyn Analyzers which should be
available with .NET 5. 

See **DotNetVault Description.pdf** for full description of this project.

RELEASE NOTES VERSION 0.2.2.12-beta:
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    Fixed a Bug 92 where copying a protected resource into another ref-struct declared in a larger scope (of the same type or containing a field at any level of nesting in its graph) could result in unsynchronized access.  

    Fix was accomplished by the addition of more analyzer rules and attributes that can trigger them.

    Unit tests were added to validate the fix and code was added to the ExampleCodePlayground demonstrating Bug92.

    A full description of Bug 92, the new attributes and analyzer rules is available now in **DotNetVault Description.pdf**.
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
RELEASE NOTES VERSION 0.2.2.1-beta:
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    A BigValueListVault added, providing a vault protecting a list-like collection, especially suited for large value types. 

    Unit tests added, including a stress test called Cafe Babe game.  The Cafe Babe game is a unit test and stand-alone console-driven stress testing utility.

    "DotNetVault.Description.pdf" updated to reflect changes.
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
RELEASE NOTES VERSION 0.2.1.22-beta
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
 This is a beta release.  Current stable release is 0.1.5.4.
 
 This release adds a ReadWriteStringBuffer vault that provides thread-safe readonly, upgradable readonly and writable access to a StringBuilder object.  It also (when binaries or source retrieved from GitHub) includes the "Clorton Game" which demonstrates usage of the readwrite vault and provides a stress test to validate its functionality.

 "DotNetVault.Description.pdf" updated to reflect changes.
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~


