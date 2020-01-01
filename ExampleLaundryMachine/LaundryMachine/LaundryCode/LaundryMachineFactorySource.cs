using System;
using JetBrains.Annotations;
using LaundryMachineFactory = System.Func<System.TimeSpan, System.TimeSpan, System.TimeSpan, 
    LaundryMachine.LaundryCode.ILaundryMachine>;
namespace LaundryMachine.LaundryCode
{
    public static class LaundryMachineFactorySource
    {
        public static LaundryMachineFactory FactoryInstance => TheLaundryMachineFactory.Value;

        public static bool SupplyAlternateLaundryMachineFactory([NotNull] LaundryMachineFactory alternate) =>
            TheLaundryMachineFactory.TrySetToAlternateValue(
                alternate ?? throw new ArgumentNullException(nameof(alternate)));

        static LaundryMachineFactorySource() => TheLaundryMachineFactory =
            new LocklessLazyWriteOnce<LaundryMachineFactory>(() => CreateLaundryMachine);

        private static ILaundryMachine CreateLaundryMachine(TimeSpan addOneDamp, TimeSpan removeOneDirt,
            TimeSpan removeOneDamp) => LaundryMachine.CreateLaundryMachine(addOneDamp, removeOneDirt, removeOneDamp);

        private static readonly LocklessLazyWriteOnce<LaundryMachineFactory> TheLaundryMachineFactory;
    }
}
