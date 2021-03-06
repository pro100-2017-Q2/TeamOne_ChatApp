﻿using ChatBase.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Windows.Input;
using static ChatBase.Constants;

// TODO: move to models
namespace ChatBase {
    public class Client : INotifyPropertyChanged {
        // Variables that allow listening to server
        private TcpClient client = new TcpClient();
        private IPEndPoint serverEP;
        private Thread listenThread;

        public User user;
        public List<Room> rooms = new List<Room>();
        public List<Message> messages = new List<Message>();
        public List<User> users = new List<User>();

        // Variables that change UI
        private string windowTitle = "";
        private string curMessage = "";
        private string curRoom = "";
        private string serverIP = "";
        private Message serverMessage = null;
        private bool isRunning = true;
        private bool connectedToServer;

        // Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event WindowHandler WindowHandler;
        public event MessageReceived MsgReceived;
        public event RoomAdded RoomHandler;
        public event HasRoom HasRoomEvent;

        public string WindowTitle {
            get => windowTitle;
            set {
                windowTitle = value;
                PropChanged();
            }
        }

        public string CurMessage {
            get => curMessage;
            set {
                curMessage = value;
                PropChanged();
            }
        }

        public string ServerIP {
            get => serverIP;
            set {
                serverIP = value;
                PropChanged();
            }
        }

        public bool ConnectedToServer {
            get => connectedToServer;
            set {
                connectedToServer = value;
                PropChanged();
            }
        }

        public string CurRoom
        {
            get => curRoom;
            set
            {
                curRoom = value;
                PropChanged();
            }
        }

        public Message ServerMessage {
            get => serverMessage;
            set {
                serverMessage = value;
                MsgReceived?.Invoke(serverMessage);
            }
        }

        public Client() {
            ConnectedToServer = false;
            ServerIP = "127.0.0.1";
        }

        /// <summary>
        /// Start the listening thread that initializes the server
        /// </summary>
        public void Start() {
            listenThread = new Thread(new ThreadStart(Init));
            listenThread.Start();
        }

        /// <summary>
        /// Start the listening thread that initializes the server
        /// </summary>
        public void Start(string ip) {
            serverEP = new IPEndPoint(IPAddress.Parse(ip), PORT);

            // TODO: show errors and reconnect tries and stuff

            listenThread = new Thread(new ThreadStart(Init));
            listenThread.Start();
        }

        /// <summary>
        /// Connect to server and start reading from server
        /// </summary>
        private void Init() {
            Reconnect();
            ReadResponse();
        }

        /// <summary>
        /// Try connecting to the server until successful or max tries
        /// </summary>
        private void Reconnect() {
            int curTry = 0;
            bool succeeded = false;

            // TODO: have main client UI show reconnect progress like a loading gif or something
            // Reconnect until we hit the max amount of tries
            while (curTry < RECONNECT_MAX_TRIES && !succeeded) {
                try {
                    curTry++;
                    //MessageFromServer("TRY " + curTry + ": Attempting to connect to " + serverEP);

                    client.Connect(serverEP);
                    succeeded = true;
                    ConnectedToServer = true;
                } catch (SocketException) {
                    // This means we failed to connect...
                    succeeded = false;
                    //MessageFromServer("Failed to connect to " + serverEP + "... Trying again in " + SECONDS_BETWEEEN_TRIES + " seconds...");

                    Thread.Sleep(SECONDS_BETWEEEN_TRIES * 1000);
                }
            }

            if (curTry == RECONNECT_MAX_TRIES) {
                //MessageFromServer("It seems that the you or the server is having connection issues, please try again later...");

                Thread.Sleep(1000);
                Window_Closed(null, null);
                WindowHandler?.Invoke();
            }
            
            //MessageFromServer("You have connected to: " + serverEP);

            //SendPacket(REQUEST_ALL_ROOMS_PACKET);
            SendPacket(PacketFactory.CreatePacket<RequestAllRoomsPacket>());
        }

        /// <summary>
        /// Reads any response we get from the server
        /// </summary>
        private void ReadResponse() {
            NetworkStream stream = client.GetStream();
            byte[] data = new byte[BUFFER_SIZE];
            Int32 bytes = 0;
            string response = "";
            
            // TODO: find a better way to listen, like get an event if stream finds input
            while (isRunning) {
                Thread.Sleep(50);  // Sleep for a bit, save some cpu cycles

                try {
                    bytes = stream.Read(data, 0, data.Length);  // Read stream
                } catch (IOException) {
                    // Lost connection...
                    client.Close();
                    client = new TcpClient();
                    ConnectedToServer = false;
                    Reconnect();
                    stream = client.GetStream();
                }

                response = Encoding.UTF8.GetString(data, 0, bytes); // Change bytes to readable string

                // If a message was recieved then do stuff
                if (response.Length > 0) {
                    // If the response is a valid packet, read it
                    if (PacketFactory.JsonToPacket(response, out IPacket tempPacket)) {
                        ReadPacket(tempPacket);
                    }
                }
            }
        }

