using System;
using DotNetVault.Attributes;
using DotNetVault.Vaults;
using JetBrains.Annotations;

namespace ExampleCodePlayground
{
    [VaultSafe]
    internal struct MyMutableStruct
    {
        public static MyMutableStruct CreateMutableStruct(DateTime ts, ulong count, [NotNull] string text) =>
            new MyMutableStruct(ts, count, text);

        public DateTime TimeStamp { get; }
        public ulong Count { get; set; }

        [NotNull]
        public string Text
        {
            get => _text ??= string.Empty;
            set => _text = value ?? throw new ArgumentNullException(nameof(value));
        }

        private MyMutableStruct(DateTime dt, ulong count, [NotNull] string text)
        {
            TimeStamp = dt;
            Count = count;
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public override string ToString() =>
            $"[{typeof(MyMutableStruct).Name}] -- Time: [{TimeStamp:O}], Count: [{Count.ToString()}], Text: [{Text}].";

        private string _text;
    }

    internal static class MutableStructCareExamples
    {
        internal static void LargeMutableStructsAreNowEfficientProtectedResourcesWithReturnByReference()
        {
            using var lck = TheVault.SpinLock();
            Console.WriteLine($"Printing protected resource:\t\t{lck.Value.ToString()}");
            var copy = lck.Value;
            Console.WriteLine($"Printing copy of protected resource:\t\t{copy.ToString()}");
            Console.WriteLine("Incrementing protected resources count and appending text.");
            lck.Value.Count += 1;
            lck.Value.Text += " Accessing large structs by reference is efficient!";
            Console.WriteLine(lck.Value);
            Console.WriteLine($"Printing protected resource:\t\t{lck.Value.ToString()}");
            Console.WriteLine($"Printing unaffected copy of protected resource:\t\t{copy.ToString()}");
            Console.WriteLine(
                "Going to change the value of the copy so count is 12 and text is `in lecto sunt tuo!'.  This will not affect protected resource.");
            copy.Text = "in lecto sunt tuo!";
            copy.Count = 12;
            Console.WriteLine($"Printing protected resource:\t\t{lck.Value.ToString()}");
            Console.WriteLine($"Printing independently mutated copy of protected resource:\t\t{copy.ToString()}");
            Console.WriteLine(
                "We can, of course, work on the copy and then overwrite the protected resource with it.  " +
                "For large structs, it is probably best to just work through the .Value property.");
            lck.Value = copy;
            Console.WriteLine("now protected resource and copy will once again be the same...");
            Console.WriteLine($"Printing protected resource:\t\t{lck.Value.ToString()}");
            Console.WriteLine($"Printing copy of protected resource:\t\t{copy.ToString()}");

            Console.WriteLine(
                $"{nameof(LargeMutableStructsAreNowEfficientProtectedResourcesWithReturnByReference)} functions properly.");
        }

        internal static void DemonstrateNeedForRuleAgainstLocalRefAlias()
        {
            Console.WriteLine($"Starting {nameof(DemonstrateNeedForRuleAgainstLocalRefAlias)}.");
            try
            {
                MyMutableStruct value = MyMutableStruct.CreateMutableStruct(DateTime.Now, 42, "Hello there!");
                ref MyMutableStruct alias = ref value;
                {
                    using var lck = TheVault.SpinLock();
                    //following line will not compile because it may permit unsynchronized access after lck's
                    //lifetime ends.  If you want to access by reference, simply use lck.Value and changes will propagate
                    //efficiently.
                    //alias = ref lck.Value;
                }
                //The following line will result in unsynchronized access to mutable resource.
                //for this reason, aliasing the protected resource into a ref local is forbidden.
                alias.Count *= 42;
                Console.WriteLine(TheVault.CopyCurrentValue(TimeSpan.FromMilliseconds(250)));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"{nameof(DemonstrateNeedForRuleAgainstLocalRefAlias)} FAILED.  Exception: [{ex}].");
                throw;
            }

        }

        private static readonly BasicVault<MyMutableStruct> TheVault =
            new BasicVault<MyMutableStruct>(
                MyMutableStruct.CreateMutableStruct(DateTime.Now, 0, "Hello, DotNetVault!"));
    }
}
