using ATMSim;
using ATMSimTests.Fakes;
using FluentAssertions;
using System.Net.NetworkInformation;

namespace ATMSimTests
{
    public class AtmTests
    {
        const string teclasRetiroConRecibo = "AAA";
        const string teclasRetiroSinRecibo = "AAC";
        const string teclasConsultaDeBalance = "B";

        private static IATM CrearATM(string nombre, IConsoleWriter consoleWriter, IThreadSleeper threadSleeper)
            => new ATM(nombre, consoleWriter, threadSleeper);

        private static string CrearCuentaYTarjeta(IAutorizador autorizador, TipoCuenta tipoCuenta, int balanceInicial, string binTarjeta, string pin)
        {
            string numeroCuenta = autorizador.CrearCuenta(tipoCuenta, balanceInicial);
            string numeroTarjeta = autorizador.CrearTarjeta(binTarjeta, numeroCuenta);
            autorizador.AsignarPin(numeroTarjeta, pin);
            return numeroTarjeta;
        }

        private static void RegistrarATMEnSwitch(IATM atm, IATMSwitch atmSwitch, IHSM hsm)
        {
            ComponentesLlave llaveATM = hsm.GenerarLlave();
            atm.InstalarLlave(llaveATM.LlaveEnClaro);
            atmSwitch.RegistrarATM(atm, llaveATM.LlaveEncriptada);
        }

        private static IAutorizador CrearAutorizador(string nombre, IHSM hsm) => new Autorizador(nombre, hsm);

        private static void RegistrarAutorizadorEnSwitch(IAutorizador autorizador, IATMSwitch atmSwitch, IHSM hsm)
        {
            ComponentesLlave llaveAutorizador = hsm.GenerarLlave();
            autorizador.InstalarLlave(llaveAutorizador.LlaveEncriptada);
            atmSwitch.RegistrarAutorizador(autorizador, llaveAutorizador.LlaveEncriptada);
            atmSwitch.AgregarRuta("459413", autorizador.Nombre);
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


        [Fact]
        public void Withdrawal_with_balance_on_account_is_successful()
        {
            // ARRANGE
            FakeConsoleWriter consoleWriter = new FakeConsoleWriter();
            FakeThreadSleeper threadSleeper = new FakeThreadSleeper();

            IHSM hsm = new HSM();

            IATMSwitch atmSwitch = CrearSwitch(hsm, consoleWriter);

            IATM sut = CrearATM("AJP001", consoleWriter, threadSleeper);
            RegistrarATMEnSwitch(sut, atmSwitch, hsm);

            IAutorizador autorizador = CrearAutorizador("AutDB", hsm);
            RegistrarAutorizadorEnSwitch(autorizador, atmSwitch, hsm);

            string numeroTarjeta = CrearCuentaYTarjeta(autorizador, TipoCuenta.Ahorros, 20_000, "459413", "1234");

            // ACT
            sut.EnviarTransactionRequest("AAA", numeroTarjeta, "1234", 100);

            // ASSERT
            consoleWriter.consoleText.Should().Contain("> Efectivo dispensado: 100");

        }
        //Caso #1: Verificar que el retiro sin balance no es exitoso 
        [Fact]
        public void Withdrawal_with_no_balance_on_account_isnt_successful()
        {
            // ARRANGE
            FakeConsoleWriter consoleWriter = new FakeConsoleWriter();
            FakeThreadSleeper threadSleeper = new FakeThreadSleeper();

            IHSM hsm = new HSM();

            IATMSwitch atmSwitch = CrearSwitch(hsm, consoleWriter);

            IATM sut = CrearATM("AJP001", consoleWriter, threadSleeper);
            RegistrarATMEnSwitch(sut, atmSwitch, hsm);

            IAutorizador autorizador = CrearAutorizador("AutDB", hsm);
            RegistrarAutorizadorEnSwitch(autorizador, atmSwitch, hsm);

            string numeroTarjeta = CrearCuentaYTarjeta(autorizador, TipoCuenta.Ahorros, 0, "459413", "1234");

            // ACT
            sut.EnviarTransactionRequest("AAA", numeroTarjeta, "1234", 0);

            // ASSERT
            consoleWriter.consoleText.Should().Contain("> Efectivo dispensado: 0");

        }
        //Caso #2: Verifica la transaccion
    }
}