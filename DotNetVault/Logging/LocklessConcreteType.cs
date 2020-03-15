using System;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;

namespace DotNetVault.Logging
{
    sealed class LocklessConcreteType
    {
        [NotNull]
        public Type Value
        {
            get
            {
                Type ret = _type;
                if (ret == null)
                {
                    Interlocked.CompareExchange(ref _type, _owner.GetType(), null);
                    ret = _type;
                }
                Debug.Assert(ret != null);
                return ret;
            }
        }

        internal LocklessConcreteType([NotNull] object owner) =>
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));

        private volatile Type _type;
        [NotNull] private readonly object _owner;
    }
}
