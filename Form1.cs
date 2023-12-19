using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using Server_Network_App.Utils;

namespace Server_Network_App
{
    public partial class _winFormServer : Form
    {
        // constants
        private const string CRLF = "\r\n";


        // fields
        private List<TcpClient> _client_list;
        private TcpListener _listener;
        private int _client_count;
        private bool _keep_going;
        private int _port = 5000;

        private UdpClient _udpClient;
        private Thread _udpThread;
        private int _udpPort = 54321;

        public _winFormServer()
        {
            InitializeComponent();
            _client_list = new List<TcpClient>();
            _client_count = 0;
            _connectedClientsTextBox.Text = "0";
            _startServerButton.Enabled = true;
            _stopServerButton.Enabled = false;
            _sendCommandButton.Enabled = false;
            _statusTextBox.Text = string.Empty;
        }



        #region Event Handlers

        private void StartServerButtonHandler(object sender, EventArgs e)
        {
            _client_count = 0;
            _connectedClientsTextBox.Text = _client_count.ToString();
            try
            {
                if (!Int32.TryParse(_portTextBox.Text, out _port))
                {
                    _port = 5000;
                    _statusTextBox.Text += CRLF + "You entered an invalid port value. Using port " + _port;
                }

                // Close existing UDP client
                if (_udpClient != null)
                {
                    _udpClient.Close();
                }

                // Start TCP server
                Thread tcpThread = new Thread(ListenForIncomingConnections);
                tcpThread.Name = "Server Listener Thread";
                tcpThread.IsBackground = true;
                tcpThread.Start();

                // Start UDP server
                _udpClient = new UdpClient(_udpPort);
                _udpThread = new Thread(ListenUDP);
                _udpThread.IsBackground = true;
                _udpThread.Start();

                _startServerButton.Enabled = false;
                _stopServerButton.Enabled = true;
                _sendCommandButton.Enabled = true;
            }
            catch (Exception ex)
            {
                _statusTextBox.Text += CRLF + "Problem starting server.";
                _statusTextBox.Text += CRLF + ex.ToString();
            }
        } // End of StartServerButtonHandler
        private void StopServerButtonHandler(object sender, EventArgs e)
        {
            _keep_going = false;
            _statusTextBox.Text = string.Empty;
            _statusTextBox.Text = "Shuting down server, disconnectiong all clients...";
            //_client_count = 0;

            try
            {
                foreach(TcpClient client in _client_list)
                {
                    client.Close();
                    
                }
                _connectedClientsTextBox.InvokeEx(cctb => cctb.Text = _client_count.ToString());
                
                _client_list.Clear();
                _listener.Stop();
                _udpClient.Close();
                //_udpThread.Join(); // Wait for UDP thread to finish


            }
            catch(Exception ex)
            {
                _statusTextBox.Text += CRLF + "Problem stopping the serverserver, or client connections forcibly closed...";
                _statusTextBox.Text+= CRLF + ex.ToString();
            }
            _startServerButton.Enabled = true;
            _stopServerButton.Enabled = false;
            _sendCommandButton.Enabled = false;

        } // End of StopServerButtonHandler

        private void SendCommandButtonHandler(object sender, EventArgs e)
        {
            try
            {

                foreach(TcpClient client in _client_list)
                {
                    StreamWriter writer = new StreamWriter(client.GetStream());
                    writer.WriteLine(_clientCommandTextBox.Text);
                    writer.Flush();
                    
                }
                //_statusTextBox.Text += CRLF + "Command Sent from Server: " + _clientCommandTextBox.Text;
                _clientCommandTextBox.Text = string.Empty;


            }catch (Exception ex)
            {
                _statusTextBox.Text += CRLF + "Problem sending command to clients";
                _statusTextBox.Text += CRLF + ex.ToString();
            }
        } // End of SendCommandButtonHandler
        #endregion Event Handlers

        private void ListenForIncomingConnections()
        {
            try
            {
                _keep_going = true;
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Tcp Server started. Listening on port: " + _port);
                while(_keep_going)
                {
                    _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Waiting for incoming client connections...");
                    TcpClient client = _listener.AcceptTcpClient(); // blocks here until client connects
                    _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Incoming client connection accepted...");
                    Thread t = new Thread(ProcessClientRequests);
                    t.IsBackground = true;
                    t.Start(client);
                }

            }catch (Exception ex)
            {
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + ex.ToString());
            }

            _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Exiting listener thred...");
            _statusTextBox.InvokeEx(stb => stb.Text = string.Empty);

        } // End ListenForIncomingConnections method

        private void ProcessClientRequests(object o)
        {
            TcpClient client = (TcpClient)o;
            _client_list.Add(client);
            _client_count += 1;
            _connectedClientsTextBox.InvokeEx(cctb => cctb.Text =  _client_count.ToString());

            string input = string.Empty;
            try
            {
                StreamReader reader = new StreamReader(client.GetStream());
                StreamWriter writer = new StreamWriter(client.GetStream());
                while(client.Connected)
                {
                    input = reader.ReadLine(); // block here until somthing is recieved from the client
                    switch (input)
                    {
                        default:
                            {
                                _statusTextBox.InvokeEx(stb => 
                                stb.Text += CRLF + "From client " + client.GetHashCode() + ": " + input);
                                writer.WriteLine("Server recieved: " + input);
                                writer.Flush();

                                break;
                            }
                    }

                    SendToUDPServer(input);
                }
            }catch(SocketException se)
            {
                 _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Problem processing client requests.");
                 _statusTextBox.InvokeEx(stb => stb.Text =se.ToString());
            }
            catch (Exception ex)
            {
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Problem processing client requests. ");
                _statusTextBox.InvokeEx(stb => stb.Text =ex.ToString());
            }
            _client_list.Remove(client);
            _client_count -= 1;
            _connectedClientsTextBox.InvokeEx(cctb => cctb.Text = _client_count.ToString());
            _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Finished processing client requests for client: " + client.GetHashCode());


        } // End ProcessClientRequests method

        private void SendToUDPServer(string message)
        {
            try
            {
                // Convert the message to bytes
                byte[] data = Encoding.ASCII.GetBytes(message);

                // Send the message using UDP
                _udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, _udpPort));

                // Additional status or logging if needed
                //_statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Message sent to UDP server: " + message);
            }
            catch (Exception ex)
            {
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Problem sending message to UDP server: " + ex.ToString());
            }
        } // End of SendToUDPServer method



        private void ListenUDP()
        {
            try
            {
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "UDP Server started. Listening on port: " + _udpPort);

                while (_keep_going)
                {
                    IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] receivedBytes = _udpClient.Receive(ref remoteEndPoint);
                    string receivedMessage = Encoding.ASCII.GetString(receivedBytes);

                    // Broadcast the UDP message to all clients
                    BroadcastToClients(receivedMessage);

                    // Additional processing if needed
                    //_statusTextBox.InvokeEx(stb => stb.Text += CRLF + "UDP received from client: " + receivedMessage);
                }
            }
            catch (Exception ex)
            {
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Problem with UDP server: " + ex.ToString());
            }
            finally
            {
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Exiting UDP server thread...");
            }
        } // End of ListenUDP method

        private void BroadcastToClients(string message)
        {
            try
            {
                foreach (TcpClient client in _client_list)
                {
                    StreamWriter writer = new StreamWriter(client.GetStream());
                    writer.WriteLine(message);
                    writer.Flush();
                }

                // Additional status or logging if needed
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Message broadcast to clients: " + message);
            }
            catch (Exception ex)
            {
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Problem broadcasting message to clients: " + ex.ToString());
            }
        } // End of BroadcastToClients method


    }
}
