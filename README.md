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
		releasing the lock,
		
		3. for read-write vaults, if a readonly-lock is obtained, its readonly nature 
		is enforced at compile time,
		
		4. static analysis rules enforced by compilation errors emitted from the
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


