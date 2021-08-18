# DotNetVault

## Synchronization Library and Static Analyzer for C\# 8.0+  
**MAJOR NEW RELEASE (version 0.2.5.x)**

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

1. [A quick-start installation guide for installing the DotNetVault library and analyzer for use in Visual Studio 2019+ on Windows.](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Installation%20Guide%20Visual%20Studio%202019%20(Windows%2010).md#dotnetvault-quick-start-installation-guide-visual-studio-2019-windows-10)
2. [A quick start installation guide for installing the DotNetVault library and analyzer for use in JetBrains Rider 2019.3.1+](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Installation%20Guide%20%E2%80%93%20JetBrains%20Rider%20(Tested%20on%20Amazon%20Linux).md#dotnetvault-quick-start-installation-guide--jetbrains-rider-201931-tested-on-amazon-linux) (created on an Amazon Linux environment, presumably applicable to any platform supporting JetBrains Rider 2019.3.1+).
3. [A guided overview of the functionality of DotNetVault](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/DotNetVault%20Quick%20Start%20Functionality%20Tour%20%E2%80%93%20JetBrains%20Rider%20(Amazon%20Linux).md#dotnetvault-quick-start-functionality-tour--jetbrains-rider-201931-amazon-linux) along with a [test project](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x/DotNetVaultQuickStart) available on Github in both source and compiled code.  

### **Development Roadmap** 

#### *Release History*

#### *Version 0.2.5.18*

Non-beta release using version 1.0 of High Precision Timestamps.  Originally beta tested via version 0.1.1.0-beta in DotNetVault version 0.2.5.10-beta et seq.  Dependency on HpTimestamps changed from included dll to a dependency on package.

Also, added a new (minor) feature: the [ReportWhiteListLocationsAttribute], when applied to a struct or class, will emit a compiler warning giving you the path of the vaultsafewhitelist and the conditionally vault safe generic whitelist files.

#### *Version 0.2.5.x*

No major new features will be added to version 0.2.5.  Development will remain open in the [0.2.5 branch](https://github.com/cpsusie/DotNetVault/tree/v0.2.5.x) primarily for refinements, bug fixes and documentation updates.  Versions 0.2.5+ will continue to support .NET Framework 4.8, .NET Standard 2.0+ and .NET Core 3.1+ but will not make use of any features from the upcoming Version 5 of the unified DotNet framework.  If you are not upgrading your projects to .NET 5, continue to use releases numbered 0.2 but make no upgrade to any package versioned 0.3+.  

See **[DotNetVault Description.pdf](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVault%20Description.pdf)** which serves as the most complete design document for this project.