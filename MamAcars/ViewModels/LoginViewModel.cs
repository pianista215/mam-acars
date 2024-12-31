using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MamAcars.ViewModels
{
    public class LoginViewModel
    {
        public string Username { get; set; }
        public string Password { get; set; }

        public async Task<bool> Login()
        {
            // Implement login logic here
            // Call the REST API using HttpClient and process the response
            // ...

            // Return true if login is successful, false otherwise
            return false; // Placeholder, replace with actual logic
        }
    }
}
