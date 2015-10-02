﻿namespace Reyna
{
    using System;
    using Reyna.Interfaces;

    internal sealed class StoreService : ServiceBase, IStoreService
    {
        public StoreService( IAutoResetEventAdapter waitHandle) : base(waitHandle, false)
        {
        }

        private IRepository TargetStore { get; set; }

        protected override void ThreadStart()
        {
            while (!this.Terminate)
            {
                this.WaitHandle.WaitOne();
                IMessage message = null;

                while ((message = this.SourceStore.Get()) != null)
                {
                    long storageSizeLimit = new Preferences().StorageSizeLimit;
                    if (storageSizeLimit == -1)
                    {
                        this.TargetStore.Add(message);
                    }
                    else
                    {
                        this.TargetStore.Add(message, storageSizeLimit);
                    }
                    
                    this.SourceStore.Remove();
                }

                this.WaitHandle.Reset();
            }
        }

        public void Initialize(IRepository sourceStore, IRepository targetStore)
        {
            if (targetStore == null)
            {
                throw new ArgumentNullException("targetStore");
            }

            this.TargetStore = targetStore;
            this.TargetStore.Initialise();
            base.Initialize(sourceStore);
        }
    }
}
