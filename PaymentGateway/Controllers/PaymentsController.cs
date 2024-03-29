﻿using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PaymentGateway.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PaymentGateway.Controllers
{
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {

        public IConfiguration Configuration { get; }

        //connection string for database
        public PaymentsController(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        //Take parameters values and then execute the flow
        [HttpPost]
        public async Task<IActionResult> PaymentGateway([FromQuery] Payment payment)
        {
            string customerBankUrl = "http://localhost:6600/api/customerbank";
            string merchantBankUrl = "http://localhost:6600/api/merchantbank";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                var response = await client.PostAsJsonAsync(new Uri(customerBankUrl), payment);
                var content = await response.Content.ReadAsStringAsync();
                var paymentdetails = JsonConvert.DeserializeObject<Payment>(content);

                if (paymentdetails.Status.Equals("successful"))
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    var merchantBankResponse = await client.PostAsJsonAsync(new Uri(merchantBankUrl), payment);
                    var merchantBankcontent = await response.Content.ReadAsStringAsync();
                    var merchantpaymentdetails = JsonConvert.DeserializeObject<Payment>(content);

                    if (merchantpaymentdetails.Status.Equals("successful"))
                    {
                        DatabaseConnection(merchantpaymentdetails);
                        return Ok("Payment successful && reference = " + merchantpaymentdetails.Reference);
                    }
                    else
                    {
                        return BadRequest("Payment Unsuccesful");
                    }
                }
                else
                    return BadRequest("Payment Unsuccesful");
            }
        }

        //retrieve payment details 
        [HttpGet]
        public IActionResult GetPayment(string reference)
        {
            return Ok(RetrievePayment(reference));
        }

        //set up the database connection
        public void DatabaseConnection(Payment payment)
        {

            string connectionString = Configuration["ConnectionStrings:DefaultConnection"];

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string sql = $"Insert Into payment (cardNumber , date , amount , currency , cvv , reference , status) Values ('{payment.CardNumber}' , '{payment.Date}' , '{payment.Amount}' , '{payment.Currency}' , '{payment.Cvv}'  , '{payment.Reference}'  , '{payment.Status}' )";

                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.CommandType = CommandType.Text;
                    connection.Open();
                    command.ExecuteNonQuery();
                    connection.Close();
                }
            }
        }

        //retrieve payment details 
        public Payment RetrievePayment(string reference)
        {
            string connectionString = Configuration["ConnectionStrings:DefaultConnection"];

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                Payment payment = (connection.Query<Payment>($"Select * From payment Where reference = '{reference}'").FirstOrDefault());

                //mask the card number 
                var str = payment.CardNumber;
                if (str.Length > 4)
                {
                    payment.CardNumber =
                        string.Concat(
                            "".PadLeft(12, 'X'),
                            str.Substring(str.Length - 4)
                        );
                }
                return payment;
            }
        }
    }
}