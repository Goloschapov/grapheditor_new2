using Fleck;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions; 
using System.Threading;
using System.Threading.Tasks;

namespace MultiUserPaint
{
    class Program
    {
        public static Dictionary<IWebSocketConnection, List<Color>> Users = new Dictionary<IWebSocketConnection, List<Color>>();
        public static List<Color> Canvas = new List<Color>();
        public static int Width = 800;
        public static int Height = 500;
        static Mutex mutexObj = new Mutex();
        

        static void Main(string[] args)
        {
            GenerateColorList();
            var socket = new WebSocketServer("ws://127.0.0.1:8081/");
            
            IWebSocketConnection connection = null;
            int count = 0;

            socket.Start(conn =>
            {
                conn.OnOpen = () =>
                {
                    count++;
                    connection = conn;
                    Users.Add(connection, Canvas);
                    conn.Send("name|" + count.ToString());
                };

                conn.OnMessage = message =>
                {
                    if (message == "getBackground")
                    {
                        Users[conn] = Canvas; 
                        conn.Send(ColorListToString(Canvas));
                    }
                    else
                    {
                        if (message == "ping test")
                        {
                            conn.Send("pong test");
                        }
                        else
                        {
                            if (message != "")
                            {
                                Refresh(message, conn);
                                SetBackground();
                            }
                        }
                    }
                };
                conn.OnClose = () =>
                {
                    Users.Remove(conn);
                };
            });

            if (connection != null)
            {
                connection.Send("Messaggio di esempio");
            }

            Console.Read();
        }

        static void GenerateColorList()
        {
            for (int i = 0; i < 500 * 800; i++)
                Canvas.Add(Color.FromArgb(255, 255, 255, 254));
        }

        static void Refresh(string message, IWebSocketConnection conn)
        {
            mutexObj.WaitOne();
            List<Color> Message = StringToColorList(message);

            for (int i = 0; i < Message.Count; i++)
                if (Message[i] != Users[conn][i])
                    Canvas[i] = Message[i];

            mutexObj.ReleaseMutex();
        }

        static void SetBackground()
        {
            for (int i = 0; i < Users.Count; i++)
            {
                Users[Users.ElementAt(i).Key] = Canvas;
                Users.ElementAt(i).Key.Send(ColorListToString(Canvas));
            }
        }

        public static string ColorListToString(List<Color> canvas)
        {
            Bitmap pic = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    pic.SetPixel(x, y, canvas[x * Height + y]);
            
            using (var ms = new MemoryStream())
            {
                pic.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                string SigBase64 = Convert.ToBase64String(ms.GetBuffer()); //Get Base64
                return "data:image/png;base64," + SigBase64;
            }

        }

        public static List<Color> StringToColorList(string message)
        {
            List<Color> canvas = new List<Color>();
            var base64Data = Regex.Match(message, @"data:image/(?<type>.+?),(?<data>.+)").Groups["data"].Value;
            Image img = null;
            byte[] bitmapBytes = Convert.FromBase64String(base64Data);
            using (MemoryStream memoryStream = new MemoryStream(bitmapBytes)) { img = Image.FromStream(memoryStream); }

            using (Bitmap bmp = new Bitmap(img))
            {
                for (int x = 0; x < bmp.Width; x++)
                    for (int y = 0; y < bmp.Height; y++)
                        canvas.Add(bmp.GetPixel(x, y));
            }

            return canvas;
        }
    }
}
