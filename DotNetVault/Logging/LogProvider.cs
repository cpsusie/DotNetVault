using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using System.Threading;
using JetBrains.Annotations;

namespace DotNetVault.Logging
{
    internal sealed class LogProvider : ILogProvider
    {
        internal static ILogProvider CreateInstance() => new LogProvider();

        internal static ILogProvider CreateInstance([NotNull] string fileName) =>
            new LogProvider(fileName ?? throw new ArgumentNullException(nameof(fileName)));

        public bool IsDisposed => _disposed.IsSet;

        public bool IsRunning => !IsDisposed && !_threadDone.IsSet && _started.IsSet;

        private LogProvider() : this(Assembly.GetExecutingAssembly().GetName().Name) { }

        private LogProvider([NotNull] string fileName)
        {
            _threadDone = new SetOnceFlag();
            _disposed = new SetOnceFlag();
            _starting = new SetOnceFlag();
            _started = new SetOnceFlag();
            _cts = new CancellationTokenSource();
            _logCollection = new BlockingCollection<LogAction>(new ConcurrentQueue<LogAction>());
            _starting.TrySet();
            string logPath = BuildLogPath(fileName);
            _fileAccessMutex = new Mutex(false, Guid);
            _fileName = new FileInfo(BuildLogPath(fileName));
            _thread = new Thread(ThreadLoop);
            _threadId = _thread.ManagedThreadId;
            _thread.Start(_cts.Token);
            while (!_started.IsSet)
            {
                Thread.Yield();
            }
        }

        static string BuildLogPath([NotNull] string basis)
        {
            string file= Path.ChangeExtension(string.Concat(StripEnd(basis), "_log"), ".txt");
            string dirPath = TheDirectoryInfo.Value.FullName;
            return dirPath.EndsWith("\\") ? dirPath + file : dirPath + "\\" + file;
        }

        static string StripEnd([NotNull] string basis) => basis.TrimEnd('\\');
        public void Log([NotNull] Exception ex) =>
            _logCollection.Add(new LogAction(ex ?? throw new ArgumentNullException(nameof(ex))));

        public void Log(string message) => _logCollection.Add(new LogAction(message));

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [SecurityCritical]
        ~LogProvider()
        {
            Dispose(false);
        }
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_threadDone.IsSet)
                {
                    _cts.Cancel();
                    while (!_threadDone.IsSet)
                    {
                        Thread.Yield();
                    }
                }

                IDisposable disposable = _logCollection;
                try
                {
                    disposable?.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }


                disposable = _cts;
                try
                {
                    disposable?.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }

            try
            {
                _fileAccessMutex?.Dispose();
            }
            catch (Exception e)
            {
                Console.Error.WriteLineAsync(e.ToString());
            }
        }

        private  void ThreadLoop(object tokenObj)
        {
            CancellationToken token = (CancellationToken) tokenObj;
            try
            {
                Debug.Assert(_starting.IsSet);
                if (!_started.TrySet())
                {
                    throw new InvalidOperationException("Cannot start thread more than once.");
                }

                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var action = _logCollection.Take(token);
                    WriteLogAction(action, token);
                }

            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                _threadDone.TrySet();
            }
        }

        private void WriteLogAction(in LogAction action, CancellationToken token)
        {
            bool wroteIt = false;
            const int maxRetries = 3;
            int tries = 0;
            Debug.Assert(Thread.CurrentThread.ManagedThreadId == _threadId);
            while (!wroteIt && tries <= maxRetries)
            {
                try
                {
                    ++tries;
                    token.ThrowIfCancellationRequested();
                    _fileAccessMutex.WaitOne();
                    try
                    {
                        using (var sw = _fileName.AppendText())
                        {
                            sw.WriteLine(action.ToString());
                            wroteIt = true;
                        }
                    }
                    finally
                    {
                        _fileAccessMutex.ReleaseMutex();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    wroteIt = false;
                    Thread.Yield();
                }
            }

        }

        private static DirectoryInfo GetDirectoryInfo()
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            folder = folder.EndsWith("\\")
                ? folder + LogFolder
                : folder + "\\" + LogFolder;
            var temp = new DirectoryInfo(folder);
            lock (DirectoryAccessSyncObject)
            {
                if (!temp.Exists)
                {
                    temp.Create();
                    temp.Refresh();
                    Debug.Assert(temp.Exists);
                }
            }
            return temp;
        }

       
        private const string Guid = ConfigurationDetails.IsDebugBuild
            ? "585a10ca-815d-449c-a3e9-b8ef0eb7baa7"
            // ReSharper disable once UnreachableCode
            : "01bd1e27-bf56-4db3-b4b1-a7d04c14026c";
        private readonly Mutex _fileAccessMutex;
        private const string TraceLogFolderName = "VaultAnalyzerTraceLogs";
        private const string DebugLogFolderName = "VaultAnalyzerDebugLogs";
        // ReSharper disable once UnreachableCode DEBUG OR RELEASE AFFECTS VALUE BUT IS COMPILE TIME CONSTANT
        private const string LogFolder = ConfigurationDetails.IsDebugBuild ? DebugLogFolderName : TraceLogFolderName;
        private static readonly object DirectoryAccessSyncObject = new object();
        private static readonly LocklessWriteOnce<DirectoryInfo> TheDirectoryInfo = new LocklessWriteOnce<DirectoryInfo>(GetDirectoryInfo);
        private readonly int _threadId;
        private readonly FileInfo _fileName;
        private readonly Thread _thread;
        private readonly SetOnceFlag _threadDone;
        private readonly SetOnceFlag _started;
        private readonly SetOnceFlag _starting;
        private readonly SetOnceFlag _disposed;
        private readonly BlockingCollection<LogAction> _logCollection;
        private readonly CancellationTokenSource _cts;
    }
}