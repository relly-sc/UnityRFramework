using NUnit.Framework;

namespace UnityRFramework.Sample.Messenger.Tests
{
    public sealed class MessengerTests
    {
        [SetUp]
        public void SetUp()
        {
            Messenger.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            Messenger.ClearAll();
        }

        [Test]
        public void PublishAndUnsubscribe_UsesTypedPayload()
        {
            int received = 0;
            System.Action<int> handler = value => received += value;

            Messenger.Subscribe("score", handler);
            Messenger.Publish("score", 3);
            Messenger.Unsubscribe("score", handler);
            Messenger.Publish("score", 5);

            Assert.That(received, Is.EqualTo(3));
        }

        [Test]
        public void SameMessageCannotUseDifferentPayloadType()
        {
            Messenger.Subscribe("state", (System.Action<int>)(_ => { }));

            Assert.Throws<System.InvalidOperationException>(() =>
                Messenger.Subscribe("state", (System.Action<string>)(_ => { })));
        }
    }
}
