﻿namespace Reyna.Facts.Store
{
    using System;
    using Xunit;
    using Reyna.Interfaces;

    public class GivenAnInMemoryQueue
    {
        [Fact]
        public void WhenConstructingShouldNotThrow()
        {
            new InMemoryQueue();
        }

        [Fact]
        public void WhenCallingInitialiseShouldNotThrow()
        {
            InMemoryQueue q = new InMemoryQueue();
            q.Initialise();
        }

        [Fact]
        public void WhenCallingAddShouldAddAndRaiseEvent()
        {
            var messageUri = new Uri("http://www.google.com");
            var messageBody = string.Empty;

            var queue = new InMemoryQueue();

            var enqueueCounter = 0;
            queue.MessageAdded += (sender, e) => { enqueueCounter++; };

            queue.Add(new Message(messageUri, messageBody));

            Assert.NotNull(queue.Get());
            Assert.Equal(messageUri, queue.Get().Url);
            Assert.Equal(messageBody, queue.Get().Body);
            Assert.Equal(1, enqueueCounter);
        }

        [Fact]
        public void WhenCallingGetAndIsEmptyShouldNotThrow()
        {
            var queue = new InMemoryQueue();
            Assert.Null(queue.Get());
        }

        [Fact]
        public void WhenCallingRemoveAndIsEmptyShouldNotThrow()
        {
            var queue = new InMemoryQueue();
            Assert.Null(queue.Remove());
        }

        [Fact]
        public void WhenCallingRemoveAndIsNotEmptyShouldRemove()
        {
            var queue = new InMemoryQueue();
            queue.Add(new Message(null, null));

            var actual = queue.Remove();
            Assert.NotNull(actual);
            Assert.Null(actual.Url);
            Assert.Null(actual.Body);
        }

        [Fact]
        public void WhenCallingAddMessageWithSizeThenRemoveAndIsNotEmptyShouldRemove()
        {
            var queue = new InMemoryQueue();
            queue.Add(new Message(null, null), 1);

            var actual = queue.Remove();
            Assert.NotNull(actual);
            Assert.Null(actual.Url);
            Assert.Null(actual.Body);
        }
    }
}
