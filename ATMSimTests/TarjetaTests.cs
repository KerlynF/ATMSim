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

        //CASO #1: Prueba que una tarjeta con un número inválido lance una excepción al crearse

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

        //CASO #2:  Prueba que un número de tarjeta inválido falle al ser validado

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

        //CASO #3:Prueba que un número de tarjeta sea enmascarado correctamente
        [Fact]
        public void mask_card_number()
        {
            // Arrange
            string cardNumber = "4532533161442322";
            string expectedMaskedNumber = "453253******2322";
            // Act
            string actualMaskedNumber = Tarjeta.EnmascararNumero(cardNumber);
            // Assert
            actualMaskedNumber.Should().Be(expectedMaskedNumber);
        }


    }
}