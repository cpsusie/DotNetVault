# DotNetVault

## Synchronization Library and Static Analyzer for C\# 8.0+  
**MAJOR NEW RELEASE Version 1.0**

DotNetVault takes its inspiration from the synchronization mechanisms provided
by Rust language and the Facebook Folly C++ synchronization library. These
synchronization mechanisms observe that the mutex should own the data they
protect. RAII destroys the lock when it goes out of scope – even if an
exception is thrown or early return taken. DotNetVault provides mechanisms for 
RAII-based thread synchronization and actively prevents (**at compile-time**) resources protected by it from  thread-unsafe or non-synchronized access. 

### **Advantages:**

#### **Easy to Change Underlying Synchronization Mechanisms**  
DotNetVault uses a variety of different underlying synchronization mechanisms:

  * **Monitor + Sync object**  
  * **Atomics**  
  * **ReaderWriterLockSlim** 
  
If you use them directly, and decide to switch to (or try) a different mechanism, it will require extensive refactoring  **DotNetVault simplifies the required refactoring.**  All vaults expose a **common *compile-time* API.**  
   
#### **Deadlock Avoidance**  
  
Using DotNetVault, ***all* access is subject to RAII, scoped-based lock acquisition and release**.  Failure to obtain a lock throws an exception --- there can be no mistake as to whether it is obtained.  When a locks scope ends, it is released.  By default, **all** lock acquisitions are timed -- you must explicitly and clearly use the differently and ominously named untimed acquisition methods if you wish to avoid the slight overhead imposed by timed acquisition. (Typically after using and heavily testing using the standard timed acquisition methods, ensuring there are no deadlocks, profiling and discovering a bottleneck caused by timed acquisition, and then switching to the untimed acquisition method in those identified bottlenecks.  It is **hard** to deadlock accidentally in DotNetVault.

#### **RAII (Scope-based) Lock Acquisition and Release:**  

Locks are stack-only objects (ref structs) and the integrated Roslyn analyzer forces you to declare the lock inline in a using statement or declaration, or it will cause a compilation error.  

 * There is no danger of accidentally holding the lock open longer than its scope **even in the presence of an exception or early return.**
 * There is no danger of being able to access the protected resource if the lock is not obtained.
 * There is no danger of being able to access the protected resource after release.  
 
#### **Enforcement of Read-Only Access When Lock is Read-Only**  

 ReaderWriterLockSlim is unique among the synchronization primitives employed by DotNetVault in allowing for multiple threads to hold *read-only* locks at the same time.  DotNetVault not only prevents access to the underlying resource while the correct lock is not held, it also enforces that, while a **read-only lock** is held, the protected object **cannot be mutated**.  This is also enforced statically, **at compile-time**.

#### **Isolation of Protected Resources**

Static enforcement prevents unsynchronized access to protected resources.

### **Resources For Learning To Use DotNetVault**

#### **Quick Start Guides**

1. [A quick-start installation guide for installing the DotNetVault library and analyzer for use in Visual Studio 2019+ on Windows.](https://github.com/cpsusie/DotNetVault/blob/master/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Installation%20Guide%20Visual%20Studio%202019%20(Windows%2010).md#dotnetvault-quick-start-installation-guide-visual-studio-2019-windows-10)
2. [A quick start installation guide for installing the DotNetVault library and analyzer for use in JetBrains Rider 2019.3.1+](https://github.com/cpsusie/DotNetVault/blob/master/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Installation%20Guide%20%E2%80%93%20JetBrains%20Rider%20(Tested%20on%20Amazon%20Linux).md#dotnetvault-quick-start-installation-guide--jetbrains-rider-201931-tested-on-amazon-linux) (created on an Amazon Linux environment, presumably applicable to any platform supporting JetBrains Rider 2019.3.1+).
3. [A guided overview of the functionality of DotNetVault](https://github.com/cpsusie/DotNetVault/blob/master/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Functionality%20Tour%20%E2%80%93%20JetBrains%20Rider%20(Amazon%20Linux).md#dotnetvault-quick-start-functionality-tour--jetbrains-rider-201931-amazon-linux) along with a [test project](https://github.com/cpsusie/DotNetVault/tree/master/DotNetVaultQuickStart) available on Github in both source and compiled code.  

### **Development Roadmap** 

#### *Version 1.0*

This version represents the finalization of the work done in versions 0.2.5.x.  Versions 1.0+ will remain usable (assuming C# 8 manually enabled) from a .NET Framework 4.8 or NetStandard 2.0 environment (as well as .NET Core 3.1 and .NET 5). No major new features will be added to this version.  Development will remain open in the [1.0 branch](https://github.com/cpsusie/DotNetVault/tree/v1.0) primarily for refinements, bug fixes and documentation updates.  If you are not upgrading your projects to .NET 5, continue to use releases numbered 1.0.  Analyzer behavior will be updated only to close any encountered loopholes (or minor textual or formatting changes).

#### *Version 0.2.5.x*

  * Upgrading to new versions of Roslyn libraries, immutable collections and other minor dependency upgrades  
  * Changing some of the formatting of analyzer diagnostics to comply with Roslyn authors' recommendations  
  * Adding Monitor Vaults (using Monitor.Enter + sync object) as the synchronization mechanism  
  * Adding ReadWrite Vaults (using ReaderWriterLockSlim) as their synchronization mechanism  
  * Fixing flawed static analyzer rules  
  * Adding new analyzer rules to close encountered loopholes in the ruleset that potentially allowed unsynchronized access to protected resource objects  
  * Unit tests as appropriate for new functionality  
  * Creation of quick start installation guides with test projects  
  * Not including project PDF in the released package but instead providing an md document and a txt document with links to those documents in the GitHub repository  
  * Significant updates to the formatting and content of project markdown documents  
  * Adding Source Link and releasing a symbol package along with the nuget package for this project  
  * Writing many test projects and demonstration projects to verify functionality, stress test and profile performance of the vaults  
  * Adding a document serving as a guide to using large mutable value types generally and as a repository for shared mutable state  
  
#### 

#### Future Features

They will be released starting at version 2.0.  It is likely that the next version of DotNetVault will be targeting the upcoming unified framework version 5.0+ and not support prior versions of DotNet.  The primary focus of development will be the code generation capabilities of the Roslyn platform planned for release with .NET version 5.0+.  It is hoped to allow development and (to some extent) automated generation of customized vaults and their locked resource objects for users of this library.


See **[DotNetVault Description_v1.0.pdf](https://github.com/cpsusie/DotNetVault/blob/master/DotNetVault_Description_v1.0.pdf)** which serves as the most complete design document for this project.  Its latest draft version can be found [here](https://github.com/cpsusie/DotNetVault/blob/master/DotNetVault_Description_Latest_Draft.pdf).