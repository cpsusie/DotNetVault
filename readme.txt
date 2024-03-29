Synchronization Library and Static Analysis Tool for C# 8

DotNetVault is a library and static code analysis tool that makes managing shared mutable state in multi-threaded applications more manageable and less error prone.  It also provides a common abstraction over several commonly used synchronization mechanisms, allowing you to change from one underlying type to another (such as from lock free synchronization to mutex/monitor lock based) without needing to refactor your code. Where errors do still occur, they are easier to locate and identify.
 
If you cannot read the README.md file locally, please use the following link to read it on github: https://github.com/cpsusie/DotNetVault/blob/master/README.md#dotnetvault.

The project description (a detailed design document) can be read here: https://github.com/cpsusie/DotNetVault/blob/master/DotNetVault_Description_v1.0.pdf.