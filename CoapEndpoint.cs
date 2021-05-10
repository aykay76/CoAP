using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;

namespace coap.core
{
    // CoAP endpoint is a communication endpoint with another host. Whatever consumes this will have 
    // multiple endpoints if it is communicating with multiple parties
    public class CoapEndpoint
    {
        IPEndPoint localHost = null;

        // message ID for this endpoint
        UInt16 messageID;
        // create hash for message IDs, we will truncate this to get the first 16 bits
        SHA256 hasher;
        byte[] idhash;

        private UdpClient endpoint;

        private Queue<CoapMessage> outgoingQueue;

        private List<Task> messagesInProgress;

        // TODO: add mechanism for retry/exponential backoff

        private bool queueBusy;

        public enum SendMessageStatus { SentImmediately, BusyQueued, Sending };

        private IRequestHandler requestHandler;
        private IResponseHandler responseHandler;
        private IEmptyMessageHandler emptyMessageHandler;
        private List<CoapMessage> unconfirmed;

        private int ACK_TIMEOUT = 2;
        private float ACK_RANDOM_FACTOR = 1.5f;
        private int MAX_RETRANSMIT = 4;
        private int NSTART = 1;
        private int DEFAULT_LEISURE = 5;
        private int PROBING_RATE = 1;

        Task receiveTask = null;
        Task sendTask = null;

        public CoapEndpoint()
        {
            queueBusy = false;

            messagesInProgress = new List<Task>();

            // generate initial message ID
            hasher = SHA256.Create();

            unconfirmed = new List<CoapMessage>();
            outgoingQueue = new Queue<CoapMessage>();
        }

        // TODO: either add raw message handler or remove IMessageHandler and CoapMessageHandler

        public CoapEndpoint WithRequestHandler(IRequestHandler handler)
        {
            requestHandler = handler;
            return this;
        }

        public CoapEndpoint WithResponseHandler(IResponseHandler handler)
        {
            responseHandler = handler;
            return this;
        }

        public CoapEndpoint WithEmptyMessageHandler(IEmptyMessageHandler handler)
        {
            emptyMessageHandler = handler;
            return this;
        }

        public CoapEndpoint WithLocalEndpoint(IPEndPoint localEndpoint)
        {
            localHost = localEndpoint;

            try
            {
                // create a new endpoint and associate with a local endpoint
                endpoint = new UdpClient(localHost);
                endpoint.EnableBroadcast = true;

                // successul, create ID for first message
                idhash = hasher.ComputeHash(localHost.Address.GetAddressBytes());
            }
            catch (SocketException ex)
            {
                throw ex;
            }

            return this;
        }

        public void StartListening(CancellationToken token)
        {
            var exitEvent = new ManualResetEvent(false);

            Console.CancelKeyPress += (sender, eventArgs) => {
                                  eventArgs.Cancel = true;
                                  exitEvent.Set();
                              };

            // processing receiving on a background thread, freeing this thread to handle sending
            receiveTask = Task.Run(() => BeginReceiving(token));

            // TODO: I don't like this spinning forever, this needs to be triggered only when something
            // is worth checking, and needs to be done on a Task.Run()
            // sendTask = Task.Run(() => BeginSending(token));
            exitEvent.WaitOne();
        }

        public async Task WaitForStop()
        {
            if (receiveTask != null)
            {
                Console.WriteLine("Waiting for receive thread");
                await receiveTask;
            }
            if (sendTask != null)
            {
                Console.WriteLine("Waiting for send thread");
                await sendTask;
            }
        }

        // this method will run on a background thread once the endpoint has begun
        public async Task BeginReceiving(CancellationToken token)
        {
            Task<UdpReceiveResult> recvTask = null;

            while (!token.IsCancellationRequested)
            {
                // only try to receive if i wasn't already
                if (recvTask == null)
                {
                    recvTask = endpoint.ReceiveAsync();
                }

                // wait for receive whether started now or previously
                if (await Task.WhenAny(recvTask, Task.Delay(100)) == recvTask)
                {
                    UdpReceiveResult result = recvTask.Result;

                    // deserialise the message from the datagram
                    CoapMessage message = null;
                    try
                    {
                        message = CoapMessage.FromByteArray(result.Buffer, result.RemoteEndPoint);
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }

                    // TODO: check MessageID and Token against requests to correlate response
                    // TODO: if a correlated response is received call the response handler
                    if (message.IsRequest())
                    {
                        await requestHandler.HandleRequest(message);
                    }
                    else if (message.IsResponse())
                    {
                        // check the unconfirmed list to see if the message ID matches
                        foreach (CoapMessage m in unconfirmed)
                        {
                            if (message.MessageID == m.MessageID)
                            {
                                unconfirmed.Remove(m);
                                break;
                            }
                        }

                        await responseHandler.HandleResponse(message);
                    }
                    else
                    {
                        await emptyMessageHandler.HandleEmptyMessage(message);
                    }

                    recvTask = null;
                }
            }
        }

