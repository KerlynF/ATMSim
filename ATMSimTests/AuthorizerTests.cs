using ATMSim;
using ATMSimTests.Fakes;
using FluentAssertions;
using System.Security.Cryptography;

namespace ATMSimTests
{
    public class AuthorizerTests
    {


        private static string CrearCuentaYTarjeta(IAutorizador autorizador, TipoCuenta tipoCuenta, int balanceInicial, string binTarjeta, string pin)
        {
            string numeroCuenta = autorizador.CrearCuenta(tipoCuenta, balanceInicial);
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

        [Fact]
        public void Accounts_of_type_checking_allow_overdraft()
        {
            // ARRANGE
            IHSM hsm = new HSM();
            IAutorizador sut = CrearAutorizador("Autorizador", hsm);
            ComponentesLlave llave = hsm.GenerarLlave();
            sut.InstalarLlave(llave.LlaveEncriptada);
            string numeroTarjeta = CrearCuentaYTarjeta(sut, TipoCuenta.Corriente, 10_000, "455555", "1234");
            byte[] criptogramaPin = Encriptar("1234", llave.LlaveEnClaro);

            // ACT
            RespuestaRetiro respuesta = sut.AutorizarRetiro(numeroTarjeta, 15_500, criptogramaPin);

            // ASSERT
            respuesta.MontoAutorizado.Should().Be(15_500);
            respuesta.BalanceLuegoDelRetiro.Should().Be(-5_500);
            respuesta.CodigoRespuesta.Should().Be(0);

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

        //NUEVO CASO DE PRUEBA: Verificar que una consulta de saldo con el PIN correcto devuelve el saldo actual. 

        [Fact]
        public void Balance_Inquiry_with_correct_pin_returns_accountBalance()
        {
            // ARRANGE
            IHSM hsm = new HSM();
            IAutorizador sut = CrearAutorizador("Autorizador", hsm);
            ComponentesLlave llave = hsm.GenerarLlave();
            sut.InstalarLlave(llave.LlaveEncriptada);
            string numeroTarjeta = CrearCuentaYTarjeta(sut, TipoCuenta.Corriente, 10_000, "455555", "1234");
            byte[] criptogramaPin = Encriptar("1234", llave.LlaveEnClaro);

            // ACT
            RespuestaConsultaDeBalance respuesta = sut.ConsultarBalance(numeroTarjeta, criptogramaPin);

            // ASSERT
            respuesta.CodigoRespuesta.Should().Be(0);
            respuesta.BalanceActual.Should().Be(10_000);
        }

        //NUEVO CASO DE PRUEBA: Verifica que una cuenta con saldo suficiente pueda usarse para autorizar un retiro. 
        [Fact]
        public void Accounts_with_sufficient_balance_can_withdraw()
        {
            // ARRANGE
            IHSM hsm = new HSM();
            IAutorizador sut = CrearAutorizador("Autorizador", hsm);
            ComponentesLlave llave = hsm.GenerarLlave();
            sut.InstalarLlave(llave.LlaveEncriptada);
            string numeroTarjeta = CrearCuentaYTarjeta(sut, TipoCuenta.Corriente, 10_000, "455555", "1234");
            byte[] criptogramaPin = Encriptar("1234", llave.LlaveEnClaro);

            // ACT
            RespuestaRetiro respuesta = sut.AutorizarRetiro(numeroTarjeta, 5_000, criptogramaPin);

            // ASSERT
            respuesta.MontoAutorizado.Should().Be(5_000);
            respuesta.BalanceLuegoDelRetiro.Should().Be(5_000);
            respuesta.CodigoRespuesta.Should().Be(0);
        }

        //CASO NUEVO #3
      
    }
}