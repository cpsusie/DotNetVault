using System;
using System.Diagnostics.Contracts;
using DotNetVault.Attributes;
using DotNetVault.Vaults;

namespace ExampleCodePlayground
{
    [VaultSafe]
    internal struct MyMutableStruct
    { 
        public static MyMutableStruct CreateMutableStruct(DateTime ts, ulong count) => new MyMutableStruct(ts, count);
        public DateTime TimeStamp { get; }
        public ulong Count { get; set; }

        private MyMutableStruct(DateTime dt, ulong count)
        {
            TimeStamp = dt;
            Count = count;
        }
        public void IncrementCount() => Count = Count + 1;
        public override string ToString() =>
            $"[{typeof(MyMutableStruct).Name}] -- Time: [{TimeStamp:O}], Count: [{Count.ToString()}]";
        [Pure]  public MyMutableStruct WithNewTimeStamp(DateTime ts) => new MyMutableStruct(ts, Count);
        [Pure] public MyMutableStruct WithIncrementedCount() => new MyMutableStruct(TimeStamp, ++Count);

    }

    internal static class MutableStructCareExamples
    {
        internal static void ThisMightNotWorkTheWayYouThink()
        {
            using var lck = TheVault.SpinLock();
            Console.WriteLine(lck.Value.ToString());
            //This won't do what you might think it does.
            lck.Value.IncrementCount();
            Console.WriteLine(lck.Value);
            //This is the right way to do it
            lck.Value = lck.Value.WithIncrementedCount();
            Console.WriteLine(lck.Value);
        }
        private static readonly BasicVault<MyMutableStruct> TheVault = new BasicVault<MyMutableStruct>(MyMutableStruct.CreateMutableStruct(DateTime.Now, 0));
    }
}
