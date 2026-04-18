using System.Collections.Generic;
using NUnit.Framework;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Core.Server;

namespace Mosaic.Bridge.Tests.Dispatcher
{
    [TestFixture]
    public class RoundRobinTests
    {
        [Test]
        public void TryDequeue_RoundRobinAcrossClients()
        {
            var queue = new ToolQueue(capacity: 200);

            // Enqueue 3 requests for each of 3 clients
            var clients = new[] { "client-a", "client-b", "client-c" };
            foreach (var clientId in clients)
            {
                for (int i = 0; i < 3; i++)
                {
                    var pr = MakePendingRequest(clientId, $"{clientId}-{i}");
                    queue.TryEnqueue(pr);
                }
            }

            Assert.AreEqual(9, queue.Count);

            // Dequeue all and verify round-robin order: a,b,c,a,b,c,a,b,c
            var dequeued = new List<string>();
            while (queue.TryDequeue(out var pr))
            {
                dequeued.Add(pr.ClientId);
            }

            Assert.AreEqual(9, dequeued.Count);

            Assert.AreEqual("client-a", dequeued[0]);
            Assert.AreEqual("client-b", dequeued[1]);
            Assert.AreEqual("client-c", dequeued[2]);
            Assert.AreEqual("client-a", dequeued[3]);
            Assert.AreEqual("client-b", dequeued[4]);
            Assert.AreEqual("client-c", dequeued[5]);
            Assert.AreEqual("client-a", dequeued[6]);
            Assert.AreEqual("client-b", dequeued[7]);
            Assert.AreEqual("client-c", dequeued[8]);
        }

        [Test]
        public void TryDequeue_SingleClient_FifoOrder()
        {
            var queue = new ToolQueue(capacity: 200);

            for (int i = 0; i < 5; i++)
            {
                var pr = MakePendingRequest("single-client", $"/tool/{i}");
                queue.TryEnqueue(pr);
            }

            var urls = new List<string>();
            while (queue.TryDequeue(out var pr))
            {
                urls.Add(pr.Request.RawUrl);
            }

            Assert.AreEqual(5, urls.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual($"/tool/{i}", urls[i], $"Item {i} should be in FIFO order");
            }
        }

        [Test]
        public void TryDequeue_EmptyQueue_ReturnsFalse()
        {
            var queue = new ToolQueue(capacity: 200);
            Assert.IsFalse(queue.TryDequeue(out var pr));
            Assert.IsNull(pr);
        }

        [Test]
        public void DrainWith_CompletesAllPendingRequests()
        {
            var queue = new ToolQueue(capacity: 200);

            var requests = new List<PendingRequest>();
            for (int i = 0; i < 5; i++)
            {
                var pr = MakePendingRequest("client-a", $"/tool/{i}");
                queue.TryEnqueue(pr);
                requests.Add(pr);
            }

            var drainResponse = new HandlerResponse
            {
                StatusCode = 503,
                ContentType = "application/json",
                Body = "{\"error\":\"drained\"}"
            };

            int drained = queue.DrainWith(drainResponse);
            Assert.AreEqual(5, drained);
            Assert.AreEqual(0, queue.Count);

            foreach (var pr in requests)
            {
                Assert.IsTrue(pr.Tcs.Task.IsCompleted);
                Assert.AreEqual(503, pr.Tcs.Task.Result.StatusCode);
            }
        }

        private static PendingRequest MakePendingRequest(string clientId, string rawUrl = "/test")
        {
            return new PendingRequest(new HandlerRequest
            {
                Method = "POST",
                RawUrl = rawUrl,
                Body = new byte[0],
                ClientId = clientId
            })
            {
                ClientId = clientId
            };
        }
    }
}
