**Project:  LoRaWAN Gateway and Network Server on IoT Edge**

**Participants:**  Ronnie Saurenmann (Project Creator), Mikhail Chatillon, Todd Holmquist-Sutherland (Lead PM), Hong Bu (PM), Bart Jansen, Bret Stateham, Paul Foster, Sergiy Poplavsky, Tobias Weisserth

**Challenge Statement**

LoRaWan is a type of wireless telecommunication wide area network that is gaining quite a bit of momentum right now. It is designed to allow long range communications at a low bit rate among things (connected objects), such as sensors operated on a battery. The key advantage of this technology is to communicate wirelessly over up to 15 kilometers with a standard gateway and with very low battery consumption. A simple IoT device with a single sensor, equipped with LoRa and connected to a 9-volt battery, should last up to 10 years. Another big advantage is that you can create your own network “similar” in how you would set up a WiFi network.

Network topology is of star-of-stars type, with the leaf sensors sending data to gateways forwarding it to the internet. Nowadays, even for simple scenarios like having 10 devices connected to a single LoRaWan gateway (hardware with antenna), you need to connect your gateway to an Network Server (provided by vendors like Loriot, Senet, The Thing Network, etc…) and then work through their connectors to integrate the gateways and devices with IoT Hub. The idea of this hack is to have all the basic required functionality (LoRa packet forwarder, LoRa network server, etc.) running as IoT Edge modules on a LoRaWan enabled Raspberry Pi. This enables customers to connect LoRaWan devices to a single inexpensive gateway and send the messages to IoT Hub without any license fee and intermediary servers.  We believe this will enable enterprises and service providers requiring smaller deployments to take advantage of LoRaWAN for use cases (TO BE EXPLORED IN LEVERAGE PLAN) that would have been infeasible before for either cost or time-to-market reasons.  Additionally, through Azure Functions, the decoding of the device message payload can happen at the Edge, thereby simplifying the IoT Hub ingestion pipeline.

**Further Notes on the Challenge**

In a recent IoT hack (Berlin), it took 3 IoT savvy engineers 3 days to get the gateway setup with the Lora provider!  Customers need an easier onboarding experience for using/developing with LoRaWAN connected devices.  We want to show customers, equipment vendors (and possibly network providers) how that is possible by leveraging Azure IoT and Edge!

We will package the LoRa packet forwarder and network server into Edge modules.

Kit that we will use: http://wiki.seeed.cc/LoRa_LoRaWan_Gateway_Kit/

It can be plugged on top of this template: https://github.com/loriot/AzureSolutionTemplate 

 

**Key Questions/Expected Learnings**

1.  Can we eliminate the need for installing/setting up a dedicated Lora network server box in the edge environment (outside of the device, using IoT Edge)?
2.  How will the Lora network providers react?  Will we find any customers/willing partners among them?  They target telcos/large enterprises who need complex asset and network management features.  How relevant are the intermediate/small use cases, either as an adoption driver for Azure IoT, or as quick-start/emulation mode for the network providers?
3.  Can we package the OSS Lora packet forwarder into an IoT Edge module? Can we drive the configuration of the packet forwarder from the Cloud through IoT Hub?
4.  Can we achieve one-click installation?
5.  How easy can we make it to manage Lora-connected devices in IoT Hub? (Similar ease of use to standard DSL router, plug it in at home, and simply send to IoT Hub.)
6.  Lora network server and packet forwarder are complex and not well-documented. How simple can we make this—to enable simple evaluation/demo use case?  Do we need something lighter weight for the packet forwarder or Lora network server? Should we write a minimalist network server in .NET core as lighter weight version?
7.  Can we achieve in-Edge payload decoding and be sufficiently fast to react within a message frame time?

 

**Target Deliverables**

Reference implementation that LoRaWAN infrastructure vendors (gateway providers) and end customers can use to get started, consisting of:

1. LoRa network server and packet forwarder in a Docker container running as IoT Edge modules.

2. Whitepaper with all components and all config steps needed to demonstrate (1); published in GitHub

