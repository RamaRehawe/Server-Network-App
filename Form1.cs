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

        public _winFormServer()
        {
            InitializeComponent();
            _client_list = new List<TcpClient>();
            _connectedClientsTextBox.Text = "0";
            _startServerButton.Enabled = true;
            _stopServerButton.Enabled = false;
            _sendCommandButton.Enabled = false;
            _statusTextBox.Text = string.Empty;
        }



        #region Event Handlers

        private void StartServerButtonHandler(object sender, EventArgs e)
        {
            try
            {
                if(!Int32.TryParse(_portTextBox.Text, out _port))
                {
                    _port = 5000;
                    _statusTextBox.Text += CRLF + "You enterd an invalid port value. Using port " + _port; 

                }
                Thread t = new Thread(ListenForIncomingConnections);
                t.Name = "Server Listner Thread";
                t.IsBackground = true;
                t.Start();

                _startServerButton.Enabled = false;
                _stopServerButton.Enabled = true;
                _sendCommandButton.Enabled = true;


            }catch (Exception ex)
            {
                _statusTextBox.Text += CRLF + "Problem starting server.";
                _statusTextBox.Text += CRLF + ex.ToString();
            }
        }
        private void StopServerButtonHandler(object sender, EventArgs e)
        {
            _keep_going = false;
            _statusTextBox.Text = string.Empty;
            _statusTextBox.Text = "Shuting down server, disconnectiong all clients...";

            try
            {
                foreach(TcpClient client in _client_list)
                {
                    client.Close();
                }
                _client_count = 0;
                _connectedClientsTextBox.Text = "0";
                _client_list.Clear();
                _listener.Stop();


            }catch(Exception ex)
            {
                _statusTextBox.Text += CRLF + "Problem stopping the serverserver, or client connections forcibly closed...";
                _statusTextBox.Text+= CRLF + ex.ToString();
            }

            _startServerButton.Enabled = true;
            _stopServerButton.Enabled = false;
            _sendCommandButton.Enabled = false;

        }

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
                _clientCommandTextBox.Text = string.Empty;


            }catch (Exception ex)
            {
                _statusTextBox.Text += CRLF + "Problem sending command to clients";
                _statusTextBox.Text += CRLF + ex.ToString();
            }
        }
        #endregion Event Handlers

        private void ListenForIncomingConnections()
        {
            try
            {
                _keep_going = true;
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Server started. Listening on port: " + _port);
                while(_keep_going)
                {
                    _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Waiting for incoming client connections...");
                    TcpClient client = _listener.AcceptTcpClient(); // blocks here until client connects
                    _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Incoming client connection accepted...");
                    Thread t = new Thread(ProcessClientRequests);
                    t.IsBackground = true;
                    t.Start(client);
                }

            }
            catch (SocketException se)
            {

            }catch (Exception ex)
            {
                _statusTextBox.InvokeEx(stb => stb.Text += CRLF + ex.ToString());
            }

            _statusTextBox.InvokeEx(stb => stb.Text += CRLF + "Exiting listener thred...");
            _statusTextBox.InvokeEx(stb => stb.Text = string.Empty);

        } // end ListenForIncomingConnections() method

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


        } // end ProcessClientRequests() method
    }
}
