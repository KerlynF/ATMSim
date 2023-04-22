using ATMSim;
using ATMSimTests.Fakes;
using FluentAssertions;
using System.Security.Cryptography;

namespace ATMSimTests
{
    public class AuthorizerTests
    {


        private static string CrearCuentaYTarjeta(IAutorizador autorizador, TipoCuenta tipoCuenta, int balanceInicial, string binTarjeta, string pin, int sobregiro = 0)
        {
            string numeroCuenta;
            if(tipoCuenta == TipoCuenta.Corriente){
               numeroCuenta = autorizador.CrearCuenta(tipoCuenta, balanceInicial, sobregiro);
            }
            else{
                numeroCuenta = autorizador.CrearCuenta(tipoCuenta, balanceInicial);
            }
            
            string numeroTarjeta = autorizador.CrearTarjeta(binTarjeta, numeroCuenta);
            autorizador.AsignarPin(numeroTarjeta, pin);
            return numeroTarjeta;
        }

        private static IAutorizador CrearAutorizador(string nombre, IHSM hsm) => new Autorizador(nombre, hsm);

        public byte[] Encriptar(string textoPlano, byte[] llaveEnClaro)
        {
            const int TAMANO_LLAVE = 32;

            byte[] llave = llaveEnClaro.Skip(0).Take(TAMANO_LLAVE).ToArray();
            byte[] iv = llaveEnClaro.Skip(TAMANO_LLAVE).ToArray();
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
        //MODIFIED THIS TEST DUE TO THE NEW FEATURE
        [Fact]
        public void Accounts_of_type_checking_allow_overdraft()
        {
            // ARRANGE
            IHSM hsm = new HSM();
            IAutorizador sut = CrearAutorizador("Autorizador", hsm);
            ComponentesLlave llave = hsm.GenerarLlave();
            sut.InstalarLlave(llave.LlaveEncriptada);
            string numeroTarjeta = CrearCuentaYTarjeta(sut, TipoCuenta.Corriente, 10_000, "455555", "1234", 300);
            byte[] criptogramaPin = Encriptar("1234", llave.LlaveEnClaro);

            // ACT
            RespuestaRetiro respuesta = sut.AutorizarRetiro(numeroTarjeta, 15_500, criptogramaPin);

            // ASSERT
               respuesta.MontoAutorizado.Should().BeNull();
               respuesta.BalanceLuegoDelRetiro.Should().BeNull();
               respuesta.CodigoRespuesta.Should().Be(51);

        }

        [Fact]
        public void Balance_Inquiry_with_incorrect_pin_return_respcode_55()
        {
            // ARRANGE
            IHSM hsm = new HSM();
            IAutorizador sut = CrearAutorizador("Autorizador", hsm);
            ComponentesLlave llave = hsm.GenerarLlave();
            sut.InstalarLlave(llave.LlaveEncriptada);
            string numeroTarjeta = CrearCuentaYTarjeta(sut, TipoCuenta.Corriente, 10_000, "455555", "1234");

            byte[] criptogramaPinIncorrecto = Encriptar("9999", llave.LlaveEnClaro);

            // ACT
            RespuestaConsultaDeBalance respuesta = sut.ConsultarBalance(numeroTarjeta, criptogramaPinIncorrecto);

            // ASSERT
            respuesta.CodigoRespuesta.Should().Be(55);
            respuesta.BalanceActual.Should().BeNull();

        }

    }
}