﻿namespace Reyna
{
    using System.Threading;
    using Reyna.Interfaces;

    public class NamedWaitHandle : INamedWaitHandle
    {
        public NamedWaitHandle()
        {
        }

        public void Initialize(bool initialState, string name)
        {
            this.EventWaitHandle = new EventWaitHandle(initialState, EventResetMode.ManualReset, name);
        }

        private EventWaitHandle EventWaitHandle { get; set; }

        public bool Set()
        {
            return this.EventWaitHandle.Set();
        }

        public bool WaitOne()
        {
            return this.EventWaitHandle.WaitOne();
        }

        public bool Reset()
        {
            return this.EventWaitHandle.Reset();
        }
    }
}
