DotNetVault is a library and static code analysis tool that makes managing shared mutable state in multi-threaded applications more manageable and less error prone. Where errors do still occur, they are easier to locate and identify.

A full project description is included in "DotNetVault Description.pdf".  

Release Notes:

Bug # 50 Fix: The assignment target of a method the return value of which has the UsingMandatoryAttribute now must be declared inline in the using statement.  Documentation updated to reflect.  Added metadata to NuGet package including references to the project's GitHub repository.  Unit test "TestOutOfLineDeclarationCausesDiagnostic" added to validate bug fix.