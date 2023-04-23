using ATMSim;
using ATMSimTests.Fakes;
using FluentAssertions;
using System.Security.Cryptography;

namespace ATMSimTests
{
    public class AuthorizerTests
    {


        private static string CrearCuentaYTarjeta(IAutorizador autorizador, TipoCuenta tipoCuenta, double balanceInicial, string binTarjeta, string pin, int sobregiro = 0)
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

        //NUEVO CASO DE PRUEBA 1 : Verifica que una consulta de saldo con el PIN correcto devuelve el saldo actual. 

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

        //NUEVO CASO DE PRUEBA 2 : Verifica que una cuenta con saldo suficiente pueda usarse para autorizar un retiro. 

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


        // NUEVO CASO DE PRUEBA 3: Verifica que una cuenta  no puede realizar un retiro si no tiene fondos suficientes

        [Fact]
        public void Accounts_with_insufficient_balance_cannot_withdraw()
        {
            // ARRANGE
            IHSM hsm = new HSM();
            IAutorizador sut = CrearAutorizador("Autorizador", hsm);
            ComponentesLlave llave = hsm.GenerarLlave();
            sut.InstalarLlave(llave.LlaveEncriptada);
            string numeroTarjeta = CrearCuentaYTarjeta(sut, TipoCuenta.Ahorros, 1_000, "455555", "1234");
            byte[] criptogramaPin = Encriptar("1234", llave.LlaveEnClaro);

            // ACT
            RespuestaRetiro respuesta = sut.AutorizarRetiro(numeroTarjeta, 2_000, criptogramaPin);

            // ASSERT
            respuesta.CodigoRespuesta.Should().Be(51);

        }

        //Prueba del limite de retiro asignado (RQ05-LIMITE DE RETIRO)
        [Fact]
        public void Withdrawal_Amount_Over_the_Limit()
        {
            // ARRANGE
            IHSM hsm = new HSM();
            IAutorizador sut = CrearAutorizador("Autorizador", hsm);
            ComponentesLlave llave = hsm.GenerarLlave();
            sut.InstalarLlave(llave.LlaveEncriptada);
            sut.AsignarLimiteRetiro(5_000);

            string numeroTarjeta = CrearCuentaYTarjeta(sut, TipoCuenta.Ahorros, 20_000, "455555", "1234");
            byte[] criptogramaPin = Encriptar("1234", llave.LlaveEnClaro);

            // ACT
            RespuestaRetiro respuesta = sut.AutorizarRetiro(numeroTarjeta, 10_000, criptogramaPin);

            // ASSERT
            respuesta.MontoAutorizado.Should().BeNull();
            respuesta.BalanceLuegoDelRetiro.Should().BeNull();
            respuesta.CodigoRespuesta.Should().Be(911);
        }

        //prueba del Req01-Montos Decimales (montos y balances en decimales)
        [Fact]
        public void Withdrawals_with_two_decimal()
{
            // ARRANGE
            IHSM hsm = new HSM();
            IAutorizador sut = CrearAutorizador("Autorizador", hsm);
            ComponentesLlave llave = hsm.GenerarLlave();
            sut.InstalarLlave(llave.LlaveEncriptada);
            string numeroTarjeta = CrearCuentaYTarjeta(sut, TipoCuenta.Ahorros, 6_000.029, "455555", "1234");

            byte[] criptogramaPinCorrecto = Encriptar("1234", llave.LlaveEnClaro);

            // ACT
            RespuestaRetiro respuesta = sut.AutorizarRetiro(numeroTarjeta, 3_000.053, criptogramaPinCorrecto);

            // ASSERT
            respuesta.MontoAutorizado.Should().Be(3_000.05);
            respuesta.BalanceLuegoDelRetiro.Should().Be(2_999.98);
            respuesta.CodigoRespuesta.Should().Be(0);


        }

    }
}