﻿using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class IoTHubSender : IDisposable
    {
        private DeviceClient deviceClient;
        
        private string DevEUI;

        private string PrimaryKey;


        public IoTHubSender(string DevEUI, string PrimaryKey)
        {
            this.DevEUI = DevEUI;
            this.PrimaryKey = PrimaryKey;

            CreateDeviceClient();

        }

        private void CreateDeviceClient()
        {
            if (deviceClient == null)
            {
                try
                {

                    string partConnection = createIoTHubConnectionString(false);
                    string deviceConnectionStr = $"{partConnection}DeviceId={DevEUI};SharedAccessKey={PrimaryKey}";

                    deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionStr, TransportType.Mqtt_Tcp_Only);

                    //we set the retry only when sending msgs
                    deviceClient.SetRetryPolicy(new NoRetry());

                    //if the server disconnects dispose the deviceclient and new one will be created when a new d2c msg comes in.
                    deviceClient.SetConnectionStatusChangesHandler((status, reason) =>
                    {
                        if (status == ConnectionStatus.Disconnected)
                        {
                            deviceClient.Dispose();
                            deviceClient = null;
                            Console.WriteLine("Connection closed by the server");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not create IoT Hub Device Client with error: {ex.Message}");
                }

            }
        }

        public async Task SendMessage(string strMessage)
        {

            if (!string.IsNullOrEmpty(strMessage))
            {

                try
                {
                    CreateDeviceClient();

                    //Enable retry for this send message
                    deviceClient.SetRetryPolicy(new ExponentialBackoff(int.MaxValue, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100)));
                    await deviceClient.SendEventAsync(new Message(UTF8Encoding.ASCII.GetBytes(strMessage)));

                    //in future retrive the c2d msg to be sent to the device
                    //var c2dMsg = await deviceClient.ReceiveAsync((TimeSpan.FromSeconds(2)));

                    //disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                    deviceClient.SetRetryPolicy(new NoRetry());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not send message to IoTHub/Edge with error: {ex.Message}");
                }

            }
        }

        private string createIoTHubConnectionString(bool enableGateway)
        {
            string connectionString = string.Empty;

            string hostName = Environment.GetEnvironmentVariable("IOTEDGE_IOTHUBHOSTNAME");
            string gatewayHostName = Environment.GetEnvironmentVariable("IOTEDGE_GATEWAYHOSTNAME");

            if(string.IsNullOrEmpty(hostName))
            {
                Console.WriteLine("Environment variable IOTEDGE_IOTHUBHOSTNAME not found, creation of iothub connection not possible");
            }
            

            connectionString += $"HostName={hostName};";

            if (enableGateway)
                connectionString += $"GatewayHostName={hostName};";
                      
            

            return connectionString;



        }

        public void Dispose()
        {
            if (deviceClient != null)
            {
                try { deviceClient.Dispose(); } catch (Exception ex) { Console.WriteLine($"Device Client disposing error: {ex.Message}"); }
            }
        }
    }
}
