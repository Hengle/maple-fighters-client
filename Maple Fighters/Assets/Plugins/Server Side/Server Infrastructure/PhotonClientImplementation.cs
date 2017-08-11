﻿using CommonCommunicationInterfaces;
using CommonTools.Coroutines;
using ExitGames.Client.Photon;
using System;
using System.Collections.Generic;
using PhotonCommonImplementation;
using UnityEngine;

namespace PhotonClientImplementation
{
    public sealed class PhotonPeer : IServerPeer, IPhotonPeerListener, IOperationRequestSender, IEventNotifier, IPeerDisconnectionNotifier, IOperationResponseNotifier
    {
        private readonly ICoroutinesExecuter coroutinesExecuter;

        private readonly PeerConnectionInformation serverConnectionInformation;

        private short requestId;

        private readonly Queue<RawMessageData> eventsBuffer;
        private readonly Queue<Tuple<RawMessageResponseData, short>> operationResponsesBuffer;
        private readonly Queue<BufferOption> optionsBuffer;

        public NetworkTrafficState NetworkTrafficState
        {
            get
            {
                return networkTrafficState;
            }
            set
            {
                if (networkTrafficState == NetworkTrafficState.Paused && value == NetworkTrafficState.Flowing)
                {
                    while (optionsBuffer.Count > 0)
                    {
                        switch (optionsBuffer.Dequeue())
                        {
                            case BufferOption.OperationResponse:
                            {
                                var tuple = operationResponsesBuffer.Dequeue();
                                var operationResponded = OperationResponded;

                                if (operationResponded != null)
                                {
                                    var messageResponseData = tuple.Item1;
                                    var num = (int) tuple.Item2;

                                    operationResponded(messageResponseData, (short) num);
                                }
                                break;
                            }
                            case BufferOption.Event:
                            {
                                var rawMessageData1 = eventsBuffer.Dequeue();
                                var eventRecieved = EventRecieved;

                                if (eventRecieved != null)
                                {
                                    var rawMessageData2 = rawMessageData1;
                                    eventRecieved(rawMessageData2);
                                }
                                break;
                            }
                            default:
                            {
                                throw new ArgumentOutOfRangeException();
                            }
                        }
                    }

                    Debug.Assert(operationResponsesBuffer.Count == 0, "Buffer has more than what flushed");
                    Debug.Assert(eventsBuffer.Count == 0, "Buffer has more than what flushed");
                }

                networkTrafficState = value;
            }
        }
        private NetworkTrafficState networkTrafficState;

        public ExitGames.Client.Photon.PhotonPeer RawPeer { get; private set; }

        public bool IsConnected => RawPeer.PeerState == PeerStateValue.Connected;

        public PeerStateValue State => RawPeer.PeerState;

        public IPeerDisconnectionNotifier PeerDisconnectionNotifier => (IPeerDisconnectionNotifier)this;

        public IOperationRequestSender OperationRequestSender => (IOperationRequestSender)this;
        public IOperationResponseNotifier OperationResponseNotifier => (IOperationResponseNotifier)this;
        public IEventNotifier EventNotifier => (IEventNotifier)this;

        public event Action<RawMessageData> EventRecieved;
        public event Action<RawMessageResponseData, short> OperationResponded;

        public event Action<StatusCode> StatusChanged;
        public event Action<DisconnectReason, string> Disconnected;

        public PhotonPeer(PeerConnectionInformation serverConnectionInformation, ConnectionProtocol connectionProtocol, ICoroutinesExecuter coroutinesExecuter)
        {
            NetworkTrafficState = NetworkTrafficState.Flowing;

            this.serverConnectionInformation = serverConnectionInformation;

            RawPeer = new ExitGames.Client.Photon.PhotonPeer((IPhotonPeerListener)this, connectionProtocol)
            {
                ChannelCount = 4
            };

            if (connectionProtocol == ConnectionProtocol.WebSocket ||
                connectionProtocol == ConnectionProtocol.WebSocketSecure)
            {
                // To support WebGL export in Unity, we find and assign the SocketWebTcpThread or SocketWebTcpCoroutine class (if it's in the project).
                Type websocketType = Type.GetType("ExitGames.Client.Photon.SocketWebTcpThread, Assembly-CSharp", false);
                websocketType = websocketType ?? Type.GetType("ExitGames.Client.Photon.SocketWebTcpThread, Assembly-CSharp-firstpass", false);
                websocketType = websocketType ?? Type.GetType("ExitGames.Client.Photon.SocketWebTcpCoroutine, Assembly-CSharp", false);
                websocketType = websocketType ?? Type.GetType("ExitGames.Client.Photon.SocketWebTcpCoroutine, Assembly-CSharp-firstpass", false);
                /*var websocketType = Type.GetType("ExitGames.Client.Photon.SocketWebTcpCoroutine, Assembly-CSharp",
                    false);
                websocketType = websocketType ??
                                Type.GetType("ExitGames.Client.Photon.SocketWebTcpCoroutine, Assembly-CSharp-firstpass",
                                    false);*/

                if (websocketType != null)
                {
                    RawPeer.SocketImplementation = websocketType;
                }

                RawPeer.DebugOut = DebugLevel.INFO;
            }

            this.coroutinesExecuter = coroutinesExecuter;
            coroutinesExecuter.StartCoroutine(UpdateEngine());

            eventsBuffer = new Queue<RawMessageData>(10);
            operationResponsesBuffer = new Queue<Tuple<RawMessageResponseData, short>>(10);
            optionsBuffer = new Queue<BufferOption>(10);
        }

