using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace raufnw3
{
    class Program
    {
        public class GameState
        {
            public string Gamer1 { get; set; } = "";
            public string Gamer2 { get; set; } = "";
            public int[][] Game { get; set; } = new int[3][] {
                new int[] { 0, 0, 0 },
                new int[] { 0, 0, 0 },
                new int[] { 0, 0, 0 }
            };
        }

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== UDP Tic-Tac-Toe Oyunu ===");
            Console.Write("Rolu seçin (1 - Server/Gamer1 (X), 2 - Client/Gamer2 (O)): ");
            string role = Console.ReadLine();

            Console.Write("Adınızı daxil edin: ");
            string playerName = Console.ReadLine();

            int localPort = 5000;
            int remotePort = 5001;
            string remoteIp = "127.0.0.1";

            if (role == "2")
            {
                localPort = 5001;
                remotePort = 5000;
            }

            UdpClient udpClient = new UdpClient(localPort);
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);

            GameState gameState = new GameState();
            bool isMyTurn = (role == "1");
            int mySymbol = (role == "1") ? 1 : -1;

            if (role == "1")
            {
                gameState.Gamer1 = playerName;
                Console.WriteLine("İkinci oyunçunun qoşulması gözlənilir...");

                var receivedResult = await udpClient.ReceiveAsync();
                string msg = Encoding.UTF8.GetString(receivedResult.Buffer);
                gameState.Gamer2 = msg;
                remoteEndPoint = receivedResult.RemoteEndPoint;

                Console.WriteLine($"Oyunçu 2 qoşuldu: {gameState.Gamer2}");
                SaveGame(gameState);
            }
            else
            {
                gameState.Gamer2 = playerName;
                byte[] nameBytes = Encoding.UTF8.GetBytes(playerName);
                await udpClient.SendAsync(nameBytes, nameBytes.Length, remoteEndPoint);
                Console.WriteLine("Serverə qoşulundu. Oyun başlayır!");
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var result = await udpClient.ReceiveAsync();
                        string jsonString = Encoding.UTF8.GetString(result.Buffer);

                        var receivedState = JsonSerializer.Deserialize<GameState>(jsonString);
                        if (receivedState != null)
                        {
                            gameState = receivedState;
                            PrintBoard(gameState);

                            if (CheckWin(gameState.Game, out int winner))
                            {
                                if (winner != 0)
                                    Console.WriteLine(winner == mySymbol ? "Təbriklər, qazandınız!" : "Uduzdunuz!");
                                else
                                    Console.WriteLine("Oyun heç-heçə bitdi!");
                                break;
                            }

                            Console.WriteLine("Sıra sizdədir. Gediş edin (sətir sütun, məsələn: 0 1):");
                            isMyTurn = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Xəta baş verdi: {ex.Message}");
                }
            });

            while (true)
            {
                PrintBoard(gameState);

                if (CheckWin(gameState.Game, out int winner))
                {
                    if (winner != 0)
                        Console.WriteLine(winner == mySymbol ? "Təbriklər, qazandınız!" : "Uduzdunuz!");
                    else
                        Console.WriteLine("Oyun heç-heçə bitdi!");
                    break;
                }

                if (!isMyTurn)
                {
                    await Task.Delay(500);
                    continue;
                }

                Console.WriteLine("Sizin növbənizdir (Sətir Sütun, məsələn: 0 2):");
                string input = Console.ReadLine();
                var parts = input?.Split(' ');

                if (parts == null || parts.Length != 2 ||
                    !int.TryParse(parts[0], out int r) ||
                    !int.TryParse(parts[1], out int c) ||
                    r < 0 || r > 2 || c < 0 || c > 2 || gameState.Game[r][c] != 0)
                {
                    Console.WriteLine("Yanlış gediş! Yenidən cəhd edin.");
                    continue;
                }

                gameState.Game[r][c] = mySymbol;
                isMyTurn = false;

                string jsonOutput = JsonSerializer.Serialize(gameState);
                byte[] data = Encoding.UTF8.GetBytes(jsonOutput);
                await udpClient.SendAsync(data, data.Length, remoteEndPoint);
                SaveGame(gameState);

                Console.WriteLine("Gediş göndərildi. Qarşı tərəf gözlənilir...");
            }
        }

        static void PrintBoard(GameState state)
        {
            Console.Clear();
            Console.WriteLine($"Oyunçu 1 (X): {state.Gamer1} vs Oyunçu 2 (0): {state.Gamer2}");
            Console.WriteLine("  0 1 2");
            for (int i = 0; i < 3; i++)
            {
                Console.Write(i + " ");
                for (int j = 0; j < 3; j++)
                {
                    char symbol = state.Game[i][j] == 1 ? 'X' : (state.Game[i][j] == -1 ? '0' : '.');
                    Console.Write(symbol + " ");
                }
                Console.WriteLine();
            }
        }

        static bool CheckWin(int[][] board, out int winner)
        {
            winner = 0;

            for (int i = 0; i < 3; i++)
            {
                if (board[i][0] != 0 && board[i][0] == board[i][1] && board[i][1] == board[i][2])
                {
                    winner = board[i][0];
                    return true;
                }
                if (board[0][i] != 0 && board[0][i] == board[1][i] && board[1][i] == board[2][i])
                {
                    winner = board[0][i];
                    return true;
                }
            }

            if (board[0][0] != 0 && board[0][0] == board[1][1] && board[1][1] == board[2][2])
            {
                winner = board[0][0];
                return true;
            }
            if (board[0][2] != 0 && board[0][2] == board[1][1] && board[1][1] == board[2][0])
            {
                winner = board[0][2];
                return true;
            }

            foreach (var row in board)
            {
                foreach (var cell in row)
                {
                    if (cell == 0) return false;
                }
            }

            return true;
        }

        static void SaveGame(GameState state)
        {
            try
            {
                string fileName = $"{state.Gamer1}_123-{state.Gamer2}_123.json";
                string jsonString = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(fileName, jsonString);
            }
            catch (Exception)
            {
            }
        }
    }
}