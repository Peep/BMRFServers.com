﻿using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SourceRcon
{
    /// <summary>
    ///     Summary description for SourceRcon.
    /// </summary>
    public class SourceRcon
    {
        public SourceRcon()
        {
            S = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            PacketCount = 0;

#if DEBUG
            TempPackets = new ArrayList();
#endif
        }

        public bool Connect(IPEndPoint Server, string password)
        {
            try
            {
                S.Connect(Server);
            }
            catch (SocketException)
            {
                OnError(ConnectionFailedString);
                OnConnectionSuccess(false);
                return false;
            }

            var SA = new RCONPacket();
            SA.RequestId = 1;
            SA.String1 = password;
            SA.ServerDataSent = RCONPacket.SERVERDATA_sent.SERVERDATA_AUTH;

            SendRCONPacket(SA);

            // This is the first time we've sent, so we can start listening now!
            StartGetNewPacket();

            return true;
        }

        public void ServerCommand(string command)
        {
            if (connected)
            {
                var PacketToSend = new RCONPacket();
                PacketToSend.RequestId = 2;
                PacketToSend.ServerDataSent = RCONPacket.SERVERDATA_sent.SERVERDATA_EXECCOMMAND;
                PacketToSend.String1 = command;
                SendRCONPacket(PacketToSend);
            }
        }

        private void SendRCONPacket(RCONPacket p)
        {
            byte[] Packet = p.OutputAsBytes();
            S.BeginSend(Packet, 0, Packet.Length, SocketFlags.None, SendCallback, this);
        }

        private bool connected;

        public bool Connected
        {
            get { return connected; }
        }

        private void SendCallback(IAsyncResult ar)
        {
            S.EndSend(ar);
        }

        private int PacketCount;

        private void StartGetNewPacket()
        {
            var state = new RecState();
            state.IsPacketLength = true;
            state.Data = new byte[4];
            state.PacketCount = PacketCount;
            PacketCount++;
#if DEBUG
            TempPackets.Add(state);
#endif
            S.BeginReceive(state.Data, 0, 4, SocketFlags.None, ReceiveCallback, state);
        }

#if DEBUG
        public ArrayList TempPackets;
#endif

        private void ReceiveCallback(IAsyncResult ar)
        {
            bool recsuccess = false;
            RecState state = null;

            try
            {
                int bytesgotten = S.EndReceive(ar);
                state = (RecState) ar.AsyncState;
                state.BytesSoFar += bytesgotten;
                recsuccess = true;

#if DEBUG
                Console.WriteLine("Receive Callback. Packet: {0} First packet: {1}, Bytes so far: {2}",
                    state.PacketCount, state.IsPacketLength, state.BytesSoFar);
#endif
            }
            catch (SocketException)
            {
                OnError(ConnectionClosed);
            }

            if (recsuccess)
                ProcessIncomingData(state);
        }

        private void ProcessIncomingData(RecState state)
        {
            if (state.IsPacketLength)
            {
                // First 4 bytes of a new packet.
                state.PacketLength = BitConverter.ToInt32(state.Data, 0);

                state.IsPacketLength = false;
                state.BytesSoFar = 0;
                state.Data = new byte[state.PacketLength];
                S.BeginReceive(state.Data, 0, state.PacketLength, SocketFlags.None, ReceiveCallback, state);
            }
            else
            {
                // Do something with data...

                if (state.BytesSoFar < state.PacketLength)
                {
                    // Missing data.
                    S.BeginReceive(state.Data, state.BytesSoFar, state.PacketLength - state.BytesSoFar, SocketFlags.None,
                        ReceiveCallback, state);
                }
                else
                {
                    // Process data.
#if DEBUG
                    Console.WriteLine("Complete packet.");
#endif

                    var RetPack = new RCONPacket();
                    RetPack.ParseFromBytes(state.Data, this);

                    ProcessResponse(RetPack);

                    // Wait for new packet.
                    StartGetNewPacket();
                }
            }
        }

        private void ProcessResponse(RCONPacket P)
        {
            switch (P.ServerDataReceived)
            {
                case RCONPacket.SERVERDATA_rec.SERVERDATA_AUTH_RESPONSE:
                    if (P.RequestId != -1)
                    {
                        // Connected.
                        connected = true;
                        OnError(ConnectionSuccessString);
                        OnConnectionSuccess(true);
                    }
                    else
                    {
                        // Failed!
                        OnError(ConnectionFailedString);
                        OnConnectionSuccess(false);
                    }
                    break;
                case RCONPacket.SERVERDATA_rec.SERVERDATA_RESPONSE_VALUE:
                    if (hadjunkpacket)
                    {
                        // Real packet!
                        OnServerOutput(P.String1);
                    }
                    else
                    {
                        hadjunkpacket = true;
                        OnError(GotJunkPacket);
                    }
                    break;
                default:
                    OnError(UnknownResponseType);
                    break;
            }
        }

        private bool hadjunkpacket;

        internal void OnServerOutput(string output)
        {
            if (ServerOutput != null)
            {
                ServerOutput(output);
            }
        }

        internal void OnError(string error)
        {
            if (Errors != null)
            {
                Errors(error);
            }
        }

        internal void OnConnectionSuccess(bool info)
        {
            if (ConnectionSuccess != null)
            {
                ConnectionSuccess(info);
            }
        }

        public event StringOutput ServerOutput;
        public event StringOutput Errors;
        public event BoolInfo ConnectionSuccess;

        public static string ConnectionClosed = "Connection closed by remote host";
        public static string ConnectionSuccessString = "Connection Succeeded!";
        public static string ConnectionFailedString = "Connection Failed!";
        public static string UnknownResponseType = "Unknown response";
        public static string GotJunkPacket = "Had junk packet. This is normal.";

        private readonly Socket S;
    }

    public delegate void StringOutput(string output);

    public delegate void BoolInfo(bool info);

    internal class RecState
    {
        public int BytesSoFar;
        public byte[] Data;
        public bool IsPacketLength;
        public int PacketCount;
        public int PacketLength;

        internal RecState()
        {
            PacketLength = -1;
            BytesSoFar = 0;
            IsPacketLength = false;
        }
    }


    internal class RCONPacket
    {
        public enum SERVERDATA_rec
        {
            SERVERDATA_RESPONSE_VALUE = 0,
            SERVERDATA_AUTH_RESPONSE = 2,
            None = 255
        }

        public enum SERVERDATA_sent
        {
            SERVERDATA_AUTH = 3,
            SERVERDATA_EXECCOMMAND = 2,
            None = 255
        }

        internal int RequestId;
        internal SERVERDATA_rec ServerDataReceived;
        internal SERVERDATA_sent ServerDataSent;
        internal string String1;
        internal string String2;

        internal RCONPacket()
        {
            RequestId = 0;
            String1 = "blah";
            String2 = String.Empty;
            ServerDataSent = SERVERDATA_sent.None;
            ServerDataReceived = SERVERDATA_rec.None;
        }

        internal byte[] OutputAsBytes()
        {
            byte[] packetsize;
            byte[] reqid;
            byte[] serverdata;
            byte[] bstring1;
            byte[] bstring2;

            var utf = new UTF8Encoding();

            bstring1 = utf.GetBytes(String1);
            bstring2 = utf.GetBytes(String2);

            serverdata = BitConverter.GetBytes((int) ServerDataSent);
            reqid = BitConverter.GetBytes(RequestId);

            // Compose into one packet.
            var FinalPacket = new byte[4 + 4 + 4 + bstring1.Length + 1 + bstring2.Length + 1];
            packetsize = BitConverter.GetBytes(FinalPacket.Length - 4);

            int BPtr = 0;
            packetsize.CopyTo(FinalPacket, BPtr);
            BPtr += 4;

            reqid.CopyTo(FinalPacket, BPtr);
            BPtr += 4;

            serverdata.CopyTo(FinalPacket, BPtr);
            BPtr += 4;

            bstring1.CopyTo(FinalPacket, BPtr);
            BPtr += bstring1.Length;

            FinalPacket[BPtr] = 0;
            BPtr++;

            bstring2.CopyTo(FinalPacket, BPtr);
            BPtr += bstring2.Length;

            FinalPacket[BPtr] = 0;
            BPtr++;

            return FinalPacket;
        }

        internal void ParseFromBytes(byte[] bytes, SourceRcon parent)
        {
            int BPtr = 0;
            ArrayList stringcache;
            var utf = new UTF8Encoding();

            // First 4 bytes are ReqId.
            RequestId = BitConverter.ToInt32(bytes, BPtr);
            BPtr += 4;
            // Next 4 are server data.
            ServerDataReceived = (SERVERDATA_rec) BitConverter.ToInt32(bytes, BPtr);
            BPtr += 4;
            // string1 till /0
            stringcache = new ArrayList();
            while (bytes[BPtr] != 0)
            {
                stringcache.Add(bytes[BPtr]);
                BPtr++;
            }
            String1 = utf.GetString((byte[]) stringcache.ToArray(typeof (byte)));
            BPtr++;

            // string2 till /0

            stringcache = new ArrayList();
            while (bytes[BPtr] != 0)
            {
                stringcache.Add(bytes[BPtr]);
                BPtr++;
            }
            String2 = utf.GetString((byte[]) stringcache.ToArray(typeof (byte)));
            BPtr++;

            // Repeat if there's more data?

            if (BPtr != bytes.Length)
            {
                parent.OnError("Urk, extra data!");
            }
        }
    }
}