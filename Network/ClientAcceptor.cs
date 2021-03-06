﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PIGMServer.Network
{
    /// <summary>
    /// Static class that handles all incoming Client connections.
    /// </summary>
    static class ClientAcceptor
    {
        static readonly List<Client> clients = new List<Client>();                  // Connected Clients.
        static readonly List<ClientOwner> acceptQueue = new List<ClientOwner>();    // Owners waiting for Clients.
        public static Func<int> OwnerCreationFunc = null;                           // Function to create new Client Owners.

        /// <summary>
        /// Add a TCP Client to the Acceptor.
        /// </summary>
        /// <param name="tcpClient">TCP Client to add.</param>
        public static void AddTCPClient(TcpClient tcpClient)
        {
            if (acceptQueue.Count > 0) // ... If there are Owners waiting...
            {
                // ... Take the first owner in the queue and hand over Client.
                ClientOwner nextOwner = acceptQueue[0];
                Client newClient = new Client(tcpClient);
                clients.Add(newClient);
                nextOwner.Give(newClient);
                newClient.HandedTo(nextOwner);

                // If the Owner can't accept anymore Clients, remove it.
                if (!nextOwner.CanAcceptClient())
                {
                    acceptQueue.Remove(nextOwner);
                }
            }
            else // ... Otherwise...
            {
                // Create new Owner and assign Client.
                OwnerCreationFunc();
                AddTCPClient(tcpClient);
            }

        }

        /// <summary>
        /// Start the Acceptor with given TCP Listener.
        /// </summary>
        /// <param name="server">TCP Listener to use.</param>
        public static void Start(TcpListener server)
        {
            // Create a new thread which accepts new connecting Clients.
            Thread acceptThread = new Thread(() =>
            {
                while (true)
                {
                    TcpClient newClient = server.AcceptTcpClient();
                    AddTCPClient(newClient);
                }
            });

            // Name and start the thread.
            acceptThread.Name = "Accept Thread";
            acceptThread.Start();

            // Meanwhile continously check connected clients for new messages.
            while (true)
            {
                List<Client> tempClients = clients;
                int clientCount = tempClients.Count;

                for (int i = 0; i < clientCount; i++)
                {
                    Client client = tempClients[i];

                    if (client != null && client.tcpClient.Connected)
                        CheckClient(client);
                }

                Thread.Sleep(5); // Preserve CPU.
            }
        }

        /// <summary>
        /// Send Message to the Client.
        /// </summary>
        /// <param name="client">Client to send Message to.</param>
        /// <param name="message">Message to send.</param>
        public static void SendMessage(Client client, Message message)
        {
            byte[] response = message.Encode(true);

            try
            {
                if (client.Connected && client.Stream.CanWrite)
                {
                    client.Stream.Write(response, 0, response.Length);
                }
            }
            catch (IOException e)
            {
                client.tcpClient.Close();

                Console.WriteLine("Disconnecting client");
                Console.WriteLine(e.Message.ToString());
            }
            catch (Exception e) { }
        }

        /// <summary>
        /// Queue the given Owner.
        /// </summary>
        /// <param name="owner">Owner to queue.</param>
        public static void QueueOwner(ClientOwner owner)
        {
            acceptQueue.Add(owner);
        }

        #region Borrowed Code With Modifications.
        // Stolen from Mozilla ;)
        // Using CC-BY-SA License, link to resource: https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
        private static void CheckClient(Client appClient)
        {
            TcpClient client = appClient.tcpClient;
            NetworkStream stream = appClient.Stream;

            if (!stream.DataAvailable && client.Available < 3)
            {
                return;
            }

            int available = client.Available;
            byte[] bytes = new byte[available];
            stream.Read(bytes, 0, available);
            string s = Encoding.UTF8.GetString(bytes);

            if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
            {
                Console.WriteLine("=====Handshaking from client=====\n{0}", s);

                // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                // 3. Compute SHA-1 and Base64 hash of the new value
                // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                byte[] response = Encoding.UTF8.GetBytes(
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");

                stream.Write(response, 0, response.Length);
                appClient.Connected = true;
            }
            else
            {
                bool fin = (bytes[0] & 0b10000000) != 0;
                bool mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"

                int opcode = bytes[0] & 0b00001111, // expecting 1 - text message
                    msglen = bytes[1] - 128, // & 0111 1111
                    offset = 2;

                if (msglen == 126)
                {
                    // was ToUInt16(bytes, offset) but the result is incorrect
                    msglen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                    offset = 4;
                }
                else if (mask)
                {
                    byte[] decoded = new byte[msglen];
                    byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                    offset += 4;

                    for (int i = 0; i < msglen; ++i)
                        decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                    // Send message to client owner.
                    Message message = new Message(decoded);

                    if(appClient.Owner != null)
                        appClient.Owner.HandleMessage(appClient, message);
                }
            }
        }
        #endregion
    }
}
