using System;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;

namespace LaundryMachine.LaundryCode
{
    public sealed class LocklessConcreteType
    {
        public static implicit operator Type([NotNull] LocklessConcreteType convert) =>
            (convert ?? throw new ArgumentNullException(nameof(convert))).Value;

        [NotNull]
        public Type Value
        {
            get
            {
                Type ret = _type;
                if (ret == null)
                {
                    var type = _owner.GetType();
                    Interlocked.CompareExchange(ref _type, type, null);
                    ret = _type;
                }
                Debug.Assert(ret != null);
                return ret;
            }
        }

        public LocklessConcreteType([NotNull] object owner) =>
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));

        private volatile Type _type;
        [NotNull] private readonly object _owner;
    }
}