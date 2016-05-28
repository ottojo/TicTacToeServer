using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TicTacToeServer;

namespace TicTacToeServer
{
    public partial class Form1 : Form
    {

        Socket socket;
        IPAddress localIp;
        int localPort;
        const int clientPort = 12345;
        List<Player> waitingPlayers = new List<Player>();
        List<Game> games = new List<Game>();
        ASCIIEncoding enc = new ASCIIEncoding();

        public Form1()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Schreibt einen Logeintrag in das Logfenster
        /// </summary>
        /// <param name="message">Logeintrag</param>
        public void log(string message)
        {
            if (this.textBoxLog.InvokeRequired)
            {
                this.textBoxLog.BeginInvoke((MethodInvoker)delegate ()
                {
                    this.textBoxLog.AppendText("\r\n[" + DateTime.Now.ToString("H:mm:ss tt") + "] " + message);
                });
            }
            else
            {
                textBoxLog.AppendText("\r\n[" + DateTime.Now.ToString("H:mm:ss tt") + "] " + message);
            }
        }

        /// <summary>
        /// Aktualisiert die Spieler- und Spieleliste
        /// </summary>
        void updateLists()
        {
            this.textBoxLog.BeginInvoke((MethodInvoker)delegate ()
            {
                listBoxGames.Items.Clear();
                listBoxGames.Items.AddRange(games.ToArray());
                listBoxPlayers.Items.Clear();
                listBoxPlayers.Items.AddRange(waitingPlayers.ToArray());
            });
        }

        /// <summary>
        /// Startet den Server
        /// </summary>
        private void startServer()
        {
            if (localIp == null)
            {
                using (var form = new IpChooserDialog(getLocalIPs()))
                {
                    var result = form.ShowDialog();
                    localIp = form.chosenAddress;
                }
            }
            if (!int.TryParse(textBoxLocalPort.Text, out localPort))
            {
                localPort = 12345;
                textBoxLocalPort.Text = localPort.ToString();
            }
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            EndPoint localEndPoint = new IPEndPoint(localIp, localPort);
            socket.Bind(localEndPoint);
            byte[] buffer = new byte[1024];
            EndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
            socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remoteEp, new AsyncCallback(messageCallback), buffer);

            log("Server gestartet auf " + localEndPoint.ToString());
            labelStatus.Text = "ONLINE";
            labelStatus.ForeColor = System.Drawing.Color.Green;
            buttonStart.Enabled = false;
            buttonStop.Enabled = true;
        }

        /// <summary>
        /// Stoppt den Server
        /// </summary>
        private void stopServer()
        {


            foreach (Game g in games)
            {
                log("Stoppe Spiel " + g.ToString());
                g.playerO.sendGameEnd(2, ref socket);
                g.playerX.sendGameEnd(2, ref socket);
            }
            log("Lösche aktive Spiele");
            games.Clear();

            foreach (Player p in waitingPlayers)
            {
                log("Sende Spielende an  wartenden Spieler " + p.ToString());
                p.sendGameEnd(2, ref socket);
            }
            log("Lösche wartende Spieler");
            waitingPlayers.Clear();

            log("Stoppe socket");
            socket.Shutdown(SocketShutdown.Both);
            socket.Dispose();

            log("Server gestoppt.");
            buttonStart.Enabled = true;
            buttonStop.Enabled = false;
            labelStatus.Text = "OFFLINE";
            labelStatus.ForeColor = System.Drawing.Color.Red;
        }

        /// <summary>
        /// Erkennt lokale IP Adressen und filtert sie nach IPv4
        /// </summary>
        /// <returns>Liste aller verfügbaren lokalen IPv4 Adressen</returns>
        private List<IPAddress> getLocalIPs()
        {
            List<IPAddress> result = new List<IPAddress>();
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            foreach (IPAddress a in localIPs)
            {
                if (a.AddressFamily == AddressFamily.InterNetwork)
                    result.Add(a);
            }
            return result;
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            buttonStart.Enabled = false;
            buttonStop.Enabled = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            buttonStop.Enabled = false;
            listBoxGames.DisplayMember = "Description";
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
        }