        /// <summary>
        /// Check if enter is pressed, if so then send the message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void MessageBoxKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter || e.Key == Key.Return) {
                if (client.Connected) {
                    MessagePacket p = PacketFactory.CreatePacket<MessagePacket>().AlterContent(CurMessage);
                    p.Owner = user.ScreenName;
                    p.Room = user.CurRoom.Name;

                    SendPacket(p);
                    CurMessage = "";
                }
                else {
                    //ServerMessage = new Message(new User);
                    // Send error message
                }
            }
        }

        /// <summary>
        /// If a user creates a room, send a request to the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void RoomGenerationButtonClick(object sender, System.Windows.RoutedEventArgs e) {
            if (CurRoom != null) {
                RequestCreateRoomPacket p = PacketFactory.CreatePacket<RequestCreateRoomPacket>() as RequestCreateRoomPacket;
                p.Content = CurRoom;
                SendPacket(p);
                CurRoom = "";
            }
        }

        public void SerializeAndSendFile(Stream file, string filetype)
        {
            if (!file.GetType().IsSerializable)
            {
                Console.WriteLine("not serializable");
            } else
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    new BinaryFormatter().Serialize(stream,file);
                    MessagePacket p = PacketFactory.CreatePacket<MessagePacket>() as MessagePacket;
                    p.Content = filetype + ":" + Convert.ToBase64String(stream.ToArray());
                    Console.WriteLine(p.Content);
                    SendPacket(p);
                }
            }

        }


        /// <summary>
        /// Read a received Packet and perform action based on type
        /// </summary>
        /// <param name="json">Json string of Packet</param>
        private void ReadPacket(IPacket p) {
            switch (p.Type) {
                case PacketType.ClientID:
                    WindowTitle = "Connected to " + serverEP.Address + " | Client " + p.Content;
                    user = new User("Client " + p.Content);
                    break;
                case PacketType.Message:
                    MessagePacket msgPacket = p as MessagePacket;
                    Room room = user.CurRoom;
                    User sender;

                    if (msgPacket.Owner != "" && msgPacket.Owner != null) {
                        try {
                            sender = users.Where(u => u.ScreenName == msgPacket.Owner).Single();
                        }
                        catch (InvalidOperationException) {
                            sender = new User("UNKNOWN");   // If it doesn't have a valid owner, output UNKNOWN for debugging purposes
                        }
                    }
                    else {
                        // If it doesn't have an owner, it must have come from the server
                        sender = new User("SERVER");
                    }

                    if (msgPacket.Room != "" && msgPacket.Room != null) {
                        // If it doesn't have a room, default to the general room
                        room = rooms.Where(r => r.Name == msgPacket.Room).ElementAt(0);
                    }

                    ServerMessage = new Message(sender, room, p.Content, DateTime.Now);
                    break;
                case PacketType.Goodbye:
                    MessageFromServer("Server has shutdown, closing connection...");
                    client.Close();
                    isRunning = false;
                    break;
                case PacketType.ResponseAllRooms:
                    string[] roomNames = p.Content.Split(',');

                    foreach (string roomName in roomNames) {
                        if (roomName != "" && roomName != " " && roomName != "\n") {
                            bool taken = false;
                            foreach (Room r in rooms) {
                                if (r.Name == roomName) {
                                    taken = true;
                                }
                            }

                            if (!taken) {
                                rooms.Add(new Room(roomName));

                                RoomHandler?.Invoke(rooms[rooms.Count - 1]);
                            }
                        }
                    }

                    // TODO: fix this stuff
                    if (user.CurRoom == null) {
                        user.CurRoom = rooms[0];
                        HasRoomEvent?.Invoke();
                    }
                    break;
                case PacketType.ResponseAllUsers:
                    string[] userNames = p.Content.Split(',');

                    foreach (string userName in userNames) {
                        if (userName != "" && userName != " " && userName != "\n") {
                            bool taken = false;
                            foreach (User u in users) {
                                if (u.ScreenName == userName) {
                                    taken = true;
                                }
                            }

                            if (!taken) {
                                users.Add(new User(userName));
                            }
                        }
                    }

                    break;
                case PacketType.RoomCreated:
                    if (p.Content != "" && p.Content != " " && p.Content != "\n") {
                        bool taken = false;
                        foreach (Room r in rooms) {
                            if (r.Name == p.Content) {
                                taken = true;
                            }
                        }

                        if (!taken) {
                            rooms.Add(new Room(p.Content));
                            RoomHandler?.Invoke(rooms[rooms.Count - 1]);
                        }
                    }

                    if (user.CurRoom == null) {
                        user.CurRoom = rooms[0];
                        HasRoomEvent?.Invoke();
                    }
                    break;
                case PacketType.UserJoined:
                    users.Add(new User(p.Content));
                    MessageFromServer(p.Content + " has joined the chat!");
                    break;
                default:
                    Console.WriteLine("ERROR: wrong packet type........... Content: {0}", p.Content);
                    break;
            }
        }

        /// <summary>
        /// Sends a packet to the server
        /// </summary>
        /// <param name="p">Packet to send</param>
        private void SendPacket(IPacket p) {
            if (client.Connected) {
                NetworkStream clientStream = client.GetStream();
                string json = p.ToJsonString();

                byte[] buffer = Encoding.UTF8.GetBytes(json);

                clientStream.Write(buffer, 0, buffer.Length);
                clientStream.Flush();
            }
        }

        /// <summary>
        /// Send a message to the UI and have it look like it came from the server
        /// </summary>
        /// <param name="msg">Message to send</param>
        private void MessageFromServer(String msg) {
            ServerMessage = new Message(new User("SERVER"), rooms[0], msg, DateTime.Now);
        }

        /// <summary>
        /// If the client window is closed, dispose of everything
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Window_Closed(object sender, EventArgs e) {
            SendPacket(PacketFactory.CreatePacket<GoodByePacket>().AlterContent("Client"));

            if (listenThread != null) {
                listenThread.Abort();
            }
            client.Close();
        }

        /// <summary>
        /// If a property is changed, invoke the PropertyChanged event
        /// </summary>
        /// <param name="prop"></param>
        private void PropChanged([CallerMemberName] string prop = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
