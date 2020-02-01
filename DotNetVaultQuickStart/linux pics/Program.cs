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
                using var lck = strVault.SpinLock();
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
