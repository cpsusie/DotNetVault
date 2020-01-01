using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DotNetVault.LockedResources;

namespace DotNetVault.Test.TestCases
{
    public static class DataFlowTestCases
    {
        public static void TestDataFlow1()
        {
            Func<string, int> myHelper = (string s) =>
            {
                bool? wasEven;
                if (TheStringBuilder.Value.Length < 1)
                {
                    wasEven = null;
                    TheStringBuilder.Value.AppendLine("Clorton, voltron, Ganymede!");
                }
                else if (TheStringBuilder.Value.Length % 2 == 0)
                {
                    TheStringBuilder.Value.Remove(TheStringBuilder.Value.Length - 1, 1);
                    TheStringBuilder.Value.AppendLine("Dickity rickity clickity!");
                    wasEven = true;
                }
                else
                {
                    TheStringBuilder.Value.Remove(TheStringBuilder.Value.Length - 2, 1);
                    TheStringBuilder.Value.AppendLine("Oh no you don't!");
                    wasEven = false;
                }

                TheStringBuilder.Value.Append(s ?? "You sunavabitch, the string is null!");

                switch (wasEven)
                {
                    case true:
                        if (TheStringBuilder.Value.Length % 2 == 0)
                            TheStringBuilder.Value.Append("?");
                        break;
                    case false:
                        if (TheStringBuilder.Value.Length % 2 != 0)
                            TheStringBuilder.Value.Remove(TheStringBuilder.Value.Length - 1, 1);
                        break;
                }

                return TheStringBuilder.Value.Length * TheStringBuilder.Value.Length;
            };

            List<int> myList = new List<int>();
            myList.Add(myHelper("Eat shit asshole!"));
            myList.Add(myHelper("Ok just kidding I really love you."));
            myList.Add(myHelper("But not in a homo kind of way bro."));

            Console.WriteLine(myList.Sum());
            Console.WriteLine(TheStringBuilder.Value);
        }


        private static readonly ThreadLocal<StringBuilder> TheStringBuilder = new ThreadLocal<StringBuilder>(() => new StringBuilder());
    }
}
