using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ATMSim
{
    public class Tarjeta
    {
        public string Numero { get; private set; }

        //magic number a const
        public static string EnmascararNumero(string numeroTarjeta)
        {
            const int CardNumberMaskLength = 10;
            return numeroTarjeta[0..6] + new String('*', numeroTarjeta.Length - CardNumberMaskLength) + numeroTarjeta[^4..];
        }


        public string NumeroCuenta { get; private set; }

        public Tarjeta(string numero, string numeroCuenta, bool contieneDigitoVerificador = false)
        {
            if (!Regex.Match(numero, @"[0-9]{15,19}").Success)
                throw new ArgumentException("Numero de tarjeta inválido");

            if (contieneDigitoVerificador)
            {
                if (!ValidarIntegridad(numero))
                        throw new ArgumentException("Dígito verificador inválido");
            }
            else
            {
                numero = numero + CalcularDigitoVerificacion(numero);
            }

            NumeroCuenta = numeroCuenta;
            Numero = numero;
        }

        // used switch statement to simplify logic
        public static int CalcularDigitoVerificacion(string numeroSinDigitoVerificador)
         {
            int sum = 0;
            int count = 1;
            for (int n = numeroSinDigitoVerificador.Length - 1; n >= 0; n -= 1)
            {
                int multiplo = count % 2 == 0 ? 1 : 2;
                switch (multiplo)
                {
                    case 1:
                        sum += (int)char.GetNumericValue(numeroSinDigitoVerificador[n]);
                        break;
                    case 2:
                        int prod = (int)char.GetNumericValue(numeroSinDigitoVerificador[n]) * 2;
                        sum += prod > 9 ? prod - 9 : prod;
                        break;
                }
                count++;
            }
            return 10 - (sum % 10);
        }
        public static bool ValidarIntegridad(string numero)
        {
            // Es lo equivalente a `numero[:-1]` en python:
            string numeroSinDigitoVerificador = numero[..^1];

            // Es lo equivalente a `numero[-1]` en python:
            int digitoVerificadorAValidar = (int) char.GetNumericValue(numero[^1]);

            return CalcularDigitoVerificacion(numeroSinDigitoVerificador) == digitoVerificadorAValidar;
        }
    }
}
