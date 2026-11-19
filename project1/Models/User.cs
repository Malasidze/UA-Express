using System;
using System.Collections.Generic;
using System.Text;

namespace project1.Models
{
    internal class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        private string _email;

        public string Email
        {
            get { return _email; }
            set
            {
                if (!value.Contains("@"))
                {
                    Console.WriteLine("Email invalid");
                }
                _email = value;
            }
        }

        public string Password { get; set; }
        public bool IsLogged { get; set; }

        public override string? ToString()
        {
            return base.ToString();
        }
    }
}
