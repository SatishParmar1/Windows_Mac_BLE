using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WindowsBleMesh
{
    public class UdpMesh
    {
        private const int Port = 12345;
        private readonly UdpClient _udpClient;
        private bool _listening;

        public event EventHandler<string>? MessageReceived;
        public event EventHandler<string>? Log;

        public UdpMesh()
        {
            _udpClient = new UdpClient();
            _udpClient.EnableBroadcast = true;
            // Bind to all interfaces
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
        }

        public void Start()
        {
            if (_listening) return;
            _listening = true;
            Log?.Invoke(this, "UDP: Starting listener on port " + Port);
            Task.Run(ListenLoop);
        }

        public void Stop()
        {
            _listening = false;
            _udpClient.Close();
        }

        public async Task BroadcastMessageAsync(string message)
        {
            try
            {
                // We can reuse the BleSecurity encryption if we want, or send plain for now.
                // Let's use the same encryption to be consistent.
                byte[] encryptedData = BleSecurity.Encrypt(message);
                
                // Prefix with a "Magic Byte" to distinguish from other traffic if needed, 
                // but for now just send the encrypted bytes.
                
                await _udpClient.SendAsync(encryptedData, encryptedData.Length, new IPEndPoint(IPAddress.Broadcast, Port));
                Log?.Invoke(this, $"UDP: Broadcasted {encryptedData.Length} bytes.");
            }
            catch (Exception ex)
            {
                Log?.Invoke(this, $"UDP: Error broadcasting: {ex.Message}");
            }
        }

        private async Task ListenLoop()
        {
            while (_listening)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    // Filter out our own packets if possible? 
                    // UDP Broadcasts are received by the sender too on some interfaces.
                    // We can filter by IP, but let's just decrypt and see.

                    byte[] data = result.Buffer;
                    try
                    {
                        string message = BleSecurity.Decrypt(data);
                        // If decryption succeeds, it's likely our message.
                        MessageReceived?.Invoke(this, message);
                        Log?.Invoke(this, $"UDP: Received message from {result.RemoteEndPoint}");
                    }
                    catch (Exception ex)
                    {
                        // Decryption failed, probably not our packet or garbage.
                        Log?.Invoke(this, $"UDP: Decryption failed from {result.RemoteEndPoint}: {ex.Message}");
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log?.Invoke(this, $"UDP: Receive error: {ex.Message}");
                    await Task.Delay(1000); // Backoff
                }
            }
        }
    }
}