        public async Task BeginSending(CancellationToken token)
        {
            Stopwatch stopwatch = new Stopwatch();
            while (!token.IsCancellationRequested)
            {
                stopwatch.Start();

                List<CoapMessage> toDelete = new List<CoapMessage>();

                // loop to check/retry unconfirmed messages
                foreach (CoapMessage message in unconfirmed)
                {
                    if (message.SendAttempts >= MAX_RETRANSMIT)
                    {
                        toDelete.Add(message);
                    }
                    else
                    {
                        // check last sent time against retry timeout - CPU efficient timer for 1s between checks
                        if ((DateTime.Now - message.LastSent).TotalSeconds >= ACK_TIMEOUT)
                        {
                            // get ready to try again
                            SendRequest(message);
                        }
                    }
                }

                // now remove all the ones that need to be removed
                foreach (CoapMessage message in toDelete)
                {
                    // TODO: need to notify my owner that the message was unconfirmed
                    unconfirmed.Remove(message);
                }

                if (stopwatch.ElapsedMilliseconds < 1000)
                {
                    await Task.Delay(1000 - (int)stopwatch.ElapsedMilliseconds);
                }
            }
        }

        public void SendGet(Uri uri, bool reliable)
        {
            CoapMessageType type = CoapMessageType.NonConfirmable;

            if (reliable)
            {
                type = CoapMessageType.Confirmable;
            }

            CoapMessage message = new CoapMessage(type, CoapMessageCode.GET);
            message.Uri = uri;
            SendRequest(message);
        }

        public void SendPost(IPEndPoint destination, Uri uri, bool reliable, string content)
        {
            CoapMessageType type = CoapMessageType.NonConfirmable;

            if (reliable)
            {
                type = CoapMessageType.Confirmable;
            }

            CoapMessage message = new CoapMessage(type, CoapMessageCode.POST);
            message.Destination = destination;
            message.Uri = uri;
            message.Payload = content == null ? null : System.Text.Encoding.UTF8.GetBytes(content);
            SendRequest(message);
        }

        public void SendPost(IPEndPoint destination, Uri uri, bool reliable, object content)
        {
            CoapMessageType type = CoapMessageType.NonConfirmable;

            if (reliable)
            {
                type = CoapMessageType.Confirmable;
            }

            string text = JsonSerializer.Serialize(content);

            CoapMessage message = new CoapMessage(type, CoapMessageCode.POST);
            message.Destination = destination;
            message.Uri = uri;
            message.Payload = content == null ? null : System.Text.Encoding.UTF8.GetBytes(text);
            SendRequest(message);
        }

        public void SendPut(Uri uri, bool reliable, string content)
        {
            CoapMessageType type = CoapMessageType.NonConfirmable;

            if (reliable)
            {
                type = CoapMessageType.Confirmable;
            }

            CoapMessage message = new CoapMessage(type, CoapMessageCode.PUT);
            message.Uri = uri;
            message.Payload = System.Text.Encoding.UTF8.GetBytes(content);
            SendRequest(message);
        }

        public void SendDelete(Uri uri, bool reliable)
        {
            CoapMessageType type = CoapMessageType.NonConfirmable;

            if (reliable)
            {
                type = CoapMessageType.Confirmable;
            }

            CoapMessage message = new CoapMessage(type, CoapMessageCode.DELETE);
            message.Uri = uri;
            SendRequest(message);
        }

        // TODO: build this kind of mechanism into handling a queue of tasks:
        // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/start-multiple-async-tasks-and-process-them-as-they-complete
        // https://devblogs.microsoft.com/pfxteam/processing-tasks-as-they-complete/
        public SendMessageStatus SendMessage(CoapMessage message)
        {
            message.DecomposeUri(message.Uri);

            message.MessageID = (ushort)(idhash[0] << 8);
            message.MessageID |= (ushort)idhash[1];

            // prepare hasher for next message ID
            idhash = hasher.ComputeHash(idhash);

            SendMessageStatus status = SendMessageStatus.BusyQueued;

            // TODO: move this to something that will check in the background - not as part of send! - check for any messages that are complete
            List<Task> toRemove = new List<Task>();
            foreach (Task t in messagesInProgress)
            {
                if (t.IsCompleted || t.IsCanceled || t.IsFaulted)
                {
                    // TODO: do a bit more than this, handle errors
                    toRemove.Add(t);
                }
            }
            foreach (Task t in toRemove)
            {
                messagesInProgress.Remove(t);
            }

            // TODO: if busy sending enqueue the message, make max configurable 
            if (messagesInProgress.Count > 100)
            {
                // too busy, queue the message and move on
                outgoingQueue.Enqueue(message);
                status = SendMessageStatus.BusyQueued;
            }
            else
            {
                // TODO: generate tokens and whatever needed to fulfill the protocol

                // encode the message into a byte array
                byte[] datagram = message.ToByteArray();

                // attempt to send the message
                Task<int> t = null;

                t = endpoint.SendAsync(datagram, datagram.Length, message.Destination);

                if (t.IsCompleted)
                {
                    status = SendMessageStatus.SentImmediately;
                }
                else
                {
                    Console.WriteLine($"Didn't send immediately so putting on progress list to check");
                    status = SendMessageStatus.Sending;
                    messagesInProgress.Add(t);
                }
            }

            // TODO: kick queue

            return status;
        }
        protected void SendRequest(CoapMessage message)
        {
            // if the message isn't confirmed we need some tracking information to try again at some point
            message.LastSent = DateTime.Now;
            message.SendAttempts = 1;

            if (message.Type == CoapMessageType.Confirmable)
            {
                // adding this now will cause it to be sent now and again when the background thread processes
                // this means my timer isn't working
                unconfirmed.Add(message);
            }

            SendMessage(message);
        }

        public void SendResponse(CoapMessage message)
        {
            // TODO: send response appropriate to the request passed in
            // depending on whether this is a client or server, the response will be different.
            // it could be that the response is actually a request for something else
            // given a response to something that requires another resource
        }
    }
}