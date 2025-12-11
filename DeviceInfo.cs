using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WindowsBleMesh
{
    public class DeviceInfo
    {
        public string DeviceId { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string ProcessorInfo { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public long TotalMemoryMB { get; set; }
        public string MACAddress { get; set; } = string.Empty;
        public List<string> IPAddresses { get; set; } = new();
        public string MotherboardSerial { get; set; } = string.Empty;
        public string BIOSSerial { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Generates a unique, persistent Device ID based on hardware identifiers
        /// </summary>
        public static string GenerateDeviceId()
        {
            try
            {
                string hardwareString = GetMotherboardSerial() + GetBIOSSerial() + GetMACAddress();
                
                if (string.IsNullOrEmpty(hardwareString))
                {
                    // Fallback to machine name + user profile path
                    hardwareString = Environment.MachineName + Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }

                using (var sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(hardwareString));
                    // Take first 8 bytes for a shorter ID
                    return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToUpperInvariant();
                }
            }
            catch
            {
                // Fallback to GUID-based ID (stored in registry/file for persistence)
                return Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant();
            }
        }

        /// <summary>
        /// Collects all local device information
        /// </summary>
        public static DeviceInfo CollectLocalInfo()
        {
            var info = new DeviceInfo
            {
                DeviceId = GenerateDeviceId(),
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                OSVersion = Environment.OSVersion.ToString(),
                Platform = RuntimeInformation.OSDescription,
                ProcessorCount = Environment.ProcessorCount,
                Timestamp = DateTime.UtcNow
            };

            // Get Processor Info
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    info.ProcessorInfo = obj["Name"]?.ToString() ?? "Unknown";
                    break;
                }
            }
            catch
            {
                info.ProcessorInfo = "Unknown";
            }

            // Get Total Memory
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out ulong totalBytes))
                    {
                        info.TotalMemoryMB = (long)(totalBytes / (1024 * 1024));
                    }
                    break;
                }
            }
            catch
            {
                info.TotalMemoryMB = 0;
            }

            // Get MAC Address
            info.MACAddress = GetMACAddress();

            // Get IP Addresses
            info.IPAddresses = GetIPAddresses();

            // Get Hardware Serials
            info.MotherboardSerial = GetMotherboardSerial();
            info.BIOSSerial = GetBIOSSerial();

            return info;
        }

        private static string GetMACAddress()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up &&
                                         n.NetworkInterfaceType != NetworkInterfaceType.Loopback);
                
                if (nic != null)
                {
                    return BitConverter.ToString(nic.GetPhysicalAddress().GetAddressBytes());
                }
            }
            catch { }
            return string.Empty;
        }

        private static List<string> GetIPAddresses()
        {
            var ips = new List<string>();
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        ips.Add(ip.ToString());
                    }
                }
            }
            catch { }
            return ips;
        }

        private static string GetMotherboardSerial()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (var obj in searcher.Get())
                {
                    return obj["SerialNumber"]?.ToString() ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        private static string GetBIOSSerial()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
                foreach (var obj in searcher.Get())
                {
                    return obj["SerialNumber"]?.ToString() ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// Serialize to JSON for transmission
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = false });
        }

        /// <summary>
        /// Deserialize from JSON
        /// </summary>
        public static DeviceInfo? FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<DeviceInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create a compact summary for BLE transmission (due to size limits)
        /// </summary>
        public string ToCompactString()
        {
            return $"DEV|{DeviceId}|{MachineName}|{UserName}|{Platform}|{MACAddress}|{string.Join(",", IPAddresses)}";
        }

        /// <summary>
        /// Parse compact string format
        /// </summary>
        public static DeviceInfo? FromCompactString(string compact)
        {
            try
            {
                var parts = compact.Split('|');
                if (parts.Length >= 6 && parts[0] == "DEV")
                {
                    return new DeviceInfo
                    {
                        DeviceId = parts[1],
                        MachineName = parts[2],
                        UserName = parts[3],
                        Platform = parts[4],
                        MACAddress = parts[5],
                        IPAddresses = parts.Length > 6 ? parts[6].Split(',').ToList() : new List<string>(),
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
            catch { }
            return null;
        }

        public override string ToString()
        {
            return $"[{DeviceId}] {MachineName} ({UserName}) - {Platform}";
        }
    }

    /// <summary>
    /// Manages discovered devices from the mesh network
    /// </summary>
    public class DeviceRegistry
    {
        private readonly Dictionary<string, DeviceInfo> _devices = new();
        private readonly object _lock = new();

        public event EventHandler<DeviceInfo>? DeviceDiscovered;
        public event EventHandler<DeviceInfo>? DeviceUpdated;

        public void RegisterDevice(DeviceInfo device)
        {
            lock (_lock)
            {
                bool isNew = !_devices.ContainsKey(device.DeviceId);
                _devices[device.DeviceId] = device;

                if (isNew)
                {
                    DeviceDiscovered?.Invoke(this, device);
                }
                else
                {
                    DeviceUpdated?.Invoke(this, device);
                }
            }
        }

        public IEnumerable<DeviceInfo> GetAllDevices()
        {
            lock (_lock)
            {
                return _devices.Values.ToList();
            }
        }

        public DeviceInfo? GetDevice(string deviceId)
        {
            lock (_lock)
            {
                return _devices.TryGetValue(deviceId, out var device) ? device : null;
            }
        }

        public int Count
        {
            get
            {
                lock (_lock) { return _devices.Count; }
            }
        }
    }
}
