Synchronization Library and Static Analysis Tool for C# 8

    DotNetVault is a library and static code analysis tool that makes managing shared mutable state in multi-threaded applications more manageable and less error prone.  It also provides a common abstraction over several commonly used synchronization mechanisms, allowing you to change from one underlying type to another (such as from lock free synchronization to mutex/monitor lock based) without needing to refactor your code. Where errors do still occur, they are easier to locate and identify.

    It provides an abstraction over several different underlying synchronization mechanisms: lock-free synchronization using atomics, Monitor.Enter (i.e. C# lock(syncObj) {}) and ReaderWriterLockSlim. Because the common functionality shared by vaults providing these varied underlying mechanism is exposed by a common (compile-time) API, it is easier than ever to experiment with changing the underlying mechanism without need for much code refactoring.  Simply change the type of vault you instantiate to one that uses the synchronization mechanism you desire.

    A full project description is included in "DotNetVault Description.pdf".  Source code, example projects, unit tests, stress test and quick start guide on GitHub.

RELEASE NOTES VERSION 0.2.2.1-beta:

    A BigValueListVault added, providing a vault protecting a list-like collection, especially suited for large value types. 

    Unit tests added, including a stress test called Cafe Babe game.  The Cafe Babe game is a unit test and stand-alone console-driven stress testing utility.

    "DotNetVault.Description.pdf" updated to reflect changes.

RELEASE NOTES VERSION 0.2.1.22-beta

     This release adds a ReadWriteStringBuffer vault that provides thread-safe readonly, upgradable readonly and writable access to a StringBuilder object.  It also (when binaries or source retrieved from GitHub) includes the "Clorton Game" which demonstrates usage of the readwrite vault and provides a stress test to validate its functionality.

     "DotNetVault.Description.pdf" updated to reflect changes.

RELEASE NOTES VERSION 0.2.1.9-alpha 

    This release contains MAJOR feature updates but is still considered unstable alpha.

    Major new feature: vaults with varying underlying synchronization mechanisms.  You may now chose lock=free atomics (only mechanism before), the .NET standard Monitor.Enter (used by C# lock statement) or ReaderWriterLockSlim.  Because these vaults have a compatible (at compile-time) API, you can easily switch between synchronization mechanisms without any extensive refactoring required.  Also, the new vault based on ReaderWriterLock slim allows for shared readonly locks, upgradable readonly locks and exclusive read-write locks.  If you are coming from an old version of this project, you may need to refactor in some places as their are a significant number of small breaking changes.  It should, however, be a quick and painless process.
    
    Fixed Bug 76.  Illegal references to non-vault-safe types inside mutable vault's locked resource objects delegates where not being detected in the case of local functions or using anonymous function syntax. 

    Fixed Bug 64.  Structs with fields containing immutable reference types as fields were being incorrectly identified as not being vault-safe when those fields were not read-only.  Since structs are value types and the type field is immutable, there is no danger of a data race when one retains a copy of such a protected resource after releasing a lock.  The analyzer was fixed to account for this.  Unit tests were added to confirm the fix and detect future regressions on this issue.  The Project description was updated to reflect this fix and explain Bug 64.

    More unit tests.  There are now two unit test projects included.  The older one (DotNetVault.Test) tests the functionality of the built-in static analyzer.  The newer unit test project (VaultUnitTests) tests the functionality and synchronization mechanisms provided for the vaults.  It may also serve, in addition to the pre-existing sample code projects, as an introduction to this library.

    Documentation (including "DotNetVault Description.pdf") updated to reflect changes.