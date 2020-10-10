# DotNetVault Quick Start Installation Guide – JetBrains Rider 2019.3.1 (Tested on Amazon Linux)


1. Make sure you have JetBrains Rider 2019.3.1 or later installed and have selected .NET Core 3.1+ as your framework.  

     ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_install_rider2019.3.1_linux/pic_1.png?raw=true)

2. Make sure Roslyn Analyzers are enabled.  See:  
https://www.jetbrains.com/help/rider/Settings_Roslyn_Analyzers.html 

3. Create a new .NET Core Console project as shown:   

   ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_install_rider2019.3.1_linux/pic_2.png?raw=true)

4.	Right click on your project and chose “Manage NuGet packages”.    
5.	As shown enter “DotNetVault” into the search bar in the NuGet Panel, select the latest version (0.2.5.x or later) of DotNetVault,  
then click the plus button.  Choose “Yes” in the confirmation dialog.  
 
    ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_install_rider2019.3.1_linux/pic_3.png?raw=true)

    ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_install_rider2019.3.1_linux/pic_4.png?raw=true)
  
6. If installation succeeded, you should see something like the below:  

    ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_install_rider2019.3.1_linux/pic_5.png?raw=true)  
  
7. Verify that Roslyn Analyzers are enabled.  
    * To ensure that the static analyzer installed correctly, delete the “Hello World” program entered in by default and replace it with the following code:  
    
        ```csharp
        using System;
        using System.Threading;
        using DotNetVault.Vaults;
        
        namespace LinuxDotNetVaultSetup
        {
            class Program
            {
                static void Main(string[] args)
                {
                    var strVault = new BasicVault<string>(string.Empty);
                   
                    Thread t1 = new Thread(() =>
                    {
                        Thread.SpinWait(50000);
                        var lck = strVault.SpinLock();
                        lck.Value += "Hello from thread 1, DotNetVault!  ";
                    });
                    Thread t2 = new Thread(() =>
                    {
                        using var lck = strVault.SpinLock();
                        lck.Value += "Hello from thread 2, DotNetVault!  ";
                    });
        
                    t1.Start();
                    t2.Start();          
                    t2.Join();
                    t1.Join();
        
                    string finalResult = strVault.CopyCurrentValue(TimeSpan.FromMilliseconds(100));
                    Console.WriteLine(finalResult);
                }      
            }
        }
        ```  
          
    * Click “Build” (or press Ctrl-Shift-B) as shown:  
    
        ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_install_rider2019.3.1_linux/pic_6.png?raw=true)
          
    * If you have properly installed the DotNetVault package and Roslyn is enabled, the project **should NOT build**.  If it builds, consult JetBrains documentation for how to enable Roslyn Analyzers.  Do not attempt to use this library without static analysis enabled.  Assuming it does not build, you should see the following as a result of your build attempt:  
    
        ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_install_rider2019.3.1_linux/pic_7.png?raw=true)  
        
    * The error is that you must guard the return value from a Lock() or SpinLock() method (or any other method whose return value you choose to annotate with the *UsingMandatory* attribute) with a using statement or declaration.  Failure to ensure that the lock is promptly released would cause a serious error in your program … it would timeout whenever in the future you attempted to obtain the lock.  
      
    * To fix the error, on line 16, change “var lck =…” to “using var lck =…” as shown then Build again.  This time, the build should succeed as shown:  
      
        ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnetvault_install_rider2019.3.1_linux/pic_8.png?raw=true)
      
    * Now, you should be able to run the application as shown:  
      
      > [CORP\cpsusie@a-3fz49v9t6b1rq netcoreapp3.1]$ *pwd*  
**/home/cpsusie/RiderProjects/LinuxDotNetVaultSetup/LinuxDotNetVaultSetup/bin/Release/netcoreapp3.1**  
[CORP\cpsusie@a-3fz49v9t6b1rq netcoreapp3.1]$ *ls*  
**DotNetVault.dll            LinuxDotNetVaultSetup            LinuxDotNetVaultSetup.dll  LinuxDotNetVaultSetup.runtimeconfig.dev.json  
JetBrains.Annotations.dll  LinuxDotNetVaultSetup.deps.json  LinuxDotNetVaultSetup.pdb  LinuxDotNetVaultSetup.runtimeconfig.json**  
[CORP\cpsusie@a-3fz49v9t6b1rq netcoreapp3.1]$ *dotnet LinuxDotNetVaultSetup.dll*  
**Hello from thread 1, DotNetVault!  Hello from thread 2, DotNetVault!**    
[CORP\cpsusie@a-3fz49v9t6b1rq netcoreapp3.1]$   
  
    * Congratuations, you have successfully setup a project to use the DotNetVault library and static analyzer.  As a next step, consider the *Functionality Tour* to jump right into using this library.  For more detailed information, you may consult the latest version of *[Project Description.pdf](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVault%20Description.pdf)*.