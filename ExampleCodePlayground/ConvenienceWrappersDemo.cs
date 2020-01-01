using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DotNetVault.Vaults;
using DotNetVault.VsWrappers;

namespace ExampleCodePlayground
{
    static class ConvenienceWrappersDemo
    {
        public static void ShowWrapperUsage()
        {
            Console.WriteLine("Begin showing wrapper usage.");
            MutableResourceVault<List<int>> vault =
                MutableResourceVault<List<int>>.CreateMutableResourceVault(() => 
                        new List<int> { 1, 2, 3, 4 },
                    TimeSpan.FromMilliseconds(250));
            ImmutableArray<int> finalContents;
            {
                using var lck = 
                    vault.SpinLock();
                List<int> addUs = new List<int> {5, 6, 7, 8};
                //ERROR DotNetVault_VsDelegateCapture cannot capute non-vault
                //safe param addUs, of type List, not vault-safe	
                //lck.ExecuteAction((ref List<int> res) => res.AddRange(addUs));

                //Ok reference is to thin readonly wrapper around list of vault-safe type.
                //state cannot be commingled in the delegate.
                VsListWrapper<int> wrapper = VsListWrapper<int>.FromList(addUs);
                lck.ExecuteAction((ref List<int> res) => res.AddRange(wrapper));
                finalContents = lck.ExecuteQuery((in List<int> res) => 
                    res.ToImmutableArray());
            }
            Console.WriteLine("Printing final contents: ");
            StringBuilder sb = new StringBuilder("{");
            foreach (var i in finalContents)
            {
                sb.Append($"{i}, ");
            }

            if (sb[^1] == ' ' && sb[^2] == ',')
            {
                sb.Remove(sb.Length - 2, 2);
            }

            sb.Append("}");
            Console.WriteLine(sb.ToString());

            Console.WriteLine("Done showing wrapper usage.");
            Console.WriteLine();
        }
        


    }
}
