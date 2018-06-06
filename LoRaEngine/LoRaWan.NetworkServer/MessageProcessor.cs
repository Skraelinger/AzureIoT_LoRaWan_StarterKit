using Newtonsoft.Json.Linq;
using PacketManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class MessageProcessor : IDisposable
    {
        string testKey = "8AFE71A145B253E49C3031AD068277A1";
        string testDeviceId = "BE7A00000000888F";
        const int HubRetryCount = 10;
        IoTHubSender sender = null;

        public async Task processMessage(byte[] message, string connectionString)
        {
            LoRaMessage loraMessage = new LoRaMessage(message);
            byte[] messageToSend = new Byte[0];
            if (!loraMessage.isLoRaMessage)
            {
                if (loraMessage.physicalPayload.identifier == PhysicalIdentifier.PULL_DATA)
                {
                    PhysicalPayload pullAck = new PhysicalPayload(loraMessage.physicalPayload.token,PhysicalIdentifier.PULL_ACK,null);
                     messageToSend=pullAck.GetMessage();
                    Console.WriteLine("Pull Ack sent");
                }
            }
            else
            {
                if (loraMessage.loRaMessageType == LoRaMessageType.JoinRequest)
                {
                    Console.WriteLine("Join Request Received");
                    Random rnd = new Random();
                    byte[] appNonce = new byte[3];
                    byte[] netId = new byte[3] { 0,0,0};
                    byte[] devAddr = new byte[4] { 0,0,0,1};
                    netId = StringToByteArray("000024");
                    devAddr = StringToByteArray("4917E265");
                    
                    rnd.NextBytes(appNonce);
                    LoRaPayloadJoinAccept loRaPayloadJoinAccept = new LoRaPayloadJoinAccept(
                        //NETID 0 / 1 is default test 
                        BitConverter.ToString(netId).Replace("-",""),
                        //todo add app key management
                        testKey,
                        //todo add device address management
                        devAddr ,
                        appNonce
                        );
                    var _datr = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].datr;
                    uint _rfch= ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].rfch;
                    double _freq= ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].freq;
                    long _tmst = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].tmst;
                    LoRaMessage joinAcceptMessage = new LoRaMessage(loRaPayloadJoinAccept, LoRaMessageType.JoinAccept, loraMessage.physicalPayload.token
                        ,_datr,0, _freq, _tmst);
                    messageToSend =joinAcceptMessage.physicalPayload.GetMessage();

                    Console.WriteLine("Join Accept sent");
                    Console.WriteLine(BitConverter.ToString(messageToSend));

                }else if (loraMessage.loRaMessageType == LoRaMessageType.JoinAccept)
                {
                    Console.WriteLine("join accept message received");
                    Console.WriteLine(BitConverter.ToString(message));

                    //loraMessage.payloadMessage.devAddr;
                }
                //normal message
                else
                {
                    Console.WriteLine($"Processing message from device: {BitConverter.ToString(loraMessage.payloadMessage.devAddr)}");

                    Shared.loraKeysList.TryGetValue(BitConverter.ToString(loraMessage.payloadMessage.devAddr), out LoraKeys loraKeys);

                    if (loraMessage.CheckMic(testKey))
                    {
                        string decryptedMessage = null;
                        try
                        {
                            decryptedMessage = loraMessage.DecryptPayload(testKey);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to decrypt message: {ex.Message}");
                        }

                        if (string.IsNullOrEmpty(decryptedMessage))
                        {
                            return;
                        }

                        PhysicalPayload pushAck = new PhysicalPayload(loraMessage.physicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                        messageToSend = pushAck.GetMessage();
                        Console.WriteLine($"Sending message '{decryptedMessage}' to hub...");

                        int hubSendCounter = 1;
                        while (HubRetryCount != hubSendCounter)
                        {
                            try
                            {
                                sender = new IoTHubSender(connectionString, testDeviceId);
                                await sender.sendMessage(decryptedMessage);
                                hubSendCounter = HubRetryCount;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to send message: {ex.Message}");
                                hubSendCounter++;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Check MIC failed! Message will be ignored...");
                    }
                }

            }
            var debug = BitConverter.ToString(messageToSend);
            //send reply
            await UdpServer.SendMessage(messageToSend);
                }

        public void Dispose()
        {
            if(sender != null)
            {
                try { sender.Dispose(); } catch (Exception ex) { Console.WriteLine($"IoT Hub Sender disposing error: {ex.Message}"); }
            }
        }

        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}
