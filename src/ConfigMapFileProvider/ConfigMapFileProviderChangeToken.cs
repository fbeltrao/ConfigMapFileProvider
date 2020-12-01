using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Timer = System.Threading.Timer;

namespace Microsoft.Extensions.Configuration
{
    public sealed class ConfigMapFileProviderChangeToken : IChangeToken, IDisposable
    {
        class CallbackRegistration : IDisposable
        {
            Action<object> callback;
            object state;
            Action<CallbackRegistration> unregister;


            public CallbackRegistration(Action<object> callback, object state, Action<CallbackRegistration> unregister)
            {
                this.callback = callback;
                this.state = state;
                this.unregister = unregister;
            }

            public void Notify()
            {
                var localState = this.state;
                var localCallback = this.callback;
                if (localCallback != null)
                {
                    localCallback.Invoke(localState);
                }
            }


            public void Dispose()
            {
                var localUnregister = Interlocked.Exchange(ref unregister, null);
                if (localUnregister != null)
                {
                    localUnregister(this);
                    this.callback = null;
                    this.state = null;
                }
            }
        }

        List<CallbackRegistration> registeredCallbacks;
        private readonly string rootPath;
        private string filter;
        private readonly int detectChangeIntervalMs;
        private Timer timer;
        private bool hasChanged;
        private string lastChecksum;
        object timerLock = new object();

        public ConfigMapFileProviderChangeToken(string rootPath, string filter, int detectChangeIntervalMs = 30_000)
        {
            Console.WriteLine($"new {nameof(ConfigMapFileProviderChangeToken)} for {filter}");
            registeredCallbacks = new List<CallbackRegistration>();
            this.rootPath = rootPath;
            this.filter = filter;
            this.detectChangeIntervalMs = detectChangeIntervalMs;
        }

        internal void EnsureStarted()
        {
            lock (timerLock)
            {
                if (timer == null)
                {
                    var fullPath = Path.Combine(rootPath, filter);
                    if (File.Exists(fullPath))
                    {
                        this.timer = new Timer(CheckForChanges);
                        this.timer.Change(0, detectChangeIntervalMs);
                    }
                }
            }
        }

        private void CheckForChanges(object state)
        {
            var fullPath = Path.Combine(rootPath, filter);

            Console.WriteLine($"Checking for changes in {fullPath}");

            var newCheckSum = GetFileChecksum(fullPath);
            var newHasChangesValue = false;
            if (this.lastChecksum != null && this.lastChecksum != newCheckSum)
            {
                Console.WriteLine($"File {fullPath} was modified!");

                // changed
                NotifyChanges();

                newHasChangesValue = true;
            }

            this.hasChanged = newHasChangesValue;

            this.lastChecksum = newCheckSum;
            
        }

        private void NotifyChanges()
        {
            var localRegisteredCallbacks = registeredCallbacks;
            if (localRegisteredCallbacks != null)
            {
                var count = localRegisteredCallbacks.Count;
                for (int i = 0; i < count; i++)
                {
                    localRegisteredCallbacks[i].Notify();
                }
            }
        }

        string GetFileChecksum(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream));
                }
            }
        }

        public bool HasChanged => this.hasChanged;

        public bool ActiveChangeCallbacks => true;

        public IDisposable RegisterChangeCallback(Action<object> callback, object state)
        {
            var localRegisteredCallbacks = registeredCallbacks;
            if (localRegisteredCallbacks == null)
                throw new ObjectDisposedException(nameof(registeredCallbacks));

            var cbRegistration = new CallbackRegistration(callback, state, (cb) => localRegisteredCallbacks.Remove(cb));
            localRegisteredCallbacks.Add(cbRegistration);

            return cbRegistration;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref registeredCallbacks, null);

            Timer localTimer = null;
            lock (timerLock)
            {
                localTimer = Interlocked.Exchange(ref timer, null);
            }

            if (localTimer != null)
            {
                localTimer.Dispose();
            }
        }
    }
}