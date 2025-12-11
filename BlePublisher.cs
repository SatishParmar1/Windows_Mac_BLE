using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace WindowsBleMesh
{
    public class BlePublisher
    {
        private readonly BluetoothLEAdvertisementPublisher _publisher;
        private readonly ushort _companyId;
        private static byte _nextMsgId = (byte)new Random().Next(0, 255); // Start with random ID to avoid history collisions on receiver

        public event EventHandler<string> Log;

        public BlePublisher(ushort companyId = 0x1234)
        {
            _companyId = companyId;
            _publisher = new BluetoothLEAdvertisementPublisher();
            
            // Enable Extended Advertising (Bluetooth 5.0+)
            try 
            {
                _publisher.UseExtendedAdvertisement = true;
            }
            catch { /* Feature might not be supported on older Windows builds */ }

            _publisher.StatusChanged += (sender, args) => 
            {
                Log?.Invoke(this, $"Publisher Status: {args.Status} Error: {args.Error}");
            };
        }

        public async Task PublishMessageAsync(string message, int durationPerPacketMs = 100, int repetitions = 3)
        {
            Log?.Invoke(this, $"Publisher: Preparing to send '{message}'");

            // 1. Encrypt
            // byte[] encryptedData = BleSecurity.Encrypt(message);
            byte[] encryptedData = System.Text.Encoding.UTF8.GetBytes(message); // DEBUG: Plain Text for visibility

            // 2. Fragment
            // Use Rolling MsgId (0-255) as per spec
            byte msgId = _nextMsgId++;
            
            // Determine payload size based on advertising mode
            int payloadSize = _publisher.UseExtendedAdvertisement 
                ? BleFragmentation.ExtendedPayloadSize 
                : BleFragmentation.LegacyPayloadSize;

            List<BlePacket> packets = BleFragmentation.FragmentData(encryptedData, msgId, payloadSize);

            Log?.Invoke(this, $"Publisher: Split into {packets.Count} packets (Size: {payloadSize}). MsgId: {msgId:X2}");

            // 3. Advertise
            for (int r = 0; r < repetitions; r++)
            {
                // Console.WriteLine($"Cycle {r + 1}/{repetitions}...");
                foreach (var packet in packets)
                {
                    var writer = new DataWriter();
                    writer.WriteBytes(packet.ToBytes());

                    var manufacturerData = new BluetoothLEManufacturerData
                    {
                        CompanyId = _companyId,
                        Data = writer.DetachBuffer()
                    };

                    _publisher.Advertisement.ManufacturerData.Clear();
                    _publisher.Advertisement.ManufacturerData.Add(manufacturerData);

                    _publisher.Start();
                    // Console.WriteLine($"Broadcasting Packet {packet.Index}/{packet.Total}");
                    await Task.Delay(durationPerPacketMs);
                    _publisher.Stop();
                }
            }
            Log?.Invoke(this, "Publisher: Transmission complete.");
        }
    }
}
