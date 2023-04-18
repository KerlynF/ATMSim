using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ATMSim;
using FluentAssertions;

namespace ATMSimTests
{
    public class TarjetaTests
    {
        [Fact]
        public void create_card_with_an_invalid_number()
        {
            //arrange
            string badNumber = "f121212121212";
            string fakeAccountNumber = "873739739739";
            string failMessage = "Numero de tarjeta inválido"; 
            //act
            Action tarjeta = () => new Tarjeta(badNumber, fakeAccountNumber);
            //assert
            tarjeta.Should().Throw<ArgumentException>().WithMessage(failMessage);
        }

        [Fact]
        public void validate_integrity_with_a_bad_number()
        {
            //arrange
            string badCardNumber = "1223131312312453";
            //act
            bool response = Tarjeta.ValidarIntegridad(badCardNumber);
            //assert
            response.Should().BeFalse();
        }
    }
}