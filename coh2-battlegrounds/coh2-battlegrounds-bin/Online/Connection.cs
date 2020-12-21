﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Battlegrounds.Online {
    
    /// <summary>
    /// Represents a server-client connection. Provides abstraction for <see cref="Socket"/> send and receive methods. This class cannot be inherited.
    /// </summary>
    public sealed class Connection {

        Thread m_processThread;

        volatile Queue<Message> m_messageQueue;
        volatile Dictionary<int, Action<Message>> m_identifierCallback;
        volatile Socket m_socket;
        volatile bool m_isOpen;
        volatile bool m_isListening;

        /// <summary>
        /// The <see cref="Socket"/> the <see cref="Connection"/> instance is using.
        /// </summary>
        public Socket ConnectionSocket => m_socket;

        /// <summary>
        /// Flag for connection state.
        /// </summary>
        public bool IsConnected => m_socket?.Connected ?? false;

        /// <summary>
        /// The event to trigger when a <see cref="Message"/> has been received.
        /// </summary>
        public event Action<Message> OnMessage;

        /// <summary>
        /// Create a new <see cref="Connection"/> instance for a <see cref="Socket"/>.
        /// </summary>
        /// <param name="socket">The socket to base <see cref="Connection"/> on.</param>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="OutOfMemoryException"/>
        public Connection(Socket socket) {

            this.m_socket = socket;
            this.m_isOpen = true;
            this.m_identifierCallback = new Dictionary<int, Action<Message>>();
            this.m_messageQueue = new Queue<Message>();
            this.m_isListening = false;

            this.Start();

        }

        /// <summary>
        /// Create a new <see cref="Connection"/> instance for a <see cref="Socket"/>. Can start or wait with listening for messages.
        /// </summary>
        /// <param name="socket">The socket to base <see cref="Connection"/> on.</param>
        /// <param name="startListening">Start listening for new messages once instantiated.</param>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="OutOfMemoryException"/>
        public Connection(Socket socket, bool startListening) {

            this.m_socket = socket;
            this.m_isOpen = startListening;
            this.m_identifierCallback = new Dictionary<int, Action<Message>>();
            this.m_messageQueue = new Queue<Message>();
            this.m_isListening = false;

            if (this.m_isOpen) {
                this.Start();
            }

        }

        private void MessageReceived(Socket source, Message message) {
            this.m_isListening = false;
            if (message.Descriptor == MessageType.SERVER_PING) {
                this.SendMessage(message.CreateResponse(MessageType.USER_PING));
            } else {
                Trace.WriteLine($"Received message <<{message}>> ({message.ToBytes().Length} bytes){Environment.NewLine}", "Online-Service");
                if (this.m_identifierCallback?.ContainsKey(message.Identifier) ?? false) {
                    this.m_identifierCallback[message.Identifier].Invoke(message);
                } else {
                    this.OnMessage?.Invoke(message);
                }
            }
            if (this.m_isOpen) {
                if (source != this.m_socket) {
                    Trace.WriteLine("Socket-Mismatch!");
                }
                this.Listen();
            }
        }

        private void InternalProccessor() {
            while (this.m_isOpen || this.m_messageQueue.Count > 0) {
                try {
                    if (this.m_messageQueue.Count > 0) {

                        Message topMessage = this.m_messageQueue.Dequeue();

                        lock (this.m_socket) {
                            byte[] msg = topMessage.ToBytes();
                            this.m_socket.SendAll(msg);
                            Trace.WriteLine($"Sent message <<{topMessage}>> ({msg.Length} bytes){Environment.NewLine}", "Online-Service");
                        }

                        Thread.Sleep(120);

                    } else {
                        Thread.Sleep(60);
                    }
                } catch (ObjectDisposedException) {
                    break;
                }
            }
        }

        /// <summary>
        /// Start listening for <see cref="Message"/> data.
        /// </summary>
        public void Listen() {
            if (!this.m_isListening) {
                MessageSender.WaitForMessage(this.m_socket, this.MessageReceived);
                this.m_isListening = true;
            }
        }

        /// <summary>
        /// Enqueue a <see cref="Message"/> to be send as soon as possible.
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(Message message)
            => this.m_messageQueue.Enqueue(message);

        /// <summary>
        /// Start listening and sending <see cref="Message"/> data.
        /// </summary>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="OutOfMemoryException"/>
        public void Start() {
            this.m_isOpen = true;
            this.m_processThread = new Thread(this.InternalProccessor);
            this.m_processThread.Start();
            this.m_isListening = false; // reset in next func call
            this.Listen();
        }

        /// <summary>
        /// Stop listening for and sending <see cref="Message"/> data.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException"/>
        /// <exception cref="ThreadStartException"/>
        /// <exception cref="SocketException"/>
        /// <exception cref="ObjectDisposedException"/>
        /// <exception cref="System.Security.SecurityException"/>
        public void Stop() {

            this.m_isOpen = false;
            this.m_isListening = false;

            if (this.m_socket != null) {

                Task.Run(async () => { 
                    while (this.m_processThread.IsAlive) {
                        await Task.Delay(1);
                    }
                    this.m_socket.Shutdown(SocketShutdown.Both);
                    this.m_socket.Close();
                    this.m_socket = null;
                });

            }

        }

        /// <summary>
        /// Set intercepting <see cref="Message"/> identifier callback. When used, messages with identifier will be intercepted and the action invoked.
        /// This will bypass the <see cref="OnMessage"/> and <see cref="OnFile"/> events.
        /// </summary>
        /// <param name="identifier">The integer used to identify the message.</param>
        /// <param name="onMessage">The callback action to trigger when receiving message with identifier.</param>
        /// <exception cref="ArgumentException"/>
        /// <remarks>Remember to use <see cref="ClearIdentifierReceiver(int)"/> when done.</remarks>
        public void SetIdentifierReceiver(int identifier, Action<Message> onMessage) { 
            if (this.m_identifierCallback.ContainsKey(identifier)) {
                throw new ArgumentException($"The identifier '{identifier}' already has a callback.");
            } else {
                this.m_identifierCallback.Add(identifier, onMessage);
            }
        }

        /// <summary>
        /// Clear the callback associated with an identifier.
        /// </summary>
        /// <param name="identifier">The identifier callback to remove.</param>
        public void ClearIdentifierReceiver(int identifier) {
            if (this.m_identifierCallback.ContainsKey(identifier)) {
                this.m_identifierCallback.Remove(identifier);
            }
        }

    }

}
