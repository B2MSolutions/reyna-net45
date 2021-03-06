﻿namespace Reyna.Facts
{
    using System;
    using System.Threading;
    using Moq;
    using Interfaces;
    using Xunit;
    using System.Collections.Generic;

    public class GivenAForwardService
    {
        private readonly ForwardService _service;
        private readonly Mock<IAutoResetEventAdapter> _waitHandle;
        private readonly Mock<IHttpClient> _httpClient;
        private readonly Mock<INetworkStateService> _networkStateService;
        private readonly Mock<IRepository> _persistentStore;
        private readonly Mock<IBatchConfiguration> _batchConfiguration;
        private readonly Mock<IPeriodicBackoutCheck> _periodicBackoutCheck;

        public GivenAForwardService()
        {
            _waitHandle = new Mock<IAutoResetEventAdapter>();
            _httpClient = new Mock<IHttpClient>();
            _networkStateService = new Mock<INetworkStateService>();
            _persistentStore = new Mock<IRepository>();
            _batchConfiguration = new Mock<IBatchConfiguration>();
            _periodicBackoutCheck = new Mock<IPeriodicBackoutCheck>();
            var loggerMock = new Mock<IReynaLogger>();

            _periodicBackoutCheck.Setup(pbc => pbc.IsTimeElapsed("ForwardService", It.IsAny<long>())).Returns(true);

            _service = new ForwardService(_waitHandle.Object, loggerMock.Object, _batchConfiguration.Object, _periodicBackoutCheck.Object);
        }

        [Fact]
        public void WhenCallingInitialiseShouldInitialiseClassCorrectly()
        {
            _service.Initialize(_persistentStore.Object, _httpClient.Object, _networkStateService.Object, 10000, 5000, false);

            Assert.Equal(_persistentStore.Object, _service.SourceStore);
            Assert.Equal(_httpClient.Object, _service.HttpClient);
            Assert.Equal(_networkStateService.Object, _service.NetworkState);
            Assert.Equal(5000, _service.SleepMilliseconds);
            Assert.Equal(10000, _service.TemporaryErrorMilliseconds);
            Assert.IsType<MessageProvider>(_service.MessageProvider);
        }

        [Fact]
        public void WhenCallingInitialiseShouldInitialiseClassCorrectlyWhenUsingBatchUploading()
        {
            _service.Initialize(_persistentStore.Object, _httpClient.Object, _networkStateService.Object, 10000, 5000, true);

            Assert.Equal(_persistentStore.Object, _service.SourceStore);
            Assert.Equal(_httpClient.Object, _service.HttpClient);
            Assert.Equal(_networkStateService.Object, _service.NetworkState);
            Assert.Equal(5000, _service.SleepMilliseconds);
            Assert.Equal(10000, _service.TemporaryErrorMilliseconds);
            Assert.IsType<BatchProvider>(_service.MessageProvider);
        }

        [Fact]
        public void WhenCallingInitialiseWithNullHttpClientShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => _service.Initialize(_persistentStore.Object, null, _networkStateService.Object, 10000, 5000, false));
        }

        [Fact]
        public void WhenCallingInitialiseWithNullNullNetworkStateServiceShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => _service.Initialize(_persistentStore.Object, _httpClient.Object, null, 10000, 5000, false));
        }

        [Fact]
        public void WhenCallingInitialiseWithNullNullSourceStoreShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => _service.Initialize(null, _httpClient.Object, _networkStateService.Object, 10000, 5000, false));
        }

        [Fact]
        public void WhenCallingStartShouldCallWaitHandle()
        {
            _service.Initialize(_persistentStore.Object, _httpClient.Object, _networkStateService.Object, 10000, 5000, false);

            _service.Start();
            Thread.Sleep(50);
            _service.Stop();

            _waitHandle.Verify(w => w.WaitOne(), Times.AtLeastOnce());
            _waitHandle.Verify(w => w.Reset(), Times.AtLeastOnce());
        }

        [Fact]
        public void WhenCallingStartAndThereIsAMessageShouldCallSendOnHttpClientAndRemoveTheMessage()
        {
            _service.Initialize(_persistentStore.Object, _httpClient.Object, _networkStateService.Object, 10000, 5000, false);
            Message message = new Message(new Uri("http://google.com"), "MessageBody");
            List<IMessage> messages = new List<IMessage> { message };

            var messageProvider = new Mock<IMessageProvider>();
            messageProvider.Setup(mp => mp.CanSend).Returns(true);
            messageProvider.SetupSequence(mp => mp.GetNext()).Returns(message).Returns(null);
            _service.MessageProvider = messageProvider.Object;

            _service.DoWork();

            _httpClient.Verify(h => h.Post(message), Times.Exactly(1));
            messageProvider.Verify(mp => mp.Delete(message));
        }

        [Fact]
        public void WhenCallingDoWorkAndHttpClientReturnsTemporaryErrorShouldNotRemoveMessage()
        {
            _service.Initialize(_persistentStore.Object, _httpClient.Object, _networkStateService.Object, 1, 1, false);
            Message message = new Message(new Uri("http://google.com"), "MessageBody");
            List<IMessage> messages = new List<IMessage> { message };

            var messageProvider = new Mock<IMessageProvider>();
            messageProvider.Setup(mp => mp.CanSend).Returns(true);
            messageProvider.SetupSequence(mp => mp.GetNext()).Returns(message).Returns(null);
            _service.MessageProvider = messageProvider.Object;

            _persistentStore.Setup(v => v.Get()).Returns(() => GetMessage(messages));
            _persistentStore.Setup(v => v.Remove()).Callback(() => RemoveMessage(messages, message));
            _httpClient.Setup(h => h.Post(It.IsAny<IMessage>())).Returns(Result.TemporaryError);

            _service.DoWork();

            _httpClient.Verify(h => h.Post(message), Times.Exactly(1));
            _waitHandle.Verify(w => w.Reset(), Times.Exactly(1));
            _persistentStore.Verify(p => p.Remove(), Times.Never);
            messageProvider.Verify(mp => mp.Delete(message), Times.Never);
        }

        [Fact]
        public void WhenCallingDoWorkAndHttpClientReturnsblackoutShouldNotRemoveMessage()
        {
            _service.Initialize(_persistentStore.Object, _httpClient.Object, _networkStateService.Object, 0, 0, false);
            Message message = new Message(new Uri("http://google.com"), "MessageBody");
            List<IMessage> messages = new List<IMessage> { message };

            var messageProvider = new Mock<IMessageProvider>();
            messageProvider.Setup(mp => mp.CanSend).Returns(true);
            messageProvider.SetupSequence(mp => mp.GetNext()).Returns(message).Returns(null);
            _service.MessageProvider = messageProvider.Object;

            _persistentStore.Setup(v => v.Get()).Returns(() => GetMessage(messages));
            _persistentStore.Setup(v => v.Remove()).Callback(() => RemoveMessage(messages, message));
            _httpClient.Setup(h => h.Post(It.IsAny<IMessage>())).Returns(Result.Blackout);

            _service.DoWork();

            _httpClient.Verify(h => h.Post(message), Times.Exactly(1));
            _waitHandle.Verify(w => w.Reset(), Times.Exactly(1));
            _persistentStore.Verify(p => p.Remove(), Times.Never);
            messageProvider.Verify(mp => mp.Delete(message), Times.Never);
        }

        [Fact]
        public void WhenCallingDoWorkAndHttpClientReturnsNotConnectedShouldNotRemoveMessage()
        {
            _service.Initialize(_persistentStore.Object, _httpClient.Object, _networkStateService.Object, 0, 0, false);
            Message message = new Message(new Uri("http://google.com"), "MessageBody");
            List<IMessage> messages = new List<IMessage> { message };

            var messageProvider = new Mock<IMessageProvider>();
            messageProvider.Setup(mp => mp.CanSend).Returns(true);
            messageProvider.SetupSequence(mp => mp.GetNext()).Returns(message).Returns(null);
            _service.MessageProvider = messageProvider.Object;

            _persistentStore.Setup(v => v.Get()).Returns(() => GetMessage(messages));
            _persistentStore.Setup(v => v.Remove()).Callback(() => RemoveMessage(messages, message));
            _httpClient.Setup(h => h.Post(It.IsAny<IMessage>())).Returns(Result.NotConnected);

            _service.DoWork();

            _httpClient.Verify(h => h.Post(message), Times.Exactly(1));
            _waitHandle.Verify(w => w.Reset(), Times.Exactly(1));
            _persistentStore.Verify(p => p.Remove(), Times.Never);
            messageProvider.Verify(mp => mp.Delete(message), Times.Never);
        }

        [Fact]
        public void WhenCallingDoWorkAndServiceIsTerminatingShouldNotSendMessage()
        {
            _service.Initialize(_persistentStore.Object, _httpClient.Object, _networkStateService.Object, 0, 0, false);
            Message message = new Message(new Uri("http://google.com"), "MessageBody");
            List<IMessage> messages = new List<IMessage> { message };

            var messageProvider = new Mock<IMessageProvider>();
            messageProvider.Setup(mp => mp.CanSend).Returns(true);
            _service.MessageProvider = messageProvider.Object;

            _persistentStore.Setup(v => v.Get()).Returns(() => GetMessage(messages));
            _persistentStore.Setup(v => v.Remove()).Callback(() => RemoveMessage(messages, message));

            _service.Terminate = true;
            _service.DoWork();

            _httpClient.Verify(h => h.Post(message), Times.Never());
            _waitHandle.Verify(w => w.Reset(), Times.Exactly(1));
            _persistentStore.Verify(p => p.Remove(), Times.Never);
            Assert.Equal(1, messages.Count);
        }

        [Fact]
        public void WhenCallingOnNetworkConnectedShouldCallSetOnWaitHandle()
        {
            _service.OnNetworkConnected(this, new EventArgs());
            _waitHandle.Verify(w => w.Set(), Times.Exactly(1));
        }

        [Fact]
        public void WhenCallingOnNetworkConnectedAndServiceIsTerminatingShouldNotCallSetOnWaitHandle()
        {
            _service.Terminate = true;
            _service.OnNetworkConnected(this, new EventArgs());
            _waitHandle.Verify(w => w.Set(), Times.Never());
        }

        [Fact]
        public void WhenCallingResumeShouldSignalWorkToDo()
        {
            _service.Resume();

            _waitHandle.Verify(wh => wh.Set(), Times.Once);
        }

        private void RemoveMessage(List<IMessage> messages, IMessage message)
        {
            messages.Remove(message);
        }

        private IMessage GetMessage(List<IMessage> messages)
        {
            if (messages.Count > 0)
            {
                return messages[0];
            }
            else
            {
                return null;
            }
        }
    }
}
