using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ATMSim
{
    public interface IAutorizador
    {
        public RespuestaConsultaDeBalance ConsultarBalance(string numeroTarjeta, byte[] criptogramaPin);
        public RespuestaRetiro AutorizarRetiro(string numeroTarjeta, double montoRetiro, byte[] criptogramaPin);
        public string CrearTarjeta(string bin, string numeroCuenta);
        public string CrearCuenta(TipoCuenta tipo, double montoDeApertura = 0);
        public string Nombre { get; }
        public void AsignarPin(string numeroTarjeta, string pin);
        public void InstalarLlave(byte[] criptogramaLlaveAutorizador);


    }

    public class Respuesta
    {
        public int CodigoRespuesta { get; private set; }
        public Respuesta(int codigoRespuesta) => CodigoRespuesta = codigoRespuesta;
    }

    public class RespuestaConsultaDeBalance : Respuesta
    {
        public double? BalanceActual { get; private set; }
        public RespuestaConsultaDeBalance(int codigoRespuesta, double? balanceActual = null) : base(codigoRespuesta) 
            => BalanceActual = balanceActual;
    }

    public class RespuestaRetiro : Respuesta
    {
        public double? MontoAutorizado { get; private set; }
        public double? BalanceLuegoDelRetiro { get; private set; }
        public RespuestaRetiro(int codigoRespuesta, double? montoAutorizado = null, double ? balanceLuegoDelRetiro = null) : base(codigoRespuesta)
            => (MontoAutorizado, BalanceLuegoDelRetiro) = (montoAutorizado, balanceLuegoDelRetiro);


    }

    public class Autorizador : IAutorizador
    {
        private const int tamanoNumeroTarjeta  = 16; 
        private const int tamanoNumeroCuenta = 9;
        private const string prefijoDeCuenta = "7";

        private Random random = new Random();
        public string Nombre { get; private set; }

        private IHSM hsm;

        List<Tarjeta> tarjetas = new List<Tarjeta>();
        List<Cuenta> cuentas = new List<Cuenta>();

        Dictionary<string, byte[]> pinesTarjetas = new Dictionary<string, byte[]>();

        private byte[]? criptogramaLlaveAutorizador;

        public Autorizador(string nombre, IHSM hsm)
        {
            Nombre = nombre;
            this.hsm = hsm;
        }

        public void AsignarPin(string numeroTarjeta, string pin) 
        {
            if (!Regex.Match(pin, @"[0-9]{4}").Success)
                throw new ArgumentException("El Pin debe ser numérico, de 4 dígitos");

            if (!TarjetaExiste(numeroTarjeta))
                throw new ArgumentException("Número de tarjeta no reconocido");

            byte[] criptogramaPin = hsm.EncriptarPinConLlaveMaestra(pin);

            pinesTarjetas[numeroTarjeta] = criptogramaPin;

        }

        private bool TarjetaExiste(string numeroTarjeta) => tarjetas.Where(x => x.Numero == numeroTarjeta).Any();

        private Tarjeta ObtenerTarjeta(string numeroTarjeta) => tarjetas.Where(x => x.Numero == numeroTarjeta).Single();

        private byte[] ObtenerCriptogramaPinTarjeta(string numeroTarjeta) => pinesTarjetas[numeroTarjeta];

        private bool TarjetaTienePin(string numeroTarjeta) => pinesTarjetas.ContainsKey(numeroTarjeta);

        private bool CuentaExiste(string numeroCuenta) => cuentas.Where(x => x.Numero == numeroCuenta).Any();

        private Cuenta ObtenerCuenta(string numeroCuenta) => cuentas.Where(x => x.Numero == numeroCuenta).Single();

        public RespuestaConsultaDeBalance ConsultarBalance(string numeroTarjeta, byte[] criptogramaPin)
        {
            if (!TarjetaExiste(numeroTarjeta))
                return new RespuestaConsultaDeBalance(56); // Esta tarjeta no se reconoce

            if (!TarjetaTienePin(numeroTarjeta))
                return new RespuestaConsultaDeBalance(55); // Esta tarjeta no tiene pin asignado

            byte[] criptogramaPinReal = ObtenerCriptogramaPinTarjeta(numeroTarjeta);

            if(!hsm.ValidarPin(criptogramaPin, criptogramaLlaveAutorizador, criptogramaPinReal))
                return new RespuestaConsultaDeBalance(55); // Pin incorrecto

            Tarjeta tarjeta = ObtenerTarjeta(numeroTarjeta);
            Cuenta cuenta = ObtenerCuenta(tarjeta.NumeroCuenta);

            return new RespuestaConsultaDeBalance(0, Math.Round(cuenta.Monto, 2)); // Autorizado
        }

        public RespuestaRetiro AutorizarRetiro(string numeroTarjeta, double montoRetiro, byte[] criptogramaPin)
        {
            if (!TarjetaExiste(numeroTarjeta))
                return new RespuestaRetiro(56); // Esta tarjeta no se reconoce

            if (!TarjetaTienePin(numeroTarjeta))
                return new RespuestaRetiro(55); // Esta tarjeta no tiene pin asignado

            byte[] criptogramaPinReal = ObtenerCriptogramaPinTarjeta(numeroTarjeta);

            if (!hsm.ValidarPin(criptogramaPin, criptogramaLlaveAutorizador, criptogramaPinReal))
                return new RespuestaRetiro(55); // Pin incorrecto

            Tarjeta tarjeta = ObtenerTarjeta(numeroTarjeta);
            Cuenta cuenta = ObtenerCuenta(tarjeta.NumeroCuenta);

            if (cuenta.Tipo == TipoCuenta.Ahorros && cuenta.Monto < montoRetiro)
                return new RespuestaRetiro(51); // Fondos Insuficientes
            else
            {
                cuenta.Monto -= montoRetiro;
                return new RespuestaRetiro(0, Math.Round(montoRetiro, 2), Math.Round(cuenta.Monto, 2)); // Autorizado
            }
            
        }

        public void InstalarLlave(byte[] criptogramaLlaveAutorizador)
        {
            this.criptogramaLlaveAutorizador = criptogramaLlaveAutorizador;
        }

        public string CrearTarjeta(string bin, string numeroCuenta)
        {
            if (!Regex.Match(bin, @"[0-9]{6}").Success)
                throw new ArgumentException("El Bin debe ser numérico, de 6 dígitos");

            if (bin[0] != '4')
                throw new NotImplementedException("Sólo se soportan tarjertas VISA, que inician con 4");

            if (!cuentas.Where(x => x.Numero == numeroCuenta).Any())
                throw new NotImplementedException("Número de cuenta no encontrado");

            string numeroSinDigitoVerificador;
            do 
            {
                // repetir hasta encontrar un número único (sin tomar en cuenta el digito verificador)
                numeroSinDigitoVerificador = GenerarNumeroAleatorio(tamanoNumeroTarjeta - 1, bin);
            } 
            while (tarjetas.Where(x => x.Numero[..^1] == numeroSinDigitoVerificador).Any());

            Tarjeta tarjeta = new Tarjeta(numeroSinDigitoVerificador, numeroCuenta);
            tarjetas.Add(tarjeta);

            return tarjeta.Numero;
        }

        public string CrearCuenta(TipoCuenta tipo, double montoDeApertura = 0)
        {
            string numero;
            do
            {
                // repetir hasta encontrar un número único
                numero = GenerarNumeroAleatorio(tamanoNumeroCuenta, prefijoDeCuenta);
            } 
            while (CuentaExiste(numero));

            Cuenta cuenta = new Cuenta(numero, tipo, Math.Round(montoDeApertura, 2));
            cuentas.Add(cuenta);

            return cuenta.Numero;
        }

        private string GenerarNumeroAleatorio(int cantidadPosiciones, string prefijo="", string sufijo="")
        {
            const string digitos = "0123456789";

            if (!Regex.Match(prefijo + sufijo, @"[0-9]+").Success)
                throw new ArgumentException("El Sufijo y el Prefijo deben ser caracteres numéricos");

            if (cantidadPosiciones <= prefijo.Length + sufijo.Length)
                throw new ArgumentException("Debe haber al menos una posición que no sean parte del prefijo/sufijo");

            // Arreglar el length
            string numero =  new string(Enumerable.Repeat(digitos, cantidadPosiciones - prefijo.Length - sufijo.Length)
                                                  .Select(s => s[random.Next(s.Length)])
                                                  .ToArray());
            return prefijo + numero + sufijo;

        }

    }
}
