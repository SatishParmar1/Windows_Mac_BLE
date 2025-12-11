import SwiftUI
import CoreBluetooth

struct ContentView: View {
    @StateObject private var chatModel = ChatViewModel()
    @State private var messageText = ""
    
    var body: some View {
        HSplitView {
            // Messages Panel
            VStack {
                Text("macOS BLE Mesh Chat")
                    .font(.title)
                    .padding()
                
                Text("Device ID: \(chatModel.localDeviceId)")
                    .font(.caption)
                    .foregroundColor(.secondary)
                
                ScrollViewReader { proxy in
                    ScrollView {
                        LazyVStack(alignment: .leading, spacing: 8) {
                            ForEach(chatModel.messages) { msg in
                                Text(msg.text)
                                    .padding(8)
                                    .background(msg.isMe ? Color.blue.opacity(0.2) : Color.gray.opacity(0.2))
                                    .cornerRadius(8)
                                    .frame(maxWidth: .infinity, alignment: msg.isMe ? .trailing : .leading)
                                    .id(msg.id)
                            }
                        }
                        .padding()
                    }
                    .onChange(of: chatModel.messages.count) { _ in
                        if let last = chatModel.messages.last {
                            withAnimation {
                                proxy.scrollTo(last.id, anchor: .bottom)
                            }
                        }
                    }
                }
                
                HStack {
                    TextField("Enter message...", text: $messageText)
                        .textFieldStyle(RoundedBorderTextFieldStyle())
                        .onSubmit {
                            sendMessage()
                        }
                    
                    Button("Send") {
                        sendMessage()
                    }
                    .disabled(messageText.isEmpty)
                }
                .padding()
            }
            .frame(minWidth: 400)
            
            // Devices Panel
            VStack(alignment: .leading) {
                Text("Discovered Devices")
                    .font(.headline)
                    .padding(.horizontal)
                    .padding(.top)
                
                List(chatModel.discoveredDevices) { device in
                    VStack(alignment: .leading, spacing: 4) {
                        Text("[\(device.deviceId)]")
                            .font(.caption.monospaced())
                            .foregroundColor(.secondary)
                        Text(device.machineName)
                            .font(.headline)
                        Text(device.userName)
                            .font(.subheadline)
                            .foregroundColor(.secondary)
                        Text(device.platform)
                            .font(.caption)
                            .foregroundColor(.secondary)
                        if !device.ipAddresses.isEmpty {
                            Text("IPs: \(device.ipAddresses.joined(separator: ", "))")
                                .font(.caption)
                                .foregroundColor(.secondary)
                        }
                    }
                    .padding(.vertical, 4)
                }
            }
            .frame(minWidth: 200)
        }
        .frame(minWidth: 700, minHeight: 500)
        .onAppear {
            chatModel.start()
        }
    }
    
    private func sendMessage() {
        guard !messageText.isEmpty else { return }
        chatModel.send(messageText)
        messageText = ""
    }
}

struct ChatMessage: Identifiable {
    let id = UUID()
    let text: String
    let isMe: Bool
    let timestamp = Date()
}

struct DiscoveredDevice: Identifiable {
    let id: String
    let deviceId: String
    let machineName: String
    let userName: String
    let platform: String
    let macAddress: String
    let ipAddresses: [String]
    
    init(from info: DeviceInfo) {
        self.id = info.deviceId
        self.deviceId = info.deviceId
        self.machineName = info.machineName
        self.userName = info.userName
        self.platform = info.platform
        self.macAddress = info.macAddress
        self.ipAddresses = info.ipAddresses
    }
}

class ChatViewModel: ObservableObject {
    @Published var messages: [ChatMessage] = []
    @Published var discoveredDevices: [DiscoveredDevice] = []
    @Published var localDeviceId: String = ""
    
    private let chat = BleChat()
    
    func start() {
        localDeviceId = chat.localDeviceInfo.deviceId
        
        chat.onMessageReceived = { [weak self] text in
            DispatchQueue.main.async {
                self?.messages.append(ChatMessage(text: "Peer: \(text)", isMe: false))
            }
        }
        
        chat.onDeviceDiscovered = { [weak self] device in
            DispatchQueue.main.async {
                // Update or add device
                if let index = self?.discoveredDevices.firstIndex(where: { $0.deviceId == device.deviceId }) {
                    self?.discoveredDevices[index] = DiscoveredDevice(from: device)
                } else {
                    self?.discoveredDevices.append(DiscoveredDevice(from: device))
                    self?.messages.append(ChatMessage(text: "[NEW DEVICE] \(device.machineName) (\(device.userName))", isMe: false))
                }
            }
        }
        
        chat.startListening()
        messages.append(ChatMessage(text: "System: Device ID: \(localDeviceId)", isMe: false))
        messages.append(ChatMessage(text: "System: Listening for messages...", isMe: false))
        
        print("[DEVICE] Local Device ID: \(localDeviceId)")
        print("[DEVICE] Machine: \(chat.localDeviceInfo.machineName)")
        print("[DEVICE] User: \(chat.localDeviceInfo.userName)")
        print("[DEVICE] Platform: \(chat.localDeviceInfo.platform)")
    }
    
    func send(_ text: String) {
        chat.send(message: text)
        messages.append(ChatMessage(text: "Me: \(text)", isMe: true))
    }
}
