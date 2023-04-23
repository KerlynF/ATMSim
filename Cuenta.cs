using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ATMSim
{
    public class IntentoSobregiroCuentaDeAhorrosException : Exception { }
    public class EstaCuentaNoContieneEseCampoException : Exception { }
    public enum TipoCuenta
    {
        Ahorros,
        Corriente
    }

    internal class Cuenta
    {
        public TipoCuenta Tipo { get; private set; }
        public string Numero { get; private set; }
        int montoSobregiro;
        double monto = 0;
        public int MontoSobregiro{
            get {
                if(this.Tipo == TipoCuenta.Corriente){
                    return montoSobregiro;
                }
                throw new EstaCuentaNoContieneEseCampoException();
            }

            set{
                if(this.Tipo != TipoCuenta.Corriente){
                    throw new EstaCuentaNoContieneEseCampoException();
                }
                else{
                    montoSobregiro = value;
                }
            }
        }
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

        public Cuenta(string numero, TipoCuenta tipo, double monto = 0, int montoSobregiro = 0) 
        {
            if (!Regex.Match(numero, @"[0-9]+").Success)
                throw new ArgumentException("Numero de cuenta inválido");

            Numero = numero;
            Tipo = tipo;
            Monto = monto;
            if(tipo == TipoCuenta.Corriente){
                MontoSobregiro = montoSobregiro;
            }
        }
        
    }
}
