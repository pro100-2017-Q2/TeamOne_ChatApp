﻿using System;
using System.Collections.Generic;
using System.Linq;
using ChatBase;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NewServer {
    public partial class Server : Window {
        private TcpListener tcpListener;
        private Thread listenThread;
        private int connClients = 0;

        private delegate void WriteMessageDelegate(string msg);

        public Server() {
            InitializeComponent();
            StartServer();
        }

        private void StartServer() {
            // Change to IpAddress.Any for internet communication
            tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), Constants.PORT);
            listenThread = new Thread(new ThreadStart(ListenForClients));
            listenThread.Start();
        }

        private void ListenForClients() {
            tcpListener.Start();

            // Do not stop until the server is closed
            while (true) {
                // Block until client has connected
                TcpClient client = tcpListener.AcceptTcpClient();

                // Create thread to handle communication with client
                connClients++;
                //labelUsers.Content = "Connected users: " + connClients;
                labelUsers.Dispatcher.Invoke(() => labelUsers.Content = "Connected users: " + connClients);

                Thread clientThread = new Thread(new ParameterizedThreadStart(ClientCommsHandler));
                clientThread.Start(client);
            }
        }

        private void ClientCommsHandler(object client) {
            TcpClient tcpClient = (TcpClient)client;
            NetworkStream clientStream = tcpClient.GetStream();

            byte[] msg = new byte[Constants.BUFFER_SIZE];
            int bytesRead;

            while (true) {
                bytesRead = 0;

                try {
                    // Block until message received
                    bytesRead = clientStream.Read(msg, 0, Constants.BUFFER_SIZE);
                }
                catch (Exception ex) {
                    MessageBox.Show(ex.ToString());
                    break;
                }

                if (bytesRead == 0) {
                    // Client disconnected
                    connClients--;
                    //labelUsers.Content = connClients.ToString();
                    labelUsers.Dispatcher.Invoke(() => labelUsers.Content = "Connected users: " + connClients);
                    break;
                }

                // Message received
                ASCIIEncoding encoder = new ASCIIEncoding();

                // Convert bytes to string and display string
                string message = encoder.GetString(msg, 0, bytesRead);
                WriteMessage(message);

                // Echo message
                Echo(message, encoder, clientStream);
            }

            tcpClient.Close();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e) {

        }

        private void ListenButton_Click(object sender, RoutedEventArgs e) {

        }

        private void SendButton_Click(object sender, RoutedEventArgs e) {

        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {

        }

        private void WriteMessage(string msg) {
            //msgReceived.AppendText(msg + Environment.NewLine);
            //msgReceived.Dispatcher.Invoke(this.UpdateText);
            msgReceived.Dispatcher.Invoke(() => msgReceived.AppendText(msg + Environment.NewLine));
        }

        private void Echo(string msg, ASCIIEncoding encoder, NetworkStream clientStream) {
            // Echo message back
            byte[] buffer = encoder.GetBytes(msg);

            clientStream.Write(buffer, 0, buffer.Length);
            clientStream.Flush();
        }
    }
}