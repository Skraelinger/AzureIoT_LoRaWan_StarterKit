using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer
{

    public class LoraDeviceInfo
    {
        public string DevAddr;
        public string DevEUI;
        public string AppKey;
        public string AppEUI;
        public string NwkSKey;
        public string AppSKey;
        public string PrimaryKey;
        public string AppNounce;
        public string DevNounce;
        public string NetId;
        public bool IsOurDevice = false;
        public bool IsJoinValid = false;
        public IoTHubSender HubSender;
    }

}
