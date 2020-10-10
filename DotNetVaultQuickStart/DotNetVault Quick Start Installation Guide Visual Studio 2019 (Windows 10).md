# DotNetVault Quick Start Installation Guide Visual Studio 2019 (Windows 10)  
  
1. Open Visual Studio and Create a new .NET Core 3.1+ or .NET Framework 4.8 Console Application.  
2.  **If you chose .NET Framework 4.8**, open the .csproj file and add the following Line `<LangVersion>8.0</LangVersion>` under each “Platform/Config” PropertyGroup as shown in highlight below.  This is unnecessary if you chose a .NET Core 3.1+ based Console Application. 
  
     ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnet_vault_install_vs2019_win/pic_1.png?raw=true)

3. Right click on your project and chose “Manage NuGet packages”.  Click on the Browse option, select “Include Prerelease”, enter “cpsusie dotnetvault” into the search block, select DotNetVault, then click “Install”.  

     ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnet_vault_install_vs2019_win/pic_2.png?raw=true)
       
4. You are now set up to use DotNetVault in your project.  
  
5. Test Program Entry  
    * To ensure that the static analyzer installed correctly, delete the “Hello World” program entered in by default and replace it with the following code:  
      
     ```csharp
     using System;
     using System.Threading;
     using DotNetVault.Vaults;

     namespace DotNetVaultQuickStart
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
 
    * Change the Configuration to **Release** and Build the project:  
    
        ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnet_vault_install_vs2019_win/pic_3.png?raw=true)
          
    * *If you have installed everything correctly, the build should* **fail**.  If it builds, consult Visual Studio documentation for how to enable Roslyn Analyzers.  Do not attempt to use this library without static analysis enabled.  Assuming it does not build, you should see the following as a result of your build attempt:  
 
        ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnet_vault_install_vs2019_win/pic_4.png?raw=true)
        
    * The error is that you must **guard** the return value from a *Lock()* or *SpinLock()* method (or any other method whose return value you choose to annotate with the *UsingMandatory* attribute) with a using statement or declaration.  Failure to ensure that the lock is promptly released would cause a serious error in your program … it would timeout whenever in the future you attempted to obtain the lock.  
    * To fix the error, on line 16, change “var lck =…” to “using var lck =…” as shown then Build again.  This time, the build should succeed as shown:  
    
        ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnet_vault_install_vs2019_win/pic_5.png?raw=true)
          
    * Now you should be able to run the application as shown:  
    
        ![](https://github.com/cpsusie/DotNetVault/blob/v0.2.5.x/DotNetVaultQuickStart/dotnet_vault_install_vs2019_win/pic_6.png?raw=true)  
    
 6. Congratulations, you have successfully built an application using DotNetVault.  Next, we will look at a QuickStart application that shows the very basics of what can be done with this library and its integrated static analyzer.   
 