        /// <summary>
        /// Empfängt eine Nachricht und leitet diese zusammen mit dem Absender an handleMessage() weiter
        /// </summary>
        /// <param name="iAsyncResult">Status der Anfrage</param>
        void messageCallback(IAsyncResult iAsyncResult)
        {
            try
            {
                EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                int size = socket.EndReceiveFrom(iAsyncResult, ref remote);
                if (size > 0)
                {
                    byte[] receivedData = (byte[])iAsyncResult.AsyncState;
                    //Console.WriteLine(remote.ToString() + " " + "\"" + enc.GetString(receivedData) + "\"");
                    String dataString = enc.GetString(receivedData).Trim('\r', '\n', '\0', ' ');
                    handleMessage(dataString, (IPEndPoint)remote);
                }


                byte[] buffer = new byte[1024];
                EndPoint remoteEp = new IPEndPoint(IPAddress.Any, 0);
                socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remoteEp, new AsyncCallback(messageCallback), buffer);
            }
            catch (System.ObjectDisposedException e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Verarbeitet eine eingehende Nachricht
        /// </summary>
        /// <param name="message">Nachricht</param>
        /// <param name="remoteEndpoint">Absender</param>
        void handleMessage(string message, IPEndPoint remoteEndpoint)
        {
            log("Got message \"" + message + "\" from " + remoteEndpoint.ToString());
            switch (message.Substring(0, 1))
            {
                case "n":   //Player logging in
                    Player player = new Player(remoteEndpoint, message.Substring(1), this);
                    log("Added Player to queue: " + player.ToString());
                    waitingPlayers.Add(player);
                    if (waitingPlayers.Count == 2)
                    {
                        log("2 Players in queue, creating game");
                        games.Add(new Game(waitingPlayers.ElementAt(0), waitingPlayers.ElementAt(1), ref socket, this));
                        waitingPlayers.Clear();
                    }
                    break;
                case "c":
                    log("Request from " + remoteEndpoint.ToString() + " : " + message);
                    Game game = findGame(games, remoteEndpoint.Address);
                    if (game != null)
                    {
                        game.setField(int.Parse(message.Substring(1, 1)), int.Parse(message.Substring(2, 1)), ref socket, remoteEndpoint);


                        switch (game.winner)
                        {
                            case null:
                                break;
                            case "X":
                                //addToHighscore(game.playerX.name);
                                log("Deleting Game " + game.ToString());
                                games.Remove(game);
                                break;
                            case "O":
                                //addToHighscore(game.playerO.name);
                                log("Deleting Game " + game.ToString());
                                games.Remove(game);
                                break;
                            case "none":
                                log("Deleting Game " + game.ToString());
                                games.Remove(game);
                                break;
                        }



                    }
                    else
                    {
                        log("Got request \"" + message + "\" from \"" + remoteEndpoint.ToString() + "\" who doesn't seem to be in a game.");
                    }
                    break;
                default:
                    log("Did not recognise message \"" + message + "\"" + " from " + remoteEndpoint.ToString());
                    break;
            }
            updateLists();
        }


        //Hilfsfunktionen für TextBox Beschriftung
        private void textBoxLocalPort_Enter(object sender, EventArgs e)
        {
            if (textBoxLocalPort.Text == "Port")
                textBoxLocalPort.Text = "";
        }

        private void textBoxLocalPort_Leave(object sender, EventArgs e)
        {
            if (textBoxLocalPort.Text == "")
                textBoxLocalPort.Text = "Port";
        }

        /// <summary>
        /// Findet ein Spiel in einer Liste mit dem die angegebene IP Adresse verbunden ist
        /// </summary>
        /// <param name="list">Liste der Spiele</param>
        /// <param name="ip">Zu suchende IP Adresse</param>
        /// <returns>Spiel dass die angegebene IP Adresse enthält</returns>
        private Game findGame(List<Game> list, IPAddress ip)
        {
            log("Searching for game containing " + ip.ToString());
            foreach (Game g in list)
            {
                log("Checking Game " + g.ToString());

                if (g.playerO.playerEndpoint.Address.Equals(ip) || g.playerX.playerEndpoint.Address.Equals(ip))
                    return g;
            }
            return null;
        }

        private void buttonStart_Click_1(object sender, EventArgs e)
        {
            startServer();
            updateLists();
        }

        private void buttonStop_Click_1(object sender, EventArgs e)
        {
            stopServer();
            updateLists();
        }
    }

    /// <summary>
    /// Repräsentiert einen verbundenen Spieler
    /// </summary>
    class Player
    {
        /// <summary>
        /// Rolle des Spielers ("X", "O")
        /// </summary>
        public string role; 

        /// <summary>
        /// Name des Spielers
        /// </summary>
        public string name;

        public IPEndPoint playerEndpoint;
        private ASCIIEncoding enc;
        private Form1 form;

        /// <summary>
        /// Konstruktor
        /// </summary>
        /// <param name="playerEndpoint">Endpoint des Spielers</param>
        /// <param name="name">Name des Spielers</param>
        /// <param name="form">Aufrufende Form1</param>
        public Player(IPEndPoint playerEndpoint, string name, Form1 form)
        {
            this.form = form;
            this.playerEndpoint = playerEndpoint;
            this.name = name;
            enc = new System.Text.ASCIIEncoding();

        }

