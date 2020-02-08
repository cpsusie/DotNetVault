# DotNetVault
Synchronization Library and Static Analysis Tool for C# 8

DotNetVault takes its inspiration from the synchronization mechanisms provided by Rust language and the Facebook Folly C++ synchronization library. These synchronization mechanisms observe that the mutex should own the data they protect. You literally cannot access the protected data without first obtaining the lock. RAII destroys the lock when it goes out of scope – even if an exception is thrown or early return taken.

**Advantages:**

​	**Deadlock avoidance**: by default, all locks are timed.  If the resource has already been obtained or you have accidentally changed the acquisition order of various locks somewhere in the code, you get a *TimeoutException*, allowing you to identify your mistake.  In addition to being able to base termination of an acquisition attempt on timeout, you can also use a cancellation token to propagate the cancellation request.

​    **RAII (Scope-based) Lock Acquisition and Release:**  Locks are stack-only objects (ref structs) and the integrated Roslyn analyzer forces you to declare the lock inline in a using statement or declaration, or it will cause a compilation error.  There is no danger of accidentally holding the lock open longer than its scope even in the presence of an exception or early return.

   **Isolation of Protected Resources**:  The need for programmer discipline is reduced:	
    1. programmers do not need to remember which mutexes protect which resources,
    2. programmers cannot access the protected resource before they obtain the lock and cannot access any mutable state from the protected resource after releasing the lock,
    3. static analysis rules enforced by compilation errors emitted from the integrated Roslyn analyzer prevent references to mutable state from outside the protected resource from becoming part of the protected resource and prevent the leaking of references to mutable state inside the protected resource to the outside.

The ubiquity of shared mutable state in Garbage Collected languages like C# can work at cross purposes to thread-safety.  One approach to thread-safety in such languages is to elimate the use of mutable state.  Because this is not always possible or even desireable, the synchronization mechanisms employed in C# typically rely on programmer knowledge and discipline.  DotNetVault uses Disposable Ref Structs together with custom language rules enforced by an integrated Roslyn analyzer to prevent unsynchronized sharing of protected resources.  Locks cannot be held longer than their scope and, by default, will timeout.  This enables deadlock-avoidance.

Try DotNetVault. There is a learning curve because it is restrictive about sharing protected resources.  There are plenty of documents and example projects provided with the source code of this project that can ease you into that learning curve and demonstrate DotNetVault's suitability for use in highly complex concurrent code.  Armed with the resources that DotNetVault provides, you will be able to approach concurrent programming, including use of shared mutable state, with a high degree of confidence.

See **DotNetVault Description version 0.1.5.2.pdf** for full description of this project.

RELEASE NOTES VERSION 0.1.5.2:

    Fixed Bug 64.  Structs with fields containing immutable reference types as fields were being incorrectly identified as not being vault-safe when those fields were not read-only.  Since structs are value types and the type field is immutable, there is no danger of a data race when one retains a copy of such a protected resource after releasing a lock.  The analyzer was fixed to account for this.  Unit tests were added to confirm the fix and detect future regressions on this issue.  The Project description was updated to reflect this fix and explain Bug 64.

RELEASE NOTES VERSION 0.1.5.0:

    This is the first release not explicitly marked beta or alpha.  This is currently a one-person project produced outside of work hours.  It is almost certainly not bug-free or without flaws, but it has been used extensively enough in the test projects to prove itself useful in managing shared mutable state in complex concurrent state machine scenarios.  I am confident that it will prove useful, despite any residual bugs and flaws.  You should not expect bug free or flawless conformance to specifications.  It will prove, however, far more useful than problematic.  Please report bugs or feature requests.

    Updated Project Description PDF.  Updated README.md.


