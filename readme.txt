Synchronization Library and Static Analysis Tool for C# 8

    DotNetVault is a library and static code analysis tool that makes managing shared mutable state in multi-threaded applications more manageable and less error prone.  It also provides a common abstraction over several commonly used synchronization mechanisms, allowing you to change from one underlying type to another (such as from lock free synchronization to mutex/monitor lock based) without needing to refactor your code. Where errors do still occur, they are easier to locate and identify.

    It provides an abstraction over several different underlying synchronization mechanisms: lock-free synchronization using atomics, Monitor.Enter (i.e. C# lock(syncObj) {}) and ReaderWriterLockSlim. Because the common functionality shared by vaults providing these varied underlying mechanism is exposed by a common (compile-time) API, it is easier than ever to experiment with changing the underlying mechanism without need for much code refactoring.  Simply change the type of vault you instantiate to one that uses the synchronization mechanism you desire.

    A full project description is included in "DotNetVault Description.pdf".  Source code, example projects, unit tests, stress test and quick start guide on GitHub.

RELEASE NOTES VERSION 0.2.2.12-beta:

    Fixed a Bug 92 where copying a protected resource into another ref-struct declared in a larger scope (of the same type or containing a field at any level of nesting in its graph) could result in unsynchronized access.  

    Fix was accomplished by the addition of more analyzer rules and attributes that can trigger them.

    Unit tests were added to validate the fix and code was added to the ExampleCodePlayground demonstrating Bug92.

    A full description of Bug 92, the new attributes and analyzer rules is available now in "DotNetVault Description.pdf".

RELEASE NOTES VERSION 0.2.2.1-beta:

    A BigValueListVault added, providing a vault protecting a list-like collection, especially suited for large value types. 

    Unit tests added, including a stress test called Cafe Babe game.  The Cafe Babe game is a unit test and stand-alone console-driven stress testing utility.

    "DotNetVault.Description.pdf" updated to reflect changes.

RELEASE NOTES VERSION 0.2.1.22-beta:

     This release adds a ReadWriteStringBuffer vault that provides thread-safe readonly, upgradable readonly and writable access to a StringBuilder object.  It also (when binaries or source retrieved from GitHub) includes the "Clorton Game" which demonstrates usage of the readwrite vault and provides a stress test to validate its functionality.

     "DotNetVault.Description.pdf" updated to reflect changes.
