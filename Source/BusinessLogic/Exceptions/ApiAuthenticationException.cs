﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace BusinessLogic.Exceptions
{
    public class ApiAuthenticationException : ApiFriendlyException
    {
        public ApiAuthenticationException(string authTokenHeaderName) : base(string.Format("Invalid {0}", authTokenHeaderName), HttpStatusCode.Unauthorized)
        {
        }
    }
}
