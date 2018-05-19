using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Linq;
using System.Text;

namespace PacketManager
{
    #region LoRaPayloadWrapper
    public abstract class LoRaPayloadWrapper
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
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public  LoRaPayloadWrapper(byte[] inputMessage)
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
        public LoRaPayloadWrapper()
        {
           
        }



    }
    #endregion

    #region LoRaPayloadDownlinkWrapper
    public abstract class LoRaPayloadDownlinkWrapper:LoRaPayloadWrapper
    {

        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaPayloadDownlinkWrapper(byte[] inputMessage):base(inputMessage)
        {
    
        }

        /// <summary>
        /// This is used for downlink, when we need to compute those fields
        /// </summary>
        public LoRaPayloadDownlinkWrapper()
        {

        }

        public abstract string EncryptPayload(string appSkey);

        public abstract byte[] CalculateMic(string nwskey);

    }
    #endregion


    #region LoRaPayloadDownlinkWrapper
    public abstract class LoRaPayloadUplinkWrapper : LoRaPayloadWrapper
    {
    

        /// <summary>
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaPayloadUplinkWrapper(byte[] inputMessage) : base(inputMessage)
        {

        }

        /// <summary>
        /// This is used for downlink, when we need to compute those fields
        /// </summary>
        public LoRaPayloadUplinkWrapper()
        {

        }

        public abstract string DecryptPayload(string appSkey);

        public abstract bool CheckMic(string nwskey);

    }
    #endregion


    #region LoRaPayloadJoinRequest
    public class LoRaPayloadJoinRequest : LoRaPayloadUplinkWrapper
    {

        //aka JoinEUI
        public byte[] appEUI;
        public byte[] devEUI;
        public byte[] devNonce;

        public LoRaPayloadJoinRequest(byte[] inputMessage) : base(inputMessage)
        {
            //TODO implement a table to store a mapping with joinEUI-DevNonce

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
    /// the body of an Uplink message
    /// </summary>
    public class LoRaPayloadUplink : LoRaPayloadUplinkWrapper
    {
        /// <summary>
        /// Device MAC Address
        /// </summary>
        public byte[] devAddr;
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
            var checkDir=(mhdr[0] >> 5);
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
    public class LoRaPayloadJoinAccept : LoRaPayloadDownlinkWrapper
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
        /// Assigned Dev Address
        /// </summary>
        public byte[] devAddr;

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

        public LoRaPayloadJoinAccept(string _netId,string appKey) 
        {
            appNonce = new byte[3];
            netID = new byte[3];
            devAddr = new byte[4];
            dlSettings = new byte[1];
            rxDelay = new byte[1];
            cfList = null ;
            //set payload Wrapper fields
            mhdr = new byte[1];
            appNonce = BitConverter.GetBytes(0);
            netID = StringToByteArray(_netId);
            devAddr = StringToByteArray("00000000");
            //default param 869.525 MHz / DR0 (SF12, 125 kHz)  
            dlSettings = BitConverter.GetBytes(0);
            //cfList = StringToByteArray("1F84B9E25D9C4115F02EEA0B3DD3E20B");
            cfList = null;
            //todo implement a fctn? Could be error reason
            fcnt = BitConverter.GetBytes(0x00);
            mhdr = BitConverter.GetBytes(0x20);
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
            if (cfList!=null)
                frmpayload = appNonce.Concat(netID).Concat(devAddr).Concat(rfu).Concat(rxDelay).Concat(cfList).Concat(mic).ToArray();
            else
                frmpayload = appNonce.Concat(netID).Concat(devAddr).Concat(rfu).Concat(rxDelay).Concat(mic).ToArray();

            byte[] sBlock = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            int size = 12 +( cfList!=null?cfList.Length:0);
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
                
                
            byte[] result = new byte[16];
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            result = MacUtilities.DoFinal(mac);
            mic = result.Take(4).ToArray();
            return mic;
        }

        public byte[] getFinalMessage()
        {
            
            var downlinkmsg = new DownlinkPktFwdMessage(Convert.ToBase64String(rawMessage));
            return Encoding.Default.GetBytes(JsonConvert.SerializeObject(downlinkmsg));
        }
    }

    public class DownlinkPktFwdMessage
    {
        public Txpk txpk;

        public DownlinkPktFwdMessage(string _data)
        {
            txpk = new Txpk()
            {
                imme = true,
                data = _data,
                size = (uint)(_data.Length * 3 / 4)
            };
        }
    }

    public class Txpk
    {
        public bool imme;
        public string data;
        public uint size; 
    }
    #endregion
    #region LoRaMetada
    public class LoRaMetada
    {
        public byte[] gatewayMacAddress { get; set; }
        public dynamic fullPayload { get; set; }
        public string rawB64data { get; set; }
        public string devaddr { get; set; }
        public string decodedData { get; set; }
        public bool processed { get; set; }
        public string devAddr { get; set; }

        public LoRaMetada(byte[] input)
        {
            gatewayMacAddress = input.Skip(4).Take(6).ToArray();
            var c = BitConverter.ToString(gatewayMacAddress);
            var payload = Encoding.Default.GetString(input.Skip(12).ToArray());

            //check for correct message
            if (payload.Count() == 0)
            {
                processed = false;
                return;
            }

            fullPayload = JObject.Parse(payload);
            //check for correct message
            //TODO have message types
            if (fullPayload.rxpk == null || fullPayload.rxpk[0].data == null)
            {
                processed = false;
                return;
            }
            rawB64data = Convert.ToString(fullPayload.rxpk[0].data);

            processed = true;

            //get the address
            byte[] addrbytes = new byte[4];
            Array.Copy(input, 1, addrbytes, 0, 4);
            //address correct but inversed
            Array.Reverse(addrbytes);
            devAddr = BitConverter.ToString(addrbytes);
        }
    }
    #endregion
    /// <summary>
    /// class exposing usefull message stuff
    /// </summary>
    public class LoRaMessage
    {
        public LoRaPayloadWrapper payloadMessage;
        public LoRaMetada lorametadata;


        /// <summary>
        /// This contructor is used in case of uplink message, hence we don't know the message type yet
        /// </summary>
        /// <param name="inputMessage"></param>
        public LoRaMessage(byte[] inputMessage)
        {
            lorametadata = new LoRaMetada(inputMessage);
            //set up the parts of the raw message   
            byte[] convertedInputMessage = Convert.FromBase64String(lorametadata.rawB64data);
            var messageType = convertedInputMessage[0]>>5;

            //Uplink Message
            if (messageType == 2)
               payloadMessage = new LoRaPayloadUplink(convertedInputMessage);
            if (messageType == 0)
                payloadMessage = new LoRaPayloadJoinRequest(convertedInputMessage);

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
        public LoRaMessage(byte[] messagePayload, int type)
        {
            //construct a Join Accept Message
            if(type == 1)
            {

            }
         

        }

        public bool CheckMic(string nwskey)
        {
            return ((LoRaPayloadUplinkWrapper)payloadMessage).CheckMic(nwskey);
        }

        public  string DecryptPayload(string appSkey)
        {
            var retValue = ((LoRaPayloadUplinkWrapper)payloadMessage).DecryptPayload(appSkey);
            lorametadata.decodedData = retValue;
            return retValue;
        }


    }

 
}
