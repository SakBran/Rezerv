using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rezerv.Application.Exceptions
{
    public class BusinessRuleException : Exception
    {
        public int StatusCode { get; }
        public string Code { get; }

        public BusinessRuleException(int statusCode, string code, string message) : base(message)
        {
            StatusCode = statusCode;
            Code = code;
        }

        public static BusinessRuleException NotFound(string code, string msg) => new(404, code, msg);
        public static BusinessRuleException BadRequest(string code, string msg) => new(400, code, msg);
        public static BusinessRuleException Conflict(string code, string msg) => new(409, code, msg);
    }
}