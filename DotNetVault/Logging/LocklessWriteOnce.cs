using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using DotNetVault.Exceptions;
using JetBrains.Annotations;

[assembly: InternalsVisibleTo("VaultUnitTests")]

namespace DotNetVault.Logging
{
    internal sealed class WriteOnce<T>
    {
        public bool IsSet
        {
            get
            {
                lock (_syncObject)
                {
                    return _isSet;
                }
            }
        }

        public T Value
        {
            get
            {
                T ret;
                lock (_syncObject)
                {
                    if (!_isSet)
                    {
                        var del = _defaultInit;
                        if (del == null)
                        {
                            throw new InvalidOperationException("No default initializer and not yet set.");
                        }

                        try
                        {
                            _value = del();
                            _isSet = true;
                            _defaultInit = null;
                        }
                        catch (Exception e)
                        {
                            throw new DelegateThrewException<Func<T>>(del, nameof(_defaultInit), e);
                        }
                    }
                    Debug.Assert(_isSet);
                    ret = _value;
                }
                return ret;
            }
        }

        public WriteOnce([NotNull] Func<T> defaultInit)
        {
            _defaultInit = defaultInit ?? throw new ArgumentNullException(nameof(defaultInit));
        }

        public WriteOnce() => _defaultInit = null;

        public bool TrySet(T value)
        {
            lock (_syncObject)
            {
                if (_isSet)
                {
                    return false;
                }

                _value = value;
                _isSet = true;
                return true;
            }
        }

        private Func<T> _defaultInit;
        private T _value;
        private bool _isSet;
        private readonly object _syncObject = new object();
    }

    internal sealed class LocklessWriteOnce<T> where T : class
    {

        public static implicit operator T([NotNull] LocklessWriteOnce<T> getMyVal) =>
            (getMyVal ?? throw new ArgumentNullException(nameof(getMyVal))).Value;

        public T Value
        {
            get
            {
                T val = _field;
                if (val == null)
                {
                    T newVal;
                    try
                    {
                        newVal = _defaultFactory();
                        if (newVal == null)
                        {
                            throw new LocklessWriteOnceDefaultFactoryReturnedNullException<T>();
                        }
                    }
                    catch (LocklessWriteOnceDefaultFactoryReturnedNullException<T>)
                    {
                        throw;
                    }
                    catch (Exception inner)
                    {
                        throw new LocklessWriteOnceDefaultFactoryThrewException<T>(inner);
                    }

                    Debug.Assert(newVal != null);

                    T oldVal = Interlocked.CompareExchange(ref _field, newVal, null);
                    if (oldVal != null && newVal is IDisposable d)
                    {
                        try
                        {
                            d.Dispose();
                        }
                        catch (Exception e)
                        {
                            TraceLog.Log(e);
                        }
                    }

                    val = _field;
                    Debug.Assert(val != null);
                }
                return val;
            }
        }

        public LocklessWriteOnce([NotNull] Func<T> defaultGen) =>
            _defaultFactory = defaultGen ?? throw new ArgumentNullException(nameof(defaultGen));

        public bool SetToNonDefaultValue([NotNull] T val)
        {
            if (val == null) throw new ArgumentNullException(nameof(val));

            T oldVal = Interlocked.CompareExchange(ref _field, val, null);
            return oldVal == null;
        }

        private readonly Func<T> _defaultFactory;
        private volatile T _field;
    }

    internal abstract class LocklessWriteOnceExceptionBase : Exception
    {
        protected LocklessWriteOnceExceptionBase([CanBeNull] string message, [CanBeNull] Exception inner) : base(message, inner) { }
    }

    internal sealed class LocklessWriteOnceDefaultFactoryReturnedNullException<T> : LocklessWriteOnceExceptionBase where T : class
    {
        public LocklessWriteOnceDefaultFactoryReturnedNullException() : base(
            $"The {typeof(LocklessWriteOnce<T>).FullName} object's default value generator returned null.", null)
        {

        }
    }

    internal sealed class LocklessWriteOnceDefaultFactoryThrewException<T> : LocklessWriteOnceExceptionBase
        where T : class
    {
        public LocklessWriteOnceDefaultFactoryThrewException([NotNull] Exception inner) : base(CreateMessage(inner), inner)
        {

        }

        private static string CreateMessage([NotNull] Exception inner)
            => string.Format(DefaultMsgFormat, typeof(LocklessWriteOnce<T>).FullName,
                (inner ?? throw new ArgumentNullException(nameof(inner))).GetType().FullName, inner);

        private const string DefaultMsgFormat =
            "The {0} object's default value generating delegate threw an exception of type {1}.  Contents: [{2}].";
    }
}
