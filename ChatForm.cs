using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;

namespace WindowsBleMesh
{
    public class ChatForm : Form
    {
        private BlePublisher? _publisher;
        private BleWatcher? _watcher;
        private ListBox _messageList;
        private TextBox _inputBox;
        private Button _sendButton;

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
        }

        private async Task InitializeBleAsync()
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null)
            {
                AddMessage("Error: No Bluetooth Adapter found on this device.");
                AddMessage("Please ensure your PC has Bluetooth and it is turned ON.");
                _inputBox.Enabled = false;
                _sendButton.Enabled = false;
                return;
            }

            if (!adapter.IsLowEnergySupported)
            {
                AddMessage("Error: Your Bluetooth adapter does not support Low Energy (BLE).");
                return;
            }

            _publisher = new BlePublisher();
            _publisher.Log += OnLog;
            
            _watcher = new BleWatcher();
            _watcher.MessageReceived += OnMessageReceived;
            _watcher.Log += OnLog;
            _watcher.Start();
            
            AddMessage("System: Bluetooth initialized successfully.");
            AddMessage("System: Listening for messages...");
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
        }

        private async void SendButton_Click(object? sender, EventArgs e)
        {
            string text = _inputBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (_publisher == null) 
            {
                AddMessage("Error: Bluetooth not initialized.");
                return;
            }

            _inputBox.Text = "";
            AddMessage($"Me: {text}");

            try
            {
                await _publisher.PublishMessageAsync(text);
            }
            catch (Exception ex)
            {
                AddMessage($"Error sending: {ex.Message}");
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
            _watcher?.Stop();
            base.OnFormClosing(e);
        }
    }
}
