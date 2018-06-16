using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PacketManager
{

    /// <summary>
    /// The Physical Payload wrapper
    /// </summary>
    public class PhysicalPayload
    {

        //case of inbound messages
        public PhysicalPayload(byte[] input)
        {

            protocolVersion = input[0];
            Array.Copy(input, 1, token, 0, 2);
            identifier = input[3];

            //PUSH_DATA That packet type is used by the gateway mainly to forward the RF packets received, and associated metadata, to the server
            if (identifier == 0x00)
            {
                Array.Copy(input, 4, gatewayIdentifier, 0, 8);
                message = new byte[input.Length - 12];
                Array.Copy(input, 12, message, 0, input.Length - 12);
            }

            //PULL_DATA That packet type is used by the gateway to poll data from the server.
            if (identifier == 0x02)
            {
                Array.Copy(input, 4, gatewayIdentifier, 0, 8);
            }

            //TX_ACK That packet type is used by the gateway to send a feedback to the to inform if a downlink request has been accepted or rejected by the gateway.
            if (identifier == 0x05)
            {
                Array.Copy(input, 4, gatewayIdentifier, 0, 8);
                Array.Copy(input, 12, message, 0, input.Length - 12);
            }
        }

        //downlink transmission
        public PhysicalPayload(byte[] _token, byte type, byte[] _message)
        {
            //0x01 PUSH_ACK That packet type is used by the server to acknowledge immediately all the PUSH_DATA packets received.
            //0x04 PULL_ACK That packet type is used by the server to confirm that the network route is open and that the server can send PULL_RESP packets at any time.
            if (type == 0x01 || type == 0x04)
            {
                token = _token;
                identifier = type;
            }

            //0x03 PULL_RESP That packet type is used by the server to send RF packets and  metadata that will have to be emitted by the gateway.
            if (identifier == 0x03)
            {
                token = _token;
                identifier = type;
                Array.Copy(_message, 0, message, 0, _message.Length);
            }

        }

        //1 byte
        public byte protocolVersion = 2;
        //1-2 bytes
        public byte[] token =new byte[2];
        //1 byte
        public byte identifier;
        //8 bytes
        public byte[] gatewayIdentifier = new byte[8];
        //0-unlimited
        public byte[] message;

        public byte[] GetMessage()
        {
            List<byte> returnList = new List<byte>();
            returnList.Add(protocolVersion);
            returnList.AddRange(token);
            returnList.Add(identifier);
            returnList.AddRange(gatewayIdentifier);
            returnList.AddRange(message);
            return returnList.ToArray();
        }
    }
    public class Txpk
    {
        public bool imme;
        public string data;
        public uint size;
        public double freq; //868
        public uint rfch;
        public string modu;
        public string datr;
        public string codr;
        public uint powe;
    }

    public class Rxpk
    {
        public string time;
        public uint tmms;
        public uint tmst;
        public double freq; //868
        public uint chan;
        public uint rfch;
        public int stat;
        public string modu;
        public string datr;
        public string codr;
        public int rssi;
        public float lsnr;
        public uint size;
        public string data;
    }

    #region LoRaGenericPayload
    /// <summary>
    /// The LoRaPayloadWrapper class wraps all the information any LoRa message share in common
    /// </summary>
    public abstract class LoRaGenericPayload
    {
        /// <summary>
        /// raw byte of the message
        /// </summary>
        public byte[] rawMessage;
        /// <summary>
        /// MACHeader of the message
        /// </summary>
        public byte[] mhdr;

        /// <summary>
        /// Message Integrity Code
        /// </summary>
        public byte[] mic;


        /// <summary>
        /// Assigned Dev Address
        /// </summary>
        public byte[] devAddr;



        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaGenericPayload(byte[] inputMessage)
        {
            rawMessage = inputMessage;
            //get the mhdr
            byte[] mhdr = new byte[1];
            Array.Copy(inputMessage, 0, mhdr, 0, 1);
            this.mhdr = mhdr;

            //MIC 4 last bytes
            byte[] mic = new byte[4];
            Array.Copy(inputMessage, inputMessage.Length - 4, mic, 0, 4);
            this.mic = mic;
        }

        /// <summary>
        /// This is used for downlink, The field will be computed at message creation
        /// </summary>
        public LoRaGenericPayload()
        {

        }



    }
    #endregion

    #region LoRaDownlinkPayload
    /// <summary>
    /// Common class for all the Downlink LoRa Messages.
    /// </summary>
    public abstract class LoRaDownlinkPayload : LoRaGenericPayload
    {

        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding.
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaDownlinkPayload(byte[] inputMessage) : base(inputMessage)
        {

        }

        /// <summary>
        /// This is used for downlink, when we need to compute those fields
        /// TODO change this constructor as appropriate
        /// </summary>
        public LoRaDownlinkPayload()
        {

        }

        /// <summary>
        /// Method to calculate the encrypted version of the payload
        /// </summary>
        /// <param name="appSkey">the Application Secret Key</param>
        /// <returns></returns>
        public abstract string EncryptPayload(string appSkey);

        /// <summary>
        /// A Method to calculate the Mic of the message
        /// </summary>
        /// <param name="nwskey">The Network Secret Key</param>
        /// <returns></returns>
        public abstract byte[] CalculateMic(string nwskey);

        /// <summary>
        /// Method to take the different fields and assemble them in the message bytes
        /// </summary>
        /// <returns></returns>
        public abstract byte[] ToMessage();

    }
    #endregion

    #region LoRaUplinkPayload
    /// <summary>
    /// Common class for all the Uplink LoRa Messages.
    /// </summary>
    public abstract class LoRaUplinkPayload : LoRaGenericPayload
    {
        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaUplinkPayload(byte[] inputMessage) : base(inputMessage)
        {

        }

        /// <summary>
        /// This is used for downlink, when we need to compute those fields
        /// </summary>
        public LoRaUplinkPayload()
        {

        }

        /// <summary>
        /// Method to Deccrypt payloads.
        /// </summary>
        /// <param name="appSkey">The Application Secret Key</param>
        /// <returns></returns>
        public abstract string DecryptPayload(string appSkey);


        /// <summary>
        /// Method to check a Mic
        /// </summary>
        /// <param name="nwskey">The Network Secret Key</param>
        /// <returns></returns>
        public abstract bool CheckMic(string nwskey);

    }
    #endregion

    #region LoRaPayloadJoinRequest
    /// <summary>
    /// Implementation of the Join Request message type.
    /// </summary>
    public class LoRaPayloadJoinRequest : LoRaUplinkPayload
    {

        //aka JoinEUI
        public byte[] appEUI;
        public byte[] devEUI;
        public byte[] devNonce;

        public LoRaPayloadJoinRequest(byte[] inputMessage) : base(inputMessage)
        {
              //TODO Clean up debug fields
            var inputmsgstr = BitConverter.ToString(inputMessage);
            //get the joinEUI field
            appEUI = new byte[8];
            Array.Copy(inputMessage, 1, appEUI, 0, 8);

            var appEUIStr = BitConverter.ToString(appEUI);
            //get the DevEUI
            devEUI = new byte[8];
            Array.Copy(inputMessage, 9, devEUI, 0, 8);

            var devEUIStr = BitConverter.ToString(devEUI);
            //get the DevNonce
            devNonce = new byte[2];
            Array.Copy(inputMessage, 17, devNonce, 0, 2);

            var devNonceStr = BitConverter.ToString(devNonce);

        }

        public override bool CheckMic(string AppKey)
        {
            //appEUI = StringToByteArray("526973696E674846");
            IMac mac = MacUtilities.GetMac("AESCMAC");

            KeyParameter key = new KeyParameter(StringToByteArray(AppKey));
            mac.Init(key);
            var appEUIStr = BitConverter.ToString(appEUI);
            var devEUIStr = BitConverter.ToString(devEUI);
            var devNonceStr = BitConverter.ToString(devNonce);

            var micstr = BitConverter.ToString(mic);

            var algoinput = mhdr.Concat(appEUI).Concat(devEUI).Concat(devNonce).ToArray();
            byte[] result = new byte[19];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            var resStr = BitConverter.ToString(result);
            return mic.SequenceEqual(result.Take(4).ToArray());
        }

        public override string DecryptPayload(string appSkey)
        {
            throw new NotImplementedException("The payload is not encrypted in case of a join message");
        }

        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
    #endregion
    #region LoRaPayloadUplink
    /// <summary>
    /// the body of an Uplink (normal) message
    /// </summary>
    public class LoRaPayloadUplink : LoRaUplinkPayload
    {
      
        /// <summary>
        /// Frame control octet
        /// </summary>
        public byte[] fctrl;
        /// <summary>
        /// Frame Counter
        /// </summary>
        public byte[] fcnt;
        /// <summary>
        /// Optional frame
        /// </summary>
        public byte[] fopts;
        /// <summary>
        /// Port field
        /// </summary>
        public byte[] fport;
        /// <summary>
        /// MAC Frame Payload Encryption 
        /// </summary>
        public byte[] frmpayload;


        /// <summary>
        /// get message direction
        /// </summary>
        public int direction;
        public bool processed;


        /// <param name="inputMessage"></param>
        public LoRaPayloadUplink(byte[] inputMessage) : base(inputMessage)
        {

            //get direction
            var checkDir = (mhdr[0] >> 5);
            //in this case the payload is not downlink of our type

            if (checkDir != 2)
            {
                processed = false;
                return;
            }
            processed = true;
            direction = (mhdr[0] & (1 << 6 - 1));

            //get the address
            byte[] addrbytes = new byte[4];
            Array.Copy(inputMessage, 1, addrbytes, 0, 4);
            //address correct but inversed
            Array.Reverse(addrbytes);
            this.devAddr = addrbytes;

            //Fctrl Frame Control Octet
            byte[] fctrl = new byte[1];
            Array.Copy(inputMessage, 5, fctrl, 0, 1);
            byte optlength = new byte();
            int foptsSize = (optlength << 4) >> 4;
            this.fctrl = fctrl;

            //Fcnt
            byte[] fcnt = new byte[2];
            Array.Copy(inputMessage, 6, fcnt, 0, 2);
            this.fcnt = fcnt;

            //FOpts
            byte[] fopts = new byte[foptsSize];
            Array.Copy(inputMessage, 8, fopts, 0, foptsSize);
            this.fopts = fopts;

            //Fport can be empty if no commands! 
            byte[] fport = new byte[1];
            Array.Copy(inputMessage, 8 + foptsSize, fport, 0, 1);
            this.fport = fport;

            //frmpayload
            byte[] FRMPayload = new byte[inputMessage.Length - 9 - 4 - foptsSize];
            Array.Copy(inputMessage, 9 + foptsSize, FRMPayload, 0, inputMessage.Length - 9 - 4 - foptsSize);
            this.frmpayload = FRMPayload;

        }

        /// <summary>
        /// Method to check if the mic is valid
        /// </summary>
        /// <param name="nwskey">the network security key</param>
        /// <returns></returns>
        public override bool CheckMic(string nwskey)
        {
            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(StringToByteArray(nwskey));
            mac.Init(key);
            byte[] block = { 0x49, 0x00, 0x00, 0x00, 0x00, (byte)direction, (byte)(devAddr[3]), (byte)(devAddr[2]), (byte)(devAddr[1]),
                (byte)(devAddr[0]),  fcnt[0] , fcnt[1],0x00, 0x00, 0x00, (byte)(rawMessage.Length-4) };
            var algoinput = block.Concat(rawMessage.Take(rawMessage.Length - 4)).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            return mic.SequenceEqual(result.Take(4).ToArray());
        }

        /// <summary>
        /// src https://github.com/jieter/python-lora/blob/master/lora/crypto.py
        /// </summary>
        public override string DecryptPayload(string appSkey)
        {
            AesEngine aesEngine = new AesEngine();
            aesEngine.Init(true, new KeyParameter(StringToByteArray(appSkey)));

            byte[] aBlock = { 0x01, 0x00, 0x00, 0x00, 0x00, (byte)direction, (byte)(devAddr[3]), (byte)(devAddr[2]), (byte)(devAddr[1]),
                (byte)(devAddr[0]),(byte)(fcnt[0]),(byte)(fcnt[1]),  0x00 , 0x00, 0x00, 0x00 };

            byte[] sBlock = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            int size = frmpayload.Length;
            byte[] decrypted = new byte[size];
            byte bufferIndex = 0;
            short ctr = 1;
            int i;
            while (size >= 16)
            {
                aBlock[15] = (byte)((ctr) & 0xFF);
                ctr++;
                aesEngine.ProcessBlock(aBlock, 0, sBlock, 0);
                for (i = 0; i < 16; i++)
                {
                    decrypted[bufferIndex + i] = (byte)(frmpayload[bufferIndex + i] ^ sBlock[i]);
                }
                size -= 16;
                bufferIndex += 16;
            }
            if (size > 0)
            {
                aBlock[15] = (byte)((ctr) & 0xFF);
                aesEngine.ProcessBlock(aBlock, 0, sBlock, 0);
                for (i = 0; i < size; i++)
                {
                    decrypted[bufferIndex + i] = (byte)(frmpayload[bufferIndex + i] ^ sBlock[i]);
                }
            }
            return Encoding.Default.GetString(decrypted);
        }

        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
    #endregion
    #region LoRaPayloadJoinAccept
    /// <summary>
    /// Implementation of a LoRa Join-Accept frame
    /// </summary>
    public class LoRaPayloadJoinAccept : LoRaDownlinkPayload
    {
        /// <summary>
        /// Server Nonce aka JoinNonce
        /// </summary>
        public byte[] appNonce;

        /// <summary>
        /// Device home network aka Home_NetId
        /// </summary>
        public byte[] netID;

        /// <summary>
        /// DLSettings
        /// </summary>
        public byte[] dlSettings;

        /// <summary>
        /// RxDelay
        /// </summary>
        public byte[] rxDelay;

        /// <summary>
        /// CFList / Optional
        /// </summary>
        public byte[] cfList;

        /// <summary>
        /// Frame Counter
        /// </summary>
        public byte[] fcnt;

        public LoRaPayloadJoinAccept(string _netId, string appKey, byte[] _devAddr)
        {
            appNonce = new byte[3];
            netID = new byte[3];
            devAddr = _devAddr;
            dlSettings = new byte[1];
            rxDelay = new byte[1];
            cfList = null;
            //set payload Wrapper fields
            mhdr = new byte[] { 32};
            Random rnd = new Random();
            rnd.NextBytes(appNonce);
            netID = StringToByteArray(_netId);
            //default param 869.525 MHz / DR0 (SF12, 125 kHz)  
            dlSettings = BitConverter.GetBytes(0);
            //TODO Implement
            cfList = null;
            fcnt = BitConverter.GetBytes(0x01);
           
            CalculateMic(appKey);
            EncryptPayload(appKey);
        }

        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public override string EncryptPayload(string appSkey)
        {
            //return null;
            AesEngine aesEngine = new AesEngine();
            aesEngine.Init(true, new KeyParameter(StringToByteArray(appSkey)));
            byte[] rfu = new byte[1];
            rfu[0] = 0x0;
            //downlink direction
            byte direction = 0x01;
            byte[] aBlock = { 0x01, 0x00, 0x00, 0x00, 0x00, direction, (byte)(devAddr[3]), (byte)(devAddr[2]), (byte)(devAddr[1]),
                (byte)(devAddr[0]),(byte)(fcnt[0]),(byte)(fcnt[1]),  0x00 , 0x00, 0x00, 0x00 };
            byte[] frmpayload;
            if (cfList != null)
                frmpayload = appNonce.Concat(netID).Concat(devAddr).Concat(rfu).Concat(rxDelay).Concat(cfList).Concat(mic).ToArray();
            else
                frmpayload = appNonce.Concat(netID).Concat(devAddr).Concat(rfu).Concat(rxDelay).Concat(mic).ToArray();

            byte[] sBlock = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            int size = 12 + (cfList != null ? cfList.Length : 0);
            byte[] decrypted = new byte[size];
            byte bufferIndex = 0;
            short ctr = 1;
            int i;
            while (size >= 16)
            {
                aBlock[15] = (byte)((ctr) & 0xFF);
                ctr++;
                aesEngine.ProcessBlock(aBlock, 0, sBlock, 0);
                for (i = 0; i < 16; i++)
                {
                    decrypted[bufferIndex + i] = (byte)(frmpayload[bufferIndex + i] ^ sBlock[i]);
                }
                size -= 16;
                bufferIndex += 16;
            }
            if (size > 0)
            {
                aBlock[15] = (byte)((ctr) & 0xFF);
                aesEngine.ProcessBlock(aBlock, 0, sBlock, 0);
                for (i = 0; i < size; i++)
                {
                    decrypted[bufferIndex + i] = (byte)(frmpayload[bufferIndex + i] ^ sBlock[i]);
                }
            }
            rawMessage = decrypted;
            return Encoding.Default.GetString(decrypted);
        }

        public override byte[] CalculateMic(string appKey)
        {

            IMac mac = MacUtilities.GetMac("AESCMAC");
            KeyParameter key = new KeyParameter(StringToByteArray(appKey));
            mac.Init(key);
            byte[] rfu = new byte[1];
            rfu[0] = 0x0;

            var algoinput = mhdr.Concat(appNonce).Concat(netID).Concat(devAddr).Concat(rfu).Concat(rxDelay).ToArray();
            if (cfList != null)
                algoinput = algoinput.Concat(cfList).ToArray();
            byte[] msgLength = BitConverter.GetBytes(algoinput.Length);

            byte direction = 0x01;
            byte[] aBlock = { 0x49, 0x00, 0x00, 0x00, 0x00, direction, (byte)(devAddr[3]), (byte)(devAddr[2]), (byte)(devAddr[1]),
                (byte)(devAddr[0]),(byte)(fcnt[0]),(byte)(fcnt[1]),  0x00 , 0x00, 0x00, msgLength[0] };
            algoinput = aBlock.Concat(algoinput).ToArray();
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            mic = result.Take(4).ToArray();
            return mic;
        }

        public byte[] getFinalMessage(byte [] token)
        {

            var downlinkmsg = new DownlinkPktFwdMessage(Convert.ToBase64String(rawMessage));
            var messageBytes = Encoding.Default.GetBytes(JsonConvert.SerializeObject(downlinkmsg));
            PhysicalPayload message = new PhysicalPayload(token,0x03,messageBytes);
            return message.GetMessage();
        }

        public override byte[] ToMessage()
        {
            List<byte> messageArray = new List<Byte>();
            messageArray.AddRange(mhdr);
            messageArray.AddRange(rawMessage);
            //messageArray.AddRange(mic);
    
            return messageArray.ToArray();
        }
    }
    #endregion

    #region LoRaMetada

    /// <summary>
    /// Metadata about a Lora Packet, featuring a Lora Packet, the payload and the data.
    /// </summary>
    public class LoRaMetada
    {

        public PktFwdMessage fullPayload { get; set; }
        public string rawB64data { get; set; }
        public string decodedData { get; set; }



        /// <summary>
        /// Case of Uplink message. 
        /// </summary>
        /// <param name="input"></param>
        public LoRaMetada(byte[] input)
        {  
            var payload = Encoding.Default.GetString(input);
            Console.WriteLine(payload);
            var payloadObject = JsonConvert.DeserializeObject<UplinkPktFwdMessage>(payload);
            fullPayload = payloadObject;
            //TODO to this in a loop.
            rawB64data = payloadObject.rxpk[0].data;
       
        }

        /// <summary>
        /// Case of Downlink message. TODO refactor this
        /// </summary>
        /// <param name="input"></param>
        public LoRaMetada(LoRaGenericPayload payloadMessage, LoRaMessageType tmp)
        {
           rawB64data = Convert.ToBase64String(((LoRaPayloadJoinAccept)payloadMessage).ToMessage());

        }
    }
    #endregion

    #region LoRaMessage
    public enum LoRaMessageType
    {
        JoinRequest,
        JoinAccept,
        UnconfirmedDataUp,
        UnconfirmedDataDown,
        ConfirmedDataUp,
        ConfirmedDataDown,
        RFU,
        Proprietary
    }
    /// <summary>
    /// class exposing usefull message stuff
    /// </summary>
    public class LoRaMessage
    {
        public bool processed = false;
        public LoRaGenericPayload payloadMessage;
        public LoRaMetada loraMetadata;
        public PhysicalPayload physicalPayload;

        /// <summary>
        /// This contructor is used in case of uplink message, hence we don't know the message type yet
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaMessage(byte[] inputMessage)
        {
            //packet normally sent by the gateway as heartbeat. TODO find more elegant way to integrate.
            if (inputMessage.Length > 12 && inputMessage.Length!=111)
            {
                processed = true;
                physicalPayload = new PhysicalPayload(inputMessage);
                loraMetadata = new LoRaMetada(physicalPayload.message);
                //set up the parts of the raw message   
                byte[] convertedInputMessage = Convert.FromBase64String(loraMetadata.rawB64data);
                var messageType = convertedInputMessage[0] >> 5;

                //Uplink Message
                if (messageType == 2)
                    payloadMessage = new LoRaPayloadUplink(convertedInputMessage);
                if (messageType == 0)
                    payloadMessage = new LoRaPayloadJoinRequest(convertedInputMessage);
            }
            else
            {
                Console.WriteLine(BitConverter.ToString(inputMessage));
                processed = false;
            }

        }

        /// <summary>
        /// This contructor is used in case of downlink message
        /// </summary>
        /// <param name="inputMessage"></param>
        /// <param name="type">
        /// 0 = Join Request
        /// 1 = Join Accept
        /// 2 = Unconfirmed Data up
        /// 3 = Unconfirmed Data down
        /// 4 = Confirmed Data up
        /// 5 = Confirmed Data down
        /// 6 = Rejoin Request</param>
        public LoRaMessage(LoRaGenericPayload payload, LoRaMessageType type,byte[] physicalToken)
        {
            //construct a Join Accept Message
            if (type == LoRaMessageType.JoinAccept)
            {
                payloadMessage = (LoRaPayloadJoinAccept)payload;
                loraMetadata = new LoRaMetada(payloadMessage, type);
     
                physicalPayload = new PhysicalPayload(physicalToken,0x03,Encoding.Default.GetBytes(loraMetadata.rawB64data));
                physicalPayload.message = ((LoRaPayloadJoinAccept)payload).getFinalMessage(physicalToken);

            }
            else if (type == LoRaMessageType.UnconfirmedDataDown)
            {
                throw new NotImplementedException();
            }
            else if (type == LoRaMessageType.ConfirmedDataDown)
            {
                throw new NotImplementedException();
            }

        }

        /// <summary>
        /// Method to map the Mic check to the appropriate implementation.
        /// </summary>
        /// <param name="nwskey">The Neetwork Secret Key</param>
        /// <returns>a boolean telling if the MIC is valid or not</returns>
        public bool CheckMic(string nwskey)
        {
            return ((LoRaUplinkPayload)payloadMessage).CheckMic(nwskey);
        }

        /// <summary>
        /// Method to decrypt payload to the appropriate implementation.
        /// </summary>
        /// <param name="nwskey">The Application Secret Key</param>
        /// <returns>a boolean telling if the MIC is valid or not</returns>
        public string DecryptPayload(string appSkey)
        {
            var retValue = ((LoRaUplinkPayload)payloadMessage).DecryptPayload(appSkey);
            loraMetadata.decodedData = retValue;
            return retValue;
        }
    }
    #endregion

    #region PacketForwarder

    /// <summary>
    /// Base type of a Packet Forwarder message (lower level)
    /// </summary>
    public class PktFwdMessage
    {
        PktFwdType pktFwdType;
    }


    enum PktFwdType
    {
        Downlink,
        Uplink
    }

    /// <summary>
    /// JSON of a Downlink message for the Packet forwarder.
    /// </summary>
    public class DownlinkPktFwdMessage : PktFwdMessage
    {
        public Txpk txpk;


        //TODO change values to match network.
        public DownlinkPktFwdMessage(string _data)
        {
            txpk = new Txpk()
            {
                imme = true,
                data = _data,
                size = (uint)(_data.Length * 3 / 4),
                freq = 868.100000,
                rfch = 0,
                modu = "LORA",
                datr = "SF11BW125",
                codr = "4/5",
                powe = 14

            };
        }
    }


    /// <summary>
    /// an uplink Json for the packet forwarder.
    /// </summary>
    public class UplinkPktFwdMessage : PktFwdMessage
    {
        public List<Rxpk> rxpk = new List<Rxpk>();
    }


    #endregion
}
