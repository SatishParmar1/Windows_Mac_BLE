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
        
        // Address -> MsgId -> (Index -> Packet)
        private readonly Dictionary<ulong, Dictionary<byte, Dictionary<byte, BlePacket>>> _messageBuffer = new();
        
        // Address -> Set of completed MsgIds
        private readonly Dictionary<ulong, HashSet<byte>> _completedMessages = new(); 
        // Address -> Queue for history management
        private readonly Dictionary<ulong, Queue<byte>> _completedMessagesQueue = new();
        
        private const int MaxHistory = 50;

        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> Log;

        public BleWatcher(ushort companyId = 0x1234)
        {
            _companyId = companyId;
            _watcher = new BluetoothLEAdvertisementWatcher();
            _watcher.ScanningMode = BluetoothLEScanningMode.Active;
            _watcher.Received += OnAdvertisementReceived;
            _watcher.Stopped += (s, e) => Log?.Invoke(this, $"Watcher: Stopped. Error: {e.Error}");
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
            // Log every packet as requested ("open connection every message")
            // Log?.Invoke(this, $"Watcher: RX {args.BluetoothAddress:X} RSSI: {args.RawSignalStrengthInDBm}");

            foreach (var manufacturerData in args.Advertisement.ManufacturerData)
            {
                if (manufacturerData.CompanyId == _companyId)
                {
                    Log?.Invoke(this, $"Watcher: Found Target Company ID {manufacturerData.CompanyId:X4} from {args.BluetoothAddress:X}");
                    try
                    {
                        byte[] data = manufacturerData.Data.ToArray();
                        var packet = BlePacket.FromBytes(data);
                        
                        // DEBUG: Try to read payload as text
                        string debugPayload = System.Text.Encoding.UTF8.GetString(packet.Payload);
                        Log?.Invoke(this, $"Watcher: Packet MsgId:{packet.MsgId:X2} Idx:{packet.Index}/{packet.Total} Content: '{debugPayload}'");
                        
                        ProcessPacket(packet, args.BluetoothAddress);
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke(this, $"Watcher: Error parsing packet: {ex.Message}");
                    }
                }
            }
        }

        private void ProcessPacket(BlePacket packet, ulong address)
        {
            lock (_messageBuffer)
            {
                // Initialize structures for this address if needed
                if (!_completedMessages.ContainsKey(address))
                {
                    _completedMessages[address] = new HashSet<byte>();
                    _completedMessagesQueue[address] = new Queue<byte>();
                }

                if (_completedMessages[address].Contains(packet.MsgId))
                    return; // Already processed this message from this sender

                if (!_messageBuffer.ContainsKey(address))
                {
                    _messageBuffer[address] = new Dictionary<byte, Dictionary<byte, BlePacket>>();
                }

                var senderBuffer = _messageBuffer[address];

                if (!senderBuffer.ContainsKey(packet.MsgId))
                {
                    senderBuffer[packet.MsgId] = new Dictionary<byte, BlePacket>();
                }

                var fragments = senderBuffer[packet.MsgId];
                if (!fragments.ContainsKey(packet.Index))
                {
                    fragments[packet.Index] = packet;
                    
                    if (fragments.Count == packet.Total)
                    {
                        // All packets received
                        CompleteMessage(packet.MsgId, fragments.Values.ToList(), address);
                    }
                }
            }
        }

        private void CompleteMessage(byte msgId, List<BlePacket> packets, ulong address)
        {
            try
            {
                byte[] encryptedData = BleFragmentation.ReassembleData(packets);
                // string message = BleSecurity.Decrypt(encryptedData);
                string message = System.Text.Encoding.UTF8.GetString(encryptedData); // DEBUG: Plain Text

                MessageReceived?.Invoke(this, message);

                // Update duplicate detection history for this sender
                var completedSet = _completedMessages[address];
                var completedQueue = _completedMessagesQueue[address];

                if (!completedSet.Contains(msgId))
                {
                    completedSet.Add(msgId);
                    completedQueue.Enqueue(msgId);
                    
                    if (completedQueue.Count > MaxHistory)
                    {
                        byte oldId = completedQueue.Dequeue();
                        completedSet.Remove(oldId);
                    }
                }

                if (_messageBuffer.ContainsKey(address))
                {
                    _messageBuffer[address].Remove(msgId);
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke(this, $"Watcher: Error reassembling/decrypting: {ex.Message}");
            }
        }
    }
}
