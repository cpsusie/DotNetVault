using System;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public class UnsupportedEnumTypeException : Exception
    {
        public UnsupportedEnumTypeException([NotNull] Type type) : base(
            GetMessage(type ?? throw new ArgumentNullException(nameof(type)))) {}

        static string GetMessage(Type type) => $"The type [{type.Name}] is not a supported enumeration backing type.";
    }
}