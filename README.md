DotNetVault
===========

Synchronization Library and Static Analysis Tool for C\# 8

DotNetVault takes its inspiration from the synchronization mechanisms provided
by Rust language and the Facebook Folly C++ synchronization library. These
synchronization mechanisms observe that the mutex should own the data they
protect. You literally cannot access the protected data without first obtaining
the lock. RAII destroys the lock when it goes out of scope – even if an
exception is thrown or early return taken.

**Advantages:**

​ **Deadlock avoidance**: by default, all locks are timed. If the resource has
already been obtained or you have accidentally changed the acquisition order of
various locks somewhere in the code, you get a *TimeoutException*, allowing you
to identify your mistake. In addition to being able to base termination of an
acquisition attempt on timeout, you can also use a cancellation token to
propagate the cancellation request.

​ **RAII (Scope-based) Lock Acquisition and Release:** Locks are stack-only
objects (ref structs) and the integrated Roslyn analyzer forces you to declare
the lock inline in a using statement or declaration, or it will cause a
compilation error. There is no danger of accidentally holding the lock open
longer than its scope even in the presence of an exception or early return.

**Incredible Flexibility to Change Underlying Synchronization Mechanism:**  
Vaults are provided that use varied underyling mechanisms (Monitor Locks, 
Atomic Exchanges, and ReaderWriterLockSlim).These vaults provide a common
compile time API for their common functionality.  Thus, you can easily change 
from a synchronization mechanism using Monitor.Enter (which is used by C#'s
lock statement) to a mechanism based on lock free atomics or even 
ReaderWriterLock.  This flexibility will allow you to profile code and 
make changes without needing to extensively refactor your code.

**Isolation of Protected Resources**: The need for programmer discipline is
reduced:  
		1. programmers do not need to remember which mutexes protect which resources, 
		2. programmers cannot access the protected resource before they obtain the 
		lock and cannot access any mutable state from the protected resource after 
		releasing the lock, and 
		3. static analysis rules enforced by compilation errors emitted from the
		integrated Roslyn analyzer prevent references to mutable state from outside the
		protected resource from becoming part of the protected resource and prevent the
		leaking of references to mutable state inside the protected resource to the
		outside.

The ubiquity of shared mutable state in Garbage Collected languages like C\# can
work at cross purposes to thread-safety. One approach to thread-safety in such
languages is to elimate the use of mutable state. Because this is not always
possible or even desireable, the synchronization mechanisms employed in C\#
typically rely on programmer knowledge and discipline. DotNetVault uses
Disposable Ref Structs together with custom language rules enforced by an
integrated Roslyn analyzer to prevent unsynchronized sharing of protected
resources. Locks cannot be held longer than their scope and, by default, will
timeout. This enables deadlock-avoidance.

Try DotNetVault. There is a learning curve because it is restrictive about
sharing protected resources. There are plenty of documents and example projects
provided with the source code of this project that can ease you into that
learning curve and demonstrate DotNetVault's suitability for use in highly
complex concurrent code. Armed with the resources that DotNetVault provides, you
will be able to approach concurrent programming, including use of shared mutable
state, with a high degree of confidence.

See **DotNetVault Description.pdf** for full description of this project.

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

RELEASE NOTES VERSION 0.2.1.9-alpha

~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
This release contains MAJOR feature updates but is still considered unstable alpha.

Major new feature: vaults with varying underlying synchronization mechanisms.  You may now chose lock=free atomics (only mechanism before), the .NET standard Monitor.Enter (used by C# lock statement) or ReaderWriterLockSlim.  Because these vaults have a compatible (at compile-time) API, you can easily switch between synchronization mechanisms without any extensive refactoring required.  Also, the new vault based on ReaderWriterLock slim allows for shared readonly locks, upgradable readonly locks and exclusive read-write locks.  If you are coming from an old version of this project, you may need to refactor in some places as their are a significant number of small breaking changes.  It should, however, be a quick and painless process.

Fixed Bug 76.  Illegal references to non-vault-safe types inside mutable vault's locked resource objects delegates where not being detected in the case of local functions or using anonymous function syntax. 

More unit tests.  There are now two unit test projects included.  The older one (DotNetVault.Test) tests the functionality of the built-in static analyzer.  The newer unit test project (VaultUnitTests) tests the functionality and synchronization mechanisms provided for the vaults.  It may also serve, in addition to the pre-existing sample code projects, as an introduction to this library.

Documentation (including "DotNetVault Description.pdf") updated to reflect changes.
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

RELEASE NOTES VERSION 0.2.0.2-alpha

~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
This is an unstable alpha release.  The current stable release is 0.1.5.2.  

The "Value" property of the BasicVault's locked resource object is now returned by reference.  This enables more efficient use of large mutable structs as protected resource objects.  An additional analysis rule was added to prevent ref local aliasing of the property, to prevent possible unsynchronized access.  Documentation updated to reflect.
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
