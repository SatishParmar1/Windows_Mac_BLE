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

        public BlePublisher(ushort companyId = 0x1234)
        {
            _companyId = companyId;
            _publisher = new BluetoothLEAdvertisementPublisher();
        }

        public async Task PublishMessageAsync(string message, int durationPerPacketMs = 200, int repetitions = 3)
        {
            Console.WriteLine($"Preparing to send: {message}");

            // 1. Encrypt
            byte[] encryptedData = BleSecurity.Encrypt(message);

            // 2. Fragment
            // Generate a random MsgId
            byte msgId = (byte)new Random().Next(0, 256);
            List<BlePacket> packets = BleFragmentation.FragmentData(encryptedData, msgId);

            Console.WriteLine($"Message split into {packets.Count} packets. MsgId: {msgId:X2}");

            // 3. Advertise
            for (int r = 0; r < repetitions; r++)
            {
                Console.WriteLine($"Cycle {r + 1}/{repetitions}...");
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
            Console.WriteLine("Transmission complete.");
        }
    }
}
