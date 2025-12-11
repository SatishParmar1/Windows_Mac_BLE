using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WindowsBleMesh
{
    public class BlePacket
    {
        public byte MsgId { get; set; }
        public byte Index { get; set; }
        public byte Total { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        public byte[] ToBytes()
        {
            var bytes = new List<byte>();
            bytes.Add(MsgId);
            bytes.Add(Index);
            bytes.Add(Total);
            if (Payload != null)
            {
                bytes.AddRange(Payload);
            }
            return bytes.ToArray();
        }

        public static BlePacket FromBytes(byte[] data)
        {
            if (data == null || data.Length < 3)
                throw new ArgumentException("Invalid packet data");

            var packet = new BlePacket
            {
                MsgId = data[0],
                Index = data[1],
                Total = data[2]
            };

            if (data.Length > 3)
            {
                packet.Payload = new byte[data.Length - 3];
                Array.Copy(data, 3, packet.Payload, 0, data.Length - 3);
            }

            return packet;
        }
    }

    public static class BleFragmentation
    {
        // Standard Legacy: 31 bytes - overhead = ~21 bytes
        // Extended: 255 bytes - overhead = ~240 bytes
        public const int LegacyPayloadSize = 21;
        public const int ExtendedPayloadSize = 240;

        public static List<BlePacket> FragmentData(byte[] data, byte msgId, int maxPayloadSize = LegacyPayloadSize)
        {
            var packets = new List<BlePacket>();
            int totalPackets = (int)Math.Ceiling((double)data.Length / maxPayloadSize);

            if (totalPackets > 255)
                throw new ArgumentException("Data too large for protocol");

            for (int i = 0; i < totalPackets; i++)
            {
                int offset = i * maxPayloadSize;
                int length = Math.Min(maxPayloadSize, data.Length - offset);
                var chunk = new byte[length];
                Array.Copy(data, offset, chunk, 0, length);

                packets.Add(new BlePacket
                {
                    MsgId = msgId,
                    Index = (byte)(i + 1),
                    Total = (byte)totalPackets,
                    Payload = chunk
                });
            }

            return packets;
        }

        public static byte[] ReassembleData(List<BlePacket> packets)
        {
            packets.Sort((a, b) => a.Index.CompareTo(b.Index));
            var data = new List<byte>();  
            foreach (var packet in packets)
            {
                if (packet.Payload != null)
                    data.AddRange(packet.Payload);
            }
            return data.ToArray();
        }
    }
}