        /// <summary>
        /// Schickt den Status eines Feldes an den Spieler
        /// </summary>
        /// <param name="x">X Koordinate</param>
        /// <param name="y">Y Koordinate</param>
        /// <param name="symbol">Wert des Feldes ("X", "0", "")</param>
        /// <param name="socket">Zu verwender Socket</param>
        public void sendField(int x, int y, string symbol, ref Socket socket)
        {
            this.form.log(this.ToString() + " sending field " + x.ToString() + "|" + y.ToString() + " is " + symbol);
            sendString("c" + x.ToString() + y.ToString() + symbol, ref socket);
        }

        /// <summary>
        /// Signalisiert dem Spieler das Ende des Spiels
        /// </summary>
        /// <param name="won">Status: 1: Gewonnen, 0: Verloren, 2: Gleichstand</param>
        /// <param name="socket">Zu verwendender Socket</param>
        public void sendGameEnd(int won, ref Socket socket)
        {
            sendString("e" + won.ToString(), ref socket);
        }

        /// <summary>
        /// Benachrichtigt den Spieler über neue Runde, erwartet Spielzug
        /// </summary>
        /// <param name="socket">Zu verwendender Socket</param>
        public void notifyNewRound(ref Socket socket)
        {
            this.form.log(ToString() + " notifying of new round");
            sendString("p", ref socket);
        }

        /// <summary>
        /// Schickt eine Nachricht an den Spieler
        /// </summary>
        /// <param name="stringMessage">Nachricht</param>
        /// <param name="socket">Zu verwendender Socket</param>
        private void sendString(String stringMessage, ref Socket socket)
        {
            socket.Connect(playerEndpoint);
            byte[] message = new byte[stringMessage.Length];
            enc.GetBytes(stringMessage, 0, stringMessage.Length, message, 0);
            socket.Send(message);
            Console.Write(this.ToString() + " sending bytes: ");
            this.form.log(BitConverter.ToString(message));
            socket.Connect(new IPEndPoint(IPAddress.Any, 0));
        }

        public override string ToString()
        {
            return "Player[\"" + name + "\", " + playerEndpoint.ToString() + "]";
        }
    }

    /// <summary>
    /// Repräsentiert ein aus 2 Spielern bestehendes Spiel
    /// </summary>
    class Game
    {
        public Player playerX;
        public Player playerO;

        /// <summary>
        /// Aktueller Spieler ("X", "O")
        /// </summary>
        public string currentPlayerString;

        public int roundsComplete = 0;
        string[,] field = new string[3, 3];
        public string winner;
        private Form1 form;

        /// <summary>
        /// Konstruktor
        /// </summary>
        /// <param name="player1">Erster Spieler</param>
        /// <param name="player2">Zweiter Spieler</param>
        /// <param name="socket">Zu verwendender Socket</param>
        /// <param name="form">Aufrufende Form1</param>
        public Game(Player player1, Player player2, ref Socket socket, Form1 form)
        {
            this.form = form;
            this.playerX = player1;
            this.playerX.role = "X";
            this.playerO = player2;
            this.playerO.role = "O";
            currentPlayerString = "X";
            playerX.notifyNewRound(ref socket);
        }

        /// <summary>
        /// Gibt den aktuellen Spieler zurück der gerade am Zug ist
        /// </summary>
        /// <returns>Aktueller Spieler</returns>
        private Player currentPlayer()
        {
            switch(currentPlayerString)
            {
                case "X":
                    return playerX;
                case "O":
                    return playerO;
            }
            return null;
        }

        /// <summary>
        /// Setzt ein Feld und benachrichtigt die Spieler
        /// </summary>
        /// <param name="x">X Koordinate</param>
        /// <param name="y">Y Koordinate</param>
        /// <param name="socket">Zu verwendender Socket</param>
        /// <param name="remoteEndpoint">Endpoint des Spielers, der das Feld setzen möchte</param>
        public void setField(int x, int y, ref Socket socket, EndPoint remoteEndpoint)
        {

            if (field[x, y] == null && currentPlayer().playerEndpoint.Equals(remoteEndpoint))
            {
                this.form.log("Setting field " + x.ToString() + "|" + y.ToString() + " to " + currentPlayerString);
                field[x, y] = currentPlayerString;
                playerX.sendField(x, y, currentPlayerString, ref socket);
                playerO.sendField(x, y, currentPlayerString, ref socket);

                if (spielGewonnen(field, x, y))
                {
                    this.form.log("Game won by " + currentPlayerString + ", Board:");
                    this.form.log(string.Join(", ", field));
                    if (currentPlayerString == "X")
                    {
                        playerX.sendGameEnd(1, ref socket);
                        playerO.sendGameEnd(0, ref socket);
                        winner = "X";
                    }
                    else if (currentPlayerString == "O")
                    {
                        playerO.sendGameEnd(1, ref socket);
                        playerX.sendGameEnd(0, ref socket);
                        winner = "O";
                    }
                    return;
                }
                nextPlayer(ref socket);
            }
            else
            {
                this.form.log("Field " + x.ToString() + "|" + y.ToString() + " is not empty.");
            }
        }

