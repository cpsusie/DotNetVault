Synchronization Library and Static Analysis Tool for C# 8

See "DotNetVaultDescription.Pdf" for full description of this project.

RELEASE NOTES VERSION 0.2.1.9-alpha

    This release contains MAJOR feature updates but is still considered unstable alpha.

    Major new feature: vaults with varying underlying synchronization mechanisms.  You may now chose lock=free atomics (only mechanism before), the .NET standard Monitor.Enter (used by C# lock statement) or ReaderWriterLockSlim.  Because these vaults have a compatible (at compile-time) API, you can easily switch between synchronization mechanisms without any extensive refactoring required.  Also, the new vault based on ReaderWriterLock slim allows for shared readonly locks, upgradable readonly locks and exclusive read-write locks.  If you are coming from an old version of this project, you may need to refactor in some places as their are a significant number of small breaking changes.  It should, however, be a quick and painless process.
    
    Fixed Bug 76.  Illegal references to non-vault-safe types inside mutable vault's locked resource objects delegates where not being detected in the case of local functions or using anonymous function syntax. 

    More unit tests.  There are now two unit test projects included.  The older one (DotNetVault.Test) tests the functionality of the built-in static analyzer.  The newer unit test project (VaultUnitTests) tests the functionality and synchronization mechanisms provided for the vaults.  It may also serve, in addition to the pre-existing sample code projects, as an introduction to this library.

    Documentation (including "DotNetVault Description.pdf") updated to reflect changes.

RELEASE NOTES VERSION 0.2.0.2-alpha

    This is an unstable alpha release.  The current stable release is 0.1.5.2.  

    The "Value" property of the BasicVault's locked resource object is now returned by reference.  This enables more efficient use of large mutable structs as protected resource objects.  An additional analysis rule was added to prevent ref local aliasing of the property, to prevent possible unsynchronized access.  Documentation updated to reflect.