using ATMSim;
using ATMSimTests.Fakes;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ATMSimTests
{
    public class AtmSwitchTests
    {
        const string teclasRetiroConRecibo = "AAA";
        const string teclasRetiroSinRecibo = "AAC";
        const string teclasConsultaDeBalance = "B";

        const string binTarjeta = "459413";

        private static IATM CrearATMFalso(string nombre) => new FakeATM(nombre);

        private static string CrearCuentaYTarjeta(IAutorizador autorizador, TipoCuenta tipoCuenta, int balanceInicial, string binTarjeta, string pin)
        {
            string numeroCuenta = autorizador.CrearCuenta(tipoCuenta, balanceInicial);
            string numeroTarjeta = autorizador.CrearTarjeta(binTarjeta, numeroCuenta);
            autorizador.AsignarPin(numeroTarjeta, pin);
            return numeroTarjeta;
        }

        private static IAutorizador CrearAutorizador(string nombre, IHSM hsm) => new Autorizador(nombre, hsm);

        private static void RegistrarATMEnSwitch(IATM atm, IATMSwitch atmSwitch, IHSM hsm)
        {
            ComponentesLlave llaveATM = hsm.GenerarLlave();
            atm.InstalarLlave(llaveATM.LlaveEnClaro);
            atmSwitch.RegistrarATM(atm, llaveATM.LlaveEncriptada);
        }


        private static void RegistrarAutorizadorEnSwitch(IAutorizador autorizador, IATMSwitch atmSwitch, IHSM hsm)
        {
            ComponentesLlave llaveAutorizador = hsm.GenerarLlave();
            autorizador.InstalarLlave(llaveAutorizador.LlaveEncriptada);
            atmSwitch.RegistrarAutorizador(autorizador, llaveAutorizador.LlaveEncriptada);
            atmSwitch.AgregarRuta(binTarjeta, autorizador.Nombre);
        }

        private static IATMSwitch CrearSwitch(IHSM hsm, IConsoleWriter consoleWriter)
        {
            IATMSwitch atmSwitch = new ATMSwitch(hsm, consoleWriter);
            atmSwitch.AgregarConfiguracionOpKey(new ConfiguracionOpKey()
            {
                Teclas = teclasRetiroConRecibo,
                TipoTransaccion = TipoTransaccion.Retiro,
                Recibo = true
            });
            atmSwitch.AgregarConfiguracionOpKey(new ConfiguracionOpKey()
            {
                Teclas = teclasRetiroSinRecibo,
                TipoTransaccion = TipoTransaccion.Retiro,
                Recibo = false
            });
            atmSwitch.AgregarConfiguracionOpKey(new ConfiguracionOpKey()
            {
                Teclas = teclasConsultaDeBalance,
                TipoTransaccion = TipoTransaccion.Consulta,
                Recibo = false
            });
            return atmSwitch;
        }

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
        public void Withdrawal_with_balance_on_account_is_successful()
        {
            // ARRANGE
            FakeConsoleWriter consoleWriter = new FakeConsoleWriter();

            IHSM hsm = new HSM();

            IATMSwitch sut = CrearSwitch(hsm, consoleWriter);

            IATM atm = CrearATMFalso("AJP001");
            RegistrarATMEnSwitch(atm, sut, hsm);

            IAutorizador autorizador = CrearAutorizador("AutDB", hsm);
            RegistrarAutorizadorEnSwitch(autorizador, sut, hsm);

            string numeroTarjeta = CrearCuentaYTarjeta(autorizador, TipoCuenta.Ahorros, 20_000, binTarjeta, "1234");

            byte[] llaveEnClaro = ((FakeATM)atm).Llave;
            byte[] criptogramaPin = Encriptar("1234", llaveEnClaro);

            // ACT
            List<Comando> comandosRespuesta = sut.Autorizar(atm, teclasRetiroConRecibo, numeroTarjeta, 200, criptogramaPin);

            // ASSERT
            comandosRespuesta.Should().HaveCountGreaterThanOrEqualTo(1);
            comandosRespuesta.Should().Contain(x => x.GetType() == typeof(ComandoDispensarEfectivo));
            
            ComandoDispensarEfectivo comando = (ComandoDispensarEfectivo)comandosRespuesta
                .Where(x => x.GetType() == typeof(ComandoDispensarEfectivo)).Single();
            comando.Monto.Should().Be(200);

        }



    }
}
