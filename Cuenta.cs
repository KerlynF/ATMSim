using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ATMSim
{
    public class IntentoSobregiroCuentaDeAhorrosException : Exception { }
    public enum TipoCuenta
    {
        Ahorros,
        Corriente
    }

    internal class Cuenta
    {
        public TipoCuenta Tipo { get; private set; }
        public string Numero { get; private set; }

        double monto = 0;
        public double Monto { 
            get { return monto; } 
            set 
            {
                if (Tipo == TipoCuenta.Ahorros && value < 0)
                    throw new IntentoSobregiroCuentaDeAhorrosException();
                else
                    monto = value;
            } 
        }

        public Cuenta(string numero, TipoCuenta tipo, double monto = 0) 
        {
            if (!Regex.Match(numero, @"[0-9]+").Success)
                throw new ArgumentException("Numero de cuenta inválido");

            Numero = numero;
            Tipo = tipo;
            Monto = monto;
        }
        
    }
}
