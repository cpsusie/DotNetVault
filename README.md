# DotNetVault
Synchronization Library and Static Analysis Tool for C# 8

DotNetVault takes its inspiration from the synchronization mechanisms provided by Rust language and the Facebook Folly C++ synchronization library. These synchronization mechanisms observe that the mutex should own the data they protect. You literally cannot access the protected data without first obtaining the lock. RAII destroys the lock when it goes out of scope – even if an exception is thrown or early return taken.

The ubiquity of shared mutable state in Garbage Collected languages like C# can work at cross purposes to thread-safety.  One approach to thread-safety in such languages is to elimate the use of mutable state.  Because this is not always possible or even desireable, the synchronization mechanisms employed in C# typically rely on programmer knowledge and discipline.  DotNetVault uses Disposable Ref Structs together with custom language rules enforced by an integrated Roslyn analyzer to prevent unsynchronized sharing of protected resources.  Locks cannot be held longer than their scope and, by default, will timeout.  This enables deadlock-avoidance.

Try DotNetVault. There is a learning curve because it is restrictive about sharing protected resources.  There are plenty of documents and example projects provided with the source code of this project that can ease you into that learning curve and demonstrate DotNetVault's suitability for use in highly complex concurrent code.  Armed with the resources that DotNetVault provides, you will be able to approach concurrent programming, including use of shared mutable state, with a high degree of confidence.

Advantages:

​	**Deadlock avoidance**: by default, all locks are timed.  If the resource has already been obtained or you have accidentally changed the acquisition order of various locks somewhere in the code, you get a *TimeoutException*, allowing you to identify your mistake.  In addition to being able to base termination of an acquisition attempt on timeout, you can also use a cancellation token to propagate the cancellation request.

​    **RAII (Scope-based) Lock Acquisition and Release:**  Locks are stack-only objects (ref structs) and the integrated Roslyn analyzer forces you to declare the lock inline in a using statement or declaration, or it will cause a compilation error.  There is no danger of accidentally holding the lock open longer than its scope even in the presence of an exception or early return.

   **Isolation of Protected Resources**:  The need for programmer discipline is reduced:	
    1. programmers do not need to remember which mutexes protect which resources,
    2. programmers cannot access the protected resource before they obtain the lock and cannot access any mutable state from the protected resource after releasing the lock,
    3. static analysis rules enforced by compilation errors emitted from the integrated Roslyn analyzer prevent references to mutable state from outside the protected resource from becoming part of the protected resource and prevent the leaking of references to mutable state inside the protected resource to the outside.

See **Pdf for full description of this project.**

**NOTE: Version 0.1.5.0 is released on the v0.1.5 branch.**  Further development on that branch will be dedicated solely to minor cosmetic updates, bug fixes and correction of clerical errors.  **Further feature development will continue on the master branch.**

RELEASE NOTES VERSION 0.1.5.0:

   This is the first release not explicitly marked beta or alpha.  This is currently a one-person project produced outside of work hours.  It is almost certainly not bug-free or without flaws, but it has been used extensively enough in the test projects to prove itself useful in managing shared mutable state in complex concurrent state machine scenarios.  I am confident that it will prove useful, despite any residual bugs and flaws.  You should not expect bug free or flawless conformance to specifications.  It will prove, however, far more useful than problematic.  Please report bugs or feature requests.

   Updated Project Description PDF.  Updated README.md.

RELEASE NOTES VERSION 0.1.4.2:

    Added quick start guide pdf for Linux (project available on GitHub)

    Upated quick start guide pdf for Windows (project available on GitHub)

RELEASE NOTES VERSION 0.1.4.1:

    Added quick start guide pdf and project (project available on GitHub).

    Updated readme.md

RELEASE NOTES VERSION 0.1.4.0:
    
    Bug# 61 FIXED.  Double dispose is now practically impossible.  Analyzer now forbids out of line, pre-declaration of a variable that will be the subject of a using statement or declaration.  Analysis rules now prevent manual calls to Dispose method and additional method and analysis rules were added to account for the two use-cases where manual release of protected resource is necessary.  These rules make it difficult to accidentally use the new manual release method accidentally.
    
    Bug# 62 FIXED.  Analysis now considers call of extension method using extension method syntax to be equivalent to a call thereto using static syntax.
    
    Bug# 48 Fields in base classes were not being considered in vault safety analysis.  An otherwise fine sealed class could be considered vault safe despite fields in a base clase violating vault-safety rules.
    
    Bug# 48 FIXED.  Analyzer now considers all fields from base classes when performing vault-safety analysis.  If a base class has field that, if present in sealed derived class being analyzed for vault-safety, would prevent the sealed derived class from being considered vault-safe, the sealed derived class will not be considered vault-safe.  A derived class, however, will not be considered not vault-safe MERELY because it inherits from a base class.  This change to the analyzer, does not, however, permit the base classes themselves to be used in a vault-safe context.
    
    Laundry machine simulation can go significantly more quickly now.  With parameters of 1, 2, and 3 milliseconds and 200 laundry articles, the test completes on my pc in less than two minutes.  It no longer sleeps during task simulation if the timespan parameters entered are small enough that the sleeping will cause the tasks to take much longer than the parameters specified.  
    
    A few convenience-based changes were made to the LaundryMachine simulation and the ConsoleStressTest.  Code in the ExampleCodePlayground, ConsoleStressTest and LaundryStessTest projects was updated to comply with new analysis rules as needed.
    
    The console stress test now outputs whether the time stamps it gathers are based on a high precision timer or not.
    
    Unit tests were added to the unit test project that validate the new analyis rules.
    
    The pdf documentation was edited and updated based on the foregoing changes.
    
    Xml Doc Comments for DotNetVault analyzer/library are now included in the NuGet package.