        /// <summary>
        /// Benachrichtigt den nächsten Spieler zu Spielen
        /// </summary>
        /// <param name="socket"></param>
        private void nextPlayer(ref Socket socket)
        {
            if (currentPlayerString == "X")
            {
                currentPlayerString = "O";
                playerO.notifyNewRound(ref socket);
            }
            else
            {
                currentPlayerString = "X";
                playerX.notifyNewRound(ref socket);
            }
            roundsComplete++;
            this.form.log("Finished " + roundsComplete + " rounds.");
            if (roundsComplete >= 9)
            {
                this.form.log("9 Rounds finished, ending " + this);
                playerX.sendGameEnd(2, ref socket);
                playerO.sendGameEnd(2, ref socket);
                winner = "none";
            }
        }

        /// <summary>
        /// Prüft, ob das Spiel gewonnen ist
        /// </summary>
        /// <param name="field">Spielfeld</param>
        /// <param name="x">X Koordinate des letzten Zuges</param>
        /// <param name="y">Y Koordinate des letzten Zuges</param>
        /// <returns>Gibt true zurück wenn das Spiel gewonnen ist</returns>
        bool spielGewonnen(string[,] field, int x, int y)
        {
            this.form.log("Checking if game is won");
            this.form.log("Field has size " + field.GetLength(0) + "," + field.GetLength(1));
            for (int i = 0; i < field.GetLength(0); i++)
            {    //Alle Felder der aktuellen Zeile
                this.form.log("Checking field " + i + "|" + y);
                if (field[i, y] != currentPlayerString)      //Abbrechen sobald ein Feld der Reihe nicht dem aktuellen Spieler gehört
                    break;
                if (i == field.GetLength(0) - 1)              //Spiel ist gewonnen da alle Felder geprüft wurden und jedes dem aktuellen Spieler gehören
                    return true;
            }
            for (int i = 0; i < field.GetLength(1); i++)
            { //Alle Felder der aktuellen Spalte
                this.form.log("Checking field " + x + "|" + i);
                if (field[x, i] != currentPlayerString)      //Abbrechen sobald ein Feld der Reihe nicht dem aktuellen Spieler gehört
                    break;
                if (i == field.GetLength(1) - 1)              //Spiel ist gewonnen da alle Felder geprüft wurden und jedes dem aktuellen Spieler gehören
                    return true;
            }
            if (x == y)
            {                                   //Auf Diagonalen
                for (int i = 0; i < field.GetLength(0); i++)
                {//Alle Felder der Diagonalen
                    this.form.log("Checking field " + i + "|" + i);
                    if (field[i, i] != currentPlayerString)  //Abbrechen sobald ein Feld der Reihe nicht dem aktuellen Spieler gehört
                        break;
                    if (i == field.GetLength(0) - 1)          //Spiel ist gewonnen da alle Felder geprüft wurden und jedes dem aktuellen Spieler gehören
                        return true;
                }
            }
            for (int i = 0; i < field.GetLength(0); i++)
            {    //Alle Felder der 2. Diagonalen
                this.form.log("Checking field " + i + "|" + (field.GetLength(0) - 1 - i).ToString());
                if (field[i, field.GetLength(0) - 1 - i] != currentPlayerString)   //Abbrechen sobald ein Feld der Reihe nicht dem aktuellen Spieler gehört     //TODO FIX
                    break;
                if (i == field.GetLength(0) - 1)              //Spiel ist gewonnen da alle Felder geprüft wurden und jedes dem aktuellen Spieler gehören
                    return true;
            }
            return false;                                   //Keiner der Tests war erfolgreich, das Spiel ist also nicht gewonnen
        }

        public override string ToString()
        {
            return "Game[X[" + playerX.ToString() + "], O[" + playerO.ToString() + "], currentPlayer[" + currentPlayerString + "]]";
        }

        /// <summary>
        /// Kurzbeschreibung des Spieles (Ohne IP Adressen)
        /// </summary>
        public string Description
        {
            get
            {
                return "Game[X[" + playerX.name + "], O[" + playerO.name + "], currentPlayer[" + currentPlayerString + "]]";
            }
        }

    }
}
