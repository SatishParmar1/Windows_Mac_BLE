import SwiftUI
import CoreBluetooth

struct ContentView: View {
    @StateObject private var chatModel = ChatViewModel()
    @State private var messageText = ""
    
    var body: some View {
        VStack {
            Text("macOS BLE Mesh Chat")
                .font(.title)
                .padding()
            
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
        .frame(minWidth: 400, minHeight: 500)
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

class ChatViewModel: ObservableObject {
    @Published var messages: [ChatMessage] = []
    private let chat = BleChat()
    
    func start() {
        // In a real implementation, you'd bind a callback from BleChat to here.
        // We need to modify BleChat slightly to support a callback or delegate.
        // For this example, we assume BleChat has a callback property we can set.
        
        chat.onMessageReceived = { [weak self] text in
            DispatchQueue.main.async {
                self?.messages.append(ChatMessage(text: "Peer: \(text)", isMe: false))
            }
        }
        
        chat.startListening()
        messages.append(ChatMessage(text: "System: Listening for messages...", isMe: false))
    }
    
    func send(_ text: String) {
        chat.send(message: text)
        messages.append(ChatMessage(text: "Me: \(text)", isMe: true))
    }
}
