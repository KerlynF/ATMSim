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
        //Caso #1: Retiro con balance en la cuenta correctamente
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
        //Caso #2: Retiro cuando la cuenta no exista o este suspendida
        [Fact]
        public void Withdrawal_fails_when_account_does_not_exist()
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
            sut.EnviarTransactionRequest("AAA", "1234567890", "1234", 100);

            // ASSERT
            consoleWriter.consoleText.Should().Contain("> Mostrando pantalla:\n\tLo Sentimos. En este momento no podemos procesar su transacción.\n\t\n\tPor favor intente más tarde...\n> Fin de la Transaccion\n\n\n");
        }
        //Caso #3: Verifica que el retiro con un pin incorrecto no es aceptable
        [Fact]
        public void Withdrawal_fails_when_using_invalid_PIN()
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
            sut.EnviarTransactionRequest("AAA", numeroTarjeta, "0000", 100);

            // ASSERT
            consoleWriter.consoleText.Should().Contain("> Mostrando pantalla:\n\tPin incorrecto\n> Fin de la Transaccion\n\n\n");
        }
        //Caso #4: Retiro sin monto en la cuenta
        [Fact]
        public void Withdrawal_without_monto_is_not_successful()
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
            sut.EnviarTransactionRequest("AAA", numeroTarjeta, "1234", 0);

            // ASSERT
            consoleWriter.consoleText.Should().Contain("Monto inválido");

        }
    }
}