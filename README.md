DotNetVault
===========

Synchronization Library and Static Analysis Tool for C\# 8

See Pdf for full description of this project.

RELEASE NOTES:

VERSION 0.1.3.13:

>   Fixed two flaws in the ConsoleStressTest.

>   The default ordering comparer for stress test logic now considers ThreadId,
>   then Action Number, then TimeStamp, then Text.

>   It now takes linearithmic rather than quadratic time to process and validate
>   the results of the Console Stress test.

>   Added a table of know flaws and issues to the pdf documentation. Code
>   examples shown for these flaws now appear in the ExampleCodePlayground as
>   well.

VERSION 0.1.3.11:

>   Added Console Stress Test utility to allow stress test without access to WPF
>   and to provide simplified starting point for future quick start guide.
>   Updated "DotNetVault Description.pdf" so that the changes made in 1.3.8
>   release are reflected in the documentation.

VERSION 0.1.3.8:

>   Bug \# 50 Fix: The assignment target of a method the return value of which
>   has the UsingMandatoryAttribute now must be declared inline in the using
>   statement. Documentation updated to reflect. Added metadata to NuGet package
>   including references to the project's GitHub repository. Unit test
>   "TestOutOfLineDeclarationCausesDiagnostic" added to validate bug fix.
