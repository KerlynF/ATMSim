using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ATMSim
{
    public interface IATM
    {
        public IATMSwitch? Switch { get; set; }
        public string Nombre { get; set; }

        public bool Configurado { get; }

        public void EnviarTransactionRequest(string opKeyBuffer, string numeroTarjeta, string pin, int monto = 0);
        public void InstalarLlave(byte[] llave);
        public void Reestablecer();

    }
    public class ATMNoEstaRegistradoException : Exception { }

    public class Comando { }

    public class ComandoDispensarEfectivo : Comando
    {
        public double Monto { get; private set; }
        public ComandoDispensarEfectivo(double monto) { Monto = monto; }
    }
    
    public class ComandoImprimirRecibo : Comando
    {
        public string TextoRecibo { get; private set; }
        public ComandoImprimirRecibo(string textoRecibo) { TextoRecibo = textoRecibo; }
    }
    
    public class ComandoMostrarInfoEnPantalla : Comando
    {
        public string TextoPantalla { get; private set; }
        public bool Error { get; private set; }
        public ComandoMostrarInfoEnPantalla(string textoPantalla, bool error = false) => (TextoPantalla, Error) = (textoPantalla, error);
    }

    public class ComandoDevolverTarjeta : Comando { }

    public class ATM: IATM
    {
        private const int TAMANO_LLAVE = 32; // bytes


        byte[]? tpk;
        public IATMSwitch? Switch { get; set; } 
        public string Nombre { get; set; }

        private IConsoleWriter consoleWriter;

        private IThreadSleeper threadSleeper;

        public ATM(string nombre, IConsoleWriter consoleWriter, IThreadSleeper threadSleeper)
        {
            Nombre = nombre;
            this.consoleWriter = consoleWriter;
            this.threadSleeper = threadSleeper;
        }

        public bool Configurado { get { return tpk != null && Switch != null; } }

        public void EnviarTransactionRequest(string opKeyBuffer, string numeroTarjeta, string pin, int monto = 0)
        {
            if (!Configurado)
                throw new InvalidOperationException("El ATM aún no está configurado correctamente");

            if (!Regex.Match(pin, @"[0-9]{4}").Success)
                MostrarError("ERROR.\n\nEl Pin debe ser un número de 4 digitos.");

            byte[] criptogramaPin = Encriptar(pin);

            List<Comando> comandosDeRespuesta = Switch.Autorizar(this, opKeyBuffer, numeroTarjeta, monto, criptogramaPin);
            EjecutarListaComandos(comandosDeRespuesta);
        }


        public void InstalarLlave(byte[] llave)
        {
            tpk = llave;
        }

        public void Reestablecer()
        {
            tpk = null;
            Switch = null;
        }



        private void MostrarError(string mensajeDeError)
        {
            List<Comando> comandos = new List<Comando>();
            comandos.Add(new ComandoMostrarInfoEnPantalla(mensajeDeError, true));
            EjecutarListaComandos(comandos);
        }

        private void EjecutarListaComandos(List<Comando> comandos)
        {
            foreach (Comando comando in comandos)
            {
                switch (comando) 
                {
                    case ComandoDispensarEfectivo cmd: EjecutarComandoDispensarEfectivo(cmd); break;
                    case ComandoDevolverTarjeta cmd: EjecutarComandoDevolverTarjeta(cmd); break;
                    case ComandoImprimirRecibo cmd: EjecutarComandoImprimirRecibo(cmd); break; 
                    case ComandoMostrarInfoEnPantalla cmd: EjecutarComandoMostrarInfoEnPantalla(cmd); break;
                    default: throw new InvalidOperationException($"Comando {comando.GetType().Name} no soportado por el ATM");
                }
            }
            consoleWriter.ForegroundColor = ConsoleColor.DarkYellow;
            consoleWriter.WriteLine($"> Fin de la Transaccion\n\n");
            consoleWriter.ResetColor();
        }

        private void EjecutarComandoDispensarEfectivo(ComandoDispensarEfectivo comando)
        {
            threadSleeper.Sleep(1000);
            consoleWriter.ForegroundColor = ConsoleColor.Yellow;
            consoleWriter.Write($"> Efectivo dispensado: ");
            consoleWriter.ResetColor();
            consoleWriter.WriteLine(comando.Monto.ToString());
            threadSleeper.Sleep(2000);
        }

        private void EjecutarComandoDevolverTarjeta(ComandoDevolverTarjeta comando)
        {
            threadSleeper.Sleep(500);
            consoleWriter.ForegroundColor = ConsoleColor.Yellow;
            consoleWriter.WriteLine("> Tarjeta devuelta");
            consoleWriter.ResetColor();
            threadSleeper.Sleep(1000);
        }

        private void EjecutarComandoImprimirRecibo(ComandoImprimirRecibo comando)
        {
            threadSleeper.Sleep(500);
            consoleWriter.ForegroundColor = ConsoleColor.Yellow;
            consoleWriter.WriteLine("> Imprimiento Recibo:");
            consoleWriter.ForegroundColor = ConsoleColor.Black;
            consoleWriter.BackgroundColor = ConsoleColor.White;
            string texto = "\t" + comando.TextoRecibo.Replace("\n", "\n\t"); // Poniendole sangría
            consoleWriter.Write(texto);
            consoleWriter.ResetColor();
            consoleWriter.WriteLine("");
            threadSleeper.Sleep(1500);
        }

        private void EjecutarComandoMostrarInfoEnPantalla(ComandoMostrarInfoEnPantalla comando)
        {
            consoleWriter.ForegroundColor = ConsoleColor.Yellow;
            consoleWriter.WriteLine("> Mostrando pantalla:");
            consoleWriter.ForegroundColor = comando.Error? ConsoleColor.Red : ConsoleColor.White;
            consoleWriter.BackgroundColor = ConsoleColor.DarkBlue;
            string texto = "\t" + comando.TextoPantalla.Replace("\n", "\n\t"); // Poniendole sangría
            consoleWriter.Write(texto);
            consoleWriter.ResetColor();
            consoleWriter.WriteLine("");
            threadSleeper.Sleep(500);
        }


        private byte[] Encriptar(string textoPlano)
        {
            if (!Configurado)
                throw new InvalidOperationException("El ATM aún no está configurado correctamente");

            byte[] llave = tpk.Skip(0).Take(TAMANO_LLAVE).ToArray();
            byte[] iv = tpk.Skip(TAMANO_LLAVE).ToArray();
            using (Aes llaveAes = Aes.Create())
            {
                llaveAes.Key = llave;
                llaveAes.IV = iv;

                ICryptoTransform encriptador = llaveAes.CreateEncryptor();

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encriptador, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(textoPlano);
                        }
                        return ms.ToArray();
                    }
                }

            }


        }



    }
}
