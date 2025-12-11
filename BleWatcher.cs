using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth.Advertisement;

namespace WindowsBleMesh
{
    public class BleWatcher
    {
        private readonly BluetoothLEAdvertisementWatcher _watcher;
        private readonly ushort _companyId;
        
        // MsgId -> (Total, ReceivedPackets)
        private readonly Dictionary<byte, Dictionary<byte, BlePacket>> _messageBuffer = new();
        private readonly HashSet<byte> _completedMessages = new(); // To avoid re-processing same message

        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> Log;

        public BleWatcher(ushort companyId = 0x1234)
        {
            _companyId = companyId;
            _watcher = new BluetoothLEAdvertisementWatcher();
            _watcher.ScanningMode = BluetoothLEScanningMode.Active;
            _watcher.Received += OnAdvertisementReceived;
        }

        public void Start()
        {
            Log?.Invoke(this, "Watcher: Starting scan...");
            _watcher.Start();
        }

        public void Stop()
        {
            Log?.Invoke(this, "Watcher: Stopping scan...");
            _watcher.Stop();
        }

        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            // Log?.Invoke(this, $"Watcher: Adv received from {args.BluetoothAddress:X} RSSI: {args.RawSignalStrengthInDBm}");
            foreach (var manufacturerData in args.Advertisement.ManufacturerData)
            {
                if (manufacturerData.CompanyId == _companyId)
                {
                    try
                    {
                        byte[] data = manufacturerData.Data.ToArray();
                        var packet = BlePacket.FromBytes(data);
                        Log?.Invoke(this, $"Watcher: Packet received MsgId:{packet.MsgId} Idx:{packet.Index}/{packet.Total}");
                        ProcessPacket(packet);
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke(this, $"Watcher: Error parsing packet: {ex.Message}");
                    }
                }
            }
        }

        private void ProcessPacket(BlePacket packet)
        {
            lock (_messageBuffer)
            {
                if (_completedMessages.Contains(packet.MsgId))
                    return; // Already processed this message

                if (!_messageBuffer.ContainsKey(packet.MsgId))
                {
                    _messageBuffer[packet.MsgId] = new Dictionary<byte, BlePacket>();
                }

                var fragments = _messageBuffer[packet.MsgId];
                if (!fragments.ContainsKey(packet.Index))
                {
                    fragments[packet.Index] = packet;
                    // Console.WriteLine($"Received Packet {packet.Index}/{packet.Total} for MsgId: {packet.MsgId:X2}");

                    if (fragments.Count == packet.Total)
                    {
                        // All packets received
                        CompleteMessage(packet.MsgId, fragments.Values.ToList());
                    }
                }
            }
        }

        private void CompleteMessage(byte msgId, List<BlePacket> packets)
        {
            try
            {
                byte[] encryptedData = BleFragmentation.ReassembleData(packets);
                string message = BleSecurity.Decrypt(encryptedData);

                MessageReceived?.Invoke(this, message);

                _completedMessages.Add(msgId);
                _messageBuffer.Remove(msgId);
            }
            catch (Exception ex)
            {
                Log?.Invoke(this, $"Watcher: Error decrypting message {msgId:X2}: {ex.Message}");
            }
        }
    }
}
