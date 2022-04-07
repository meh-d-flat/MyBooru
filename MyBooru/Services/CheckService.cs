using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooru.Services
{
    public class CheckService : Contracts.ICheckService
    {
        readonly IConfiguration config;

        public CheckService(IConfiguration configuration)
        {
            config = configuration;
        }

        public bool DBSetup()
        {
            throw new NotImplementedException();
        }
    }
}