        private IEnumerator<IYieldInstruction> UpdateEngine()
        {
            while (true)
            {
                Update();
                yield return (IYieldInstruction)null;
            }
        }

        private void Update()
        {
            do { } while (RawPeer.DispatchIncomingCommands());
            do { } while (RawPeer.SendOutgoingCommands());
        }

        public void SetNetworkTrafficState(NetworkTrafficState state)
        {
            NetworkTrafficState = state;
        }

        public void Connect()
        {
            var serverAddress = $"{serverConnectionInformation.Ip}:{serverConnectionInformation.Port}";
            Debug.Assert(RawPeer.Connect(serverAddress, "Game"), $"PhotonPeer::Connect() -> Could not begin connection with: {serverAddress}");
        }

        public void Disconnect()
        {
            if (RawPeer.PeerState == PeerStateValue.Disconnected &&
                RawPeer.PeerState == PeerStateValue.Disconnecting)
            {
                return;
            }

            RawPeer.Disconnect();

            var photonPeer = this;
            coroutinesExecuter.WaitAndDo(photonPeer.WaitForDisconnect().StartCoroutine(coroutinesExecuter), Dispose);
        }

        public void DebugReturn(DebugLevel level, string message)
        {
            switch (level)
            {
                case DebugLevel.ERROR:
                {
                    Debug.LogError($"PhotonPeer::DebugReturn() -> Debug Level: {level} Message: {message}");
                    break;
                }
                case DebugLevel.WARNING:
                {
                    Debug.LogWarning($"PhotonPeer::DebugReturn() -> Debug Level: {level} Message: {message}");
                    break;
                }
                case DebugLevel.INFO:
                {
                    Debug.Log($"PhotonPeer::DebugReturn() -> Debug Level: {level} Message: {message}");
                    break;
                }
                default:
                {
                    Debug.Log($"PhotonPeer::DebugReturn() -> Debug Level: {level} Message: {message}");
                    break;
                }
            }
        }

        public void OnOperationResponse(OperationResponse operationResponse)
        {
            var requestId = operationResponse.ExtractRequestId();
            var parameter = operationResponse.Parameters[0] as byte[];

            var messageResponseData1 = new RawMessageResponseData(operationResponse.OperationCode, parameter, operationResponse.ReturnCode);

            if (NetworkTrafficState == NetworkTrafficState.Paused)
            {
                optionsBuffer.Enqueue(PhotonPeer.BufferOption.OperationResponse);
                operationResponsesBuffer.Enqueue(new Tuple<RawMessageResponseData, short>(messageResponseData1, requestId));
            }
            else
            {
                var operationResponded = OperationResponded;
                if (operationResponded == null)
                {
                    return;
                }

                var messageResponseData2 = messageResponseData1;
                var numberId = (int)requestId;

                operationResponded(messageResponseData2, (short)numberId);
            }
        }

        public void OnStatusChanged(StatusCode statusCode)
        {
            var disconnectReason = 0;

            switch (statusCode)
            {
                case StatusCode.Disconnect:
                    break;
                case StatusCode.TimeoutDisconnect:
                    disconnectReason = 1;
                    break;
                case StatusCode.DisconnectByServer:
                case StatusCode.DisconnectByServerUserLimit:
                case StatusCode.DisconnectByServerLogic:
                    disconnectReason = 4;
                    break;
            }

            Disconnected?.Invoke((DisconnectReason)disconnectReason, statusCode.ToString());
            StatusChanged?.Invoke(statusCode);
        }

