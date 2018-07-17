using Microsoft.Azure.Devices.Client;
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
        //string testKey = "2B7E151628AED2A6ABF7158809CF4F3C";
        //string testDeviceId = "BE7A00000000888F";
        //private static UInt16 counter=1;

        private DateTime startTimeProcessing;
     

        public async Task processMessage(byte[] message)
        {
            startTimeProcessing = DateTime.Now;

            LoRaMessage loraMessage = new LoRaMessage(message);

            byte[] udpMsgForPktForwarder = new Byte[0];

            if (!loraMessage.isLoRaMessage)
            {
                udpMsgForPktForwarder = ProcessNonLoraMessage(loraMessage);
            }
            else
            {
                //join message
                if (loraMessage.loRaMessageType == LoRaMessageType.JoinRequest)
                {
                    udpMsgForPktForwarder = await ProcessJoinRequest(loraMessage);

                }

                //normal message
                else if( loraMessage.loRaMessageType ==LoRaMessageType.UnconfirmedDataUp || loraMessage.loRaMessageType == LoRaMessageType.ConfirmedDataUp)
                {
                    udpMsgForPktForwarder = await ProcessLoraMessage(loraMessage);

                }

            }
          

            //send reply to pktforwarder
            await UdpServer.UdpSendMessage(udpMsgForPktForwarder);
        }

        private byte[] ProcessNonLoraMessage(LoRaMessage loraMessage)
        {
            byte[] udpMsgForPktForwarder = new byte[0];
            if (loraMessage.physicalPayload.identifier == PhysicalIdentifier.PULL_DATA)
            {
               

                PhysicalPayload pullAck = new PhysicalPayload(loraMessage.physicalPayload.token, PhysicalIdentifier.PULL_ACK, null);

                udpMsgForPktForwarder = pullAck.GetMessage();

            }

            return udpMsgForPktForwarder;
        }
        private async Task<byte[]> ProcessLoraMessage(LoRaMessage loraMessage)
        {
            byte[] udpMsgForPktForwarder = new byte[0];
            string devAddr = BitConverter.ToString(loraMessage.payloadMessage.devAddr).Replace("-", "");

            Console.WriteLine($"Processing message from device: {devAddr}");          

            Cache.TryGetValue(devAddr, out LoraDeviceInfo loraDeviceInfo);

           

            if (loraDeviceInfo == null)
            {
                Console.WriteLine("No cache");

                loraDeviceInfo = await LoraDeviceInfoManager.GetLoraDeviceInfoAsync(devAddr);         

                Cache.AddToCache(devAddr, loraDeviceInfo);

            }
            else
            {
                Console.WriteLine("From cache");
              
            }

      

            if (loraDeviceInfo.IsOurDevice)
            {
             

                if (loraMessage.CheckMic(loraDeviceInfo.NwkSKey))
                {


                    if (loraDeviceInfo.HubSender == null)
                    {

                        loraDeviceInfo.HubSender = new IoTHubSender(loraDeviceInfo.DevEUI, loraDeviceInfo.PrimaryKey);

                    }

                    //start checking for new c2d message asap 
                    Message c2dMsg = await loraDeviceInfo.HubSender.GetMessageAsync(TimeSpan.FromMilliseconds(10));

                   

                    //todo ronnie double check the fcnt logic
                    UInt16 fcntup = BitConverter.ToUInt16(((LoRaPayloadStandardData)loraMessage.payloadMessage).fcnt, 0);

                    //todo ronnie add tollernace range
                    //check if the frame counter is valid: either is above the server one or is an ABP device resetting the counter (relaxed seqno checking)
                    if (fcntup > loraDeviceInfo.FCntUp || (fcntup==1 && String.IsNullOrEmpty(loraDeviceInfo.AppEUI)))
                    {
                        Console.WriteLine($"Valid frame counter, msg: {fcntup} server: {loraDeviceInfo.FCntUp}");
                        loraDeviceInfo.FCntUp = fcntup;

                        string decryptedMessage = null;
                        try
                        {
                            decryptedMessage = loraMessage.DecryptPayload(loraDeviceInfo.AppSKey);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to decrypt message: {ex.Message}");
                        }



                        Rxpk rxPk = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0];


                        dynamic fullPayload = JObject.FromObject(rxPk);

                        string jsonDataPayload = LoraDecoders.DecodeMessage(decryptedMessage);

                        fullPayload.data = JObject.Parse(jsonDataPayload);
                        fullPayload.EUI = loraDeviceInfo.DevEUI;
                        fullPayload.edgets = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;


                        string iotHubMsg = fullPayload.ToString(Newtonsoft.Json.Formatting.None);



                        Console.WriteLine($"Sending message '{jsonDataPayload}' to hub...");

                        await loraDeviceInfo.HubSender.SendMessageAsync(iotHubMsg);
                        
                       


                    }
                    else
                    {
                        Console.WriteLine($"Invalid frame counter, msg: {fcntup} server: {loraDeviceInfo.FCntUp}");
                    }

                    //check again for messages just before sending the ack
                    if (c2dMsg == null)
                        c2dMsg = await loraDeviceInfo.HubSender.GetMessageAsync(TimeSpan.FromMilliseconds(10));

                    byte[] bytesC2dMsg = null;
                    //check if we got a c2d message to be added in the ack message
                    if (c2dMsg != null)
                    {
                        bytesC2dMsg = c2dMsg.GetBytes();
                        if (bytesC2dMsg != null)
                            Console.WriteLine($"C2D message: {Encoding.UTF8.GetString(bytesC2dMsg)}");
                    }


                    //if no confirmation and no downstream messages send ack only
                    if (loraMessage.loRaMessageType == LoRaMessageType.UnconfirmedDataUp && c2dMsg ==null )
                    {

                        PhysicalPayload pushAck = new PhysicalPayload(loraMessage.physicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);
                        udpMsgForPktForwarder = pushAck.GetMessage();

                        _ = loraDeviceInfo.HubSender.UpdateFcntAsync(loraDeviceInfo.FCntUp, null);

                    } 
                    //if confirmation or cloud to device msg send down the message
                    else if (loraMessage.loRaMessageType == LoRaMessageType.ConfirmedDataUp || c2dMsg!=null )
                    {

                        //increase the fcnt down and save it to iot hub twins
                        loraDeviceInfo.FCntDown++;

                        Console.WriteLine($"Down frame counter: {loraDeviceInfo.FCntDown}");

                        //Saving both fcnts to twins
                        _= loraDeviceInfo.HubSender.UpdateFcntAsync(loraDeviceInfo.FCntUp, loraDeviceInfo.FCntDown);


                        var _datr = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].datr;

                        uint _rfch = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].rfch;

                        double _freq = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].freq;

                        long _tmst = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].tmst;

                        Byte[] devAddrCorrect = new byte[4];
                        Array.Copy(loraMessage.payloadMessage.devAddr, devAddrCorrect, 4);
                        Array.Reverse(devAddrCorrect);

                        

                        LoRaPayloadStandardData ackLoRaMessage = new LoRaPayloadStandardData(StringToByteArray("A0"),
                            devAddrCorrect,
                             new byte[1] { 32 },
                             BitConverter.GetBytes(loraDeviceInfo.FCntDown)
                             ,
                            null,
                            null,
                            bytesC2dMsg
                            ,
                            1);


                        var s = ackLoRaMessage.PerformEncryption(loraDeviceInfo.AppSKey);
                        ackLoRaMessage.SetMic(loraDeviceInfo.NwkSKey);



                        byte[] rndToken = new byte[2];
                        Random rnd = new Random();
                        rnd.NextBytes(rndToken);
                        LoRaMessage ackMessage = new LoRaMessage(ackLoRaMessage, LoRaMessageType.ConfirmedDataDown, rndToken, _datr, 0, _freq, _tmst);
                     
                        udpMsgForPktForwarder = ackMessage.physicalPayload.GetMessage();

                        if (c2dMsg != null)
                            _ = loraDeviceInfo.HubSender.CompleteAsync(c2dMsg);



                    }

                }
                else
                {
                    Console.WriteLine("Check MIC failed! Device will be ignored from now on...");
                    loraDeviceInfo.IsOurDevice = false;
                }

            }
            else
            {
                Console.WriteLine($"Ignore message because is not our device");
            }

            Console.WriteLine($"Total processing time: {DateTime.Now - startTimeProcessing}");

            return udpMsgForPktForwarder;
        }

       

        private async Task<byte[]> ProcessJoinRequest(LoRaMessage loraMessage)
        {
            Console.WriteLine("Join Request Received");

            byte[] udpMsgForPktForwarder = new Byte[0];

            LoraDeviceInfo joinLoraDeviceInfo;

            var joinReq = (LoRaPayloadJoinRequest)loraMessage.payloadMessage;

            Array.Reverse(joinReq.devEUI);
            Array.Reverse(joinReq.appEUI);

            string devEui = BitConverter.ToString(joinReq.devEUI).Replace("-", "");
            string devNonce = BitConverter.ToString(joinReq.devNonce).Replace("-", "");

            //checking if this devnonce was already processed or the deveui was already refused
            Cache.TryGetValue(devEui, out joinLoraDeviceInfo);


            //we have a join request in the cache
            if (joinLoraDeviceInfo != null)
            {

               
                //it is not our device so ingore the join
                if (!joinLoraDeviceInfo.IsOurDevice)
                {
                    Console.WriteLine("Join Request refused the device is not ours");
                    return null;
                }
                //is our device but the join was not valid
                else if (!joinLoraDeviceInfo.IsJoinValid)
                {
                    //if the devNonce is equal to the current it is a potential replay attck
                    if (joinLoraDeviceInfo.DevNonce == devNonce)
                    {
                        Console.WriteLine("Join Request refused devNonce already used");
                        return null;
                    }
                }

            }

            

            joinLoraDeviceInfo = await LoraDeviceInfoManager.PerformOTAAAsync(devEui, BitConverter.ToString(joinReq.appEUI).Replace("-", ""), devNonce);

            if (joinLoraDeviceInfo.IsJoinValid)
            {

                byte[] appNonce = StringToByteArray(joinLoraDeviceInfo.AppNonce);

                byte[] netId = StringToByteArray(joinLoraDeviceInfo.NetId);

               

                byte[] devAddr = StringToByteArray(joinLoraDeviceInfo.DevAddr);

                string appKey = joinLoraDeviceInfo.AppKey;

                Array.Reverse(netId);
                Array.Reverse(appNonce);

                LoRaPayloadJoinAccept loRaPayloadJoinAccept = new LoRaPayloadJoinAccept(
                    //NETID 0 / 1 is default test 
                    BitConverter.ToString(netId).Replace("-", ""),
                    //todo add app key management
                    appKey,
                    //todo add device address management
                    devAddr,
                    appNonce
                    );

                var _datr = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].datr;

                uint _rfch = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].rfch;

                double _freq = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].freq;

                long _tmst = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].tmst;

                LoRaMessage joinAcceptMessage = new LoRaMessage(loRaPayloadJoinAccept, LoRaMessageType.JoinAccept, loraMessage.physicalPayload.token, _datr, 0, _freq, _tmst);

                udpMsgForPktForwarder = joinAcceptMessage.physicalPayload.GetMessage();

                //todo ronnie this should be saved back to the server too
                joinLoraDeviceInfo.FCntUp = 0;
                joinLoraDeviceInfo.FCntDown = 0;

                //add to cache for processing normal messages. This awioids one additional call to the server.
                Cache.AddToCache(joinLoraDeviceInfo.DevAddr, joinLoraDeviceInfo);

                Console.WriteLine("Join Accept sent");
                  
             }

            //add to cache to avoid replay attack, btw server side does the check too.
            Cache.AddToCache(devEui, joinLoraDeviceInfo);

            return udpMsgForPktForwarder;
        }

        private byte[] StringToByteArray(string hex)
        {

            return Enumerable.Range(0, hex.Length)

                             .Where(x => x % 2 == 0)

                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))

                             .ToArray();

        }

        public void Dispose()
        {

        }
    }
}