3. (stretch for SyncWeek) ARM Template to support a “Deploy to Azure” of the above setup with one-click setup;instructions for RaspPi setup via documentation

4. (stretch for SyncWeek) Provision pre-configured image for RaspPi to further simplify the setup process.

   

##### Work Breakdown for SyncWeek Hack

1.  Running C module (semtech packet forwarder) and Node.Js module (Network Gateway) directly, meaning not containerized, on RaspPi--get them sending messages to an IoT Edge module on the RaspPi (done)
2.  Take the C packet forwarder and run it in a container as IoT Edge Module*
3.  Write a minimalistic network server (gateway) with ABP (Activation by Personalization) support in .NET Core as IoT Edge Module*. If possible multiplex Lora devices with identity translation as described in the Major Open Items chapter
4.  Write a component for Azure Function that do the Decryption* and MIC validation that can run on both IoT Edge and on Azure (hard-wire DevAddr and required NwkSKey, AppSKey Lora keys). (NOTE: this step can be done in parallel to above steps)
5.  Enable Lora configuration in Device Twin to manage the registration and access control of Lora connected devices (Lora keys). Ideally it should also provide support for OTAA. (NOTE: this step can be done in parallel to above steps)
6.  Create payload decoding function and connect to Loriot template (earlier CSE project) – end-to-end demo setup to show data coming in and visualization (NOTE: this step can be done in parallel to above steps)
7.  Write some Arduino code to read the various sensors and send it through LoRa (NOTE: this step can be done in parallel to above steps)

*some early prototyping done

###### Optional work if we have time (can be all done in parallel):
1.  Implement on point 2 a configuration system where you can change setting on IoT Hub and is pushed down it to the packet forwarder config file globa_config.json. Ideally it should also pull the right packet_forwarder and compile for the target platform.
2.  Implement on point 3 OTAA (Over the Air Activation)
3.  Run the components done in point 3, 4 on a Azure Container to enable commercial gateways to talk directly to IoT Hub
4.  Implement on point 4 a frame counter (FCntUp) validation
5.  Implement C2D Lora messaging 
6.  Implement on ASA (Azure Stream Analytics) message deduplication to allow multiple gateways support
7.  ARM Template

 

#### **Leverage Plan**

**Competitive/Alternative Technologies**

None that we know of. SmartHome/Swisscom customer has implemented a concentrator that exemplifies the pattern (though it only works for their specific sensor which has custom firmware and only work with 1 channel).

**Advantages**

Decrypt and decode on the edge, to enable taking action on the edge, e.g., based on sensor values.
Very easy to set up for simple use cases with a pluggable interface to gateway providers for more complex use cases.

**Stakeholders and Onboarding Strategies**

Telcos offer connectivity and equipment as a bundle. Customers pay for device and bandwidth similar to mobile phone contracts.

LoRaWAN infrastructure vendors like Loriot, etc.  You buy hardware independently and connect to their service.

**Major Open Items**

Currently IoT Edge supports modules in .NET Core, Python, ASA, ML and Functions. There are OSS versions of the network server and decryption components in Python and they would be a possible candidate to run as Edge module. Unfortunately, currently IoT Edge does not support Python Module on ARM chip. The best solution is to write (port) these components to .NET Core. Most packet forwarders are all forks of the semtech version: https://github.com/Lora-net/packet_forwarder, although it would be possible to port this part to .NET too, it makes more sense to try to use this one to maximize the compatibility with the various lora HW gateway.

Considering these IoT Edge patterns: https://docs.microsoft.com/en-us/azure/iot-edge/iot-edge-as-gateway 
Should we implement the whole as protocol translation pattern (much simpler) but all the message are showing up in IoT Hub as coming from the gateway and not for the original device? Or should we implement it as Identity Translator patter to have device identity? To note that there are no examples how to implement this pattern. We could use some of the idea of the gateway code that we have publish here: https://github.com/fbeltrao/IoTHubGateway

**Next Steps**

**Ecosystem Analysis**