        public void OnEvent(EventData eventData)
        {
            var rawMessageData = new RawMessageData(eventData.Code, eventData.Parameters[0] as byte[]);

            if (NetworkTrafficState == NetworkTrafficState.Paused)
            {
                optionsBuffer.Enqueue(BufferOption.Event);
                eventsBuffer.Enqueue(rawMessageData);
            }
            else
            {
                EventRecieved?.Invoke(rawMessageData);
            }
        }

        public void Dispose()
        {
            coroutinesExecuter.Dispose();
        }

        public short Send<TParam>(MessageData<TParam> data, MessageSendOptions sendOptions) 
            where TParam : IParameters, new()
        {
            if (sendOptions.Flush)
            {
                Debug.Log("PhotonPeer::Send() -> SendOptions::Flush is not supported!");
            }

            requestId = (short)(requestId + 1);

            Debug.Assert(RawPeer.OpCustom(
                Utils.ToPhotonOperationRequest(data, requestId), sendOptions.Reliable, sendOptions.ChannelId, sendOptions.Encrypted),
                "PhotonPeer::Send() -> Could not send operation request!");

            return requestId;
        }

        private enum BufferOption
        {
            OperationResponse,
            Event
        }
    }

    public static class PeerUtils
    {
        public static IEnumerator<IYieldInstruction> WaitForDisconnect(this PhotonPeer peer)
        {
            do
            {
                yield return (IYieldInstruction)null;
            }
            while ((uint)peer.State > 0U);
        }

        public static IEnumerator<IYieldInstruction> WaitForConnect(this PhotonPeer peer, Action onConnected, Action onConnectionFailed)
        {
            do
            {
                yield return (IYieldInstruction)null;
            }
            while (peer.State == PeerStateValue.Connecting || peer.State == PeerStateValue.InitializingApplication);

            switch (peer.State)
            {
                case PeerStateValue.Connected:
                {
                    onConnected?.Invoke();
                    break;
                }
                case PeerStateValue.Disconnected:
                {
                    onConnectionFailed?.Invoke();
                    break;
                }
                default:
                {
                    Debug.LogWarning($"PeerUtils::WaitForConnect() -> Unexpected state while waiting for connection: {peer.State}");
                    break;
                }
            }
        }
    }

    public static class Utils
    {
        public static void SetRequestId(this OperationRequest operationRequest, short requestId)
        {
            if (operationRequest.Parameters == null)
            {
                operationRequest.Parameters = new Dictionary<byte, object>();
            }

            PhotonCommonImplementation.Utils.SetRequestId(operationRequest.Parameters, requestId);
        }

        public static short ExtractRequestId(this OperationRequest operationRequest)
        {
            return PhotonCommonImplementation.Utils.ExtractRequestId(operationRequest.Parameters);
        }

        public static OperationRequest ToPhotonOperationRequest<TParam>(MessageData<TParam> request, short requestId) where TParam : IParameters, new()
        {
            var photonParameters = PhotonCommonImplementation.Utils.ToPhotonParameters((IParameters)request.Parameters);
            var operationRequest = new OperationRequest
            {
                OperationCode = request.Code,
                Parameters = photonParameters
            };

            operationRequest.SetRequestId(requestId);

            return operationRequest;
        }

        public static OperationResponse ToPhotonOperationResponse(RawMessageResponseData response, short requestId)
        {
            var photonParameters = PhotonCommonImplementation.Utils.ToPhotonParameters(response.Parameters);
            var operationResponse = new OperationResponse
            {
                OperationCode = response.Code,
                Parameters = photonParameters,
                ReturnCode = response.ReturnCode
            };

            operationResponse.SetRequestId(requestId);

            return operationResponse;
        }

        public static short ExtractRequestId(this OperationResponse operationResponse)
        {
            return ExtractRequestId(operationResponse.Parameters);
        }

        public static void SetRequestId(this OperationResponse operationResponse, short requestId)
        {
            if (operationResponse.Parameters == null)
            {
                operationResponse.Parameters = new Dictionary<byte, object>();
            }

            PhotonCommonImplementation.Utils.SetRequestId(operationResponse.Parameters, requestId);
        }

        private static byte GetAdditionalParameterCodeValue(AdditionalParameterCode parameterCode)
        {
            return (byte)(byte.MaxValue - parameterCode);
        }

        private static short ExtractRequestId(IReadOnlyDictionary<byte, object> parameters)
        {
            var parameterCodeValue = Utils.GetAdditionalParameterCodeValue(AdditionalParameterCode.RequestId);
            Debug.Assert(parameters.ContainsKey(parameterCodeValue), "ExtractRequestId() -> Could not find requestId");
            return (short)parameters[parameterCodeValue];
        }
    }
}